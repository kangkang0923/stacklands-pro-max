using System;
using HarmonyLib;
using UnityEngine;

namespace StacklandsProMaxNS
{
    /// <summary>
    /// "自动暂停"的共用逻辑（迁移自创意工坊 **Auto Pause** `3017796084`，
    /// id `AutoPauseOnLoad` v1.1.1，作者 NBKRedSpy，有公开源码
    /// github.com/NBKRedSpy/Stacklands-AutoPauseOnLoad）。
    ///
    /// 原 mod 的两个行为（它的描述原话："Automatically pauses the game when a save is loaded
    /// and also when the player changes locations."）：
    ///   1. **读档后暂停**：它 Patch `MainMenu.Awake`，给 `ContinueButton.Clicked` 挂一个回调，
    ///      点了就调 `GameScreen.TimePause()`；
    ///   2. **换地点后暂停**（可用它唯一的设置项 `Pause On Board Change` 关掉）：它 Patch
    ///      `TransitionScreen.StartTransition(Action, TransitionType, float)`，把回调包一层，
    ///      原回调跑完后再暂停。
    ///
    /// ── 为什么第 1 条我们**不能对齐**（0.5.6 实测时读通的时序） ────────────────
    ///
    /// 原 mod 在"点击 Continue 那一帧"就调 `TimePause()`，但那一刻
    /// `GameScreen.instance` **很可能是 null**：`GameScreen` 是 `GameCanvas` 的子对象、
    /// 屏幕是按需激活的（`GameCanvas.Awake` 里 `GetComponentsInChildren<SokScreen>(true)`
    /// 只是把引用收全，未激活的屏幕不会跑 `Awake`，静态 `instance` 也就没被赋值）——
    /// 而 `TimePause()` 是 `GameScreen` 上的方法。所以对齐会出现"有时候暂停、有时候不暂停"。
    ///
    /// 我们的做法：**点 Continue 只记一个"下次进入游戏就暂停"的请求**，真正的暂停放在
    /// `WorldManager.Play()` 的后置里做（`Play()` 内部第一行就是
    /// `GameCanvas.instance.SetScreen<GameScreen>()`，所以那时 `GameScreen.instance` 一定有效）。
    /// 顺带好处：新开一局不会误暂停（新局不走 Continue），和原 mod 的语义一致。
    ///
    /// ⚠️ `GameScreen.TimePause()` 是**开关式**的（`SpeedUp >= 1` 就暂停、否则恢复），
    /// 所以调用前一定先判断 `WorldManager.instance.SpeedUp != 0f`（原 mod 的 `PauseGame()` 就是这么写的），
    /// 否则同一次读档里两条路径各调一次就会"暂停又恢复"。
    /// </summary>
    internal static class AutoPause
    {
        /// <summary>"点了 Continue，等真正进入游戏后暂停一次"的请求。</summary>
        private static bool pauseOnNextPlay;

        /// <summary>请求的时间戳（`Time.unscaledTime`）：超过 <see cref="RequestLifetime"/> 就作废，免得挂着一个旧请求。</summary>
        private static float pauseRequestAt;

        /// <summary>请求的有效期（秒）：读档过渡一般 1 秒内完成，给足余量又不会长期挂着。</summary>
        private const float RequestLifetime = 60f;

        /// <summary>点了"继续游戏"（或走了等价路径）时调用：只记请求，不马上暂停。</summary>
        internal static void RequestPauseOnLoad()
        {
            if (!ModConfig.AutoPauseLoad)
            {
                return;
            }
            pauseOnNextPlay = true;
            pauseRequestAt = Time.unscaledTime;
        }

        /// <summary>真正开始游戏时调用（`WorldManager.Play()` 后置）。</summary>
        internal static void OnGameStarted()
        {
            if (!pauseOnNextPlay)
            {
                return;
            }
            pauseOnNextPlay = false;
            if (!ModConfig.AutoPauseLoad || Time.unscaledTime - pauseRequestAt > RequestLifetime)
            {
                return;
            }
            Debug.Log("[StacklandsProMax] 自动暂停：读档完成，执行暂停（SpeedUp=" + WorldManager.instance.SpeedUp + "）");
            Pause();
        }

        /// <summary>
        /// 暂停游戏（只在**当前正在跑**的时候调用 —— `TimePause()` 是开关式的，重复调用会变成恢复）。
        /// </summary>
        internal static void Pause()
        {
            WorldManager world = WorldManager.instance;
            if (world == null || world.SpeedUp == 0f)
            {
                return;
            }
            GameScreen screen = GameScreen.instance;
            if (screen == null)
            {
                return;
            }
            screen.TimePause();
        }
    }

    /// <summary>
    /// ① 自动暂停：**加载上一回合存档时**记一个请求（迁移 Auto Pause 的第 1 个行为）。
    /// 真正的暂停在 `AutoPausePlayPatch`（`WorldManager.Play()` 后置）里做，原因见 `AutoPause` 的注释。
    ///
    /// ⚠️ 0.5.9 改挂点：最早挂在 `MainMenu.Awake`、给 ContinueButton 追加回调——但当前游戏版本的启动
    /// 顺序是 `SceneBootstrapper.Awake` 里**先实例化各个管理器（含主菜单 UI，Awake 连锁触发），
    /// 最后才 `ReadyUpMods()`**——等我们的补丁生效时 `MainMenu.Awake` 早就跑完了，而这个界面之后
    /// 只会SetActive 切换、不会重建，Awake 不会再跑 → 订阅永远加不上（0.5.9 用户实测"读档暂停失效"）。
    /// 改成 Postfix `WorldManager.LoadPreviousRound()`：点「继续游戏」必然走它（MainMenu 的两个
    /// Continue 回调里都是 `LoadPreviousRound(); Play();`），此时补丁早已生效；
    /// 「新的开始」不走它，所以新局不误暂停；调试用的强制重载（CheckForceReloadSave）也走它，语义正好。
    /// </summary>
    [HarmonyPatch(typeof(WorldManager), "LoadPreviousRound")]
    internal static class AutoPauseLoadRoundPatch
    {
        [HarmonyPostfix]
        internal static void Postfix()
        {
            if (ModConfig.AutoPauseLoad)
            {
                Debug.Log("[StacklandsProMax] 自动暂停：检测到读档（LoadPreviousRound），已记录暂停请求");
            }
            AutoPause.RequestPauseOnLoad();
        }
    }

    /// <summary>
    /// ② 自动暂停：游戏真正开始跑的那一刻暂停（`WorldManager.Play()` 后置）。
    /// `Play()` 里第一行是 `GameCanvas.instance.SetScreen&lt;GameScreen&gt;()`，所以这时
    /// `GameScreen.instance` 一定有效 —— 这是本项目相对原 mod 的可靠性修正（见 `AutoPause` 注释）。
    /// </summary>
    [HarmonyPatch(typeof(WorldManager), "Play")]
    internal static class AutoPausePlayPatch
    {
        [HarmonyPostfix]
        internal static void Postfix()
        {
            AutoPause.OnGameStarted();
        }
    }

    /// <summary>
    /// ③ 自动暂停：**换地点后**暂停（迁移 Auto Pause 的第 2 个行为，对应设置项
    /// 「切换地点后自动暂停」）。
    ///
    /// 做法参照原 mod：Patch `TransitionScreen` 的**私有**重载
    /// `StartTransition(Action, TransitionType, float)`，把 `___onTransition` 包一层，
    /// 原回调跑完后再暂停。换地点（`WorldManager.GoToBoard`）正是走这个重载。
    ///
    /// 我们加了一条原 mod 没有的判据：**只有"场地真的变了"才暂停**。
    /// 因为 `StartTransition` 是所有过渡的总入口（读档、剧情、退出到主菜单都走它），
    /// 不判一下就会"每段剧情结束都暂停一次"。判据用回调前后的 `CurrentBoard` 引用比较（最直接），
    /// 另外跳过 `GameState.InMenu`（回主菜单、退出到标题界面时不暂停）。
    /// </summary>
    [HarmonyPatch(typeof(TransitionScreen), "StartTransition",
        new Type[] { typeof(Action), typeof(TransitionType), typeof(float) })]
    internal static class AutoPauseTransitionPatch
    {
        [HarmonyPostfix]
        internal static void Postfix(Action onTransition, ref Action ___onTransition)
        {
            Action original = onTransition;
            if (original == null)
            {
                return;
            }
            ___onTransition = delegate
            {
                WorldManager world = WorldManager.instance;
                GameBoard before = world != null ? world.CurrentBoard : null;

                original();

                if (!ModConfig.AutoPauseBoard)
                {
                    return; // 这个开关在过渡结束那一刻再读一次，游戏内改设置能立刻生效
                }
                world = WorldManager.instance;
                if (world == null || world.CurrentGameState == WorldManager.GameState.InMenu)
                {
                    return;
                }
                if (world.CurrentBoard == before)
                {
                    return; // 场地没变（剧情/读档回到同一场地/其它过渡）→ 不暂停
                }
                AutoPause.Pause();
            };
        }
    }
}
