using HarmonyLib;

namespace StacklandsProMaxNS
{
    /// <summary>
    /// 原版**食物容器**（`FoodWarehouse`，城市地图里的食物存储卡）**点击取出食物时，卡牌往旁边空地掉**。
    ///
    /// 用户需求原话（0.6.7）：「原版的资源箱和资源容器、食物容器，取出物品时，卡片的掉落位置也要改一下，
    /// 改成优先往空地掉，这个在资源箱或者磁力资源箱上，应该是已经改过的，你看一下」。
    ///
    /// ── 原版 `FoodWarehouse.RemoveFood` 的形状（反编译核对） ────────────────────
    ///
    /// 它按 `FoodValue` 拆出 `count` 张食物，**每张都造在容器自己的位置上**
    /// （`WorldManager.CreateCard(base.transform.position, ...)`），`Restack` 成一叠之后：
    ///   - `checkOutput == true`（运输输出计时器 `OutputCard` 那条路）→ `StackSendCheckTarget(...)`，往管道里送，**不动**；
    ///   - `checkOutput == false`（玩家点击 / Shift+点击）→ `WorldManager.StackSend(list[0], OutputDir)`。
    ///
    /// 而 `StackSend` 的兜底是 `SendIt()`——**随机方向**甩出去（`Random.insideUnitCircle * 4.5`），
    /// 落在哪全看运气：可能压回容器自己身上、可能压到隔壁的牌堆/建筑上（还会被自动归堆吞掉，
    /// 看着像"点了一下东西没出来"）。资源箱那条路（`ResourceChestEjectPatch`）0.5.5 起就改成了
    /// 共用弹射，食物容器一直漏掉了，所以这里补上。
    ///
    /// ── 做法：Postfix，一行不改原版逻辑 ───────────────────────────────────────
    ///
    /// 原版已经把卡造好、也 `Restack` 好了，我们只在它之后把"落点"改掉：
    /// 用和资源箱同一个 `ChestEject.EjectToPileOrSide`——**2 格内有同种牌堆就直接叠上去**，
    /// 没有才找一块**空地**弹过去（0.6.7 的落点选址，见 `Patches\ChestEject.cs`）。
    ///
    /// 三条让路规则（都在共用代码里，这里不重复实现）：
    ///   ① `BounceTarget != null`（磁铁 / 磁力箱 / 磁力容器已经在 `StackSend` 里认领了这叠，
    ///      或者是原版已经给它找了个同种牌堆）→ 一律放手，让它自己飞；
    ///   ② 运输输出（`checkOutput`）走的是管道，和"掉落位置"无关，直接跳过；
    ///   ③ 一张都没取出来（`__result == null`，容器是空的）→ 什么都不做。
    ///
    /// ⚠️ 受设置项「资源箱取出的牌往边上掉」（config.json 的键是旧名
    /// `Resource chest ejects sideways`，按项目约定**键名永不复用/改名**）控制：
    /// 关掉＝食物容器也交回原版（随机方向甩出去）。
    /// </summary>
    [HarmonyPatch(typeof(FoodWarehouse), "RemoveFood")]
    internal static class FoodWarehouseEjectPatch
    {
        [HarmonyPostfix]
        internal static void Postfix(FoodWarehouse __instance, bool checkOutput, GameCard __result)
        {
            if (!ModConfig.ResourceChestEjectSide || checkOutput)
            {
                return;
            }
            if (__instance == null || __result == null || __result.Destroyed)
            {
                return;
            }

            GameCard chestCard = __instance.MyGameCard;
            if (chestCard == null || chestCard.Destroyed)
            {
                return;
            }

            // 磁力族 / 同种牌堆已经在原版 StackSend 里认领了这张卡 → 不抢（共用代码里也有这道判断，
            // 这里只是少一次扫描）。
            if (__result.BounceTarget != null)
            {
                return;
            }

            ChestEject.EjectToPileOrSide(__result, chestCard);
        }
    }
}
