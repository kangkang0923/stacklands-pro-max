using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace StacklandsProMaxNS
{
    /// <summary>
    /// **月末吃饭的食物优先级**（0.5.9）：先吃炖锅（火锅）里的，**不吃叠在高级温室上的食物**。
    ///
    /// 用户需求原话：「吃饭的时候不应该吃高级温室上的物品，应该先吃炖锅的」。
    ///
    /// ── 原版的"吃饭"是怎么选食物的（反编译核实） ─────────────────────────────
    ///
    /// 村民吃饭只发生在**月末动画**里：`Cutscenes.FeedVillagers()` 给每个村民逐份取食物，
    /// 取哪份由私有静态方法 **`Cutscenes.GetFoodToUseUp()`** 决定——把场上所有 `Food` 卡
    /// 按下面这个**排序键**升序排（越小越先被吃）：
    ///
    /// ```text
    ///   堆里有 MessHall（食堂）   → -1000 + 堆内下标          // 食堂最先
    ///   是 Hotpot（炖锅/火锅）    → -100 + FoodValue          // ★ 炖锅次之
    ///   在变质且没在计时          → -5
    ///   当前 Demand 要这张卡      →  5
    ///   堆里有卡在计时（生产中）   →  4
    ///   全场仅剩 1 张且是生食      →  3
    ///   生食                      →  2
    ///   熟食                      →  0
    /// ```
    ///
    /// 排完再按 `FoodValue`（份量小的先吃）、卡牌价值细分，取第一张 `FoodValue > 0` 的。
    ///
    /// ── 为什么这两个需求会出问题（都是本 mod 自己引入的） ─────────────────────
    ///
    /// ① **炖锅反而最后才被吃**：原版键值 `-100 + FoodValue` 是在"火锅最多囤 50 点"的前提下写的
    ///    （50 - 100 = -50，永远排在熟食 0 / 生食 2 前面）。而本 mod 的「火锅容量 10000」
    ///    （`HotpotCapacityPatch`）把 `FoodValue` 顶到了几千上万——键值变成几千的正数，
    ///    炖锅从"最先吃"跌到"几乎最后吃"。修法：键值改成 `-100 + Math.Min(FoodValue, 50)`，
    ///    把 FoodValue 夹回原版假设的范围，炖锅恢复"仅次于食堂"的优先级（多个炖锅时份量少的先吃）。
    /// ② **高级温室上的"种子"被吃掉**：高级温室的自动循环靠"6 个同种食物一直叠在温室上"
    ///    （`AdvancedGreenhousePatch`：什么都不消耗 → 种子留着 → 配方下一帧仍然成立）。
    ///    而生长中的温室**有计时器**（`GetCardWithStatusInStack()` 认 `TimerRunning`）→ 键值 4，
    ///    别处没熟食/生食时就轮到它们——月末一顿饭把温室的种子吃掉，生产线就断了。
    ///    修法：把"堆里含高级温室"的食物**整体排除**（连候选都进不了，而不是排最后——
    ///    用户口径是"不应该吃"，别的食物吃光时宁可让村民饿着）。
    ///
    /// ── 为什么是 Prefix 接替整个方法 ─────────────────────────────────────────
    ///
    /// 排序键写在 `OrderBy` 的**闭包 lambda**里（编译器生成的嵌套方法，对外层方法做 transpiler
    /// 够不着它——和 `MagnetTargetPatch` 接替 `TrySendToMagnet` 是同一个原因），所以这里用
    /// Prefix 返回 false 接替整个 `GetFoodToUseUp`，逐行对齐原版逻辑、只动上面两处。
    /// 异常时放行交回原版（和本项目其它补丁同款兜底）。
    ///
    /// ⚠️ 方法所在的类**随游戏版本挪过窝**：本地反编译产物（旧版）里在 `Cutscenes`，
    /// 实测当前游戏 DLL（ilspycmd 核实）已挪到 `EndOfMonthCutscenes`，方法名和逻辑都没变。
    /// 所以用 `TargetMethod()` 动态绑定：优先 `EndOfMonthCutscenes`，找不到再回退 `Cutscenes`。
    ///
    /// ⚠️ 范围：只动**月末吃饭**这一条路。Cities 的需求/住房消耗走的是另一条
    /// `CitiesManager.GetFoodToUse()`（按份量从大到小、计时中的排最后），不是"村民吃饭"，不碰。
    /// </summary>
    [HarmonyPatch]
    internal static class FeedingPriorityPatch
    {
        private static bool loggedError;

        /// <summary>按游戏版本绑到真正写着吃饭选食物逻辑的那个类（见类注释"挪过窝"）。</summary>
        private static MethodBase TargetMethod()
        {
            MethodBase shipped = AccessTools.Method(typeof(EndOfMonthCutscenes), "GetFoodToUseUp");
            if (shipped != null)
            {
                return shipped;
            }
            return AccessTools.Method(typeof(Cutscenes), "GetFoodToUseUp")
                ?? AccessTools.Method(typeof(EndOfMonthCutscenes), "GetFoodToUseUp"); // 都没有 → PatchAll 会报错进日志（Mod.cs 里 try/catch 住了）
        }

        [HarmonyPrefix]
        internal static bool Prefix(ref Food __result)
        {
            try
            {
                if (WorldManager.instance == null)
                {
                    return true; // 数据异常时交回原版处理
                }

                List<Food> cards = WorldManager.instance.GetCards<Food>();
                if (cards.Count == 0)
                {
                    __result = null;
                    return false;
                }

                // 对齐原版：当前 Demand（有的话）参与排序键
                Demand currentDemand = WorldManager.instance.CurrentRunVariables.ActiveDemand != null
                    ? DemandManager.instance.GetDemandById(WorldManager.instance.CurrentRunVariables.ActiveDemand.DemandId)
                    : null;

                __result = cards
                    .Where(delegate (Food c)
                    {
                        return c.FoodValue > 0 && !IsOnAdvancedGreenhouse(c);
                    })
                    .OrderBy(delegate (Food c) { return SortKey(c, currentDemand); })
                    .ThenBy(delegate (Food x) { return x.FoodValue; })
                    .ThenBy(delegate (Food x) { return x.GetValue(); })
                    .FirstOrDefault();

                return false;
            }
            catch (Exception ex)
            {
                if (!loggedError)
                {
                    loggedError = true;
                    Debug.LogWarning("[StacklandsProMax] 吃饭优先级补丁异常（本次交回原版逻辑）：" + ex);
                }
                return true;
            }
        }

        /// <summary>
        /// 这份食物是不是叠在高级温室那一堆里（温室本体或它的任意子卡都算）？
        /// 是 → 村民吃饭时**完全不选它**：那 6 个"种子"是高级温室自动循环的生产原料。
        /// </summary>
        private static bool IsOnAdvancedGreenhouse(Food food)
        {
            if (food == null || food.MyGameCard == null)
            {
                return false;
            }
            return food.MyGameCard.HasCardInStack(
                (CardData x) => x != null && x.Id == AdvancedGreenhouse.CardId) != null;
        }

        /// <summary>
        /// 原版排序键（对齐），唯一的改动：Hotpot 的 `-100 + FoodValue` 夹成
        /// `-100 + Math.Min(FoodValue, 50)`——见类注释 ①。
        /// </summary>
        private static int SortKey(Food c, Demand currentDemand)
        {
            bool hasStatusInStack = c.MyGameCard.GetCardWithStatusInStack() != null;
            if (c.MyGameCard.HasCardInStack((CardData x) => x is MessHall))
            {
                return -1000 + c.MyGameCard.GetCardIndex();
            }
            if (c is Hotpot)
            {
                // ★ 原：-100 + FoodValue。原版 FoodValue ≤ 50 时它稳居第二（仅次于食堂）；
                //   本 mod 把火锅容量提到 10000 后这个键值会变成大正数，炖锅反而最后吃。
                //   夹回 50 = 恢复原版的相对顺序；多个炖锅之间仍按份量（ThenBy FoodValue）细分。
                return -100 + Math.Min(c.FoodValue, 50);
            }
            if (c.IsSpoiling && !hasStatusInStack)
            {
                return -5;
            }
            if (currentDemand != null)
            {
                if (currentDemand.CardToGet == "royal_banquet"
                    && (c.Id == "fruit_salad" || c.Id == "wine" || c.Id == "roasted_meat" || c.Id == "olive_oil"))
                {
                    return 5;
                }
                if (currentDemand.CardToGet == c.Id)
                {
                    return 5;
                }
            }
            if (hasStatusInStack)
            {
                return 4;
            }
            if (WorldManager.instance.GetCardCount(c.Id) == 1 && !c.IsCookedFood)
            {
                return 3;
            }
            return (!c.IsCookedFood) ? 2 : 0;
        }
    }
}
