using System;
using System.IO;
using System.Linq;
using System.Threading;

namespace MengxiLib.BepInEx.Coexist
{
    /// <summary>
    /// 「与别人的前置库同场」检测器。
    ///
    /// ★ 为什么要做这件事（2026-10-01 fork MengxiLib 时定下）
    ///   本库用 Dobby 原生钩子占用三个方法：<c>Plant.Update</c>、<c>Plant.FixedUpdate</c>、
    ///   <c>SavePlantData..ctor</c>。原生钩子改写的是 IL2CPP 方法入口的那段机器码。
    ///   同一个入口被**两个**detour 依次改写时，后一个的 trampoline 会指回前一个，
    ///   而前一个跳回 original 时拿到的又是被改写过的地址 —— 两层 detour 互相指回，
    ///   每帧无限递归，表现为**进关卡第一帧就 Stack overflow 闪退**（本仓有两次实机事故记录）。
    ///   所以「能不能和别人的前置库一起装」不是风格问题，是硬约束。
    ///
    /// ★ 为什么用两条判据而不是只看程序集在不在
    ///   BepInEx 按文件夹顺序加载插件，本库完全可能**先于**对方加载 —— 那一刻
    ///   <see cref="AppDomain"/> 里还没有对方，装原生钩子就晚了，之后再也退不回来。
    ///   因此：
    ///     ① 扫 AppDomain：对方已经加载（常见情形，靠它兜住大多数）；
    ///     ② 扫插件目录：对方就在磁盘上（顺序无关，是决定性判据）。
    ///   ② 宁可"过度降级"也不可"漏判" —— 误判的代价只是少一次 native hook，
    ///   而漏判的代价是必崩。
    ///
    /// ★ 降级之后还剩什么
    ///   除那三个原生钩子外的一切都照常：Harmony 补丁链可叠加、
    ///   <c>GameAPP.resourcesManager.allPlants</c> 的写入有 Contains 去重、
    ///   TypeMgr 集合同步是幂等 Add。也就是"能一起装、能一起玩"。
    ///   唯一的功能损失见 <see cref="CoexistPlantDriver"/> 的说明（存档不写入）。
    /// </summary>
    public static class CoexistMode
    {
        /// <summary>对方的程序集名（原上游前置库）。本库 fork 之后两个程序集可以同时存在于同一个 AppDomain。</summary>
        public const string ForeignAssemblyName = "CustomizeLib.BepInEx";

        /// <summary>
        /// 惰性求值且**只算一次**：这两个判据都要扫 AppDomain / 目录，
        /// 不能放进每帧的热路径里。
        /// </summary>
        private static readonly Lazy<bool> _foreignPresent =
            new(Detect, LazyThreadSafetyMode.ExecutionAndPublication);

        /// <summary>当前是否处于「与对方前置库同场」的降级模式。</summary>
        public static bool ForeignLibPresent => _foreignPresent.Value;

        private static bool Detect()
        {
            // 判据①：对方程序集已在 AppDomain 里
            bool loaded = false;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.GetName().Name == ForeignAssemblyName) { loaded = true; break; }
            }
            if (loaded) return true;

            // 判据②：插件目录里躺着对方的 DLL（与加载顺序无关的决定性判据）
            try
            {
                // ⚠ 必须写 global::：本库命名空间是 MengxiLib.BepInEx，
                //   不加前缀时标识符 BepInEx 会被优先解析成 MengxiLib.BepInEx（CS0234）。
                var dir = global::BepInEx.Paths.PluginPath;
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                {
                    return Directory.EnumerateFiles(dir, ForeignAssemblyName + ".dll", SearchOption.TopDirectoryOnly).Any();
                }
            }
            catch (Exception)
            {
                // 拿不到插件目录不是致命问题，退回「只按判据①」，不要在这里抛。
            }
            return false;
        }

        /// <summary>
        /// 供原生钩子调用点使用的总闸：为 true 时**一个 Dobby 钩子都不许下**。
        /// 每个被拦下的钩子都要留下日志——否则玩家只会看到「功能不生效」，无从排查。
        /// </summary>
        internal static bool AllowNativeHook(string owner)
        {
            if (!ForeignLibPresent) return true;
            CustomCore.CLogger.LogWarning(
                $"[共存模式] 已检测到 {ForeignAssemblyName}，跳过 {owner} 的原生钩子，" +
                $"改由 Board.Update 驱动器调度。存档序列化在共存模式下不可用。");
            return false;
        }
    }
}