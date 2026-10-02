using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Il2CppInterop.Runtime.Attributes;
using Unity.VisualScripting;
using UnityEngine;

namespace MengxiLib.BepInEx.ExtensionData.Unity
{
    #pragma warning disable
    public static class ExtensionDataUnity
    {
        public static ExtDataRef<T> GetOrInitData<T>(this UnityEngine.Object obj, string name, T defaultValue = default(T))
        {
            var data = obj.GetData<T>(name);
            if (data.val == null) obj.SetData(name, defaultValue);
            return data;   // 原实现是 return obj.GetData<T>(name); —— 同一个 (obj, name) 再取一次，纯浪费
        }

        public static ExtDataRef<T> GetData<T>(this UnityEngine.Object obj, string name)
        {
            if (obj is GameObject go) return GetData<T>(go, name);
            if (obj is Component comp) return GetData<T>(comp, name);
            return null;
        }

        public static ExtDataRef<T> GetOrInitData<T>(this GameObject obj, string name, T defaultValue = default(T))
        {
            var data = obj.GetData<T>(name);
            if (data.val == null) obj.SetData(name, defaultValue);
            return data;
        }

        public static ExtDataRef<T> GetData<T>(this GameObject obj, string name)
        {
            // 原来这里有一句 var dataComp = obj.GetOrAddComponent<DataComponent>(); 但 dataComp 从未被使用，
            // 只是一次白付的原生 GetComponent（命中时）。ExtDataRef 的 val getter 自己会取组件，
            // 真正写入时 SetData 也会取，所以这里不需要预取。
            return new ExtDataRef<T>(obj, name);
        }

        public static ExtDataRef<T> GetOrInitData<T>(this Component obj, string name, T defaultValue = default(T))
        {
            var data = obj.GetData<T>(name);
            if (data.val == null) obj.SetData(name, defaultValue);
            return data;
        }

        public static ExtDataRef<T> GetData<T>(this Component obj, string name)
        {
            // 同上：原来那句 dataComp 是死变量。
            return new ExtDataRef<T>(obj, name);
        }

        public static void SetData(this UnityEngine.Object obj, string name, object value)
        {
            if (obj is GameObject go) SetData(go, name, value);
            if (obj is Component comp) SetData(comp, name, value);
        }

        public static void SetData(this GameObject obj, string name, object value)
        {
            var dataComp = obj.GetOrAddComponent<DataComponent>();
            dataComp.SetData(name, value);
        }

        public static void SetData(this Component obj, string name, object value)
        {
            var dataComp = obj.gameObject.GetOrAddComponent<DataComponent>();
            dataComp.SetData(name, value);
        }
    }

    public class DataComponent : MonoBehaviour
    {
        public Dictionary<string, object> datas = new();

        // ★ 跟进上游 CustomizeLib 的安卓兼容处理：给这两个方法加 [HideFromIl2Cpp]。
        //   它们只从托管侧调用（GetData/SetData 扩展方法与 ExtDataRef），IL2CPP 侧没有任何调用者。
        //   不加的话，ClassInjector 会尝试为它们生成 native↔managed 桥接，而 `object` 返回值
        //   在 Il2CppInterop 里不支持 → 启动日志刷
        //     "unsupported return type System.Object"（并跳过注入），属纯噪音但会掩盖真实报错。
        [HideFromIl2Cpp]
        public object? GetData(string name)
        {
            // 原实现是 if (!datas.ContainsKey(name)) datas.Add(name, null); return datas[name];
            // 即"读操作会顺手往字典里插一个 null"。这有两个问题：
            //   1) 一次读取变成 ContainsKey + Add 两次哈希查找；
            //   2) 读操作修改字典，在别的线程遍历时可能抛 InvalidOperationException。
            // 改成 TryGetValue 后可观察行为不变（缺键依旧返回 null），且不再有副作用。
            return datas.TryGetValue(name, out var value) ? value : null;
        }

        [HideFromIl2Cpp]
        public void SetData(string name, object value)
        {
            datas[name] = value;
        }
    }

    public class ExtDataRef<T>
    {
        public T? val
        {
            get
            {
                // 原实现写成 parent.GetOrAddComponent<DataComponent>().GetData(name) == null ? ... : parent.GetOrAddComponent<...>().GetData(name)
                // 两个分支各做一次 GetOrAddComponent，而本 getter 在每帧路径上（ExtDataRef 的隐式转换）会被访问多次，
                // 每次都是两次原生 GetComponent。这里只取一次组件、一次数据。
                var data = parent.GetOrAddComponent<DataComponent>().GetData(name);
                return data == null ? default : (T)data;
            }
            set => parent.GetOrAddComponent<DataComponent>().SetData(name, value);
        }
        public string name = "";
        public UnityEngine.Object? parent = null;

        public ExtDataRef(UnityEngine.Object parent, string name)
        {
            this.parent = parent;
            this.name = name;
        }

        public static implicit operator T(ExtDataRef<T> extDataRef) => extDataRef.val;
    }
}
