using BepInEx.Unity.IL2CPP.Hook;
using MengxiLib.BepInEx.Coexist;
using MengxiLib.BepInEx.UnmanagedTools;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace MengxiLib.BepInEx.Hook
{
    public static unsafe class LibNativeHook
    {
        private static List<INativeDetour> Detours { get; set; } = [];

        /// <summary>
        /// 全部原生钩子的**唯一收口**。
        ///
        /// ★ 为什么收口在这里（而不是散在各钩子的 ApplyHook 里逐个加判断）
        ///   同一个 IL2CPP 方法入口被两个 detour 依次改写时，后者的 trampoline 会指回前者，
        ///   而前者跳回 original 时拿到的又是被改写过的地址 —— 两层 detour 互指回，
        ///   每帧无限递归，直接 Stack overflow 闪退（本仓有两次实机事故）。
        ///   只要守住这一个方法，将来新增任何 <c>[ApplyNativeHook]</c> 类型都自动受保护，
        ///   不会因为"忘了在某个新钩子里加判断"而漏掉。
        ///
        /// ★ 为什么不能"改成 Harmony 补丁当替代"
        ///   Harmony 与 Dobby 同样是两层改写入口的 detour，叠加一样互指回。
        ///   被占死的入口只能**换入口**，见 <c>Coexist\CoexistPlantDriver</c>。
        /// </summary>
        public static INativeDetour? CreateAndApply<T>(nint from, T to, out T original) where T : Delegate =>
            CreateAndApply(from, to, out original, "未命名钩子");

        /// <summary>带来源标注的重载：让被拦下的日志能指名道姓，而不是只说"某个钩子被跳过"。</summary>
        public static INativeDetour? CreateAndApply<T>(nint from, T to, out T original, string owner) where T : Delegate
        {
            original = null!;
            if (!CoexistMode.AllowNativeHook(owner))
                return null;

            var detour = INativeDetour.CreateAndApply(from, to, out original);
            Detours.Add(detour);
            return detour;
        }

        /// <summary>
        /// 获取 Il2Cpp 方法结构体指针并初始化类
        /// </summary>
        /// <param name="asmName">程序集名</param>
        /// <param name="namespaze">命名空间</param>
        /// <param name="className">类名</param>
        /// <param name="isGeneric">泛型</param>
        /// <param name="methodName">方法名</param>
        /// <param name="returnTypeName">返回值类型名</param>
        /// <param name="argsTypes">参数列表</param>
        /// <returns>Il2Cpp 方法结构体指针</returns>
        public static IntPtr GetAndInitMethod(string asmName, string namespaze, string className, bool isGeneric, string methodName,
            string returnTypeName, params string[] argsTypes)
        {
            var clz = IL2CPP.GetIl2CppClass(asmName, namespaze, className);
            // ★ 跟进上游 CustomizeLib：这里原来写的是 IL2CPP.il2cpp_init(clz)。
            //   il2cpp_init 是**整个 IL2CPP 运行时的初始化入口**（无参语义），拿类指针去调它是误用；
            //   取方法地址前该做的是「初始化这个类」（跑 static 构造 / 建 vtable 前的类型初始化），
            //   正确 API 是 il2cpp_runtime_class_init。写错的后果：类的类型初始化没做，
            //   钩子装在一个未初始化的入口上，表现为钩子静默不生效或随机崩（上游安卓端还直接崩在 JIT）。
            IL2CPP.il2cpp_runtime_class_init(clz);
            return IL2CPP.GetIl2CppMethod(clz, isGeneric, methodName, returnTypeName, argsTypes);
        }

        /// <summary>
        /// 获取 Il2Cpp 方法指针并初始化类
        /// </summary>
        /// <param name="asmName">程序集名</param>
        /// <param name="namespaze">命名空间</param>
        /// <param name="className">类名</param>
        /// <param name="isGeneric">泛型</param>
        /// <param name="methodName">方法名</param>
        /// <param name="returnTypeName">返回值类型名</param>
        /// <param name="argsTypes">参数列表</param>
        /// <returns>Il2Cpp 方法指针</returns>
        public static IntPtr GetAndInitMethodAddr(string asmName, string namespaze, string className, bool isGeneric, string methodName,
            string returnTypeName, params string[] argsTypes)
        {
            var strc = GetAndInitMethod(asmName, namespaze, className, isGeneric, methodName, returnTypeName, argsTypes);
            return GetMethodAddr(strc);
        }

        /// <summary>
        /// 获取 Il2Cpp 方法指针并初始化类
        /// </summary>
        /// <param name="target">类型</param>
        /// <param name="method">方法</param>
        /// <returns>Il2Cpp 方法指针</returns>
        public static IntPtr GetAndInitMethodAddr(Type target, MethodBase method)
        {
            var argTypes = new List<string>();
            foreach (var parameter in method.GetParameters())
            {
                bool addr = parameter.IsOut || parameter.ParameterType.IsByRef;
                var type = parameter.ParameterType.IsByRef ? parameter.ParameterType.GetElementType() : parameter.ParameterType;
                argTypes.Add(GetTypeName(type!, addr));
            }
            var ret = method is MethodInfo info ? GetTypeName(info.ReturnType) : ClassTools.Void;
            return GetAndInitMethodAddr($"{target.Assembly.GetName().Name}.dll", target.Namespace ?? "", target.Name, method.IsGenericMethod, method.Name,
                ret, [.. argTypes]);
        }


        /// <summary>
        /// 获取 Il2Cpp 方法指针并初始化类
        /// </summary>
        /// <param name="target">类型</param>
        /// <param name="name">方法名</param>
        /// <returns>Il2Cpp 方法指针</returns>
        public static IntPtr GetAndInitMethodAddr(Type target, string name) =>
            GetAndInitMethodAddr(target, target.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)!);

        /// <summary>
        /// 获取类型在 Il2Cpp 中的名称
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="addr">是否添加取址(ref / out 参数)</param>
        /// <returns>类型名</returns>
        public static string GetTypeName<T>(bool addr = false) => IL2CPP.RenderTypeName<T>(addr);

        /// <summary>
        /// 获取类型在 Il2Cpp 中的名称
        /// </summary>
        /// <param name="t">类型</param>
        /// <param name="addr">是否添加取址(ref / out 参数)</param>
        /// <returns>类型名</returns>
        public static string GetTypeName(Type t, bool addr = false) => IL2CPP.RenderTypeName(t, addr);

        /// <summary>
        /// 获取 Il2Cpp 方法地址
        /// </summary>
        /// <param name="methodInfo">Il2Cpp 方法结构体指针</param>
        /// <returns>方法地址</returns>
        public static IntPtr GetMethodAddr(Il2CppMethodInfo* methodInfo) => UnityVersionHandler.Wrap(methodInfo).MethodPointer;

        /// <summary>
        /// 获取 Il2Cpp 方法地址
        /// </summary>
        /// <param name="methodInfo">Il2Cpp 方法结构体指针</param>
        /// <returns>方法地址</returns>
        public static IntPtr GetMethodAddr(IntPtr methodInfo) => GetMethodAddr((Il2CppMethodInfo*)methodInfo);
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class ApplyNativeHookAttribute(string targetMethod = "ApplyHook") : Attribute
    {
        public string TargetMethod = targetMethod;
    }

    internal static class ApplyNativeHookTools
    {
        public static void RunAll()
        {
            foreach (var type in SystemTools.GetAllTypes())
            {
                foreach (var attr in type.GetCustomAttributes<ApplyNativeHookAttribute>())
                {
                    var method = type.GetMethod(attr.TargetMethod, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                    if (method == null)
                    {
                        CustomCore.CLogger.LogError($"Not found method {attr.TargetMethod} on type {type}");
                        continue;
                    }
                    method.Invoke(null, []);
                }
            }
        }
    }
}
