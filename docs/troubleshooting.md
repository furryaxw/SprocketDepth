# 故障排查

| 症状 | 高概率原因 | 检查 |
|---|---|---|
| 重复缩小画面、像分形 | 把 packed mip atlas 整张显示 | 不要直接 Copy `_CameraDepthTexture`；使用线性化输出纹理 |
| 红色深度图 | `RFloat` 被当作 `(R,0,0,1)` 输出 | 确认使用 HDRP Blit pass 20 的 `.rrrr` |
| 按开关无视觉变化 | Custom Pass 未命中、默认 draw 仍执行，或 pass 20 用了三顶点 | 检查目标 pass native pointer、Prefix 返回值和四顶点 quad |
| 全屏固定灰色 | Camera target 和注入有效，但 shader 数据链未运行 | 固定 Clear 是控制实验，不是深度输出 |
| 黑色距离极近 | 直接显示 reversed-Z 非线性深度 | 检查 `DepthOfFieldCoC/KMainManual` 是否完成 |
| Shader pass 缺失 | HDRP 版本不同或 shader stripping | 记录 `Hidden/HDRP/Blit` 的 `passCount`，要求大于 20 |
| Compute shader 缺失 | `DepthOfFieldCoC` 尚未加载或版本改名 | 查看 `LastError` 中的 Depth/CoC candidate inventory |
| 只剩 UI / 场景不渲染 | 错误覆盖 Camera target、错误 clear 或异常 pass | 先用固定 Clear 验证 target，再逐段恢复 compute 和 blit |
| 植被、水体、地形遮挡异常 | 使用第二 Camera/重绘场景 | 确认没有调用 `RenderDepthFromCamera` 或 `Camera.Render` |

## 推荐日志

首次成功时记录：

```csharp
Log(depth.LastFrameInfo?.ToString() ?? "<no frame>");
```

失败时记录一次：

```csharp
Log(depth.LastError);
```

不要每帧写日志；这会制造 I/O 和 GC 压力并干扰性能判断。
