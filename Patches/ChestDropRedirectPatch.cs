using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace StacklandsProMaxNS
{
    /// <summary>
    /// 记住"这一叠是刚从哪个箱子里取出来的"，用来在**松手那一刻**决定要不要把它弹到旁边。
    /// 由两条取牌路径登记：
    ///   - `ResourceChestEjectPatch`（原版资源箱的 `RemoveResources`，Stacks n Capacity 的拖出也走它）；
    ///   - `ChestDragOutPatch`（本项目磁力箱的 `TakeOutStack`）。
    /// 记录只在很短的时间内有效（`RememberSeconds`），并且被消费或过期就丢掉，不会无限增长。
    /// </summary>
    internal static class ChestTakeOut
    {
        /// <summary>记录有效期：从"取出"到"松手"之间的正常间隔是几帧，30 秒足够宽松又不会留垃圾。</summary>
        private const float RememberSeconds = 30f;

        private class Record
        {
            internal GameCard Root;
            internal GameCard Chest;
            internal float ExpiresAt;
            /// <summary>取出那一刻的鼠标位置（拖出时鼠标就在源箱子上）。</summary>
            internal Vector3 TakeOutMousePos;
            /// <summary>拖动过程中鼠标离开取出点的最远距离（每帧采样，见 <see cref="Tick"/>）。</summary>
            internal float MaxExcursion;
        }

        private static readonly List<Record> Records = new List<Record>();

        /// <summary>登记：这一叠是从这个箱子里取出来的（root 必须是挂在鼠标上的那张根卡）。</summary>
        internal static void Remember(GameCard root, GameCard chest)
        {
            if (root == null || chest == null)
            {
                return;
            }
            Prune();
            Vector3 mousePos = WorldManager.instance != null
                ? WorldManager.instance.mouseWorldPosition
                : Vector3.zero;
            Records.Add(new Record
            {
                Root = root,
                Chest = chest,
                ExpiresAt = Time.time + RememberSeconds,
                TakeOutMousePos = mousePos
            });
        }

        /// <summary>
        /// 每帧采样：手里正拖着的登记卡，记录鼠标离开取出点的最远距离。
        /// 松手时靠它区分「点了一下」（几乎没动）和「拖出去又拖回来」（动过）。
        /// </summary>
        internal static void Tick()
        {
            WorldManager world = WorldManager.instance;
            if (world == null || Records.Count == 0)
            {
                return;
            }
            for (int i = 0; i < Records.Count; i++)
            {
                Record record = Records[i];
                if (record.Root == null || world.DraggingDraggable != record.Root)
                {
                    continue; // 只采样"手里正拖着"的那条；没在拖的记录偏移没有意义
                }
                Vector3 delta = world.mouseWorldPosition - record.TakeOutMousePos;
                delta.y = 0f;
                float excursion = delta.magnitude;
                if (excursion > record.MaxExcursion)
                {
                    record.MaxExcursion = excursion;
                }
            }
        }

        /// <summary>
        /// 取走并返回"这张卡是从哪个箱子取出来的"以及拖动过程中的最大鼠标偏移；
        /// 不是我们登记的卡就返回 false（＝跟原版没关系）。命中后立即删除记录（一次性）。
        /// </summary>
        internal static bool TryTake(GameCard dragging, out GameCard chest, out float maxExcursion)
        {
            chest = null;
            maxExcursion = 0f;
            if (dragging == null)
            {
                return false;
            }
            Prune();
            for (int i = 0; i < Records.Count; i++)
            {
                if (Records[i].Root != dragging)
                {
                    continue;
                }
                chest = Records[i].Chest;
                maxExcursion = Records[i].MaxExcursion;
                Records.RemoveAt(i);
                return true;
            }
            return false;
        }

        private static void Prune()
        {
            for (int i = Records.Count - 1; i >= 0; i--)
            {
                Record record = Records[i];
                // Unity 的 == 对"已销毁对象"返回 true，所以这一行也顺手清掉被销毁的卡
                if (record.Root == null || record.Chest == null || Time.time > record.ExpiresAt)
                {
                    Records.RemoveAt(i);
                }
            }
        }
    }

    /// <summary>
    /// 「拖出（Ctrl / Shift + 左键）后**只是点了一下**」时，把取出来的这一叠**朝旁边弹到空地**，
    /// 而不是让它落回箱子里。
    ///
    /// 用户需求原话（0.5.6）：「资源箱和磁力箱，ctrl或shift+左键单击，目的是让箱子弹出卡牌，
    /// 但是卡牌似乎是往正上方弹出的，出来后又被吸回去了，把弹出的方向改一下应该就行了吧」。
    ///
    /// ── 为什么会"往正上方冒出来、又被吸回去"（反编译核实） ──────────────────────
    ///
    /// 拖出＝我们在鼠标**按下那一帧**把一叠牌取出来挂到鼠标上（`ChestDragOutPatch` /
    /// `ResourceChestEjectPatch`），原版随后每帧把 `TargetPosition` 设成鼠标位置。
    /// 玩家只是**点了一下**（鼠标没移动）时，松手这一帧原版 `WorldManager.DropCard()` 里：
    ///
    /// 有 `DraggingCard` 时先看 `NearbyCardTarget`：有就交给它处理，没有才走 `CheckIfCanAddOnStack`（← 问题在这一句）；
    /// 随后若有 `DraggingDraggable`，停拖并清空这个字段。
    ///
    /// `NearbyCardTarget` 只可能是 `CardTarget`（SellBox / BuyBoosterBox 那类），**箱子不是**，
    /// 所以它一定是 null → 走 `CheckIfCanAddOnStack` → 该方法找出"和这一叠**重叠**、且能把它放上去"的卡：

    /// 它遍历这一叠周围所有重叠的卡，只要某张卡的 `CardData.CanHaveCardOnTop(这一叠的类型)` 放行，
    /// 就把这一叠设成它的子卡并返回 true。
    ///
    /// 而这一叠的位置**就在箱子正上方**、类型又完全匹配 → 首选目标就是它刚出来的那个箱子 →
    /// `SetParent(箱子)` → 箱子下一帧把自己身上的这张卡吞进存储（`ResourceCount`）。
    /// 表现就是"牌在箱子上方冒一下，然后被吸回去"。
    ///
    /// ── 做法：插手 `WorldManager.DropCard`（私有方法，按名字打补丁） ─────────────
    ///
    /// 取出的那一刻把"这一叠 ← 哪个箱子"记进 <see cref="ChestTakeOut"/>；松手时若
    /// ① 被拖的正好是记住的那一叠，且 ② 它还**和源箱子重叠着**（＝原版正准备把它叠回箱子上），
    /// 就跳过原版那套，自己做原版 `DropCard` 的最后两行 + 把它弹到旁边空地上：
    ///
    /// ```csharp
    /// dragging.StopDragging();                      // 原版 DropCard 的收尾
    /// world.SetDraggingDraggableToNull();           //   （两个都是 public，行为与原版完全一致）
    /// ChestEject.EjectToSide(dragging, 源箱子);      // 改方向：朝场地外侧弹到地上
    /// ```
    ///
    /// 判据用"**是否还和源箱子重叠**"而不是"鼠标有没有动"，是为了直接对齐原版自己的判据
    /// （`CheckIfCanAddOnStack` 就是 `GetOverlappingCards()` + `CanHaveCardOnTop()`）——
    /// 已经拖开再松手的（不重叠）一律放行交回原版：玩家想放哪就放哪，我们只接手"点一下就松手"这一种。
    ///
    /// ── 0.5.17：光看"还和源箱子重叠"会误伤「拖出去又放回来」 ────────────────────
    ///
    /// 用户反馈：「Ctrl/Shift+左键拖拽出来的卡牌，还没松开的时候，如果不需要了，可以直接放回磁力箱。
    /// 目前是无法放回去的，会被直接弹开。」原因是"拖回来松手"时这叠牌**也还压在源箱子上**，
    /// 和"点一下"没法区分。而松手位置就在取出点附近（都是箱子上），位移≈0，
    /// 所以判据改成**拖动全程的最大鼠标偏移**（<see cref="ChestTakeOut.Tick"/> 每帧采样）：
    /// 动过＝主动放回 → 交回原版（`CheckIfCanAddOnStack` 把它叠回箱子、`AbsorbStackedResources` 入库）；
    /// 几乎没动＝点一下 → 维持弹出。两条原需求都保住。
    ///
    /// ⚠️ 受设置项「取出的牌往边上掉」控制（config.json 的键仍是旧名 `Resource chest ejects sideways`，
    /// 按项目约定**键名永不复用/改名**）：关掉＝完全交回原版，于是又会看到"弹起来又落回箱子上"。
    /// </summary>
    [HarmonyPatch(typeof(WorldManager), "DropCard")]
    internal static class ChestDropRedirectPatch
    {
        /// <summary>
        /// 拖动全程鼠标偏移超过这个距离（格）就视为"玩家主动拖动过"，松手不再弹出。
        /// 点一下的抖动远小于此（取出点和松手点都在箱子上），一次有意的拖出至少会把牌挪出
        /// 大半个卡位（弹射落点 1.2 格也是这个量级，见 <see cref="ChestEject.EjectDistance"/>）。
        /// </summary>
        internal const float ClickMoveThreshold = 0.4f;

        [HarmonyPrefix]
        internal static bool Prefix(WorldManager __instance)
        {
            if (!ModConfig.ResourceChestEjectSide || __instance == null)
            {
                return true; // 开关关掉：交回原版
            }

            GameCard dragging = __instance.DraggingCard;
            if (dragging == null || dragging.Destroyed)
            {
                return true;
            }

            GameCard chest;
            float maxExcursion;
            if (!ChestTakeOut.TryTake(dragging, out chest, out maxExcursion))
            {
                return true; // 不是"刚从箱子里取出来"的那一叠（普通拖拽、框选整组搬…）
            }
            if (chest == null || chest.Destroyed)
            {
                return true; // 箱子没了
            }

            // ── 0.5.17：区分「点一下」和「拖出去又放回来」 ─────────────────────────
            // 0.5.6 需求是"点一下取牌别被吸回去"（鼠标几乎没动）；但拖出去后再拖回箱子上松手，
            // 也是"还压在源箱子上"，会被误弹。松手回到箱子上时位移≈0，所以判据不能用
            // "松手时离取出点多远"，要用拖动全程的最大偏移（<see cref="ChestTakeOut.Tick"/> 逐帧采样）：
            // 动过 ＝ 玩家主动放回 → 交回原版（DropCard 的 CheckIfCanAddOnStack 把它叠回箱子，箱子照常入库）；
            // 没动过 ＝ 点一下 → 维持 0.5.6 的弹出行为。
            if (maxExcursion >= ClickMoveThreshold)
            {
                return true;
            }
            if (!OverlapsChest(dragging, chest))
            {
                return true; // 玩家已经把它拖开了：想放哪就放哪，交回原版
            }

            // 收尾对齐原版 DropCard 的最后两行（都是 public，所以行为一致），只是不做"叠回箱子上"那一步
            dragging.StopDragging();
            __instance.SetDraggingDraggableToNull();
            // 0.5.19：弹出前先找 2 格内的同种牌堆——有就飞过去叠上（连续 Ctrl/Shift 取出的多叠牌
            // 会自动归成一堆，与原版资源箱取出一致），没有才往边上弹（用户需求
            // 「Shift+左键取出的多叠卡牌之间不会自动吸附堆叠，改成可以堆叠的」）
            ChestEject.EjectToPileOrSide(dragging.GetRootCard(), chest);
            return false;
        }

        /// <summary>
        /// 这一叠现在还压在这个箱子上吗？＝把原版 `CheckIfCanAddOnStack` 的判据（重叠 + 能放上去）
        /// 只针对**源箱子**这一个是非判断。
        /// </summary>
        private static bool OverlapsChest(GameCard card, GameCard chest)
        {
            List<GameCard> overlapping = card.GetOverlappingCards();
            if (overlapping == null)
            {
                return false;
            }
            GameCard chestRoot = chest.GetRootCard();
            for (int i = 0; i < overlapping.Count; i++)
            {
                GameCard other = overlapping[i];
                if (other == null)
                {
                    continue;
                }
                // 箱子可能叠在别的卡上（或被别的卡压着），所以两边都比一次根卡
                if (other == chest || other.GetRootCard() == chest
                    || (chestRoot != null && (other == chestRoot || other.GetRootCard() == chestRoot)))
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>
    /// `WorldManager.Update` 之后逐帧采样"手里拖着的取出记录"的最大鼠标偏移
    /// （和本项目其它 Update 后置补丁同一套路）。松手那一帧原版 `DropCard` 先于本后置执行，
    /// 用的是上一帧为止采样到的偏移——对判断"有没有拖动过"毫无影响。
    /// </summary>
    [HarmonyPatch(typeof(WorldManager), "Update")]
    internal static class ChestTakeOutTickPatch
    {
        [HarmonyPostfix]
        internal static void Postfix()
        {
            ChestTakeOut.Tick();
        }
    }
}
