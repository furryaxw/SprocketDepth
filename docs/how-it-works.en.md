# How It Works

[中文](how-it-works.zh.md) | **English**

## Data flow

```text
Target Camera native HDRP render
  → _CameraDepthTexture (packed mip atlas)
  → DepthOfFieldCoC / KMainManual
  → normalized linear RFloat Tex2DArray
  ├─ TextureOnly: for later commands to sample
  └─ TextureAndGrayscaleTarget
       → Hidden/HDRP/Blit pass 20
       → current Camera color target
```

The library only records compute and blit commands; it does not call `Camera.Render` or `RenderDepthFromCamera`. Occlusion relationships for terrain, Nature Renderer vegetation, vehicles, water, and so on come from the depth render the game's native Camera has already completed.

## 1. HDRP packed depth atlas

At a test resolution of `1600x900`, the HDRP global resource showed up as:

```text
CameraDepthBufferMipChain_1600x1350_R32_SFloat_Tex2DArray_dynamic
```

`1600x1350` is not the current screen size but the total size of several depth mips packed into the same atlas. Displaying the whole atlas as an ordinary 2D image produces a repeated, shrinking picture. The library does not display the atlas directly; instead it has the HDRP compute read mip 0 at viewport-pixel granularity.

## 2. Linearization and normalization

HDRP's `DepthOfFieldCoC.compute` calls internally:

```hlsl
float linearEyeDepth = LinearEyeDepth(depth, _ZBufferParams);
```

The library selects `KMainManual`. In white-near mode it passes:

```text
_Params = (-1, 0, MaxDistanceMeters, 0)
```

which yields:

```text
nearCoC = 0
farCoC = saturate((linearDepth - range) / (0 - range))
       = saturate(1 - linearDepth / range)
```

Black-near mode passes `_Params = (-1, 0, 0, MaxDistanceMeters)`, which yields
`saturate(linearDepth / range)`.

Taking 300 meters as an example:

| Linear view distance | RFloat value | Black/white |
|---:|---:|---|
| 0 m | 1.0 | White |
| 75 m | 0.75 | Light gray |
| 150 m | 0.5 | Mid gray |
| 225 m | 0.25 | Dark gray |
| ≥300 m | 0.0 | Black |

## 3. Single-channel to grayscale

The compute output is `RFloat`. When D3D11 samples a single-channel texture, a plain `float4` Copy typically yields:

```text
(R, 0, 0, 1)
```

So using `CustomPassUtils/Copy` directly produces a red depth map.

HDRP's `Hidden/HDRP/Blit` pass 20 uses `FragBilinearRedToRGBA`, whose source behaves as:

```hlsl
return SAMPLE_TEXTURE2D_X_LOD(...).rrrr;
```

It explicitly expands R across RGBA, so the output is true grayscale.

## 4. Why four vertices are required

Pass 20's vertex shader is `VertQuad`, which requires:

```text
MeshTopology.Quads
vertexCount = 4
```

`FullScreenCustomPass`'s default `CoreUtils.DrawFullScreen` uses a three-vertex fullscreen triangle. Setting pass 20 only as `fullscreenPassMaterial` while still taking the default three-vertex path does not satisfy that pass's input contract, and may manifest as no change at all.

## 5. Resource lifecycle

- The compute shader and presentation material are resolved and cached on first use;
- The same `RenderTexture` is reused while the viewport size is unchanged;
- On a size change it is released and recreated;
- `Dispose()` releases the materials and textures;
- Release must happen on the Unity main thread and after the last queued command that references those resources.
