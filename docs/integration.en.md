# IL2CPP / Custom Pass Integration

[中文](integration.zh.md) | **English**

## Recommended injection settings

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

`AfterPostProcess` is the injection point verified so far. It is not a mathematical requirement for depth linearization, but it guarantees that the target color handle and the native depth pyramid are both available in the current game version.

## Why the host still needs a Harmony adapter

HDRP's `FullScreenCustomPass.Execute` is protected. Current IL2CPP mods cannot seamlessly inject a custom implementation through ordinary managed inheritance, so SprocketThermal creates a native `FullScreenCustomPass` and then hands its `CustomPassContext` to the library from a Harmony Prefix.

The library itself does not reference Harmony; that is the host's adaptation responsibility.

## Ownership check

Do not decide ownership by pass name alone. Names can collide with other mods or game objects. Keep the pass you created and compare IL2CPP native pointers:

```csharp
internal bool Owns(FullScreenCustomPass candidate)
{
    return ownedPass != null &&
           candidate != null &&
           candidate.Pointer == ownedPass.Pointer;
}
```

## Prefix example

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
            return true; // Not this module's pass; keep the game's original implementation.

        bool recorded = host.Depth.TryRecord(
            __0,
            DepthMapOutput.TextureAndGrayscaleTarget);
        if (!recorded)
            host.LogOnce(host.Depth.LastError);

        // The commands are already recorded; do not run the default three-vertex FullScreen draw.
        return false;
    }
}
```

## Command ordering with TextureOnly

`TryRecord(..., TextureOnly)` only records the compute dispatch into the CommandBuffer. Obtaining the `NormalizedDepthTexture` reference immediately on the CPU side does not mean the GPU has finished writing it; continuing to record sampling commands in the same CommandBuffer is enough, since GPU ordering guarantees it:

```csharp
if (depth.TryRecord(context, DepthMapOutput.TextureOnly))
{
    RenderTexture texture = depth.NormalizedDepthTexture!;
    context.cmd.SetGlobalTexture(MyDepthId, texture);
    // Later draw/compute work can sample MyDepthId.
}
```

Do not use a CPU `ReadPixels` in the same frame to infer that the GPU contents are already complete. When you need a readback, use an asynchronous GPU readback and handle its lifecycle separately.

## Deployment

Recommended location:

```text
G:\Sprocket0.2.55.5\BepInEx\plugins\SprocketDepth.dll
```

In the host mod project, add:

```xml
<ProjectReference Include="..\SprocketDepth\SprocketDepth.csproj">
  <Private>true</Private>
</ProjectReference>
```

If you only distribute binaries, reference `SprocketDepth.dll` directly and make sure the target machine's game, Unity/HDRP, and IL2CPP dependency versions are compatible.
