using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace StacklandsProMaxNS
{
    /// <summary>
    /// 游戏内 mod 设置界面里的开关（每个功能/每张卡一项，默认全开）。
    ///
    /// 机制：`Mod.Config` 是 `ConfigFile`，对应 `Mods\&lt;id&gt;\config.json`。
    /// 用 `GetEntry&lt;T&gt;(键, 默认值, new ConfigUI { NameTerm = ..., TooltipTerm = ... })` 注册后，
    /// 游戏在主菜单 **Mods → 选中本 mod** 的界面里会**自动**为每个条目生成一个控件
    /// （bool → 按钮点击切换 Yes/No；int/float → **数字输入框**（TMP_InputField）；string → 文本输入框；
    /// `ConfigUI.Hidden = true` 则不显示）。0.5.18 起堆叠上限 / 各容量上限都用 int 输入框，
    /// 填回原版值＝原版行为，所以原来配对的"开/关"开关全部去掉了。
    ///
    /// 关键点：UI 改写 `ConfigEntry&lt;T&gt;.BoxedValue` 时会触发 `OnChanged`，
    /// 所以这里直接挂 `OnChanged` 更新静态字段 —— **改完立刻生效，不需要重启游戏**
    /// （所有补丁和磁力箱逻辑都在运行时读这些字段，而不是在打补丁时决定）。
    /// 唯一的例外是"抽卡池"：它是一张**真的列表**（`SetCardBagData.Chances`），
    /// 得把条目真的摘掉/放回去，所以卡总开关变化时会调 ApplyCardAvailability() 重建卡池。
    /// </summary>
    internal static class ModConfig
    {
        // ---- 第一类：每张新卡一个总开关（关掉 = 这张卡"不存在"）----
        internal static bool CardMagnetChest = true;         // 磁力箱（卡牌本体 + 它的创意）
        internal static bool CardMagnetCurrency = true;      // 磁力货币箱（0.5.18 起一个开关同时管：硬币磁力箱 + 贝壳磁力箱 + 磁力信用卡）
        internal static bool CardSuperWarehouse = true;      // 高级仓库（卡牌本体 + 它的创意）
        internal static bool CardAdvancedGreenhouse = true;  // 高级温室（卡牌本体 + 它的创意）
        internal static bool CardMagicCollector = true;      // 魔法收集器（卡牌本体 + 它的创意）
        internal static bool CardAdvancedMarket = true;      // 高级集市（卡牌本体 + 它的创意，0.5.19）
        internal static bool CardMagneticContainer = true;   // 磁力容器（城市专属，卡牌本体 + 它的创意，0.6.0）
        internal static bool CardAdvancedRecyclingCenter = true; // 高级回收中心（城市专属，0.6.2）

        // ---- 第二类：单个功能的开关（只留"玩家真的会想关掉"的那几个）----
        internal static bool OpenWholePack = true;           // 点击一次开完整个卡包
        internal static bool NoPortalAfterWitch = true;      // 打败女巫后不再刷随机传送门
        internal static bool IdleVillagerBorder = true;      // 空闲村民红色边框
        internal static bool IdleFacilityBorder = true;      // 空闲生产设施红色边框（0.5.6 合并 Show Idle Villager 的另一半）
        internal static bool RightDrag = true;               // 右键拖动牌堆 / 单张牌
        internal static bool DoubleClickRestack = true;      // 双击把附近同种卡归成一堆
        internal static bool BoxSelect = true;               // Ctrl+拖空白框选多张卡，然后整组一起搬
        internal static bool HitTextCenter = true;           // 伤害/治疗数字显示在卡牌中间（合并 Hit Text To Center）
        internal static bool ResourceChestEjectSide = true;  // 原版资源箱取出的牌"往边上掉"（原版是弹起来落回箱子上）
        internal static bool ResourceChestMoreTypes = true;  // 资源箱能收的卡牌类型扩到磁力箱白名单（材料+食物+装备）
        internal static bool SendToChestHotkey = true;       // 按快捷键把鼠标指着的卡/牌堆"送"进箱子或同类堆
        internal static bool IdeaSearch = true;              // Alt+左键＝按标题搜主意、Alt+右键＝按描述搜主意
        internal static bool BreedingPenKeepAnimals = true;  // 动物留在饲养栏：繁殖后亲代不弹出去，自动连着繁殖
        internal static bool ResourceChestNoCardCount = true; // 资源箱内的资源不按数量计入卡牌上限（每个箱子固定占 2 张，同 Cities 口径）
        internal static bool QuickBuyPack = true;            // Ctrl+点击购买箱快速购买卡包（散币优先，不够从箱子/信用卡扣）

        // ---- 第三类：迁移来的两个创意工坊 mod（各留一个总开关）----
        internal static bool SidebarHeight = true;           // 左侧信息栏自动加高（迁移 HigherSidebar）
        internal static bool DetailInfo = true;              // 详细战斗/掉落信息（迁移 BetterInfo）
        internal static float InfoBoxMinHeight = 300f;       // 信息栏最小高度（原 mod 默认就是 300）
        internal static float InfoBoxMaxHeight = 700f;       // 信息栏最大高度（原 mod 默认就是 700）

        // ---- 第四类：另外两个迁移来的 mod（各一个总开关，0.5.6）----
        internal static bool AutoPauseLoad = true;           // 读档后自动暂停（迁移 Auto Pause）
        internal static bool AutoPauseBoard = true;          // 切换地点后自动暂停（迁移 Auto Pause）
        internal static bool MultiRowCombat = true;          // 战斗多行排列（迁移 Lopi's Conflict Rows）
        internal static float ConflictRowSize = 7f;          // 每行战斗单位上限（原 mod 默认 7）

        /// <summary>
        /// 「把卡送往箱子」的快捷键（`UnityEngine.InputSystem.Key` 的名字，大小写不敏感）。
        /// 存的是玩家填的原文（设置界面的文本框会显示它），真正用的是解析结果 <see cref="SendToChestKey"/>。
        /// 0.5.9 默认从 R 改成 **F**（用户要求："让卡牌飞起来以便它能飞往磁力箱"的按键从 R 改成 F；
        /// 存档里存过别的值就还是用玩家自己设的）。
        /// </summary>
        internal static string SendToChestKeyName = "F";

        /// <summary>解析后的快捷键；填了无效的名字会回落到默认的 `F`。</summary>
        internal static Key SendToChestKey = Key.F;

        /// <summary>
        /// 磁力箱 / 硬币磁力箱 / 超级仓库的"子功能"不再单独给开关，一律常开
        /// （用户要求："每个新增的卡牌只留一个总开关，其他多余的开关去掉"）。
        /// 想临时关掉某一项，就改这里的常量重新编译。
        /// </summary>
        internal const bool MagnetPull = true;               // 箱子：自动吸附
        internal const bool MagnetImmunity = true;           // 箱子：取出后 10 分钟不被回吸
        internal const bool MagnetLook = true;               // 箱子：外观同原版
        internal const bool MagnetRecipe = true;             // 箱子：允许合成配方
        internal const bool SuperWarehouseCapacity = true;   // 超级仓库：+140 卡牌上限
        internal const bool WarehouseStacking = true;        // 仓库可互相叠放（超级仓库配方需要）

        // ---- 可调数值（0.5.18 起容量/上限全部做成游戏内输入框，原版值填回去＝原版行为）----
        internal static float StackSearchDistance = 4f;      // 自动归堆的搜索距离（原版 2）
        internal static int StackLimitSize = 100;            // 卡牌堆叠上限（原版 30）
        internal static int ResourceChestCapacity = 10000;   // 原版资源箱存储上限（原版 100）
        internal static int CoinShellChestCapacity = 10000;  // 硬币箱/贝壳宝箱存币上限（原版 100）
        internal static int CreditcardCapacity = 100000;     // 信用卡存钞上限（原版 1000）
        internal static int HotpotCapacity = 10000;          // 火锅（炖锅）食物容量（原版 50）
        internal static int MagnetChestCapacity = 10000;     // 磁力箱存储上限
        internal static int CoinShellMagnetChestCapacity = 10000; // 硬币/贝壳磁力箱存币上限
        internal static int MagnetCreditcardCapacity = 100000;    // 磁力信用卡存钞上限

        /// <summary>框选最多同时搬多少张（不是"能选多少张"，而是防止一次搬上百张时每帧算一堆位置）。</summary>
        internal const int BoxSelectMaxCards = 200;

        // ---- 本 mod 新增的卡（id 与"这张卡是否可用"的对应表）----
        /// <summary>磁力箱的卡 id。</summary>
        internal const string MagnetChestId = "stacklandspro_magnet_chest";

        /// <summary>硬币磁力箱的卡 id。</summary>
        internal const string CoinMagnetChestId = "stacklandspro_coin_magnet_chest";

        /// <summary>贝壳磁力箱的卡 id。</summary>
        internal const string ShellMagnetChestId = "stacklandspro_shell_magnet_chest";

        /// <summary>磁力信用卡的卡 id。</summary>
        internal const string MagnetCreditcardId = "stacklandspro_magnet_creditcard";

        /// <summary>超级仓库（界面显示名＝高级仓库）的卡 id。</summary>
        internal const string SuperWarehouseId = "stacklandspro_super_warehouse";

        /// <summary>高级温室的卡 id（实际字符串在 <see cref="AdvancedGreenhouse.CardId"/>，这里只做转发避免两处写死）。</summary>
        internal const string AdvancedGreenhouseId = AdvancedGreenhouse.CardId;

        /// <summary>魔法收集器的卡 id（实际字符串在 <see cref="MagicCollectorCard.CardId"/>，这里只做转发避免两处写死）。</summary>
        internal const string MagicCollectorId = MagicCollectorCard.CardId;

        /// <summary>高级集市的卡 id。</summary>
        internal const string AdvancedMarketId = "stacklandspro_advanced_market";

        /// <summary>磁力容器的卡 id（城市专属，实际字符串在 <see cref="MagneticContainer.CardId"/>）。</summary>
        internal const string MagneticContainerId = MagneticContainer.CardId;

        /// <summary>高级回收中心的卡 id（城市专属，实际字符串在 <see cref="AdvancedRecyclingCenter.CardId"/>）。</summary>
        internal const string AdvancedRecyclingCenterId = AdvancedRecyclingCenter.CardId;

        /// <summary>
        /// "卡总开关"关掉时要一起失效的 id：**产物卡本身 + 它的创意卡**。
        /// 关掉一张卡 = 它的创意也抽不到（"没有这个卡牌相关内容"），所以两者一起进禁用集合。
        /// </summary>
        private static readonly string[] MagnetChestIds =
        {
            MagnetChestId,
            "stacklandspro_blueprint_magnet_chest",
        };

        private static readonly string[] CoinMagnetChestIds =
        {
            CoinMagnetChestId,
            "stacklandspro_blueprint_coin_magnet_chest",
        };

        private static readonly string[] ShellMagnetChestIds =
        {
            ShellMagnetChestId,
            "stacklandspro_blueprint_shell_magnet_chest",
        };

        private static readonly string[] MagnetCreditcardIds =
        {
            MagnetCreditcardId,
            "stacklandspro_blueprint_magnet_creditcard",
        };

        private static readonly string[] SuperWarehouseIds =
        {
            SuperWarehouseId,
            "stacklandspro_blueprint_super_warehouse",
        };

        private static readonly string[] AdvancedGreenhouseIds =
        {
            AdvancedGreenhouseId,
            "stacklandspro_blueprint_advanced_greenhouse",
        };

        private static readonly string[] MagicCollectorIds =
        {
            MagicCollectorId,
            "stacklandspro_blueprint_magic_collector",
        };

        private static readonly string[] AdvancedMarketIds =
        {
            AdvancedMarketId,
            "stacklandspro_blueprint_advanced_market",
        };

        private static readonly string[] MagneticContainerIds =
        {
            MagneticContainerId,
            "stacklandspro_blueprint_magnetic_container",
        };

        private static readonly string[] AdvancedRecyclingCenterIds =
        {
            AdvancedRecyclingCenterId,
            "stacklandspro_blueprint_advanced_recycling_center",
        };

        /// <summary>被关掉的卡的 id（供补丁做 O(1) 判断；开关变化时由 RebuildDisabledCards 重建）。</summary>
        private static readonly HashSet<string> DisabledCardIds = new HashSet<string>();

        /// <summary>我们登记进抽卡池的卡（卡 id → 卡包类型与权重）。刷新卡池时以此为准重建。</summary>
        private static readonly List<RegisteredCard> RegisteredCards = new List<RegisteredCard>();

        private class RegisteredCard
        {
            public string CardId;
            public SetCardBagType BagType;
            public int Chance;
        }

        /// <summary>在 Mod.Ready() 里调用一次：把开关注册进游戏设置界面。</summary>
        internal static void Init(Mod mod)
        {
            ConfigFile config = mod.Config;
            if (config == null)
            {
                return;
            }

            // ---- 每张卡一个总开关 ----
            Register(config, "Card: Magnet chest", "card_magnet_chest",
                delegate (bool v) { CardMagnetChest = v; OnCardToggleChanged(); });
            // 0.5.18：硬币磁力箱 / 贝壳磁力箱 / 磁力信用卡三个开关合并成一个"磁力货币箱"
            Register(config, "Card: Magnet currency chests", "card_magnet_currency",
                delegate (bool v) { CardMagnetCurrency = v; OnCardToggleChanged(); });
            Register(config, "Card: Super warehouse", "card_super_warehouse",
                delegate (bool v) { CardSuperWarehouse = v; OnCardToggleChanged(); });
            Register(config, "Card: Advanced greenhouse", "card_advanced_greenhouse",
                delegate (bool v) { CardAdvancedGreenhouse = v; OnCardToggleChanged(); });
            Register(config, "Card: Magic collector", "card_magic_collector",
                delegate (bool v) { CardMagicCollector = v; OnCardToggleChanged(); });
            Register(config, "Card: Advanced market", "card_advanced_market",
                delegate (bool v) { CardAdvancedMarket = v; OnCardToggleChanged(); });
            Register(config, "Card: Magnetic container", "card_magnetic_container",
                delegate (bool v) { CardMagneticContainer = v; OnCardToggleChanged(); });
            Register(config, "Card: Advanced recycling center", "card_advanced_recycling_center",
                delegate (bool v) { CardAdvancedRecyclingCenter = v; OnCardToggleChanged(); });

            // ---- 功能开关 ----
            // 0.5.18：堆叠上限 / 归堆距离 / 各容量上限改成输入框（int 条目在设置界面就是数字输入框），
            // 填回原版值＝原版行为，所以不再需要"开/关"开关
            Register(config, "Stack limit", "stack_limit_size", 100,
                delegate (int v) { StackLimitSize = ClampAtLeast(v, 1); });
            Register(config, "Stack search distance", "stack_distance", 4f,
                delegate (float v) { StackSearchDistance = v; });
            Register(config, "Open whole pack", "open_pack", delegate (bool v) { OpenWholePack = v; });
            Register(config, "No portal after witch", "no_portal", delegate (bool v) { NoPortalAfterWitch = v; });
            Register(config, "Idle villager border", "idle_villager", delegate (bool v) { IdleVillagerBorder = v; });
            Register(config, "Idle facility border", "idle_facility",
                delegate (bool v) { IdleFacilityBorder = v; });
            Register(config, "Right-drag cards", "right_drag", delegate (bool v) { RightDrag = v; });
            Register(config, "Double-click restack", "double_click",
                delegate (bool v) { DoubleClickRestack = v; });
            Register(config, "Box select cards", "box_select", delegate (bool v) { BoxSelect = v; });
            Register(config, "Hit text to center", "hit_text_center", delegate (bool v) { HitTextCenter = v; });
            Register(config, "Resource chest ejects sideways", "resource_chest_eject",
                delegate (bool v) { ResourceChestEjectSide = v; });
            Register(config, "Resource chest capacity", "resource_chest_capacity", 10000,
                delegate (int v) { ResourceChestCapacity = ClampAtLeast(v, 1); });
            Register(config, "Resource chest more card types", "resource_chest_more_types",
                delegate (bool v) { ResourceChestMoreTypes = v; });
            Register(config, "Coin/shell chest max coins", "coin_shell_chest_capacity", 10000,
                delegate (int v) { CoinShellChestCapacity = ClampAtLeast(v, 1); });
            Register(config, "Credit card max dollars", "creditcard_capacity_size", 100000,
                delegate (int v) { CreditcardCapacity = ClampAtLeast(v, 1); });
            Register(config, "Hotpot food capacity", "hotpot_capacity_size", 10000,
                delegate (int v) { HotpotCapacity = ClampAtLeast(v, 1); });
            Register(config, "Magnet chest capacity", "magnet_chest_capacity", 10000,
                delegate (int v) { MagnetChestCapacity = ClampAtLeast(v, 1); });
            Register(config, "Coin/shell magnet chest capacity", "coin_shell_magnet_chest_capacity", 10000,
                delegate (int v) { CoinShellMagnetChestCapacity = ClampAtLeast(v, 1); });
            Register(config, "Magnet credit card capacity", "magnet_creditcard_capacity", 100000,
                delegate (int v) { MagnetCreditcardCapacity = ClampAtLeast(v, 1); });
            Register(config, "Send card to chest hotkey", "send_to_chest",
                delegate (bool v) { SendToChestHotkey = v; });
            Register(config, "Send to chest key", "send_to_chest_key", "F",
                delegate (string v) { SetSendToChestKey(v); });
            Register(config, "Quick buy pack on Ctrl+click", "quick_buy_pack",
                delegate (bool v) { QuickBuyPack = v; });
            Register(config, "Idea search on Alt+click", "idea_search",
                delegate (bool v) { IdeaSearch = v; });
            Register(config, "Breeding pen keeps animals", "breeding_pen_keep",
                delegate (bool v) { BreedingPenKeepAnimals = v; });
            Register(config, "Resource chest cards don't count", "resource_chest_no_count",
                delegate (bool v) { ResourceChestNoCardCount = v; });

            // ---- 迁移来的两个 mod（HigherSidebar / BetterInfo）----
            // 用户要求：「迁移这两个mod的功能，迁移完成后帮我禁用这两个mod」，
            // 所以这两个功能各自只留**一个总开关**（"开关要克制"），子功能一律常开。
            Register(config, "Sidebar: auto height", "sidebar_height",
                delegate (bool v) { SidebarHeight = v; });
            Register(config, "Sidebar: min height", "sidebar_min_height", 300f,
                delegate (float v) { InfoBoxMinHeight = v; });
            Register(config, "Sidebar: max height", "sidebar_max_height", 700f,
                delegate (float v) { InfoBoxMaxHeight = v; });
            Register(config, "Detailed info", "detail_info",
                delegate (bool v) { DetailInfo = v; ApplyDetailInfoGate(); });

            // ---- 迁移来的 Auto Pause / Lopi's Conflict Rows（0.5.6）----
            // Auto Pause 原本只有一个「换地点暂停」开关（读档暂停是常开的），本项目按"每项功能一个开关"
            // 给它两个开关；Conflict Rows 原本只有一个数值（没有开关），这里补一个总开关。
            Register(config, "Auto pause on load", "auto_pause_load",
                delegate (bool v) { AutoPauseLoad = v; });
            Register(config, "Auto pause on location change", "auto_pause_board",
                delegate (bool v) { AutoPauseBoard = v; });
            Register(config, "Multi-row combat", "conflict_rows",
                delegate (bool v) { MultiRowCombat = v; });
            Register(config, "Combat row size", "conflict_row_size", 7f,
                delegate (float v) { ConflictRowSize = v; });
        }

        /// <summary>
        /// 「详细信息」的**总闸门**：原版 `AdvancedSettingsScreen.AdvancedCombatStatsEnabled`
        /// （Options → Advanced 里那个"详细战斗数值"）。
        ///
        /// 为什么必须由我们来开：`Combatable.UpdateCardText()` 与 `BaseVillager.UpdateCardText()` 都是
        /// `if (AdvancedCombatStatsEnabled || 当前界面是图鉴) 才追加 GetCombatableDescriptionAdvanced()`，
        /// 它关着 = **连原版那四项数值都不会显示**，更别说我们补的攻击类型/估算 DPS 了。
        /// 它的值从 `PlayerPrefs` 读（本机从没存过这个键 → 默认 0＝关），
        /// 而**读它的那行代码在那个"玩家点进设置才懒创建"的界面 `Awake()` 里** ——
        /// 所以只在那个 `Awake` 的后置里设是**完全失效**的（0.5.6 实测症状：村民的攻速/伤害整块不显示）。
        /// 这里在 `Mod.Ready()` 阶段（以及玩家在游戏内把本开关打开时）设一次兜底，
        /// `InfoDetailAdvancedCombatPatch` 在那个界面 `Awake` 之后再设一次。
        /// 详见 `Patches\InfoDetailPatch.cs` ⑫ 的注释。
        /// </summary>
        private static void ApplyDetailInfoGate()
        {
            if (DetailInfo)
            {
                AdvancedSettingsScreen.AdvancedCombatStatsEnabled = true;
            }
        }

        /// <summary>
        /// 解析并应用快捷键设置：填 `Key` 枚举的名字（`R` / `X` / `F5` / `Space` / `Digit1` …，大小写不敏感）。
        /// 空值或解析不出来 → 回落到默认的 `R`（并在日志里说一句，方便玩家自查拼写）。
        /// </summary>
        internal static void SetSendToChestKey(string value)
        {
            string trimmed = value == null ? null : value.Trim();
            Key parsed;
            if (!string.IsNullOrEmpty(trimmed)
                && Enum.TryParse<Key>(trimmed, true, out parsed)
                && parsed != Key.None)
            {
                SendToChestKey = parsed;
                SendToChestKeyName = trimmed;
                return;
            }

            if (!string.IsNullOrEmpty(trimmed) && !string.Equals(trimmed, "F", StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogWarning("[StacklandsProMax] 快捷键 \"" + trimmed
                    + "\" 无法识别（要填 Key 枚举名，如 F / X / F5 / Space），已回落到默认的 F");
            }
            SendToChestKey = Key.F;
            SendToChestKeyName = "F";
        }

        private static void Register(ConfigFile config, string key, string termSuffix, Action<bool> apply)
        {
            Register(config, key, termSuffix, true, apply);
        }

        private static void Register(ConfigFile config, string key, string termSuffix, bool defaultValue,
            Action<bool> apply)
        {
            ConfigEntry<bool> entry = config.GetEntry<bool>(key, defaultValue, new ConfigUI
            {
                NameTerm = "stacklandspro_cfg_" + termSuffix + "_name",
                TooltipTerm = "stacklandspro_cfg_" + termSuffix + "_tip"
            });
            entry.OnChanged = apply;
            apply(entry.Value); // 用已经存下来的值初始化静态开关
        }

        private static void Register(ConfigFile config, string key, string termSuffix, float defaultValue,
            Action<float> apply)
        {
            ConfigEntry<float> entry = config.GetEntry<float>(key, defaultValue, new ConfigUI
            {
                NameTerm = "stacklandspro_cfg_" + termSuffix + "_name",
                TooltipTerm = "stacklandspro_cfg_" + termSuffix + "_tip"
            });
            entry.OnChanged = apply;
            apply(entry.Value); // 用已经存下来的值初始化
        }

        /// <summary>
        /// 整数类设置（0.5.18 起堆叠上限 / 各容量上限都用它）：原版设置界面为 int 条目生成的
        /// 就是**数字输入框**（`CreateNumberConfig`：TMP_InputField + Integer 校验），玩家可自由填数。
        /// 填回原版值（如堆叠上限 30、资源箱 100）就是原版行为，所以不再配"开/关"开关。
        /// ⚠️ 新键名与旧 bool 开关的键**故意不同**（config.json 里类型变了不能复用旧键）。
        /// </summary>
        private static void Register(ConfigFile config, string key, string termSuffix, int defaultValue,
            Action<int> apply)
        {
            ConfigEntry<int> entry = config.GetEntry<int>(key, defaultValue, new ConfigUI
            {
                NameTerm = "stacklandspro_cfg_" + termSuffix + "_name",
                TooltipTerm = "stacklandspro_cfg_" + termSuffix + "_tip"
            });
            entry.OnChanged = apply;
            apply(entry.Value); // 用已经存下来的值初始化
        }

        /// <summary>输入框里被清空/填了非法值时原版 `int.Parse` 会抛异常（条目保持旧值），这里再兜一道下限。</summary>
        private static int ClampAtLeast(int value, int min)
        {
            return Mathf.Max(min, value);
        }

        /// <summary>文本类设置（目前只有"送往箱子的快捷键"）。游戏会为它生成一个可输入的文本框。</summary>
        private static void Register(ConfigFile config, string key, string termSuffix, string defaultValue,
            Action<string> apply)
        {
            ConfigEntry<string> entry = config.GetEntry<string>(key, defaultValue, new ConfigUI
            {
                NameTerm = "stacklandspro_cfg_" + termSuffix + "_name",
                TooltipTerm = "stacklandspro_cfg_" + termSuffix + "_tip"
            });
            entry.OnChanged = apply;
            apply(entry.Value); // 用已经存下来的值初始化
        }

        /// <summary>卡总开关变化：重建"关掉的卡"集合，并刷新抽卡池（顺便刷图鉴）。</summary>
        private static void OnCardToggleChanged()
        {
            RebuildDisabledCards();
            ApplyCardAvailability();
            RefreshCardopedia(); // ApplyCardAvailability 里本来就调了，这里显式再保证一次（顺序无关）
        }

        private static void RebuildDisabledCards()
        {
            DisabledCardIds.Clear();
            if (!CardMagnetChest)
            {
                DisabledCardIds.UnionWith(MagnetChestIds);
            }
            if (!CardMagnetCurrency)
            {
                // 0.5.18：一个开关管三种"磁力货币箱"
                DisabledCardIds.UnionWith(CoinMagnetChestIds);
                DisabledCardIds.UnionWith(ShellMagnetChestIds);
                DisabledCardIds.UnionWith(MagnetCreditcardIds);
            }
            if (!CardSuperWarehouse)
            {
                DisabledCardIds.UnionWith(SuperWarehouseIds);
            }
            if (!CardAdvancedGreenhouse)
            {
                DisabledCardIds.UnionWith(AdvancedGreenhouseIds);
            }
            if (!CardMagicCollector)
            {
                DisabledCardIds.UnionWith(MagicCollectorIds);
            }
            if (!CardAdvancedMarket)
            {
                DisabledCardIds.UnionWith(AdvancedMarketIds);
            }
            if (!CardMagneticContainer)
            {
                DisabledCardIds.UnionWith(MagneticContainerIds);
            }
            if (!CardAdvancedRecyclingCenter)
            {
                DisabledCardIds.UnionWith(AdvancedRecyclingCenterIds);
            }
        }

        /// <summary>这张卡现在是不是被玩家关掉了？（补丁用它做早退判断）</summary>
        internal static bool IsCardDisabled(string cardId)
        {
            return !string.IsNullOrEmpty(cardId) && DisabledCardIds.Contains(cardId);
        }

        /// <summary>有没有任何一张卡被关掉？（补丁用它做"总闸"，全开时零开销）</summary>
        internal static bool HasDisabledCards
        {
            get { return DisabledCardIds.Count > 0; }
        }

        // ------------------------------------------------------------------
        //  抽卡池 / 卡牌图鉴：让"关掉的卡"真的抽不到
        // ------------------------------------------------------------------

        /// <summary>
        /// 登记"这张卡原本加在哪个卡包、权重多少"。Mod.Ready() 登记新卡时顺手记一笔，
        /// 之后刷新卡池就能按这份记录原样重建（不会越刷越少、也不会丢失原来的权重）。
        /// </summary>
        internal static void RememberCardBag(string cardId, SetCardBagType bagType, int chance)
        {
            if (string.IsNullOrEmpty(cardId))
            {
                return;
            }
            RegisteredCards.Add(new RegisteredCard { CardId = cardId, BagType = bagType, Chance = chance });
        }

        /// <summary>
        /// 把"关掉的卡"从所有抽卡池里摘掉、把重新打开的放回去。
        ///
        /// 为什么能立刻生效：抽卡池每次开卡包都会**现算**
        /// （`CardBag.GetRawCardChanges` 直接读 `loader.SetCardBags` 里的 Chances），
        /// 所以改这张列表就够了，不用重启游戏。
        /// 自己登记的卡（RegisteredCards）先全部移除再按当前开关重建，
        /// 所以反复开关也不会重复或丢权重。只动我们自己的卡，原版卡池完全不碰。
        /// </summary>
        internal static void ApplyCardAvailability()
        {
            GameDataLoader loader = GetLoader();
            if (loader == null || loader.SetCardBags == null)
            {
                return;
            }

            HashSet<string> ourIds = new HashSet<string>();
            foreach (RegisteredCard registered in RegisteredCards)
            {
                ourIds.Add(registered.CardId);
            }

            // 1) 把自己登记过的卡全部从卡池里移除
            if (ourIds.Count > 0)
            {
                foreach (SetCardBagData bag in loader.SetCardBags)
                {
                    if (bag == null || bag.Chances == null)
                    {
                        continue;
                    }
                    bag.Chances.RemoveAll(delegate (SimpleCardChance c)
                    {
                        return c == null || ourIds.Contains(c.CardId);
                    });
                }
            }

            // 2) 按当前开关把"还开着的卡"放回它原本的卡包
            foreach (RegisteredCard registered in RegisteredCards)
            {
                if (IsCardDisabled(registered.CardId))
                {
                    continue;
                }
                foreach (SetCardBagData bag in loader.SetCardBags)
                {
                    if (bag != null && bag.SetCardBagType == registered.BagType && bag.Chances != null)
                    {
                        bag.Chances.Add(new SimpleCardChance(registered.CardId, registered.Chance));
                    }
                }
            }

            // 3) 卡牌图鉴：关掉的卡不再出现（只改我们自己的卡）
            RefreshCardopedia();
        }

        /// <summary>卡包列表可能在 Mod.Ready() 之前还没准备好，所以到处都用这个取一遍。</summary>
        private static GameDataLoader GetLoader()
        {
            if (WorldManager.instance == null)
            {
                return null;
            }
            return WorldManager.instance.GameDataLoader;
        }

        /// <summary>由 Mod.Ready() 在卡包登记完之后调用：建集合 + 立即按开关刷新一次卡池。</summary>
        internal static void OnCardBagsReady()
        {
            RebuildDisabledCards();
            ApplyCardAvailability();
        }

        /// <summary>
        /// 卡牌图鉴（Cardopedia）按 `HideFromCardopedia` 过滤
        /// （`CardDataPrefabs.Count(x => !x.HideFromCardopedia)` 等），所以把关掉的卡标上即可。
        /// **产物卡和它的创意卡一起标**——"没有这个卡牌相关内容"。
        /// 注意：只改**我们自己的卡**，不碰原版卡的数据。
        /// </summary>
        private static void RefreshCardopedia()
        {
            GameDataLoader loader = GetLoader();
            if (loader == null)
            {
                return;
            }
            ApplyCardopediaGroup(loader, MagnetChestIds, !CardMagnetChest);
            ApplyCardopediaGroup(loader, CoinMagnetChestIds, !CardMagnetCurrency);
            ApplyCardopediaGroup(loader, ShellMagnetChestIds, !CardMagnetCurrency);
            ApplyCardopediaGroup(loader, MagnetCreditcardIds, !CardMagnetCurrency);
            ApplyCardopediaGroup(loader, SuperWarehouseIds, !CardSuperWarehouse);
            ApplyCardopediaGroup(loader, AdvancedGreenhouseIds, !CardAdvancedGreenhouse);
            ApplyCardopediaGroup(loader, MagicCollectorIds, !CardMagicCollector);
            ApplyCardopediaGroup(loader, AdvancedMarketIds, !CardAdvancedMarket);
            ApplyCardopediaGroup(loader, MagneticContainerIds, !CardMagneticContainer);
            ApplyCardopediaGroup(loader, AdvancedRecyclingCenterIds, !CardAdvancedRecyclingCenter);
        }

        private static void ApplyCardopediaGroup(GameDataLoader loader, string[] cardIds, bool hidden)
        {
            foreach (string cardId in cardIds)
            {
                CardData prefab = loader.GetCardFromId(cardId);
                if (prefab != null)
                {
                    prefab.HideFromCardopedia = hidden;
                }
            }
        }

        /// <summary>
        /// 给"堆叠上限"的 transpiler 用：它把原版那个 30 常量替换成对这个方法的调用，
        /// 于是设置里的数字输入框**立刻生效**（不需要重新打补丁）。
        /// 0.5.18 起上限是输入框（默认 100）；填 30 ＝原版行为。
        /// </summary>
        internal static int GetMaxStackSize()
        {
            return StackLimitSize;
        }

        /// <summary>
        /// 给"自动归堆搜索距离"的 transpiler 用：把原版写死的 2f 换成这个方法调用，
        /// 于是能运行时改（不用重启游戏）。
        /// 0.5.18 起不再有"归堆更远"开关：距离本身就是输入框，填 2 ＝原版行为。
        /// </summary>
        internal static float GetStackSearchDistance()
        {
            // 原版是 2；这里给个下限，防止填 0/负数导致"永远归不了堆"
            return Mathf.Max(1f, StackSearchDistance);
        }
    }
}
