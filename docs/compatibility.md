# 兼容性与限制

## 已验证基线

| 项目 | 版本 / 后端 |
|---|---|
| 游戏 | Sprocket 0.2.53.1 / 0.2.53.2 |
| Unity | 2022.3.62f2 |
| HDRP | Unity 2022.3 对应 HDRP 14 系列 |
| MelonLoader | 0.7.2, net6 |
| 图形后端 | Windows D3D11 |
| 输出维度 | 单 slice `Tex2DArray` |

`0.1.2` 的两个构造函数都已在 Sprocket `0.2.53.2` 中通过 LaserRangefinder 与 Thermal 宿主验证，深度图输出已由用户确认游戏内可用。

## 非稳定 HDRP 契约

以下名称和 pass 编号属于 HDRP 实现细节：

- `_CameraDepthTexture`
- `DepthOfFieldCoC`
- `KMainManual`
- `_DepthMinMaxAvg`
- `_OutputCoCTexture`
- `Hidden/HDRP/Blit`
- pass 20 `FragBilinearRedToRGBA`

升级 Unity/HDRP 后必须重新验证资源是否存在、kernel/pass 编号是否变化，以及输出格式是否仍兼容。

## 当前限制

- 支持近白远黑或近黑远白，极性和范围在构造时固定；
- 输出是归一化 RFloat，不是米值纹理；
- 仅验证一个 texture-array slice，不支持 XR 多视图；
- compute shader 通过已加载资源名称查找；若资源尚未加载会失败；
- 依赖支持 RFloat UAV 和 compute shader 的图形设备；
- 必须在 HDRP 已生成当前 Camera 深度金字塔的 Custom Pass 时机调用；
- 多 Camera 环境中，调用者必须保证当前全局 `_CameraDepthTexture` 属于正在执行的目标 Camera；
- 不提供 Camera、Volume、Harmony 或 MelonLogger 管理。

## 明确禁止的回退

不要用 `RenderDepthFromCamera` 或第二台 Camera 重新渲染场景来填充深度。Sprocket 的 Nature Renderer 和特殊植被/水体路径曾因此出现严重异常和性能问题。
