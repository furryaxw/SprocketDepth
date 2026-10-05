# Troubleshooting

[中文](troubleshooting.zh.md) | **English**

| Symptom | Likely cause | Check |
|---|---|---|
| Repeated, shrinking, fractal-like image | Displaying the whole packed mip atlas | Do not Copy `_CameraDepthTexture` directly; use the linearized output texture |
| Red depth map | `RFloat` being output as `(R,0,0,1)` | Confirm you are using the HDRP Blit pass 20 `.rrrr` |
| No visual change when toggling | Custom Pass not hit, default draw still running, or pass 20 using three vertices | Check the target pass native pointer, the Prefix return value, and the four-vertex quad |
| Uniform full-screen gray | Camera target and injection are valid, but the shader data chain is not running | A fixed Clear is a control experiment, not depth output |
| Black at very close range | Displaying reversed-Z nonlinear depth directly | Check whether `DepthOfFieldCoC/KMainManual` completed |
| Shader pass missing | Different HDRP version or shader stripping | Log `Hidden/HDRP/Blit`'s `passCount`; it must be greater than 20 |
| Compute shader missing | `DepthOfFieldCoC` not loaded yet, or renamed in a different version | Check the Depth/CoC candidate inventory in `LastError` |
| Only UI left / scene not rendering | Camera target overwritten incorrectly, wrong clear, or a bad pass | First verify the target with a fixed Clear, then restore compute and blit piece by piece |
| Vegetation, water, terrain occlusion artifacts | Using a second Camera / re-rendering the scene | Confirm `RenderDepthFromCamera` or `Camera.Render` is not being called |

## Recommended logging

Log on first success:

```csharp
Log(depth.LastFrameInfo?.ToString() ?? "<no frame>");
```

Log once on failure:

```csharp
Log(depth.LastError);
```

Do not log every frame; that creates I/O and GC pressure and skews performance assessments.
