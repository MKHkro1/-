using MengxiLib.BepInEx.Hook;
using MengxiLib.BepInEx.Utility;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace MengxiLib.BepInEx.Extra.PlantExtra.IPlantEvent
{
    [HarmonyPatch(typeof(SavePlantData))]
    [HarmonyPriority(Priority.First)] // 数值越大执行顺序越靠后
    public static class SavePlantDataPatch
    {
        [HarmonyPatch(nameof(SavePlantData.LoadData))]
        [HarmonyPrefix]
        public static void PreLoadData(SavePlantData __instance, ref Plant plant)
        {
            PlantEvent.AfterDeserialized(plant, __instance, TriggerType.Pre);
        }

        [HarmonyPatch(nameof(SavePlantData.LoadData))]
        [HarmonyPostfix]
        public static void PostLoadData(SavePlantData __instance, ref Plant plant)
        {
            PlantEvent.AfterDeserialized(plant, __instance, TriggerType.Post);
        }
    }

    [ApplyNativeHook]
    public class SavePlantDataConstructorHook
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void SavePlantDataConstructor(IntPtr @this, IntPtr plant, IntPtr method);

        private static SavePlantDataConstructor Original = null!;

        public static void ApplyHook()
        {
            // ⚠ 这个入口没有降级替代：共存模式下本库模组的植物存档不会写入（见 Coexist\CoexistPlantDriver 说明第三条）。
            LibNativeHook.CreateAndApply(LibNativeHook.GetAndInitMethodAddr(typeof(SavePlantData), typeof(SavePlantData).GetConstructor([typeof(Plant)])!), OnSavePlantDataConstructor, out Original, "SavePlantData..ctor（存档序列化）");
        }

        public static void OnSavePlantDataConstructor(IntPtr @this, IntPtr p, IntPtr method)
        {
            var plant = new Plant(p);
            var data = new SavePlantData(@this);
            PlantEvent.BeforeSerialized(plant!, data, TriggerType.Pre);
            Original.Invoke(@this, p, method);
            PlantEvent.BeforeSerialized(plant!, data, TriggerType.Post);
        }
    }
}
