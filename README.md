# SprocketDepth

[中文](README.zh.md) | **English**

A reusable HDRP depth-texture library for Sprocket IL2CPP mods. It reuses the native depth the current Camera has already produced, and never redraws the scene with a second Camera.

## Features

- Converts the HDRP `_CameraDepthTexture` into a single-channel `RFloat` normalized linear depth texture.
- Supports either polarity (white-near/far-black or black-near/far-white), and can either generate the texture only or display it on the current view.
- Adapts automatically to viewport size changes and manages the lifecycle of the ComputeShader, Material, and RenderTexture.
- Exposes frame and error information so host mods can diagnose integration problems.

## Output

The value of `NormalizedDepthTexture` is:

```text
value = saturate(1 - linearEyeDepthMeters / MaxDistanceMeters)
```

The value is `1` near the Camera and `0` at the configured distance. The output is a normalized linear depth, not a distance expressed in meters. Passing `whiteNear: false` at construction switches to
`saturate(linearEyeDepthMeters / MaxDistanceMeters)`.

## Integration

Call it from an HDRP Custom Pass on the target Camera:

```csharp
using SprocketDepth;

private readonly HdrpDepthMapRenderer depth = new(300.0f);

bool recorded = depth.TryRecord(context, DepthMapOutput.TextureOnly);
var normalizedDepth = depth.NormalizedDepthTexture;
```

The caller is responsible for choosing the Camera, creating the Custom Pass, deciding the injection point, and calling `Dispose()` after the last GPU command has completed.

When used as a dependency of another plugin, place `SprocketDepth.dll` in the `BepInEx\plugins` directory.

## Building

The project targets .NET 6 and references the local Sprocket BepInEx/IL2CPP assemblies. The default directory layout is:

```text
G:\Sprocket0.2.55.5\
├── BepInEx\
│   ├── core\
│   └── plugins\
└── mods\SprocketDepth\
```

```powershell
dotnet build .\SprocketDepth.csproj --configuration Release
```

The default build deploys the DLL to `BepInEx\plugins`. Use `-p:SkipLibraryDeploy=true` to only produce the DLL; if the repository lives elsewhere, point at the game root with `-p:SprocketGameRoot="G:\Sprocket0.2.55.5"`.

## Documentation

- [How it works](docs/how-it-works.en.md)
- [IL2CPP / Custom Pass integration](docs/integration.en.md)
- [Compatibility and limitations](docs/compatibility.en.md)
- [Troubleshooting](docs/troubleshooting.en.md)

The currently verified environment is Sprocket `0.2.55.5`, Unity `6000.3.21f1`, BepInEx `6.0.0-be.788` (IL2CPP / net6), and Windows D3D11.

## License

[GPL-3.0-only](LICENSE.txt)
