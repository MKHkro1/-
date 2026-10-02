// #define DEBUG_FEATURE__ENABLE_MULTI_LEVEL_BUFF // 启用多级词条

using AlmanacData;
using Core;
using MengxiLib.BepInEx.Coexist;
using MengxiLib.BepInEx.ExtensionData.Basic;
using MengxiLib.BepInEx.Script;
using MengxiLib.BepInEx.ToolInterfaces;
using MengxiLib.BepInEx.UnmanagedTools;
using GameLevel;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Injection;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppInterop.Runtime.Runtime;
using Microsoft.VisualBasic;
using System;
using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using TMPro;
using Unity.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using ZenGarden;
using static SingleBuffManager;
using static UnityEngine.Object;

#pragma warning disable
///
///Credit to likefengzi(https://github.com/likefengzi)(https://space.bilibili.com/237491236)
///
namespace MengxiLib.BepInEx.Patch
{
    /// <summary>
    /// 注册融合洋芋配方
    /// </summary>
    [HarmonyPatch(typeof(MixBomb), nameof(MixBomb.AttributeEvent))]
    public static class MixBombPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(MixBomb __instance)
        {
            bool success = false;
            if (__instance != null)
            {
                List<Plant> plants = Lawnf.Get1x1Plants(__instance.thePlantColumn, __instance.thePlantRow).ToArray().ToList();
                if (plants is null)
                    return true;
                foreach (Plant plant in plants)
                {
                    if (plant != null && CustomCore.CustomMixBombFusions.Keys.Any(k => k.Item2 == plant.thePlantType))
                    {
                        List<(PlantType, PlantType, PlantType)> mixBombFusions = CustomCore.CustomMixBombFusions
                            .Where(kvp => kvp.Key.Item2 == plant.thePlantType)
                            .Select(kvp => kvp.Key)
                            .ToList();
                        List<Plant> leftPlant = Lawnf.Get1x1Plants(__instance.thePlantColumn - 1, __instance.thePlantRow).ToArray().ToList();
                        List<Plant> rightPlant = Lawnf.Get1x1Plants(__instance.thePlantColumn + 1, __instance.thePlantRow).ToArray().ToList();
                        foreach ((PlantType, PlantType, PlantType) fusion in mixBombFusions)
                        {
                            Plant? firstLeftPlant = leftPlant.FirstOrDefault(p => p.thePlantType == fusion.Item1);
                            Plant? firstRightPlant = rightPlant.FirstOrDefault(p => p.thePlantType == fusion.Item3);
                            if (firstLeftPlant == null || firstRightPlant == null)
                            {
                                CustomCore.CustomMixBombFusions[fusion].Item2[UnityEngine.Random.Range(0, CustomCore.CustomMixBombFusions[fusion].Item2.Count)](firstLeftPlant, plant, firstRightPlant);
                                continue;
                            }
                            if (leftPlant.Any(p => p.thePlantType == fusion.Item1) && rightPlant.Any(p => p.thePlantType == fusion.Item3))
                            {
                                CustomCore.CustomMixBombFusions[fusion].Item1[UnityEngine.Random.Range(0, CustomCore.CustomMixBombFusions[fusion].Item1.Count)](firstLeftPlant, plant, firstRightPlant);
                                success = true;
                            }
                            else
                            {
                                CustomCore.CustomMixBombFusions[fusion].Item2[UnityEngine.Random.Range(0, CustomCore.CustomMixBombFusions[fusion].Item2.Count)](firstLeftPlant, plant, firstRightPlant);
                            }
                        }
                    }
                }
            }
            if (__instance != null && success)
                __instance.Die();
            if (success)
                return false;
            return true;
        }
    }

    /// <summary>
    /// 注册肥料使用事件
    /// </summary>
    [HarmonyPatch(typeof(Fertilize))]
    public static class FertilizePatch
    {
        [HarmonyPatch(nameof(Fertilize.Upgrade))]
        [HarmonyPostfix]
        public static void PostUpgrade(Fertilize __instance)
        {
            if (__instance == null || __instance.theTargetPlant == null) return;

            int column = __instance.theTargetPlant.thePlantColumn;
            int row = __instance.theTargetPlant.thePlantRow;

            List<Plant> plants = Lawnf.Get1x1Plants(column, row).ToArray().ToList<Plant>(); // 获取植物，il2cpp窝爱你
            if (plants == null) return;

            for (int i = 0; i < plants.Count; i++)
            {
                Plant plant = plants[i];
                if (plant == null) continue;
                if (plant.thePlantColumn != column || plant.thePlantRow != row) continue;
                if (Board.Instance == null) return;

                if (CustomCore.CustomUseFertilize.ContainsKey(plant.thePlantType))
                {
                    CustomCore.CustomUseFertilize[plant.thePlantType](plant);
                }
            }

            UnityEngine.Object.Destroy(__instance.gameObject);
        }
    }

    [HarmonyPatch(typeof(AlmanacPlantWindow))]
    public static class AlmanacPlantWindowPatch
    {
        [HarmonyPatch(nameof(AlmanacPlantWindow.SetPlant))]
        [HarmonyPostfix]
        public static void PostInitWindow(AlmanacPlantWindow __instance, ref PlantType thePlantType)
        {
            {
                PlantType plantType = thePlantType;
                if (CustomCore.CustomPlantsSkin.ContainsKey(plantType))
                    __instance.skinButton.SetActive(CustomCore.CustomPlantsSkin.ContainsKey(plantType));
            }
            {
                PlantType plantType = thePlantType;
                if (CustomCore.CustomPlantTypes.Contains(plantType))
                    __instance.skinButton.SetActive(CustomCore.CustomPlantsSkin.ContainsKey(plantType));
            }
            {
                PlantType plantType = thePlantType;
                if (CustomCore.CustomPlantsSkinActive.ContainsKey(plantType) && CustomCore.CustomPlantsSkinActive[plantType]) return;
                String fullName = Directory.GetParent(Application.dataPath)?.FullName;
                if (fullName == null)
                    return;
                string skinPath = Path.Combine(fullName, "BepInEx", "plugins", "Skin");
                if (!Directory.Exists(skinPath))
                    return;
                var regex = new Regex($@"^skin_{(int)plantType}(?!\d).*$", RegexOptions.IgnoreCase);
                var files = Directory.GetFiles(skinPath).Where(str => regex.IsMatch(Path.GetFileNameWithoutExtension(str))).ToList();
                __instance.skinButton.SetActive(files.Count > 0);
            }
        }

        [HarmonyPatch(nameof(AlmanacPlantWindow.LeftSkin))]
        [HarmonyPrefix]
        public static void PreLeftSkin(AlmanacPlantWindow __instance, out (bool active, int oldIndex) __state)
        {
            __state = (__instance.skinButton.active, GameAPP.resourcesManager.plantSkinDic[__instance.currentPlantType]);

            // PatchMgr.OnChangeSkin(__instance.currentPlantType, GameAPP.resourcesManager.plantSkinDic[__instance.currentPlantType]);
        }

        [HarmonyPatch(nameof(AlmanacPlantWindow.LeftSkin))]
        [HarmonyPostfix]
        public static void PostLeftSkin(AlmanacPlantWindow __instance, (bool active, int oldIndex) __state)
        {
            __instance.skinButton.SetActive(__state.active);

            PatchMgr.OnChangeSkin(__instance.currentPlantType, GameAPP.resourcesManager.plantSkinDic[__instance.currentPlantType]);
            PatchMgr.RunSkinScript(__instance.currentPlantType, __state.oldIndex, GameAPP.resourcesManager.plantSkinDic[__instance.currentPlantType]);
            PatchMgr.SaveSkin();
        }

        [HarmonyPatch(nameof(AlmanacPlantWindow.RightSkin))]
        [HarmonyPrefix]
        public static void PreRightSkin(AlmanacPlantWindow __instance, out (bool active, int oldIndex) __state)
        {
            __state = (__instance.skinButton.active, GameAPP.resourcesManager.plantSkinDic[__instance.currentPlantType]);

            // PatchMgr.OnChangeSkin(__instance.currentPlantType, GameAPP.resourcesManager.plantSkinDic[__instance.currentPlantType]);
        }

        [HarmonyPatch(nameof(AlmanacPlantWindow.RightSkin))]
        [HarmonyPostfix]
        public static void PostRightSkin(AlmanacPlantWindow __instance, (bool active, int oldIndex) __state)
        {
            __instance.skinButton.SetActive(__state.active);

            PatchMgr.OnChangeSkin(__instance.currentPlantType, GameAPP.resourcesManager.plantSkinDic[__instance.currentPlantType]);
            PatchMgr.RunSkinScript(__instance.currentPlantType, __state.oldIndex, GameAPP.resourcesManager.plantSkinDic[__instance.currentPlantType]);
            PatchMgr.SaveSkin();
        }
    }

    [HarmonyPatch(typeof(AlmanacPlantMenu))]
    public static class AlmanacPlantMenuPatch
    {
        [HarmonyPatch(nameof(AlmanacPlantMenu.Awake))]
        // 排在最后只为**顺序确定**（我们稳定排在原库那个栏目下面），与"避免撞名"无关 —— 名字不同本来就不会撞。
        [HarmonyPriority(Priority.Last)]
        [HarmonyPostfix]
        public static void PostAwake(AlmanacPlantMenu __instance)
        {
            var content = __instance.transform.FindChild("Scroll View/Viewport/Content");

            // ★ 共存：各建各的，绝不碰原版 CustomizeLib.BepInEx 建的那个。
            //   我们的按钮名是 LookCustomMengxi（原库是 LookCustom），互相 Find 不到对方，
            //   所以不存在"接管/改名"这回事 —— 界面上就是两组按钮，各自的筛选与点击逻辑。
            var go = content.FindChild("LookRedCard").gameObject;
            var newSelect = Instantiate(go, content);
            Action action = () =>
            {
                Func<PlantType, bool> func = (plantType) => !Enum.IsDefined(plantType);
                __instance.ShowPlants(func);
            };
            UnityEvent unityEvent = new();
            unityEvent.AddListener(action);
            newSelect.GetComponent<UIButton>().clickEvent = unityEvent;
            newSelect.name = CustomTabNames.LookCustom;
            CustomTabLabel.MarkPlants(newSelect.gameObject);
            newSelect.transform.localPosition = new Vector3(0f, -44f * newSelect.transform.childCount + 72f, 0f);

            var rect = __instance.transform.FindChild("Scroll View/Viewport/Content").GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, 
                rect.sizeDelta.y + 80f);
        }
    }

    [HarmonyPatch(typeof(AlmanacDataLoader))]
    public static class AlmanacDataLoaderPatch
    {
        [HarmonyPatch(nameof(AlmanacDataLoader.LoadZombieData))]
        [HarmonyPostfix]
        public static void PostLoadZombieData()
        {
            foreach (var item in CustomCore.ZombiesAlmanac)
            {
                if (AlmanacDataLoader.zombieDatas.ContainsKey(item.Key)) continue;
                if (item.Value.Item3 != null)
                {
                    AlmanacDataLoader.zombieDatas.Add(item.Key, item.Value.Item3);
                    continue;
                }
                var data = new ZombieInfo();
                var newName = Regex.Replace(item.Value.Item1, @"\([^()]*\)", "");
                data.name = newName;
                data.info = item.Value.Item2;
                data.introduce = "";
                data.theZombieType = item.Key;
                AlmanacDataLoader.zombieDatas.Add(item.Key, data);
            }
        }

        [HarmonyPatch(nameof(AlmanacDataLoader.LoadPlantData))]
        [HarmonyPostfix]
        public static void PostLoadPlantData()
        {
            foreach (var (key, value) in CustomCore.PlantsAlmanac)
            {
                if (AlmanacDataLoader.plantDatas.ContainsKey(key)) continue;
                var data = new PlantInfo();
                var newName = Regex.Replace(value.name, @"\([^()]*\)", "");
                data.name = newName;
                data.info = value.info;
                data.seedType = (int)value.plantType;
                data.cost = value.cost;
                AlmanacDataLoader.plantDatas.Add(key, data);
            }
        }
    }

    [HarmonyPatch(typeof(ConveyManager))]
    public static class ConveyManagerPatch
    {
        [HarmonyPatch(nameof(ConveyManager.Awake))]
        [HarmonyPostfix]
        public static void PostAwake(ConveyManager __instance)
        {
            if (Utils.IsCustomLevel(out var levelData) && levelData.BoardTag.isConvey && levelData.ConveyBeltPlantTypes().Count > 0)
            {
                __instance.plants = levelData.ConveyBeltPlantTypes().ToIl2CppList();
            }
        }

        [HarmonyPatch(nameof(ConveyManager.GetCardPool))]
        [HarmonyPostfix]
        public static void PostGetCardPool(ref Il2CppSystem.Collections.Generic.List<PlantType> __result)
        {
            if (Utils.IsCustomLevel(out var levelData) && levelData.BoardTag.isConvey && levelData.ConveyBeltPlantTypes().Count > 0)
            {
                __result = levelData.ConveyBeltPlantTypes().ToIl2CppList();
            }
        }
    }

    [HarmonyPatch(typeof(InGameText), nameof(InGameText.ShowText))]
    public static class InGameTextPatch
    {
        public static bool disable = false;
        [HarmonyPrefix]
        public static bool Prefix(string text, float time)
        {
            if (text == "通关挑战模式解锁配方" && time == 7f && disable)
            {
                disable = false;
                return false;
            }
            return true;
        }
    }

    /// <summary>
    /// 为二创植物附加植物特性
    /// </summary>
    [HarmonyPatch(typeof(CreatePlant))]
    public static class CreatePlantPatch
    {
        [HarmonyPatch(nameof(CreatePlant.SetPlant))]
        [HarmonyPostfix]
        public static void Postfix_SetPlant(CreatePlant __instance, ref int newColumn, ref int newRow, ref Plant __result)
        {
            if (__result != null && __result.TryGetComponent<Plant>(out var plant) &&
                CustomCore.CustomPlantTypes.Contains(plant.thePlantType))
            {
                TypeMgr.GetPlantTag(plant);
            }
        }

        [HarmonyPatch(nameof(CreatePlant.Lim))]
        [HarmonyPostfix]
        public static void PostLim(CreatePlant __instance, ref PlantType theSeedType, ref bool __result)
        {
            // 自定义条件
            {
                if (CustomCore.CustomBanMix.ContainsKey(theSeedType) && CustomCore.CustomBanMix[theSeedType].Item1 != null)
                {
                    if (CustomCore.CustomBanMix[theSeedType].Item1.Invoke())
                    {
                        CustomCore.CustomBanMix[theSeedType].Item2?.Invoke();
                    }
                    else
                    {
                        __result = true;
                        InGameTextPatch.disable = true;
                        CustomCore.CustomBanMix[theSeedType].Item3?.Invoke();
                    }
                }
            }
        }

        [HarmonyPatch(nameof(CreatePlant.LimTravel))]
        [HarmonyPostfix]
        public static void Postfix_LimTravel(CreatePlant __instance, ref PlantType theSeedType, ref bool __result)
        {
            // 判定
            {
                bool isCanSet = false;
                if (TravelMgr.Instance != null && Board.Instance.boardTag.isTravel)
                    isCanSet = true;
                if (__instance.board.boardTag.enableAllTravelPlant || __instance.board.boardTag.enableTravelPlant || __instance.board.boardTag.isTravel)
                    isCanSet = true;

                if (CustomCore.CustomUltimatePlants.Contains(theSeedType) && !isCanSet)
                {
                    __result = true;
                    InGameText.Instance.ShowText("该配方仅旅行生存系列或深渊可用", 3f, false);
                }
            }
            
            // 强究
            {
                if (CustomCore.CustomStrongUltimatePlants.ContainsKey(theSeedType))
                {
                    if (__instance.board == null)
                        __result = false;
                    else
                    {
                        if (!__instance.board.boardTag.enableAllTravelPlant && !__instance.board.boardTag.enableTravelPlant && !__instance.board.boardTag.isSuperRandom && !__instance.board.boardTag.isUltimateSuperRandom)
                        {
                            __result = true;
                            InGameText.Instance.ShowText("该配方仅旅行模式或深渊可用", 4f);
                        }
                        else
                        {
                            if (TravelMgr.Instance == null)
                                __result = false;
                            else
                            {
                                if (TravelMgr.Instance.data.unlockedPlants.Contains((TravelUnlocks)CustomCore.CustomStrongUltimatePlants[theSeedType]) || __instance.board.boardTag.enableAllTravelPlant || __instance.board.boardTag.isSuperRandom || __instance.board.boardTag.isUltimateSuperRandom)
                                    __result = false;
                                else
                                {
                                    __result = true;
                                    InGameText.Instance.ShowText("该配方需要抽取", 4f);
                                }
                            }
                        }
                    }
                }
            }
        }

        [HarmonyPatch(nameof(CreatePlant.MixBombCheck))]
        [HarmonyPrefix]
        public static bool Prefix_MixBombCheck(CreatePlant __instance, ref int theBoxColumn, ref int theBoxRow, ref bool __result)
        {
            List<Plant> plants = Lawnf.Get1x1Plants(theBoxColumn, theBoxRow).ToArray().ToList();
            foreach (var plant in plants)
            {
                if (plant == null) continue;
                if (CustomCore.CustomMixBombFusions.Any(kvp => kvp.Key.Item2 == plant.thePlantType))
                {
                    __result = true;
                    return false;
                }
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(CreateBullet))]
    public static class CreateBulletPatch
    {
        [HarmonyPatch(nameof(CreateBullet.SetBullet))]
        [HarmonyPrefix]
        public static void PreSetBullet(float x, float y, ref BulletType theBulletType, out (bool, BulletType, BulletType, PlantType) __state)
        {
            var colliders = Physics2D.OverlapCircleAll(new Vector2(x - 0.1f, y), 0.2f, LayerMask.GetMask("Plant"));
            foreach (var collider in colliders)
            {
                if (collider == null || collider.gameObject == null || collider.IsDestroyed() || collider.gameObject.IsDestroyed()) continue;
                if (!collider.TryGetComponent<Plant>(out var plant) || plant == null || plant.IsDestroyed()) continue;
                if (!GameAPP.resourcesManager.plantSkinDic.TryGetValue(plant.thePlantType, out var val)) continue;
                if (CustomCore.CustomBulletsSkinID.TryGetValue((plant.thePlantType, theBulletType, val), out var list))
                {
                    var ori = theBulletType;
                    theBulletType = list[UnityEngine.Random.Range(0, list.Count)];
                    __state = (true, ori, theBulletType, plant.thePlantType);
                    return;
                }
            }

            var circleColliders = Physics2D.OverlapCircleAll(new Vector2(x - 0.1f, y), 0.2f, LayerMask.GetMask("Bullet"));
            foreach (var collider in circleColliders)
            {
                if (collider == null || collider.gameObject == null || collider.IsDestroyed() || collider.gameObject.IsDestroyed()) continue;
                if (!collider.TryGetComponent<Bullet>(out var bullet) || bullet == null || bullet.IsDestroyed()) continue;
                if (bullet.GetData("SkinFromType") == null || bullet.GetData("SkinData") == null) continue;
                var pt = bullet.GetData<PlantType>("SkinFromType");
                if (!GameAPP.resourcesManager.plantSkinDic.TryGetValue(pt, out var val)) continue;
                if (CustomCore.CustomBulletsSkinID.TryGetValue((pt, theBulletType, val), out var list))
                {
                    var ori = theBulletType;
                    theBulletType = list[UnityEngine.Random.Range(0, list.Count)];
                    __state = (true, ori, theBulletType, pt);
                    return;
                }
            }

            var positions = PositionRecorder.GetRecordPositions(new Vector2(x - 0.1f, y), 0.1f);
            foreach (var item in positions)
            {
                if (!GameAPP.resourcesManager.plantSkinDic.TryGetValue(item.plantType, out var val)) continue;
                if (CustomCore.CustomBulletsSkinID.TryGetValue((item.plantType, theBulletType, val), out var list))
                {
                    var ori = theBulletType;
                    theBulletType = list[UnityEngine.Random.Range(0, list.Count)];
                    __state = (true, ori, theBulletType, item.plantType);
                    PositionRecorder.RemovePosition(item.index);
                    return;
                }
            }

            __state = (false, (BulletType)(-1), (BulletType)(-1), (PlantType)(-1));
        }

        [HarmonyPatch(nameof(CreateBullet.SetBullet))]
        [HarmonyPostfix]
        public static void PostSetBullet(ref Bullet __result, (bool, BulletType, BulletType, PlantType) __state)
        {
            if (__state.Item1)
            {
                __result.theBulletType = __state.Item2;
                __result.SetData("SkinData", __state.Item3);
                __result.SetData("SkinFromType", __state.Item4);
            }
        }
    }

    /// <summary>
    /// 子弹移动路径
    /// </summary>
    [HarmonyPatch(typeof(Bullet))]
    public static class BulletPatch
    {
        [HarmonyPatch(nameof(Bullet.Update))]
        [HarmonyPostfix]
        public static void PrePostionUpdate(Bullet __instance)
        {
            if (CustomCore.CustomBulletMovingWay.ContainsKey((int)__instance.MoveWay))
            {
                CustomCore.CustomBulletMovingWay[(int)__instance.MoveWay](__instance);
            }
        }

        [HarmonyPatch(nameof(Bullet.Die))]
        [HarmonyPrefix]
        public static void PreDie(Bullet __instance)
        {
            if (__instance.GetData("SkinData") != null)
            {
                PositionRecorder.AddPositonToList(__instance.transform.position, __instance.fromType);
                __instance.theBulletType = __instance.GetData<BulletType>("SkinData");
            }
        }
    }

    [HarmonyPatch(typeof(Lawnf))]
    public class LawnfPatch
    {
        [HarmonyPatch(nameof(Lawnf.GetUpgradedPlantCost))]
        [HarmonyPrefix]
        public static bool Prefix(ref PlantType thePlantType, ref int targetLevel, ref int __result)
        {
            if (CustomCore.CustomUltimatePlants.Contains(thePlantType))
            {
                __result = 1500 * (targetLevel) * (targetLevel + 1) / 2;
                return false;
            }
            return true;
        }

        [HarmonyPatch(nameof(Lawnf.IsUltiPlant))]
        [HarmonyPrefix]
        public static bool Prefix(ref PlantType thePlantType, ref bool __result)
        {
            if (CustomCore.CustomPlantTypes.Contains(thePlantType))
            {
                __result = CustomCore.CustomUltimatePlants.Contains(thePlantType);
                return false;
            }
            return true;
        }

        [HarmonyPatch(nameof(Lawnf.GetUltimatePlants))]
        [HarmonyPostfix]
        public static void Postfix(ref Il2CppSystem.Collections.Generic.List<PlantType> __result)
        {
            foreach (PlantType plantType in CustomCore.CustomUltimatePlants)
            {
                if (!__result.Contains(plantType))
                {
                    __result.Add(plantType);
                }
            }
        }

        [HarmonyPatch(nameof(Lawnf.GetName), new Type[] { typeof(PlantType) })]
        [HarmonyPrefix]
        public static bool PreGetName(PlantType thePlantType, ref string __result)
        {
            if (CustomCore.CustomPlantNames.ContainsKey(thePlantType))
            {
                __result = CustomCore.CustomPlantNames[thePlantType];
                return false;
            }
            return true;
        }

        [HarmonyPatch(nameof(Lawnf.GetName), new Type[] { typeof(ZombieType) })]
        [HarmonyPrefix]
        public static bool PreGetName_Zombie(ZombieType theZombieType, ref string __result)
        {
            if (CustomCore.CustomZombieNames.ContainsKey(theZombieType))
            {
                __result = CustomCore.CustomZombieNames[theZombieType];
                return false;
            }
            return true;
        }

        [HarmonyPatch(nameof(Lawnf.TravelAdvanced))]
        [HarmonyPostfix]
        public static void PostTravelAdvanced_0(ref AdvBuff buff, ref bool __result)
        {
            var result = MultiLevelBuff.IsMultiLevelBuff(BuffType.AdvancedBuff, (int)buff);
            if (!result.Item1)
                return;
            int index = result.Item2;
            if (TravelMgr.Instance == null)
                return;
            var array = TravelMgr.Instance.GetData<int[]>("CustomBuffsLevel");
            if (array is null)
                return;
            if (index < array.Length)
                __result = array[index] > 0;
        }

        [HarmonyPatch(nameof(Lawnf.TravelUltimate))]
        [HarmonyPostfix]
        public static void PostTravelUltimate_0(ref UltiBuff buff, ref bool __result)
        {
            var result = MultiLevelBuff.IsMultiLevelBuff(BuffType.UltimateBuff, (int)buff);
            if (!result.Item1)
                return;
            int index = result.Item2;
            if (TravelMgr.Instance == null)
                return;
            var array = TravelMgr.Instance.GetData<int[]>("CustomBuffsLevel");
            if (array is null)
                return;
            if (index < array.Length)
                __result = array[index] > 0;
        }

        [HarmonyPatch(nameof(Lawnf.TravelUltimateLevel))]
        [HarmonyPostfix]
        public static void PostTravelUltimateLevel(ref UltiBuff buff, ref int __result)
        {
            var result = MultiLevelBuff.IsMultiLevelBuff(BuffType.UltimateBuff, (int)buff);
            if (!result.Item1)
                return;
            int index2 = result.Item2;
            if (TravelMgr.Instance == null)
                return;
            var array = TravelMgr.Instance.GetData<int[]>("CustomBuffsLevel");
            if (array is null)
                return;
            if ((int)buff < array.Length)
                __result = array[index2];
        }

        [HarmonyPatch(nameof(Lawnf.TravelDebuff), new Type[] { typeof(TravelDebuff) })]
        [HarmonyPostfix]
        public static void PostTravelDebuff_1(ref TravelDebuff buff, ref bool __result)
        {
            var result = MultiLevelBuff.IsMultiLevelBuff(BuffType.Debuff, (int)buff);
            if (!result.Item1)
                return;
            int index = result.Item2;
            if (TravelMgr.Instance == null)
                return;
            var array = TravelMgr.Instance.GetData<int[]>("CustomBuffsLevel");
            if (array is null)
                return;
            if (index < array.Length)
                __result = array[index] > 0;
        }
    }

    [HarmonyPatch(typeof(Lawnf))]
    public static class LawnfPatch_BuffGet
    {
        [HarmonyPatch(nameof(Lawnf.TravelAdvanced), new Type[] { typeof(AdvBuff) })]
        [HarmonyPrefix]
        public static void PreTravelAdvanced_1(ref AdvBuff buff)
        {
            if (CustomCore.CustomBuffIDMapping.ContainsKey((BuffType.AdvancedBuff, (int)buff)))
                buff = (AdvBuff)CustomCore.CustomBuffIDMapping[(BuffType.AdvancedBuff, (int)buff)];
        }

        [HarmonyPatch(nameof(Lawnf.TravelUltimate), new Type[] { typeof(UltiBuff) })]
        [HarmonyPrefix]
        public static void PreTravelUltimate_1(ref UltiBuff buff)
        {
            if (CustomCore.CustomBuffIDMapping.ContainsKey((BuffType.UltimateBuff, (int)buff)))
                buff = (UltiBuff)CustomCore.CustomBuffIDMapping[(BuffType.UltimateBuff, (int)buff)];
        }

        [HarmonyPatch(nameof(Lawnf.TravelDebuff), new Type[] { typeof(TravelDebuff) })]
        [HarmonyPrefix]
        public static void PreTravelDebuff_1(ref TravelDebuff buff)
        {
            if (CustomCore.CustomBuffIDMapping.ContainsKey((BuffType.Debuff, (int)buff)))
                buff = (TravelDebuff)CustomCore.CustomBuffIDMapping[(BuffType.Debuff, (int)buff)];
        }
    }

    /// <summary>
    /// 点击其他Button，隐藏二创植物界面
    /// </summary>
    [HarmonyPatch(typeof(UIButton))]
    public static class HideCustomPlantCards
    {
        [HarmonyPatch(nameof(UIButton.OnMouseUpAsButton))]
        [HarmonyPostfix]
        public static void PostfixStart(UIButton __instance)
        {
            if (SelectCustomPlants.Instance != null && SelectCustomPlants.CustomPage != null && SelectCustomPlants.CustomPage.activeSelf)
            {
                SelectCustomPlants.CustomPage.SetActive(false);
            }
        }
    }

    [HarmonyPatch(typeof(InGameUI))]
    public static class InGameUIPatch
    {
        [HarmonyPatch(nameof(InGameUI.SetUniqueText))]
        [HarmonyPostfix]
        public static void PostSetUniqueText(InGameUI __instance, ref Il2CppSystem.Collections.Generic.List<TextMeshProUGUI> T)
        {
            if (GameAPP.theBoardType is (LevelType)66)
            {
                __instance.ChangeString(T, CustomCore.CustomLevels[GameAPP.theBoardLevel].Name());
            }
        }

        [HarmonyPatch(nameof(InGameUI.MoveCardToTarget))]
        [HarmonyPrefix]
        public static void PreMoveCardToTarget(ref CardUI card)
        {
            foreach (CheckCardState check in CustomCore.checkBehaviours)
            {
                if (check != null)
                {
                    check.movingCardUI = card;
                    check.CheckState();
                }
            }
        }

        [HarmonyPatch(nameof(InGameUI.RemoveCardFromBank))]
        [HarmonyPostfix]
        public static void PostReMoveCardFromBank(ref CardUI card)
        {
            foreach (CheckCardState check in CustomCore.checkBehaviours)
            {
                if (check != null)
                {
                    check.movingCardUI = card;
                    check.CheckState();
                }
            }
        }
    }

    [HarmonyPatch(typeof(InitBoard))]
    public static class InitBoardPatch
    {
        [HarmonyPatch(nameof(InitBoard.PreSelectCard))]
        [HarmonyPostfix]
        public static void PostPreSelectCard(InitBoard __instance)
        {
            if (GameAPP.theBoardType is (LevelType)66)
            {
                foreach (var c in CustomCore.CustomLevels[GameAPP.theBoardLevel].PreSelectCards())
                {
                    __instance.PreSelect(c);
                }
            }
        }
    }

    /// <summary>
    /// 自定义关卡（theBoardType == LevelType 66）的词条注入：把该关卡配置的
    /// AdvBuffs / UltiBuffs / Debuffs 写进 TravelMgr.data。
    ///
    /// ★ 为什么挂点要按平台分流（跟进上游 CustomizeLib 的「修复选卡过场 UniTask 异常」）
    ///   原实现挂在 <c>InitBoard.RightMoveCamera(float)</c>，而该方法的返回类型是
    ///   <c>Cysharp.Threading.Tasks.UniTask</c> —— Harmony 生成的托管跳板必须把这个
    ///   IL2CPP 返回值原样封送回 native 调用方。安卓的 CoreCLR 启动器做不到这一点，
    ///   结果是进选卡过场时直接崩（上游为此把同一段逻辑改挂 <c>InitBoard.Awake</c>）。
    ///   两个方法的原生地址都是**独占**的（RightMoveCamera VA 0x1809F5180、
    ///   Awake VA 0x1809F1610，全反编译源码各只有 1 个成员引用，不是空方法体合并 thunk），
    ///   所以按平台换挂点在结构上是安全的。
    ///   PC 侧继续挂在 RightMoveCamera：**时序与改动前逐字一致**，不引入任何行为变化。
    ///
    /// ★ 安卓路径的额外兜底
    ///   Awake 比 RightMoveCamera 早得多，那时 TravelMgr.data 很可能还没建好。
    ///   所以安卓路径下若当帧拿不到 data，会启动一个**有上限（120 帧 ≈ 2 秒）**的协程重试，
    ///   拿到即注入并退出；始终拿不到就打一条 LogWarning 说明原因（失败路径必须留痕）。
    /// </summary>
    [HarmonyPatch]
    [HarmonyPriority(Priority.First)]
    internal static class InitBoardCustomLevelBuffPatch
    {
        /// <summary>安卓改挂 Awake（UniTask 封送问题）；PC 保持 RightMoveCamera。静态只算一次，两处共用同一个真值。</summary>
        private static readonly bool UseAwakeHook = Application.platform == RuntimePlatform.Android;

        private static MethodBase TargetMethod()
        {
            if (UseAwakeHook)
            {
                var awake = AccessTools.Method(typeof(InitBoard), nameof(InitBoard.Awake));
                if (awake != null)
                    return awake;

                CustomCore.CLogger.LogWarning(
                    "[前置库] 安卓模式下未能解析 InitBoard.Awake，已回退到 RightMoveCamera —— 可能重现 UniTask 封送异常。");
            }

            return AccessTools.Method(typeof(InitBoard), nameof(InitBoard.RightMoveCamera));
        }

        [HarmonyPostfix]
        private static void Postfix()
        {
            if (GameAPP.theBoardType is not (LevelType)66) return;

            if (ApplyCustomLevelBuffs()) return;

            // 只有安卓（Awake 挂点）才可能在这里拿不到 data —— PC 的 RightMoveCamera 时机上 data 已就绪，
            // 保持"拿不到就静默返回"的原行为，不给 PC 引入任何新的时序。
            if (!UseAwakeHook) return;

            var app = GameAPP.Instance;
            if (app != null)
                app.StartCoroutine(DeferredApplyCustomLevelBuffs());
        }

        /// <summary>注入本体。返回 true 表示"已完成或本来就没什么可做"，false 表示"数据还没就绪、可以稍后重试"。</summary>
        private static bool ApplyCustomLevelBuffs()
        {
            // CustomCore.CustomLevels 是 List<CustomLevelData>、按关卡号取下标（原实现就是 CustomLevels[level]），
            // 所以这里判的是下标越界而不是字典缺键。越界 = 该关卡没注册自定义数据 ⇒ 无词条可注入，按已完成处理。
            var levels = CustomCore.CustomLevels;
            var level = GameAPP.theBoardLevel;
            if (levels == null || level < 0 || level >= levels.Count) return true;

            var levelData = levels[level];

            var app = GameAPP.Instance;
            if (app == null) return false;

            var travelMgr = app.GetOrAddComponent<TravelMgr>();
            var data = travelMgr?.data;
            if (data == null) return false;

            foreach (var a in levelData.AdvBuffs())
            {
                if (a >= 0)
                {
                    data.advBuffs.Add((AdvBuff)a);
                }
            }
            foreach (var u in levelData.UltiBuffs())
            {
                if (u.Item1 >= 0 && u.Item2 >= 0)
                {
                    data.ultiBuffs.Add((UltiBuff)u.Item1);
                    if (u.Item2 > 1)
                        data.ultiBuffs_lv2.Add((UltiBuff)u.Item1);
                }
            }
            foreach (var d in levelData.Debuffs())
            {
                if (d >= 0)
                {
                    data.travelDebuffs.Add((TravelDebuff)d);
                }
            }

            return true;
        }

        /// <summary>安卓路径的兜底重试：最多 120 帧（≈2 秒），拿到 TravelMgr.data 立即注入并退出。</summary>
        private static IEnumerator DeferredApplyCustomLevelBuffs()
        {
            for (var i = 0; i < 120; i++)
            {
                if (GameAPP.theBoardType is not (LevelType)66) yield break;
                if (ApplyCustomLevelBuffs()) yield break;
                yield return null;
            }

            CustomCore.CLogger.LogWarning(
                "[前置库] 自定义关卡（LevelType 66）的词条注入未在 2 秒内完成，已放弃：TravelMgr.data 始终为空。");
        }
    }

    [HarmonyPatch(typeof(InitZombieList))]
    public static class InitZombieListAllowZombiePatch
    {
        [HarmonyPatch(nameof(InitZombieList.PickZombie))]
        [HarmonyPrefix]
        public static void PrePickZombie()
        {
            if (Utils.IsCustomLevel(out var levelData))
            {
                foreach (var z in levelData.ZombieList())
                    InitZombieList.zombieToSpawns.Add(z);
            }
        }
    }

    /// <summary>
    /// 花钱开大招
    /// </summary>
    [HarmonyPatch(typeof(Money))]
    public static class MoneyPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(Money.ReinforcePlant))]
        public static bool PreReinforcePlant(Money __instance, ref Plant plant)
        {
            if (CustomCore.SuperSkills.ContainsKey(plant.thePlantType))
            {
                var cost = CustomCore.SuperSkills[plant.thePlantType].Item1(plant);//实时计算大招花费

                if (Board.Instance.theMoney < cost)//如果钱不够
                {
                    InGameText.Instance.ShowText($"大招需要{cost}金币", 5);//提示
                    return false;//直接返回
                }

                if (plant.SuperSkill())
                {
                    CustomCore.SuperSkills[plant.thePlantType].Item2(plant);//执行大招代码
                    plant.AnimSuperShoot();
                    __instance.UsedEvent(plant.thePlantColumn, plant.thePlantRow, cost);
                    __instance.OtherSuperSkill(plant);
                }

                return false;
            }

            return true;
        }
    }
    [HarmonyPatch(typeof(Mouse))]
    public static class MousePatch
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(Mouse.GetPlantsOnMouse))]
        public static void PostGetPlantsOnMouse(Mouse __instance, ref Il2CppSystem.Collections.Generic.List<Plant> __result)
        {
            // ★ IZ 点击兼容（跟进上游 CustomizeLib「完善IZ点击兼容」）：
            //   IZ 模式下不能动「鼠标底下有哪些植物」这份清单，否则游戏自己的 IZ 点击判定会失效。
            if (__instance.board != null && __instance.board.boardTag.isIZ)
                return;

            PostGetPlantsOnMouseCore(ref __result);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void PostGetPlantsOnMouseCore(ref Il2CppSystem.Collections.Generic.List<Plant> result)
        {
            for (int i = result.Count - 1; i >= 0; i--)
            {
                if (result.ToArray()[i] != null && TypeMgr.BigNut(result.ToArray()[i].thePlantType))
                {
                    result.RemoveAt(i);
                }
            }
        }

        [HarmonyPatch(nameof(Mouse.Update))]
        [HarmonyPrefix]
        public static bool PreMouseClick(Mouse __instance)
        {
            if (!Input.GetMouseButtonDown(0))
                return true;
            if (__instance.theItemOnMouse == null)
                return true;
            var list = new List<Plant>();
            Vector3 worldPosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector2 rayPosition = new Vector2(worldPosition.x, worldPosition.y);

            // 从鼠标位置发射射线检测碰撞
            foreach (var hit in Physics2D.RaycastAll(rayPosition, Vector2.zero))
            {
                if (hit.collider == null || hit.collider.gameObject == null || hit.collider.gameObject.IsDestroyed())
                    continue;
                if (!hit.collider.gameObject.TryGetComponent<Plant>(out var plant))
                    continue;
                if (plant == null)
                    continue;
                list.Add(plant);
            }
            if (list.Count <= 0)
                return true;
            bool found = false;
            bool clear = false;
            List<Action<Plant>> executedActions = [];
            foreach (var item in list)
            {
                if (item == null)
                    continue;
                if (__instance.thePlantOnGlove != null && item == __instance.thePlantOnGlove)
                    continue;
                if (CustomCore.CustomClickCardOnPlantEvents.ContainsKey((item.thePlantType, __instance.thePlantTypeOnMouse)))
                {
                    bool block = false, clearOrigin = false;
                    foreach (var (action, can, onPlant) in CustomCore.CustomClickCardOnPlantEvents[(item.thePlantType, __instance.thePlantTypeOnMouse)])
                    {
                        if (executedActions.Contains(action)) // 判断，不然会多执行一次
                            continue;
                        if (can != null && !can(item))
                            continue;
                        if (onPlant.Trigger == CustomClickCardOnPlant.TriggerType.CardOnly && __instance.thePlantOnGlove != null)
                            continue;
                        if (onPlant.Trigger == CustomClickCardOnPlant.TriggerType.GloveOnly && __instance.thePlantOnGlove == null)
                            continue;
                        action(item);
                        executedActions.Add(action);
                        if (onPlant.BlockFusion)
                            block = true;
                        if (!onPlant.SaveOrigin)
                            clearOrigin = true;
                        found = true;
                    }
                    if (block)
                    {
                        return false;
                    }
                    if (clearOrigin)
                    {
                        clear = true;
                    }
                }
            }
            if (found && clear)
            {
                if (__instance.theCardOnMouse != null)
                {
                    if (__instance.theCardOnMouse.TryGetComponent<DroppedCard>(out var card) && card != null)
                    {
                        card.usedTimes++;
                        if (Board.Instance != null)
                        {
                            Board.Instance.UseSun(card.theSeedCost);

                            // 高级旅行检查
                            if (Lawnf.TravelAdvanced((AdvBuff)5004))
                            {
                                Board.Instance.UseSun(Board.Instance.theSun / 2);
                            }
                        }
                    }
                    else
                    {
                        __instance.theCardOnMouse.CD = 0f;
                        __instance.theCardOnMouse.isPickUp = false;
                        if (Board.Instance != null)
                        {
                            Board.Instance.UseSun(__instance.theCardOnMouse.theSeedCost);

                            // 高级旅行检查
                            if (Lawnf.TravelAdvanced((AdvBuff)5004))
                            {
                                Board.Instance.UseSun(Board.Instance.theSun / 2);
                            }
                        }
                    }
                }
                if (__instance.thePlantOnGlove != null)
                {
                    __instance.thePlantOnGlove.Die(Plant.DieReason.ByShovel);
                    Glove glove = Glove.Instance;
                    if (glove != null)
                    {
                        float gloveCD = Lawnf.GetGloveCD();
                        glove.fullCD = gloveCD;
                        glove.CD = 0f;

                        // 特殊植物类型冷却时间调整
                        if (TypeMgr.IsPuff(__instance.thePlantTypeOnMouse) || TypeMgr.IsPot(__instance.thePlantTypeOnMouse) ||
                            TypeMgr.IsLily(__instance.thePlantTypeOnMouse) || TypeMgr.FlyingPlants(__instance.thePlantTypeOnMouse))
                        {
                            glove.CD = (glove.fullCD + glove.fullCD) / 3f;
                        }
                    }
                }
                Destroy(__instance.theItemOnMouse);
                __instance.ClearItemOnMouse(false);
            }
            if (!clear)
                return true;
            return !found;
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(Mouse.LeftClickWithNothing))]
        public static void PostLeftClickWithNothing(Mouse __instance)
        {
            // ★ IZ 点击兼容：IZ 模式下不派发二创植物的自定义点击事件（同上游）。
            if (__instance.board != null && __instance.board.boardTag.isIZ)
                return;

            PostLeftClickWithNothingCore();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void PostLeftClickWithNothingCore()
        {
            foreach (GameObject gameObject in (List<GameObject>)[..from RaycastHit2D raycastHit2D in
                                           (RaycastHit2D[])Physics2D.RaycastAll(Camera.main.ScreenToWorldPoint(Input.mousePosition),
                                           Vector2.zero) select raycastHit2D.collider.gameObject])
            {
                if (gameObject.TryGetComponent<Plant>(out var plant) && CustomCore.CustomPlantClicks.ContainsKey(plant.thePlantType))
                {
                    CustomCore.CustomPlantClicks[plant.thePlantType](plant);
                    return;
                }
            }
        }
    }

    [HarmonyPatch(typeof(GameAPP))]
    public static class GameAPPPatch
    {
        [HarmonyPatch(nameof(GameAPP.Awake))]
        [HarmonyPostfix]
        public static void PostAwake()
        {
            InterfaceCreator.InitInstance();
        }

        [HarmonyPatch(nameof(GameAPP.Start))]
        [HarmonyPostfix]
        public static void PostStart(GameAPP __instance)
        {
            if (!PatchMgr.TravelData.load)
            {
                PatchMgr.TravelData.SetBuffArr();
                PatchMgr.TravelData.load = true;
            }
            __instance.StartCoroutine(CoreTools.Init());

            // 触发游戏启动事件
            EventListenr.Trigger(ListenerType.OnGameLaunch);
        }

        [HarmonyPatch(nameof(GameAPP.LoadResources))]
        [HarmonyPrefix]
        public static void Prefix()
        {
            try
            {
                #region 自动扩容
                // 扩容particlePrefab
                if (CustomCore.CustomParticles.Count > 0 && (int)CustomCore.CustomParticles.Keys.DefaultIfEmpty().Max() + 1 >= GameAPP.particlePrefab.Length)
                {
                    long size_particlePrefab = (int)CustomCore.CustomParticles.Keys.DefaultIfEmpty().Max();
                    Il2CppReferenceArray<GameObject> particlePrefab = new Il2CppReferenceArray<GameObject>(size_particlePrefab + 1);
                    GameAPP.particlePrefab = particlePrefab;
                }

                // 扩容spritePrefab
                if (CustomCore.CustomSprites.Count > 0 && CustomCore.CustomSprites.Keys.DefaultIfEmpty().Max() + 1 >= GameAPP.spritePrefab.Length)
                {
                    long size_spritePrefab = CustomCore.CustomSprites.Keys.Max();
                    Il2CppReferenceArray<Sprite> spritePrefab = new Il2CppReferenceArray<Sprite>(size_spritePrefab + 1);
                    GameAPP.spritePrefab = spritePrefab;
                }
                #endregion
            }
            catch (InvalidOperationException) { }
            foreach (var plant in CustomCore.CustomPlants)//二创植物
            {
                GameAPP.resourcesManager.plantPrefabs[plant.Key] = plant.Value.Prefab;//注册预制体
                GameAPP.resourcesManager.plantPrefabs[plant.Key].tag = "Plant";//必须打tag
                if (!GameAPP.resourcesManager.allPlants.Contains(plant.Key))
                    GameAPP.resourcesManager.allPlants.Add(plant.Key);//注册植物类型
                if (plant.Value.PlantData is not null)
                {
                    // ★ 必须用索引器、不能用 Add（4.0.5）：Add 在同键已存在时会抛
                    //   ArgumentException: An item with the same key has already been added。
                    //   而 4.0.5 的 PlantDataManager.Init() 会在进关卡时清空并重建这张表，
                    //   本库在它的 Postfix 里已把二创植物登记回去（见 Patch/SetPlant405Compat.cs）；
                    //   此后 LoadResources 再跑一次时键已存在，索引器写入是幂等的，不会炸。
                    PlantDataManager.PlantData_Default[plant.Key] = plant.Value.PlantData;//注册植物数据
                }
                GameAPP.resourcesManager.plantPreviews[plant.Key] = plant.Value.Preview;//注册植物预览
                GameAPP.resourcesManager.plantPreviews[plant.Key].tag = "Preview";//必修打tag
                // 先判存在再 Add：索引器会把 true 覆盖成 false（重新锁上已解锁植物），
                // 而同键 Add 又会抛异常 —— 两种写法各有一个坑，所以要这一句守卫。
                if (!PlantDataManager.unlocked.ContainsKey(plant.Key))
                    PlantDataManager.unlocked.Add(plant.Key, false);
            }
            foreach (var f in CustomCore.CustomFusions)
            {
                MixData.AddOrderedRecipe((PlantType)f.Item2, (PlantType)f.Item3, (PlantType)f.Item1);
            }

            foreach (var z in CustomCore.CustomZombies)//注册二创僵尸
            {
                if (!GameAPP.resourcesManager.allZombieTypes.Contains(z.Key))
                    GameAPP.resourcesManager.allZombieTypes.Add(z.Key);//注册僵尸类型
                GameAPP.resourcesManager.zombiePrefabs[z.Key] = z.Value.Item1;//注册僵尸预制体
                GameAPP.resourcesManager.zombiePrefabs[z.Key].layer = LayerMask.NameToLayer("Zombie"); // 改层级
                GameAPP.resourcesManager.zombiePrefabs[z.Key].tag = "Zombie";//必修打tag
                InitZombieList.allowAllzombies.Add(z.Key);
                if (z.Value.Item2 != null)
                    GameAPP.resourcesManager.zombieSprites[z.Key] = z.Value.Item2;
            }

            // 先注册二创子弹，再注册皮肤，不然注册二创子弹皮肤会出bug
            foreach (var bullet in CustomCore.CustomBullets)//注册二创子弹
            {
                GameAPP.resourcesManager.bulletPrefabs[bullet.Key] = bullet.Value;//注册子弹预制体
                if (!GameAPP.resourcesManager.allBullets.Contains(bullet.Key))
                    GameAPP.resourcesManager.allBullets.Add(bullet.Key);//注册子弹类型
            }

            foreach (var (id, list) in CustomCore.CustomSkinBullet) //注册二创皮肤子弹
            {
                foreach (var (newBulletID, bullet) in list)
                {
                    if (bullet == null) continue;
                    foreach (var comp in GameAPP.resourcesManager.bulletPrefabs[id].GetComponents<Component>())
                        if (bullet != null && !bullet.TryGetComponent(comp.GetIl2CppType(), out var cmp) && cmp == null)
                            bullet.AddComponent(comp.GetIl2CppType());
                    bullet.GetComponent<Bullet>().theBulletType = id;
                    GameAPP.resourcesManager.bulletPrefabs[newBulletID] = bullet;
                    if (!GameAPP.resourcesManager.allBullets.Contains(newBulletID))
                        GameAPP.resourcesManager.allBullets.Add(newBulletID);
                }
            }

            foreach (var par in CustomCore.CustomParticles)//注册粒子效果
            {
                GameAPP.particlePrefab[(int)par.Key] = par.Value;
                GameAPP.resourcesManager.particlePrefabs[par.Key] = par.Value;//注册粒子效果预制体
                if (!GameAPP.resourcesManager.allParticles.Contains(par.Key))
                    GameAPP.resourcesManager.allParticles.Add(par.Key);//注册粒子效果类型
            }

            foreach (var spr in CustomCore.CustomSprites)//注册自定义精灵贴图
            {
                GameAPP.spritePrefab[spr.Key] = spr.Value;
            }

            // 把键的index加上prefabs的Count得到新的实际Index
            // CustomCore 的注册表已改为只读属性（{ get; }），不能再整体替换引用。
            // 改成「先算出新字典，再原地 Clear + 回填」。顺序不能颠倒 ——
            // 先 Clear 会把正在被读取的数据源本身清空。
            var newBulletsSkinID = CustomCore.CustomBulletsSkinID.ToDictionary(kvp =>
                (kvp.Key.pt, kvp.Key.oriBulletType, 
                kvp.Key.index + (GameAPP.resourcesManager._plantPrefabs.TryGetValue(kvp.Key.pt, out var list) ? list.Count : 0)), // 如果有，用列表的长度，否则用0
                kvp => kvp.Value);
            CustomCore.CustomBulletsSkinID.Clear();
            foreach (var kvp in newBulletsSkinID)
                CustomCore.CustomBulletsSkinID[kvp.Key] = kvp.Value;

            GameAPP.Instance.StartCoroutine(PatchMgr.RegisterSkin()); // 在所有注册完成之后启动皮肤协程
        }

        [HarmonyPatch(nameof(GameAPP.LoadResources))]
        [HarmonyPostfix]
        public static void PostLoadResources()
        {
            foreach (var audio in CustomCore.CustomSounds) // 注册自定义音效
            {
                GameAPP.soundManager.sounds.Add((SoundType)audio.Key, audio.Value);
            }

            foreach (var music in CustomCore.CustomMusics) // 注册自定义音乐
            {
                GameAPP.soundManager.musics.Add(music.Key, music.Value);
                SoundManager.MusicNames.Add(music.Key, music.Key.ToString());
            }
        }
    }

    [HarmonyPatch(typeof(UIMgr), nameof(UIMgr.EnterMainMenu))]
    public static class NoticeMenuPatch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            try
            {
                if (PatchMgr.Load) return;
                var behaviour = new GameObject("CustomCore Behaviour");
                behaviour.AddComponent<PositionRecorder>();
                behaviour.transform.SetParent(null);
                DontDestroyOnLoad(behaviour);

                // 注册红卡
                //
                // 2026-10-01 适配 4.0.5：这里原本还有一句 propertyInfo.SetValue(null, redPlant)，
                // 在 4.0.5 上会抛 ArgumentException「Property set method not found」，因为游戏把
                // TypeMgr.RedPlant / TypeMgr.UncrashablePlants 从**静态字段**改成了**只读属性**：
                //   4.0   public static readonly HashSet<PlantType> RedPlant;            （字段，偏移 0x48）
                //   4.0.5 public static HashSet<PlantType> RedPlant { get; }               （属性，只有 getter）
                // 而 4.0.5 的 getter 实现（IDA TypeMgr$$get_RedPlant @0x1807FE710）是
                //     return TypeConfiguration::GetPlants(27, 0);
                // GetPlants 内部读的是**静态缓存**字典 TypeConfiguration_TypeInfo->static_fields->plantGroups
                // 再 Dictionary::get_Item(category) —— 返回的是字典里那个 HashSet 的**同一引用**。
                // ⇒ 拿到的集合直接 Add 就生效，SetValue 从来都是多余的（4.0 时它是 static readonly 字段，
                //   GetValue 拿到的本来就是字段本身，赋回去是 no-op），现已删除。
                {
                    var propertyInfo = typeof(TypeMgr).GetProperty("RedPlant", BindingFlags.Static | BindingFlags.Public);
                    if (propertyInfo is null)
                        return;
                    var value = propertyInfo.GetValue(null);
                    if (value is null)
                        return;
                    var redPlant = (Il2CppSystem.Collections.Generic.HashSet<PlantType>)value;
                    foreach (var (k, v) in CustomCore.TypeMgrExtra.LevelPlants)
                        if (v == CardLevel.Red)
                            redPlant.Add(k);
                }
                // 注册防碾压植物（同样见上：4.0.5 起 UncrashablePlants 是只读属性，getter 走
                // TypeConfiguration::GetPlants(26, 0)，TypeMgr$$get_UncrashablePlants @0x1807FE760）
                {
                    var propertyInfo = typeof(TypeMgr).GetProperty("UncrashablePlants", BindingFlags.Static | BindingFlags.Public);
                    if (propertyInfo is null)
                        return;
                    var value = propertyInfo.GetValue(null);
                    if (value is null)
                        return;
                    var uncrashablePlants = (Il2CppSystem.Collections.Generic.HashSet<PlantType>)value;
                    foreach (var item in CustomCore.TypeMgrExtra.UncrashablePlants)
                        uncrashablePlants.Add(item);
                }

                PatchMgr.Load = true;
                foreach (var action in CorePlugin.OnGameInitAction)
                    action.Invoke();
            }
            finally
            {
                PatchMgr.Load = true;
            }
        }
    }

    [HarmonyPatch]
    public static class OptionMenuPatch
    {
        [HarmonyTargetMethod]
        public static MethodBase GetTargetMethod()
        {
            foreach (var type in typeof(OptionMenu).GetNestedTypes(BindingFlags.Public | BindingFlags.Instance))
            {
                var method = type.GetMethod("_OnLockAlmanacMenu_b__0", BindingFlags.Public | BindingFlags.Instance);
                if (method != null) return method;
            }
            return null;
        }

        [HarmonyPostfix]
        public static void PostOnLockAlmanacMenu()
        {
            foreach (var pt in GameAPP.resourcesManager.allPlants)
            {
                if (!GameAPP.config.meetPlant_runTime.Contains(pt))
                    GameAPP.config.meetPlant_runTime.Add(pt);
            }
        }
    }

    [HarmonyPatch(typeof(Plant))]
    public static class PlantPatch
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(Plant.UseItem))]
        public static void PostUseItem(Plant __instance, ref BucketType type, ref Bucket bucket)
        {
            if (CustomCore.CustomUseItems.ContainsKey((__instance.thePlantType, type)))
            {
                CustomCore.CustomUseItems[(__instance.thePlantType, type)](__instance);
                UnityEngine.Object.Destroy(bucket.gameObject);
            }
        }

        [HarmonyPatch(nameof(Plant.Start))]
        [HarmonyPostfix]
        public static void PostStart(Plant __instance)
        {
            if (__instance != null && CustomCore.CustomOnMixEvent.ContainsKey((__instance.firstParent, __instance.secondParent)))
            {
                foreach (var action in CustomCore.CustomOnMixEvent[(__instance.firstParent, __instance.secondParent)])
                    action.Invoke(__instance);
            }
        }
    }

    /// <summary>
    /// 显示自定义卡
    /// </summary>
    [HarmonyPatch(typeof(SeedLibrary))]
    public static class SeedLibraryPatch
    {
        [HarmonyPatch(nameof(SeedLibrary.Awake))]
        [HarmonyPriority(Priority.Last)]
        [HarmonyPostfix]
        public static void PostAwake(SeedLibrary __instance)
        {
            SelectCustomPlants.InitButton();
            // 注册自定义卡牌
            PatchMgr.ShowCustomCards(__instance);
        }
    }

    /// <summary>
    /// 显示自定义卡
    /// </summary>
    [HarmonyPatch(typeof(PlantCardPackageBuilder))]
    public static class PlantCardPackageBuilderPatch
    {
        [HarmonyPatch(nameof(PlantCardPackageBuilder.Start))]
        [HarmonyPriority(Priority.Last)]
        [HarmonyPostfix]
        public static void PostStart(PlantCardPackageBuilder __instance)
        {
            SelectCustomPlants.InitButton();
            // 注册自定义卡牌
            PatchMgr.ShowCustomCards(__instance);
        }
    }

    [HarmonyPatch(typeof(Board))]
    public static class Board_Patch
    {
        [HarmonyPatch(nameof(Board.Start))]
        [HarmonyPostfix]
        public static void PostStart()
        {
            if (TravelMgr.Instance == null)
                return;
            if (TravelMgr.Instance.GetData("LoadByEndless") is null)
                TravelMgr.Instance.SetData("LoadByEndless", false);
            if ((TravelMgr.Instance.GetData("CustomBuffsLevel") is null ||
                (TravelMgr.Instance.GetData("CustomBuffsLevel") != null && TravelMgr.Instance.GetData<int[]>("CustomBuffsLevel").SequenceEqual(new int[CustomCore.CustomAdvancedBuffs.Count]))) &&
                !TravelMgr.Instance.GetData<bool>("LoadByEndless"))
            {
                TravelMgr.Instance.SetData("CustomBuffsLevel", new int[CustomCore.CustomAdvancedBuffs.Count]);
            }
        }

        [HarmonyPatch(nameof(Board.OnDestroy))]
        [HarmonyPostfix]
        public static void PostOnDestroy()
        {
            try
            {
                if (TravelMgr.Instance == null)
                    return;
                if (TravelMgr.Instance.GetData("LoadByEndless") is null)
                    TravelMgr.Instance.SetData("LoadByEndless", false);
                if ((TravelMgr.Instance.GetData("CustomBuffsLevel") is null ||
                    (TravelMgr.Instance.GetData("CustomBuffsLevel") != null && TravelMgr.Instance.GetData<int[]>("CustomBuffsLevel").SequenceEqual(new int[CustomCore.CustomAdvancedBuffs.Count]))) &&
                    !TravelMgr.Instance.GetData<bool>("LoadByEndless"))
                {
                    TravelMgr.Instance.SetData("CustomBuffsLevel", new int[CustomCore.CustomAdvancedBuffs.Count]);
                }
            }
            catch { }
        }

        [HarmonyPatch(nameof(Board.Update))]
        [HarmonyPostfix]
        public static void PostUpdate()
        {
            if (TravelMgr.Instance == null)
                return;
            try
            {
                var array = (int[])TravelMgr.Instance.GetData("CustomBuffsLevel");
                if (array is null)
                    return;
                foreach (var (key, value) in CustomCore.CustomBuffsLevel)
                {
                    var result = MultiLevelBuff.IsMultiLevelBuff(key.Item1, key.Item2);
                    if (!result.Item1)
                        continue;
                    int index = result.Item2;
                    if (index >= array.Length)
                        continue;
                    var data = TravelMgr.Instance.data;
                    var id = new BuffID(key.Item2);
                    switch (key.Item1)
                    {
                        case BuffType.AdvancedBuff:
                            {
                                if (!data.advBuffs.Contains(id))
                                    array[index] = 0;
                                if (array[index] <= 0 && data.advBuffs.Contains(id))
                                    array[index] = 1;
                            }
                            break;
                        case BuffType.UltimateBuff:
                            {
                                if (!data.ultiBuffs.Contains(id) && !data.ultiBuffs_lv2.Contains(id))
                                    array[index] = 0;
                                if (array[index] <= 0 && data.ultiBuffs.Contains(id))
                                    array[index] = 1;
                                if (array[index] <= 0 && data.ultiBuffs_lv2.Contains(id))
                                    array[index] = 2;
                            }
                            break;
                        case BuffType.Debuff:
                            {
                                if (!data.travelDebuffs.Contains(id))
                                    array[index] = 0;
                                if (array[index] <= 0 && data.travelDebuffs.Contains(id))
                                    array[index] = 1;
                            }
                            break;
                        case BuffType.UnlockPlant:
                            {
                                if (!data.unlockedPlants.Contains(id))
                                    array[index] = 0;
                                if (array[index] <= 0 && data.unlockedPlants.Contains(id))
                                    array[index] = 1;
                            }
                            break;
                    }
                    TravelMgr.Instance.SetData("CustomBuffsLevel", array);
                }
            }
            catch (ArgumentException) { }
        }

        [HarmonyPatch(nameof(Board.WheatLimit))]
        [HarmonyPrefix]
        public static bool PreWheatLimit(ref PlantType plantType, ref bool __result)
        {
            if (CustomCore.CustomUltimatePlants.Contains(plantType))
            {
                __result = true;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(BoardAction))]
    public static class BoardActionPatch
    {
        [HarmonyPatch(nameof(BoardAction.CreateCherryExplode))]
        [HarmonyPrefix]
        public static bool PreCreateCherryExplode(Board __instance, ref Vector2 v, ref int theRow,
            ref CherryBombType bombType, ref int damage, ref PlantType fromType, ref Il2CppSystem.Action<Zombie> action, ref bool immediately, ref BombCherry __result)
        {
            if (CustomCore.CustomCherrys.ContainsKey(bombType) && __instance != null)
            {
                CreateParticle.SetParticle(CustomCore.CustomCherryStartID + (int)bombType, v, 11);
                ScreenShake.TriggerShake(0.15f);
                GameAPP.PlaySound(40, 0.5f, 1.0f);

                BombCherry cherry = new BombCherry();
                cherry.board = __instance;
                cherry.damageToZombie = damage;
                cherry.bombRow = theRow;
                cherry.bombType = bombType;
                cherry.zombieAction = action;
                cherry.bombPosition = v;
                cherry.fromType = fromType;
                cherry.targetPlant = null;

                if (immediately)
                {
                    cherry.Explode(CustomDamageMaker.DamageMaker);
                }

                __result = cherry;
                return false;
            }
            return true;
        }
    }

    /// <summary>
    /// 二创词条文本染色
    /// </summary>
    [HarmonyPatch(typeof(TravelBuffOptionButton))]
    public static class TravelBuffOptionButtonPatch
    {
        [HarmonyPatch(nameof(TravelBuffOptionButton.SetBuff))]
        [HarmonyPrefix]
        public static void PreSetBuff(TravelBuffOptionButton __instance, Il2CppSystem.Object buff)
        {
            __instance.GeneralSet(buff);
        }

        /// <summary>
        /// 强究词条显示植物修复
        /// </summary>
        [HarmonyPatch(nameof(TravelBuffOptionButton.SetPlant), new Type[] { })]
        [HarmonyPostfix]
        public static void PostSetPlant(TravelBuffOptionButton __instance)
        {
            var (buffType, buffIndex) = __instance.TryGetTypeAndID();
            var list = CustomCore.CustomUltimateBuffs.
                Where(kvp => kvp.Key == buffIndex).
                ToList();
            if (buffType == BuffType.UltimateBuff && list.Count > 0)
            {
                foreach (var value in list)
                {
                    if (value.Value.Item1 == PlantType.Nothing)
                        __instance.SetPlant(PlantType.EndoFlame);
                    else
                        __instance.SetPlant(value.Value.Item1);
                }
            }
        }
    }

    [HarmonyPatch(typeof(TravelBuffOptionButton))]
    public static class TravelBuffOptionButtonIconPatch
    {
        [HarmonyPatch(nameof(TravelBuffOptionButton.SetBuff))]
        [HarmonyPrefix]
        public static void PreSetBuff(TravelBuffOptionButton __instance, Il2CppSystem.Object buff)
        {
            __instance.GeneralSet(buff);
        }

        [HarmonyPatch(nameof(TravelBuffOptionButton.SetBuff))]
        [HarmonyPostfix]
        public static void PostSetBuff(TravelBuffOptionButton __instance)
        {
            var tuple = __instance.TryGetTypeAndID();
            if (CustomCore.CustomBuffsBg.ContainsKey(tuple))
            {
                __instance.SetBackground(CustomCore.CustomBuffsBg[tuple]);
            }
            if (CustomCore.CustomBuffIcon.ContainsKey(tuple))
            {
                if (__instance.show.IsObjExist())
                    Destroy(__instance.show.gameObject);
                __instance.SetPlant((CustomCore.CustomBuffIcon[tuple]));
            }
        }
    }

    //[HarmonyPatch(typeof(TravelBuff))]
    //public static class TravelBuffPatch
    //{
    //    [HarmonyPrefix]
    //    [HarmonyPatch(nameof(TravelBuff.ChangeSprite))]
    //    public static void PreChangeSprite(TravelBuff __instance)
    //    {
    //        var list = CustomCore.CustomUltimateBuffs.
    //                Where(kvp => kvp.Key == __instance.theBuffNumber).
    //                Select(kvp => kvp.Value).
    //                ToList();
    //        if (__instance.theBuffType == (int)BuffType.UltimateBuff && list.Count > 0)
    //        {
    //            foreach (var item in list)
    //            {
    //                if (item.Item1 == PlantType.Nothing)
    //                    __instance.thePlantType = PlantType.EndoFlame;
    //                else
    //                    __instance.thePlantType = item.Item1;
    //            }
    //        }

    //        if (__instance.theBuffType == 1 && CustomCore.CustomAdvancedBuffs.ContainsKey(__instance.theBuffNumber))
    //        {
    //            __instance.thePlantType = CustomCore.CustomAdvancedBuffs[__instance.theBuffNumber].Item1;
    //        }
    //    }
    //}

    [HarmonyPatch(typeof(TravelLookBuff))]
    public static class TravelLookBuffPatch
    {
        [HarmonyPatch(nameof(TravelLookBuff.SetBuff))]
        [HarmonyPrefix]
        public static void PreSetBuff(TravelLookBuff __instance, Il2CppSystem.Object buff)
        {
            __instance.GeneralSet(buff);
        }

        [HarmonyPatch(nameof(TravelLookBuff.SetBuff))]
        [HarmonyPostfix]
        public static void PostSetBuff(TravelLookBuff __instance)
        {
            var (buffType, buffIndex) = __instance.TryGetTypeAndID();
            if (CustomCore.CustomBuffIcon.ContainsKey((buffType, buffIndex)))
            {
                if (__instance.show != null)
                    Destroy(__instance.show);
                __instance.SetPlant(CustomCore.CustomBuffIcon[(buffType, buffIndex)]);
            }
            if (CustomCore.CustomBuffsBg.ContainsKey((buffType, buffIndex)))
            {
                __instance.SetBackground(CustomCore.CustomBuffsBg[(buffType, buffIndex)]);
            }
            if (CustomCore.CustomDebuffs.ContainsKey(buffIndex) && buffType == BuffType.Debuff)
            {
                if (__instance.show != null)
                    Destroy(__instance.show);
                __instance.SetZombie(CustomCore.CustomDebuffs[buffIndex].Item2);
            }

            // 多级词条文本显示
            var result = MultiLevelBuff.IsMultiLevelBuff(buffType, buffIndex);
            try
            {
                // 如果是多级词条
                if (result.Item1)
                {
                    var array = MultiLevelBuff.GetBuffArray();
                    if (array is null) return; // 如果数据数组为空直接返回
                    int index = result.Item2;
                    int maxLevel = MultiLevelBuff.GetBuffMaxLevel(buffType, buffIndex);
                    if (TravelLookMenu.Instance.showAll) // 如果是iz的全选模式
                    {
                        __instance.SetText(array[index] != 0, array[index]);
                        if (array[index] <= maxLevel &&
                            array[index] != 0)
                        {
                            if (maxLevel > 1)
                                __instance.SetText($"已开启（{array[index]}级）");
                            else
                                __instance.SetText($"已开启");
                        }
                        return;
                    }
                    else
                    {
                        if (array[index] < maxLevel && maxLevel != 1)
                        {
                            __instance.SetText($"{array[index]}级");
                        }
                        else if (array[index] >= maxLevel && maxLevel != 1)
                        {
                            __instance.SetText("已满级");
                        }
                        TravelMgr.Instance.SetData(LevelBuffData.LEVEL_BUFF_ARR, array);
                    }
                }
            }
            catch (ArgumentException ex)
            {
                CustomCore.CLogger.LogWarning($"StackTrace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// 高级词条升级处理
        /// </summary>
        [HarmonyPatch(nameof(TravelLookBuff.OnMouseUpAsButton))]
        [HarmonyPrefix]
        public static bool PreOnMouseUpAsButton(TravelLookBuff __instance)
        {
            var (buffType, buffIndex) = __instance.TryGetTypeAndID();
            var result = MultiLevelBuff.IsMultiLevelBuff(buffType, buffIndex);
            bool reset = false; // 重置升级词条
            if (result.Item1)
            {
                try
                {
                    var array = MultiLevelBuff.GetBuffArray();
                    if (array is null) return true;
                    int index = result.Item2;
                    int maxLevel = MultiLevelBuff.GetBuffMaxLevel(buffType, buffIndex);
                    if (TravelLookMenu.Instance.showAll) // 如果是iz的全选
                    {
                        MultiLevelBuff.AddBuffLevel(buffType, buffIndex);
                        __instance.SetText(array[index] != 0, array[index]); // 设置文本
                        if (array[index] <= maxLevel && array[index] != 0)
                        {
                            if (maxLevel > 1)
                                __instance.SetText($"已开启（{array[index]}级）");
                            else
                                __instance.SetText($"已开启");
                        }
                        TravelMgr.Instance.SetData(LevelBuffData.LEVEL_BUFF_ARR, array);
                        return false;
                    }
                    else
                    {
                        if (array[index] < maxLevel && CoreTools.TravelAdvanced("升级") && maxLevel != 1)
                        {
                            array[index] = array[index] + 1; // 升级
                            reset = true;
                            if (array[index] >= maxLevel)
                                __instance.SetText("已满级");
                            else
                                __instance.SetText($"{array[index]}级");
                        }
                        if (array[index] >= maxLevel)
                        {
                            __instance.SetText("已满级");
                        }
                        TravelMgr.Instance.SetData("CustomBuffsLevel", array);
                    }
                }
                catch (ArgumentException ex)
                {
                    CustomCore.CLogger.LogWarning($"StackTrace: {ex.StackTrace}");
                }
            }
            if (reset)
            {
                __instance.manager.data.advBuffs.Remove(CoreTools.GetAdvBuffByString("升级")); // 移除升级
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(AlmanacBuffMenu))]
    public static class AlmanacBuffMenuPatch
    {
        [HarmonyPatch(nameof(AlmanacBuffMenu.OnToolClick))]
        [HarmonyPrefix]
        public static bool PreOnToolClick(AlmanacBuffMenu __instance, ref UIButton button)
        {
            if (__instance.current == null) return true;
            var buff = __instance.cardInfos[__instance.current].buff;
            var (buffType, buffIndex) = TravelExtensions.GetTypeAndID(buff);
            var result = MultiLevelBuff.IsMultiLevelBuff(buffType, buffIndex);
            bool reset = false; // 重置升级词条
            if (result.Item1)
            {
                try
                {
                    var buttonText = button.GetComponentInChildren<TextMeshProUGUI>();
                    var array = MultiLevelBuff.GetBuffArray();
                    if (array is null) return true;
                    int index = result.Item2;
                    int maxLevel = MultiLevelBuff.GetBuffMaxLevel(buffType, buffIndex);
                    if (__instance.editMode) // 如果是iz的全选
                    {
                        MultiLevelBuff.AddBuffLevel(buffType, buffIndex);
                        MultiLevelBuff.SetToolText(button, buffType, buffIndex, array[index] != 0); // 设置文本
                        TravelMgr.Instance.SetData(LevelBuffData.LEVEL_BUFF_ARR, array);
                        // 更新卡片UI的透明度
                        var hasBuff = Lawnf.HasTravelBuff(buff) ? 0f : 1f;
                        __instance.current.GetComponent<Image>().color = new Color(hasBuff, 1f, hasBuff, 1f);
                        return false;
                    }
                    else
                    {
                        if (array[index] < maxLevel && CoreTools.TravelAdvanced("升级") && maxLevel != 1)
                        {
                            array[index] = array[index] + 1; // 升级
                            reset = true;
                            if (array[index] >= maxLevel)
                                buttonText.text = "已满级";
                            else
                                buttonText.text = $"{array[index]}级";
                        }
                        if (array[index] >= maxLevel) buttonText.text = "已满级";
                        TravelMgr.Instance.SetData("CustomBuffsLevel", array);
                        // 更新卡片UI的透明度
                        var hasBuff = Lawnf.HasTravelBuff(buff) ? 0f : 1f;
                        __instance.current.GetComponent<Image>().color = new Color(hasBuff, 1f, hasBuff, 1f);
                    }
                }
                catch (ArgumentException ex)
                {
                    CustomCore.CLogger.LogWarning($"StackTrace: {ex.StackTrace}");
                }
            }
            if (reset)
            {
                TravelMgr.Instance.data.advBuffs.Remove(CoreTools.GetAdvBuffByString("升级")); // 移除升级
                return false;
            }
            return true;
        }

        [HarmonyPatch(nameof(AlmanacBuffMenu.OnCardClick))]
        [HarmonyPostfix]
        public static void PostOnCardClick(AlmanacBuffMenu __instance, ref AlmanacCardUI card)
        {
            var button = __instance.transform.FindChild("Tool").GetComponent<UIButton>();
            var buttonText = button.GetComponentInChildren<TextMeshProUGUI>();
            if (__instance.current == null || !__instance.cardInfos.ContainsKey(__instance.current)) return;
            var buff = __instance.cardInfos[__instance.current].buff;
            var (buffType, buffIndex) = TravelExtensions.GetTypeAndID(buff);
            var array = MultiLevelBuff.GetBuffArray();
            if (array is null) return;
            var result = MultiLevelBuff.IsMultiLevelBuff(buffType, buffIndex);
            if (result.Item1)
            {
                MultiLevelBuff.SetToolText(button, buffType, buffIndex, array[result.Item2] != 0); // 设置文本
            }
        }

        [HarmonyPatch(nameof(AlmanacBuffMenu.InitMenu))]
        [HarmonyPrefix]
        public static void PreInitMenu(AlmanacBuffMenu __instance, out bool __state)
        {
            __state = __instance.inited;
        }

        [HarmonyPatch(nameof(AlmanacBuffMenu.InitMenu))]
        [HarmonyPostfix]
        public static void PostInitMenu(AlmanacBuffMenu __instance, bool __state)
        {
            if (__state) return;
            {
                var curse = __instance.transform.FindChild("Scroll View/Viewport/Content/curseBuffs").gameObject;
                var customBuffs = Instantiate(curse, __instance.transform.FindChild("Scroll View/Viewport/Content"));
                customBuffs.name = "customBuffs";
                customBuffs.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text = "二创词条";
                var list = new Il2CppSystem.Collections.Generic.List<AlmanacCardUI>();
                int cnt = 0; // 当前是第几次循环
                foreach (var ((buffType, id), (desc, icon, zt)) in CustomCore.CustomBuffs)
                {
                    var obj = new Il2CppSystem.Object();
                    switch (buffType)
                    {
                        case BuffType.UnlockPlant:
                            obj = Il2CppExtensions.BoxEnumToIl2Object<TravelUnlocks>(id);
                            break;
                        case BuffType.AdvancedBuff:
                            obj = Il2CppExtensions.BoxEnumToIl2Object<AdvBuff>(id);
                            break;
                        case BuffType.UltimateBuff:
                            obj = Il2CppExtensions.BoxEnumToIl2Object<UltiBuff>(id);
                            break;
                        case BuffType.Debuff:
                            obj = Il2CppExtensions.BoxEnumToIl2Object<TravelDebuff>(id);
                            break;
                        case BuffType.InvestmentBuff:
                            obj = Il2CppExtensions.BoxEnumToIl2Object<InvestBuff>(id);
                            break;
                    }
                    if (!Lawnf.HasTravelBuff(obj) && !__instance.editMode && AlmanacBuffMenu.lookBuff) continue;
                    var cardInfo = new AlmanacBuffMenu.CardInfo
                    {
                        buff = obj,
                        description = desc,
                        isZombie = buffType == BuffType.Debuff
                    };
                    if (buffType == BuffType.Debuff)
                        cardInfo.zombieType = zt;
                    else
                        cardInfo.plantType = icon;
                    __instance.CreateCardUI(cardInfo, list);
                    if (__instance.editMode)
                    {
                        var hasBuff = Lawnf.HasTravelBuff(obj) ? 0f : 1f;
                        list[cnt].GetComponent<Image>().color = new Color(hasBuff, 1f, hasBuff, 1f);
                        cnt++;
                    }
                }
                foreach (var cardUI in list)
                    cardUI.gameObject.SetActive(false);

                Action action = () =>
                {
                    __instance.SetAllCards(false);
                    foreach (var cardUI in list)
                        cardUI.gameObject.SetActive(true);
                };

                UnityEvent unityEvent = new UnityEvent();
                unityEvent.AddListener(action);
                customBuffs.GetComponent<UIButton>().clickEvent = unityEvent;
            }

            {
                foreach (var ((buffType, id), (almanacType, icon, zt)) in CustomCore.CustomAlmanacBuffType)
                {
                    var obj = new Il2CppSystem.Object();
                    var list = new Il2CppSystem.Collections.Generic.List<AlmanacCardUI>();
                    switch (almanacType)
                    {
                        case AlmanacBuffType.WeakUltimate:
                            obj = Il2CppExtensions.BoxEnumToIl2Object<AdvBuff>(id);
                            list = __instance.weakUltiBuffs;
                            break;
                        case AlmanacBuffType.StrongUltimate:
                            obj = Il2CppExtensions.BoxEnumToIl2Object<UltiBuff>(id);
                            list = __instance.strongUltiBuffs;
                            break;
                        case AlmanacBuffType.General:
                            obj = Il2CppExtensions.BoxEnumToIl2Object<AdvBuff>(id);
                            list = __instance.generalBuffs;
                            break;
                        case AlmanacBuffType.Random:
                            obj = Il2CppExtensions.BoxEnumToIl2Object<AdvBuff>(id);
                            list = __instance.randomBuffs;
                            break;
                        case AlmanacBuffType.Curse:
                            obj = Il2CppExtensions.BoxEnumToIl2Object<AdvBuff>(id);
                            list = __instance.curseBuffs;
                            break;
                        case AlmanacBuffType.Rogue:
                            obj = Il2CppExtensions.BoxEnumToIl2Object<AdvBuff>(id);
                            list = __instance.rogueBuffs;
                            break;
                        case AlmanacBuffType.Combo:
                            obj = Il2CppExtensions.BoxEnumToIl2Object<AdvBuff>(id);
                            list = __instance.comboBuffs;
                            break;
                        case AlmanacBuffType.Tiny:
                            obj = Il2CppExtensions.BoxEnumToIl2Object<AdvBuff>(id);
                            list = __instance.tinyBuffs;
                            break;
                        case AlmanacBuffType.Zombie:
                            obj = Il2CppExtensions.BoxEnumToIl2Object<TravelDebuff>(id);
                            list = __instance.zombieBuffs;
                            break;
                        case AlmanacBuffType.Shooting:
                            obj = Il2CppExtensions.BoxEnumToIl2Object<AdvBuff>(id);
                            list = __instance.shootingBuffs;
                            break;
                    }
                    if (!Lawnf.HasTravelBuff(obj) && !__instance.editMode && AlmanacBuffMenu.lookBuff) continue;
                    var cardInfo = new AlmanacBuffMenu.CardInfo
                    {
                        buff = obj,
                        description = TravelMgr.Instance.GetText(obj),
                        isZombie = almanacType == AlmanacBuffType.Zombie
                    };
                    if (almanacType == AlmanacBuffType.Zombie)
                        cardInfo.zombieType = zt;
                    else
                        cardInfo.plantType = icon;
                    __instance.CreateCardUI(cardInfo, list);
                }
            }
        }

        [HarmonyPatch(nameof(AlmanacBuffMenu.OnCardClick))]
        [HarmonyPostfix]
        public static void PostOnCardClick(AlmanacBuffMenu __instance, AlmanacCardUI card)
        {
            AlmanacBuffMenu.CardInfo cardInfo = __instance.cardInfos[card];
            var (type, id) = TravelExtensions.GetTypeAndID(cardInfo.buff);
            if (CustomCore.CustomBuffsBg.ContainsKey((type, id)))
            {
                if (CustomCore.CustomBuffsBg[(type, id)].BgType == BuffBgType.Day)
                    __instance.windowBackground.sprite = __instance.day;
                else if (CustomCore.CustomBuffsBg[(type, id)].BgType == BuffBgType.Night)
                    __instance.windowBackground.sprite = __instance.night;
                else if (CustomCore.CustomBuffsBg[(type, id)].BgType == BuffBgType.Night)
                    __instance.windowBackground.sprite = __instance.pool;
            }
        }
    }

    [HarmonyPatch(typeof(TravelMgr))]
    public static class TravelMgrPatch
    {
        [HarmonyPatch(nameof(TravelMgr.OnBoardStart))]
        [HarmonyPostfix]
        public static void PostOnBoardStart(TravelMgr __instance)
        {
            if (__instance.GetData("CustomBuffsLevel") is null)
            {
                __instance.SetData("CustomBuffsLevel", new int[CustomCore.CustomBuffsLevel.Count]);
            }
            if (__instance.GetData("LoadByEndless") is null)
                __instance.SetData("LoadByEndless", false);
            if (!__instance.GetData<bool>("LoadByEndless"))
            {
                __instance.SetData("CustomBuffsLevel", new int[CustomCore.CustomBuffsLevel.Count]);
            }
            TravelMgr.Instance.SetData("LoadByEndless", false); // 重置标志位，避免进入其他模式后不重置
        }

        [HarmonyPatch(nameof(TravelMgr.GetAdvancedBuffPool))]
        [HarmonyPostfix]
        public static void PostGetAdvancedBuffPool(ref Il2CppSystem.Collections.Generic.List<AdvBuff> __result)
        {
            foreach (var (key, value) in CustomCore.CustomAdvancedBuffs)
            {
                if (value.Item3.Invoke() && !TravelMgr.Instance.data.advBuffs.Contains((AdvBuff)key))
                    __result.Add((AdvBuff)key);
            }

            foreach (var (key, list) in CustomCore.CustomPlantInfo)
            {
                if (Lawnf.GetPlantCount(key, Board.Instance) > 0 && __result.Contains((AdvBuff)key))
                {
                    foreach (var (buffType, id) in list)
                        if (buffType == BuffType.AdvancedBuff && __result.Contains((AdvBuff)id))
                            for (int i = 0; i < __result.Count / 8; i++)
                                __result.Add((AdvBuff)id);
                }
            }
        }

        [HarmonyPatch(nameof(TravelMgr.GetDebuffPool))]
        [HarmonyPostfix]
        public static void GetDebuffPool(ref Il2CppSystem.Collections.Generic.List<TravelDebuff> __result)
        {
            foreach (var (key, value) in CustomCore.CustomDebuffs)
            {
                if (value.Item3.Invoke() && !TravelMgr.Instance.data.travelDebuffs.Contains((TravelDebuff)key))
                {
                    __result.Add((TravelDebuff)key);
                }
            }
        }

        [HarmonyPatch(nameof(TravelMgr.GetText))]
        [HarmonyPostfix]
        public static void PostGetText(Il2CppSystem.Object buff, ref string __result)
        {
            var (type, id) = TravelExtensions.GetTypeAndID(buff);
            if (CustomCore.CustomBuffText.ContainsKey((type, id)))
                __result = CustomCore.CustomBuffText[(type, id)];
        }
    }

    [HarmonyPatch(typeof(TravelPackage))]
    public static class TravelPackagePatch
    {
        [HarmonyPatch(nameof(TravelPackage.Init))]
        [HarmonyPostfix]
        public static void PostInit(TravelPackage __instance)
        {
            foreach (var (key, value) in CustomCore.CustomDebuffs)
            {
                if (value.Item3.Invoke() && !TravelMgr.Instance.data.travelDebuffs.Contains((TravelDebuff)key))
                {
                    __instance.Debuffs.Add((TravelDebuff)key);
                }
            }
        }
    }

    [HarmonyPatch(typeof(TravelHelper))]
    public static class TravelHelperPatch
    {
        [HarmonyPatch(nameof(TravelHelper.GetAllUltimatePlantTypes))]
        [HarmonyPostfix]
        public static void PostGetAllUltimatePlantTypes(ref Il2CppSystem.Collections.Generic.List<PlantType> __result, ref bool isStrongUltimate)
        {
            if (isStrongUltimate)
            {
                foreach (var (pt, _) in CustomCore.CustomStrongUltimatePlants)
                    __result.Add(pt);
            }
            else
            {
                foreach (var pt in CustomCore.CustomUltimatePlants)
                    if (!CustomCore.CustomStrongUltimatePlants.ContainsKey(pt)) // 排除强究
                        __result.Add(pt);
            }
        }
    }

    [HarmonyPatch(typeof(TravelLookMenu))]
    public static class TravelLookMenuPatch
    {
        [HarmonyPatch(nameof(TravelLookMenu.GetAdvBuffs))]
        [HarmonyPostfix]
        public static void PostGetAdvBuffs(TravelLookMenu __instance, ref Il2CppSystem.Collections.Generic.List<AdvBuff> __result)
        {
            if (CustomCore.CustomAdvancedBuffs.Count <= 0)
                return;
            foreach (var (id, _) in CustomCore.CustomAdvancedBuffs)
                if (__instance.showAll)
                    __result.Add((AdvBuff)id);
        }

        [HarmonyPatch(nameof(TravelLookMenu.GetDebuffs))]
        [HarmonyPostfix]
        public static void PostGetDebuffs(TravelLookMenu __instance, ref Il2CppSystem.Collections.Generic.List<TravelDebuff> __result)
        {
            if (CustomCore.CustomDebuffs.Count <= 0)
                return;
            foreach (var (id, _) in CustomCore.CustomDebuffs)
                if (__instance.showAll)
                    __result.Add((TravelDebuff)id);
        }

        [HarmonyPatch(nameof(TravelLookMenu.GetUltiBuffs))]
        [HarmonyPostfix]
        public static void PostGetUltimateBuffs(TravelLookMenu __instance,
            ref Il2CppSystem.ValueTuple<Il2CppSystem.Collections.Generic.List<UltiBuff>, Il2CppSystem.Collections.Generic.List<UltiBuff>>
            __result)
        {
            if (CustomCore.CustomUltimateBuffs.Count <= 0)
                return;
            foreach (var (id, _) in CustomCore.CustomUltimateBuffs)
            {
                if (__instance.showAll)
                    __result.Item1.Add((UltiBuff)id);
                if (__instance.showAll)
                    __result.Item2.Add((UltiBuff)id);
            }
        }
    }

    [HarmonyPatch(typeof(TravelStore))]
    public static class TravelStorePatch
    {
        [HarmonyPatch(nameof(TravelStore.SetCost))]
        [HarmonyPostfix]
        public static void PostRefreshBuff(ref TravelStoreWindow window)
        {
            var (buffType, buffIndex) = window.TryGetTypeAndID();
            if (CustomCore.CustomBuffCost.ContainsKey((buffType, buffIndex)))
            {
                window.cost = CustomCore.CustomBuffCost[(buffType, buffIndex)];
                if (Lawnf.TravelCurse() || TravelMgr.Instance.data.Invest)
                {
                    if (window.cost > 15000)
                    {
                        window.UpdateButtonText("过于昂贵", UnityEngine.Color.red);
                        window.canBuy = false;
                        return;
                    }
                }
                window.UpdateButtonText($"{window.cost}分", UnityEngine.Color.yellow);
                window.canBuy = true;
            }
        }
    }

    [HarmonyPatch(typeof(TravelStoreWindow))]
    public static class TravelStoreWindowPatch
    {
        [HarmonyPatch(nameof(TravelStoreWindow.SetType))]
        [HarmonyPostfix]
        public static void Postfix(TravelStoreWindow __instance, Il2CppSystem.Object buff)
        {
            var (buffType, index) = __instance.GeneralSet(buff);
            if (CustomCore.CustomBuffsBg.ContainsKey((buffType, index)))
            {
                __instance.SetBackground(CustomCore.CustomBuffsBg[(buffType, index)]);
            }
            if (CustomCore.CustomBuffIcon.ContainsKey((buffType, index)))
            {
                if (__instance.show != null)
                    Destroy(__instance.show);
                __instance.SetPlant(CustomCore.CustomBuffIcon[(buffType, index)]);
            }
        }
    }

    [HarmonyPatch(typeof(TypeMgr))]
    public static class TypeMgrPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(TypeMgr.BigNut))]
        public static bool PreBigNut(ref PlantType theSeedType, ref bool __result)
        {
            if (CustomCore.TypeMgrExtra.BigNut.Contains(theSeedType))
            {
                __result = true;
                return false;
            }

            if (CustomCore.TypeMgrExtraSkin.BigNut.TryGetValue(theSeedType, out int value))
            {
                switch (value)
                {
                    case -1:
                        return true;

                    case 0:
                        __result = false;
                        return false;

                    case 1:
                        __result = true;
                        return false;
                }
            }

            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(TypeMgr.IsDriverZombie))]
        public static bool PreDriverZombie(ref ZombieType theZombieType, ref bool __result)
        {
            if (CustomCore.TypeMgrExtra.DriverZombie.Contains(theZombieType))
            {
                __result = true;
                return false;
            }

            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(TypeMgr.BigZombie))]
        public static bool PreBigZombie(ref ZombieType theZombieType, ref bool __result)
        {
            if (CustomCore.TypeMgrExtra.BigZombie.Contains(theZombieType))
            {
                __result = true;
                return false;
            }

            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(TypeMgr.DoubleBoxPlants))]
        public static bool PreDoubleBoxPlants(ref PlantType thePlantType, ref bool __result)
        {
            if (CustomCore.TypeMgrExtra.DoubleBoxPlants.Contains(thePlantType))
            {
                __result = true;
                return false;
            }

            if (CustomCore.TypeMgrExtraSkin.DoubleBoxPlants.TryGetValue(thePlantType, out int value))
            {
                switch (value)
                {
                    case -1:
                        return true;

                    case 0:
                        __result = false;
                        return false;

                    case 1:
                        __result = true;
                        return false;
                }
            }

            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(TypeMgr.FlyingPlants))]
        public static bool PreFlyingPlants(ref PlantType thePlantType, ref bool __result)
        {
            if (CustomCore.TypeMgrExtra.FlyingPlants.Contains(thePlantType))
            {
                __result = true;
                return false;
            }

            if (CustomCore.TypeMgrExtraSkin.FlyingPlants.TryGetValue(thePlantType, out int value))
            {
                switch (value)
                {
                    case -1:
                        return true;

                    case 0:
                        __result = false;
                        return false;

                    case 1:
                        __result = true;
                        return false;
                }
            }

            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(TypeMgr.GetPlantTag))]
        public static bool PreGetPlantTag(ref Plant plant)
        {
            if (CustomCore.CustomPlantTypes.Contains(plant.thePlantType))
            {
                plant.plantTag = new()
                {
                    icePlant = TypeMgr.IsIcePlant(plant.thePlantType),
                    caltropPlant = TypeMgr.IsCaltrop(plant.thePlantType),
                    doubleBoxPlant = TypeMgr.DoubleBoxPlants(plant.thePlantType),
                    firePlant = TypeMgr.IsFirePlant(plant.thePlantType),
                    flyingPlant = TypeMgr.FlyingPlants(plant.thePlantType),
                    lanternPlant = TypeMgr.IsPlantern(plant.thePlantType),
                    smallLanternPlant = TypeMgr.IsSmallRangeLantern(plant.thePlantType),
                    magnetPlant = TypeMgr.IsMagnetPlants(plant.thePlantType),
                    nutPlant = TypeMgr.IsNut(plant.thePlantType),
                    tallNutPlant = TypeMgr.IsTallNut(plant.thePlantType),
                    potatoPlant = TypeMgr.IsPotatoMine(plant.thePlantType),
                    potPlant = TypeMgr.IsPot(plant.thePlantType),
                    puffPlant = TypeMgr.IsPuff(plant.thePlantType),
                    pumpkinPlant = TypeMgr.IsPumpkin(plant.thePlantType),
                    spickRockPlant = TypeMgr.IsSpickRock(plant.thePlantType),
                    tanglekelpPlant = TypeMgr.IsTangkelp(plant.thePlantType),
                    waterPlant = TypeMgr.IsWaterPlant(plant.thePlantType),
                };

                return false;
            }

            if (CustomCore.CustomPlantsSkin.ContainsKey(plant.thePlantType))
            {
                plant.plantTag = new()
                {
                    icePlant = TypeMgr.IsIcePlant(plant.thePlantType),
                    caltropPlant = TypeMgr.IsCaltrop(plant.thePlantType),
                    doubleBoxPlant = TypeMgr.DoubleBoxPlants(plant.thePlantType),
                    firePlant = TypeMgr.IsFirePlant(plant.thePlantType),
                    flyingPlant = TypeMgr.FlyingPlants(plant.thePlantType),
                    lanternPlant = TypeMgr.IsPlantern(plant.thePlantType),
                    smallLanternPlant = TypeMgr.IsSmallRangeLantern(plant.thePlantType),
                    magnetPlant = TypeMgr.IsMagnetPlants(plant.thePlantType),
                    nutPlant = TypeMgr.IsNut(plant.thePlantType),
                    tallNutPlant = TypeMgr.IsTallNut(plant.thePlantType),
                    potatoPlant = TypeMgr.IsPotatoMine(plant.thePlantType),
                    potPlant = TypeMgr.IsPot(plant.thePlantType),
                    puffPlant = TypeMgr.IsPuff(plant.thePlantType),
                    pumpkinPlant = TypeMgr.IsPumpkin(plant.thePlantType),
                    spickRockPlant = TypeMgr.IsSpickRock(plant.thePlantType),
                    tanglekelpPlant = TypeMgr.IsTangkelp(plant.thePlantType),
                    waterPlant = TypeMgr.IsWaterPlant(plant.thePlantType)
                };

                return false;
            }

            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(TypeMgr.IsCaltrop))]
        public static bool PreIsCaltrop(ref PlantType theSeedType, ref bool __result)
        {
            if (CustomCore.TypeMgrExtra.IsCaltrop.Contains(theSeedType))
            {
                __result = true;
                return false;
            }

            if (CustomCore.TypeMgrExtraSkin.IsCaltrop.TryGetValue(theSeedType, out int value))
            {
                switch (value)
                {
                    case -1:
                        return true;

                    case 0:
                        __result = false;
                        return false;

                    case 1:
                        __result = true;
                        return false;
                }
            }

            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(TypeMgr.IsFirePlant))]
        public static bool PreIsFirePlant(ref PlantType theSeedType, ref bool __result)
        {
            if (CustomCore.TypeMgrExtra.IsFirePlant.Contains(theSeedType))
            {
                __result = true;
                return false;
            }

            if (CustomCore.TypeMgrExtraSkin.IsFirePlant.TryGetValue(theSeedType, out int value))
            {
                switch (value)
                {
                    case -1:
                        return true;

                    case 0:
                        __result = false;
                        return false;

                    case 1:
                        __result = true;
                        return false;
                }
            }

            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(TypeMgr.IsIcePlant))]
        public static bool PreIsIcePlant(ref PlantType theSeedType, ref bool __result)
        {
            if (CustomCore.TypeMgrExtra.IsIcePlant.Contains(theSeedType))
            {
                __result = true;
                return false;
            }

            if (CustomCore.TypeMgrExtraSkin.IsIcePlant.TryGetValue(theSeedType, out int value))
            {
                switch (value)
                {
                    case -1:
                        return true;

                    case 0:
                        __result = false;
                        return false;

                    case 1:
                        __result = true;
                        return false;
                }
            }

            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(TypeMgr.IsMagnetPlants))]
        public static bool PreIsMagnetPlants(ref PlantType thePlantType, ref bool __result)
        {
            if (CustomCore.TypeMgrExtra.IsMagnetPlants.Contains(thePlantType))
            {
                __result = true;
                return false;
            }

            if (CustomCore.TypeMgrExtraSkin.IsMagnetPlants.TryGetValue(thePlantType, out int value))
            {
                switch (value)
                {
                    case -1:
                        return true;

                    case 0:
                        __result = false;
                        return false;

                    case 1:
                        __result = true;
                        return false;
                }
            }

            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(TypeMgr.IsNut))]
        public static bool PreIsNut(ref PlantType theSeedType, ref bool __result)
        {
            if (CustomCore.TypeMgrExtra.IsNut.Contains(theSeedType))
            {
                __result = true;
                return false;
            }

            if (CustomCore.TypeMgrExtraSkin.IsNut.TryGetValue(theSeedType, out int value))
            {
                switch (value)
                {
                    case -1:
                        return true;

                    case 0:
                        __result = false;
                        return false;

                    case 1:
                        __result = true;
                        return false;
                }
            }

            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(TypeMgr.IsPlantern))]
        public static bool PreIsPlantern(ref PlantType theSeedType, ref bool __result)
        {
            if (CustomCore.TypeMgrExtra.IsPlantern.Contains(theSeedType))
            {
                __result = true;
                return false;
            }

            if (CustomCore.TypeMgrExtraSkin.IsPlantern.TryGetValue(theSeedType, out int value))
            {
                switch (value)
                {
                    case -1:
                        return true;

                    case 0:
                        __result = false;
                        return false;

                    case 1:
                        __result = true;
                        return false;
                }
            }

            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(TypeMgr.IsPot))]
        public static bool PreIsPot(ref PlantType thePlantType, ref bool __result)
        {
            if (CustomCore.TypeMgrExtra.IsPot.Contains(thePlantType))
            {
                __result = true;
                return false;
            }

            if (CustomCore.TypeMgrExtraSkin.IsPot.TryGetValue(thePlantType, out int value))
            {
                switch (value)
                {
                    case -1:
                        return true;

                    case 0:
                        __result = false;
                        return false;

                    case 1:
                        __result = true;
                        return false;
                }
            }

            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(TypeMgr.IsPotatoMine))]
        public static bool PreIsPotatoMine(ref PlantType theSeedType, ref bool __result)
        {
            if (CustomCore.TypeMgrExtra.IsPotatoMine.Contains(theSeedType))
            {
                __result = true;
                return false;
            }

            if (CustomCore.TypeMgrExtraSkin.IsPotatoMine.TryGetValue(theSeedType, out int value))
            {
                switch (value)
                {
                    case -1:
                        return true;

                    case 0:
                        __result = false;
                        return false;

                    case 1:
                        __result = true;
                        return false;
                }
            }

            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(TypeMgr.IsPuff))]
        public static bool PreIsPuff(ref PlantType theSeedType, ref bool __result)
        {
            if (CustomCore.TypeMgrExtra.IsPuff.Contains(theSeedType))
            {
                __result = true;
                return false;
            }

            if (CustomCore.TypeMgrExtraSkin.IsPuff.TryGetValue(theSeedType, out int value))
            {
                switch (value)
                {
                    case -1:
                        return true;

                    case 0:
                        __result = false;
                        return false;

                    case 1:
                        __result = true;
                        return false;
                }
            }

            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(TypeMgr.IsPumpkin))]
        public static bool PreIsPumpkin(ref PlantType theSeedType, ref bool __result)
        {
            if (CustomCore.TypeMgrExtra.IsPumpkin.Contains(theSeedType))
            {
                __result = true;
                return false;
            }

            if (CustomCore.TypeMgrExtraSkin.IsPumpkin.TryGetValue(theSeedType, out int value))
            {
                switch (value)
                {
                    case -1:
                        return true;

                    case 0:
                        __result = false;
                        return false;

                    case 1:
                        __result = true;
                        return false;
                }
            }

            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(TypeMgr.IsSmallRangeLantern))]
        public static bool PreIsSmallRangeLantern(ref PlantType theSeedType, ref bool __result)
        {
            if (CustomCore.TypeMgrExtra.IsSmallRangeLantern.Contains(theSeedType))
            {
                __result = true;
                return false;
            }

            if (CustomCore.TypeMgrExtraSkin.IsSmallRangeLantern.TryGetValue(theSeedType, out int value))
            {
                switch (value)
                {
                    case -1:
                        return true;

                    case 0:
                        __result = false;
                        return false;

                    case 1:
                        __result = true;
                        return false;
                }
            }

            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(TypeMgr.IsPurplePlant))]
        public static bool PreIsPurplePlant(ref PlantType theSeedType, ref bool __result)
        {
            if (CustomCore.TypeMgrExtra.IsSpecialPlant.Contains(theSeedType))
            {
                __result = true;
                return false;
            }

            if (CustomCore.TypeMgrExtraSkin.IsSpecialPlant.TryGetValue(theSeedType, out int value))
            {
                switch (value)
                {
                    case -1:
                        return true;

                    case 0:
                        __result = false;
                        return false;

                    case 1:
                        __result = true;
                        return false;
                }
            }

            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(TypeMgr.IsSpickRock))]
        public static bool PreIsSpickRock(ref PlantType theSeedType, ref bool __result)
        {
            if (CustomCore.TypeMgrExtra.IsSpickRock.Contains(theSeedType))
            {
                __result = true;
                return false;
            }

            if (CustomCore.TypeMgrExtraSkin.IsSpickRock.TryGetValue(theSeedType, out int value))
            {
                switch (value)
                {
                    case -1:
                        return true;

                    case 0:
                        __result = false;
                        return false;

                    case 1:
                        __result = true;
                        return false;
                }
            }

            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(TypeMgr.IsTallNut))]
        public static bool PreIsTallNut(ref PlantType theSeedType, ref bool __result)
        {
            if (CustomCore.TypeMgrExtra.IsTallNut.Contains(theSeedType))
            {
                __result = true;
                return false;
            }

            if (CustomCore.TypeMgrExtraSkin.IsTallNut.TryGetValue(theSeedType, out int value))
            {
                switch (value)
                {
                    case -1:
                        return true;

                    case 0:
                        __result = false;
                        return false;

                    case 1:
                        __result = true;
                        return false;
                }
            }

            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(TypeMgr.IsTangkelp))]
        public static bool PreIsTangkelp(ref PlantType theSeedType, ref bool __result)
        {
            if (CustomCore.TypeMgrExtra.IsTangkelp.Contains(theSeedType))
            {
                __result = true;
                return false;
            }

            if (CustomCore.TypeMgrExtraSkin.IsTangkelp.TryGetValue(theSeedType, out int value))
            {
                switch (value)
                {
                    case -1:
                        return true;

                    case 0:
                        __result = false;
                        return false;

                    case 1:
                        __result = true;
                        return false;
                }
            }

            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(TypeMgr.IsWaterPlant))]
        public static bool PreIsWaterPlant(ref PlantType theSeedType, ref bool __result)
        {
            if (CustomCore.TypeMgrExtra.IsWaterPlant.Contains(theSeedType))
            {
                __result = true;
                return false;
            }

            if (CustomCore.TypeMgrExtraSkin.IsWaterPlant.TryGetValue(theSeedType, out int value))
            {
                switch (value)
                {
                    case -1:
                        return true;

                    case 0:
                        __result = false;
                        return false;

                    case 1:
                        __result = true;
                        return false;
                }
            }

            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(TypeMgr.UmbrellaPlants))]
        public static bool PreUmbrellaPlants(ref PlantType thePlantType, ref bool __result)
        {
            if (CustomCore.TypeMgrExtra.UmbrellaPlants.Contains(thePlantType))
            {
                __result = true;
                return false;
            }

            if (CustomCore.TypeMgrExtraSkin.UmbrellaPlants.TryGetValue(thePlantType, out int value))
            {
                switch (value)
                {
                    case -1:
                        return true;

                    case 0:
                        __result = false;
                        return false;

                    case 1:
                        __result = true;
                        return false;
                }
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(CustomMenu))]
    public static class CustomMenuPatch
    {
        [HarmonyPatch(nameof(CustomMenu.Awake))]
        [HarmonyPostfix]
        public static void PostAwake(CustomMenu __instance)
        {
            if (GameAPP.canvas.IsObjExist()&& GameAPP.canvas.childCount > 0 && GameAPP.canvas.GetChild(0).name == "ChallengeMenu(Clone)" && 
                GameAPP.canvas.GetChild(0).FindChild("Levels").IsObjExist())
            {
                var child = GameAPP.canvas.GetChild(0).FindChild("Levels").FindChild("FirstBtns").FindChild("CustomLevels");
                if (child.IsObjExist())
                    child.GetChild(1).GetComponent<BoxCollider2D>().enabled = false;
                Action action = () =>
                {
                    if (GameAPP.canvas.IsObjExist() && GameAPP.canvas.GetChild(0).FindChild("Levels").IsObjExist())
                    {
                        var child = GameAPP.canvas.GetChild(0).FindChild("Levels").FindChild("FirstBtns").FindChild("CustomLevels");
                        if (child.IsObjExist())
                            child.GetChild(1).GetComponent<BoxCollider2D>().enabled = true;
                    }
                };
                __instance.transform.FindChild("LowerButtons/Exit").GetComponent<UIButton>().clickEvent.AddListener(action);
            }
        }
    }

    [HarmonyPatch(typeof(UIMgr))]
    public static class UIMgrPatch
    {
        private static Vector3 CalculatePosition(int col, int row)
        {
            return new Vector3(-300f + col * 150, 160f - row * 130);
        }

        [HarmonyPatch(nameof(UIMgr.EnterChallengeMenu))]
        [HarmonyPostfix]
        public static void PostEnterChallengeMenu()
        {
            GameAPP.Instance.StartCoroutine(init());
            IEnumerator init()
            {
                yield return null;
                var levels = GameAPP.canvas.GetChild(0).FindChild("Levels");
                var firstBtns = levels.FindChild("FirstBtns");
                if (firstBtns.FindChild("CustomLevels") == null || firstBtns.FindChild("CustomLevels").IsDestroyed())
                {
                    GameObject custom = UnityEngine.Object.Instantiate(firstBtns.GetChild(0).gameObject, firstBtns);
                    custom.name = "CustomLevels";
                    custom.transform.localPosition = CalculatePosition((firstBtns.childCount - 1) % 6, (firstBtns.childCount - 1) / 6);
                    var window = custom.transform.FindChild("Window");
                    window.FindChild("Name").GetComponent<TextMeshProUGUI>().text = "二创关卡";
                    var adv = levels.FindChild("PageAdvantureLevel");
                    var customLevels = UnityEngine.Object.Instantiate(adv.gameObject, levels);
                    customLevels.active = false;
                    customLevels.name = "PageCustomLevel";
                    var pages = customLevels.transform.FindChild("Pages");
                    var levelSample = UnityEngine.Object.Instantiate(pages.FindChild("Page1").FindChild("Lv1").gameObject);
                    foreach (var l in pages.FindChild("Page1").GetComponentsInChildren<Transform>(true))
                    {
                        UnityEngine.Object.Destroy(l.gameObject);
                    }
                    var pageSample = UnityEngine.Object.Instantiate(pages.FindChild("Page1").gameObject);
                    UnityEngine.Object.Destroy(pages.FindChild("Page1").gameObject);
                    UnityEngine.Object.Destroy(pages.FindChild("Page2").gameObject);
                    UnityEngine.Object.Destroy(pages.FindChild("Page3").gameObject);
                    int levelIndex = 0;
                    int columnIndex = 0;
                    int rowIndex = 0;
                    int pageIndex = 0;
                    foreach (var level in CustomCore.CustomLevels)
                    {
                        if (levelIndex % 18 is 0)
                        {
                            UnityEngine.Object.Instantiate(pageSample, pages).name = $"Pages{levelIndex / 18 + 1}";
                        }
                        columnIndex = levelIndex % 6;
                        rowIndex = levelIndex / 6;
                        pageIndex = rowIndex / 3;
                        var levelBtn = UnityEngine.Object.Instantiate(levelSample, pages.FindChild($"Pages{levelIndex / 18 + 1}"));
                        levelBtn.transform.localPosition = new(-50 + 150 * columnIndex, 60 - 130 * rowIndex, 0);
                        levelBtn.transform.GetChild(0).GetComponent<UnityEngine.UI.Image>().sprite = level.Logo;
                        levelBtn.transform.GetChild(1).GetComponent<Advanture_Btn>().levelType = (LevelType)66;
                        levelBtn.transform.GetChild(1).GetComponent<Advanture_Btn>().buttonNumber = level.ID;
                        levelBtn.transform.GetChild(1).GetChild(0).GetComponent<TextMeshProUGUI>().text = level.Name();
                        levelIndex++;
                    }
                    window.GetComponent<FirstBtns>().pageToOpen = customLevels;
                    window.GetComponent<FirstBtns>().originPosition = custom.transform.localPosition;
                    UnityEngine.Object.Destroy(pageSample);
                    UnityEngine.Object.Destroy(levelSample);
                }
                //foreach (var item in CustomCore.CustomLevels)
                //    LevelManager.registry.RegisterPredefinedLevel(item.LevelData);
            }
        }

        [HarmonyPatch(nameof(UIMgr.EnterGame))]
        [HarmonyPrefix]
        public static bool PreEnterGame(ref LevelType levelType, ref int levelNumber, ref int id, ref string name)
        {
            if ((int)levelType is not 66) return true;
            var levelData = CustomCore.CustomLevels[levelNumber];

            // 清理UI资源
            SynergyManager.Instance.ClearAllSynergies();
            EventManager.ClearAllEvents();
            GameAPP.UIManager.PopAll();

            // 重置相机
            CamaraFollowMouse.Instance.ResetCamera();

            // 设置游戏速度
            Time.timeScale = GameAPP.config.gameSpeed;

            // 设置当前关卡信息
            GameAPP.theBoardType = levelType;
            GameAPP.theBoardLevel = levelNumber;

            RogueManager.Instance.Clear();
            // 清理现有的Travel管理器
            if (TravelMgr.Instance != null)
            {
                UnityEngine.Object.Destroy(TravelMgr.Instance);
                TravelMgr._instance = null;
            }

            // 创建游戏板
            GameObject boardGO = new("Board");
            GameAPP.board = boardGO;
            Board board = boardGO.AddComponent<Board>();
            var bt = levelData.BoardTag;
            bt.disableSelectCard = !levelData.NeedSelectCard;
            board.boardTag = bt;
            board.rowNum = levelData.RowCount;
            board.theMaxWave = levelData.WaveCount();
            board.theSun = levelData.Sun();
            board.config.zombieHealthMultiplier = levelData.ZombieHealthRate();
            board.seedPool = levelData.SeedRainPlantTypes().ToIl2CppList();
            board.gridSystem.UpdateGrid(levelData.ColumnCount, levelData.RowCount);
            levelData.PostBoard(board);
            if (levelData.LevelData != null)
                LevelManager.registry.RegisterPredefinedLevel(levelData.LevelData);
            // 加载并实例化地图
            var map = MapData_cs.GetMap(levelData.SceneType, board);

            InitZombieList.InitZombie(levelType, levelNumber);

            // 播放音乐并开始游戏
            GameAPP.Instance.PlayMusic(MusicType.SelectCard);
            GameAPP.theGameStatus = GameStatus.InInterlude;

            // 初始化游戏板
            levelData.PreInitBoard();

            levelData.PostInitBoard(board.gameObject.AddComponent<InitBoard>());
            foreach (var p in levelData.PrePlants())
            {
                CreatePlant.Instance.SetPlant(p.Item1, p.Item2, p.Item3);
            }

            for (int i = 0; i < board.rowNum; i++)
            {
                var floor = map.transform.FindChild($"floor{i}");
                board.plane.Add(floor);
                if (board.boardTag.isRoof)
                {
                    var floor_roof = new GameObject("floor_roof");
                    floor_roof.transform.SetParent(floor);
                    floor_roof.transform.localPosition = new Vector3(0f, 0f, 0f);
                }
                var iceRoad = Instantiate(GamePrefabs.IceRoad, new Vector3(19.7f, 0.8f, 0f), Quaternion.identity, floor).GetComponent<IceRoad>();
                iceRoad.theRow = i;
                iceRoad.roadStartX = iceRoad.x = 19.7f;
                iceRoad.transform.localPosition = new Vector3(19.7f, 0.8f, 0f);
                board.iceRoads.Add(iceRoad);
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(WaveManager))]
    public static class WaveManagerPatch
    {
        [HarmonyPatch(nameof(WaveManager.GetMaxWave))]
        [HarmonyPostfix]
        public static void PostGetMaxWave(ref int __result)
        {
            if (Utils.IsCustomLevel(out var levelData))
            {
                __result = levelData.WaveCount();
            }
        }
    }

    [HarmonyPatch(typeof(ZombieDataManager))]
    public static class ZombieDataPatch
    {
        [HarmonyPatch(nameof(ZombieDataManager.LoadData))]
        [HarmonyPostfix]
        public static void InitZombieData()
        {
            foreach (var z in CustomCore.CustomZombies)
            {
                ZombieDataManager.zombieDataDic[z.Key] = z.Value.Item3;
            }
        }
    }

    [HarmonyPatch(typeof(SynergyDisplay))]
    public static class SynergyDisplayPatch
    {
        [HarmonyPatch(nameof(SynergyDisplay.Start))]
        [HarmonyPrefix]
        public static void Prefix()
        {
            if (Utils.IsCustomLevel(out var _))
            {
                {
                    var go = SynergyManager.Instance.gameObject;
                    Destroy(SynergyManager.Instance);
                    SynergyManager._instance = go.AddComponent<SynergyManager>();
                }
                {
                    var go = TravelMgr.Instance.gameObject;
                    Destroy(TravelMgr.Instance);
                    TravelMgr._instance = go.AddComponent<TravelMgr>();
                }
            }
        }
    }

    [HarmonyPatch(typeof(SaveInfo))]
    public static class SaveInfoPatch
    {
        [HarmonyPatch(nameof(SaveInfo.SaveSurvivalData), new Type[] { typeof(SurvivalData), typeof(int), typeof(int) })]
        [HarmonyPostfix]
        public static void PostSaveSurvivalDataByButton(ref int level, ref int id)
        {
            PatchMgr.SaveEndlessData(level, id);
        }

        [HarmonyPatch(nameof(SaveInfo.SaveSurvivalData), new Type[] { typeof(int), typeof(bool), typeof(int), typeof(string) })]
        [HarmonyPostfix]
        public static void PostSaveSurvivalDataByAuto(ref int level, ref int id)
        {
            PatchMgr.SaveEndlessData(level, id);
        }
    }

    [HarmonyPatch(typeof(SaveMgr))]
    public static class SaveMgrPatch
    {
        [HarmonyPatch(nameof(SaveMgr.SaveBoard))]
        [HarmonyPostfix]
        public static void PostSaveBoard(SaveMgr __instance, ref int level, ref int id)
        {
            PatchMgr.SaveEndlessData(level, id);
        }

        [HarmonyPatch(nameof(SaveMgr.LoadBoard))]
        [HarmonyPostfix]
        public static void PostLoadBoard(SaveMgr __instance, ref int level, ref int id)
        {
            if (TravelMgr.Instance == null || SaveInfo.Instance == null)
                return;
            var idGet = SaveInfo.Instance.GetData("endlessID");
            if (idGet is null)
                return;
            var idG = (int)idGet;
            PatchMgr.LoadEndlessData(level, id, idG);
        }
    }


    [HarmonyPatch(typeof(TreasureData))]
    public static class TreasureDataPatch
    {
        [HarmonyPatch(nameof(TreasureData.GetCardLevel))]
        [HarmonyPrefix]
        public static bool GetCardLevel(TreasureData __instance, ref PlantType thePlantType, ref CardLevel __result)
        {
            if (CustomCore.TypeMgrExtra.LevelPlants.ContainsKey(thePlantType))
            {
                __result = CustomCore.TypeMgrExtra.LevelPlants[thePlantType];
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(UIMgr))]
    public static class UIMgrPatch_0
    {
        [HarmonyPatch(nameof(UIMgr.EnterGame))]
        [HarmonyPrefix]
        public static void PreEnterGame(UIMgr __instance, ref int levelNumber, ref int id, ref LevelType levelType)
        {
            if (SaveInfo.Instance == null)
                return;
            if (!Lawnf.IsTravelLevel(levelType, levelNumber))
                return;
            SaveInfo.Instance.SetData("endlessID", id);
        }
    }

    [HarmonyPatch(typeof(AlmanacZombieMenu))]
    public class AlmanacZombieMenuPatch
    {
        [HarmonyPatch(nameof(AlmanacZombieMenu.Start))]
        // 只为顺序确定（我们排在原库那个下面），不为避免撞名 —— 名字不同不会撞。
        [HarmonyPriority(Priority.Last)]
        [HarmonyPostfix]
        public static void Postfix(AlmanacZombieMenu __instance)
        {
            // ★ 共存：各建各的，绝不碰原版 CustomizeLib.BepInEx 建的那个。
            //   我们的按钮名是 LoolAllOtherMengxi（原库是 LoolAll_Other），互相 Find 不到对方。
            //
            // ⚠ 这个按钮的位置是**写死**的 (440, -499)，两库用的是同一组坐标 ⇒ 双库同装时
            //   两个按钮会完全重叠、只剩最上面那个能点。所以共存时把我们的**往下挪一格**：
            //   44 是本 UI 的行距（图鉴·植物那边就是用 -44 * childCount 排的）。
            var coexists = CoexistMode.ForeignLibPresent;
            var customButton = Instantiate(__instance.transform.Find("LookAll_1").gameObject, __instance.transform);
            customButton.name = CustomTabNames.LookAllOther;
            customButton.transform.localPosition = new Vector2(440, coexists ? -543 : -499);
            // 修改按钮文本
            CustomTabLabel.MarkZombies(customButton.gameObject);

            UnityEvent unityEvent = new UnityEvent();
            Action action = () =>
            {
                Func<ZombieType, bool> func = (zt) => !Enum.IsDefined<ZombieType>(zt);
                __instance.ShowZombieCards(func);
            };
            unityEvent.AddListener(action);
            customButton.GetComponent<UIButton>().clickEvent = unityEvent;
        }
    }

    [HarmonyPatch(typeof(Entity))]
    public static class EntityPatch
    {
        [HarmonyPatch(nameof(Entity.GetSpriteRenderers))]
        [HarmonyPrefix]
        public static bool PreGetSpriteRenderers(Entity __instance)
        {
            if (__instance.TryGetComponent<SaveMaterial>(out var _))
            {
                foreach (var child in Core.Lawnf.GetChilds(__instance.transform))
                    if (child.TryGetComponent<SpriteRenderer>(out var renderer) && child.name != "Shadow")
                        __instance.spriteRenderers.Add(renderer);
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(EffectManager))]
    public static class EffectManagerPatch
    {
        [HarmonyPatch(nameof(EffectManager.SetEffect), new Type[] { typeof(Plant), typeof(EffectType), typeof(float), typeof(float) })]
        [HarmonyPrefix]
        public static bool PreSetEffectPlant(ref Plant plant, ref EffectType effectType, ref float duration, ref float value, ref bool __result)
        {
            if (CustomCore.CustomEffects.TryGetValue(effectType, out var cons))
            {
                var effect = (BaseEffect)cons.Invoke(plant, duration, value);
                plant.effects[effectType] = effect;
                if (effect.first)
                    effect.OnStart();
                __result = true;
                return false;
            }
            return true;
        }

        [HarmonyPatch(nameof(EffectManager.SetEffect), new Type[] { typeof(Zombie), typeof(EffectType), typeof(float), typeof(float) })]
        [HarmonyPrefix]
        public static bool PreSetEffectZombie(ref Zombie zombie, ref EffectType effectType, ref float duration, ref float value, ref bool __result)
        {
            if (CustomCore.CustomEffects.TryGetValue(effectType, out var cons))
            {
                var effect = (BaseEffect)cons.Invoke(zombie, duration, value);
                zombie.effects[effectType] = effect;
                if (effect.first)
                    effect.OnStart();
                __result = true;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(PatchMgr))]
    public static class PatchMgrPatch
    {
        [HarmonyPatch(nameof(PatchMgr.ShowCards))]
        [HarmonyFinalizer]
        public static Exception ShowFinalizer()
        {
            return null;
        }
    }

    [HarmonyPatch(typeof(BulletMovement))]
    public static class BulletMovementPatch
    {
        [HarmonyPatch(nameof(BulletMovement.CanHit))]
        [HarmonyPostfix]
        public static void PreCanHit(BulletMovement __instance, ref Zombie zombie, ref bool __result)
        {
            if (zombie == null) return;
            var team = __instance.bullet.Team != zombie.Team;
            foreach (var filter in __instance.bullet.hitFilters)
                if (CustomCore.CustomBulletHitFilter.TryGetValue(filter, out var tuple))
                    if (tuple.igonreTeam && team)
                        __result = __result && tuple.func.Invoke(zombie);
        }
    }
}