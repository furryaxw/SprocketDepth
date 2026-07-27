# SprocketDepth

适用于《Sprocket》IL2CPP 模组的可复用 HDRP 深度图库。它复用当前 Camera 已生成的原生深度，不会使用第二台 Camera 重绘场景。

## 功能

- 将 HDRP `_CameraDepthTexture` 转换为单通道 `RFloat` 归一化线性深度纹理。
- 支持只生成纹理，或将深度结果以近白远黑的灰度图显示到当前画面。
- 自动适配 viewport 尺寸变化，并管理 ComputeShader、Material 和 RenderTexture 的生命周期。
- 提供帧信息和错误信息，方便宿主模组诊断接入问题。

## 输出

`NormalizedDepthTexture` 的数值为：

```text
value = saturate(1 - linearEyeDepthMeters / MaxDistanceMeters)
```

Camera 附近为 `1`，达到配置距离后为 `0`。输出值是归一化线性深度，不是直接以米为单位的距离。

## 接入

在目标 Camera 的 HDRP Custom Pass 中调用：

```csharp
using SprocketDepth;

private readonly HdrpDepthMapRenderer depth = new(300.0f);

bool recorded = depth.TryRecord(context, DepthMapOutput.TextureOnly);
var normalizedDepth = depth.NormalizedDepthTexture;
```

调用者负责选择 Camera、创建 Custom Pass、决定注入时机，并在最后一个 GPU 命令完成后调用 `Dispose()`。

作为其他 MelonLoader 模组的依赖时，将 `SprocketDepth.dll` 放入游戏根目录的 `UserLibs` 文件夹。

## 构建

项目目标框架为 .NET 6，并引用本地 Sprocket MelonLoader/IL2CPP 程序集。默认目录布局为：

```text
G:\Sprocket\
├── MelonLoader\
├── UserLibs\
└── mod\SprocketDepth\
```

```powershell
dotnet build .\SprocketDepth.csproj --configuration Release
```

默认构建会将 DLL 部署到 `UserLibs`。使用 `-p:SkipLibraryDeploy=true` 可以只生成 DLL；仓库位于其他位置时可通过 `-p:SprocketGameRoot="G:\Sprocket"` 指定游戏根目录。

## 文档

- [实现原理](docs/how-it-works.md)
- [IL2CPP / Custom Pass 接入](docs/integration.md)
- [兼容性与限制](docs/compatibility.md)
- [故障排查](docs/troubleshooting.md)

当前验证环境为 Sprocket `0.2.53.1`、Unity `2022.3.62f2`、MelonLoader `0.7.2/net6` 和 Windows D3D11。

## License

[GPL-3.0-only](LICENSE.txt)
