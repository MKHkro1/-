using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace MengxiLib.BepInEx.Patch
{
    /// <summary>
    /// 4.0.5 种植链适配层。
    ///
    /// ── 背景（2026-10-01，IDA 实锤 4.0.5 GameAssembly.dll @0x1809DE0C0）──────────────
    /// <c>CreatePlant.SetPlant</c> 在 4.0.5 里新增了「把图鉴数据写进刚种下的植物」这一段，
    /// 位置在「全新种植」分支（<c>targetPlant == null &amp;&amp; hidplant == null</c>）内：
    ///
    /// <code>
    /// PlantData = PlantDataManager.GetPlantData(theSeedType);
    /// if ( !PlantData ) goto LABEL_272;        // ← LABEL_272 = IL2CPP 空引用抛出
    /// plant.attackDamage    = PlantData.attackDamage;
    /// plant.attackInterval  = PlantData.attackInterval;
    /// plant.produceInterval = PlantData.produceInterval;
    /// plant.maxHealth       = PlantData.maxHealth;
    /// </code>
    ///
    /// 也就是说：**任何一个「能被种下去、但在 PlantDataManager 里查不到数据」的 PlantType，
    /// 种下的瞬间就是一次 NullReferenceException**。这正好解释为什么 4.0 上没有这个报错、
    /// 4.0.5 上出现 —— 4.0 的 SetPlant 不读图鉴数据。
    ///
    /// ── 本文件做两件事 ────────────────────────────────────────────────────────
    /// ① <see cref="PlantDataManagerGetPlantDataCompat"/>：**修**
    ///    本库注册过的二创植物，永远保证 <c>GetPlantData</c> 不返回 null（兜底 + 顺手补进原生表）。
    ///    这是语义上就成立的约定：「我注册的植物，我负责它的图鉴数据」。
    ///    对原版植物零影响（只在查到是本库注册的类型时才动手）。
    ///
    /// ② <see cref="SetPlantDiagnosticsPatch"/>：**诊断**
    ///    在进 native 之前把 SetPlant 会解引用的那些外部状态全查一遍，只要有一项是坏的
    ///    就打一行日志（按植物类型去重，不刷屏）。
    ///    为什么需要它：IL2CPP 在 Release 下的栈回溯**只给出一帧且地址全 0**（本次实测），
    ///    所以「异常在 SetPlant 自身还是它调用的 GetPlantData 里」无法从日志区分。
    ///    有了这行日志，下一次复现就能直接指名哪一个引用是空的。
    /// </summary>
    internal static class SetPlant405Compat
    {
        /// <summary>诊断开关。定位完成后改成 false 即可静默（保留调用点，见仓库"静默日志"约定）。</summary>
        internal static bool DiagEnabled = true;

        private static readonly HashSet<string> _reported = [];

        /// <summary>按 key 去重地记一行。同一把 key 只打一次，避免每帧/每次种植刷屏。</summary>
        internal static void ReportOnce(string key, string message)
        {
            if (!DiagEnabled) return;
            if (!_reported.Add(key)) return;
            CustomCore.CLogger.LogWarning($"[4.0.5 种植诊断] {message}");
        }

        /// <summary>把本库的植物数据补进原生表（幂等）。失败只记一次日志，绝不抛。</summary>
        internal static void EnsureNativePlantData(PlantType plantType, PlantDataManager.PlantData data)
        {
            if (data is null) return;
            try
            {
                // 用索引器而不是 Add：Add 在同键已存在时会抛 ArgumentException，
                // 而索引器是幂等的——这里可能会被反复调用。
                PlantDataManager.PlantData_Default[plantType] = data;
            }
            catch (Exception ex)
            {
                ReportOnce($"native:{plantType}", $"写入 PlantData_Default[{plantType}] 失败：{ex.GetType().Name} {ex.Message}");
            }
        }

        /// <summary>取本库登记过的植物数据；不是二创植物就返回 null。</summary>
        internal static PlantDataManager.PlantData? LookupCustomData(PlantType plantType)
        {
            try
            {
                if (CustomCore.CustomPlants.TryGetValue(plantType, out var d) && d.PlantData is not null)
                    return d.PlantData;
            }
            catch (Exception) { /* 字典并发/未初始化都不该影响主流程 */ }
            return null;
        }

        /// <summary>
        /// ★ 正解：把本库全部二创植物的图鉴数据重新写进 <c>PlantData_Default</c>。
        ///
        /// 为什么必须重新写 —— 4.0.5 的 <c>PlantDataManager.Init()</c> @0x18066F230 全文是：
        /// <code>
        /// All = Resources.LoadAll&lt;PlantDataAsset&gt;("...");       // 只从 Resources 读「图鉴资源」
        /// if (!All || All.Length == 0) throw new InvalidOperationException(...);
        /// foreach (asset in All) { ...建 PlantData...; d.Add(asset.PlantType, data); }
        /// PlantData_Default.Clear();        // ★ 清空原生表
        /// PlantData_Modified.Clear();       // ★ 清空修改表
        /// foreach (kv in d) { PlantData_Default.Add(kv); PlantData_Modified.Add(kv 的副本); }
        /// ApplyModify();
        /// </code>
        /// 二创植物**没有 PlantDataAsset 资源**（那是 Unity 序列化资源，插件造不出来），
        /// 所以重建出来的两张表里必然没有它们 —— 前置库在 <c>GameAPP.LoadResources</c> Prefix 里
        /// 写进去的条目会被整批清掉。而 <c>Init()</c> 是在**进关卡时**才跑的，
        /// 于是表现为「启动时注册成功、一进关卡数据就没了」。
        ///
        /// ⇒ 挂在 <c>Init()</c> 的 **Postfix** 上重新登记（而不是靠 GetPlantData 每次兜底）。
        /// 索引器写入、幂等；重复调用只多花一次字典赋值。
        /// </summary>
        internal static void ReRegisterAll()
        {
            int n = 0;
            try
            {
                foreach (var kv in CustomCore.CustomPlants)
                {
                    if (kv.Value.PlantData is null) continue;
                    PlantDataManager.PlantData_Default[kv.Key] = kv.Value.PlantData;
                    n++;
                }
            }
            catch (Exception ex)
            {
                ReportOnce("rereg-ex", $"重新登记梦汐植物图鉴数据时抛出：{ex.GetType().Name} {ex.Message}");
                return;
            }

            // 按"登记条数"去重：条数没变就说明只是 Init 又跑了一次，不必重复刷日志。
            if (n > 0) ReportOnce($"rereg:{n}", $"已把 {n} 个梦汐植物的图鉴数据重新登记进 PlantData_Default（4.0.5 的 PlantDataManager.Init 会清空该表）。");
        }
    }

    /// <summary>
    /// ① 兜底：二创植物的图鉴数据永远不为 null。
    ///
    /// 为什么挂在 <c>GetPlantData</c> 上而不是 <c>SetPlant</c> 上：
    ///   · SetPlant 的解引用在 native 里，托管侧前缀**改不了它的返回值**；
    ///   · GetPlantData 是 native 自己会去调的同一个托管可见方法，在这里兜住，
    ///     所有调用方（含 SetPlant）一起受益；
    ///   · 只在「查到是本库注册的二创植物」时才介入，原版植物走原路径，零影响。
    /// </summary>
    [HarmonyPatch(typeof(PlantDataManager), nameof(PlantDataManager.GetPlantData), [typeof(PlantType)])]
    internal static class PlantDataManagerGetPlantDataCompat
    {
        [HarmonyPostfix]
        private static void Postfix(PlantType plantType, ref PlantDataManager.PlantData __result)
        {
            if (__result is not null) return;

            var data = SetPlant405Compat.LookupCustomData(plantType);
            if (data is null) return;   // 原版植物 / 未注册类型：保持原生行为不变

            __result = data;
            SetPlant405Compat.EnsureNativePlantData(plantType, data);
            SetPlant405Compat.ReportOnce($"fallback:{plantType}",
                $"PlantDataManager 里没有植物 {plantType} 的数据，已用本库注册的数据兜底。" +
                $"若这条在进关卡后才出现，说明原生表在本库注册之后被重建过（4.0.5 新增了 PlantDataManager.Init/ApplyModify）。");
        }
    }

    /// <summary>
    /// ①（正解）4.0.5 的 <c>PlantDataManager.Init()</c> 会清空并重建图鉴表，二创植物必然被丢掉。
    ///
    /// 挂在它的 **Postfix** 上把本库的数据重新登记回去 —— 这才是「让原生表一直是对的」的做法，
    /// 上面 <see cref="PlantDataManagerGetPlantDataCompat"/> 的兜底则退化成「万一还漏了就救一次」的安全网。
    ///
    /// 为什么是 Postfix 而不是 Prefix：<c>Init()</c> 的顺序是
    /// 「读资源 → Clear 两张表 → 用资源重建 → ApplyModify」，
    /// 只有在**全部做完之后**登记，写进去的条目才会留下。
    ///
    /// 幂等性：<c>Init()</c> 可能被反复调用（每次进关卡/读图鉴都可能），
    /// 本补丁每次都用索引器重写同一批键，重复执行只是多几次字典赋值。
    /// </summary>
    [HarmonyPatch(typeof(PlantDataManager), nameof(PlantDataManager.Init))]
    internal static class PlantDataManagerInitCompat
    {
        [HarmonyPostfix]
        private static void Postfix() => SetPlant405Compat.ReRegisterAll();
    }

    /// <summary>
    /// ② 诊断：进 native 之前把 SetPlant 会解引用的外部状态查一遍。
    /// 只在**发现异常项**时打日志，正常路径静默（本仓「成功路径静默、失败路径留痕」约定）。
    /// </summary>
    [HarmonyPatch(typeof(CreatePlant), nameof(CreatePlant.SetPlant))]
    internal static class SetPlantDiagnosticsPatch
    {
        [HarmonyPrefix]
        private static void Prefix(CreatePlant __instance, int newColumn, int newRow, PlantType theSeedType)
        {
            if (!SetPlant405Compat.DiagEnabled) return;

            try
            {
                var bad = new List<string>(4);

                if (__instance is null) bad.Add("__instance==null");
                else if (__instance.board is null) bad.Add("CreatePlant.board==null");

                var rm = GameAPP.resourcesManager;
                if (rm is null) bad.Add("GameAPP.resourcesManager==null");
                else if (rm.plantPrefabs is null) bad.Add("resourcesManager.plantPrefabs==null");
                else if (!rm.plantPrefabs.ContainsKey(theSeedType)) bad.Add("plantPrefabs 缺该 PlantType");

                if (Board.Instance is null) bad.Add("Board.Instance==null");
                else
                {
                    if (Board.Instance.gridSystem is null) bad.Add("Board.gridSystem==null");
                    if (Board.Instance.boardEntity is null) bad.Add("Board.boardEntity==null");
                }

                if (GameAPP.config is null) bad.Add("GameAPP.config==null");

                // ★ 4.0.5 新增的解引用点：native 会直接读它的字段。查不到就是这次 NRE 的头号嫌疑。
                //   两条路径必须分开报，因为修法完全不同：
                //     · 返回 null  → 本库能用兜底补数据救（见 PlantDataManagerGetPlantDataCompat）
                //     · 直接抛异常 → 是 GetPlantData 自己的分支（4.0.5 在 theBoardType==10 且
                //                    PlantData_Modified 为 null 时会走空引用抛出），兜底救不了
                //   注意：这一句本身会触发原生的「植物数据缺失」LogError —— 那正是我们要的证据。
                try
                {
                    if (PlantDataManager.GetPlantData(theSeedType) is null)
                        bad.Add("GetPlantData 返回 null（★ native 会直接解引用它）");
                }
                catch (Exception ex)
                {
                    bad.Add($"GetPlantData 直接抛出 {ex.GetType().Name}（★ 另一条分支，兜底补数据救不了）");
                }

                if (bad.Count > 0)
                {
                    SetPlant405Compat.ReportOnce($"setplant:{theSeedType}",
                        $"theSeedType={theSeedType}({(int)theSeedType}) 列={newColumn} 行={newRow} " +
                        $"theBoardType={GameAPP.theBoardType} 是梦汐植物={CustomCore.CustomPlantTypes.Contains(theSeedType)} " +
                        $"→ 可疑项：{string.Join(" | ", bad)}");
                }
            }
            catch (Exception ex)
            {
                // 诊断代码自己出错绝不能影响种植。Harmony 会把前缀异常记日志并跳过该补丁，
                // 这里再兜一层，保证行为可预期。
                SetPlant405Compat.ReportOnce("diag-ex", $"诊断前缀自身抛出：{ex.GetType().Name} {ex.Message}");
            }
        }
    }
}
