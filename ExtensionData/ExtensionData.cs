using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MengxiLib.BepInEx.ExtensionData.Unity; // ExtensionDataComponent 转发到 DataComponent（见该类注释）
using UnityEngine;

namespace MengxiLib.BepInEx.ExtensionData.Basic
{
    public static class ExtensionData
    {
        public static Dictionary<Type, Dictionary<String, object>> staticData { get; set; } = [];
        public static Dictionary<Type, Dictionary<object, Dictionary<String, object>>> instanceData { get; set; } = [];

        public static object GetData(this Component component, String name)
        {
            if (component == null)
                return null;
            return component.gameObject.GetData(name);
        }
        public static T GetData<T>(this Component component, String name)
        {
            if (component == null)
                return default;
            try
            {
                // 原实现：if (gameObject.GetData(name) == null) return default; return (T)gameObject.GetData(name);
                // 同一个值取两次，而 GetData 内部每次都要 TryGetComponent<ExtensionDataComponent>（原生调用）。
                // 改成取一次。（本方法经 GetCachedComps 进入每帧路径。）
                var data = component.gameObject.GetData(name);
                return data == null ? default : (T)data;
            }
            catch (Exception e)
            {
                CustomCore.CLogger.LogInfo(
                    "Error on convert type (at Get Extension Data), \n" +
                    $"Message   : {e.Message}\n" +
                    $"StackTrace: {e.StackTrace}\n"
                    );
                return default;
            }
        }
        public static void SetData(this Component component, String name, object data)
        {
            if (component == null)
                return;
            component.gameObject.SetData(name, data);
        }

        public static object GetData(this GameObject gameObject, String name)
        {
            if (gameObject == null)
                return null;
            if (gameObject.TryGetComponent<ExtensionDataComponent>(out var edc))
                return edc.GetData(name);
            else
            {
                gameObject.AddComponent<ExtensionDataComponent>();
                return null;
            }
        }
        public static T GetData<T>(this GameObject gameObject, String name)
        {
            if (gameObject == null)
                return default;
            try
            {
                var data = gameObject.GetData(name);
                return data == null ? default : (T)data;
            }
            catch (Exception e)
            {
                CustomCore.CLogger.LogInfo(
                    "Error on convert type (at Get Extension Data), \n" +
                    $"Message   : {e.Message}\n" +
                    $"StackTrace: {e.StackTrace}\n"
                    );
                return default;
            }
        }
        public static void SetData(this GameObject gameObject, String name, object data)
        {
            if (gameObject == null)
                return;
            if (gameObject.TryGetComponent<ExtensionDataComponent>(out var edc))
                edc.SetData(name, data);
            else
            {
                var result = gameObject.AddComponent<ExtensionDataComponent>();
                result.SetData(name, data);
            }
        }

        public static object GetData<T>(String name) => GetData(typeof(T), name);
        public static TData GetData<TClass, TData>(String name) => GetData<TData>(typeof(TClass), name);
        public static object GetData(Type type, String name)
        {
            if (staticData.ContainsKey(type))
                if (staticData[type].ContainsKey(name))
                    return staticData[type][name];
                else
                    return null;
            else
                return null;
        }
        public static TData GetData<TData>(Type type, String name)
        {
            if (staticData.ContainsKey(type))
                if (staticData[type].ContainsKey(name))
                    return (TData)staticData[type][name];
                else
                    return default;
            else
                return default;
        }
        public static void SetData<T>(String name, object data) => SetData(typeof(T), name, data);
        public static void SetData(Type type, String name, object data)
        {
            if (staticData.ContainsKey(type))
                if (staticData[type].ContainsKey(name))
                    staticData[type][name] = data;
                else
                    staticData[type].Add(name, data);
            else
                staticData.Add(type, new Dictionary<String, object>() { { name, data } });
        }

        public static object GetData(this object obj, String name)
        {
            if (obj == null)
                return null;
            if (instanceData.ContainsKey(obj.GetType()))
                if (instanceData[obj.GetType()].ContainsKey(obj))
                    if (instanceData[obj.GetType()][obj].ContainsKey(name))
                        return instanceData[obj.GetType()][obj][name];
            return null;
        }
        public static T GetData<T>(this object obj, String name)
        {
            if (obj == null)
                return default;
            if (instanceData.ContainsKey(obj.GetType()))
                if (instanceData[obj.GetType()].ContainsKey(obj))
                    if (instanceData[obj.GetType()][obj].ContainsKey(name))
                        return (T)instanceData[obj.GetType()][obj][name];
            return default;
        }
        public static void SetData(this object obj, String name, object data)
        {
            if (obj == null)
                return;
            if (instanceData.ContainsKey(obj.GetType()))
                if (instanceData[obj.GetType()].ContainsKey(obj))
                    if (instanceData[obj.GetType()][obj].ContainsKey(name))
                        instanceData[obj.GetType()][obj][name] = data;
                    else
                        instanceData[obj.GetType()][obj].Add(name, data);
                else
                    instanceData[obj.GetType()].Add(obj, new Dictionary<String, object>() { { name, data } });
            else
                instanceData.Add(obj.GetType(), new Dictionary<object, Dictionary<String, object>>() { { obj, new Dictionary<String, object>() { { name, data } } } });
        }

        public static void WriteMethod<T>(this T comp, String name, Action<T> action) where T : Component => comp.SetData(name, action);
        public static void InvokeMethod<T>(this T comp, String name) where T : Component => comp.GetData<Action<T>>(name).Invoke(comp);
        public static void WriteMethod(this GameObject comp, String name, Action<GameObject> action) => comp.SetData(name, action);
        public static void InvokeMethod(this GameObject comp, String name) => comp.GetData<Action<GameObject>>(name).Invoke(comp);
    }

    /// <summary>
    /// 挂在 GameObject 上的扩展数据容器。
    ///
    /// 【为什么它现在只是个转发层】
    /// 本仓库原本有两套并行的"挂在 GameObject 上的 string→object 存储"：
    ///   ① 本类 ExtensionDataComponent（字段 data）
    ///   ② ExtensionDataUnity.DataComponent（字段 datas）+ ExtDataRef&lt;T&gt;
    /// 两者都能被 GetData/SetData 命中，命名也极相似，mod 作者很容易用错其中一套，
    /// 而两套各自还都带重复查找的问题。
    /// 现在把存储统一到 ②（本类只做转发），公开 API 与类名都保留，
    /// 因此外部 mod 无论是调 GetData/SetData 扩展方法、还是直接 GetComponent&lt;ExtensionDataComponent&gt;()，行为都不变。
    /// DataComponent 早已通过 ClassInjector 注册进 IL2CPP（CustomCore 里与本类同一批注册），
    /// 所以这里 GetOrAddComponent 是安全的。
    /// </summary>
    public class ExtensionDataComponent : MonoBehaviour
    {
        private DataComponent Store => gameObject.GetOrAddComponent<DataComponent>()!;

        /// <summary>底层字典的实时视图（保留只读访问，字段名 data 不变）。</summary>
        public Dictionary<String, object> data => Store.datas;

        public object GetData(String name) => Store.GetData(name)!;

        public void SetData(String name, object data) => Store.SetData(name, data);
    }
}
