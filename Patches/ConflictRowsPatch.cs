using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace StacklandsProMaxNS
{
    /// <summary>
    /// 战斗框**多行排列**（迁移自创意工坊 **Lopi's Conflict Rows** `3100344684`，
    /// id `ConflictRows` v1.0.0，作者 lopi——就是 Hit Text To Center 的作者）。
    ///
    /// 原 mod 描述原话："Allows multiple rows in combat boxes"。
    /// 原版一场冲突里，同一队的卡只排**一行**（横向越挤越长）；这个 mod 把它改成
    /// **每行最多 N 张（默认 7，可在设置里改）**，超出就**往后方再起一行**，
    /// 同时把冲突框（bounds）和尺寸算大，让框能包住多行。
    ///
    /// ── 原 mod 的做法（反编译核实，三个 Prefix 全部接管） ──────────────────────
    ///
    /// | 补丁点 | 作用 |
    /// | --- | --- |
    /// | `Conflict.GetPositionInConflict(Combatable)` | 每张战斗卡的位置：行内横向居中（最后一行不满就按实际张数居中），行号往 Z 方向偏移；敌方与我方分别朝两边排 |
    /// | `Conflict.GetConflictSize()`（**私有**） | 冲突框尺寸：宽度＝两队中较宽那行，高度＝卡高 + 每多一行加一个 `HorizonalCombatOffset` |
    /// | `Conflict.GetBounds()` | 冲突框：起始位置按两队行数差横向补偿，尺寸用上面那个 |
    ///
    /// ⚠️ 原 mod 的 dll 带 `[assembly: IgnoresAccessChecksTo("GameScripts")]`（它绕过了可见性检查），
    /// 因为 `Conflict.ClampStartPosition(Vector3)` 和 `Conflict.GetConflictSize()` 在本版本里是**私有**的。
    /// 本项目不能绕检查，所以这两个私有方法用 `AccessTools` + 反射委托（`MethodDelegate`）拿：
    /// 反射建不出来（游戏版本改名/改私有）就**整体放行交回原版**（战斗退回单行，不会出错）。
    ///
    /// 另外：我们给它加了一个总开关「战斗多行排列」（原 mod 没有开关），外加原 mod 就有的
    /// 「每行战斗单位上限」数值（默认 7）。关掉开关＝完全回原版单行排列。
    /// </summary>
    internal static class ConflictRows
    {
        /// <summary>每行最多几张战斗卡（原 mod 默认 7）。</summary>
        internal static int RowSize()
        {
            int size = Mathf.RoundToInt(ModConfig.ConflictRowSize);
            return size < 1 ? 1 : size;
        }

        private static Func<Conflict, Vector3, Vector3> clampStartPosition;
        private static Func<Conflict, Vector3> conflictSize;
        private static bool reflectionFailed;

        /// <summary>私有方法的反射委托（只建一次）。建不出来就返回 false，调用方放行给原版。</summary>
        internal static bool EnsureReflection()
        {
            if (clampStartPosition != null && conflictSize != null)
            {
                return true;
            }
            if (reflectionFailed)
            {
                return false;
            }
            try
            {
                MethodInfo clamp = AccessTools.Method(typeof(Conflict), "ClampStartPosition");
                MethodInfo size = AccessTools.Method(typeof(Conflict), "GetConflictSize");
                if (clamp == null || size == null)
                {
                    reflectionFailed = true;
                    Debug.LogWarning("[StacklandsProMax] 战斗多行排列：找不到 Conflict.ClampStartPosition / "
                        + "GetConflictSize（游戏版本可能变了），本功能已自动停用");
                    return false;
                }
                clampStartPosition = AccessTools.MethodDelegate<Func<Conflict, Vector3, Vector3>>(clamp);
                conflictSize = AccessTools.MethodDelegate<Func<Conflict, Vector3>>(size);
                return clampStartPosition != null && conflictSize != null;
            }
            catch (Exception ex)
            {
                reflectionFailed = true;
                Debug.LogWarning("[StacklandsProMax] 战斗多行排列：反射私有方法失败，本功能已自动停用: " + ex.Message);
                return false;
            }
        }

        internal static Vector3 ClampStart(Conflict conflict, Vector3 position)
        {
            return clampStartPosition(conflict, position);
        }

        internal static Vector3 Size(Conflict conflict)
        {
            return conflictSize(conflict);
        }
    }

    /// <summary>
    /// ① 战斗多行：每张战斗卡的位置（`Conflict.GetPositionInConflict` 前置接管）。
    /// </summary>
    [HarmonyPatch(typeof(Conflict), "GetPositionInConflict")]
    internal static class ConflictRowsPositionPatch
    {
        [HarmonyPrefix]
        internal static bool Prefix(Combatable b, Conflict __instance, ref Vector3 __result)
        {
            if (!ModConfig.MultiRowCombat || __instance == null || b == null || !ConflictRows.EnsureReflection())
            {
                return true; // 关掉 / 拿不到私有方法：交回原版单行排列
            }

            WorldManager world = WorldManager.instance;
            if (world == null)
            {
                return true;
            }

            int max = ConflictRows.RowSize();
            float teamSize = __instance.GetTeamSize(b.Team);
            int indexInTeam = __instance.GetIndexInTeam(b);
            if (indexInTeam < 0)
            {
                return true; // 这张卡不在这场冲突里（理论上不会发生），别乱算
            }

            int row = (int)Mathf.Floor(indexInTeam / (float)max);
            // 最后一行如果不满，整行要按实际张数重新居中（原 mod 的算法）
            bool lastPartialRow = teamSize - indexInTeam <= teamSize % max;
            float x = (indexInTeam % max)
                - (lastPartialRow ? (teamSize - 1f) % max : max - 1) * 0.5f;
            Vector3 horizontal = new Vector3(x * world.HorizonalCombatOffset, 0f, 0f);
            Vector3 start = ConflictRows.ClampStart(__instance, __instance.ConflictStartPosition);

            if (__instance.Initiator.Team != b.Team)
            {
                // 敌方：起手位置 + 行内偏移，行号往 +Z 叠
                __result = start + horizontal + row * new Vector3(0f, 0f, world.HorizonalCombatOffset);
            }
            else
            {
                // 我方：再往前（-CombatOffset），行号往 -Z 叠（两边对着排）
                __result = start + horizontal + new Vector3(0f, 0f, -world.CombatOffset)
                    - row * new Vector3(0f, 0f, world.HorizonalCombatOffset);
            }
            return false;
        }
    }

    /// <summary>
    /// ② 战斗多行：冲突框尺寸（`Conflict.GetConflictSize` 是私有方法，按名字打补丁）。
    /// 宽度取"两队里较宽那行"，高度＝卡高 + 每多一行加一个 `HorizonalCombatOffset`。
    /// </summary>
    [HarmonyPatch(typeof(Conflict), "GetConflictSize")]
    internal static class ConflictRowsSizePatch
    {
        [HarmonyPrefix]
        internal static bool Prefix(Conflict __instance, ref Vector3 __result)
        {
            if (!ModConfig.MultiRowCombat || __instance == null)
            {
                return true;
            }
            WorldManager world = WorldManager.instance;
            if (world == null)
            {
                return true;
            }

            int max = ConflictRows.RowSize();
            int players = __instance.GetTeamSize(Team.Player);
            int enemies = __instance.GetTeamSize(Team.Enemy);

            float width = Mathf.Max(Mathf.Min(players, max), Mathf.Min(enemies, max)) * world.HorizonalCombatOffset;
            float height = __instance.Initiator.MyGameCard.GetHeight()
                + world.HorizonalCombatOffset * (Mathf.Floor((players - 1) / (float)max)
                    + Mathf.Floor((enemies - 1) / (float)max));
            float depth = height + world.CombatOffset + world.ConflictHeightIncrease;

            __result = new Vector3(width + world.ConflictWidthIncrease, 0.05f, depth);
            return false;
        }
    }

    /// <summary>
    /// ③ 战斗多行：冲突框本体（`Conflict.GetBounds` 前置接管）。
    /// 起始位置按"两队行数差"横向补偿（哪边行多就往另一边挪半个），尺寸用 ② 的结果。
    /// </summary>
    [HarmonyPatch(typeof(Conflict), "GetBounds")]
    internal static class ConflictRowsBoundsPatch
    {
        [HarmonyPrefix]
        internal static bool Prefix(Conflict __instance, ref Bounds __result)
        {
            if (!ModConfig.MultiRowCombat || __instance == null || !ConflictRows.EnsureReflection())
            {
                return true;
            }
            WorldManager world = WorldManager.instance;
            if (world == null)
            {
                return true;
            }

            int max = ConflictRows.RowSize();
            float combatOffset = world.CombatOffset;
            // 两队"行数差"：我方行数 - 敌方行数
            float rowDifference = Mathf.Floor((__instance.GetTeamSize(Team.Player) - 1) / (float)max)
                - Mathf.Floor((__instance.GetTeamSize(Team.Enemy) - 1) / (float)max);
            // 先手方是玩家就取负（往 -X 补偿），敌方先手取正
            float shift = (__instance.Initiator.Team == Team.Player ? -1f : 1f)
                * world.HorizonalCombatOffset * rowDifference;

            Vector3 start = ConflictRows.ClampStart(__instance, __instance.ConflictStartPosition);
            __result = new Bounds(start - new Vector3(0f, 0f, combatOffset - shift) * 0.5f,
                ConflictRows.Size(__instance));
            return false;
        }
    }
}
