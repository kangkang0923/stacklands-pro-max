using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace StacklandsProMaxNS
{
    /// <summary>
    /// 点一下卡包就把它整个开完。
    ///
    /// 原版 Boosterpack.Clicked() 一次只开一张卡，并且：
    ///   - TimesOpened 记录已开张数，TotalCardsInPack 是总张数
    ///   - 开到最后一张时，原版会自己播烟并销毁卡包（在 SetHitEffect 的协程里）
    ///   - 每开一张卡包会放大 1.2 倍，Update 里再慢慢插值回原大小
    /// 所以这里不去重写开卡逻辑（那样会漏掉首包特判、Cities 事件、村民补给等规则），
    /// 而是"反复调用原版 Clicked"，效果和玩家手动连点完全相同：
    ///   - Prefix：卡包开完时不再响应（同时防止无限递归）
    ///   - Postfix：还没开完就排一个 0.05 秒后的下一次 Clicked
    ///
    /// 做法参考 mod：PACKSPLOSION!!!（同样是 Prefix + 0.05 秒协程链）。
    /// 额外加了一个"同一卡包只允许一条连锁"的去重，避免玩家连点时开卡速度翻倍。
    /// </summary>
    [HarmonyPatch(typeof(Boosterpack), "Clicked")]
    internal static class OpenWholePackPatch
    {
        private static readonly HashSet<Boosterpack> ChainingPacks = new HashSet<Boosterpack>();

        [HarmonyPrefix]
        internal static bool Prefix(Boosterpack __instance)
        {
            if (!ModConfig.OpenWholePack)
            {
                return true; // 设置界面里关掉 → 交回原版（一次只开一张）
            }
            if (__instance == null || __instance.PackData == null)
            {
                return true; // 数据异常时交回原版
            }
            // 已经开完的包不再响应点击（原版此时也会直接 return）
            return __instance.TimesOpened < __instance.TotalCardsInPack;
        }

        [HarmonyPostfix]
        internal static void Postfix(Boosterpack __instance)
        {
            if (!ModConfig.OpenWholePack)
            {
                return;
            }
            if (__instance == null || __instance.PackData == null)
            {
                return;
            }
            if (__instance.TimesOpened >= __instance.TotalCardsInPack)
            {
                ChainingPacks.Remove(__instance);
                return; // 开完了，收工（卡包随后会被原版销毁）
            }
            if (!ChainingPacks.Add(__instance))
            {
                return; // 这个包已经在连锁里了
            }
            if (WorldManager.instance == null)
            {
                ChainingPacks.Remove(__instance);
                return;
            }
            WorldManager.instance.StartCoroutine(OpenNextCardLater(__instance));
        }

        private static IEnumerator OpenNextCardLater(Boosterpack pack)
        {
            yield return new WaitForSeconds(ModSettings.PackOpenDelay);

            ChainingPacks.Remove(pack);
            // Unity 判空：卡包可能已经被销毁
            if (pack != null && pack.PackData != null && pack.TimesOpened < pack.TotalCardsInPack)
            {
                pack.Clicked();
            }
        }
    }
}
