using BepInEx.Unity.IL2CPP.Utils.Collections;
using Il2CppInterop.Runtime.Injection;
using Il2CppInterop.Runtime.InteropTypes;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace MengxiLib.BepInEx
{
    public class InterfaceBehaviour : MonoBehaviour { }

    /// <summary>
    /// 自定义DamageMaker类
    /// </summary>
    public class CustomDamageMaker : IDamageMaker
    {
        private static Dictionary<Team, InterfaceBehaviour> DamageMakerObjs = new Dictionary<Team, InterfaceBehaviour>();
        public static IDamageMaker DamageMaker
        {
            get => GetDamageMaker();
        }

        // 实现基础方法
        #region 基础方法
        public CustomDamageMaker(IntPtr ptr) : base(ptr) { }
        public Team Team { get; set; }
        public override bool CanAttack(IDamageable target)
        {
            // 实现你的攻击判定逻辑
            return target != null && target.Team != Team;
        }
        #endregion

        public static IDamageMaker GetDamageMaker() => InterfaceCreator.GetInterfaceInstance<CustomDamageMaker>();
    }

    public static class InterfaceCreator
    {
        private static InterfaceBehaviour? Behaviour;

        internal static void InitInstance()
        {
            var go = new GameObject("InterfaceBehaviour");
            UnityEngine.Object.DontDestroyOnLoad(go);
            Behaviour = go.AddComponent<InterfaceBehaviour>();
        }

        public static T GetInterfaceInstance<T>() where T : Il2CppObjectBase
        {
            // 获取IntPtr的构造方法
            var cons = typeof(T).GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, [typeof(IntPtr)]);
            if (cons == null) throw new ArgumentException("Cannot find a constructor with a parameter list of IntPtr");
            return (T)cons.Invoke([Behaviour?.gameObject.Pointer]);
        }
    }
}
