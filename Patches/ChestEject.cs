using System.Collections.Generic;
using UnityEngine;

namespace StacklandsProMaxNS
{
    /// <summary>
    /// 「把一叠卡**朝旁边弹到空地**上」的共用做法（原来只写在 `ResourceChestEjectPatch` 里，
    /// 0.5.6 起磁力箱"拖出后松手还压在箱子上"那条路也复用它）。
    ///
    /// ── 为什么不能用原版的 `WorldManager.StackSend` ───────────────────────────
    ///
    /// 原版 `StackSend` 的第一句是：
    ///
    /// 三道截胡：磁铁优先（`TrySendToMagnet`）、其次送箱子（`TrySendToChest`，受 `sendToChest` 开关控制）、
    /// 已有落点（`BounceTarget != null`）的直接返回。
    ///
    /// `TrySendToChest` 专门去找"最近的、能收下这张卡的 `Chest`"，而刚从箱子里取出来的这叠
    /// **位置就在箱子上方、类型完全匹配** → 首选目标就是它自己这个箱子，
    /// 表现就是"取出来又自己飞回去"（0.5.5 资源箱那轮踩过，见 `ResourceChestEjectPatch` 的注释）。
    /// 所以本项目凡是要"让卡离开箱子"的地方都**不走 `StackSend`**，而是用下面这套确定性的弹射。
    ///
    /// ── 这套弹射的做法 ───────────────────────────────────────────────────────
    ///
    ///   - 方向＝从**场地中心指向箱子**的方向（＝朝外，正好是空地那一侧），再带 ±20° 随机偏角，
    ///     连续取多张时不会全叠在同一个点上；
    ///   - 落点＝箱子位置 + 方向 × <see cref="EjectDistance"/>（默认 1.2 格），并夹在场地范围内；
    ///   - 初速度用原版 `Draggable.SendToPosition` 的同款公式（`(偏移.x*4, 5, 偏移.z*4)`），
    ///     抛物线手感与原版一致；
    ///   - **不设 `BounceTarget`**，所以它不会自己去找箱子或别的卡。
    ///
    /// 参数与公式来自 0.5.4 的磁力箱实现（用户当时实测"往边上掉"没问题），0.5.5 原样用到资源箱上，
    /// 0.5.6 又用到"拖出松手回弹"这条路上 —— 三处同一份代码，别再各写一份。
    /// </summary>
    internal static class ChestEject
    {
        /// <summary>弹出落点离箱子多远（格）。0.5.4 实测 1.2 足够离开箱子上方。</summary>
        internal const float EjectDistance = 1.2f;

        /// <summary>落点与场地边缘至少留这么远，免得卡被弹到界外。</summary>
        private const float BoardMargin = 0.3f;

        /// <summary>
        /// 把这一叠朝**远离场地中心**的方向弹出去。调用前请先确认这张卡已经不再被鼠标拖着
        /// （否则会被原版每帧重设的 `TargetPosition` 覆盖）。
        /// </summary>
        internal static void EjectToSide(GameCard card, GameCard chestCard)
        {
            if (card == null || card.Destroyed || chestCard == null || chestCard.Destroyed || chestCard.MyBoard == null)
            {
                return;
            }
            if (card.BounceTarget != null)
            {
                // 已经有磁力族（磁力箱/磁力容器）在 `TrySendToMagnet` 里认领了这张卡的落点，
                // 再改写成"往边上掉"会让它先飞向侧面、落地后再被吸进容器（0.6.1 用户反馈的
                // "点资源容器取水，水往左边落地就消失了"）。放手让它自己飞过去。
                return;
            }

            Vector3 origin = chestCard.transform.position;
            Vector3 landing = origin + EjectDirection(origin) * EjectDistance;

            Bounds bounds = chestCard.MyBoard.TightWorldBounds;
            landing.x = Mathf.Clamp(landing.x, bounds.min.x + BoardMargin, bounds.max.x - BoardMargin);
            landing.z = Mathf.Clamp(landing.z, bounds.min.z + BoardMargin, bounds.max.z - BoardMargin);

            Vector3 offset = landing - origin;
            card.Velocity = new Vector3(offset.x * 4f, 5f, offset.z * 4f);
            card.TargetPosition = landing;
        }

        /// <summary>
        /// 取出材料时的弹出：**2 格内有同种牌堆就直接合并上去**（0.5.23 起为确定性 SetParent，
        /// 见下面实现注释；没有同种堆才退回"往边上弹"）。
        ///
        /// 为什么不直接调原版 `StackSend`：它开头会先试 `TrySendToMagnet` / `TrySendToChest`，
        /// 而刚取出的这叠就在源箱子顶上、类型又匹配 → 会被送回箱子（0.5.5 踩过的坑，
        /// 见 `ResourceChestEjectPatch` 注释）。所以这里只复刻 `StackSend` 的**同种牌堆扫描**：
        /// 2 格内、同 id、能收（`CanHaveCardOnTop`）、不在战斗/没被拖/状态允许。
        /// </summary>
        internal static void EjectToPileOrSide(GameCard card, GameCard chestCard)
        {
            if (card == null || card.Destroyed || chestCard == null || chestCard.Destroyed || chestCard.MyBoard == null)
            {
                return;
            }
            if (card.BounceTarget != null)
            {
                // 磁力族（磁力箱 / 磁力容器）已经在 `TrySendToMagnet` 里认领了这张卡 ——
                // 取出来的卡应该**直接飞进容器**，而不是先被我们弹到侧面、落地后再被吸走。
                // 0.6.1 用户反馈：「点水容器取出卡片时，卡片直接往左边落地并消失，右边的水磁力容器 +1」。
                return;
            }

            GameCard best = null;
            float bestDistance = float.MaxValue;
            Vector3 origin = card.transform.position;
            List<GameCard> cards = WorldManager.instance.AllCards;
            for (int i = 0; i < cards.Count; i++)
            {
                GameCard pile = cards[i];
                if (pile == null || pile.CardData == null || pile == card || pile.MyBoard == null
                    || !pile.MyBoard.IsCurrent || pile.CardData.Id != card.CardData.Id)
                {
                    continue;
                }
                GameCard statusCard = pile.GetCardWithStatusInStack();
                if (statusCard != null && !statusCard.CardData.CanHaveCardsWhileHasStatus())
                {
                    continue;
                }
                if (pile.GetCardInCombatInStack() != null || pile.BeingDragged
                    || pile.IsChildOf(card) || pile.IsParentOf(card))
                {
                    continue;
                }
                if (!pile.CardData.CanHaveCardOnTop(card.CardData))
                {
                    continue;
                }
                Vector3 delta = pile.transform.position - origin;
                delta.y = 0f;
                float distance = delta.magnitude;
                if (distance <= 2f && distance < bestDistance)
                {
                    best = pile;
                    bestDistance = distance;
                }
            }

            if (best != null)
            {
                // 0.5.23：不再走 BounceTarget + 抛物线飞行 + 落地回调 Bounce 的链路——
                // 用户实测：单张这样飞过去能合并，**10 张的叠飞过去不合并**（原因未定位到，
                // 但手动把 10 叠拖到牌堆上＝原版 CheckIfCanAddOnStack 的 SetParent 路径是好的）。
                // 所以这里照原版 Bounce 的解析（GetLeafCard 到牌堆顶）**立即 SetParent**，确定性合并；
                // 卡牌位置由原版每帧的位置插值平滑滑过去，观感接近原版落地叠上。
                GameCard leaf = best.GetLeafCard();
                GameCard statusCard = leaf == null ? null : leaf.GetCardWithStatusInStack();
                bool canMerge = leaf != null && !leaf.Destroyed && leaf != card
                    && leaf.MyBoard != null && leaf.MyBoard.IsCurrent
                    && !leaf.BeingDragged && leaf.GetCardInCombatInStack() == null
                    && (statusCard == null || statusCard.CardData.CanHaveCardsWhileHasStatus())
                    && leaf.CardData != null && leaf.CardData.CanHaveCardOnTop(card.CardData);
                if (canMerge)
                {
                    card.SetParent(leaf);
                    card.Velocity = null;
                    card.BounceTarget = null;
                    AudioManager.me.PlaySound2D(AudioManager.me.DropOnStack,
                        UnityEngine.Random.Range(0.8f, 1.2f), 0.3f); // 与原版 Bounce 叠上时同款音效
                    return;
                }
                EjectToSide(card, chestCard);
                return;
            }
            EjectToSide(card, chestCard);
        }

        /// <summary>
        /// 弹出的水平方向：朝远离场地中心的一侧（这样卡往空地方向散，不会正好弹到紧挨着箱子的
        /// 另一张卡或建筑上），并带一点随机偏角。
        /// 箱子正好在场地正中时给一个默认方向（朝右，与原版 `StackSend` 的 outputDirection 一致）。
        /// </summary>
        private static Vector3 EjectDirection(Vector3 origin)
        {
            Vector3 outward = origin - WorldManager.instance.CurrentBoard.MiddleOfBoard();
            outward.y = 0f;
            if (outward.sqrMagnitude < 0.0001f)
            {
                outward = Vector3.right;
            }
            outward.Normalize();

            float angle = UnityEngine.Random.Range(-0.35f, 0.35f); // ±20° 左右的散开角
            float sin = Mathf.Sin(angle);
            float cos = Mathf.Cos(angle);
            return new Vector3(outward.x * cos - outward.z * sin, 0f, outward.x * sin + outward.z * cos);
        }
    }
}
