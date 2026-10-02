using MengxiLib.BepInEx;
using MengxiLib.BepInEx.ExtensionData.Unity;
using MengxiLib.BepInEx.Extra.Attributes;
using Cysharp.Threading.Tasks;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Injection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;

namespace MengxiLib.BepInEx.Extra.PlantExtra.IPlantEvent
{
    /// <summary>
    /// 植物事件
    /// </summary>
    /// <remarks>
    /// <para>所有逻辑均依赖于存储在当前对象上的缓存数据, 如果缓存中没有此组件, 则此组件的方法将不会执行, 在初始化时调用 <see cref="PlantEvent.AddToList(IPlantEvent)"/> (扩展方法) 来更新缓存</para>
    /// <para>所有方法均会被分别由触发时机为 Pre 和 Post 两个地方调用, 如果方法只需执行一次, 请为需要的方法以特性 <see cref="TriggerOnceAttribute"/> 标记</para>
    /// </remarks>
    public interface IPlantEvent
    {
        public Component? Component
        {
            get
            {
                if (this is Component comp) return comp;
                return null;
            }
        }

        /// <summary>
        /// 当植物死亡时
        /// </summary>
        /// <param name="reason">死亡原因</param>
        /// <param name="trigger">调用时机</param>
        public void DieEvent(Plant.DieReason reason, TriggerType trigger) { }

        /// <summary>
        /// 当植物死亡时
        /// </summary>
        /// <remarks>当植物死亡时，此方法一定被调用</remarks>
        /// <param name="reason">死亡原因</param>
        /// <param name="trigger">调用时机</param>
        public void DieEventMustExecute(Plant.DieReason reason, TriggerType trigger) { }

        /// <summary>
        /// 当鼠标点击植物时
        /// </summary>
        /// <remarks>若已有其他植物成功触发则不会调用</remarks>
        /// <param name="mouse">鼠标实例</param>
        /// <param name="trigger">调用时机</param>
        /// <returns>是否成功触发</returns>
        public bool OnClicked(Mouse mouse, TriggerType trigger) => false;

        /// <summary>
        /// 当鼠标点击植物时
        /// </summary>
        /// <remarks>无论是否有其他植物成功触发, 总是调用</remarks>
        /// <param name="mouse">鼠标实例</param>
        /// <param name="processOther">是否已经有其他植物成功触发</param>
        /// <param name="trigger">调用时机</param>
        /// <returns>点击数据</returns>
        public OnClickData OnClickedConfig(Mouse mouse, bool processOther, TriggerType trigger) => new();

        /// <summary>
        /// 当鼠标点击时
        /// </summary>
        /// <param name="mouse">鼠标实例</param>
        /// <param name="state">鼠标状态</param>
        /// <param name="click">点击类型</param>
        /// <param name="trigger">调用时机</param>
        public void MouseEvent(Mouse mouse, MouseState state, MouseClick click, TriggerType trigger) { }

        /// <summary>
        /// 当鼠标点击时
        /// </summary>
        /// <remarks>当鼠标点击时, 此方法一定被调用</remarks>
        /// <param name="mouse">鼠标实例</param>
        /// <param name="state">鼠标状态</param>
        /// <param name="click">点击类型</param>
        /// <param name="trigger">调用时机</param>
        public void MouseEventMustExecute(Mouse mouse, MouseState state, MouseClick click, TriggerType trigger) { }

        /// <summary>
        /// 当鼠标上物品名为 cannon 且按下左键时调用
        /// </summary>
        /// <remarks>默认阻止原方法执行</remarks>
        /// <param name="mouse">鼠标实例</param>
        /// <param name="trigger">调用时机</param>
        public void SetTargetByMouse(Mouse mouse, TriggerType trigger) { }

        /// <summary>
        /// 当鼠标上物品名为 cannon 且按下左键时调用
        /// </summary>
        /// <param name="mouse">鼠标实例</param>
        /// <param name="trigger">调用时机</param>
        /// <returns>阻止原方法执行</returns>
        public bool SetTargetByMouseBool(Mouse mouse, TriggerType trigger) => true;

        /// <summary>
        /// 每帧调用
        /// </summary>
        /// <remarks>仅当 <see cref="GameAPP.theGameStatus"/> 为 <see cref="GameStatus.InGame"/> 时调用</remarks>
        /// <param name="trigger">调用时机</param>
        public void OnUpdate(TriggerType trigger) { }

        /// <summary>
        /// 每帧调用
        /// </summary>
        /// <remarks>无 <see cref="GameAPP.theGameStatus"/> 限制</remarks>
        /// <param name="trigger">调用时机</param>
        public void OnUpdateMustExecute(TriggerType trigger) { }

        /// <summary>
        /// 每固定秒调用
        /// </summary>
        /// <remarks>仅当 <see cref="GameAPP.theGameStatus"/> 为 <see cref="GameStatus.InGame"/> 时调用</remarks>
        /// <param name="trigger">调用时机</param>
        public void OnFixedUpdate(TriggerType trigger) { }

        /// <summary>
        /// 每固定秒调用
        /// </summary>
        /// <remarks>无 <see cref="GameAPP.theGameStatus"/> 限制</remarks>
        /// <param name="trigger">调用时机</param>
        public void OnFixedUpdateMustExecute(TriggerType trigger) { }

        /// <summary>
        /// 属性事件
        /// </summary>
        /// <param name="trigger">调用时机</param>
        public void AttributeEvent(TriggerType trigger) { }

        /// <summary>
        /// 当序列化前
        /// </summary>
        /// <param name="data">序列化数据</param>
        /// <param name="trigger">调用时机</param>
        public void BeforeSerialized(SavePlantData data, TriggerType trigger) { }

        /// <summary>
        /// 当序列化后
        /// </summary>
        /// <param name="data">序列化数据</param>
        /// <param name="trigger">调用时机</param>
        public void AfterDeserialized(SavePlantData data, TriggerType trigger) { }
    }

    /// <summary>
    /// 异步版植物事件
    /// </summary>
    /// <remarks>
    /// <para>所有逻辑均依赖于存储在当前对象上的缓存数据, 如果缓存中没有此组件, 则此组件的方法将不会执行, 在初始化时调用 <see cref="PlantEvent.AddToList(IAsyncPlantEvent)"/> (扩展方法) 来更新缓存</para>
    /// <para>所有方法均会被分别由触发时机为 Pre 和 Post 两个地方调用, 如果方法只需执行一次, 请为需要的方法以特性 <see cref="TriggerOnceAttribute"/> 标记</para>
    /// </remarks>
    public interface IAsyncPlantEvent
    {
        public Component? Component
        {
            get
            {
                if (this is Component comp) return comp;
                return null;
            }
        }

        /// <summary>
        /// 当植物死亡时
        /// </summary>
        /// <param name="reason">死亡原因</param>
        /// <param name="trigger">调用时机</param>
        public async Task DieEvent(Plant.DieReason reason, TriggerType trigger) { }

        /// <summary>
        /// 当植物死亡时
        /// </summary>
        /// <remarks>当植物死亡时，此方法一定被调用</remarks>
        /// <param name="reason">死亡原因</param>
        /// <param name="trigger">调用时机</param>
        public async Task DieEventMustExecute(Plant.DieReason reason, TriggerType trigger) { }

        /// <summary>
        /// 当鼠标点击时
        /// </summary>
        /// <remarks>
        /// <para>与非异步版本行为不同, 不会因已有其他植物成功触发而不触发调用</para>
        /// <para>使用 <see cref="ResultAttribute"/> 并设置返回值类型为 <see cref="bool"/> 实现与非异步版本一致的行为</para>
        /// </remarks>
        /// <param name="mouse">鼠标实例</param>
        /// <param name="trigger">调用时机</param>
        public async Task OnClicked(Mouse mouse, TriggerType trigger) { }

        /// <summary>
        /// 当鼠标点击植物时
        /// </summary>
        /// <remarks>
        /// <para>无论是否有其他植物成功触发, 总是调用</para>
        /// <para>与非异步版本行为不同, 不会因已有其他植物成功触发而不触发调用</para>
        /// <para>使用 <see cref="ResultAttribute"/> 并设置返回值类型为 <see cref="OnClickData"/> 实现与非异步版本一致的行为</para>
        /// </remarks>
        /// <param name="mouse">鼠标实例</param>
        /// <param name="processOther">是否已经有其他植物成功触发</param>
        /// <param name="trigger">调用时机</param>
        /// <returns>点击数据</returns>
        public async Task OnClickedConfig(Mouse mouse, bool processOther, TriggerType trigger) { }

        /// <summary>
        /// 当鼠标点击时
        /// </summary>
        /// <param name="mouse">鼠标实例</param>
        /// <param name="state">鼠标状态</param>
        /// <param name="click">点击类型</param>
        /// <param name="trigger">调用时机</param>
        public async Task MouseEvent(Mouse mouse, MouseState state, MouseClick click, TriggerType trigger) { }

        /// <summary>
        /// 当鼠标点击时
        /// </summary>
        /// <remarks>当鼠标点击时, 此方法一定被调用</remarks>
        /// <param name="mouse">鼠标实例</param>
        /// <param name="state">鼠标状态</param>
        /// <param name="click">点击类型</param>
        /// <param name="trigger">调用时机</param>
        public async Task MouseEventMustExecute(Mouse mouse, MouseState state, MouseClick click, TriggerType trigger) { }

        /// <summary>
        /// 当鼠标上物品名为 cannon 且按下左键时调用
        /// </summary>
        /// <remarks>默认阻止原方法执行</remarks>
        /// <param name="mouse">鼠标实例</param>
        /// <param name="trigger">调用时机</param>
        public async Task SetTargetByMouse(Mouse mouse, TriggerType trigger) { }

        /// <summary>
        /// 当鼠标上物品名为 cannon 且按下左键时调用
        /// </summary>
        /// <remarks>使用 <see cref="ResultAttribute"/> 并设置返回值类型为 <see cref="bool"/> 实现与非异步版本一致的行为</remarks>
        /// <param name="mouse">鼠标实例</param>
        /// <param name="trigger">调用时机</param>
        public async Task SetTargetByMouseBool(Mouse mouse, TriggerType trigger) { }

        /// <summary>
        /// 每帧调用
        /// </summary>
        /// <remarks>仅当 <see cref="GameAPP.theGameStatus"/> 为 <see cref="GameStatus.InGame"/> 时调用</remarks>
        /// <param name="trigger">调用时机</param>
        public async Task OnUpdate(TriggerType trigger) { }

        /// <summary>
        /// 每帧调用
        /// </summary>
        /// <remarks>无 <see cref="GameAPP.theGameStatus"/> 限制</remarks>
        /// <param name="trigger">调用时机</param>
        public async Task OnUpdateMustExecute(TriggerType trigger) { }

        /// <summary>
        /// 每固定秒调用
        /// </summary>
        /// <remarks>仅当 <see cref="GameAPP.theGameStatus"/> 为 <see cref="GameStatus.InGame"/> 时调用</remarks>
        /// <param name="trigger">调用时机</param>
        public async Task OnFixedUpdate(TriggerType trigger) { }

        /// <summary>
        /// 每固定秒调用
        /// </summary>
        /// <remarks>无 <see cref="GameAPP.theGameStatus"/> 限制</remarks>
        /// <param name="trigger">调用时机</param>
        public async Task OnFixedUpdateMustExecute(TriggerType trigger) { }

        /// <summary>
        /// 属性事件
        /// </summary>
        /// <param name="trigger">调用时机</param>
        public async Task AttributeEvent(TriggerType trigger) { }

        /// <summary>
        /// 当序列化前
        /// </summary>
        /// <param name="data">序列化数据</param>
        /// <param name="trigger">调用时机</param>
        public async Task BeforeSerialized(SavePlantData data, TriggerType trigger) { }

        /// <summary>
        /// 当序列化后
        /// </summary>
        /// <param name="data">序列化数据</param>
        /// <param name="trigger">调用时机</param>
        public async Task AfterDeserialized(SavePlantData data, TriggerType trigger) { }
    }

    public static class PlantEvent
    {
        /// <summary>
        /// 事件分发的递归深度上限（跟进上游 CustomizeLib「修复事件分发」）。
        ///
        /// 为什么需要：事件处理函数是 mod 作者自己写的，里面完全可能再次触发本库的事件
        /// （例如在 OnUpdate 里种植物 / 融合 / 触发爆炸 → 又回到 DieEvent、OnUpdate……）。
        /// 没有上限时这就是一条无终止的递归链，直接爆栈；而爆栈现场往往只剩一帧地址，
        /// 事后极难定位。32 层足够正常玩法用到，又足以在爆栈前把它掐断。
        /// </summary>
        private const int MaxDispatchDepth = 32;

        [ThreadStatic]
        private static int DispatchDepth;

        [ThreadStatic]
        private static bool RecursionLogged;

        private static DispatchGuard BeginDispatch()
        {
            if (DispatchDepth >= MaxDispatchDepth)
            {
                if (!RecursionLogged)
                {
                    RecursionLogged = true;
                    CustomCore.CLogger.LogError($"PlantEvent dispatch depth exceeded {MaxDispatchDepth}; nested event dispatch was skipped.");
                }

                return new DispatchGuard(false);
            }

            DispatchDepth++;
            return new DispatchGuard(true);
        }

        /// <summary>成对使用：离开作用域时把深度减回去（Dispose 幂等，Ok=false 时不动全局计数）。</summary>
        private readonly struct DispatchGuard : IDisposable
        {
            public DispatchGuard(bool ok) => Ok = ok;

            public bool Ok { get; }

            public void Dispose()
            {
                if (!Ok) return;
                DispatchDepth--;
                if (DispatchDepth <= 0)
                {
                    DispatchDepth = 0;
                    RecursionLogged = false;
                }
            }
        }

        public static void DieEvent(Component self, Plant.DieReason reason, TriggerType trigger)
        {
            using var dispatch = BeginDispatch();
            if (!dispatch.Ok) return;

            foreach (var comp in GetCachedComps(self))
            {
                if (comp == null) continue;

                // 异步版本调用
                if (comp is IAsyncPlantEvent asyncPlantEvent)
                {
                    try
                    {
                        if (IsMethodTrigger(asyncPlantEvent.DieEvent, trigger))
                            if (reason != Plant.DieReason.ByFreeze && reason != Plant.DieReason.Hid && reason != Plant.DieReason.Wheel)
                                _ = asyncPlantEvent.DieEvent(reason, trigger);
                        if (IsMethodTrigger(asyncPlantEvent.DieEventMustExecute, trigger))
                            _ = asyncPlantEvent.DieEventMustExecute(reason, trigger);
                    }
                    catch (Exception ex) { LogErrorMessage(ex, true); }
                }

                // 非异步版本调用
                if (comp is IPlantEvent plantEvent)
                {
                    try
                    {
                        if (IsMethodTrigger(plantEvent.DieEvent, trigger))
                            if (reason != Plant.DieReason.ByFreeze && reason != Plant.DieReason.Hid && reason != Plant.DieReason.Wheel)
                            plantEvent.DieEvent(reason, trigger);
                        if (IsMethodTrigger(plantEvent.DieEventMustExecute, trigger))
                            plantEvent.DieEventMustExecute(reason, trigger);
                    }
                    catch (Exception ex) { LogErrorMessage(ex); }
                }
            }
        }

        public static (bool block, bool success) OnClicked(Component self, Mouse mouse, bool processOther, TriggerType trigger)
        {
            using var dispatch = BeginDispatch();
            if (!dispatch.Ok) return (false, false);
            bool block = false;
            bool success = false;
            bool ret = false;
            foreach (var comp in GetCachedComps(self))
            {
                if (comp == null) continue;

                // 异步版本调用
                if (comp is IAsyncPlantEvent asyncPlantEvent)
                {
                    try
                    {
                        if (IsMethodTrigger(asyncPlantEvent.OnClicked, trigger) && !processOther)
                        {
                            _ = asyncPlantEvent.OnClicked(mouse, trigger);
                            var datas = AttributesTools.GetMethodExtResults(asyncPlantEvent.OnClicked).OfType<bool>();
                            if (datas != null && datas.Any())
                                block |= success |= datas.Any(b => b);
                        }
                    }
                    catch (Exception ex) { LogErrorMessage(ex, true); }

                    try
                    {
                        if (IsMethodTrigger(asyncPlantEvent.OnClickedConfig, trigger))
                        {
                            _ = asyncPlantEvent.OnClickedConfig(mouse, processOther, trigger);
                            var datas = AttributesTools.GetMethodExtResults(asyncPlantEvent.OnClickedConfig).OfType<OnClickData>();
                            if (datas != null && datas.Any())
                            {
                                block |= datas.Any(data => data.block);
                                if (datas.Any(data => data.success))
                                    ret = true;
                            }
                        }
                    }
                    catch (Exception ex) { LogErrorMessage(ex, true); }
                }

                // 非异步版本调用
                if (comp is IPlantEvent plantEvent)
                {
                    try
                    {
                        if (IsMethodTrigger(plantEvent.OnClicked, trigger) && !processOther)
                            block |= success |= plantEvent.OnClicked(mouse, trigger);
                    }
                    catch (Exception ex) { LogErrorMessage(ex); }

                    try
                    {
                        if (IsMethodTrigger(plantEvent.OnClickedConfig, trigger))
                        {
                            var config = plantEvent.OnClickedConfig(mouse, processOther, trigger);
                            block |= config.block;
                            if (config.success)
                                ret = true;
                        }
                    }
                    catch (Exception ex) { LogErrorMessage(ex); }
                }

                if (ret) return (block, true);
            }
            return (block, success);
        }

        public static void MouseEvent(Component self, Mouse mouse, MouseState state, MouseClick click, TriggerType trigger)
        {
            using var dispatch = BeginDispatch();
            if (!dispatch.Ok) return;
            foreach (var comp in GetCachedComps(self))
            {
                if (comp == null) continue;

                if (comp is IAsyncPlantEvent asyncPlantEvent)
                {
                    try
                    {
                        if (IsMethodTrigger(asyncPlantEvent.MouseEvent, trigger))
                            if (GameAPP.theGameStatus == GameStatus.InGame)
                                _ = asyncPlantEvent.MouseEvent(mouse, state, click, trigger);
                        if (IsMethodTrigger(asyncPlantEvent.MouseEventMustExecute, trigger))
                            _ = asyncPlantEvent.MouseEventMustExecute(mouse, state, click, trigger);
                    }
                    catch (Exception ex) { LogErrorMessage(ex, true); }
                }

                if (comp is IPlantEvent plantEvent)
                {
                    try
                    {
                        if (IsMethodTrigger(plantEvent.MouseEvent, trigger))
                            if (GameAPP.theGameStatus == GameStatus.InGame)
                                plantEvent.MouseEvent(mouse, state, click, trigger);
                        if (IsMethodTrigger(plantEvent.MouseEventMustExecute, trigger))
                            plantEvent.MouseEventMustExecute(mouse, state, click, trigger);
                    }
                    catch (Exception ex) { LogErrorMessage(ex); }
                }
            }
        }

        public static bool SetTargetByMouse(Component self, Mouse mouse, TriggerType trigger)
        {
            using var dispatch = BeginDispatch();
            if (!dispatch.Ok) return false;
            bool block = false;

            foreach (var comp in GetCachedComps(self))
            {
                if (comp == null) continue;

                if (comp is IAsyncPlantEvent asyncPlantEvent)
                {
                    try
                    {
                        if (IsMethodTrigger(asyncPlantEvent.SetTargetByMouse, trigger))
                        {
                            _ = asyncPlantEvent.SetTargetByMouse(mouse, trigger);
                            block |= true;
                        }

                        if (IsMethodTrigger(asyncPlantEvent.SetTargetByMouseBool, trigger))
                        {
                            _ = asyncPlantEvent.SetTargetByMouseBool(mouse, trigger);
                            var datas = AttributesTools.GetMethodExtResults(asyncPlantEvent.SetTargetByMouseBool).OfType<bool>();
                            if (datas != null && datas.Any())
                                block |= datas.Any(b => b);
                        }
                    }
                    catch (Exception ex) { LogErrorMessage(ex, true); }
                }

                if (comp is IPlantEvent plantEvent)
                {
                    try
                    {
                        if (IsMethodTrigger(plantEvent.SetTargetByMouse, trigger))
                        {
                            plantEvent.SetTargetByMouse(mouse, trigger);
                            block |= true;
                        }

                        if (IsMethodTrigger(plantEvent.SetTargetByMouseBool, trigger))
                            block |= plantEvent.SetTargetByMouseBool(mouse, trigger);
                    }
                    catch (Exception ex) { LogErrorMessage(ex); }
                }
            }

            return block;
        }

        public static void OnUpdate(Component self, TriggerType trigger)
        {
            using var dispatch = BeginDispatch();
            if (!dispatch.Ok) return;
            foreach (var comp in GetCachedComps(self))
            {
                if (comp is IAsyncPlantEvent asyncPlantEvent)
                {
                    try
                    {
                        if (GameAPP.theGameStatus == GameStatus.InGame)
                            if (IsMethodTrigger(asyncPlantEvent.OnUpdate, trigger))
                                _ = asyncPlantEvent.OnUpdate(trigger);
                        if (IsMethodTrigger(asyncPlantEvent.OnUpdateMustExecute, trigger))
                            _ = asyncPlantEvent.OnUpdateMustExecute(trigger);
                    }
                    catch (Exception ex) { LogErrorMessage(ex, true); }
                }

                if (comp is IPlantEvent plantEvent)
                {
                    try
                    {
                        if (GameAPP.theGameStatus == GameStatus.InGame)
                            if (IsMethodTrigger(plantEvent.OnUpdate, trigger))
                                plantEvent.OnUpdate(trigger);
                        if (IsMethodTrigger(plantEvent.OnUpdateMustExecute, trigger))
                            plantEvent.OnUpdateMustExecute(trigger);
                    }
                    catch (Exception ex) { LogErrorMessage(ex); }
                }
            }
        }

        public static void OnFixedUpdate(Component self, Plant plant, TriggerType trigger)
        {
            using var dispatch = BeginDispatch();
            if (!dispatch.Ok) return;
            foreach (var comp in GetCachedComps(self))
            {
                if (comp is IAsyncPlantEvent asyncPlantEvent)
                {
                    try
                    {
                        if (GameAPP.theGameStatus == GameStatus.InGame && plant.anim != null && plant.anim.speed != 0f)
                            if (IsMethodTrigger(asyncPlantEvent.OnFixedUpdate, trigger))
                                _ = asyncPlantEvent.OnFixedUpdate(trigger);
                        if (IsMethodTrigger(asyncPlantEvent.OnFixedUpdateMustExecute, trigger))
                            _ = asyncPlantEvent.OnFixedUpdateMustExecute(trigger);
                    }
                    catch (Exception ex) { LogErrorMessage(ex, true); }
                }

                if (comp is IPlantEvent plantEvent)
                {
                    try
                    {
                        if (GameAPP.theGameStatus == GameStatus.InGame && plant.anim != null && plant.anim.speed != 0f)
                            if (IsMethodTrigger(plantEvent.OnFixedUpdate, trigger))
                                plantEvent.OnFixedUpdate(trigger);
                        if (IsMethodTrigger(plantEvent.OnFixedUpdateMustExecute, trigger))
                            plantEvent.OnFixedUpdateMustExecute(trigger);
                    }
                    catch (Exception ex) { LogErrorMessage(ex); }
                }
            }
        }

        public static void AttributeEvent(Component self, TriggerType trigger)
        {
            using var dispatch = BeginDispatch();
            if (!dispatch.Ok) return;
            foreach (var comp in GetCachedComps(self))
            {
                if (comp is IAsyncPlantEvent asyncPlantEvent)
                {
                    try
                    {
                        if (IsMethodTrigger(asyncPlantEvent.AttributeEvent, trigger))
                            _ = asyncPlantEvent.AttributeEvent(trigger);
                    }
                    catch (Exception ex) { LogErrorMessage(ex, true); }
                }

                if (comp is IPlantEvent plantEvent)
                {
                    try
                    {
                        if (IsMethodTrigger(plantEvent.AttributeEvent, trigger))
                            plantEvent.AttributeEvent(trigger);
                    }
                    catch (Exception ex) { LogErrorMessage(ex); }
                }
            }
        }

        public static void BeforeSerialized(Component self, SavePlantData data, TriggerType trigger)
        {
            using var dispatch = BeginDispatch();
            if (!dispatch.Ok) return;
            foreach (var comp in GetCachedComps(self))
            {
                if (comp is IAsyncPlantEvent asyncPlantEvent)
                {
                    try
                    {
                        if (IsMethodTrigger(asyncPlantEvent.BeforeSerialized, trigger))
                            _ = asyncPlantEvent.BeforeSerialized(data, trigger);
                    }
                    catch (Exception ex) { LogErrorMessage(ex, true); }
                }

                if (comp is IPlantEvent plantEvent)
                {
                    try
                    {
                        if (IsMethodTrigger(plantEvent.BeforeSerialized, trigger))
                            plantEvent.BeforeSerialized(data, trigger);
                    }
                    catch (Exception ex) { LogErrorMessage(ex); }
                }
            }
        }

        public static void AfterDeserialized(Component self, SavePlantData data, TriggerType trigger)
        {
            using var dispatch = BeginDispatch();
            if (!dispatch.Ok) return;
            foreach (var comp in GetCachedComps(self))
            {
                if (comp is IAsyncPlantEvent asyncPlantEvent)
                {
                    try
                    {
                        if (IsMethodTrigger(asyncPlantEvent.AfterDeserialized, trigger))
                            _ = asyncPlantEvent.AfterDeserialized(data, trigger);
                    }
                    catch (Exception ex) { LogErrorMessage(ex, true); }
                }

                if (comp is IPlantEvent plantEvent)
                {
                    try
                    {
                        if (IsMethodTrigger(plantEvent.AfterDeserialized, trigger))
                            plantEvent.AfterDeserialized(data, trigger);
                    }
                    catch (Exception ex) { LogErrorMessage(ex); }
                }
            }
        }

        /// <summary>
        /// TriggerOnceAttribute 的判定结果缓存。
        /// 原实现每次调用都执行 method.GetCustomAttributes(...) + OfType + ToArray：
        /// GetCustomAttributes 每次都会重新实例化特性对象（运行时不做缓存），后面还跟着两次分配。
        /// 而本方法在每帧路径上——单株单帧 OnUpdate 的 Pre/Post 各调 2 次，FixedUpdate 再来一遍，
        /// 场上 50 株 60FPS 就是每秒上万次反射。特性是静态元数据，判定结果只与 MethodInfo 有关，缓存后完全等价。
        /// 键是 MethodInfo，条目数上限 = 事件方法种类数（有界），不会无限增长。
        /// </summary>
        private static readonly Dictionary<MethodInfo, TriggerType?> TriggerCache = new();

        private static bool IsMethodTrigger(Delegate box, TriggerType trigger)
        {
            var method = box.Method;
            if (method == null) return false;
            if (!TriggerCache.TryGetValue(method, out var once))
            {
                // 没有 TriggerOnceAttribute ⇒ once 为 null ⇒ 两个 trigger 都执行（与原逻辑一致）
                var attr = method.GetCustomAttributes(typeof(TriggerOnceAttribute), false).OfType<TriggerOnceAttribute>().FirstOrDefault();
                once = attr?.Trigger;
                TriggerCache[method] = once;
            }
            return once == null || once == trigger;
        }

        public static Component?[] GetEventComponents(Component?[] comps) =>
            [.. comps.Where(comp => comp != null && (comp is IAsyncPlantEvent or IPlantEvent))];

        private static void LogErrorMessage(Exception ex, bool async = false)
        {
            // 【限频】这些事件跑在每帧路径上（Plant.Update / Plant.FixedUpdate 由原生钩子接管），
            // 某个 mod 的 OnUpdate 若每帧都抛异常，原实现会把完全相同的堆栈刷满整个 BepInEx 日志。
            // 现在同一条消息在冷却期内只打一次，并提示已被抑制多少次，冷却 10 秒。
            // 代价只落在"真的出错"这条路径上，热路径零开销。
            // 另注：这里刻意不改成"用 board == null 过滤非关卡实例"—— Entity.board 在 interop 里是属性，
            // 每株每帧读它就是一次原生调用，会把热路径省下来的开销又花回去；
            // 而 OnUpdate 本身已有 GameAPP.theGameStatus == InGame 闸，普通事件在非关卡场景本就进不来。
            var key = ex.GetType().FullName + "|" + ex.Message + "|" + (async ? "async" : "sync");
            _errorLogGate.TryGetValue(key, out var last);
            var now = Time.realtimeSinceStartup;
            if (now - last < ErrorLogCooldown)
            {
                _errorSuppressed[key] = _errorSuppressed.TryGetValue(key, out var c) ? c + 1 : 1;
                return;
            }
            if (_errorSuppressed.TryGetValue(key, out var suppressed) && suppressed > 0)
            {
                CustomCore.CLogger.LogWarning(
                    $"[IPlantEvent] 同一条异常在 {ErrorLogCooldown:F0}s 内重复 {suppressed} 次，已抑制；本条为最后一次之后的再次出现。");
                _errorSuppressed[key] = 0;
            }
            _errorLogGate[key] = now;
            CustomCore.CLogger.LogError(
                $"An exception occurred while executing I{(async ? "Async" : "")}PlantEvent: {ex.Message}\n" +
                $"StackTrace:\n" +
                $"{ex.StackTrace}");
        }

        private const float ErrorLogCooldown = 10f;
        private static readonly Dictionary<string, float> _errorLogGate = new();
        private static readonly Dictionary<string, int> _errorSuppressed = new();

        public static void MakeRefresh(Component comp) => 
            comp.SetData(Strings.CachedCompsName, GetEventComponents(comp.GetComponents<Component>()));

        public static Component?[] GetCachedComps(Component comp)
        {
            // 这是整个库里最热的一个访问器（每株植物每帧至少 2 次，OnUpdate 的 Pre/Post 各一次）。
            // 原写法 GetOrInitData(...) 内部会读一次 .val 做判空，返回时又经隐式转换再读一次 .val，
            // 而每次 .val 都是一次原生 GetOrAddComponent<DataComponent>()。
            // 改成只用非初始化版 GetData 并手动判空：稳态下每株每帧从 2 次原生调用降到 1 次。
            var data = comp.GetData<Component?[]>(Strings.CachedCompsName);
            var comps = data.val;
            if (comps == null)
            {
                comp.SetData(Strings.CachedCompsName, Array.Empty<Component?>());
                return Array.Empty<Component?>();
            }
            return comps;
        }

        public static int GetCachedCompCount(Component comp) =>
            GetCachedComps(comp).Length;

        public static bool HasEventComp(Component comp) => GetCachedCompCount(comp) > 0;

        /// <summary>
        /// 更新缓存
        /// </summary>
        public static void AddToList(this IAsyncPlantEvent self)
        {
            if (self.Component != null) MakeRefresh(self.Component);
        }

        /// <summary>
        /// 更新缓存
        /// </summary>
        public static void AddToList(this IPlantEvent self)
        {
            if (self.Component != null) MakeRefresh(self.Component);
        }

        private static class Strings
        {
            internal const string CachedCompsName = "MengxiLib_PlantCachedComps";
        }
    }

    public enum TriggerType
    {
        Pre,
        Post
    }

    public enum MouseClick
    {
        LeftClick,
        RightClick,
        MiddleClick,
        SideFront,
        SideBack
    }

    public enum MouseState
    {
        Down,
        Hold,
        Up
    }

    public struct OnClickData
    {
        public static Type DataType => typeof(OnClickData);
        public static OnClickData Success => new(true, true);
        public static OnClickData NotSuccess => new(false, false);

        /// <summary>
        /// 是否执行成功
        /// </summary>
        public bool success = false;
        /// <summary>
        /// 是否阻塞原来的点击方法, 仅当 TriggerType 为 Pre 时生效
        /// </summary>
        public bool block = false;

        public OnClickData() { }
        public OnClickData(bool success, bool block)
        {
            this.success = success; 
            this.block = block; 
        }
    }

    /// <summary>
    /// 仅触发一次标记
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class TriggerOnceAttribute : Attribute
    {
        public TriggerType Trigger { get; set; } = TriggerType.Post;

        public TriggerOnceAttribute() { }
        public TriggerOnceAttribute(TriggerType trigger) => Trigger = trigger;
    }
}
