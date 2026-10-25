using HarmonyLib;
using UnityEngine;

namespace StacklandsProMaxNS
{
    /// <summary>
    /// 资源箱的存储上限跟随设置里的「资源箱容量」输入框（0.5.8 定值 10000；0.5.18 起可调，原版 100）。
    ///
    /// `ResourceChest.MaxResourceCount` 是公开字段（原版写死 100），但只在**卡牌生成时**从 prefab 复制，
    /// 所以挂在 `ResourceChest.UpdateCard` 的后置里对**每个实例**逐帧兜底：游戏/其它 mod 什么时候改回去，
    /// 下一帧都会被顶回设置值。填 100 ＝原版行为。
    /// </summary>
    [HarmonyPatch(typeof(ResourceChest), "UpdateCard")]
    internal static class ResourceChestMaxCapacityPatch
    {
        [HarmonyPostfix]
        internal static void Postfix(ResourceChest __instance)
        {
            __instance.MaxResourceCount = ModConfig.ResourceChestCapacity;
        }
    }

    /// <summary>
    /// 资源箱能收的卡牌类型扩到**和磁力箱一样的白名单**（原版只收 CardType.Resources 且排除金币/贝壳）：
    /// **材料 + 食物 + 装备**，仍然排除货币（金币/贝壳/纸币）和便便，"一箱一种"（`HeldCardId`）规则照旧。
    ///
    /// 实现用 **Postfix 全量重算** `__result` 而不是 Prefix 接管，原因有两个：
    /// ① 已装的 Stacks n Capacity 也对 `ResourceChest.CanHaveCard` 打了 Prefix 接管——HarmonyX 里
    ///    **所有 Prefix 都会执行**（return false 拦不住别人，见功能说明 11.4），Prefix 的结果取决于
    ///    谁后写入，顺序不可控；Postfix 一定在所有 Prefix 之后跑，我们算出来的结果就是最终结果；
    /// ② 开关关掉时直接不动 `__result`，原版/其它 mod 的行为原样保留。
    /// </summary>
    [HarmonyPatch(typeof(ResourceChest), "CanHaveCard")]
    internal static class ResourceChestMoreTypesPatch
    {
        [HarmonyPostfix]
        internal static void Postfix(ResourceChest __instance, CardData otherCard, ref bool __result)
        {
            if (!ModConfig.ResourceChestMoreTypes)
            {
                return;
            }
            // ── 以下白名单抄自 MagnetChest.IsStorable（0.5.8 时的版本）──
            // ⚠️ 0.5.15 起 IsStorable 放行了便便（用户要求磁力箱收便便），这里**没有**跟着放：
            // 本补丁管的是原版资源箱，原版 Cities 场地本来就排除便便/铀，维持 0.5.8 以来"资源箱不收便便"的现状，
            // 两个白名单自此有意分叉。
            if (otherCard == null || otherCard is MagnetChest)
            {
                __result = false;
                return;
            }
            if (otherCard.MyCardType != CardType.Resources
                && otherCard.MyCardType != CardType.Food
                && otherCard.MyCardType != CardType.Equipable)
            {
                __result = false;
                return;
            }
            if (otherCard.Id == "gold" || otherCard.Id == "shell" || otherCard.Id == "poop")
            {
                __result = false; // 货币和便便交给原版箱子/别的容器
                return;
            }
            if (otherCard is Dollar)
            {
                __result = false; // 纸币同样是货币
                return;
            }
            // "一箱一种"：箱里已存的类型决定还能收什么（空箱收什么就定什么）
            __result = string.IsNullOrEmpty(__instance.HeldCardId) || otherCard.Id == __instance.HeldCardId;
        }
    }
}
