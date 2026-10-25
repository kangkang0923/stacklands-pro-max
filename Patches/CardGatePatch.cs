using HarmonyLib;

namespace StacklandsProMaxNS
{
    /// <summary>
    /// 「每张卡一个总开关」在**配方**这一侧的落地：关掉的卡，它的创意做不出东西来。
    ///
    /// 为什么不直接把创意卡从游戏里删掉：卡数据在 `Ready()` 之前就已经加载完了，
    /// 删不掉；而且玩家存档里可能还留着这张创意卡。所以做法是"让这个配方无法成立"：
    ///
    /// 合成流程是——根卡每帧调 `CardData.CheckBlueprintInStack()`（私有）→ `FindMatchingPrint()`（私有）
    /// 找到匹配的 `Subprint` → 过若干秒 `BlueprintComplete()` 消耗材料、生成产物。
    /// 这里在 `FindMatchingPrint` 上加后置补丁：**只要这个配方的产物是被关掉的卡，就把结果清成 null**，
    /// 于是计时器不会启动；而且原版发现 `FindMatchingPrint() == null` 还会主动取消已经跑起来的计时器，
    /// 两条路都堵死，产物永远做不出来。
    ///
    /// 开销：这个方法每帧会被每张根卡调用，所以先过一道静态总闸
    /// （`ModConfig.HasDisabledCards`）——一张卡都没关时几乎零开销。
    /// </summary>
    [HarmonyPatch(typeof(CardData), "FindMatchingPrint")]
    internal static class CardGatePatch
    {
        [HarmonyPostfix]
        internal static void Postfix(ref Subprint __result)
        {
            if (__result == null || !ModConfig.HasDisabledCards)
            {
                return;
            }
            // 产物被关掉了 → 这个配方不成立
            if (ModConfig.IsCardDisabled(__result.ResultCard))
            {
                __result = null;
                return;
            }
            // 创意卡本身被关掉了（总开关会把"产物 + 创意"一起禁用）→ 同样不成立
            Blueprint blueprint = __result.ParentBlueprint;
            if (blueprint != null && ModConfig.IsCardDisabled(blueprint.Id))
            {
                __result = null;
            }
        }
    }
}
