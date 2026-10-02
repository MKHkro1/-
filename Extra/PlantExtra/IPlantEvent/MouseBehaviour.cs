using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Il2CppInterop.Runtime.Attributes;
using UnityEngine;

namespace MengxiLib.BepInEx.Extra.PlantExtra.IPlantEvent
{
    public class MouseBehaviour : MonoBehaviour
    {
        public static MouseBehaviour Instance = null!;
        public Mouse mouse = null!;

        public void Awake()
        {
            Instance = this;
            mouse = Mouse.Instance;
        }

        // 下面两个方法的参数里有 TriggerType / MouseClick —— 它们是**纯托管枚举**，
        // 在 IL2CPP 侧根本没有对应类。ClassInjector 在为本类生成 native↔managed 桥接时会去
        // IL2CPP 查这两个枚举的类，拿到的是程序集名 "MengxiLib.BepInEx.dll"，
        // 而模组程序集从来不在 IL2CPP 的程序集表里，于是每次启动都会刷两条
        //   [Error :Il2CppInterop] Assembly MengxiLib.BepInEx.dll is not registered in il2cpp
        // 并且查到的 NativeClassPtr 是 0。
        //
        // 这两个方法本来就只从托管侧调用（没有任何 native 代码会回调它们），
        // 所以用 [HideFromIl2Cpp] 让注入器跳过桥接生成即可：
        // 公开签名与行为完全不变，托管侧调用不受影响，噪音消除。
        [HideFromIl2Cpp]
        public void ProcMouse(TriggerType trigger)
        {
            var plants = Lawnf.GetAllPlants();
            foreach (var val in Enum.GetValues<MouseClick>())
            {
                ProcState(plants, val, trigger);
            }
        }

        [HideFromIl2Cpp]
        public void ProcState(Il2CppSystem.Collections.Generic.List<Plant> plants, MouseClick click, TriggerType trigger)
        {
            if (Input.GetMouseButtonUp((int)click))
                foreach (var p in plants)
                    PlantEvent.MouseEvent(p, mouse, MouseState.Up, click, trigger);
            if (Input.GetMouseButton((int)click))
                foreach (var p in plants)
                    PlantEvent.MouseEvent(p, mouse, MouseState.Hold, click, trigger);
            if (Input.GetMouseButtonDown((int)click))
                foreach (var p in plants)
                    PlantEvent.MouseEvent(p, mouse, MouseState.Down, click, trigger);
        }
    }
}
