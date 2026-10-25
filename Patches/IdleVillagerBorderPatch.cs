using HarmonyLib;
using Shapes;
using UnityEngine;

namespace StacklandsProMaxNS
{
    /// <summary>
    /// 空闲村民 / **空闲生产设施** 显示红色边框。
    ///
    /// 用的是原版自带的"卡片高亮框"：
    ///   - GameCard.HighlightRectangle（Shapes.Rectangle，虚线方框）
    ///   - GameCard.HighlightActive
    /// 原版 GameCard.Update 里会在 HighlightActive 为真时用它画框，颜色取当前场地的
    /// CardHighlightColor。所以这里在 Update 之后（Postfix）把它改成红色即可，
    /// 同时自己把 enabled 置真（原版那行赋值已经跑过了，这帧不置真就看不到）。
    ///
    /// ── 村民那一半（0.5.0 合并） ─────────────────────────────────────────────
    ///
    /// "空闲"的判定（参考 mod Show Idle Villager 的判定，并补了一条"在干活的不算空闲"）：
    ///   - 是村民（BaseVillager）
    ///   - 没有在建筑里工作（!IsWorking）—— 参考 mod 没查这条；建筑里的工人虽然卡片
    ///     被收进建筑、但保险起见这里排除掉，语义上更贴近"空闲"
    ///   - 没有父卡也没有子卡（没被叠在别的东西上、手上也没拿东西）
    ///   - 不在战斗中（!InConflict）
    ///   - 玩家没有正在拖它
    ///
    /// ── 生产设施那一半（0.5.6 合并，用户原话：「把Show Idle Villager这个mod的生产设施高亮也合并过来」）──
    ///
    /// 参考 mod 的 `IsFarmIdle()`（反编译核实）认**五类卡**，判定就是 **`Parent == null && Child == null`**：
    ///
    /// | 卡 | 是什么 |
    /// | --- | --- |
    /// | `Garden` | 菜园 / 农场（原版 `growMethods` 里的 `garden` / `farm` 都是它） |
    /// | `FishingSpot` | 钓鱼点（`Harvestable` 子类，上面站着渔夫／已经钓到东西都是子卡） |
    /// | `FishTrap` | 捕鱼陷阱 |
    /// | `Greenhouse` | 温室 |
    /// | `Poop` | 便便——它出现在原版 `growMethods = { soil, poop, garden, farm, greenhouse }` 里，是**能种东西的"基质"**，空着就还能用上，所以参考 mod 也把它算进"食物生产点" |
    ///
    /// 这些卡"有没有在用"恰好就体现在**上面有没有叠东西**：长出来的食物、派去干活的村民都是它的子卡，
    /// 所以 `Child == null` ＝"空着、还没用上"，正是玩家想一眼看出来的那些卡。
    ///
    /// 与原 mod 的差异：
    ///   - 原 mod 给五类卡**各做一个开关**（外加村民一个），本项目按"开关要克制"收成
    ///     **两个开关**：「空闲村民红色边框」+「空闲生产设施红色边框」；
    ///   - 和村民那条一样，额外排除"正被玩家拖着"的卡（原 mod 没查）；
    ///   - 颜色统一取 `ModSettings.IdleBorderColor`（原 mod 两者都是 `Color.red`，这里保持红色）。
    /// </summary>
    [HarmonyPatch(typeof(GameCard), "Update")]
    internal static class IdleVillagerBorderPatch
    {
        [HarmonyPostfix]
        internal static void Postfix(GameCard __instance)
        {
            if (!ModConfig.IdleVillagerBorder && !ModConfig.IdleFacilityBorder)
            {
                return; // 设置界面里两个开关都关了
            }
            if (__instance == null || __instance.CardData == null || __instance.IsDemoCard)
            {
                return;
            }
            // GameCard.Update 会在非当前场地时提前 return，但 Postfix 仍会执行，所以要自己判断
            if (__instance.MyBoard == null || !__instance.MyBoard.IsCurrent)
            {
                return;
            }

            bool highlight =
                (ModConfig.IdleVillagerBorder
                    && __instance.CardData is BaseVillager
                    && IsIdleVillager(__instance))
                || (ModConfig.IdleFacilityBorder && IsIdleFacility(__instance));
            if (!highlight)
            {
                return;
            }

            Rectangle rectangle = __instance.HighlightRectangle;
            if (rectangle == null)
            {
                return;
            }
            rectangle.enabled = true;
            rectangle.Color = ModSettings.IdleBorderColor;
            __instance.HighlightActive = true;
        }

        private static bool IsIdleVillager(GameCard card)
        {
            if (card.BeingDragged)
            {
                return false; // 玩家正拿着它
            }
            if (card.IsWorking)
            {
                return false; // 正在建筑里干活
            }
            if (card.Parent != null || card.Child != null)
            {
                return false; // 被叠在别的东西上，或者手上拿着/叠着东西
            }
            if (card.InConflict)
            {
                return false; // 战斗中
            }
            return true;
        }

        /// <summary>
        /// 这张卡是"空着的生产设施"吗？（五类卡 + 没有父卡也没有子卡，见类注释）
        /// </summary>
        private static bool IsIdleFacility(GameCard card)
        {
            CardData data = card.CardData;
            if (!(data is Garden) && !(data is FishingSpot) && !(data is FishTrap)
                && !(data is Greenhouse) && !(data is Poop))
            {
                return false;
            }
            if (card.BeingDragged)
            {
                return false; // 玩家正拿着它（原 mod 没查这条，我们补上）
            }
            // 原 mod 的判据：没有父卡也没有子卡 ＝ 上面没长东西、也没派人/放种子
            return card.Parent == null && card.Child == null;
        }
    }
}
