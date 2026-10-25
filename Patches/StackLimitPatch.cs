using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace StacklandsProMaxNS
{
    /// <summary>
    /// 卡牌堆叠上限 30 -> ModConfig.StackLimitSize（0.5.8 曾为 999，0.5.14 起为 100；0.5.18 起是设置界面里的
    /// 数字输入框，默认 100，填 30 ＝原版行为）。
    ///
    /// 原版唯一的硬上限在 CardData.CanHaveCardOnTop 里：
    ///     它把「根卡现有子卡数 + 1」与「要叠上去那一叠的卡数」相加，超过 30 就拒绝（预制卡按 1 张算）。
    /// 这个方法同时也是拖拽落卡（GameCard.Bounce）和自动堆叠的判定入口，
    /// 所以只改这一处常量就够了。
    ///
    /// 做法参考 mod：HigherStacks（它同样用 transpiler 替换这个 30 常量）。
    /// 本项目的区别：**换成调用 ModConfig.GetMaxStackSize()** 而不是塞死一个数字，
    /// 于是设置界面里关掉开关就立刻回到 30（不需要重新打补丁、也不用重启游戏）。
    /// </summary>
    [HarmonyPatch(typeof(CardData), "CanHaveCardOnTop")]
    internal static class StackLimitPatch
    {
        private static readonly MethodInfo MaxStackSizeGetter =
            AccessTools.Method(typeof(ModConfig), nameof(ModConfig.GetMaxStackSize));

        [HarmonyTranspiler]
        internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            bool patched = false;
            foreach (CodeInstruction instruction in instructions)
            {
                if (IsIntConstant(instruction, 30))
                {
                    // ldc.i4.s 30  ->  call int ModConfig::GetMaxStackSize()
                    // （两者都往栈上压一个 int，栈平衡不变）
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = MaxStackSizeGetter;
                    patched = true;
                }
                yield return instruction;
            }

            if (!patched)
            {
                Debug.LogError("[StacklandsProMax] 没在 CardData.CanHaveCardOnTop 里找到堆叠上限常量 30，"
                    + "堆叠上限补丁没有生效（可能游戏更新改了实现）。");
            }
        }

        private static bool IsIntConstant(CodeInstruction instruction, int value)
        {
            if (instruction.opcode == OpCodes.Ldc_I4_S || instruction.opcode == OpCodes.Ldc_I4)
            {
                return Convert.ToInt32(instruction.operand) == value;
            }
            return false;
        }
    }
}
