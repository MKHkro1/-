using HarmonyLib;
using MengxiLib.BepInEx.Extra.PlantExtra.IPlantEvent;
using System;
using UnityEngine;
// ⚠ 不写 `using Il2CppSystem.Collections.Generic;`：本工程 ImplicitUsings 已引入
//   System.Collections.Generic，两者会让 List<> 变成 CS0104 歧义。Il2Cpp 集合一律写全限定名。

namespace MengxiLib.BepInEx.Coexist
{
    /// <summary>
    /// 共存模式下的 <c>IPlantEvent</c> 驱动器（<c>Board.Update</c> 后缀）。
    ///
    /// ★ 为什么需要它
    ///   独占模式下，<c>PlantEvent</c> 的每帧回调是靠 Dobby 原生钩子挂在
    ///   <c>Plant.Update</c> / <c>Plant.FixedUpdate</c> 上驱动的（见 PlantPatch.cs 的
    ///   <c>PlantUpdateHook</c> / <c>PlantFixedUpdateHook</c>）。这些入口被对方的
    ///   前置库同样用原生钩子占用，双钩必然互指回递归爆栈，所以共存模式下**不能再挂**。
    ///   换一个没人占的入口来驱动同一套分发逻辑即可。
    ///
    /// ★ 为什么选 <c>Board.Update</c>
    ///   · 它**不在**对方前置库的原生钩子占用清单里（那三个是 Plant.Update /
    ///     Plant.FixedUpdate / SavePlantData..ctor）；
    ///   · 本仓已有 40+ 个插件验证过「Harmony 挂 Board.Update + 遍历 Lawnf.GetAllPlants()
    ///     驱动自家每帧逻辑」这条路在 IL2CPP 下是稳的；
    ///   · Harmony 的补丁链对同一方法可叠加 —— 就算对方也挂了 Board.Update，
    ///     双方是**依次执行**而不是互相替换，不会互相破坏。
    ///
    /// ★ 与原生驱动器的语义差别（共存模式的已知代价，共 3 条，逐条说清）
    ///   ① Pre/Post 的位置：原生版是在**该植物自己的 Update 前后**各调一次；
    ///      本驱动版是在 Board.Update 里把 Pre/Post **连着**调两次。
    ///      影响：回调里若依赖"与植物原生 Update 的先后顺序"，在共存模式下次序会变。
    ///      绝大多数 IPlantEvent 只做每帧逻辑，两者等价。
    ///   ② FixedUpdate：<c>Board</c> 上只有 <c>Update</c>，没有 FixedUpdate/OnFixedUpdate
    ///      （Cecil 实锤 4.0.5 interop）。所以用 <see cref="Time.fixedDeltaTime"/> 做累加器，
    ///      按物理帧的节拍近似地补调 <c>PlantEvent.OnFixedUpdate</c>。
    ///      影响：节拍是近似的，帧率波动时与真实物理帧不严格对齐。
    ///   ③ ★ 存档：<c>SavePlantData</c> 的构造函数同样被对方占用、共存模式下无法挂钩，
    ///      所以**自家植物的存档数据不会写入**。游戏能正常玩，重开后植物状态丢失。
    ///      这是有意接受的取舍（见 CoexistMode.AllowNativeHook 里的告警日志）。
    ///
    /// ★ 独占模式下本类的开销 ≈ 0
    ///   补丁本身会被注册，但方法体第一句就是一次 static bool 读取；
    ///   而 <see cref="CoexistMode.ForeignLibPresent"/> 只在第一次访问时扫一次目录，
    ///   之后是缓存值。
    /// </summary>
    [HarmonyPatch(typeof(Board), "Update")]
    internal static class CoexistPlantDriver
    {
        /// <summary>
        /// FixedUpdate 补调的累加器。
        /// 用 NaN 作"还没初始化"的哨兵：<c>float.NaN</c> 与任何数的比较恒为 false，
        /// 所以必须显式判 <see cref="float.IsNaN"/> 才能走到初始化分支。
        /// </summary>
        private static float _fixedAccumulator = float.NaN;

        [HarmonyPostfix]
        private static void Postfix()
        {
            if (!CoexistMode.ForeignLibPresent) return;

            // 局外/暂停态一律不派发：与 PlantEvent 内部的 InGame 门槛保持一致，
            // 免得在主菜单里对着一堆预览体做无用功。
            if (GameAPP.theGameStatus != GameStatus.InGame) return;

            Il2CppSystem.Collections.Generic.List<Plant> plants;
            try
            {
                // 注意：返回的是 Il2CppSystem 的 List，不是托管 List。
                // 本仓的既定写法是「取 Count + 下标 for」，不要改成 foreach。
                plants = Lawnf.GetAllPlants();
            }
            catch (Exception)
            {
                // Lawnf.GetAllPlants 在 Board / boardEntity / plantArray 为 null 时会抛。
                // 换关瞬间确实有这个窗口，抛出去只是让本帧不派发，不该刷屏。
                return;
            }
            if (plants == null || plants.Count == 0) return;

            // ★ 必须每帧只推进一次，不能放进下面的 for 循环里 ——
            //   放进去会让累加器按"每株植物一次"的速度涨，植物一多节拍就完全失真。
            bool tickFixed = ShouldTickFixed();

            for (int i = 0; i < plants.Count; i++)
            {
                Plant plant = plants[i];
                if (plant == null) continue;
                try
                {
                    // 没注册 IPlantEvent 的植物直接跳过：这是每帧的快路径，
                    // 全库绝大多数植物都走不进下面的四次派发。
                    if (!PlantEvent.HasEventComp(plant)) continue;

                    PlantEvent.OnUpdate(plant, TriggerType.Pre);
                    PlantEvent.OnUpdate(plant, TriggerType.Post);

                    if (tickFixed)
                    {
                        PlantEvent.OnFixedUpdate(plant, plant, TriggerType.Pre);
                        PlantEvent.OnFixedUpdate(plant, plant, TriggerType.Post);
                    }
                }
                catch (Exception ex)
                {
                    // 单株植物的回调异常绝不能中断整轮遍历。
                    // PlantEvent 内部已对每个 comp 单独 try 了，这里兜的是它之外的东西。
                    CustomCore.CLogger.LogError($"[共存模式] 驱动植物 {plant.thePlantType} 的每帧回调时抛出异常：{ex}");
                }
            }
        }

        /// <summary>
        /// 判断本帧是否该补一次 FixedUpdate。
        /// 累加 <see cref="Time.deltaTime"/>，够一个 <see cref="Time.fixedDeltaTime"/> 就触发并清零。
        /// </summary>
        private static bool ShouldTickFixed()
        {
            float step = Time.fixedDeltaTime;
            if (step <= 0f) return false;

            if (float.IsNaN(_fixedAccumulator))
            {
                // 首次：只对齐起点，本帧不补调，避免进共存模式的第一帧连补好几帧。
                _fixedAccumulator = 0f;
                return false;
            }

            _fixedAccumulator += Time.deltaTime;
            if (_fixedAccumulator < step) return false;

            _fixedAccumulator = 0f;
            return true;
        }
    }
}