using System.Collections.Generic;
using UnityEngine;

namespace StacklandsProMaxNS
{
    /// <summary>
    /// 「把一叠卡**往旁边的空地**弹出去」的共用做法（原版资源箱点击取出 / 食物容器取出 /
    /// 磁力箱拖出 / 松手回弹 四条路共用）。
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
    ///   - 方向＝从**场地中心指向箱子**的方向（＝朝外，正好是空地那一侧）；
    ///   - 落点＝按"先近后远、同一圈先朝外"的顺序试一组候选点（1.2 / 1.6 / 2.0 格 × 0°/±30°/±60°/
    ///     ±90°/±135°/180°），**第一个"这块地方没有别的卡"的就用它**（0.6.7）；
    ///   - 圈内一个空位都找不到（箱子被建筑围死了）才退回旧行为：朝外 1.2 格；
    ///   - 落点夹在场地范围内；
    ///   - 初速度用原版 `Draggable.SendToPosition` 的同款公式（`(偏移.x*4, 5, 偏移.z*4)`），
    ///     抛物线手感与原版一致；
    ///   - **不设 `BounceTarget`**，所以它不会自己去找箱子或别的卡。
    ///
    /// ── 0.6.7「优先往空地掉」（用户原话：「取出物品时，卡片的掉落位置也要改一下，改成优先往空地掉」）──
    ///
    /// 旧实现只会"朝离开场地中心的方向弹 1.2 格"，**不看那格有没有东西**：箱子紧挨着建筑、
    /// 或者旁边已经躺着一堆牌时，取出来的卡就正好压上去（还会被自动归堆吞掉、看着像"没掉下来"）。
    /// 现在落点用 `GameCard.GetOverlappingCardsInBox` 逐个试出来：候选点周围一个卡位内没有别的卡才算空地。
    /// 判定盒按卡牌自身的实际尺寸（`GetWidth`/`GetHeight`）取 80%，擦边不算占用，
    /// 免得相邻卡位之间永远找不到空位。
    ///
    /// 参数与公式来自 0.5.4 的磁力箱实现（用户当时实测"往边上掉"没问题），0.5.5 原样用到资源箱上，
    /// 0.5.6 又用到"拖出松手回弹"这条路上 —— 几处同一份代码，别再各写一份。
    /// </summary>
    internal static class ChestEject
    {
        /// <summary>弹出落点离箱子多远（格）。0.5.4 实测 1.2 足够离开箱子上方。</summary>
        internal const float EjectDistance = 1.2f;

        /// <summary>落点与场地边缘至少留这么远，免得卡被弹到界外。</summary>
        private const float BoardMargin = 0.3f;

        /// <summary>候选落点的距离梯度（格）：先近后远，越远越不自然，所以只在近处找不到空地时才往外找。</summary>
        private static readonly float[] CandidateDistances = { 1.2f, 1.6f, 2.0f };

        /// <summary>候选落点的角度梯度（度）：相对"朝外"方向的偏角，0° 先试（正对空地那一侧）。</summary>
        private static readonly float[] CandidateAngles = { 0f, 30f, -30f, 60f, -60f, 90f, -90f, 135f, -135f, 180f };

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
            Vector3 landing;
            if (!TryFindEmptyLanding(card, chestCard, out landing))
            {
                // 圈内全是卡（箱子被围死了）：退回旧行为，朝外 1.2 格
                landing = ClampToBoard(chestCard, origin + OutwardDirection(origin) * EjectDistance);
            }

            Vector3 offset = landing - origin;
            card.Velocity = new Vector3(offset.x * 4f, 5f, offset.z * 4f);
            card.TargetPosition = landing;
        }

        /// <summary>
        /// 取出材料时的弹出：**2 格内有同种牌堆就直接合并上去**（0.5.23 起为确定性 SetParent，
        /// 见下面实现注释；没有同种堆才找一块空地弹过去）。
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
        /// 试出一块"没有别的卡"的落点（0.6.7）。顺序＝先近后远，同一距离档从正朝外开始往两侧展开，
        /// 所以正常情况下的结果与旧行为几乎一样（朝外 1.2 格），只在那一格被占时才换个位置。
        /// 圈内找不到空位返回 false，由调用方退回旧行为。
        /// </summary>
        private static bool TryFindEmptyLanding(GameCard card, GameCard chestCard, out Vector3 landing)
        {
            landing = Vector3.zero;
            Vector3 origin = chestCard.transform.position;
            Vector3 outward = OutwardDirection(origin);
            for (int d = 0; d < CandidateDistances.Length; d++)
            {
                for (int a = 0; a < CandidateAngles.Length; a++)
                {
                    Vector3 candidate = origin + Rotate(outward, CandidateAngles[a]) * CandidateDistances[d];
                    candidate = ClampToBoard(chestCard, candidate);
                    if (!IsSpotFree(card, candidate))
                    {
                        continue;
                    }
                    landing = candidate;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 这个落点上"有没有别的卡"？两重判定：
        ///   ① 地面：用原版自己的重叠判定（`GetOverlappingCardsInBox`），判定盒＝卡牌实际尺寸的 80%
        ///      （擦边不算占用），高度给足 2 格，免得因为卡面 y 偏移漏判；
        ///   ② 空中：还在飞（`Velocity` 有值）的卡按它的 `TargetPosition` 比距离——否则连点两下取牌时，
        ///      第一张还在半空中，第二张会以为落点是空的、和它重合。
        /// 别的场地上的卡都不算：`OverlapBoxNonAlloc` 是全局的，而各场地是同时存在的。
        /// </summary>
        private static bool IsSpotFree(GameCard card, Vector3 pos)
        {
            Vector3 center = new Vector3(pos.x, card.transform.position.y, pos.z);
            List<GameCard> overlapping = card.GetOverlappingCardsInBox(center, CardFootprint(card));
            if (overlapping != null)
            {
                for (int i = 0; i < overlapping.Count; i++)
                {
                    GameCard other = overlapping[i];
                    if (other == null || other.Destroyed)
                    {
                        continue;
                    }
                    if (other.MyBoard != card.MyBoard)
                    {
                        continue;
                    }
                    return false; // 同场地上任何一张卡（包括源箱子自己）压着都算占用
                }
            }

            // 还在飞的卡也要算：上面的物理盒用的是"当前坐标"，刚取出来的那叠可能还在半空中，
            // 连点两下时第二张就会以为那块地是空的、落点和第一张重合。我们给弹出卡设过
            // `TargetPosition`（＝落点），所以这里按落点再比一次距离。
            List<GameCard> cards = WorldManager.instance.AllCards;
            for (int i = 0; i < cards.Count; i++)
            {
                GameCard flying = cards[i];
                if (flying == null || flying.Destroyed || flying == card || flying.MyBoard != card.MyBoard)
                {
                    continue;
                }
                if (!flying.Velocity.HasValue)
                {
                    continue;
                }
                Vector3 target = flying.TargetPosition;
                target.y = pos.y;
                if ((target - pos).sqrMagnitude < 0.64f)
                {
                    return false; // 0.8 格内已经有一张要飞过来了
                }
            }
            return true;
        }

        /// <summary>卡位的实际尺寸（世界单位）。取 80% 是为了"擦边不算占用"，否则相邻卡位之间永远没有空位。</summary>
        private static Vector3 CardFootprint(GameCard card)
        {
            float width = 1f;
            float height = 1f;
            try
            {
                width = card.GetWidth();
                height = card.GetHeight();
            }
            catch
            {
                // 预制体还没准备好时别让取牌整个失败，退回标准卡位尺寸
            }
            if (width <= 0.01f)
            {
                width = 1f;
            }
            if (height <= 0.01f)
            {
                height = 1f;
            }
            return new Vector3(width * 0.8f, 2f, height * 0.8f);
        }

        private static Vector3 ClampToBoard(GameCard chestCard, Vector3 landing)
        {
            Bounds bounds = chestCard.MyBoard.TightWorldBounds;
            landing.x = Mathf.Clamp(landing.x, bounds.min.x + BoardMargin, bounds.max.x - BoardMargin);
            landing.z = Mathf.Clamp(landing.z, bounds.min.z + BoardMargin, bounds.max.z - BoardMargin);
            return landing;
        }

        /// <summary>
        /// 弹出的基准方向：朝远离场地中心的一侧（这样卡往空地方向散，不会正好弹到紧挨着箱子的
        /// 另一张卡或建筑上）。箱子正好在场地正中时给一个默认方向（朝右，与原版 `StackSend`
        /// 的 outputDirection 一致）。
        /// </summary>
        private static Vector3 OutwardDirection(Vector3 origin)
        {
            WorldManager world = WorldManager.instance;
            Vector3 mid = world != null && world.CurrentBoard != null ? world.CurrentBoard.MiddleOfBoard() : Vector3.zero;
            Vector3 outward = origin - mid;
            outward.y = 0f;
            if (outward.sqrMagnitude < 0.0001f)
            {
                outward = Vector3.right;
            }
            outward.Normalize();
            return outward;
        }

        /// <summary>把水平方向绕 y 轴转 `degrees` 度（和旧实现同一个公式，只是把随机角改成确定的候选角）。</summary>
        private static Vector3 Rotate(Vector3 direction, float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            float sin = Mathf.Sin(rad);
            float cos = Mathf.Cos(rad);
            return new Vector3(direction.x * cos - direction.z * sin, 0f, direction.x * sin + direction.z * cos);
        }
    }
}
