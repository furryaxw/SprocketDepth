# IL2CPP / Custom Pass 接入

## 推荐注入设置

```csharp
volume.isGlobal = true;
volume.useTargetCamera = true;
volume.targetCamera = camera;
volume.priority = 1000.0f;
volume.injectionPoint = CustomPassInjectionPoint.AfterPostProcess;

pass.targetColorBuffer = CustomPass.TargetBuffer.Camera;
pass.targetDepthBuffer = CustomPass.TargetBuffer.None;
pass.clearFlags = ClearFlag.None;
```

`AfterPostProcess` 是当前验证过的注入点。它不是深度线性化的数学要求，但能确保当前游戏版本的目标颜色句柄和原生深度金字塔都已可用。

## 为什么宿主仍需要 Harmony adapter

HDRP 的 `FullScreenCustomPass.Execute` 是 protected。当前 IL2CPP 模组不能直接用普通托管继承无缝插入自定义实现，因此 SprocketThermal 创建一个原生 `FullScreenCustomPass`，再在 Harmony Prefix 中把它的 `CustomPassContext` 交给库。

库本身不引用 Harmony；这是宿主的适配职责。

## 所有权判断

不要只按 pass 名称判断。名称可能与其他模组或游戏对象冲突。保存自己创建的 pass，并比较 IL2CPP native pointer：

```csharp
internal bool Owns(FullScreenCustomPass candidate)
{
    return ownedPass != null &&
           candidate != null &&
           candidate.Pointer == ownedPass.Pointer;
}
```

## Prefix 示例

```csharp
[HarmonyPatch(typeof(FullScreenCustomPass),
    nameof(FullScreenCustomPass.Execute))]
internal static class DepthPassPatch
{
    private static bool Prefix(
        FullScreenCustomPass __instance,
        CustomPassContext __0)
    {
        DepthHost? host = DepthHost.Instance;
        if (host == null || !host.Owns(__instance))
            return true; // 不是本模块的 pass，保留游戏原实现。

        bool recorded = host.Depth.TryRecord(
            __0,
            DepthMapOutput.TextureAndGrayscaleTarget);
        if (!recorded)
            host.LogOnce(host.Depth.LastError);

        // 已自行记录命令；不要再执行默认三顶点 FullScreen draw。
        return false;
    }
}
```

## TextureOnly 的命令顺序

`TryRecord(..., TextureOnly)` 只是把 compute dispatch 记录到 CommandBuffer。CPU 端立即取得 `NormalizedDepthTexture` 引用，不代表 GPU 已立即写完；在同一个 CommandBuffer 中继续记录采样命令即可由 GPU 顺序保证：

```csharp
if (depth.TryRecord(context, DepthMapOutput.TextureOnly))
{
    RenderTexture texture = depth.NormalizedDepthTexture!;
    context.cmd.SetGlobalTexture(MyDepthId, texture);
    // 后续 draw/compute 可以采样 MyDepthId。
}
```

不要在同一帧立刻用 CPU `ReadPixels` 推断 GPU 内容已经完成。需要读回时使用异步 GPU readback，并单独处理生命周期。

## 部署

推荐位置：

```text
G:\Sprocket\UserLibs\SprocketDepth.dll
```

宿主模组项目添加：

```xml
<ProjectReference Include="..\SprocketDepth\SprocketDepth.csproj">
  <Private>true</Private>
</ProjectReference>
```

若只分发二进制，则直接引用 `SprocketDepth.dll`，并确保目标机器的游戏、Unity/HDRP 和 IL2CPP 依赖版本兼容。
