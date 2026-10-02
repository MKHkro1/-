using System;
using TMPro;
using UnityEngine;

namespace MengxiLib.BepInEx.Patch
{
    /// <summary>
    /// 二创栏目（图鉴 / 选卡）用到的**跨库标识名**。
    ///
    /// ★★ 铁律：**必须与原版 CustomizeLib.BepInEx 的名字完全不同**，两个库才能真正共存。
    ///   原库建的三个栏目用的是（已比对 4.0 参考库源码，逐字一致）：
    ///     · LookCustom     —— 图鉴·植物
    ///     · LoolAll_Other  —— 图鉴·僵尸（原库拼写就是 LoolAll，不是 LookAll）
    ///     · SelectCustom   —— 选卡栏
    ///   如果沿用同一批名字、再用 Find 去"认领"它，就会**改掉原库的东西**
    ///   （把它的标签刷成「梦汐植物」、把它的点击逻辑据为己有）—— 那不是共存，是覆盖。
    ///
    ///   正解：**各建各的**。名字不同 ⇒ 双方互相 Find 不到对方 ⇒ 谁也碰不到谁。
    ///   界面上就是两组按钮：原库的「二创植物 / 二创僵尸」+ 我们的「梦汐植物 / 梦汐僵尸」，
    ///   各自保留自己的筛选条件与点击逻辑，互不干预。
    ///
    /// ★ 显示文本（见 CustomTabText）与这些名字无关：看到的是文本、查找用的是名字。
    /// </summary>
    internal static class CustomTabNames
    {
        /// <summary>图鉴·植物 的梦汐分栏按钮名（AlmanacPlantMenu 下）。刻意区别于原库的 LookCustom。</summary>
        internal const string LookCustom = "LookCustomMengxi";

        /// <summary>图鉴·僵尸 的梦汐分栏按钮名（AlmanacZombieMenu 下）。刻意区别于原库的 LoolAll_Other。</summary>
        internal const string LookAllOther = "LoolAllOtherMengxi";

        /// <summary>选卡栏 的梦汐按钮名（InGameUI 的 ShowCardLayout 或 IZBottomMenu.plantLibrary/Buttons 下）。刻意区别于原库的 SelectCustom。</summary>
        internal const string SelectCustom = "SelectCustomMengxi";
    }

    /// <summary>
    /// 二创栏目在界面上**显示**的文字。
    ///
    /// 与 CustomTabNames 分开写、且集中在一处，是为了让"改品牌名"只有一个落点：
    /// 以后要再改名，只动这里，不必去 PatchCore / SelectCustomPlants 里逐处找字符串。
    /// </summary>
    internal static class CustomTabText
    {
        /// <summary>植物侧栏目显示名。</summary>
        internal const string Plants = "梦汐植物";

        /// <summary>僵尸侧栏目显示名。</summary>
        internal const string Zombies = "梦汐僵尸";
    }

    /// <summary>
    /// 把栏目的显示文本刷成"梦汐"品牌名。
    ///
    /// 三处的文本形状不一样，所以要三个方法（各自与"建栏目"时的写法对应）：
    ///   · 图鉴·植物：文本挂在 TextShadow 子物体上；
    ///   · 图鉴·僵尸：按钮下**所有** TMP 文本都刷（原版建按钮时就是这么写的）；
    ///   · 选卡栏：文本在第一个子物体上。
    ///
    /// 全部包 try/catch：这是改外观的辅助逻辑，取不到节点时绝不能把建栏目的主流程带崩
    /// （父级链在 Awake/Start 的某些时点可能还没就绪）。
    /// </summary>
    internal static class CustomTabLabel
    {
        /// <summary>图鉴·植物 分栏的标签。</summary>
        internal static void MarkPlants(GameObject tab)
        {
            try
            {
                var shadow = tab.transform.FindChild("TextShadow");
                if (shadow != null)
                    shadow.gameObject.GetComponent<TextMeshProUGUI>().text = CustomTabText.Plants;
            }
            catch (Exception) { }
        }

        /// <summary>图鉴·僵尸 分栏的标签。</summary>
        internal static void MarkZombies(GameObject tab)
        {
            try
            {
                foreach (var text in tab.GetComponentsInChildren<TextMeshProUGUI>())
                    if (text != null) text.text = CustomTabText.Zombies;
            }
            catch (Exception) { }
        }

        /// <summary>选卡栏 自定义按钮的标签。</summary>
        internal static void MarkSelect(GameObject button)
        {
            try
            {
                var first = button.transform.GetChild(0);
                if (first != null)
                    first.gameObject.GetComponent<TextMeshProUGUI>().text = CustomTabText.Plants;
            }
            catch (Exception) { }
        }
    }
}
