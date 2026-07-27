# 实现原理

## 数据流

```text
目标 Camera 原生 HDRP 渲染
  → _CameraDepthTexture（packed mip atlas）
  → DepthOfFieldCoC / KMainManual
  → 归一化线性 RFloat Tex2DArray
  ├─ TextureOnly：供后续命令采样
  └─ TextureAndGrayscaleTarget
       → Hidden/HDRP/Blit pass 20
       → 当前 Camera color target
```

库只记录 compute 和 blit 命令，不调用 `Camera.Render`，也不调用 `RenderDepthFromCamera`。地形、Nature Renderer 植被、车辆、水体等遮挡关系来自游戏原生 Camera 已经完成的深度渲染。

## 1. HDRP packed depth atlas

测试分辨率为 `1600x900` 时，HDRP 全局资源曾显示为：

```text
CameraDepthBufferMipChain_1600x1350_R32_SFloat_Tex2DArray_dynamic
```

`1600x1350` 不是当前画面尺寸，而是多个深度 mip 被打包到同一 atlas 的总尺寸。直接把整张 atlas 当普通 2D 图显示，会出现重复缩小画面。库不直接显示 atlas，而是让 HDRP compute 按 viewport 像素读取 mip 0。

## 2. 线性化和归一化

HDRP 的 `DepthOfFieldCoC.compute` 内部调用：

```hlsl
float linearEyeDepth = LinearEyeDepth(depth, _ZBufferParams);
```

库选择 `KMainManual` 并传入：

```text
_Params = (-1, 0, MaxDistanceMeters, 0)
```

对应结果：

```text
nearCoC = 0
farCoC = saturate((linearDepth - range) / (0 - range))
       = saturate(1 - linearDepth / range)
```

以 300 米为例：

| 线性视距 | RFloat 值 | 黑白 |
|---:|---:|---|
| 0 m | 1.0 | 白 |
| 75 m | 0.75 | 浅灰 |
| 150 m | 0.5 | 中灰 |
| 225 m | 0.25 | 深灰 |
| ≥300 m | 0.0 | 黑 |

## 3. 单通道转灰度

compute 输出是 `RFloat`。D3D11 采样单通道纹理时，普通 `float4` Copy 得到的通常是：

```text
(R, 0, 0, 1)
```

所以直接使用 `CustomPassUtils/Copy` 会得到红色深度图。

HDRP 的 `Hidden/HDRP/Blit` pass 20 使用 `FragBilinearRedToRGBA`，源码行为是：

```hlsl
return SAMPLE_TEXTURE2D_X_LOD(...).rrrr;
```

它显式把 R 扩展到 RGBA，因此输出为真正灰度。

## 4. 为什么必须四顶点

pass 20 的 vertex shader 是 `VertQuad`，要求：

```text
MeshTopology.Quads
vertexCount = 4
```

`FullScreenCustomPass` 默认的 `CoreUtils.DrawFullScreen` 使用三顶点 fullscreen triangle。把 pass 20 只设置成 `fullscreenPassMaterial` 而继续走默认三顶点路径，不能满足该 pass 的输入契约，可能表现为完全无变化。

## 5. 资源生命周期

- compute shader 和 presentation material 首次使用时解析并缓存；
- viewport 尺寸不变时复用同一 `RenderTexture`；
- 尺寸变化时释放并重建；
- `Dispose()` 释放材质和纹理；
- 释放必须在 Unity 主线程，并晚于最后一个引用这些资源的已排队命令。
