using System;
using HarmonyLib;
using UnityEngine;

namespace StacklandsProMaxNS
{
    /// <summary>
    /// 打败女巫之后，月底不再随机刷出奇怪传送门。
    ///
    /// 原理（参考原版实现，不是硬删传送门）：
    ///   EndOfMonthCutscenes.SpecialEvents() 里判断
    ///       - 月份大于 8 且能被 4 整除 → 候选为真；
    ///       - `CurrentBoard.BoardOptions.CanSpawnPortals` 为 false → 候选置假；
    ///       - 候选成立才刷 strange_portal / rare_portal。
    ///   也就是说 BoardOptions.CanSpawnPortals 就是官方的开关。
    ///   这里在事件执行前，如果女巫已经死了，就把这个开关关掉 —— 整个刷传送门的分支
    ///   （包括"奇怪传送门出现了"那一段剧情）都会被跳过，不会有空剧情。
    ///
    /// 判断"女巫已死"同时看两处，所以对已经打死过女巫的老存档也生效：
    ///   - 存档里的 WorldManager.instance.CurrentSave.LastPlayedRound.RunVariables.FinishedWickedWitch
    ///   - 当前这局的 WorldManager.instance.CurrentRunVariables.FinishedWickedWitch
    ///
    /// 做法参考 mod：NoWitchNoPortal（它只查了存档那一处）。
    /// </summary>
    [HarmonyPatch(typeof(EndOfMonthCutscenes), "SpecialEvents")]
    internal static class NoPortalAfterWitchPatch
    {
        [HarmonyPrefix]
        internal static void Prefix()
        {
            WorldManager world = WorldManager.instance;
            if (world == null)
            {
                return;
            }
            if (!ModConfig.NoPortalAfterWitch)
            {
                return; // 设置界面里关掉 → 恢复原版（女巫死后照样可能刷随机传送门）
            }
            if (!WickedWitchAlreadyDefeated(world))
            {
                return;
            }

            GameBoard board = world.CurrentBoard;
            if (board != null && board.BoardOptions != null && board.BoardOptions.CanSpawnPortals)
            {
                board.BoardOptions.CanSpawnPortals = false;
                Debug.Log("[StacklandsProMax] 女巫已被击败，关闭随机传送门。");
            }
        }

        private static bool WickedWitchAlreadyDefeated(WorldManager world)
        {
            try
            {
                SaveGame save = world.CurrentSave;
                if (save != null && save.LastPlayedRound != null
                    && save.LastPlayedRound.RunVariables != null
                    && save.LastPlayedRound.RunVariables.FinishedWickedWitch)
                {
                    return true;
                }
            }
            catch (Exception)
            {
                // 存档结构异常时忽略，继续看当前这局
            }

            try
            {
                RunVariables run = world.CurrentRunVariables;
                return run != null && run.FinishedWickedWitch;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
