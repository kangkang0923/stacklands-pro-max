using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;

namespace StacklandsProMaxNS
{
    /// <summary>
    /// 把**伤害数字和治疗数字都显示在卡牌中间**（看清当前血量）。
    ///
    /// 合并自 mod **Hit Text To Center**（创意工坊 `3016077439`，id `HitTextToCenter` v1.1.0）：
    /// 它的补丁点是 `HitText.SetPosition` 的后置——原版这个方法会把数字放在
    /// `myGameCard.HitTextPosition`（一个**带偏移的锚点**，所以数字压在卡片下沿、挡住血量），
    /// 它改成了 `myGameCard.transform.position`（卡片正中），并顺带把"非常有效"的角标抬到卡片上方 0.11；
    /// 另外它**放过了 miss**（`IsMiss`，原版让 miss 文字面向摄像机，不该被居中）。
    ///
    /// 用户要求：**它只改了攻击的显示，把治疗的显示也改成展示在卡牌中间**。
    /// 治疗文字走的是另一条创建路径——`Combatable.CreateHitText("2", PrefabManager.instance.HealHitText)`，
    /// 用在"战斗治疗/HealLowest/吸血回复"以及被动回血上，所以需要单独识别。
    ///
    /// 怎么识别治疗文字：`HitText` 实例上**没有任何字段说明它是什么类型**，
    /// 所以我们自己在创建那一刻按预制体引用打标记（`Combatable.CreateHitText(txt, prefab)` 的后置）：
    ///   `prefab == PrefabManager.instance.HealHitText` → 记进弱表。
    /// 然后 `SetPosition` 的后置里：非 miss 时，只要"是治疗文字 **或** 中心化开关开着"，就把位置设到卡片中心。
    /// （默认两者都满足，表现＝伤害与治疗都居中；关掉开关只剩治疗居中。）
    ///
    /// 开关：设置界面「数字显示在卡牌中间」（`Hit text to center`），关掉后伤害回到原版偏移位置。
    /// 临时查看当前状态：**按 H**（每次进游戏重置，只写日志，不改变任何行为）。
    /// </summary>
    internal static class HitTextCenter
    {
        /// <summary>哪些 HitText 是治疗文字（弱键表，实例销毁后自动清掉，不用手动维护）。</summary>
        private static readonly ConditionalWeakTable<HitText, object> HealTexts =
            new ConditionalWeakTable<HitText, object>();

        private static readonly object Marker = new object();

        /// <summary>创建时打标记：这个 HitText 是不是用治疗预制体造出来的。</summary>
        internal static void TagIfHeal(HitText hitText, HitText prefab)
        {
            if (hitText == null || prefab == null)
            {
                return;
            }
            PrefabManager prefabs = PrefabManager.instance;
            if (prefabs == null || prefab != prefabs.HealHitText)
            {
                return;
            }
            RemoveTag(hitText);
            HealTexts.Add(hitText, Marker);
        }

        internal static bool IsHeal(HitText hitText)
        {
            if (hitText == null)
            {
                return false;
            }
            object unused;
            return HealTexts.TryGetValue(hitText, out unused);
        }

        private static void RemoveTag(HitText hitText)
        {
            object existing;
            if (HealTexts.TryGetValue(hitText, out existing))
            {
                HealTexts.Remove(hitText);
            }
        }

        /// <summary>原版 SetPosition 之后：把数字挪到卡片正中。</summary>
        internal static void ApplyCenter(HitText hitText)
        {
            if (hitText == null || hitText.IsMiss)
            {
                return; // 和参考 mod 一样放过 miss（它要面向摄像机飞出去）
            }
            bool isHeal = IsHeal(hitText);
            if (!isHeal && !ModConfig.HitTextCenter)
            {
                return; // 关掉开关：伤害数字回到原版位置
            }
            if (hitText.TargetCombatable == null)
            {
                return;
            }
            GameCard card = hitText.TargetCombatable.MyGameCard;
            if (card == null)
            {
                return;
            }
            hitText.transform.position = card.transform.position;
            if (hitText.VeryEffectiveText != null)
            {
                // 与参考 mod 一致：把"非常有效"角标抬到卡片上方一点
                hitText.VeryEffectiveText.transform.position = card.transform.position + new Vector3(0f, 0f, 0.11f);
            }
        }
    }

    /// <summary>创建治疗文字时给它打标记（`Combatable.CreateHitText(txt, prefab)` 是 public，直接补）。</summary>
    [HarmonyPatch(typeof(Combatable), "CreateHitText")]
    internal static class HitTextTagPatch
    {
        [HarmonyPostfix]
        internal static void Postfix(HitText __result, HitText prefab)
        {
            HitTextCenter.TagIfHeal(__result, prefab);
        }
    }

    /// <summary>原版放好位置之后，把该居中的数字挪到卡片正中（`SetPosition` 是私有方法，用字符串形式补）。</summary>
    [HarmonyPatch(typeof(HitText), "SetPosition")]
    internal static class HitTextSetPositionPatch
    {
        [HarmonyPostfix]
        internal static void Postfix(HitText __instance)
        {
            HitTextCenter.ApplyCenter(__instance);
        }
    }
}
