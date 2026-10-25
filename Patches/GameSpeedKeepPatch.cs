using HarmonyLib;
using UnityEngine;

namespace StacklandsProMaxNS
{
    /// <summary>
    /// 「游戏速度沿用」：**月结（换月亮 / 下一回合）和换地图之后，保持玩家原来的时间倍数**。
    ///
    /// 用户需求原话（0.6.7）：
    ///   「在城市地图中，如果在一个回合（月亮年）中将速度调快了，下一个回合中速度会自动变成 1 倍。
    ///     这个需要改成和上一回合速度一样」
    ///   「下一个-月-年沿用上一年的时间倍数，好像还有个自动暂停，这两个功能要在城市、海岛等多个地图全部生效」
    ///
    /// 所以这里**不区分地图**：城市 / 海岛 / 森林 / 主大陆 / Greed 走的是同一套 `WorldManager`，
    /// 补丁挂在原版那两处"强制打回 1 倍"的地方，全地图自然一起生效。
    ///
    /// ── 原版为什么会被打回 1 倍（反编译核对） ──────────────────────────────
    ///
    /// 两处写死的 `SpeedUp = 1f`：
    ///   ① `WorldManager.EndOfMonth(param)` 结尾 —— 月结（吃穿住行结算 + 一串"继续"弹窗）走协程，
    ///      启动协程后立刻把速度设回 1，协程结束时只把 `currentAnimationRoutine` 置空，**不会**改回来；
    ///   ② `WorldManager.GoToBoard(newBoard, ...)` 里那个过渡回调 —— 换地图时同样无条件设回 1
    ///      （读档、剧情过渡最后也走 `SetScreen<GameScreen>`，但只有这两处会把速度打回 1）。
    ///
    /// ── 做法：重置前记住速度，流程跑完再放回去 ────────────────────────────
    ///
    /// 为什么不直接在这两个方法的后置里改回来：那样速度会在**月结弹窗 / 换图过渡期间**就是 5 倍。
    /// 结算里"卡牌逐张消失/加血"的动画虽然用不缩放的 `WaitForSeconds`，但背景音乐音高
    /// （`AudioManager` 按 `SpeedUp` 变速）等东西会被带着跑，观感与原版不一致。所以：
    ///
    ///   ① 两处前缀：记下当时的 `SpeedUp`（只记 > 1 的；暂停在 0 / 已经在 1 倍都不记，那种情况下一段照原版从 1 倍开始）；
    ///   ② `WorldManager.Update` 后置（每帧）：等"月结协程 / 过场动画 / 地图过渡"全部结束、
    ///      玩家又能操作了，再把速度放回去，然后清掉记录。
    ///
    /// ── 和「自动暂停」的配合 ──────────────────────────────────────────────
    ///
    /// 本项目迁移进来的 Auto Pause 有个「切换地点后自动暂停」：换图过渡结束后会调
    /// `GameScreen.TimePause()`，把当前速度塞进 `prePauseSpeed` 再把 `SpeedUp` 设 0 —— 而那一刻
    /// `SpeedUp` 正是原版刚设的 1，所以"解除暂停后回到 1 倍"，沿用速度就白搭了。
    /// 因此这里发现自己在**暂停状态**下要还原时，不硬把 `SpeedUp` 拨回 5（那等于替玩家解除暂停），
    /// 而是把 `GameScreen.prePauseSpeed` 改成记住的速度：玩家照常按暂停键继续，恢复的就是上一回合的倍数。
    /// （`TimePause()` 解除暂停时读的正是这个字段，见 `GameScreen.TimePause`。）
    ///
    /// 离开游戏 / 读档（`IsPlaying == false`）时直接丢弃记录，免得把上一局的速度带进新局。
    /// </summary>
    [HarmonyPatch(typeof(WorldManager), "EndOfMonth")]
    internal static class GameSpeedRememberOnMonthEndPatch
    {
        [HarmonyPrefix]
        internal static void Prefix()
        {
            GameSpeedKeep.Remember();
        }
    }

    /// <summary>
    /// 换地图（`GoToBoard`）前记住速度——海岛 / 森林 / 城市 / 主大陆之间来回走都算。
    /// 原版在这个过渡回调里写死 `SpeedUp = 1f`。
    /// </summary>
    [HarmonyPatch(typeof(WorldManager), "GoToBoard")]
    internal static class GameSpeedRememberOnBoardChangePatch
    {
        [HarmonyPrefix]
        internal static void Prefix()
        {
            GameSpeedKeep.Remember();
        }
    }

    /// <summary>
    /// 每帧检查"重置速度的那段流程是否已经结束"，结束就把速度放回去（见 <see cref="GameSpeedKeep"/>）。
    /// 和本项目其它 Update 后置补丁同一套路：`WorldManager.Update` 里就调用了 `EndOfMonth`，
    /// 所以本后置在月结那一帧看到的一定是"协程刚启动、还没结束"，不会当场还原。
    /// </summary>
    [HarmonyPatch(typeof(WorldManager), "Update")]
    internal static class GameSpeedRestorePatch
    {
        [HarmonyPostfix]
        internal static void Postfix()
        {
            GameSpeedKeep.Tick();
        }
    }

    /// <summary>`WorldManager.SpeedUp` 的记录与还原（三个补丁类共用这一份状态）。</summary>
    internal static class GameSpeedKeep
    {
        /// <summary>被重置前的速度；≤1 表示"没有待还原的速度"（1 倍本来就是原版行为，0＝暂停不记）。</summary>
        private static float pendingSpeed;

        /// <summary>
        /// 月结 / 换地图开始前记下当前速度。
        ///
        /// 只记 **> 1** 的：1 倍本来就是原版行为，记了也是空操作；而"不记 1"还能顺便躲开一个时序坑——
        /// 月结流程里如果又发生换图（城市的 Wellbeing 归零直接回主大陆），那一刻 `SpeedUp` 已经是原版
        /// 强制设的 1，按旧写法会把刚记下的 5 倍冲掉。
        /// </summary>
        internal static void Remember()
        {
            WorldManager world = WorldManager.instance;
            if (world == null)
            {
                return;
            }
            float current = world.SpeedUp;
            if (current > 1f)
            {
                pendingSpeed = current;
            }
        }

        /// <summary>重置速度的那段流程结束后把速度放回去。</summary>
        internal static void Tick()
        {
            if (pendingSpeed <= 1f)
            {
                return; // 没有待还原的速度（或者记录的本来就是 1 倍）
            }
            WorldManager world = WorldManager.instance;
            if (world == null || !world.IsPlaying)
            {
                pendingSpeed = 0f; // 离开游戏 / 读档中：丢掉记录，别带进下一局
                return;
            }
            if (world.currentAnimationRoutine != null || world.currentAnimation != null)
            {
                return; // 月结弹窗 / 过场动画还没结束
            }
            if (TransitionScreen.InTransition)
            {
                return; // 地图过渡还没结束（回调里才是原版设 1 倍的那一刻）
            }

            if (world.SpeedUp >= 1f)
            {
                world.SpeedUp = pendingSpeed;
                pendingSpeed = 0f;
                return;
            }

            // 当前是暂停状态：多半是「换地点后自动暂停」刚把速度塞进 prePauseSpeed。
            // 这里不替玩家解除暂停，只把"恢复后该是几倍"改掉。
            GameScreen screen = GameScreen.instance;
            if (screen != null)
            {
                screen.prePauseSpeed = pendingSpeed;
            }
            pendingSpeed = 0f;
        }
    }
}
