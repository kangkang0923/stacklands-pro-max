using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;

namespace StacklandsProMaxNS
{
    /// <summary>
    /// **Ctrl+点击快速购买卡包**（0.5.16 用户需求原话：「CTRL+左键点击最上方的卡包可以快速购买卡包，
    /// 需要用到的货币优先用场上散落的，不够会自动去对应的箱子拿」）。
    ///
    /// ── 原版的购买流程（对当前游戏 DLL 反编译核实，GameScripts.dll 2026-10-02 镜像）───────
    /// 场上那个"卡包预览 + 价格"的盒子是 `BuyBoosterBox`，原版只支持**把钱拖上去**：
    /// `CardDropped` 里对拖上来的那叠逐堆扣款（先吃散币堆 → 不够调 `BuyWithChest` /
    /// `BuyWithCreditcard` 从箱子里扣），攒满 `GetCost()` 才调私有的 `CreateBoosterPack()`
    /// 生成真正的 `Boosterpack`。盒子自己的 `Click()` 是**空的**（没有点击购买），
    /// 所以本补丁挂 `Draggable.Clicked` 的 Prefix：盒子命中 `HoveredDraggable`（它不可拖拽，
    /// 原版点击分发对不可拖拽的 Draggable 调 `Clicked()`）、按住 Ctrl 且点的是鼠标左键时，
    /// 用**原版同样的扣款原语**一键付清。
    ///
    /// ── 扣款顺序（用户口径）────────────────────────────────────────────────
    /// ① **场上散落的货币**优先：同一场地上所有散卡/散堆（排除正在拖拽、飞行中、带落点的）；
    /// ② 不够再从**对应的箱子**扣（离盒子近的先扣）：金币→硬币箱（`HeldCardId=="gold"`，
    ///    含磁力箱/硬币磁力箱）、贝壳→贝壳宝箱（含贝壳磁力箱）、纸币→信用卡（含磁力信用卡）。
    /// 纸币的凑整/找零算法对齐原版 `BuyBoosterBox.CardDropped`：先按 10/20/50/100 大面额凑，
    /// 不够再破一张面额最小的够数纸币、用 `CreateDollarsFromValue` 把找零吐在盒子旁边。
    ///
    /// ── 实现细节 ──────────────────────────────────────────────────────────
    /// - 挂 `Draggable.Clicked` 而不是 `Interactable.Click`：`BuyBoosterBox : CardTarget : Interactable : Draggable`，
    ///   而点击分发在 `HoveredDraggable` 上就短路了（`Interactable.Click` 对盒子根本不会被调到）。
    /// - `CreateBoosterPack(GameCard card = null)` 是私有的 → `AccessTools.Method` 反射调；
    ///   付清后 `StoredCostAmount = 0`（原版 `CardDropped` 的收尾）。
    /// - 只付得出一部分（极端凑不出整的纸币情况）也照原版语义把 `StoredCostAmount` 往上记，
    ///   剩下的还能用原版拖拽方式补——不会重复扣也不会丢钱。
    /// - 买不起就整个不动（原版这个 Click 本来就是空的）。
    /// - 手柄没有 Ctrl：`Draggable.Clicked` 虽然在手柄"抓取"时也会被调到，
    ///   但这里用 `InputController.GetKey(LeftCtrl/RightCtrl)` 限定（同 BoxSelectPatch 的写法），
    ///   手柄玩家的点击完全走原版行为。
    /// </summary>
    [HarmonyPatch(typeof(Draggable), "Clicked")]
    internal static class QuickBuyPackPatch
    {
        /// <summary>原版纸币凑整的面额顺序（对齐 `BuyBoosterBox.takeOrder`）。</summary>
        private static readonly int[] TakeOrder = new int[4] { 10, 20, 50, 100 };

        private static System.Reflection.MethodInfo createBoosterPack;
        private static bool loggedError;

        private static bool Prefix(Draggable __instance)
        {
            try
            {
                if (!ModConfig.QuickBuyPack || !(__instance is BuyBoosterBox box))
                {
                    return true;
                }
                if (!IsCtrlClick())
                {
                    return true;
                }
                TryQuickBuy(box);
            }
            catch (Exception ex)
            {
                if (!loggedError)
                {
                    loggedError = true;
                    Debug.LogWarning("[StacklandsProMax] 快速购买卡包异常（本次点击放行交回原版）：" + ex);
                }
            }
            return true; // 原版对盒子的 Clicked() 是空的，放行没有副作用
        }

        /// <summary>
        /// 是不是"按住 Ctrl 的鼠标左键点击"？本前缀只在原版点击分发
        /// （`GetInputBegan(0)` ＝ 鼠标左键刚开始按）里被调到，所以这里只需再确认 Ctrl；
        /// 手柄的"抓取"路径不会带 Ctrl，天然被排除（同 BoxSelectPatch 的写法）。
        /// </summary>
        private static bool IsCtrlClick()
        {
            InputController input = InputController.instance;
            if (input == null)
            {
                return false;
            }
            return input.GetKey(Key.LeftCtrl) || input.GetKey(Key.RightCtrl);
        }

        private static void TryQuickBuy(BuyBoosterBox box)
        {
            WorldManager world = WorldManager.instance;
            if (world == null || box.MyBoard == null || !box.MyBoard.IsCurrent)
            {
                return;
            }
            int cost = box.GetCurrentCost();
            if (cost <= 0)
            {
                return;
            }
            BoardCurrency currency = box.BoardCurrency;

            // ① 场上散落的货币（同一场地、非拖拽/飞行/带落点）
            List<GameCard> looseCoins = new List<GameCard>();
            List<Dollar> looseBills = new List<Dollar>();
            // ② 对应的箱子 / 信用卡（含磁力箱系——它们就是 Chest/Creditcard 的子类）
            List<Chest> chests = new List<Chest>();
            List<Creditcard> creditcards = new List<Creditcard>();
            string chestHeldId = null;
            if (currency == BoardCurrency.Gold)
            {
                chestHeldId = "gold";
            }
            else if (currency == BoardCurrency.Shell)
            {
                chestHeldId = "shell";
            }

            foreach (GameCard card in world.AllCards)
            {
                if (card == null || card.CardData == null || card.MyBoard != box.MyBoard)
                {
                    continue;
                }
                if (card.BeingDragged || card.Velocity.HasValue || card.BounceTarget != null)
                {
                    continue;
                }
                if (currency == BoardCurrency.Dollar)
                {
                    if (card.CardData is Dollar dollar)
                    {
                        looseBills.Add(dollar);
                    }
                    else if (card.CardData is Creditcard creditcard)
                    {
                        creditcards.Add(creditcard);
                    }
                }
                else
                {
                    if (card.CardData.Id == chestHeldId)
                    {
                        looseCoins.Add(card);
                    }
                    else if (card.CardData is Chest chest && chest.HeldCardId == chestHeldId)
                    {
                        chests.Add(chest);
                    }
                }
            }
            chests.Sort(new DistanceComparer(box.transform.position));
            creditcards.Sort(new CreditcardDistanceComparer(box.transform.position));

            // 付不起就什么都不动（提示都没有——和"拖了一半"的原版手感一致）
            if (CountAvailable(currency, looseCoins, looseBills, chests, creditcards) < cost)
            {
                return;
            }

            int need = cost;
            if (currency == BoardCurrency.Dollar)
            {
                if (looseBills.Count > 0)
                {
                    int total = 0;
                    foreach (Dollar bill in looseBills)
                    {
                        total += bill.DollarValue;
                    }
                    need -= TakeBills(looseBills, Mathf.Min(need, total), box.transform.position);
                }
                foreach (Creditcard creditcard in creditcards)
                {
                    if (need <= 0)
                    {
                        break;
                    }
                    int take = Mathf.Min(need, creditcard.DollarCount);
                    creditcard.DollarCount -= take;
                    need -= take;
                }
            }
            else
            {
                foreach (GameCard coin in looseCoins)
                {
                    if (need <= 0)
                    {
                        break;
                    }
                    coin.RemoveFromStack();
                    coin.DestroyCard(spawnSmoke: true);
                    need--;
                }
                foreach (Chest chest in chests)
                {
                    if (need <= 0)
                    {
                        break;
                    }
                    int take = Mathf.Min(need, chest.CoinCount);
                    chest.CoinCount -= take;
                    need -= take;
                }
            }

            int paid = cost - need;
            box.StoredCostAmount += paid;
            world.CreateSmoke(box.transform.position);
            if (box.StoredCostAmount >= box.GetCost())
            {
                if (createBoosterPack == null)
                {
                    createBoosterPack = AccessTools.Method(typeof(BuyBoosterBox), "CreateBoosterPack");
                }
                createBoosterPack?.Invoke(box, new object[1] { null });
                box.StoredCostAmount = 0;
            }
        }

        private static int CountAvailable(BoardCurrency currency, List<GameCard> looseCoins,
            List<Dollar> looseBills, List<Chest> chests, List<Creditcard> creditcards)
        {
            int total = 0;
            if (currency == BoardCurrency.Dollar)
            {
                foreach (Dollar bill in looseBills)
                {
                    total += bill.DollarValue;
                }
                foreach (Creditcard creditcard in creditcards)
                {
                    total += creditcard.DollarCount;
                }
            }
            else
            {
                total += looseCoins.Count;
                foreach (Chest chest in chests)
                {
                    total += chest.CoinCount;
                }
            }
            return total;
        }

        /// <summary>
        /// 对齐原版 `BuyBoosterBox.CardDropped` 的纸币扣款：先按 10/20/50/100 大面额凑整，
        /// 凑不出再破一张"面额最小的够数"纸币、把找零吐在旁边。返回**实际收走的金额**。
        /// </summary>
        private static int TakeBills(List<Dollar> bills, int take, Vector3 changePosition)
        {
            int unpaid = take;
            for (int i = 0; i < TakeOrder.Length; i++)
            {
                int denomination = TakeOrder[i];
                int count = unpaid / denomination;
                if (count <= 0)
                {
                    continue;
                }
                for (int j = 0; j < count; j++)
                {
                    Dollar bill = bills.Find((Dollar x) => x != null && x.DollarValue == denomination);
                    if (bill == null)
                    {
                        break;
                    }
                    bills.Remove(bill);
                    bill.MyGameCard.RemoveFromStack();
                    bill.MyGameCard.DestroyCard(spawnSmoke: true);
                    unpaid -= denomination;
                    if (unpaid <= 0)
                    {
                        break;
                    }
                }
                if (unpaid <= 0)
                {
                    break;
                }
            }
            if (unpaid > 0)
            {
                // 破一张"面额最小的够数"纸币（原版是 OrderBy(DollarValue).First()，
                // 面额可能不够数会算出负找零——这里挑够数的最小面额，找零一定非负）
                Dollar cover = null;
                foreach (Dollar bill in bills)
                {
                    if (bill != null && bill.DollarValue >= unpaid
                        && (cover == null || bill.DollarValue < cover.DollarValue))
                    {
                        cover = bill;
                    }
                }
                if (cover != null)
                {
                    int change = cover.DollarValue - unpaid;
                    bills.Remove(cover);
                    cover.MyGameCard.RemoveFromStack();
                    cover.MyGameCard.DestroyCard(spawnSmoke: true);
                    WorldManager.instance.CreateDollarsFromValue(change, changePosition);
                    unpaid = 0;
                }
            }
            return take - unpaid;
        }

        /// <summary>离目标点近的箱子/信用卡先扣。</summary>
        private class DistanceComparer : IComparer<Chest>
        {
            private Vector3 target;

            public DistanceComparer(Vector3 target)
            {
                this.target = target;
            }

            public int Compare(Chest x, Chest y)
            {
                if (x == null || x.MyGameCard == null)
                {
                    return y == null ? 0 : 1;
                }
                if (y == null || y.MyGameCard == null)
                {
                    return -1;
                }
                float dx = (x.MyGameCard.transform.position - target).sqrMagnitude;
                float dy = (y.MyGameCard.transform.position - target).sqrMagnitude;
                return dx.CompareTo(dy);
            }
        }

        /// <summary>信用卡版距离比较（泛型不能共用，单开一个）。</summary>
        private class CreditcardDistanceComparer : IComparer<Creditcard>
        {
            private Vector3 target;

            public CreditcardDistanceComparer(Vector3 target)
            {
                this.target = target;
            }

            public int Compare(Creditcard x, Creditcard y)
            {
                if (x == null || x.MyGameCard == null)
                {
                    return y == null ? 0 : 1;
                }
                if (y == null || y.MyGameCard == null)
                {
                    return -1;
                }
                float dx = (x.MyGameCard.transform.position - target).sqrMagnitude;
                float dy = (y.MyGameCard.transform.position - target).sqrMagnitude;
                return dx.CompareTo(dy);
            }
        }
    }
}
