using MengxiLib.BepInEx.Utility;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace MengxiLib.BepInEx.Extra.PlantExtra.IPlantEvent
{
    [HarmonyPatch(typeof(Mouse))]
    [HarmonyPriority(Priority.First)] // 数值越大执行顺序越靠后
    public static class MousePatch
    {
        private static ResetSig sig = false;

        [HarmonyPatch(nameof(Mouse.LeftClickWithNothing))]
        [HarmonyPrefix]
        public static bool PreLeftClickWithNothing(Mouse __instance)
        {
            // ★ IZ 点击兼容（跟进上游 CustomizeLib「完善IZ点击兼容」）：
            //   IZ 模式（boardTag.isIZ）下游戏走的是它自己的一套点击判定，
            //   本库再插一层自定义点击事件分发会与之打架（表现：IZ 关卡里点不动/重复响应）。
            //   所以 IZ 模式下原样放行 —— 不拦截、也不派发。
            if (__instance.board != null && __instance.board.boardTag.isIZ)
                return true;

            return PreLeftClickWithNothingCore(__instance);
        }

        // 拆成独立方法并禁止内联：让上面那个「IZ 早退」分支保持极短，
        // 不把整段分发逻辑 JIT 进同一个方法里（上游同款处理）。
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool PreLeftClickWithNothingCore(Mouse instance)
        {
            var block = false;
            var other = false;
            var pos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            foreach (var plant in instance.GetPlantsOnMouse())
            {
                if (plant == null) continue;
                var (res, success) = PlantEvent.OnClicked(plant, instance, other, TriggerType.Pre);
                block |= res;
                other |= success;
            }
            return !block;
        }

        [HarmonyPatch(nameof(Mouse.LeftClickWithNothing))]
        [HarmonyPostfix]
        public static void PostLeftClickWithNothing(Mouse __instance)
        {
            if (__instance.board != null && __instance.board.boardTag.isIZ)
                return;

            PostLeftClickWithNothingCore(__instance);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void PostLeftClickWithNothingCore(Mouse instance)
        {
            var other = false;
            var pos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            foreach (var plant in instance.GetPlantsOnMouse())
            {
                if (plant == null) continue;
                var (_, success) = PlantEvent.OnClicked(plant, instance, other, TriggerType.Post);
                other |= success;
            }
        }

        [HarmonyPatch(nameof(Mouse.LeftClickWithSomeThing))]
        [HarmonyPrefix]
        public static bool PreLeftClickWithSomeThing(Mouse __instance, out Plant? __state)
        {
            var block = false;
            if (__instance.theItemOnMouse != null && __instance.theItemOnMouse.name == "cannon")
            {
                if (__instance.mouseX > -6.5f && __instance.cannonPlant != null)
                {
                    block |= PlantEvent.SetTargetByMouse(__instance.cannonPlant, __instance, TriggerType.Pre);
                    if (PlantEvent.GetCachedCompCount(__instance.cannonPlant!) > 0)
                        if (!block) sig.Set();
                }
            }
            __state = __instance.cannonPlant;
            return !block;
        }

        [HarmonyPatch(nameof(Mouse.LeftClickWithSomeThing))]
        [HarmonyPostfix]
        public static void PostLeftClickWithSomeThing(Mouse __instance, Plant? __state)
        {
            if (__instance.theItemOnMouse != null && __instance.theItemOnMouse.name == "cannon")
            {
                if (__instance.mouseX > -6.5f && __state != null)
                    PlantEvent.SetTargetByMouse(__state, __instance, TriggerType.Post);
                __instance.ClearItemOnMouse(true);
            }
        }

        [HarmonyPatch(nameof(Mouse.ClearItemOnMouse))]
        [HarmonyPrefix]
        public static bool PreClearItemOnMouse()
        {
            if (sig.Reset())
                return false;
            return true;
        }

        [HarmonyPatch(nameof(Mouse.Awake))]
        [HarmonyPrefix]
        public static void PreAwake(Mouse __instance)
        {
            __instance.GetOrAddComponent<MouseBehaviour>()?.mouse = __instance;
        }

        [HarmonyPatch(nameof(Mouse.Update))]
        [HarmonyPrefix]
        public static void PreUpdate()
        {
            MouseBehaviour.Instance.ProcMouse(TriggerType.Pre);
        }

        [HarmonyPatch(nameof(Mouse.Update))]
        [HarmonyPostfix]
        public static void PostUpdate()
        {
            MouseBehaviour.Instance.ProcMouse(TriggerType.Post);
        }
    }
}
