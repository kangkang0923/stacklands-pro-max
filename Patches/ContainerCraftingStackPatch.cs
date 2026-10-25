using HarmonyLib;

namespace StacklandsProMaxNS
{
    /// <summary>
    /// 磁力容器的配方堆（5 张料）需要两条原版不放行的叠法，这里各补一条。
    ///
    /// ── 规则 A：资源容器叠到材料上（0.6.0）────────────────────────────────────
    ///   材料卡是 `Resource`，它的 `CanHaveCard` 只认 同类 / `Resources` / `Humans` / `Food` / `Equipable`；
    ///   `Structures`（枚举值 0）只有在 `IsBuilding == false` 时才混得过去。
    ///   城市的资源容器 `storage_container` 是 `ResourceChest`（`Structures` 且是建筑），
    ///   所以「把它叠到铁条/铜条/科学上」会被 `CanHaveCardOnTop` 拒绝。
    ///
    /// ── 规则 B：受教育工人叠到资源容器上（0.6.1）─────────────────────────────
    ///   用户的搭法就是"材料摆好 → 扣上资源容器 → 再把工人叠上去"，而 `ResourceChest.CanHaveCard`
    ///   只收 `Resources`（和城市里 `FoodValue ≤ 0` 的食物），`Humans` 一律拒 → 工人叠不上去。
    ///   这里放行 `educated_worker` / `genius` / `robot_*`（＝原版 `any_educated_worker` 解析出的那 4 个 id）。
    ///
    ///   ⚠️ **只在资源容器"不是堆底"时放行**。原版 `ResourceChest.UpdateCard` 有一道
    ///      `if (!MyGameCard.HasParent || MyGameCard.Parent.CardData is HeavyFoundation)` 才吞子卡的判定：
    ///      容器一旦是根卡，就会把叠在上面的卡（包括这张工人）当成材料 `DestroyCard` 掉。
    ///      要求"容器有父卡"＝走进原版"不吞"的那一半分支 → 工人安全。
    ///      （万一玩家把下面的材料抽走让容器变成堆底，另有 <see cref="ContainerWorkerAbsorbGuardPatch"/> 兜底。）
    ///
    /// ── 规则 C/D：磁力信用卡的配方堆（0.6.3）─────────────────────────────────
    ///   磁力信用卡的配方（`iron_bar + copper_bar + science + creditcard + any_educated_worker`）
    ///   用的是**原版信用卡**（`creditcard`），它和资源容器一样是 `Structures` 建筑卡，
    ///   `Creditcard.CanHaveCard` 只认 `Dollar` 和同类 → 铁条/铜条/科学/工人都叠不上去。
    ///   这里把信用卡当"第二种容器"补同样两条：
    ///     C) 信用卡叠到 3 张材料上（材料 → 信用卡 → 工人），
    ///     D) 受教育工人叠到信用卡上；另外**材料叠到信用卡上**也放行，
    ///        这样"信用卡垫底 → 材料 → 工人"这种摆法同样能搭（信用卡只会吞纸币，绝不会吞人或材料）。
    ///
    /// ⚠️ 必须保留"堆叠上限"这一道：`CanHaveCardOnTop` 里"超过上限"和"不接受这张卡"用的是同一个
    ///    `return false`，所以这里要按同样的口径再算一次总数，否则这两个补丁会顺带把堆叠上限捅穿。
    /// </summary>
    [HarmonyPatch(typeof(CardData), "CanHaveCardOnTop")]
    internal static class ContainerCraftingStackPatch
    {
        internal const string ContainerId = "storage_container";

        /// <summary>磁力信用卡配方里的那张卡（原版信用卡，不是 mod 的磁力信用卡）。</summary>
        internal const string CreditcardId = "creditcard";

        [HarmonyPostfix]
        internal static void Postfix(CardData __instance, CardData otherCard, ref bool __result)
        {
            if (__result || __instance == null || otherCard == null)
            {
                return;
            }
            if (!IsAllowedPair(__instance, otherCard))
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

            // 和原版同一个口径：父卡那一整叠 + 子卡那一整叠，加起来不能超过堆叠上限
            int total = parentCard.GetRootCard().GetChildCount() + 1
                + childCard.GetRootCard().GetChildCount() + 1;
            if (total > ModConfig.GetMaxStackSize())
            {
                return;
            }

            __result = true;
        }

        /// <summary>哪一对「父卡 ← 子卡」需要放行。</summary>
        private static bool IsAllowedPair(CardData parent, CardData child)
        {
            // 规则 A：材料上面叠资源容器
            if (child.Id == ContainerId && IsRecipeMaterial(parent.Id))
            {
                return true;
            }

            // 规则 C：信用卡压到材料 / 受教育工人上（材料 → 工人 → 信用卡 这种摆法也能搭）
            if (child.Id == CreditcardId && (IsRecipeMaterial(parent.Id) || IsEducatedWorker(parent.Id)))
            {
                return true;
            }

            // 规则 D：材料 / 受教育工人压回信用卡上（信用卡垫底的摆法；信用卡只吞纸币，吞不了人）
            if (parent.Id == CreditcardId && (IsRecipeMaterial(child.Id) || IsEducatedWorker(child.Id)))
            {
                return true;
            }

            // 规则 B：资源容器上面叠受教育工人（只在这个容器不是堆底时放行，否则它会把工人吞掉）
            if (parent.Id == ContainerId && IsEducatedWorker(child.Id))
            {
                GameCard containerCard = parent.MyGameCard;
                if (containerCard == null || !containerCard.HasParent)
                {
                    return false; // 容器是根卡：不放行，交给 ContainerWorkerAbsorbGuardPatch 之外的默认"叠不上去"
                }
                if (containerCard.Parent != null && containerCard.Parent.CardData is HeavyFoundation)
                {
                    return false; // 重型地基上的容器同样会吞子卡
                }
                return true;
            }

            return false;
        }

        /// <summary>配方里能被资源容器压住的那 3 张材料（容器自己不算）。</summary>
        private static bool IsRecipeMaterial(string cardId)
        {
            return cardId == "iron_bar" || cardId == "copper_bar" || cardId == "science";
        }

        /// <summary>原版 `any_educated_worker` 解析出来的那 4 个 id（受教育工人 / 天才 / 教育型机器人）。</summary>
        internal static bool IsEducatedWorker(string cardId)
        {
            return cardId == "educated_worker" || cardId == "genius"
                || cardId == "robot_worker" || cardId == "robot_genius";
        }
    }

    /// <summary>
    /// 兜底：**绝不让资源容器把叠在上面的受教育工人当成材料吞掉**。
    ///
    /// 触发场景（0.6.1 用户实测暴露的那条路的延伸）：工人已经叠在容器上、配方正在读秒，
    /// 玩家却把容器下面的材料抽走 → 容器变成堆底 → 原版 `ResourceChest.UpdateCard` 的吞卡循环
    /// 会把上面这张工人当成"第一种资源"计进容器并 `DestroyCard`（工人直接消失，且不报错）。
    ///
    /// 做法：Prefix 在"容器现在是堆底（或站在重型地基上）**且**上面确实压着受教育工人"时
    /// 直接跳过这一帧的原版更新。容器本身就是空的、没人能塞东西进去（叠放已在
    /// <see cref="ContainerCraftingStackPatch"/> 里挡住），所以跳过它的每帧逻辑没有任何副作用；
    /// 工人一被挪走，更新立刻恢复正常。
    /// </summary>
    [HarmonyPatch(typeof(ResourceChest), "UpdateCard")]
    internal static class ContainerWorkerAbsorbGuardPatch
    {
        [HarmonyPrefix]
        internal static bool Prefix(ResourceChest __instance)
        {
            GameCard self = __instance != null ? __instance.MyGameCard : null;
            if (self == null || self.Destroyed)
            {
                return true;
            }

            // 原版吞子卡的判定条件：没有父卡，或者父卡是重型地基
            bool wouldAbsorbChildren = !self.HasParent
                || (self.Parent != null && self.Parent.CardData is HeavyFoundation);
            if (!wouldAbsorbChildren)
            {
                return true;
            }

            return !HasEducatedWorkerChild(self);
        }

        private static bool HasEducatedWorkerChild(GameCard container)
        {
            if (!container.HasChild)
            {
                return false;
            }
            foreach (GameCard child in container.GetChildCards())
            {
                if (child?.CardData != null && ContainerCraftingStackPatch.IsEducatedWorker(child.CardData.Id))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
