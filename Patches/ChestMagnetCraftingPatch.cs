using HarmonyLib;

namespace StacklandsProMaxNS
{
    /// <summary>
    /// 让「磁力箱 / 硬币磁力箱 / 贝壳磁力箱 / 磁力信用卡」的配方（磁铁 + 箱子/信用卡）真的能合成。
    ///
    /// 背景（原版机制）：合成并不需要创意卡在堆里——把材料叠成一条堆叠后，由**根卡**的
    /// CardData.CheckBlueprintInStack() 扫描所有蓝图来匹配 Subprint（见 StackMatchesSubprint）。
    /// 而 ResourceChest.CanHaveCard 只收 CardType.Resources、Chest（硬币箱）只收金币，
    /// ResourceMagnet 是 Structures，于是这些卡根本叠不到一起，配方永远做不出来。
    ///
    /// 这里放行的是「箱子叠到磁铁上」这个方向（磁铁为根、箱子为子卡），原因：
    ///   - 磁铁不会吞子卡，它只是持有；箱子叠上去完全安全
    ///   - 合成计时器由根卡启动，根是磁铁时能正常启动
    ///
    /// 为什么不顺手也做反方向（磁铁叠到箱子上）：
    ///   箱子当根时，ResourceChest.UpdateCard 的子卡循环会把子卡 DestroyCard 掉并计成材料
    ///   （ResourceCount++、HeldCardId 被设成磁铁 id）——那会把玩家的磁铁销毁掉，不可接受。
    ///   所以两个配方显示顺序都是「磁铁在前（先放磁铁），箱子在后（再叠上去）」。
    /// </summary>
    [HarmonyPatch(typeof(ResourceMagnet), "CanHaveCard")]
    internal static class ChestMagnetCraftingPatch
    {
        [HarmonyPostfix]
        internal static void Postfix(ResourceMagnet __instance, CardData otherCard, ref bool __result)
        {
            if (__result)
            {
                return;
            }
            if (__instance == null || __instance.MyGameCard == null)
            {
                return;
            }
            if (!MatchesRecipe(otherCard))
            {
                return;
            }
            // 磁铁顶上只能放一张
            if (__instance.MyGameCard.HasChild)
            {
                return;
            }
            __result = true;
        }

        /// <summary>
        /// 能被叠到磁铁上的原料：资源箱 → 磁力箱；硬币箱 / 贝壳箱 → 硬币磁力箱 / 贝壳磁力箱；
        /// 原版信用卡 → 磁力信用卡。
        /// </summary>
        private static bool MatchesRecipe(CardData otherCard)
        {
            if (otherCard is ResourceChest)
            {
                return true;
            }
            if (otherCard is CoinMagnetChest)
            {
                return false; // 硬币磁力箱 / 贝壳磁力箱是产物，不是原料
            }
            if (otherCard is MagnetCreditcard)
            {
                return false; // 磁力信用卡同样是产物
            }
            if (otherCard is Creditcard)
            {
                return true; // 原版信用卡（creditcard）
            }
            return otherCard is Chest; // 原版硬币箱（coin_chest / shell_chest 都算）
        }
    }
}
