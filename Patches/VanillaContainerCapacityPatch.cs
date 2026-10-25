using HarmonyLib;
using UnityEngine;

namespace StacklandsProMaxNS
{
    /// <summary>
    /// 硬币箱 / 贝壳宝箱的存币上限跟随设置里的「硬币箱/贝壳宝箱容量」输入框
    /// （0.5.8 定值 10000；0.5.18 起可调，原版 `Chest.MaxCoinCount = 100`）。
    ///
    /// 原版硬币箱和贝壳宝箱是**同一个 `Chest` 类**（`HeldCardId` 分别为 gold / shell），
    /// 所以一个补丁覆盖两张卡。挂在 `Chest.UpdateCard` 的后置逐帧兜底（字段在**卡牌生成时**从
    /// prefab 复制，逐帧设才能覆盖已有实例）。
    ///
    /// ⚠️ 必须排除本 mod 的磁力箱子类：`CoinMagnetChest : Chest`（贝壳磁力箱又继承它），
    /// 它们的上限由自己的 UpdateCard 按「硬币/贝壳磁力箱容量」设置（见 Mod.cs），这里不跳过的话会互相覆盖。
    /// </summary>
    [HarmonyPatch(typeof(Chest), "UpdateCard")]
    internal static class ChestCapacityPatch
    {
        [HarmonyPostfix]
        internal static void Postfix(Chest __instance)
        {
            if (__instance is CoinMagnetChest)
            {
                return; // 磁力箱系列有自己的容量输入框，不钳
            }
            __instance.MaxCoinCount = ModConfig.CoinShellChestCapacity;
        }
    }

    /// <summary>
    /// 信用卡的存钞上限跟随设置里的「信用卡容量」输入框
    /// （0.5.8 定值 100000；0.5.18 起可调，原版 `Creditcard.MaxDollarCount = 1000`）。
    ///
    /// 挂在 `Creditcard.UpdateCard` 的后置逐帧兜底。
    /// ⚠️ 必须排除本 mod 的 `MagnetCreditcard : Creditcard`（它的上限由自己的 UpdateCard 按
    /// 「磁力信用卡容量」设置，见 Mod.cs）。
    /// </summary>
    [HarmonyPatch(typeof(Creditcard), "UpdateCard")]
    internal static class CreditcardCapacityPatch
    {
        [HarmonyPostfix]
        internal static void Postfix(Creditcard __instance)
        {
            if (__instance is MagnetCreditcard)
            {
                return; // 磁力信用卡有自己的容量输入框，不钳
            }
            __instance.MaxDollarCount = ModConfig.CreditcardCapacity;
        }
    }
}
