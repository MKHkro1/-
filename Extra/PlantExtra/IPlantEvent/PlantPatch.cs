using MengxiLib.BepInEx.Hook;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace MengxiLib.BepInEx.Extra.PlantExtra.IPlantEvent
{
    #region HarmonyPatch
    public static class PlantPatches
    {
        private const string UPDATE = "Plant_Update";
        private const string FIXEDUPDATE = "Plant_FixedUpdate";

        // 神秘il2cpp，只能有一个调用的async方法，多了就崩
        // 万能方法
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static async Task LocalMethod(Plant plant, TriggerType trigger, string callData)
        {
            switch (callData)
            {
                case UPDATE:
                    {
                        if (plant != null && PlantEvent.HasEventComp(plant))
                            PlantEvent.OnUpdate(plant, trigger);
                    }
                    break;
                case FIXEDUPDATE:
                    {
                        if (plant != null && PlantEvent.HasEventComp(plant))
                            PlantEvent.OnFixedUpdate(plant, plant, trigger);
                    }
                    break;
            }
        }

        [HarmonyPatch]
        [HarmonyPriority(Priority.First)] // 数值越大执行顺序越靠后
        public static class PlantDiePatch
        {
            [HarmonyTargetMethods]
            public static IEnumerable<MethodBase> GetTargetMethods()
            {
                return SystemTools.GetAllMethods(SystemTools.GetAllDerivedTypes(typeof(Plant)), nameof(Plant.Die),
                    BindingFlags.Default.AddAllAccess().AddInstance().AddDeclaredOnly());
            }

            [HarmonyPrefix]
            public static void PreDie(Plant __instance, Plant.DieReason __0)
            {
                if (__instance != null && PlantEvent.HasEventComp(__instance))
                    PlantEvent.DieEvent(__instance, __0, TriggerType.Pre);
            }

            [HarmonyPostfix]
            public static void PostDie(Plant __instance, Plant.DieReason __0)
            {
                if (__instance != null && PlantEvent.HasEventComp(__instance))
                    PlantEvent.DieEvent(__instance, __0, TriggerType.Post);
            }
        }

        [HarmonyPatch]
        [HarmonyPriority(Priority.First)] // 数值越大执行顺序越靠后
        public static class Plant_PlantUpdatePatch
        {
            [HarmonyTargetMethods]
            public static IEnumerable<MethodBase> GetTargetMethods()
            {
                return SystemTools.GetAllMethods(SystemTools.GetAllDerivedTypes(typeof(Plant)), nameof(Plant.PlantUpdate),
                    BindingFlags.Default.AddAllAccess().AddInstance().AddDeclaredOnly());
            }

            [HarmonyPrefix]
            public static void PrePlantUpdate(Plant __instance, ref bool __state)
            {
                if (__instance != null && PlantEvent.HasEventComp(__instance))
                {
                    // OnUpdate
                    // _ = PlantEvent.Resolvers.PlantResolver.PreUpdate.Update(__instance);
                    //_ = PlantEvent.Resolvers.Run(() =>
                    //{
                    //    if (__instance != null && PlantEvent.HasEventComp(__instance))
                    //        PlantEvent.OnUpdate(__instance, TriggerType.Pre);
                    //});
                    // AttributeEvent
                    if (__instance.attributeCountdown > 0f && __instance.attributeCountdown - Time.deltaTime * __instance.attributeSpeed <= 0f)
                    {
                        __state = true;
                        PlantEvent.AttributeEvent(__instance, TriggerType.Pre);
                    }
                }
            }

            [HarmonyPostfix]
            public static void PostPlantUpdate(Plant __instance, bool __state)
            {
                if (__instance != null && PlantEvent.HasEventComp(__instance))
                {
                    // OnUpdate
                    // _ = PlantEvent.Resolvers.PlantResolver.PostUpdate.Update(__instance);
                    //_ = PlantEvent.Resolvers.Run(() =>
                    //{
                    //    if (__instance != null && PlantEvent.HasEventComp(__instance))
                    //        PlantEvent.OnUpdate(__instance, TriggerType.Post);
                    //});
                    // AttributeEvent
                    if (__state)
                    {
                        PlantEvent.AttributeEvent(__instance, TriggerType.Post);
                    }
                }
            }
        }

        //[HarmonyPatch]
        //[HarmonyPriority(Priority.First)] // 数值越大执行顺序越靠后
        //public static class Plant_UpdatePatch
        //{
        //    [HarmonyTargetMethods]
        //    public static IEnumerable<MethodBase> GetTargetMethods()
        //    {
        //        return SystemTools.GetAllMethods(SystemTools.GetAllDerivedTypes<Plant>(), nameof(Plant.Update),
        //            BindingFlags.Default.AddAllAccess().AddInstance().AddDeclaredOnly());
        //    }

        //    [HarmonyPrefix]
        //    public static void PreUpdate(Plant __instance)
        //    {
        //        _ = LocalMethod(__instance, TriggerType.Pre, UPDATE);
        //    }

        //    [HarmonyPostfix]
        //    public static void PostUpdate(Plant __instance)
        //    {
        //        _ = LocalMethod(__instance, TriggerType.Post, UPDATE);
        //    }
        //}

        //[HarmonyPatch]
        //[HarmonyPriority(Priority.First)] // 数值越大执行顺序越靠后
        //public static class Plant_FixedUpdatePatch
        //{
        //    [HarmonyTargetMethods]
        //    public static IEnumerable<MethodBase> GetTargetMethods()
        //    {
        //        return SystemTools.GetAllMethods(SystemTools.GetAllDerivedTypes<Plant>(), nameof(Plant.FixedUpdate),
        //            BindingFlags.Default.AddAllAccess().AddInstance().AddDeclaredOnly());
        //    }

        //    [HarmonyPrefix]
        //    public static void PreFixedUpdate(Plant __instance)
        //    {
        //        _ = LocalMethod(__instance, TriggerType.Pre, FIXEDUPDATE);
        //    }

        //    [HarmonyPostfix]
        //    public static void PostFixedUpdate(Plant __instance)
        //    {
        //        _ = LocalMethod(__instance, TriggerType.Post, FIXEDUPDATE);
        //    }
        //}
    }
    #endregion

    #region NativeHook
    [ApplyNativeHook]
    public class PlantUpdateHook
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void PlantUpdate(IntPtr @this, IntPtr method);

        private static PlantUpdate Original = null!;

        public static void ApplyHook()
        {
            LibNativeHook.CreateAndApply(LibNativeHook.GetAndInitMethodAddr(typeof(Plant), "Update"), OnPlantUpdate, out Original, "Plant.Update（每帧回调驱动）");
        }

        public static void OnPlantUpdate(IntPtr @this, IntPtr method)
        {
            // new 出来的包装对象必然非 null，原来那个 bool notNull = plant != null 是恒真的死判断。
            // （顺带核实：Plant -> Entity -> MonoBehaviour 整条继承链都没有 op_Equality/op_Inequality，
            //   所以 != null 只是普通引用比较，并不昂贵；真正省下的是这个无意义的中转变量。）
            var plant = new Plant(@this);
            try
            {
                PlantEvent.OnUpdate(plant, TriggerType.Pre);
            }
            catch (Exception ex)
            {
                // 必须隔离：原实现没有 try/catch，用户写在 IPlantEvent.OnUpdate 里的异常一旦抛出，
                // 下面的 Original.Invoke 就不会执行 —— 该植物这一帧的原生 Update 被整段跳过，
                // 且异常会穿过 native 帧返回 IL2CPP。这里保证原生逻辑一定跑。
                CustomCore.CLogger.LogError($"[IPlantEvent] OnUpdate(Pre) 抛出异常：{ex}");
            }
            Original.Invoke(@this, method);
            try
            {
                PlantEvent.OnUpdate(plant, TriggerType.Post);
            }
            catch (Exception ex)
            {
                CustomCore.CLogger.LogError($"[IPlantEvent] OnUpdate(Post) 抛出异常：{ex}");
            }
        }
    }

    [ApplyNativeHook]
    public class PlantFixedUpdateHook
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void PlantFixedUpdate(IntPtr @this, IntPtr method);

        private static PlantFixedUpdate Original = null!;

        public static void ApplyHook()
        {
            LibNativeHook.CreateAndApply(LibNativeHook.GetAndInitMethodAddr(typeof(Plant), nameof(Plant.FixedUpdate)), OnPlantFixedUpdate, out Original, "Plant.FixedUpdate（物理帧回调驱动）");
        }

        public static void OnPlantFixedUpdate(IntPtr @this, IntPtr method)
        {
            var plant = new Plant(@this);
            try
            {
                PlantEvent.OnFixedUpdate(plant, plant, TriggerType.Pre);
            }
            catch (Exception ex)
            {
                CustomCore.CLogger.LogError($"[IPlantEvent] OnFixedUpdate(Pre) 抛出异常：{ex}");
            }
            Original.Invoke(@this, method);
            try
            {
                PlantEvent.OnFixedUpdate(plant, plant, TriggerType.Post);
            }
            catch (Exception ex)
            {
                CustomCore.CLogger.LogError($"[IPlantEvent] OnFixedUpdate(Post) 抛出异常：{ex}");
            }
        }
    }
    #endregion
}
