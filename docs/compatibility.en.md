# Compatibility and Limitations

[中文](compatibility.zh.md) | **English**

## Verified baseline

| Item | Version / Backend |
|---|---|
| Game | Sprocket 0.2.55.5 |
| Unity | 6000.3.21f1 |
| HDRP | The HDRP version shipped with Unity 6 |
| BepInEx | 6.0.0-be.788 (IL2CPP), net6 |
| Graphics backend | Windows D3D11 |
| Output dimension | Single-slice `Tex2DArray` |

Both `1.0.0` constructors have been verified in Sprocket `0.2.55.5` through the LaserRangefinder host, and the user has confirmed the depth-texture output works in-game.

## Unstable HDRP contracts

The following names and pass indices are HDRP implementation details:

- `_CameraDepthTexture`
- `DepthOfFieldCoC`
- `KMainManual`
- `_DepthMinMaxAvg`
- `_OutputCoCTexture`
- `Hidden/HDRP/Blit`
- pass 20 `FragBilinearRedToRGBA`

After upgrading Unity/HDRP you must re-verify that the resources still exist, whether the kernel/pass indices have changed, and whether the output format is still compatible.

## Current limitations

- Supports white-near/far-black or black-near/far-white; the polarity and range are fixed at construction time;
- The output is a normalized RFloat, not a meter-valued texture;
- Only a single texture-array slice is verified; XR multi-view is not supported;
- The compute shader is looked up by the name of an already-loaded resource; it fails if the resource has not been loaded yet;
- Requires a graphics device that supports RFloat UAVs and compute shaders;
- Must be called at a Custom Pass point where HDRP has already generated the depth pyramid for the current Camera;
- In a multi-Camera setup, the caller must ensure the current global `_CameraDepthTexture` belongs to the target Camera that is executing;
- Does not manage the Camera, Volume, Harmony, or MelonLogger.

## Explicitly forbidden fallback

Do not use `RenderDepthFromCamera` or a second Camera to re-render the scene to fill in depth. Sprocket's Nature Renderer and its special vegetation/water paths have produced severe artifacts and performance problems with that approach.
