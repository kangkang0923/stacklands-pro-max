using HarmonyLib;
using UnityEngine;

namespace StacklandsProMaxNS
{
    /// <summary>
    /// 原版资源箱（Cities DLC 储物箱）**左键点击取出的材料改成"往旁边弹到地上"**。
    ///
    /// 用户需求原话（0.5.5）：「原版的资源箱，左键点击后弹出牌，是垂直向上的，然后掉到箱子上，不方便，
    /// 磁力箱的牌是往边上掉的，你是不是改过，是的话把资源箱也改成往边上掉的，**这个要加个开关**」。
    ///
    /// ── 为什么原版会"弹起来又掉回箱子上"（不是我们改的） ──────────────────────────
    ///
    /// 原版 `ResourceChest.RemoveResources(count)` 是这个形状：
    ///
    /// 它做四件事：数量先截到 `ResourceCount`；在箱子上方稍高处造出一叠；调 `WorldManager.StackSend`
    /// 把这叠朝右送出去（← 问题在这一句）；最后把箱子数量减去。
    ///
    /// 而 `WorldManager.StackSend` 的第一句是：
    ///
    /// 三道截胡：磁铁 → 箱子 → 已有落点即返回。
    ///
    /// `TrySendToChest` 会去找**最近的、能收下这张卡的 `Chest`** —— 而刚取出来的这叠，
    /// 位置就在箱子顶上、类型又完全匹配，于是**首选目标就是它自己这个箱子**：
    /// 卡被"送"回箱子 → 玩家看到的就是"垂直向上弹一下又落回箱子上"（落上去还会被 `UpdateCard` 吞回去）。
    ///
    /// 本项目的磁力箱没有这个毛病，纯粹因为 `MagnetChest : CardData` **不是原版的 `Chest`**，
    /// `TrySendToChest` 看不上它，`StackSend` 于是走到最后那句 `SendIt()`（随机方向的小弧）→ 看起来就是"往边上掉"。
    ///
    /// ── 本项目的做法 ─────────────────────────────────────────────────────────
    ///
    /// Prefix 接替 `ResourceChest.RemoveResources`，在**开关打开**时自己实现取牌，唯一区别是
    /// **不再走 `StackSend`**（所以既不会被 `TrySendToChest` 送回自己，也不会被 `TrySendToMagnet`
    /// 半路截走），而是调用共用的 `ChestEject.EjectToPileOrSide`：**2 格内有同种牌堆就飞过去叠上**（照磁力箱取出的堆叠行为），没有才**朝远离场地中心的方向**弹出
    /// `1.2` 格、初速度用原版 `SendToPosition` 的同款公式、不设 `BounceTarget`
    /// （细节与调参旋钮都在 `Patches\ChestEject.cs`）。
    ///
    /// 这套参数与公式来自 0.5.4（那时是磁力箱在用，用户实测"往边上掉"没问题；随整体回退被删掉了）。
    ///
    /// ⚠️ 关掉设置项「资源箱取出的牌往边上掉」＝完全交回原版（Prefix 直接放行），
    /// 于是又会看到"弹起来落回箱子上"的原版行为。
    ///
    /// ⚠️ 本补丁只改**取出方向**，不动存储/容量/类型/合成等任何规则；
    /// 也不会和 Stacks n Capacity 冲突：它的"拖出"是 `RemoveResources` 的**调用方**，
    /// 拿到牌堆后立刻 `StartDragging()`（`Draggable.StartDragging` 会把 `Velocity` 清空），
    /// 所以我们设的初速度对"拖出"那条路径没有任何影响。
    /// </summary>
    [HarmonyPatch(typeof(ResourceChest), "RemoveResources")]
    internal static class ResourceChestEjectPatch
    {
        [HarmonyPrefix]
        internal static bool Prefix(ResourceChest __instance, int count, ref GameCard __result)
        {
            if (!ModConfig.ResourceChestEjectSide || __instance == null)
            {
                return true; // 开关关掉：交回原版（含它"落回箱子上"的行为）
            }

            GameCard chestCard = __instance.MyGameCard;
            if (chestCard == null || chestCard.Destroyed)
            {
                return true;
            }

            int take = Mathf.Min(count, __instance.ResourceCount);
            if (take <= 0 || string.IsNullOrEmpty(__instance.HeldCardId))
            {
                // 原版在这个情况下会对着 null 调 GetRootCard() 而抛异常，这里干脆返回 null（取不到东西）
                __result = null;
                return false;
            }

            GameCard stack = WorldManager.instance.CreateCardStack(
                chestCard.transform.position + Vector3.up * 0.2f, take, __instance.HeldCardId, false);
            if (stack == null)
            {
                __result = null;
                return false;
            }

            GameCard root = stack.GetRootCard();

            // 0.6.1：磁力族（磁力箱 / 磁力容器 / 原版磁铁）要这叠卡的话，**直接让它飞过去**。
            // 用户反馈：「场上有水容器和水磁力容器，点水容器取出卡片时，卡片直接往左边落地并消失，
            // 右边的水磁力容器资源 +1」——取出的卡本来就该被吸走，不该先被我们弹到侧面。
            // 这里主动问一次（而不是依赖生成瞬间遗留的 BounceTarget），拿到的就是"直接飞进容器"。
            if (WorldManager.instance.TrySendToMagnet(root))
            {
                __instance.ResourceCount -= take;
                __result = root;
                return false;
            }

            ChestEject.EjectToPileOrSide(root, chestCard);
            // 记住"这一叠是从这个箱子取出来的"：Stacks n Capacity 的"拖出"走的也是这个方法，
            // 它随后会把这叠挂到鼠标上；玩家只点一下（没真的拖动）就松手时，靠这条记录
            // 把它改成"往旁边弹到地上"而不是落回箱子（见 ChestDropRedirectPatch）。
            ChestTakeOut.Remember(root, chestCard);
            __instance.ResourceCount -= take;
            __result = root;
            return false;
        }
    }
}
