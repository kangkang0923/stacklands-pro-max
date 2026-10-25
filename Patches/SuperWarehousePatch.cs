using System.Collections.Generic;
using HarmonyLib;

namespace StacklandsProMaxNS
{
    /// <summary>
    /// 超级仓库：给场地增加 140 张卡牌上限（= 10 个普通仓库之和）。
    ///
    /// 原版"仓库容量"不是仓库自己存东西，而是 WorldManager.CardCapIncrease(board) 里
    /// **按卡 id 硬判**累加的：
    ///     - `shed` +4、`warehouse` +14（← 一个普通仓库 = +14 张上限）、`lighthouse` +14；
    /// 所以超级仓库用一个 Postfix 在这个结果上再加 140 就行（比改原版那串 if 安全，
    /// 也不会和别的 mod 抢同一个常量）。
    /// 和原版一致：按场地上实际存在的卡数累加（叠在牌堆里的也算）。
    /// </summary>
    [HarmonyPatch(typeof(WorldManager), "CardCapIncrease")]
    internal static class SuperWarehouseCapacityPatch
    {
        internal const string SuperWarehouseId = "stacklandspro_super_warehouse";

        /// <summary>10 个普通仓库 = 10 x 14 = 140。</summary>
        internal const int ExtraCardCapacity = 140;

        [HarmonyPostfix]
        internal static void Postfix(GameBoard board, ref int __result)
        {
            // 注意：这里**不**看"卡牌：超级仓库"那个总开关。
            // 总开关只负责"抽不到 / 做不出来"；旧存档里已经存在的超级仓库照旧生效（这是与用户的约定）。
            if (board == null || WorldManager.instance == null)
            {
                return;
            }
            List<GameCard> allCards = WorldManager.instance.AllCards;
            if (allCards == null)
            {
                return;
            }
            for (int i = allCards.Count - 1; i >= 0; i--)
            {
                GameCard card = allCards[i];
                if (card == null || card.CardData == null)
                {
                    continue;
                }
                if (card.MyBoard != board)
                {
                    continue;
                }
                if (card.CardData.Id == SuperWarehouseId)
                {
                    __result += ExtraCardCapacity;
                }
            }
        }
    }

    /// <summary>
    /// 允许仓库之间互相叠放。
    ///
    /// 超级仓库的配方需要把 10 张仓库叠成一叠（原版合成是在一条堆叠上匹配配方的），
    /// 而仓库卡默认不让自己叠自己。这里在 CanHaveCardOnTop 上放行"仓库 + 仓库"这一种组合：
    /// 它是所有落卡/堆叠校验的最终入口，所以能覆盖拖拽落卡、堆叠有效性检查等所有路径；
    /// 其它组合完全不受影响。
    ///
    /// 0.5.7 起高级仓库也纳入：{仓库, 高级仓库} 任意两张都能互相叠
    /// （高级仓库外观就是仓库，混着放符合直觉；配方匹配按卡 id 数数，
    /// 叠了高级仓库的堆凑不齐 10 张"仓库"，不会误触发合成）。
    /// 高级仓库的 +140 上限按场上实际卡数统计（见上面的容量补丁），叠在一起照常每张生效。
    /// </summary>
    [HarmonyPatch(typeof(CardData), "CanHaveCardOnTop")]
    internal static class WarehouseStackingPatch
    {
        private const string WarehouseId = "warehouse";

        [HarmonyPrefix]
        internal static bool Prefix(CardData __instance, CardData otherCard, ref bool __result)
        {
            if (__instance == null || otherCard == null)
            {
                return true;
            }
            if (IsWarehouseCard(__instance.Id) && IsWarehouseCard(otherCard.Id))
            {
                __result = true;
                return false;
            }
            return true;
        }

        private static bool IsWarehouseCard(string cardId)
        {
            return cardId == WarehouseId || cardId == SuperWarehouseCapacityPatch.SuperWarehouseId;
        }
    }
}
