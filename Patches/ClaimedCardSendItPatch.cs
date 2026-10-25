using HarmonyLib;

namespace StacklandsProMaxNS
{
    /// <summary>
    /// **已经被认领（有 `BounceTarget`）的卡，不许再被 `SendIt()` 改掉飞行方向。**
    ///
    /// ── 症状（0.6.4 用户实测）────────────────────────────────────────────────
    ///   左边一张原版信用卡、右边一张磁力信用卡，点左边的信用卡取钱：
    ///   纸币**往左**飞出去、落地后才"消失"（其实是落地那一刻被 `Bounce()` 挪进磁力信用卡）。
    ///   期望是出钱的瞬间就**往右直接飞进磁力信用卡**。
    ///
    /// ── 根因 ────────────────────────────────────────────────────────────────
    ///   原版 `Creditcard.Clicked` → `WorldManager.CreateDollarsFromValue`：
    ///     ① `CreateCard` 的最后一步会调 `TrySendToMagnet`（反编译核对）→ 磁力信用卡当场认领，
    ///        `StackSendTo` 给纸币设好"朝磁力卡飞"的 `BounceTarget` + `Velocity`；
    ///     ② 紧接着 `CreateDollarsFromValue` 结尾又无条件来一句 `list.First().SendIt()`（反编译核对），
    ///        而 `GameCard.SendIt` 在 Cities 场地会把 `Velocity` 直接改成
    ///        `GetRootCard().CardData.OutputDir * 7f`（默认朝左）——**把认领好的飞行方向覆盖掉了**。
    ///   于是纸币朝左飞，落地时 `Bounce()` 才按 `BounceTarget` 把它挪进磁力卡，观感就是"落地即消失"。
    ///
    /// ── 修法 ────────────────────────────────────────────────────────────────
    ///   `GameCard.SendIt` 前置换成：有 `BounceTarget`（＝已经有落点：磁力卡、管道、牌堆）
    ///   就直接跳过。`BounceTarget` 只在落地 `Bounce()` 和开始拖拽时清空，所以这条守卫
    ///   只影响"正在飞向某个目标"的卡，不会拦住任何"该被抛出去"的卡。
    ///   这也和 0.6.1 给 `EjectToSide` / `EjectToPileOrSide` 补的 `BounceTarget != null` 守卫同一条思路。
    /// </summary>
    [HarmonyPatch(typeof(GameCard), "SendIt")]
    internal static class ClaimedCardSendItPatch
    {
        [HarmonyPrefix]
        internal static bool Prefix(GameCard __instance)
        {
            if (__instance == null)
            {
                return true;
            }
            // 已经有落点（磁力家族认领 / 管道 / 目标牌堆）→ 保持原飞行，别改成"朝输出方向抛出去"
            return __instance.BounceTarget == null;
        }
    }
}
