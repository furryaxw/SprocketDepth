using BepInEx;
using BepInEx.Unity.IL2CPP;

namespace SprocketDepth
{
    // 本程序集对外提供的是 HDRP 深度库，没有自己的行为；但 BepInEx 只会为含插件入口的程序集
    // 注册 GUID，而依赖它的模组需要声明硬依赖，所以这里保留一个空插件入口。
    [BepInPlugin(PluginGuid, "Sprocket Depth", "0.1.2")]
    public sealed class SprocketDepthPlugin : BasePlugin
    {
        internal const string PluginGuid = "furryaxw.sprocket-depth";

        public override void Load()
        {
        }
    }
}
