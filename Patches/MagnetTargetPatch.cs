using System;
using HarmonyLib;

namespace StacklandsProMaxNS
{
    /// <summary>
    /// 接替 WorldManager.TrySendToMagnet：磁铁类卡片的"截胡"入口。
    ///
    /// 原版这个方法在 WorldManager.StackSend / StackSendCheckTarget 里被调用，也就是**所有"送卡"流程**
    /// （建筑产出、传送带输出、储物箱取货……）都会经过它。原版磁铁（ResourceMagnet）就是在这里
    /// 把符合条件的卡直接改道送进自己怀里——所以原版磁铁是"资源一产出就被吸走"。
    ///
    /// 这个补丁做两件事：
    ///   1) 原版磁铁：把写死的"堆叠数 &lt; 30"换成 ModSettings.MaxStackSize，
    ///      否则把卡牌堆叠上限提到 999 之后，磁铁还是只吸到 30 张。
    ///      （注意：这个 30 在编译后位于 lambda 的闭包方法里，对 TrySendToMagnet 做 transpiler 是无效的，
    ///        所以必须用 Prefix 接替整个方法——参考 mod HigherStacks 也是这么做的。）
    ///   2) 磁力箱：让它也参与同一套"截胡"，于是磁力箱和原版磁铁一样，
    ///      在资源产出/被送出的瞬间就把它吸走，而不是等它落到地上再被扫描到。
    ///
    /// 两者合在一起按"最近的"来选，和原版"取最近的目标"语义一致。
    /// </summary>
    [HarmonyPatch(typeof(WorldManager), "TrySendToMagnet")]
    internal static class MagnetTargetPatch
    {
        [HarmonyPrefix]
        internal static bool Prefix(GameCard card, WorldManager __instance, ref bool __result)
        {
            if (card == null || card.CardData == null || __instance == null)
            {
                return true; // 数据异常时交回原版处理
            }

            CardData nearestTarget = __instance.GetNearestCardMatchingPred(
                card, candidate => MatchesTarget(candidate, card));

            if (nearestTarget == null)
            {
                __result = false;
                return false;
            }

            // 只有原版磁铁才计入原版的"用过磁铁"任务进度
            if (nearestTarget is ResourceMagnet)
            {
                QuestManager.instance.SpecialActionComplete("use_magnet");
            }

            __instance.StackSendTo(card, nearestTarget.MyGameCard.GetLeafCard());
            __result = true;
            return false;
        }

        private static bool MatchesTarget(GameCard candidate, GameCard card)
        {
            // 磁力信用卡"点击吐钱"的瞬间，新生成的纸币谁都不许截胡（包括原版磁铁）：
            // 否则钱一出生就被认领回去，观感是"飞出去又掉头飞回卡里"（0.6.3）。
            if (MagnetCreditcard.SuppressMagnetClaim)
            {
                return false;
            }
            if (candidate == null || candidate.CardData == null)
            {
                return false;
            }
            if (candidate.MyBoard != card.MyBoard)
            {
                return false;
            }

            // 1) 原版磁铁（Cities DLC）：匹配它设定的资源类型；堆叠上限跟随设置里的"卡牌堆叠上限"输入框
            //    （填 30 ＝原版行为，等于没打这个补丁）
            ResourceMagnet magnet = candidate.CardData as ResourceMagnet;
            if (magnet != null)
            {
                int stackCap = ModConfig.GetMaxStackSize();
                return magnet.PullCardId == card.CardData.Id
                    && candidate.GetStackCount() < stackCap;
            }

            // 2) 磁力箱 / 硬币磁力箱：也参与"截胡"（原版照旧只认 ResourceMagnet）
            MagnetChest chest = candidate.CardData as MagnetChest;
            if (chest != null)
            {
                return chest.WantsCard(card.CardData);
            }
            CoinMagnetChest coinChest = candidate.CardData as CoinMagnetChest;
            if (coinChest != null)
            {
                return coinChest.WantsCard(card.CardData);
            }

            // 3) 磁力信用卡：抢纸币（原版信用卡自己不会主动吸，只会吞叠上来的）
            MagnetCreditcard creditcard = candidate.CardData as MagnetCreditcard;
            if (creditcard != null)
            {
                return creditcard.WantsCard(card.CardData);
            }

            return false;
        }
    }
}
