using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;

namespace StacklandsProMaxNS
{
    /// <summary>
    /// 双击一张牌 → 把附近**同种卡**全部吸过来叠成一堆。合并自 mod **Better Mouse Controls**
    /// （创意工坊 3012078700，id `BetterMouseControls`，作者 benediktwerner）的
    /// 「Doubleclick Restack」功能。
    ///
    /// 我读了它的 DLL 确认补丁点是 `WorldManager.Update` 的后置补丁（关于游戏本身的事实），
    /// 代码在本项目里按自己的结构重写，并做了一处适配：**上限跟随本 mod 的堆叠上限**
    /// （设置界面里的「卡牌堆叠上限 100」），所以归堆能一直叠到上限值（现 100）张，
    /// 而不是像参考 mod 那样写死 30。
    ///
    /// 规则（与参考 mod 一致）：
    ///   - 两次点击间隔 &lt; 0.5 秒、鼠标移动 &lt; 5 像素、且两次都点在同一张牌上，才算双击
    ///   - 点到的牌必须是"干净的同种堆"：它往上/往下整条链都是同种卡（村民算村民、装备算装备）
    ///   - 不能是装备中的卡、不能是储物箱类（原版储物箱点击是取物，归堆会打架）
    ///   - 把 `RestackRange` 半径内其它**同种、无父卡**的卡吸到自己这一叠上
    ///   - 如果点到的牌正在战斗：把附近的同种卡直接拉进同一场战斗（参考 mod 的行为）
    /// </summary>
    [HarmonyPatch(typeof(WorldManager), "Update")]
    internal static class DoubleClickRestackPatch
    {
        private static float previousClickTime;
        private static Vector2 previousMousePos;
        private static Draggable previousDraggable;

        [HarmonyPostfix]
        internal static void Postfix(WorldManager __instance)
        {
            if (!ModConfig.DoubleClickRestack)
            {
                // 设置界面里刚关掉：清掉记忆，避免再打开时拿旧的"上一次点击"误判成双击
                previousDraggable = null;
                return;
            }
            if (__instance == null || InputController.instance == null || Mouse.current == null)
            {
                return;
            }
            if (!__instance.CanInteract || !InputController.instance.GetInputBegan(0))
            {
                return;
            }
            if (GameCanvas.instance != null
                && GameCanvas.instance.PositionIsOverUI(InputController.instance.GetInputPosition(0)))
            {
                return; // 点在 UI 上不算
            }

            Vector2 mousePos = Mouse.current.position.ReadValue();

            // 双击判定：时间、距离、而且是同一张牌
            if (previousDraggable != null
                && __instance.HoveredDraggable == previousDraggable
                && Time.time - previousClickTime < ModSettings.DoubleClickMaxDelay)
            {
                Vector2 moved = previousMousePos - mousePos;
                if (moved.sqrMagnitude <= ModSettings.DoubleClickMaxDistance * ModSettings.DoubleClickMaxDistance)
                {
                    TryRestack(previousDraggable as GameCard, __instance);
                }
            }

            previousMousePos = mousePos;
            previousDraggable = __instance.HoveredDraggable;
            previousClickTime = Time.time;
        }

        /// <summary>把附近同种的散卡吸到 <paramref name="clicked"/> 这一叠上。</summary>
        private static void TryRestack(GameCard clicked, WorldManager world)
        {
            if (clicked == null || clicked.CardData == null)
            {
                return;
            }
            // 只处理"干净的同种堆"：往下（子卡）或往上（父卡）整条链都是同种卡
            bool cleanChain = (clicked.Child == null && AllParentsAreSameCard(clicked))
                || (clicked.Parent == null && AllChildrenAreSameCard(clicked))
                || InConflict(clicked);
            if (!cleanChain)
            {
                return;
            }
            if (!clicked.CardData.CanBeDragged || clicked.IsEquipped)
            {
                return;
            }
            // 储物箱类点击 = 取物，归堆会和它打架（参考 mod 也排除了 resource_chest；
            // 这里连本 mod 的两种箱子、原版硬币箱一起排除）
            if (IsContainerCard(clicked))
            {
                return;
            }

            int maxStack = ModConfig.GetMaxStackSize();
            Conflict conflict = clicked.Combatable == null ? null : clicked.Combatable.MyConflict;

            // 点到的牌如果在牌堆中间，先把它翻到最上面：它当前所在的叶子之后接上它原来的根
            GameCard leaf = clicked.GetLeafCard();
            if (clicked.Parent != null)
            {
                GameCard rootCard = clicked.GetRootCard();
                GameCard parent = clicked.Parent;
                parent.Child = null;
                clicked.Parent = null;
                leaf.Child = rootCard;
                rootCard.Parent = leaf;
                leaf = parent;
            }

            int length = clicked.GetChildCount() + 1;
            if (length >= maxStack)
            {
                return; // 这一叠已经满了
            }

            foreach (GameCard candidate in world.AllCards)
            {
                if (candidate == null || candidate == clicked || candidate.CardData == null)
                {
                    continue;
                }
                if (candidate.MyBoard == null || !candidate.MyBoard.IsCurrent)
                {
                    continue;
                }
                if (candidate.Parent != null || !candidate.CanBeDragged())
                {
                    continue;
                }
                if (InConflict(candidate) != (conflict != null))
                {
                    continue; // 战斗中的卡只往战斗里拉，非战斗的卡不往战斗里拉
                }
                if (!SameCard(candidate, clicked))
                {
                    continue;
                }
                if (candidate.IsEquipped)
                {
                    continue;
                }
                if (conflict == null && !leaf.CardData.CanHaveCardOnTop(candidate.CardData, false))
                {
                    continue;
                }

                Vector3 offset = clicked.transform.position - candidate.transform.position;
                offset.y = 0f;
                if (offset.sqrMagnitude > ModSettings.DoubleClickRestackRange * ModSettings.DoubleClickRestackRange)
                {
                    continue;
                }

                GameCard candidateLeaf = GetLeafIfAllSameAndSpace(candidate, ref length, maxStack);
                if (candidateLeaf == null)
                {
                    continue; // 那一叠里混了别的卡，或者加起来会超过上限
                }

                if (conflict != null)
                {
                    // 战斗中：把那一叠整个拉进同一场战斗（参考 mod 的行为）
                    var childCards = candidate.GetChildCards();
                    if (candidate.Combatable != null)
                    {
                        conflict.JoinConflict(candidate.Combatable);
                    }
                    foreach (GameCard child in childCards)
                    {
                        if (child != null && child.Combatable != null)
                        {
                            conflict.JoinConflict(child.Combatable);
                        }
                    }
                    length = 0; // 进战斗的不吃堆叠上限
                }
                else
                {
                    leaf.Child = candidate;
                    candidate.Parent = leaf;
                    leaf = candidateLeaf;
                    if (length >= maxStack)
                    {
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// 一叠卡能不能整个接过来？返回它末尾那张（接在它后面用），
        /// 只要链上有一张不是同种卡、或者加起来会超过上限，就返回 null。
        /// 顺手把这一叠的张数加进 <paramref name="length"/>。
        /// </summary>
        private static GameCard GetLeafIfAllSameAndSpace(GameCard card, ref int length, int maxStack)
        {
            int count = 1;
            while (card.Child != null)
            {
                if (!SameCard(card, card.Child) || count + 1 + length > maxStack)
                {
                    return null;
                }
                count++;
                card = card.Child;
            }
            length += count;
            return card;
        }

        /// <summary>
        /// 两张卡算不算"同种"。村民之间都算同一种（原版也是这么归堆的），
        /// 装备之间也算同一种（参考 mod 的行为）。
        /// </summary>
        private static bool SameCard(GameCard a, GameCard b)
        {
            if (a == null || b == null || a.CardData == null || b.CardData == null)
            {
                return false;
            }
            if (a.CardData.Id == b.CardData.Id)
            {
                return true;
            }
            if (a.CardData is Villager && b.CardData is Villager)
            {
                return true;
            }
            if (a.CardData is Equipable)
            {
                return b.CardData is Equipable;
            }
            return false;
        }

        /// <summary>整条父卡链都是同种卡？</summary>
        private static bool AllParentsAreSameCard(GameCard card)
        {
            while (card.Parent != null)
            {
                if (!SameCard(card, card.Parent))
                {
                    return false;
                }
                card = card.Parent;
            }
            return true;
        }

        /// <summary>整条子卡链都是同种卡？</summary>
        private static bool AllChildrenAreSameCard(GameCard card)
        {
            while (card.Child != null)
            {
                if (!SameCard(card, card.Child))
                {
                    return false;
                }
                card = card.Child;
            }
            return true;
        }

        private static bool InConflict(GameCard card)
        {
            return card != null && card.Combatable != null && card.Combatable.InConflict;
        }

        /// <summary>储物箱类：点击是"取物"而不是"选中"，归堆会和它打架。</summary>
        private static bool IsContainerCard(GameCard card)
        {
            return card.CardData is ResourceChest
                || card.CardData is Chest
                || card.CardData is MagnetChest;
        }
    }
}
