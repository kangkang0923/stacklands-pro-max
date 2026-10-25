using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace StacklandsProMaxNS
{
    /// <summary>
    /// 资源箱内的物品不计入卡牌总数：**每个资源箱固定只算 1 张**（＝箱子本身）。
    ///
    /// 为什么要打这个补丁：原版 `WorldManager.GetCardCount()`（侧栏的卡牌计数、月底"卡牌超限"结算都用它）
    /// 里有一段资源箱特判（反编译核对）：
    ///     - 遇到 `ResourceChest`：Cities 场地按 1 张算，其它场地按箱内数量（`ResourceCount`）算；
    ///     - 其余每张卡各再加 1。
    /// 即箱内资源按数量计入总数。磁力箱之所以"放进去就 -1"，只是因为它**不是 ResourceChest**、
    /// 不在这段特判里，整箱只算 1 张卡。所以"照着磁力箱改"＝让资源箱在计数时也只算 1 张：
    /// Postfix 把每个被计入的资源箱减回 1（非 Cities 减 `ResourceCount`、Cities 减 1），其它什么都不动。
    ///
    /// ⚠️ 踩坑史（别再走回头路）：
    /// - 第一版 transpiler（强制 `Id == "cities"` 比较为 true）：IL 形状用 Cecil 核对过完全吻合，但补丁
    ///   打错了方法 → 静默失效；
    /// - 第二版 `[HarmonyPatch(..., new Type[0])]` + Postfix：`GetCardCount` 有**两个无参重载**
    ///   （普通 + 泛型 `GetCardCount&lt;T&gt;()`，见 Cecil 核对），`GetMethod(..., Type.EmptyTypes, ...)`
    ///   在两者之间抛 AmbiguousMatchException → PatchAll 失败 → 静默失效；
    /// - 现版：`TargetMethod()` 手动遍历选"名为 GetCardCount、0 参数、非泛型"的那个方法，唯一确定；
    ///   补丁首次生效时会打一条日志（F3 / Player.log 可查证：搜 "资源箱计数修正"）。
    /// </summary>
    [HarmonyPatch]
    internal static class ResourceChestNoCardCountPatch
    {
        private static MethodBase TargetMethod()
        {
            foreach (MethodInfo method in typeof(WorldManager).GetMethods(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (method.Name == "GetCardCount"
                    && method.GetParameters().Length == 0
                    && !method.ContainsGenericParameters)
                {
                    Debug.Log("[StacklandsProMax] 资源箱计数修正：目标方法绑定成功 " + method);
                    return method;
                }
            }
            throw new Exception("WorldManager.GetCardCount 无参非泛型重载没找到（游戏更新改了实现？）");
        }

        private static bool loggedOnce;

        [HarmonyPostfix]
        internal static void Postfix(WorldManager __instance, ref int __result)
        {
            if (!ModConfig.ResourceChestNoCardCount || __instance == null)
            {
                return;
            }
            GameBoard board = __instance.CurrentBoard;
            if (board == null)
            {
                return;
            }
            bool isCities = board.Id == "cities";
            bool canTravelToIsland = board.BoardOptions != null && board.BoardOptions.CanTravelToIsland;
            List<GameCard> cards = __instance.AllCards;
            int subtracted = 0;
            int chests = 0;
            for (int i = 0; i < cards.Count; i++)
            {
                GameCard card = cards[i];
                if (card == null || card.CardData == null || !(card.CardData is ResourceChest chest))
                {
                    continue;
                }
                if (card.MyBoard != board || card.IsEquipped)
                {
                    continue;
                }
                GameCard root = card.GetRootCard();
                if (root == null || root.CardData == null)
                {
                    continue;
                }
                // 原版会跳过"正在启航的船"（含叠在船上、随船走的卡）：这种箱子原版根本没计，不能减
                bool onSailingBoat = root.CardData is Boat { InSailOff: true }
                    || (canTravelToIsland && root.CardData.AnyChildMatchesPredicate(
                        delegate (CardData x) { return x is Boat b && b.InSailOff; }));
                if (onSailingBoat)
                {
                    continue;
                }
                // 原版把它计成 (isCities ? 2 : 1 + ResourceCount)，减回到 1（只算箱子本身）
                int vanillaContentCount = isCities ? 1 : chest.ResourceCount;
                if (vanillaContentCount > 0)
                {
                    __result -= vanillaContentCount;
                    subtracted += vanillaContentCount;
                }
                chests++;
            }
            if (!loggedOnce && chests > 0)
            {
                loggedOnce = true;
                Debug.Log("[StacklandsProMax] 资源箱计数修正已生效：场上 " + chests + " 个资源箱，本次从卡牌总数里减去 "
                    + subtracted + "（只保留箱子本身各 1 张）");
            }
        }
    }
}
