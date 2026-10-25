using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace StacklandsProMaxNS
{
    /// <summary>
    /// 卡自动归堆的**搜索距离** 2 → 可调（默认 4）。
    ///
    /// 参考 mod：**Stack Further**（NBKRedSpy/StackFurther，创意工坊 2995935585）。
    /// 它的做法是给 `WorldManager.StackSend` 和 `WorldManager.TrySendToChest` 各打一个 transpiler，
    /// 把里面写死的 `ldc.r4 2` 换成自己设置项的值。我读它的 DLL 确认了补丁点（这是关于游戏本身的事实），
    /// 代码在这里自行实现，并做了一处改进：**换成调用 `ModConfig.GetStackSearchDistance()`**
    /// （和 StackLimitPatch 一个思路），于是游戏内改数值/开关**立刻生效**，不像参考 mod 要重启。
    ///
    /// 这两处 `2f` 分别是：
    ///   - `StackSend`：建筑产出的卡在自己周围找"同类且能叠上去"的堆，够近就飞过去叠上
    ///     （离得远就 `SendIt()` 直接送出棋盘）。**这就是"苹果树产出的苹果自动归堆"那条路。**
    ///   - `TrySendToChest`：自动把资源/金币送进附近有空位的箱子（原版只有 2 格，太近了）
    ///
    /// 为什么用 transpiler 而不是 Prefix 重写整个方法：这两个方法体不小、
    /// 还牵扯传送带/箱子/磁铁的既有顺序，重写容易漏行为；只换一个常量最安全。
    ///
    /// 注：原版还有第三处同款 `2f`——`WorldManager.GetBestCardInDirection`（传送带出口选目标）。
    /// 参考 mod 没有改它，本项目保持一致，也不动，避免"超出宣传范围"地改变原版手感。
    /// </summary>
    internal abstract class StackDistanceTranspiler
    {
        private static readonly MethodInfo DistanceGetter =
            AccessTools.Method(typeof(ModConfig), nameof(ModConfig.GetStackSearchDistance));

        internal static IEnumerable<CodeInstruction> Replace(IEnumerable<CodeInstruction> instructions)
        {
            bool patched = false;
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Ldc_R4
                    && instruction.operand is float value
                    && Mathf.Approximately(value, 2f))
                {
                    // ldc.r4 2  ->  call float ModConfig::GetStackSearchDistance()
                    // （两者都往栈上压一个 float，栈平衡不变）
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = DistanceGetter;
                    patched = true;
                }
                yield return instruction;
            }

            if (!patched)
            {
                Debug.LogError("[StacklandsProMax] 没找到 Stack Further 要改的距离常量 2，"
                    + "自动归堆距离补丁没有生效（可能游戏更新改了实现）。");
            }
        }
    }

    /// <summary>建筑产出 / 卡被送出时的"附近有没有同类堆"搜索距离。</summary>
    [HarmonyPatch(typeof(WorldManager), "StackSend")]
    internal static class StackSendDistancePatch
    {
        [HarmonyTranspiler]
        internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return StackDistanceTranspiler.Replace(instructions);
        }
    }

    /// <summary>自动把资源/金币送进附近箱子的搜索距离。</summary>
    [HarmonyPatch(typeof(WorldManager), "TrySendToChest")]
    internal static class TrySendToChestDistancePatch
    {
        [HarmonyTranspiler]
        internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return StackDistanceTranspiler.Replace(instructions);
        }
    }
}
