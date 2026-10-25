using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;

namespace StacklandsProMaxNS
{
    /// <summary>
    /// 快捷键：把**鼠标指着的那张卡（或它那一整叠）**"送"出去 —— 它会自己弹起来、飞进能收下它的箱子，
    /// 或者飞到附近同类的牌堆上叠起来。默认按键 **R**，可在 mod 设置界面里改。
    ///
    /// 用户需求原话（0.5.5）：「Stacks n Capacity 插件有个功能是"按 X 将目标物品发送到宝箱/堆叠"，
    /// 这个功能可以让单张卡牌或者牌堆弹起，从而被吸入箱子，把这个功能拿过来，然后改下按键，改成 R，
    /// **mod 设置界面可以修改快捷键**」。
    ///
    /// ── 参考 mod 那条逻辑长什么样（`3378143608` AutoStack 的 `Patch_Hotkeys.Postfix`） ──
    ///
    /// 按下的键若是 Key 38（X），就取鼠标下那张卡的根卡；根卡存在、可拖动、当前没有落点时，
    /// 调一次 `StackSend` 把它朝右送出去。
    ///
    /// 核心就三行：**拿鼠标下的卡 → 抓它那一叠的根卡 → 交给原版 `WorldManager.StackSend`**。
    /// 剩下的全由原版负责：`StackSend` 先问 `TrySendToMagnet`（原版磁铁 / 本项目的磁力箱在这里"截胡"）、
    /// 再问 `TrySendToChest`（能收下它的箱子），都没有就找 2 格内同类的牌堆飞过去，最后 `SendIt()`。
    /// 所以我们**一行"飞"的逻辑都不用写**，而且天然兼容本项目的磁力箱（它就挂在 `TrySendToMagnet` 上）。
    ///
    /// ── 本项目做的两处改动 ─────────────────────────────────────────────────
    ///
    /// ① **按键可配置**：设置界面「把卡送往箱子的快捷键」是一个文本框，填 `Key` 枚举的名字即可
    ///    （`R` / `X` / `F5` / `Space` / `Digit1` …，大小写不敏感）。解析失败会回落到默认的 `R`
    ///    （见 `ModConfig.SetSendToChestKey`）。
    ///    用 `InputController.GetKeyDown()` 而不是裸的 `Keyboard.current`：前者走游戏自己的输入层，
    ///    打开菜单/被 UI 吃掉输入时不会误触发。
    /// ② **没有参照它的 Dollar 特例**（它对纸币会先 `RemoveFromParent()` 再送）：本项目的磁力箱不收纸币，
    ///    而这句话对普通牌堆没有意义，所以不引入。
    ///
    /// ⚠️ 参考 mod 里还有个跟这条功能同名的 transpiler（在 `WorldManager.Update` 里对 `SellCard` 附近动刀），
    /// 那是给它自己的"卖出"路径用的，与本功能的实际效果无关，本项目不需要，也就不引入。
    /// </summary>
    [HarmonyPatch(typeof(WorldManager), "Update")]
    internal static class SendToChestHotkeyPatch
    {
        [HarmonyPostfix]
        internal static void Postfix(WorldManager __instance)
        {
            if (!ModConfig.SendToChestHotkey || __instance == null)
            {
                return;
            }

            InputController input = InputController.instance;
            if (input == null || !input.GetKeyDown(ModConfig.SendToChestKey))
            {
                return;
            }

            // 过场/读档/被 UI 挡住的时候不做事（和原版对拖拽的 gating 一致）
            if (!__instance.CanInteract)
            {
                return;
            }

            GameCard hovered = __instance.HoveredCard;
            GameCard root = hovered == null || hovered.Destroyed ? null : hovered.GetRootCard();
            if (root == null || root.Destroyed || !root.CanBeDragged())
            {
                return;
            }
            // 已经在飞了（BounceTarget 不为空）：别叠加，等它落地。（参考 mod 同样有这个判断）
            if (root.BounceTarget != null)
            {
                return;
            }

            __instance.StackSend(root, Vector3.right, null, true);
        }
    }

    /// <summary>
    /// 屏蔽**原版残留调试提示**：按 F 时卡片上方飘出的那个「绿色笑脸 + 数字」小悬浮条。
    ///
    /// ── 它是什么（反编译核实）────────────────────────────────────────────────
    /// `WorldManager.Update()` 里有一段**官方没删掉的调试代码**：
    ///
    /// 它在按下 F 且鼠标下有卡时，只调一次 `CreateFloatingText`，在卡上飘一句 "Test hovered"。
    ///
    /// 它**只画一个提示**（`CreateFloatingText` 内部只是播个浮动动画），既不调 `AddWellbeing`、
    /// 也不改任何数据 —— 纯视觉噪声。而本项目的「把卡送往箱子」快捷键**默认就是 F**
    /// （0.5.9 起，见 `ModConfig` 的默认值），于是玩家一按 F 送卡，这段调试代码同时触发：
    /// 卡片上方冒出一条绿笑脸 +5 的假提示。用户报的现象与此完全一致。
    ///
    /// ── 做法 ─────────────────────────────────────────────────────────────
    /// 不去动 `WorldManager.Update`（那个方法又大又关键，transpiler 删分支容易误伤），
    /// 而是拦在**唯一的出口** `WorldManager.CreateFloatingText` 上：只挡描述文本等于
    /// `"Test hovered"` 的那一次调用，其余（月末结算、能源设施产出幸福度等）原样放行。
    /// 用 `__args` 取第 4 个参数（`descriptionText`）而不是按参数名注入，避免哪天官方改参数名后补丁静默失效。
    ///
    /// ⚠️ 同一段调试代码里还有 **C 键 = 在鼠标那张卡的位置再生成一张同样的卡**（免费复制卡）和
    /// O 键（村民相关）。它们不是"显示"，不在本条补丁的处理范围内；如果哪天要一并关掉，
    /// 请先问用户（改的是原版玩法行为，不只是视觉）。
    /// </summary>
    [HarmonyPatch(typeof(WorldManager), "CreateFloatingText")]
    internal static class SuppressVanillaTestFloatingTextPatch
    {
        /// <summary>原版调试提示的硬编码文案（未走本地化，所以可以直接按字符串认）。</summary>
        private const string TestHoveredText = "Test hovered";

        /// <summary>原方法签名：`CreateFloatingText(GameCard parent, bool isPositive, int amount, string descriptionText, ...)`。</summary>
        private const int DescriptionTextArgIndex = 3;

        [HarmonyPrefix]
        internal static bool Prefix(object[] __args)
        {
            if (__args == null || __args.Length <= DescriptionTextArgIndex)
            {
                return true;
            }
            string descriptionText = __args[DescriptionTextArgIndex] as string;
            return descriptionText != TestHoveredText;
        }
    }
}
