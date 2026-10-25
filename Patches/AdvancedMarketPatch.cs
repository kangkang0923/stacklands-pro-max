using HarmonyLib;

namespace StacklandsProMaxNS
{
    /// <summary>
    /// 让**不同种类**的可卖卡能摞在同一座高级集市上。
    ///
    /// 用户需求（0.5.19）：「所有堆叠在高级商店上的物品可以一次性全部售出（参考炖锅）」。集市一格
    /// 只有一条叠（市场→卡A→卡B…），而材料自身不允许叠在别的材料上（木头.CanHaveCardOnTop(苹果)=false）——
    /// 资源箱 A 的木头摞上去之后，资源箱 B 的石头就进不来了（落在集市边上、卖不掉）。
    ///
    /// 做法：`CardData.CanHaveCardOnTop` 的 Postfix——这张卡**已经叠在高级集市上**（根卡是集市、
    /// 但它自己不是集市）时，放行压上来的可卖卡。集市本体的收卡判定仍由
    /// `AdvancedMarket.CanHaveCard` 把关（整叠都可卖才收），所以不可卖的卡（村民、建筑…）照样进不来。
    /// 到点后 `AdvancedMarket.SellEverything` 按整叠总价值一次性结算（`WorldManager.SellCard`
    /// 反编译核实＝`GetStackValue` + `DestroyStack`，混叠也能正确算钱）。
    /// </summary>
    [HarmonyPatch(typeof(CardData), "CanHaveCardOnTop")]
    internal static class AdvancedMarketStackPatch
    {
        [HarmonyPostfix]
        internal static void Postfix(CardData __instance, ref bool __result)
        {
            if (__result || __instance == null || __instance.MyGameCard == null)
            {
                return;
            }
            // 本卡自己不是集市（集市自身的收卡判定由 CanHaveCard 把关），但根卡是集市 ＝ 已经摞在集市上
            if (__instance is AdvancedMarket)
            {
                return;
            }
            GameCard root = __instance.MyGameCard.GetRootCard();
            if (root != null && root.CardData is AdvancedMarket)
            {
                __result = true;
            }
        }
    }
}
