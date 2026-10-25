using System.Reflection;
using HarmonyLib;

namespace StacklandsProMaxNS
{
    /// <summary>
    /// 火锅（炖锅）的食物容量跟随设置里的「火锅容量」输入框（0.5.8 定值 10000；0.5.18 起可调，原版 50）。
    ///
    /// 原版 `Hotpot` 有个私有的 `MaxFoodValue`，默认 **50**——火锅最多囤 50 点食物，
    /// 满了 `CookFood` 就不再吞食物（`FoodValue + childCard.SpecialValue <= MaxFoodValue` 不成立）。
    /// 这是私有实例字段，没有公开入口，所以用反射在 `UpdateCard` 的后置里改：
    /// UpdateCard 每帧每张火锅都会跑，无论游戏/其它 mod 什么时候把它改回去，
    /// 下一帧都会被重新顶成设置值。填 50 ＝原版行为。
    ///
    /// 参考：Stacks n Capacity 的做法是遍历卡牌 prefab 改 `Hotpot.MaxFoodValue`（prefab 上的值
    /// 会不会传播到已生成的实例存疑，它另打了一个 Postfix 兜底），这里直接改实例字段，语义最直接。
    /// </summary>
    [HarmonyPatch(typeof(Hotpot), "UpdateCard")]
    internal static class HotpotCapacityPatch
    {
        private static FieldInfo _maxFoodField;

        [HarmonyPostfix]
        internal static void Postfix(Hotpot __instance)
        {
            if (_maxFoodField == null)
            {
                // 私有实例字段；名字是游戏程序集里写死的，找不到就放弃（功能静默失效，不炸游戏）
                _maxFoodField = typeof(Hotpot).GetField("MaxFoodValue",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (_maxFoodField == null)
                {
                    return;
                }
            }

            if (ModConfig.HotpotCapacity > 0)
            {
                _maxFoodField.SetValue(__instance, ModConfig.HotpotCapacity);
            }
        }
    }
}
