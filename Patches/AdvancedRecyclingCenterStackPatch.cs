using HarmonyLib;

namespace StacklandsProMaxNS
{
    /// <summary>
    /// 允许**回收中心之间互相叠放**（高级回收中心的配方要叠 5 张回收中心）。
    ///
    /// 背景：`RecyclingCenter : Factory` 的 `CanHaveCard` 只收 `pollution` 和 `Worker`
    /// （`Factory.DetermineCanHaveCardsWhenIsRoot => true`，所以整条判定走的是根卡自己的 `CanHaveCard`），
    /// 于是一张回收中心叠到另一张上面会被拒——5 张料的配方堆永远搭不出来。
    ///
    /// 做法对齐本项目已有的 `WarehouseStackingPatch`（超级仓库要叠 10 张仓库，同一个坑）：
    /// 在 `CardData.CanHaveCardOnTop` 上挂**后置**，只在原版已经返回 `false`、且
    /// "下面＝回收中心、上面＝回收中心"时改成 `true`。其它组合完全不受影响。
    ///
    /// ⚠️ 用后置而不是前缀：后置保留原版全部判定（损坏卡、堆叠上限……），只补这一种被拒的组合。
    ///    堆叠上限要按原版口径再算一次（原版"超上限"和"不收这张卡"用的是同一个 `return false`）。
    ///
    /// ⚠️ 只认原版 `recycling_center` 的 id，所以高级回收中心自己（id 不同）不会接受回收中心叠上来。
    /// </summary>
    [HarmonyPatch(typeof(CardData), "CanHaveCardOnTop")]
    internal static class AdvancedRecyclingCenterStackPatch
    {
        private const string RecyclingCenterId = "recycling_center";

        [HarmonyPostfix]
        internal static void Postfix(CardData __instance, CardData otherCard, ref bool __result)
        {
            if (__result || __instance == null || otherCard == null)
            {
                return;
            }
            if (__instance.Id != RecyclingCenterId || otherCard.Id != RecyclingCenterId)
            {
                return;
            }

            GameCard parentCard = __instance.MyGameCard;
            GameCard childCard = otherCard.MyGameCard;
            if (parentCard == null || childCard == null)
            {
                return;
            }
            if (parentCard.CardData.IsDamaged || childCard.CardData.IsDamaged)
            {
                return;
            }

            int total = parentCard.GetRootCard().GetChildCount() + 1
                + childCard.GetRootCard().GetChildCount() + 1;
            if (total > ModConfig.GetMaxStackSize())
            {
                return;
            }

            __result = true;
        }
    }
}
