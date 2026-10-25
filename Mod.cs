// 游戏程序集（GameScripts.dll / UnityEngine.dll）没有做可空引用标注，
// 开启 <Nullable>enable</Nullable> 会产生大量误报，所以本文件关闭可空分析。
#nullable disable

using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;

namespace StacklandsProMaxNS
{
    public class StacklandsProMax : Mod
    {
        /// <summary>
        /// 所有功能补丁类。想关掉某个功能，把对应的一行注释掉重新编译即可。
        /// </summary>
        private static readonly Type[] PatchClasses =
        {
            typeof(StackLimitPatch),         // 1) 卡牌堆叠上限 30 -> 100（0.5.8 曾为 999，0.5.14 用户改 100）
            typeof(MagnetTargetPatch),       // 1b) 磁铁/磁力箱的"截胡"入口（含堆叠上限）
            typeof(StackSendDistancePatch),      // 1c) 自动归堆的搜索距离 2 -> 可调（Stack Further）
            typeof(TrySendToChestDistancePatch), // 1d) 自动送进箱子的搜索距离 2 -> 同一个可调值
            typeof(MagnetChestLookPatch),    // 磁力箱外观：补上原版储物箱的方形内容框
            typeof(ChestMagnetCraftingPatch),// 配方：让资源箱能叠到磁铁上（否则磁力箱造不出来）
            typeof(OpenWholePackPatch),      // 2) 点一下开完整个卡包
            typeof(NoPortalAfterWitchPatch), // 3) 打败女巫后不再刷随机传送门
            typeof(IdleVillagerBorderPatch), // 4) 空闲村民红色边框
            typeof(RightDragStackPatch),     // 5) 右键按住拖动整个牌堆
            typeof(RightDragInputPatch),     // 5b) 让右键不被原版抢成"抓单张"
            typeof(DoubleClickRestackPatch), // 5c) 双击把附近同种卡归成一堆（Better Mouse Controls）
            typeof(SuperWarehouseCapacityPatch), // 6) 超级仓库 +140 卡牌上限
            typeof(WarehouseStackingPatch),  // 6b) 允许仓库互相叠放（配方需要叠 10 张）
            typeof(CardGatePatch),           // 7) 关掉的卡：配方不成立（每张卡一个总开关）
            typeof(ConnectorDiagnosticsPatch), // 8) 启动时打印连接器配置（排查"运输输出"用）
            typeof(BoxSelectTickPatch),      // 9) Ctrl+拖空白框选多张卡
            typeof(BoxSelectCardClickPatch), // 9b) Ctrl+点击卡片：挡掉卡片自身的点击（如箱子取物），不再有选中动作
            typeof(BoxSelectBoosterClickPatch), // 9c) 同上，卡包
            typeof(BoxSelectCameraPatch),    // 9d) Ctrl+拖空白时不要让原版平移镜头
            typeof(BoxSelectStackTakeoverPatch), // 9e) 框选多张后，点牌堆任意位置也能整组拖
            typeof(HitTextTagPatch),         // 10) 标记治疗数字（给下面那个补丁用）
            typeof(HitTextSetPositionPatch), // 10b) 伤害/治疗数字显示在卡牌中间（合并 Hit Text To Center）
            typeof(ChestDragOutPatch),       // 11) 箱子拖出（Shift 10 张 / Ctrl 1 张）+ 屏蔽原版 Shift 拖整叠
            typeof(ChestDropRedirectPatch),  // 11b) 拖出后"只点一下"松手时，改成往旁边弹到地上（别落回箱子）
            typeof(ChestTakeOutTickPatch),   // 11c) 拖出后逐帧采样鼠标偏移，区分"点一下"和"拖回去放回箱子"（0.5.17）
            typeof(IdeaSearchKnowledgePatch), // 14a) 主意搜索：按模式匹配标题/描述（迁移 Extra Search Mod）
            typeof(IdeaSearchInputPatch),    // 14b) Alt+左键＝标题搜索、Alt+右键＝描述搜索、Alt+点空白＝清空
            typeof(IdeaSearchCardClickPatch), // 14c) Alt 手势时挡掉卡片自身的点击（别顺手取物）
            typeof(IdeaSearchBoosterClickPatch), // 14d) 同上，卡包（别顺手开包）
            typeof(IdeaSearchButtonsPatch),  // 14e) 在搜索框旁边加三个固定按钮（搜标题/搜描述/清除）
            typeof(ResourceChestEjectPatch), // 12) 原版资源箱取出的牌往边上掉（原版会弹回箱子上）
            typeof(SendToChestHotkeyPatch),  // 13) 快捷键（默认 F）把鼠标指着的卡/牌堆送进箱子或同类堆
            typeof(SuppressVanillaTestFloatingTextPatch), // 13b) 屏蔽原版按 F 残留的调试提示（绿笑脸 "Test hovered"）
            typeof(SidebarHeightUpdatePatch), // 15) 左侧信息栏自动加高（迁移 HigherSidebar）
            typeof(SidebarHeightAwakePatch),  // 15b) 信息栏加高后不吃鼠标射线（否则挡住下面的卡）
            typeof(InfoDetailCombatPatch),    // 16a) 详细信息：战斗详解 + 估算 DPS + 攻击类型
            typeof(InfoDetailPossibleDropsPatch), // 16b) 普通 Mob 也有掉落表
            typeof(InfoDetailStablePortalPatch),  // 16c) 传送门描述补"第 N 波"
            typeof(InfoDetailBoosterpackPatch),   // 16d) 卡包内容带百分比
            typeof(InfoDetailHarvestablePatch),   // 16e) 地点/可采集物掉落表带百分比（含四个特例）
            typeof(InfoDetailCombatableHarvestablePatch), // 16f) 战斗采集物同上
            typeof(InfoDetailTravellingCartPatch),        // 16g) 旅行货车同上
            typeof(InfoDetailCardopediaPatch),    // 16h) 图鉴里不重复显示掉落摘要
            typeof(InfoDetailEquipablePatch),     // 16i) 装备补攻击类型
            typeof(InfoDetailInfoTextPatch),      // 16j) 信息栏：卡包内容 / 牌堆描述 / 岛上贝壳+金币
            typeof(InfoDetailBlueprintPatch),     // 16k) 蓝图补产物的装备详解
            typeof(InfoDetailAdvancedCombatPatch), // 16l) 打开原版"详细战斗数值"选项
            typeof(AutoPauseLoadRoundPatch),  // 17a) 自动暂停：加载上一回合存档时记一个请求（迁移 Auto Pause；0.5.9 由 MainMenu.Awake 改挂 WorldManager.LoadPreviousRound，见 AutoPausePatch.cs 注释）
            typeof(AutoPausePlayPatch),       // 17b) 自动暂停：真正进入游戏那一刻暂停（GameScreen.instance 这时才有效）
            typeof(AutoPauseTransitionPatch), // 17c) 自动暂停：换地点过渡结束后暂停
            typeof(ConflictRowsPositionPatch), // 18a) 战斗多行：每张战斗卡的位置（迁移 Lopi's Conflict Rows）
            typeof(ConflictRowsSizePatch),     // 18b) 战斗多行：冲突框尺寸
            typeof(ConflictRowsBoundsPatch),   // 18c) 战斗多行：冲突框本体
            typeof(AdvancedGreenhouseGrowthPatch),   // 19) 高级温室：把"高级温室 × 6 个种子 → 每颗 1 份、种子不消耗"的生长配方追加进原版生长蓝图
            typeof(AdvancedGreenhouseOutputPatch),   // 19b) 高级温室：产物送进旁边的容器（配套箱子/炖锅），没地方放就交回原版
            typeof(GreenhouseStackingPatch),  // 19c) 允许普通温室互相叠放（"6 张温室 → 高级温室"配方的必要前提）
            typeof(HotpotCapacityPatch),     // 20) 火锅（炖锅）的食物容量改成最大值（原版 50，参考 Stacks n Capacity）
            typeof(BreedingPenKeepAnimalsPatch), // 21) 动物留在饲养栏：繁殖后亲代不弹出去（参考 Stacks n Capacity）
            typeof(ResourceChestNoCardCountPatch), // 22) 资源箱内的资源不按数量计入卡牌上限（参考 Stacks n Capacity）
            typeof(ResourceChestMaxCapacityPatch), // 23) 资源箱存储上限 10000（原版 100）
            typeof(ResourceChestMoreTypesPatch),  // 23b) 资源箱能收的卡牌类型扩到磁力箱白名单（材料+食物+装备）
            typeof(ChestCapacityPatch),           // 23c) 硬币箱/贝壳宝箱存币上限 10000（原版 100）
            typeof(CreditcardCapacityPatch),      // 23d) 信用卡存钞上限 100000（原版 1000）
            typeof(FeedingPriorityPatch),         // 24) 月末吃饭：先吃炖锅的，不吃叠在高级温室上的（接替 Cutscenes.GetFoodToUseUp）
            typeof(DebugScreenTermInjectionPatch), // 25) K+O+F1 调试面板汉化：把缺失的 label_debug_* 中文词条注入当前语言词表
            typeof(DebugScreenRawTextPatch),      // 25b) K+O+F1 调试面板汉化：硬编码英文按钮文字按表替换（仅简体中文生效）
            typeof(QuickBuyPackPatch),            // 26) Ctrl+点击购买箱快速购买卡包（散币优先，不够从箱子/信用卡扣，0.5.16）
            typeof(TooltipPositionPatch),         // 27) 悬浮提示永远显示在鼠标右上方（原版长文案会翻到左边且截断，0.5.18）
            typeof(AdvancedMarketStackPatch),     // 28) 高级集市：放行不同种类可卖卡摞在集市上（到点整摞一起卖，0.5.19）
            typeof(ContainerCraftingStackPatch),  // 29) 磁力容器：放行"城市资源容器叠到铁条/铜条/科学上" + "受教育工人叠到资源容器上"（0.6.0 / 0.6.1）
            typeof(ContainerWorkerAbsorbGuardPatch), // 30) 磁力容器：绝不让资源容器把叠在上面的受教育工人吞掉（0.6.1）
            typeof(AdvancedRecyclingCenterStackPatch), // 31) 高级回收中心：放行"回收中心叠回收中心"，否则 5 张料的配方堆搭不出来（0.6.2）
            typeof(AdvancedRecyclingCenterBlueprintPatch), // 32) 高级回收中心：堆必须"断电 + 取出工人"才读条；判过之后跳过原版工人/能源闸（0.6.3 / 0.6.4）
            typeof(ClaimedCardSendItPatch), // 33) 已被磁力卡/管道认领的卡不许被 SendIt 改掉飞行方向（修信用卡吐钱往左飞，0.6.4）
        };

        /// <summary>
        /// 本 mod 的卡与它们各自登记进哪个卡包。卡总开关就是靠这份表重建抽卡池的
        /// （关掉 → 从卡池里摘掉；重新打开 → 原样放回去）。
        /// </summary>
        private static readonly (string CardId, SetCardBagType BagType)[] CardBagEntries =
        {
            ("stacklandspro_blueprint_magnet_chest", SetCardBagType.AdvancedBuildingIdea),
            ("stacklandspro_blueprint_magnet_chest", SetCardBagType.Cities_Ideas),
            ("stacklandspro_blueprint_super_warehouse", SetCardBagType.AdvancedBuildingIdea),
            ("stacklandspro_blueprint_coin_magnet_chest", SetCardBagType.AdvancedBuildingIdea),
            ("stacklandspro_blueprint_coin_magnet_chest", SetCardBagType.Cities_Ideas),
            ("stacklandspro_blueprint_shell_magnet_chest", SetCardBagType.AdvancedBuildingIdea),
            ("stacklandspro_blueprint_shell_magnet_chest", SetCardBagType.Cities_Ideas),
            // 磁力信用卡 0.6.2 起改成城市专属（卡与创意都标 CardUpdateType.Cities），所以只进城市卡包。
            ("stacklandspro_blueprint_magnet_creditcard", SetCardBagType.Cities_Ideas),
            ("stacklandspro_blueprint_advanced_greenhouse", SetCardBagType.AdvancedBuildingIdea),
            ("stacklandspro_blueprint_advanced_greenhouse", SetCardBagType.Cities_Ideas),
            ("stacklandspro_blueprint_magic_collector", SetCardBagType.AdvancedBuildingIdea),
            ("stacklandspro_blueprint_magic_collector", SetCardBagType.Cities_Ideas),
            ("stacklandspro_blueprint_advanced_market", SetCardBagType.AdvancedBuildingIdea),
            ("stacklandspro_blueprint_advanced_market", SetCardBagType.Cities_Ideas),
            // 磁力容器是城市专属卡（CardUpdateType.Cities），只进城市的创意卡包——
            // 主大陆的主意列表只显示 != Cities 的知识，塞进 AdvancedBuildingIdea 也看不到。
            ("stacklandspro_blueprint_magnetic_container", SetCardBagType.Cities_Ideas),
            // 高级回收中心同样是城市专属卡（0.6.2）。
            ("stacklandspro_blueprint_advanced_recycling_center", SetCardBagType.Cities_Ideas),
        };

        public override void Ready()
        {
            Logger.Log("Stacklands Pro Max is ready!");

            // 把每张卡的创意放进卡包，这样正常玩的时候能抽到它们。
            // 逐个 try/catch：没装 Cities DLC 时 Cities_Ideas 卡包可能不存在，
            // 不能让一次失败拖垮整个 Ready()（和 ApplyPatches 的防御方式一致）。
            GameDataLoader loader = WorldManager.instance.GameDataLoader;
            foreach ((string CardId, SetCardBagType BagType) entry in CardBagEntries)
            {
                try
                {
                    loader.AddCardToSetCardBag(entry.BagType, entry.CardId, 1);
                    // 记一笔，好让"卡总开关"能把它们从卡池里摘掉 / 放回去
                    ModConfig.RememberCardBag(entry.CardId, entry.BagType, 1);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning("could not add " + entry.CardId + " to " + entry.BagType + ": " + ex.Message);
                }
            }

            ModConfig.Init(this); // 注册游戏内设置界面的开关（必须在打补丁前，补丁会读这些开关）
            ModConfig.OnCardBagsReady(); // 按开关刷新一次抽卡池（关掉的卡立刻抽不到）

            // 高级温室：把"高级温室 × 6 个种子"的生长配方追加进原版 blueprint_growth。
            // 这里调一次、`BlueprintGrowth.Init` 后置再调一次（幂等）——那样无论生长蓝图是
            // 在 mod Ready 之前还是之后初始化，配方都会在（Init 里会清空重建，见 AdvancedGreenhouse 注释）。
            AdvancedGreenhouse.InjectGrowthSubprints();
            // 高级温室主意卡的图标：JSON 里借 `blueprint_greenhouse` 借不到（蓝图类卡在加载 mod 卡时还查不到），
            // 运行时把原版主意卡的图标复制过来（和磁力箱镜像原版外观同一套做法）。
            AdvancedGreenhouse.ApplyVanillaIdeaIcon();

            // 城市（工业革命 DLC）专属卡：把卡与创意卡标成 CardUpdateType.Cities，
            // 城市的合成配方（FindMatchingPrint）与城市的创意列表（UpdateIdeasLog）都靠这个类型过滤，
            // 详见 CitiesCardSupport.MarkAsCitiesCard 的注释。
            //   磁力容器（0.6.0）、高级回收中心（0.6.2）、磁力信用卡（0.6.2 起改为城市专属）
            MagneticContainer.ApplyCitiesCardType();
            CitiesCardSupport.MarkAsCitiesCard(AdvancedRecyclingCenter.CardId, AdvancedRecyclingCenter.BlueprintId);
            CitiesCardSupport.MarkAsCitiesCard("stacklandspro_magnet_creditcard", "stacklandspro_blueprint_magnet_creditcard");

            ApplyPatches();
        }

        /// <summary>逐个应用补丁：某一个失败（例如游戏更新改了方法名）不影响其它功能。</summary>
        private void ApplyPatches()
        {
            foreach (Type patchClass in PatchClasses)
            {
                try
                {
                    Harmony.PatchAll(patchClass);
                    Logger.Log("patched " + patchClass.Name);
                }
                catch (Exception ex)
                {
                    Logger.LogError("failed to patch " + patchClass.Name + ": " + ex.Message);
                }
            }
        }

        private void OnDestroy()
        {
            try
            {
                Harmony.UnpatchSelf();
            }
            catch (Exception ex)
            {
                Logger.LogError("failed to unpatch: " + ex.Message);
            }
        }

        /// <summary>
        /// 框选多张卡的覆盖层（选择框 + 已选卡的描边）。
        ///
        /// 画在 `OnGUI` 里而不是用游戏内对象，是因为它纯粹是"屏幕上的一层指示"：
        /// 原版没有现成的框选 UI 可复用（`CardSelectionBox` 之类的类不存在），
        /// 而 OnGUI 画几个矩形代价极低、也不需要额外的 prefab/贴图资源。
        /// （`Mod` 继承自 `UnityEngine.MonoBehaviour`，且 mod 对象常驻，所以这个回调会被持续调用。）
        /// </summary>
        private void OnGUI()
        {
            BoxSelect.DrawOverlay();
        }
    }

    /// <summary>
    /// 磁力箱（Magnet Chest）：自带磁铁的储物箱。
    ///
    /// 和原版储物箱（ResourceChest）+ 原版磁铁（ResourceMagnet）的关系：
    ///   - 存储方式对齐 ResourceChest：一个箱子只存一种材料，卡面显示数量，
    ///     点击取出 1 张（自己飞出来）；按住 Shift 点击＝拖出 10 张、按住 Ctrl 点击＝拖出 1 张，
    ///     这两种是挂在鼠标上、松开才落地；全部取空后自动清空类型、可重新设定。
    ///   - 吸附方式对齐原版磁铁的搬运机制 WorldManager.StackSendTo：目标卡会"飞"过来叠到箱子上，
    ///     再由 AbsorbStackedResources 吞掉并计数（原版 ResourceChest 也是这么吞的）。
    ///     区别是原版磁铁只吸"正在被传送带送出来"的卡，而这里会主动扫描周围散落的材料。
    ///
    /// 三条不会回吸的规则（"取出的材料不能被再次吸回去"）：
    ///   1) 刚从箱子里取出来的卡，会在 RemovalImmunitySeconds 秒内免疫（默认 600 秒 = 10 分钟）。
    ///      注意：吸附半径默认是 999（整块场地），所以规则 2 的"拿开"实际上没法触发——
    ///      场地内没有"拿开"可言。这两条是配套的：半径越大，越要靠时间免疫来防止
    ///      刚取出的材料被立刻吸回去。
    ///   2) 免疫期内把它拿到远离箱子的地方（超过 PullRadius * ImmunityKeepAwayFactor），免疫立即解除。
    ///   3) 玩家正在拖拽的卡、以及已经有堆叠（有父卡或自身带子卡）的卡，永远不吸。
    ///      所以把材料堆到别的卡上就绝对安全。
    ///
    /// 类型设定：往箱子上放第一张材料即设定类型（原版磁铁也是靠"叠一张卡上去"来设定）。
    ///
    /// 磁力箱本身永远不是材料：既不能被吸进、也不能叠到另一个磁力箱上（0.2.2 起）。
    /// 早期版本允许互相叠放，结果箱子会把另一个磁力箱当材料吞掉销毁，并把自己的存储类型
    /// 设成"磁力箱"，从此满场吸别的磁力箱——所以现在有 4 处防线 + 1 处存档自动修复
    /// （见 RepairBrokenStateFromOlderVersion）。
    /// </summary>
    public class MagnetChest : CardData
    {
        // 这两项会被存档保存（ExtraData 的 key 加了 mod 前缀，避免和原版箱子冲突）
        [ExtraData("stacklandspro_resource_count")]
        [HideInInspector]
        public int ResourceCount;

        [ExtraData("stacklandspro_resource_id")]
        [HideInInspector]
        public string HeldCardId = "";

        // ---- 以下参数可在 Cards/magnet_chest.json 里用 "_字段名" 覆盖，方便调参 ----
        public int MaxResourceCount = int.MaxValue;   // 最多存多少（默认无限）
        public float PullRadius = 999f;               // 吸附半径：999 = 整块场地（原版箱子自动收货半径只有 2），设 0 等于关掉吸附
        public float PullInterval = 0.1f;             // 两次吸附之间的最短间隔（秒）：0.1 = 每秒最多吸 10 张
        public float RemovalImmunitySeconds = 600f;   // 取出后的免疫时长（秒）：600 = 10 分钟
        public float ImmunityKeepAwayFactor = 1.6f;   // 拿开到「半径 x 该系数」之外就解除免疫
        public float OutputInterval = 10f;            // "运输输出"的间隔（秒）：和原版储物箱一样是 10 秒

        /// <summary>"运输输出"计时器显示的状态文字（本地化 term，见 localization.tsv）。</summary>
        [Term]
        public string OutputStatusTerm = "stacklandspro_magnet_chest_output_status";

        /// <summary>外观镜像哪张原版卡（子类可换：磁力容器镜像城市的资源容器 storage_container）。</summary>
        protected virtual string VanillaLookCardId => "resource_chest";

        /// <summary>设定存储类型之后卡面显示的名字（term 里用 [resource] 占位资源名）。</summary>
        protected virtual string NameOverrideTerm => "stacklandspro_magnet_chest_name_override";

        /// <summary>鼠标悬停时显示的详细描述（term 里用 [resource] / [amount] / [max_amount]）。</summary>
        protected virtual string DescriptionLongTerm => "stacklandspro_magnet_chest_description_long";

        private float nextPullTime;

        // 原版储物箱预制体上借来的外观数据（角标精灵 + 配色），只借一次
        private Sprite specialIconSprite;
        private bool triedLoadingVanillaLook;

        private class RemovalImmunity
        {
            public GameCard Chest;
            public float ExpiresAt;
            public float KeepAwayRadius;
        }

        private static readonly Dictionary<GameCard, RemovalImmunity> RecentlyRemoved =
            new Dictionary<GameCard, RemovalImmunity>();

        private static float nextCleanupTime;

        // ------------------------------------------------------------------
        //  堆叠规则
        // ------------------------------------------------------------------

        protected override bool CanHaveCard(CardData otherCard)
        {
            // 磁力箱绝不能被放进（也不能叠在）另一个磁力箱上。
            // 早期版本这里返回 true，导致箱子会把另一个磁力箱当成材料吞掉销毁，
            // 并且把自己的 HeldCardId 设成"磁力箱"的 id，从此满场吸别的磁力箱。
            if (otherCard is MagnetChest)
            {
                return false;
            }
            if (!IsStorable(otherCard))
            {
                return false;
            }
            // 还没设定类型时接受任何材料（用来设定类型）；设定之后只接受同一种材料
            return string.IsNullOrEmpty(HeldCardId) || otherCard.Id == HeldCardId;
        }

        /// <summary>
        /// 能被吸进箱子/放进箱子的东西：**材料（含便便，0.5.15）+ 食物 + 装备**。
        /// 材料部分和原版储物箱（ResourceChest）一致——原版 `ResourceChest.CanHaveCard` 本来就收
        /// `CardType.Resources` 的便便（只在 Cities 场地排除便便/铀），0.5.8~0.5.14 我们曾在这里
        /// 额外把便便挡掉（"交给原版箱子"），0.5.15 按用户需求「磁力箱支持放入粪便」放行。
        /// 货币（金币/贝壳/纸币）仍然不收。
        /// 子类（硬币磁力箱）会重写它来换一份白名单。
        /// </summary>
        protected virtual bool IsStorable(CardData card)
        {
            if (card == null || card is MagnetChest)
            {
                return false;
            }
            if (card.MyCardType != CardType.Resources
                && card.MyCardType != CardType.Food
                && card.MyCardType != CardType.Equipable)
            {
                return false;
            }
            if (card.Id == "gold" || card.Id == "shell")
            {
                return false; // 货币交给原版箱子；便便 0.5.15 起放行（原版资源箱本来就收，见下）
            }
            if (card is Dollar)
            {
                return false; // 纸币同样是货币
            }
            return true;
        }

        /// <summary>
        /// 这个箱子是否愿意接收某张卡——给 WorldManager.TrySendToMagnet 的补丁用。
        ///
        /// 原版磁铁就是在 TrySendToMagnet 这条管线里把"刚产出、正在被送出"的资源直接截走的，
        /// 磁力箱走同一个入口，效果就是**资源一产出就被吸走**，而不是先落到地上再被扫到。
        ///
        /// 这里允许接收一叠（产出常常是一叠）：入库时是逐张吞的，DestroyCard 会先把子卡释放出来，
        /// 不会连带销毁，所以不会丢材料。
        /// </summary>
        public virtual bool WantsCard(CardData card)
        {
            if (card == null || card is MagnetChest)
            {
                return false;
            }
            if (MyGameCard == null || MyGameCard.BeingDragged)
            {
                return false;
            }
            // 已穿在身上的装备不截胡（同 PullNearbyResources 的排除，0.5.9）：
            // 卡片正被"送出"时它理论上不该还穿着，但原版 Unequip 是先发事件后真正脱下的，
            // 保险起见在这里也挡一道。
            GameCard cardGameCard = card.MyGameCard;
            if (cardGameCard != null && (cardGameCard.IsEquipped || cardGameCard.EquipmentHolder != null))
            {
                return false;
            }
            if (string.IsNullOrEmpty(HeldCardId) || card.Id != HeldCardId)
            {
                return false; // 没设定类型 / 不是这个类型：不抢
            }
            if (ResourceCount >= MaxResourceCount)
            {
                return false; // 满了不抢，让它去原本该去的地方
            }
            if (MyGameCard.HasParent && !(MyGameCard.Parent.CardData is HeavyFoundation))
            {
                return false;
            }

            // 关键安全检查：StackSendTo 在目标收不下时会改调 SendIt()（材料直接消失），
            // 所以发送前必须按同样的条件确认一遍。
            GameCard landingTarget = MyGameCard.GetLeafCard();
            return landingTarget != null && landingTarget.CardData != null
                && landingTarget.CardData.CanHaveCardOnTop(card);
        }

        // ------------------------------------------------------------------
        //  每帧逻辑
        // ------------------------------------------------------------------

        public override void UpdateCard()
        {
            // 0.5.18 起上限跟随设置里的「磁力箱容量」输入框（JSON 里的 _MaxResourceCount 只在新卡生成时
            // 生效，这里逐帧覆盖才能对已有实例即时生效）。⚠️ 不要把 ResourceCount 往下钳到上限：
            // 旧存档里已超限的材料会被吞掉——超限只是不再吸新的，已有的照常保留。
            MaxResourceCount = ModConfig.MagnetChestCapacity;
            RepairBrokenStateFromOlderVersion();
            CleanupImmunityTable();
            AbsorbStackedResources();
            PullNearbyResources();
            UpdateOutputTimer();
            UpdateVisuals();
            base.UpdateCard();
        }

        // ------------------------------------------------------------------
        //  三路运输输出（照原版储物箱 ResourceChest 的"运输输出"机制）
        // ------------------------------------------------------------------

        /// <summary>
        /// **有状态条（计时器）时也允许卡叠上来**——必须返回 true，否则"运输输出"会把箱子废掉。
        ///
        /// 为什么必须显式写：
        ///   `GameCard.Bounce()` 落地时有一道判定
        ///       if (cardWithStatusInStack != null && !cardWithStatusInStack.CardData.CanHaveCardsWhileHasStatus())
        ///           return;      // 不吸附
        ///   而基类 `CardData.CanHaveCardsWhileHasStatus()` 返回 **false**。
        ///   原版 `ResourceChest`（它自己也有"运输输出"计时器）和 `FoodWarehouse` 都显式重写成 true；
        ///   `ResourceMagnet` / `Chest` 没有，是因为它们从来没有计时器，所以不需要。
        ///   磁力箱一旦开始"运输输出"就有了带状态条的计时器 → 不快照原版储物箱放行，
        ///   所有飞过来的材料都会在箱子上弹开、进不去（0.5.0 修过一次的真实 bug）。
        /// </summary>
        public override bool CanHaveCardsWhileHasStatus()
        {
            return true;
        }

        /// <summary>
        /// 更新"运输输出"计时器：只要**任何一路**运输输出连接到了管道、且箱里还有东西，就跑 10 秒计时器，
        /// 到点后由 <see cref="OutputCard"/> 给每一路已连接的输出各发一张卡。
        ///
        /// 原版储物箱只认**最后一个**输出连接器（`GetOutputConnector` 循环里 result 被后一个覆盖），
        /// 也只用它发一张。用户要的是"支持三路输出、每次输出给已经连接的每一路各一张"，所以这里：
        ///   - 用 `HasConnectedTransportOutput()` 判断"有没有任何一路接上了"
        ///   - `OutputCard()` 里遍历**所有**已连接的输出，用原版 `StackSendCheckTarget(..., outputIndex: i)`
        ///     指定走第 i 路管道（原版按 outputIndex 选 `TrySendWithPipe` 里的连接器列表）
        /// </summary>
        private void UpdateOutputTimer()
        {
            if (string.IsNullOrEmpty(HeldCardId) || ResourceCount <= 0 || !HasConnectedTransportOutput())
            {
                MyGameCard.CancelTimer(GetActionId("OutputCard"));
                return;
            }
            MyGameCard.StartTimer(OutputInterval, OutputCard, SokLoc.Translate(OutputStatusTerm), GetActionId("OutputCard"));
        }

        /// <summary>本箱有没有任何一路"运输输出"接上了管道？</summary>
        private bool HasConnectedTransportOutput()
        {
            if (MyGameCard == null || MyGameCard.CardConnectorChildren == null)
            {
                return false;
            }
            for (int i = 0; i < MyGameCard.CardConnectorChildren.Count; i++)
            {
                CardConnector connector = MyGameCard.CardConnectorChildren[i];
                if (IsConnectedTransportOutput(connector))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsConnectedTransportOutput(CardConnector connector)
        {
            return connector != null
                && connector.ConnectionType == ConnectionType.Transport
                && connector.CardDirection == CardDirection.output
                && connector.ConnectedNode != null;
        }

        /// <summary>
        /// 计时器到点：给**每一路已连接的运输输出各发一张卡**（用户要求的行为）。
        ///
        /// 每一次都走原版 `WorldManager.StackSendCheckTarget`，并用 outputIndex 指定第 i 路管道，
        /// 所以不会有"一张卡被随机分到某一路"的偏差；三种输出之间互不影响。
        /// 发出去的卡会登记"刚取出"免疫，免得被自己（或别的磁力箱）立刻吸回去。
        /// </summary>
        [TimedAction("stacklandspro_magnet_output")]
        public void OutputCard()
        {
            if (string.IsNullOrEmpty(HeldCardId) || ResourceCount <= 0 || MyGameCard == null
                || MyGameCard.CardConnectorChildren == null)
            {
                return;
            }

            for (int i = 0; i < MyGameCard.CardConnectorChildren.Count; i++)
            {
                if (ResourceCount <= 0)
                {
                    break;
                }
                CardConnector connector = MyGameCard.CardConnectorChildren[i];
                if (!IsConnectedTransportOutput(connector))
                {
                    continue;
                }

                CardData output = WorldManager.instance.CreateCard(
                    Position, HeldCardId, faceUp: true, checkAddToStack: false);
                if (output == null || output.MyGameCard == null)
                {
                    continue;
                }

                // 先登记取出免疫，再让它飞走：原版 StackSendCheckTarget 会立刻给它设 Velocity，
                // 而我们的吸附本来就会跳过"在飞"和"免疫期内"的卡，这道保险是为了防止它落地瞬间被吸回。
                MarkAsRecentlyRemoved(output.MyGameCard);
                WorldManager.instance.StackSendCheckTarget(
                    MyGameCard, output.MyGameCard, OutputDir, null, sendToChest: true, outputIndex: i);
                ResourceCount--;
            }

            if (ResourceCount <= 0)
            {
                HeldCardId = null; // 取空后清空类型（和点击取出、原版储物箱的语义一致）
            }
        }

        /// <summary>
        /// 修复 0.2.1 及更早版本留下的坏状态：那时磁力箱可以互相叠放，箱子会把另一个磁力箱
        /// 当成材料吞掉（销毁并计数），并把 HeldCardId 设成磁力箱自己的 id。
        /// 这样的箱子会满场吸别的磁力箱，所以这里检测到就清掉类型，并把被吞掉的箱子如数还回来。
        /// 判断条件只可能是那个 bug 造成的（正常玩法下 HeldCardId 永远不等于自己的 id），
        /// 所以对没中过这个 bug 的存档不会产生任何影响。
        /// </summary>
        private void RepairBrokenStateFromOlderVersion()
        {
            if (string.IsNullOrEmpty(HeldCardId) || HeldCardId != Id)
            {
                return;
            }

            int refund = Mathf.Clamp(ResourceCount, 0, MaxResourceCount);
            HeldCardId = null;
            ResourceCount = 0;

            if (refund > 0)
            {
                WorldManager.instance.CreateCardStack(
                    Position + Vector3.up * 0.2f, refund, Id, checkAddToStack: false);
            }
            Debug.Log("[StacklandsProMax] 这个磁力箱曾被旧版本的 bug 当成磁力箱容器，"
                + "已清空类型并归还被吞掉的 " + refund + " 个磁力箱。");
        }

        /// <summary>
        /// 把已经叠到箱子上的材料吞掉并计数（对齐原版 ResourceChest 的逻辑）。
        /// 吸附过来的卡也是走这里入库的。
        /// </summary>
        private void AbsorbStackedResources()
        {
            if (MyGameCard.HasParent && !(MyGameCard.Parent.CardData is HeavyFoundation))
            {
                return; // 被压在别的卡下面时不处理
            }

            foreach (GameCard childCard in MyGameCard.GetChildCards())
            {
                if (childCard == null || childCard.CardData == null)
                {
                    continue;
                }
                // 磁力箱永远不进箱子：既不销毁它，也不让它决定箱子存什么。
                // 正常玩法下根本走不到这里（CanHaveCard 已拒绝），这是给旧存档/其它 mod
                // 造成的异常堆叠兜底。推出去而不是留在箱顶上，避免出现"箱子上压着箱子"。
                if (childCard.CardData is MagnetChest)
                {
                    childCard.RemoveFromParent();
                    break;
                }
                // 第一张放上来的材料决定这个箱子存什么
                if (string.IsNullOrEmpty(HeldCardId))
                {
                    HeldCardId = childCard.CardData.Id;
                }
                if (childCard.CardData.Id != HeldCardId)
                {
                    continue;
                }
                if (ResourceCount >= MaxResourceCount)
                {
                    childCard.RemoveFromParent(); // 满了就把多出来的弹出去
                    break;
                }
                childCard.DestroyCard(spawnSmoke: true);
                ResourceCount++;
            }
        }

        /// <summary>扫描周围散落的同类材料，用原版磁铁的方式把它们"吸"过来。</summary>
        private void PullNearbyResources()
        {
            if (string.IsNullOrEmpty(HeldCardId)) return;         // 还没设定类型：先放一张材料上去
            if (ResourceCount >= MaxResourceCount) return;        // 满了就不吸
            if (MyGameCard.BeingDragged) return;                  // 玩家正在拖这个箱子
            if (MyGameCard.InConflict) return;                    // 战斗中不吸（此时资源卡无法落到箱子上）
            if (MyGameCard.HasParent && !(MyGameCard.Parent.CardData is HeavyFoundation)) return;
            if (Time.time < nextPullTime) return;                 // 控制吸附频率

            GameCard best = null;
            float bestSqrDistance = PullRadius * PullRadius;
            Vector3 myPosition = MyGameCard.transform.position;

            foreach (GameCard candidate in WorldManager.instance.AllCards)
            {
                if (candidate == null || candidate == MyGameCard) continue;
                if (candidate.CardData == null || candidate.CardData is MagnetChest) continue; // 永不吸磁力箱
                if (candidate.CardData.Id != HeldCardId) continue;
                if (candidate.MyBoard != MyGameCard.MyBoard) continue;
                if (candidate.HasParent || candidate.HasChild) continue;   // 只吸完全散落的单张，不拆别人的堆
                // ⚠️ 已穿在身上（村民/战斗单位）的装备必须排除：原版 GameCard.Equip() 会把装备卡
                // RemoveFromStack() 再挂进持有者的 EquipmentChildren——穿身上时 Parent == null，
                // 上面那道 HasParent/HasChild 过滤拦不住它，不挡的话村民身上的斧头会被整场吸走（0.5.9 实测报告）。
                if (candidate.IsEquipped || candidate.EquipmentHolder != null) continue;
                if (candidate.BeingDragged) continue;                      // 玩家手上的绝对不抢
                if (!candidate.gameObject.activeInHierarchy) continue;
                if (candidate.Velocity.HasValue || candidate.BounceTarget != null) continue; // 已经在飞了
                if (IsRecentlyRemoved(candidate)) continue;                // 刚从这个箱子取出来的

                Vector3 offset = candidate.transform.position - myPosition;
                offset.y = 0f;
                float sqrDistance = offset.sqrMagnitude;
                if (sqrDistance > bestSqrDistance) continue;

                bestSqrDistance = sqrDistance;
                best = candidate;
            }

            if (best == null) return;

            // 安全检查：原版 StackSendTo 在目标无法接收时会改调 SendIt()，那是把卡片直接送出棋盘
            // （材料就没了）。所以先按同样的判定条件确认一遍，确认不了就跳过，等下一轮。
            GameCard landingTarget = MyGameCard.GetLeafCard();
            if (landingTarget == null || landingTarget.CardData == null
                || !landingTarget.CardData.CanHaveCardOnTop(best.CardData))
            {
                // 送不出去通常只是"箱子顶上刚好还压着一张没吞掉的材料卡"，下一轮就好了，
                // 所以按正常节奏重试，不在这里罚一个长等待。
                nextPullTime = Time.time + Mathf.Max(0.05f, PullInterval);
                return;
            }

            nextPullTime = Time.time + Mathf.Max(0.05f, PullInterval);
            // 搬运方式对齐原版磁铁 WorldManager.StackSendTo：给卡片设 BounceTarget + Velocity，
            // 它会飞过来叠到箱子上，落地后由 AbsorbStackedResources 吞掉计数。
            // 弧线用的就是原版参数（水平 = 距离 x4，高度 7）——之前试过的"低平快"弧线观感不好，已改回。
            WorldManager.instance.StackSendTo(best, MyGameCard);
        }

        /// <summary>只负责显示：外观照原版储物箱、右上角数量角标、卡面图标。</summary>
        private void UpdateVisuals()
        {
            ApplyVanillaChestLook();

            MyGameCard.SpecialValue = ResourceCount;

            if (MyGameCard.SpecialIcon != null && specialIconSprite != null)
            {
                MyGameCard.SpecialIcon.sprite = specialIconSprite;
            }

            // 和原版储物箱完全一致：卡面图标显示当前存的材料（取空则显示空图标）
            CardData storedPrefab = string.IsNullOrEmpty(HeldCardId) ? null : WorldManager.instance.GetCardPrefab(HeldCardId);
            Icon = (storedPrefab != null) ? storedPrefab.Icon : SpriteManager.instance.EmptyTexture;
            MyGameCard.UpdateIcon();
        }

        /// <summary>
        /// 外观完全照原版储物箱（ResourceChest）：配色、是否使用自定义配色、右上角角标精灵，
        /// 全部直接复制原版预制体上的值。所以卡牌颜色、以及"里面资源图标"的呈现方式都和原版储物箱一致。
        ///
        /// 为什么要运行时复制而不是写死在 JSON 里：
        ///   原版是按 "MyCardType == Structures &amp;&amp; IsBuilding" 在 Building / Structure 两套调色板之间选
        ///   （ColorManager.GetPaletteForCard），而 IsBuilding 存在预制体里、代码里查不到值。
        ///   直接镜像原版储物箱的这个字段，就不需要猜它是哪种。
        /// 另外配色只在卡片创建时算一次（GameCard.UpdateCardPalette），所以改完要手动刷新一次，
        /// 否则要等卡片被重建才生效。
        /// </summary>
        private void ApplyVanillaChestLook()
        {
            if (triedLoadingVanillaLook)
            {
                return;
            }
            triedLoadingVanillaLook = true;

            // 镜像哪张原版卡由子类决定：磁力箱→原版储物箱 resource_chest；磁力容器→城市资源容器 storage_container
            CardData vanillaChest = WorldManager.instance.GetCardPrefab(VanillaLookCardId);
            if (vanillaChest == null)
            {
                Debug.LogWarning("[StacklandsProMax] 找不到原版容器 " + VanillaLookCardId + "，本卡将使用默认外观。");
                return;
            }

            IsBuilding = vanillaChest.IsBuilding;
            HasUniquePalette = vanillaChest.HasUniquePalette;
            MyPalette = vanillaChest.MyPalette;
            if (vanillaChest is ResourceChest chest)
            {
                specialIconSprite = chest.SpecialIcon;
            }

            if (MyGameCard != null)
            {
                MyGameCard.UpdateCardPalette();
            }
        }

        public override void UpdateCardText()
        {
            nameOverride = null;
            descriptionOverride = null;

            if (string.IsNullOrEmpty(HeldCardId))
            {
                return;
            }
            CardData storedPrefab = WorldManager.instance.GameDataLoader.GetCardFromId(HeldCardId);
            if (storedPrefab == null)
            {
                return;
            }
            nameOverride = SokLoc.Translate(NameOverrideTerm,
                LocParam.Create("resource", storedPrefab.Name));
            if (MyGameCard.IsHovered)
            {
                descriptionOverride = SokLoc.Translate(DescriptionLongTerm,
                    LocParam.Create("resource", storedPrefab.Name),
                    LocParam.Create("amount", ResourceCount.ToString()),
                    LocParam.Create("max_amount", MaxResourceCount.ToString()));
            }
        }

        // ------------------------------------------------------------------
        //  取出材料
        // ------------------------------------------------------------------

        public override void Clicked()
        {
            if (!IsDamaged && ResourceCount > 0)
            {
                int count = 1;
                if (InputController.instance.GetKey(Key.LeftShift) || InputController.instance.GetKey(Key.RightShift))
                {
                    count = 5;
                }
                RemoveResources(count);

                if (ResourceCount <= 0)
                {
                    HeldCardId = null; // 取空后清空类型，可以拿去装别的材料
                }
            }
            base.Clicked();
        }
        /// <summary>
        /// 取出一叠材料、**准备挂到鼠标上**（Shift 拖出 10 张 / Ctrl 拖出 1 张；
        /// 由 `ChestDragOutPatch` 在玩家按下左键那一帧调用）。
        ///
        /// 与 `RemoveResources` 的唯一区别：这里**不调用 `WorldManager.StackSend`**。因为
        ///   - `StackSend` 是"让它自己飞出去"，会给这叠设上速度与飞行弧线，而它马上就要被鼠标拖着走；
        ///   - `StackSend` 第一步是 `WorldManager.TrySendToMagnet`，场上只要有能收下它的磁力箱
        ///     （**包括发出这张卡的箱子自己**）就会被改道送进箱子 → 表现成"拖出来一下又回去了"。
        /// 仍然登记"取出免疫"（和普通点击取出一致），这样玩家把这叠随手丢在箱子边上也不会被立刻吸回去。
        /// </summary>
        public GameCard TakeOutStack(int count)
        {
            count = Mathf.Min(count, ResourceCount);
            if (count <= 0 || string.IsNullOrEmpty(HeldCardId))
            {
                return null;
            }

            GameCard stack = WorldManager.instance.CreateCardStack(
                Position + Vector3.up * 0.2f, count, HeldCardId, checkAddToStack: false);
            if (stack == null)
            {
                return null;
            }

            GameCard root = stack.GetRootCard();
            ResourceCount -= count;
            if (ResourceCount <= 0)
            {
                // 取空后清空类型——和点击取出（Clicked()）同一个语义。
                // ⚠️ 漏了这句就是 0.5.9 修的 bug：Ctrl/Shift 拖出最后一件后 HeldCardId 残留，
                // 箱名/卡面图标还显示原物品，且 CanHaveCard 只认同种卡 → 别的放不进去。
                // 拖出的这叠要是又被放回箱子，AbsorbStackedResources 会照常重新设定类型。
                HeldCardId = null;
            }
            MarkAsRecentlyRemoved(root);
            return root;
        }

        /// <summary>取出一叠材料，并把它们标记为"刚取出"，避免被自己（或别的磁力箱）立刻吸回去。</summary>
        public GameCard RemoveResources(int count)
        {
            count = Mathf.Min(count, ResourceCount);
            if (count <= 0 || string.IsNullOrEmpty(HeldCardId))
            {
                return null;
            }

            GameCard stack = WorldManager.instance.CreateCardStack(
                Position + Vector3.up * 0.2f, count, HeldCardId, checkAddToStack: false);
            if (stack == null)
            {
                return null;
            }

            GameCard root = stack.GetRootCard();
            WorldManager.instance.StackSend(root, Vector3.right);
            ResourceCount -= count;
            MarkAsRecentlyRemoved(root);
            return root;
        }

        // ------------------------------------------------------------------
        //  取出后的免疫
        // ------------------------------------------------------------------

        protected virtual void MarkAsRecentlyRemoved(GameCard stackRoot)
        {
            if (stackRoot == null)
            {
                return;
            }
            float keepAwayRadius = PullRadius * Mathf.Max(1f, ImmunityKeepAwayFactor);
            float expiresAt = Time.time + Mathf.Max(1f, RemovalImmunitySeconds);

            MarkOne(stackRoot, keepAwayRadius, expiresAt);
            foreach (GameCard card in stackRoot.GetAllCardsInStack())
            {
                MarkOne(card, keepAwayRadius, expiresAt);
            }
            // 补一层保险：如果取出的张数超过一个堆的上限，挂在根卡下面的也一起标记
            foreach (GameCard card in stackRoot.GetChildCards())
            {
                MarkOne(card, keepAwayRadius, expiresAt);
            }
        }

        private void MarkOne(GameCard card, float keepAwayRadius, float expiresAt)
        {
            MarkRecentlyRemovedFrom(card, MyGameCard, keepAwayRadius, expiresAt);
        }

        /// <summary>
        /// 把一张卡登记成"刚从箱子取出来"（磁力箱和硬币磁力箱共用同一张免疫表）。
        /// </summary>
        internal static void MarkRecentlyRemovedFrom(GameCard card, GameCard chest, float keepAwayRadius,
            float expiresAt)
        {
            if (card == null || chest == null)
            {
                return;
            }
            RecentlyRemoved[card] = new RemovalImmunity
            {
                Chest = chest,
                ExpiresAt = expiresAt,
                KeepAwayRadius = keepAwayRadius
            };
        }

        /// <summary>这张卡是不是"刚从箱子里拿出来、还不该被吸回去"？（硬币磁力箱也用它判断）</summary>
        internal static bool IsRecentlyRemoved(GameCard card)
        {
            if (card == null)
            {
                // Unity 判空：卡已被销毁（记录留给定期清理去删）
                return false;
            }
            RemovalImmunity immunity;
            if (!RecentlyRemoved.TryGetValue(card, out immunity))
            {
                return false;
            }
            if (immunity.Chest == null || Time.time >= immunity.ExpiresAt)
            {
                RecentlyRemoved.Remove(card);
                return false;
            }

            Vector3 offset = card.transform.position - immunity.Chest.transform.position;
            offset.y = 0f;
            if (offset.magnitude > immunity.KeepAwayRadius)
            {
                RecentlyRemoved.Remove(card); // 玩家把它拿开了，恢复普通材料
                return false;
            }
            return true;
        }

        /// <summary>清理免疫表（磁力箱每帧调一次，硬币磁力箱也借它用）。</summary>
        internal static void CleanupImmunity()
        {
            CleanupImmunityTable();
        }

        /// <summary>定期清理已被摧毁/已过期的记录，避免字典越攒越大。</summary>
        private static void CleanupImmunityTable()
        {
            if (Time.time < nextCleanupTime)
            {
                return;
            }
            nextCleanupTime = Time.time + 5f;

            List<GameCard> stale = null;
            foreach (KeyValuePair<GameCard, RemovalImmunity> pair in RecentlyRemoved)
            {
                if (pair.Key == null || Time.time >= pair.Value.ExpiresAt)
                {
                    if (stale == null)
                    {
                        stale = new List<GameCard>();
                    }
                    stale.Add(pair.Key);
                }
            }
            if (stale == null)
            {
                return;
            }
            foreach (GameCard card in stale)
            {
                RecentlyRemoved.Remove(card);
            }
        }
    }

    /// <summary>
    /// 磁力容器（Magnetic Container）：城市场地（工业革命 DLC）版的「资源容器 + 磁铁」。
    ///
    /// 需求原话：「在工业革命DLC，也就是 city 这个地图，加一种类卡片叫磁力容器，主意为：铁条，铜条，科学，
    /// 资源容器，受教育工人。功能类似于主大陆的磁力资源箱，可以自动吸取卡片，一些操作快捷键也都参考磁力资源箱就行」。
    ///
    /// 与磁力箱（MagnetChest）的差别只有两处，其余全部继承（吸附扫描 / TrySendToMagnet 截胡 / 取出 10 分钟免疫 /
    /// Shift 拖出 10 张、Ctrl 拖出 1 张 / F 键送箱 / 取出往边上掉 / 三路运输输出 / 卡面数量角标）：
    ///   1) **收卡范围照原版资源容器（ResourceChest）**，不是磁力箱的"材料 + 食物 + 装备"白名单：
    ///      只收 `CardType.Resources`（城市里排除便便 `poop` 与铀 `quantum_entangled_uranium`），
    ///      另保留原版那条「城市里 `FoodValue ≤ 0` 的食物放行」的特例；不收正常食物、装备、货币、建筑。
    ///   2) **外观镜像原版城市资源容器 `storage_container`**（配色 / 右上角标 / 空图标），名字与长描述用自己的 term。
    ///
    /// 额外的一道保险见 <see cref="WantsCard"/>：正在飞行的卡不抢。
    /// </summary>
    public class MagneticContainer : MagnetChest
    {
        public const string CardId = "stacklandspro_magnetic_container";
        public const string BlueprintId = "stacklandspro_blueprint_magnetic_container";

        /// <summary>外观镜像城市的原版资源容器（不是主大陆的 resource_chest）。</summary>
        protected override string VanillaLookCardId => "storage_container";

        protected override string NameOverrideTerm => "stacklandspro_magnetic_container_name_override";

        protected override string DescriptionLongTerm => "stacklandspro_magnetic_container_description_long";

        private static bool IsCitiesBoard
        {
            get
            {
                return WorldManager.instance != null
                    && WorldManager.instance.CurrentBoard != null
                    && WorldManager.instance.CurrentBoard.Id == "cities";
            }
        }

        /// <summary>
        /// 收卡口径＝原版 `ResourceChest.CanHaveCard` 的白名单部分（逐条对齐，含城市两条例外）。
        /// 父类的磁力箱白名单（材料 + 食物 + 装备）在这里被收窄：食物只认"填不饱肚子"的那种，装备一律不收。
        /// </summary>
        protected override bool IsStorable(CardData card)
        {
            if (card == null || card is MagnetChest)
            {
                return false;
            }
            bool cities = IsCitiesBoard;
            if (card is Food food && food.FoodValue <= 0 && cities)
            {
                return true; // 原版特例：城市里 FoodValue <= 0 的食物也算"可存的资源"
            }
            if (card.MyCardType != CardType.Resources)
            {
                return false; // 食物 / 装备 / 建筑 / 概念卡一律不收
            }
            if (card.Id == "gold" || card.Id == "shell")
            {
                return false; // 货币交给原版箱子
            }
            if (card is Dollar)
            {
                return false;
            }
            if (cities && (card.Id == "poop" || card.Id == "quantum_entangled_uranium"))
            {
                return false; // 原版在城市场地故意不收这两样（污染产物不许囤）
            }
            return true;
        }

        /// <summary>
        /// 截胡入口再加一道保险：**已经在飞的卡不抢**。
        ///
        /// 为什么需要：管道（Transport 连接器）从 A 的输出送到 B 的输入时，原版是"管道优先"——
        /// `StackSendCheckTarget` 先试 `TrySendWithPipe`，管道命中后 `StackSendTo` 会把磁力先设的落点覆盖掉，
        /// 所以产线的料最终不会被吸走。但那依赖"原版先磁力、后管道覆盖"这个顺序；把"在飞的卡"直接挡掉，
        /// 语义就变成"磁力容器永远不碰正在运输的卡"，不再依赖原版送卡顺序。
        /// </summary>
        public override bool WantsCard(CardData card)
        {
            GameCard gameCard = card != null ? card.MyGameCard : null;
            if (gameCard != null && (gameCard.Velocity.HasValue || gameCard.BounceTarget != null))
            {
                return false;
            }
            return base.WantsCard(card);
        }

        /// <summary>
        /// 把磁力容器与它的创意卡标成 `CardUpdateType.Cities`
        /// （理由见 <see cref="CitiesCardSupport.MarkAsCitiesCard"/>）。
        /// </summary>
        internal static void ApplyCitiesCardType()
        {
            CitiesCardSupport.MarkAsCitiesCard(CardId, BlueprintId);
        }
    }

    /// <summary>
    /// 把一张 mod 卡与它的创意卡标成 `CardUpdateType.Cities`——**城市场地专属卡能用的关键**：
    ///
    ///   - `CardData.FindMatchingPrint`（反编译核对）在城市场地会跳过所有
    ///     `CardUpdateType != Cities` 的蓝图，而 mod 卡/蓝图加载时被统一设成 `CardUpdateType.Mod`
    ///     （`LoadModCard` / `LoadModBlueprint`）→ 不标就永远不出合成进度条；
    ///   - `GameScreen.UpdateIdeasLog` 在城市场地只显示 `CardUpdateType == Cities` 的知识
    ///     → 不标的话玩家在城市的主意列表里根本看不到这张卡的创意。
    ///
    /// 代价（有意接受）：主大陆/岛上的主意列表只显示 `!= Cities` 的知识，所以标了的卡（及其创意）
    /// **只出现在城市**。目前用到它的是三张城市专属卡：磁力容器、高级回收中心、磁力信用卡。
    /// </summary>
    internal static class CitiesCardSupport
    {
        internal static void MarkAsCitiesCard(string cardId, string blueprintId)
        {
            try
            {
                GameDataLoader loader = WorldManager.instance.GameDataLoader;
                CardData card = loader.GetCardFromId(cardId, false);
                if (card != null)
                {
                    card.CardUpdateType = CardUpdateType.Cities;
                }
                else
                {
                    Debug.LogWarning("[StacklandsProMax] 找不到卡 " + cardId + "，城市配方可能不生效。");
                }

                // 蓝图也是 CardData（BlueprintPrefabs = CardDataPrefabs.OfType<Blueprint>()），
                // 所以这里改的是 FindMatchingPrint 遍历的同一个实例。
                if (string.IsNullOrEmpty(blueprintId))
                {
                    return;
                }
                CardData blueprint = loader.GetCardFromId(blueprintId, false);
                if (blueprint != null)
                {
                    blueprint.CardUpdateType = CardUpdateType.Cities;
                }
                else
                {
                    Debug.LogWarning("[StacklandsProMax] 找不到创意卡 " + blueprintId + "，城市配方可能不生效。");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[StacklandsProMax] 标记城市卡类型失败（" + cardId + "）：" + ex.Message);
            }
        }
    }

    /// <summary>
    /// 硬币磁力箱（Coin Magnet Chest）：硬币箱 + 磁铁。
    ///
    /// 需求原话：「增加"硬币磁力箱"，主意是硬币箱+磁铁，功能、机制等和磁力箱一样，
    /// 能大范围吸取硬币，从箱子里取出的硬币十分钟内不会被吸回去」。
    ///
    /// 它同时继承两边的机制：
    ///   - `Chest`（原版硬币箱）：金币存进 `CoinCount`、`HeldCardId` 固定 "gold"、
    ///     点击一次取 5 张、卡面显示箱内金币图标与数量。**箱内上限按用户要求设为"那个很大的上限值"**。
    ///   - `MagnetChest` 的那套"吸附"逻辑（本类自己实现一遍，因为 C# 只能单继承）：
    ///     ① 走 `WorldManager.TrySendToMagnet` 截胡 → 金币一产出就被改道进箱
    ///     ② 扫描场上散落的金币，用原版 `StackSendTo` 拖过来
    ///     ③ 取出的金币登记 10 分钟免疫，不会被自己立刻吸回去
    ///
    /// 存储类型是固定的（只有金币），所以这里没有磁力箱那种"用第一张卡设定类型"的流程。
    /// 设置界面里的「自动吸附 / 取出后不被回吸」两个开关对这只箱子同样有效。
    /// </summary>
    public class CoinMagnetChest : Chest
    {
        /// <summary>硬币箱只收金币（贝壳版在 JSON 里用 `_CoinPullCardId` 覆盖成 "shell"）。</summary>
        public string CoinPullCardId = "gold";

        /// <summary>
        /// 外观要镜像的原版卡 id（默认原版硬币箱 `coin_chest`；贝壳版在 JSON 里覆盖成 `shell_chest`）。
        /// 理由和磁力箱的 `ApplyVanillaChestLook` 一样：`IsBuilding` 之类只存在预制体里，代码里查不到值。
        /// </summary>
        public string VanillaLookCardId = "coin_chest";

        // ---- 吸附参数（可在 Cards/coin_magnet_chest.json 里用 "_字段名" 覆盖）----
        public float PullRadius = 999f;               // 吸附半径：999 = 整块场地
        public float PullInterval = 0.1f;             // 两次吸附之间的最短间隔（秒）
        public float RemovalImmunitySeconds = 600f;   // 取出后的免疫时长（秒）：600 = 10 分钟
        public float ImmunityKeepAwayFactor = 1.6f;   // 拿开到「半径 x 该系数」之外就解除免疫

        private float nextPullTime;

        // 原版硬币箱预制体的外观数据是否已经借过一次
        private bool triedLoadingVanillaLook;

        protected override bool CanHaveCard(CardData otherCard)
        {
            // 只收金币。注意这里**不**调用 base.CanHaveCard：
            // 原版那个实现里 `otherCard.Id == Id`（自己叠自己）会返回 true，
            // 而我们不想让硬币磁力箱互相叠（叠起来下面那只会停止吸附，还容易出怪状态）。
            if (otherCard == null)
            {
                return false;
            }
            return otherCard.Id == CoinPullCardId;
        }

        /// <summary>
        /// 同理（见 `MagnetChest.CanHaveCardsWhileHasStatus` 的说明）：原版 `Chest` 没有重写这个，
        /// 所以硬币磁力箱一旦有计时器/状态条，金币就会被 `GameCard.Bounce()` 拒之门外。
        /// 原版硬币箱没有计时器所以不需要；我们的箱子要能持续吸金币，必须显式放行。
        /// </summary>
        public override bool CanHaveCardsWhileHasStatus()
        {
            return true;
        }

        /// <summary>
        /// 这个箱子是否愿意接收某张卡——给 WorldManager.TrySendToMagnet 的补丁用。
        /// 原版磁铁就是在那里把"刚产出、正在被送出"的资源直接截走的；走同一个入口，
        /// 效果就是**金币一产出就被吸走**，而不是先落到地上再被扫到。
        /// </summary>
        public bool WantsCard(CardData card)
        {
            if (card == null || card.Id != CoinPullCardId)
            {
                return false; // 不是金币：不抢
            }
            if (MyGameCard == null || MyGameCard.BeingDragged)
            {
                return false;
            }
            if (MyGameCard.HasParent && !(MyGameCard.Parent.CardData is HeavyFoundation))
            {
                return false;
            }
            // 关键安全检查：StackSendTo 在目标收不下时会改调 SendIt()（卡片直接消失），
            // 所以发送前必须按同样的条件确认一遍。
            GameCard landingTarget = MyGameCard.GetLeafCard();
            return landingTarget != null && landingTarget.CardData != null
                && landingTarget.CardData.CanHaveCardOnTop(card);
        }

        public override void UpdateCard()
        {
            // 0.5.14：上限 1 万（0.5.8~0.5.13 曾设 int.MaxValue 无限；0.5.14 用户定值，与原版箱子定值口径一致）。
            // 0.5.18：改为跟随设置里的「硬币/贝壳磁力箱容量」输入框。
            // ⚠️ 不要把 CoinCount 往下钳到上限：旧存档里已超限的硬币/贝壳会被吞掉
            // （同 MagnetCreditcard 0.5.13 的处理）——超限只是不再吸新的，已有的照常保留/花销。
            MaxCoinCount = ModConfig.CoinShellMagnetChestCapacity;
            AbsorbStackedCoins();
            PullNearbyCoins();
            UpdateCoinVisuals();
            MagnetChestCleanup();
            base.UpdateCard();
        }

        /// <summary>
        /// 把已经叠到箱子上的金币吞掉并计数（对齐原版 Chest.UpdateCard 的循环，
        /// 但只处理自己这一张，不像原版那样在整叠里找"还有空位的箱子"）。
        /// 吸附过来的金币也是走这里入库的。
        /// </summary>
        private void AbsorbStackedCoins()
        {
            if (MyGameCard.HasParent && !(MyGameCard.Parent.CardData is HeavyFoundation))
            {
                return; // 被压在别的卡下面时不处理
            }
            foreach (GameCard childCard in MyGameCard.GetChildCards())
            {
                if (childCard == null || childCard.CardData == null)
                {
                    continue;
                }
                if (childCard.CardData.Id != CoinPullCardId)
                {
                    continue;
                }
                if (CoinCount >= MaxCoinCount)
                {
                    childCard.RemoveFromParent(); // 满了就把多出来的弹出去
                    break;
                }
                childCard.DestroyCard(spawnSmoke: true);
                CoinCount++;
            }
        }

        /// <summary>扫描周围散落的金币，用原版磁铁的方式把它们"吸"过来。</summary>
        private void PullNearbyCoins()
        {
            if (CoinCount >= MaxCoinCount) return;                // 满了就不吸
            if (MyGameCard.BeingDragged) return;                  // 玩家正在拖这个箱子
            if (MyGameCard.InConflict) return;                    // 战斗中不吸
            if (MyGameCard.HasParent && !(MyGameCard.Parent.CardData is HeavyFoundation)) return;
            if (Time.time < nextPullTime) return;                 // 控制吸附频率

            GameCard best = null;
            float bestSqrDistance = PullRadius * PullRadius;
            Vector3 myPosition = MyGameCard.transform.position;

            foreach (GameCard candidate in WorldManager.instance.AllCards)
            {
                if (candidate == null || candidate == MyGameCard) continue;
                if (candidate.CardData == null || candidate.CardData.Id != CoinPullCardId) continue;
                if (candidate.MyBoard != MyGameCard.MyBoard) continue;
                if (candidate.HasParent || candidate.HasChild) continue;   // 只吸完全散落的单张，不拆别人的堆
                if (candidate.BeingDragged) continue;                      // 玩家手上的绝对不抢
                if (!candidate.gameObject.activeInHierarchy) continue;
                if (candidate.Velocity.HasValue || candidate.BounceTarget != null) continue; // 已经在飞了
                if (MagnetChest.IsRecentlyRemoved(candidate)) continue;    // 刚从这个箱子（或磁力箱）取出来的

                Vector3 offset = candidate.transform.position - myPosition;
                offset.y = 0f;
                float sqrDistance = offset.sqrMagnitude;
                if (sqrDistance > bestSqrDistance) continue;

                bestSqrDistance = sqrDistance;
                best = candidate;
            }

            if (best == null) return;

            // 安全检查：原版 StackSendTo 在目标无法接收时会改调 SendIt()（卡片就没了），
            // 所以先按同样的判定条件确认一遍，确认不了就跳过，等下一轮。
            GameCard landingTarget = MyGameCard.GetLeafCard();
            if (landingTarget == null || landingTarget.CardData == null
                || !landingTarget.CardData.CanHaveCardOnTop(best.CardData))
            {
                nextPullTime = Time.time + Mathf.Max(0.05f, PullInterval);
                return;
            }

            nextPullTime = Time.time + Mathf.Max(0.05f, PullInterval);
            WorldManager.instance.StackSendTo(best, MyGameCard);
        }

        /// <summary>
        /// 取出的金币登记"10 分钟免疫"（需求原话：「从箱子里取出的硬币十分钟内不会被吸回去」）。
        /// 免疫表是跟磁力箱共用的那张，所以刚取出的金币对两种箱子都免疫。
        /// 原版 Chest.Clicked 会把金币送出去，所以这里在 base.Clicked() 之后登记。
        /// </summary>
        public override void Clicked()
        {
            int before = CoinCount;
            GameCard previousDragging = WorldManager.instance == null ? null : WorldManager.instance.DraggingCard;
            base.Clicked();

            if (CoinCount >= before)
            {
                return; // 没取出任何东西（箱子是空的）
            }

            float keepAwayRadius = PullRadius * Mathf.Max(1f, ImmunityKeepAwayFactor);
            float expiresAt = Time.time + Mathf.Max(1f, RemovalImmunitySeconds);

            // 原版 Chest.Clicked 取出的那几张金币刚被送出去，还在飞：找出刚落地的金币。
            // 判据：位置离箱子很近、且是散落的单张金币。宁可漏标也不要错标，
            // 所以只标"这一帧新出现的、没有父卡的金币"。
            foreach (GameCard card in WorldManager.instance.AllCards)
            {
                if (card == null || card.CardData == null) continue;
                if (card.CardData.Id != CoinPullCardId) continue;
                if (card.MyBoard != MyGameCard.MyBoard) continue;
                if (card.HasParent || card.HasChild) continue;
                if (card.BeingDragged) continue;
                if (!card.gameObject.activeInHierarchy) continue;
                if (card == previousDragging) continue;

                Vector3 offset = card.transform.position - MyGameCard.transform.position;
                offset.y = 0f;
                if (offset.sqrMagnitude > PullRadius * PullRadius) continue;

                MagnetChest.MarkRecentlyRemovedFrom(card, MyGameCard, keepAwayRadius, expiresAt);
            }
        }

        /// <summary>
        /// 硬币磁力箱的卡面显示：**完全按原版硬币箱那一套来**（用户要求"改成硬币箱的样子"）。
        ///
        /// 原版硬币箱（`Chest.UpdateCard`）只做一件事：`Value = CoinCount`。于是 `GameCard` 用
        /// `CoinText` + `CoinIcon` 画出"金币图标 + 数量"（这是 `GameCard` 上独立于
        /// `SpecialValue`/`SpecialText` 的一套，由 `Chest` 自己驱动）。它**不用** `SpecialValue`。
        ///
        /// 之前这里把 `Value`、`SpecialValue`、`SpecialIcon`、卡面 `Icon` 全都设了一遍，结果：
        ///   - 同一数量被画了两遍，用户看到"右下角硬币数量似乎叠在一起了"
        ///   - 卡面中间被换成金币图标（原版硬币箱中间是**卡自己的图标**，也就是箱子图）
        /// 现在这里只做"镜像原版硬币箱的配色"，别的什么都不碰。
        /// 卡面图标在 `GameCard.Start()` 里已经按卡片自己的 icon 初始化过了，原版硬币箱也从不改它。
        /// </summary>
        private void UpdateCoinVisuals()
        {
            ApplyVanillaCoinChestLook();
        }

        /// <summary>
        /// 外观照镜像的原版箱（硬币版＝原版硬币箱 `coin_chest`，贝壳版＝原版贝壳宝箱 `shell_chest`）：
        /// 把它的配色字段直接复制过来
        /// （理由和磁力箱的 ApplyVanillaChestLook 一样：IsBuilding 存在预制体里，代码里查不到值）。
        /// </summary>
        private void ApplyVanillaCoinChestLook()
        {
            if (triedLoadingVanillaLook)
            {
                return;
            }
            triedLoadingVanillaLook = true;

            string sourceId = string.IsNullOrEmpty(VanillaLookCardId) ? "coin_chest" : VanillaLookCardId;
            CardData vanillaCoinChest = WorldManager.instance.GetCardPrefab(sourceId);
            if (vanillaCoinChest == null)
            {
                Debug.LogWarning("[StacklandsProMax] 找不到原版箱子 " + sourceId + "，本箱将使用默认外观。");
                return;
            }

            IsBuilding = vanillaCoinChest.IsBuilding;
            HasUniquePalette = vanillaCoinChest.HasUniquePalette;
            MyPalette = vanillaCoinChest.MyPalette;
            if (vanillaCoinChest is Chest chest)
            {
                ChestTerm = chest.ChestTerm;
            }
            // 诊断用：把原版箱子的卡面图标名字打一行，方便在游戏里确认"中间该显示哪张图"
            Debug.Log("[StacklandsProMax] 原版箱子 " + sourceId + " 的卡面图标 = "
                + (vanillaCoinChest.Icon == null ? "<null>" : vanillaCoinChest.Icon.name)
                + "；本卡自己的图标 = " + (Icon == null ? "<null>" : Icon.name));

            if (MyGameCard != null)
            {
                MyGameCard.UpdateCardPalette();
            }
        }

        /// <summary>定期清理免疫表（复用磁力箱的那张表，避免两份记录各清一半）。</summary>
        private static void MagnetChestCleanup()
        {
            MagnetChest.CleanupImmunity();
        }
    }

    /// <summary>
    /// 贝壳磁力箱（Shell Magnet Chest）：**贝壳宝箱（`shell_chest`）+ 磁铁**，
    /// 也就是硬币磁力箱的"贝壳版"。
    ///
    /// 需求原话：「参考硬币箱和硬币磁力箱，做一个贝壳宝箱的升级版，名字就叫贝壳磁力箱」。
    ///
    /// 原版机制（反编译核实）：金币和贝壳都走 `Chest` 这条线，而不是 `ResourceChest`——
    /// `WorldManager.TrySendToChest` 里是
    ///     先看这张卡的 id 是不是 "gold" 或 "shell"：是就找 `HeldCardId` 匹配且未满的 `Chest`，否则找 `ResourceChest`；
    /// 而 `Chest.HeldCardId` 默认 `"gold"`，原版贝壳宝箱就是把它改成 `"shell"` 的那张卡
    /// （`shell_chest` / `blueprint_shell_chest` 都是原版真实卡 id）。
    ///
    /// 所以本类**不需要任何新逻辑**：存储（`CoinCount`）、点击取 5、吸附（`TrySendToMagnet` 截胡 +
    /// 扫描场上散落的贝壳）、取出 10 分钟免疫、卡面照原版箱子，全部继承 `CoinMagnetChest`
    /// （那套又是对齐磁力箱的）。区别只在**数据**，全部写在 `Cards/shell_magnet_chest.json` 里：
    ///   - `_HeldCardId = "shell"`        → 存贝壳、`TrySendToChest` 也认它
    ///   - `_CoinPullCardId = "shell"`    → 只吸贝壳
    ///   - `_VanillaLookCardId = "shell_chest"` → 外观镜像原版**贝壳宝箱**（不是硬币箱）
    ///   - `"icon": "shell_chest"`        → 卡面图标借原版贝壳宝箱
    /// 留这个类是为了"代码里的名字和卡对得上"，以及以后要加贝壳专属行为时有地方落。
    /// </summary>
    public class ShellMagnetChest : CoinMagnetChest
    {
    }

    /// <summary>
    /// 磁力信用卡（Magnet Credit Card）：**信用卡（原版 `creditcard`）+ 磁铁**。
    ///
    /// 需求原话（0.5.5）：「再做一个信用卡的升级版，叫磁力信用卡」。
    ///
    /// 原版信用卡（`Creditcard`，继承 `CardData` 并实现 `ICurrency`）：
    ///   - 钱存在 `DollarCount` 里（`[ExtraData("dollar_count")]`，会存档），上限 `MaxDollarCount = 1000`；
    ///   - `CanHaveCard` 只接受 `Dollar`（纸币）或另一张信用卡；
    ///   - `UpdateCard()` 把叠在自己身上的 `Dollar` 吞进 `DollarCount`（`DollarValue` 是面额，超出上限的部分
    ///     会 `CreateDollarsFromValue` 再吐出来）；
    ///   - `Clicked()` 一次最多吐出 **100** 面额的纸币；
    ///   - 实现 `ICurrency`，所以买东西时会被原版结算流程扣钱（`BuyWithCreditcard`）。
    ///
    /// 本类＝给它装上磁铁（和磁力箱/硬币磁力箱/贝壳磁力箱同一套做法，代码在本类里重写一遍，
    /// 因为 C# 只能单继承、而这里要继承 `Creditcard` 的存储与结算）：
    ///   ① `TrySendToMagnet` 截胡 → 纸币一出现就被改道进卡
    ///   ② 扫描场上散落的纸币，用原版 `StackSendTo` 拖过来（吞卡计数仍由原版 `Creditcard.UpdateCard()` 负责）
    ///   ③ `Clicked()` 吐出来的纸币登记 **10 分钟免疫**（和三种箱子共用同一张免疫表），不会被自己立刻吸回去
    ///
    /// 容量：JSON 里的 `_MaxDollarCount`（0.5.8 无限 → 0.5.13 原版 1000 → **0.5.14 用户定值 100000**；
    /// 超限后只是不再吸新纸币，已有余额照常保留——见 UpdateCard 里的说明）。
    /// 外观镜像原版信用卡（`_VanillaLookCardId` 默认 `creditcard`）。
    /// </summary>
    public class MagnetCreditcard : Creditcard
    {
        /// <summary>外观要镜像的原版卡 id（默认原版信用卡 `creditcard`）。</summary>
        public string VanillaLookCardId = "creditcard";

        // ---- 吸附参数（可在 Cards/magnet_creditcard.json 里用 "_字段名" 覆盖）----
        public float PullRadius = 999f;               // 吸附半径：999 = 整块场地
        public float PullInterval = 0.1f;             // 两次吸附之间的最短间隔（秒）
        public float RemovalImmunitySeconds = 600f;   // 取出后的免疫时长（秒）：600 = 10 分钟
        public float ImmunityKeepAwayFactor = 1.6f;   // 拿开到「半径 x 该系数」之外就解除免疫

        private float nextPullTime;
        private bool triedLoadingVanillaLook;

        /// <summary>
        /// 正在"点击吐钱"的瞬间，磁力信用卡（以及场上任何磁力卡）都不许截胡新生成的纸币。
        ///
        /// 为什么：`Creditcard.Clicked` → `CreateDollarsFromValue` → `CreateCard` 的最后一步会调
        /// `WorldManager.TrySendToMagnet`（反编译核对），而我们就挂在这个入口上 →
        /// **玩家点出来的钱一出生就被自己认领**、`BounceTarget` 直接指回本卡：
        /// 观感就是"钱往左边飞出去、然后掉头飞回卡里消失"（和 0.6.1 磁力容器
        /// "取出的卡先侧飞再消失"是同一类问题）。这里在"点击吐钱"这一小段时间里把截胡入口整个关掉。
        /// </summary>
        internal static bool SuppressMagnetClaim;

        /// <summary>
        /// 这个卡是否愿意接收某张卡——给 `WorldManager.TrySendToMagnet` 的补丁用。
        /// 判断条件和三种箱子一样：目标收不下就绝不抢（`StackSendTo` 在目标收不下时会改调 `SendIt()`，
        /// 那会让这张纸币直接消失）。
        /// </summary>
        public bool WantsCard(CardData card)
        {
            if (SuppressMagnetClaim)
            {
                return false; // 这次是玩家点卡吐钱，不许任何磁力卡截胡（见 SuppressMagnetClaim 注释）
            }
            if (!(card is Dollar))
            {
                return false; // 只抢纸币
            }
            if (MyGameCard == null || MyGameCard.BeingDragged)
            {
                return false;
            }
            if (DollarCount >= MaxDollarCount)
            {
                return false; // 满了不抢
            }
            if (MyGameCard.HasParent && !(MyGameCard.Parent.CardData is HeavyFoundation))
            {
                return false;
            }
            GameCard landingTarget = MyGameCard.GetLeafCard();
            return landingTarget != null && landingTarget.CardData != null
                && landingTarget.CardData.CanHaveCardOnTop(card);
        }

        public override void UpdateCard()
        {
            // 0.5.18：上限跟随设置里的「磁力信用卡容量」输入框（JSON `_MaxDollarCount` 只在新卡生成时生效）。
            // ⚠️ 不要把 DollarCount 往下钳到 MaxDollarCount（0.5.13 去掉了这行 Clamp）：
            // 上限从无限改回 1000 后，旧存档里已超限的余额会被这行吞掉（ Clamp(5000, 0, 1000) = 1000）。
            // 超限时行为与磁力箱一致：已有的照常保留/花销，只是不再吸新的。
            MaxDollarCount = ModConfig.MagnetCreditcardCapacity;

            PullNearbyDollars();
            ApplyVanillaCreditcardLook();
            MagnetChest.CleanupImmunity();

            // 吞掉叠上来的纸币（含面额拆分、超出上限再吐出来）交给原版实现，
            // 与三种箱子不同：那里的吸收入口是"原版 Chest/ResourceChest 的循环"，这里直接用原版的。
            base.UpdateCard();
        }

        /// <summary>扫描周围散落的纸币，用原版磁铁的方式把它们"吸"过来。</summary>
        private void PullNearbyDollars()
        {
            if (DollarCount >= MaxDollarCount) return;            // 满了就不吸
            if (MyGameCard.BeingDragged) return;                  // 玩家正在拖这张卡
            if (MyGameCard.InConflict) return;                    // 战斗中不吸
            if (MyGameCard.HasParent && !(MyGameCard.Parent.CardData is HeavyFoundation)) return;
            if (Time.time < nextPullTime) return;                 // 控制吸附频率

            GameCard best = null;
            float bestSqrDistance = PullRadius * PullRadius;
            Vector3 myPosition = MyGameCard.transform.position;

            foreach (GameCard candidate in WorldManager.instance.AllCards)
            {
                if (candidate == null || candidate == MyGameCard) continue;
                if (!(candidate.CardData is Dollar)) continue;
                if (candidate.MyBoard != MyGameCard.MyBoard) continue;
                if (candidate.HasParent || candidate.HasChild) continue;   // 只吸完全散落的单张，不拆别人的堆
                if (candidate.BeingDragged) continue;                      // 玩家手上的绝对不抢
                if (!candidate.gameObject.activeInHierarchy) continue;
                if (candidate.Velocity.HasValue || candidate.BounceTarget != null) continue; // 已经在飞了
                if (MagnetChest.IsRecentlyRemoved(candidate)) continue;    // 刚从这里（或三种箱子）取出来的

                Vector3 offset = candidate.transform.position - myPosition;
                offset.y = 0f;
                float sqrDistance = offset.sqrMagnitude;
                if (sqrDistance > bestSqrDistance) continue;

                bestSqrDistance = sqrDistance;
                best = candidate;
            }

            if (best == null) return;

            // 安全检查：原版 StackSendTo 在目标无法接收时会改调 SendIt()（纸币就没了），
            // 所以先按同样的判定条件确认一遍，确认不了就跳过，等下一轮。
            GameCard landingTarget = MyGameCard.GetLeafCard();
            if (landingTarget == null || landingTarget.CardData == null
                || !landingTarget.CardData.CanHaveCardOnTop(best.CardData))
            {
                nextPullTime = Time.time + Mathf.Max(0.05f, PullInterval);
                return;
            }

            nextPullTime = Time.time + Mathf.Max(0.05f, PullInterval);
            WorldManager.instance.StackSendTo(best, MyGameCard);
        }

        /// <summary>
        /// 余额显示对齐原版 `Creditcard.UpdateCardText()`，只去掉"卡上必须有连接器"的门槛（0.5.12）。
        /// 原版这句"余额 X/Y"只在卡挂着能源/管道连接器时才显示——原版信用卡是 Cities 卡、
        /// 预制体自带连接器所以看得见；本卡没声明连接器，照原版条件的话悬停永远看不到余额
        /// （用户需求原话「显示余额功能要有。把磁力信用卡的显示余额功能改成和原信用卡一样」）。
        /// 词条用原版的 `BankDescriptionTerm`（在 ApplyVanillaCreditcardLook 里从原版信用卡拷来），
        /// 所以文案、占位符（count / max_count / 美元图标）和官方翻译与原版完全一致；
        /// ⚠️ 上限 0.5.14 起为用户定值 100000（JSON `_MaxDollarCount`；0.5.13 曾短期同原版 1000），
        /// 文案里的 count/max_count 就是真实余额/上限。
        /// </summary>
        public override void UpdateCardText()
        {
            if (MyGameCard != null && MyGameCard.IsHovered)
            {
                descriptionOverride = SokLoc.Translate(BankDescriptionTerm,
                    LocParam.Create("count", DollarCount.ToString()),
                    LocParam.Create("max_count", MaxDollarCount.ToString()),
                    LocParam.Create("icon", Icons.Dollar));
                string connectorInfo = GetConnectorInfoString(MyGameCard);
                if (!string.IsNullOrEmpty(connectorInfo))
                {
                    descriptionOverride = descriptionOverride + "\n\n<i>" + connectorInfo + "</i>";
                }
            }
        }

        /// <summary>
        /// 点击吐钱：**这一瞬间不许任何磁力卡截胡新生成的纸币**，吐出来的纸币再登记"10 分钟免疫"。
        ///
        /// 原版 `Creditcard.Clicked()` 一次性把最多 100 面额的纸币 `CreateDollarsFromValue` 出来，
        /// 而 `CreateCard` 的最后一步会调 `WorldManager.TrySendToMagnet` → 本卡会被问"要不要" →
        /// 纸币当场被认领回本卡（观感：钱飞出去又掉头飞回来）。所以这里：
        ///   ① 进 `base.Clicked()` 之前把 <see cref="SuppressMagnetClaim"/> 打开，吐完立刻关掉；
        ///   ② 之后只给"这次新出现的纸币"登记免疫（对比点击前后的纸币集合，精确到张，
        ///      不再依赖"是否散落/是否在半径内"这些会被堆叠/DollarCount 拆分搞乱的条件）。
        /// </summary>
        public override void Clicked()
        {
            int before = DollarCount;
            GameCard previousDragging = WorldManager.instance == null ? null : WorldManager.instance.DraggingCard;
            HashSet<GameCard> dollarsBefore = CollectDollarCards();

            bool previousSuppress = SuppressMagnetClaim;
            SuppressMagnetClaim = true;
            try
            {
                base.Clicked();
            }
            finally
            {
                SuppressMagnetClaim = previousSuppress;
            }

            if (DollarCount >= before)
            {
                return; // 没吐出任何东西（卡里没钱）
            }

            float keepAwayRadius = PullRadius * Mathf.Max(1f, ImmunityKeepAwayFactor);
            float expiresAt = Time.time + Mathf.Max(1f, RemovalImmunitySeconds);

            foreach (GameCard card in CollectDollarCards())
            {
                if (dollarsBefore.Contains(card)) continue; // 点击之前就在场上的，不动
                if (card == previousDragging) continue;

                MagnetChest.MarkRecentlyRemovedFrom(card, MyGameCard, keepAwayRadius, expiresAt);
            }
        }

        /// <summary>场上所有纸币的卡（用于对比"点击前后多了哪几张"）。</summary>
        private static HashSet<GameCard> CollectDollarCards()
        {
            HashSet<GameCard> result = new HashSet<GameCard>();
            if (WorldManager.instance == null)
            {
                return result;
            }
            foreach (GameCard card in WorldManager.instance.AllCards)
            {
                if (card == null || card.CardData == null) continue;
                if (!(card.CardData is Dollar)) continue;
                result.Add(card);
            }
            return result;
        }

        /// <summary>
        /// 外观照镜像的原版信用卡（配色 + 说明文案的 term），理由和磁力箱一致：
        /// `IsBuilding` / 配色存在预制体里，代码里查不到值。
        /// </summary>
        private void ApplyVanillaCreditcardLook()
        {
            if (triedLoadingVanillaLook)
            {
                return;
            }
            triedLoadingVanillaLook = true;

            string sourceId = string.IsNullOrEmpty(VanillaLookCardId) ? "creditcard" : VanillaLookCardId;
            CardData vanilla = WorldManager.instance.GetCardPrefab(sourceId);
            if (vanilla == null)
            {
                Debug.LogWarning("[StacklandsProMax] 找不到原版信用卡 " + sourceId + "，磁力信用卡将使用默认外观。");
                return;
            }

            IsBuilding = vanilla.IsBuilding;
            HasUniquePalette = vanilla.HasUniquePalette;
            MyPalette = vanilla.MyPalette;
            if (vanilla is Creditcard creditcard)
            {
                BankDescriptionTerm = creditcard.BankDescriptionTerm;
            }
            // ★ 卡面图标也对齐原版（连 null 一起对齐）：JSON 里写 "icon": "creditcard" 实测解析不到
            //   （0.5.11 用户报"卡面中间的图标和信用卡不一样"，Player.log 里本卡 Icon = <null>，
            //   原版 = creditcard）——mod 卡加载时机早于原版 Cities 卡的 Icon 就绪，和高级温室
            //   主意图标（ApplyVanillaIdeaIcon）是同一类时序问题，同样用"运行时从原版预制体拷引用"修。
            string iconBefore = Icon == null ? "<null>" : Icon.name;
            Icon = vanilla.Icon;
            Debug.Log("[StacklandsProMax] 原版信用卡 " + sourceId + " 的卡面图标 = "
                + (vanilla.Icon == null ? "<null>" : vanilla.Icon.name)
                + "；本卡图标 " + iconBefore + " -> " + (Icon == null ? "<null>" : Icon.name));

            if (MyGameCard != null)
            {
                MyGameCard.UpdateCardPalette();
                MyGameCard.UpdateIcon(); // 配色在创建时才算一次要手动刷；Icon 同理，改完立即重画卡面
            }
        }
    }

    /// <summary>
    /// 高级回收中心（Advanced Recycling Center，0.6.2 新增，城市场地专属）。
    ///
    /// 用户需求原话：「新增卡牌高级回收中心，由5个回收中心合成，原先的回收中心是每30秒清除10点污染，
    /// 高级回收中心是每6秒清除10点污染，回收中心需要接1路下水道，高级回收中心需要5路。
    /// 回收中心需要两个人，高级回收中心需要5个，回收中心有一个运输输出，高级回收中心也只有一个」。
    ///
    /// ── 实现＝继承原版 `RecyclingCenter`，只改四处 ──────────────────────────────
    ///
    ///   1) **周期 30 秒 → 6 秒**：原版把 `30f` 写死在 `RecyclingCenter.UpdateCard` 里
    ///      （`MyGameCard.StartTimer(30f, DumpPollution, ...)`）。这里既不复写整个方法、也不打 transpiler：
    ///      `base.UpdateCard()` 跑完后再补一次 `StartTimer(6f, ...)` —— 原版 `StartTimer` 在
    ///      `TimerActionId` 相同的分支里**只把 `TargetTimerTime` 改小**（反编译核对），
    ///      而计时器结算条件是 `CurrentTimerTime >= TargetTimerTime`（24459），
    ///      进度条 fill 又是 `CurrentTimerTime / TargetTimerTime`（55747）→ 6 秒整点结算、条也正常。
    ///      计时器没在跑（工人/下水道不够、溢出、拖拽中）时这段什么都不做，交回原版。
    ///
    ///   2) **工人 2 → 5、下水道 1 → 5 路、高压电 1 → 3 路、运输输出 1 路**：全部由
    ///      `Cards/advanced_recycling_center.json` 的字段声明，不写代码——`_WorkerAmount` 由
    ///      `EnergyConsumer.UpdateCard` 的 `WorkerAmountMet()` 校验（不够就取消计时器）；
    ///      **`Sewer` 只算"下水道"、不算能源**（`CardConnector.IsEnergyConnector` 只认 LV/HV，
    ///      见反编译核对），所以：
    ///        · 3 路**高压电输入**（`CardDirection 0` + `ConnectionType 3`）由 `HasEnergyInput()` →
    ///          `AllInputConnectorsPowered()` 校验，每一路都要接上并通电；
    ///        · 5 路**下水道输出**（`CardDirection 1` + `ConnectionType 4`）由 `HasSewerConnected()` →
    ///          `AllSewerOutputsFunctional()` 校验（它只认 `output` 方向的下水道，反编译核对），
    ///          每一路都要接上；
    ///        · 运输输出 1 路（`CardDirection 1` + `ConnectionType 5`），产物照原版沿 `OutputDir`
    ///          走 `StackSendCheckTarget`。
    ///      （0.6.2 曾把下水道写成 `input`：那样既不算能源、也不进 `HasSewerConnected` 的判定
    ///        → 卡上多出几个永远不会被校验的下水道输入口，0.6.3 已改正。
    ///        数量 0.6.6 定稿：**高压电输入 3 路、下水道输出 5 路、运输输出 1 路**。）
    ///
    ///   3) **合成时必须先"停摆"**（0.6.4，用户定的规矩）：5 张回收中心叠好后，堆里只要还有一张
    ///      接着电、或者还有工人没取出来，就不出进度条；拆线/取人的瞬间条件满足，进度条自己出来
    ///      （`AdvancedRecyclingCenterBlueprintPatch`）。
    ///
    ///   4) **外观与文案**：空/半满/满三个图标是原版 prefab 上的 Sprite 资产，JSON 填不了，
    ///      运行时从原版 `recycling_center` 上拷一份；名字/描述/溢出文案用自己的 term。
    ///
    /// 其余全部继承：收污染卡（`CanHaveCard` 认 `pollution` 与 `Worker`）、存满溢出 + 120 秒结算、
    /// 每个周期产 1 个金属废料沿运输输出送走。溢出阈值见 JSON（`_PollutionOverflowMin/Max` 默认 200/300）——
    /// 比原版宽松，因为清理快 5 倍，缓冲区大一点才不会三天两头溢出。
    /// </summary>
    public class AdvancedRecyclingCenter : RecyclingCenter
    {
        public const string CardId = "stacklandspro_advanced_recycling_center";
        public const string BlueprintId = "stacklandspro_blueprint_advanced_recycling_center";

        /// <summary>清除一次污染的周期（秒）。原版写死 30，这版是 6。</summary>
        public const float CleanIntervalSeconds = 6f;

        private bool triedLoadingVanillaLook;

        public override void OnInitialCreate()
        {
            ApplyVanillaLook();
            base.OnInitialCreate();
        }

        public override void UpdateCard()
        {
            ApplyVanillaLook(); // 图标必须先有，否则原版 UpdateCard 会把 Icon 设成 null
            base.UpdateCard();
            ShortenPollutionTimer();
        }

        /// <summary>
        /// 把原版刚起的 30 秒计时器压到 <see cref="CleanIntervalSeconds"/>。
        /// 只在"计时器确实在跑、且跑的就是清除污染这一件事"时动手。
        /// </summary>
        private void ShortenPollutionTimer()
        {
            GameCard card = MyGameCard;
            if (card == null || !card.TimerRunning)
            {
                return;
            }
            string actionId = GetActionId("DumpPollution");
            if (card.TimerActionId != actionId || card.TargetTimerTime <= CleanIntervalSeconds)
            {
                return;
            }
            card.StartTimer(CleanIntervalSeconds, DumpPollution, card.Status, actionId);
        }

        /// <summary>
        /// 从原版回收中心 prefab 上拷三个图标（空/半满/满）。原版 `UpdateCard` 每帧按存量切这三个图标，
        /// JSON 里没有 Sprite 字段可填，所以只能运行时借。
        /// </summary>
        private void ApplyVanillaLook()
        {
            if (triedLoadingVanillaLook)
            {
                return;
            }
            triedLoadingVanillaLook = true;

            CardData vanilla = WorldManager.instance.GetCardPrefab("recycling_center");
            if (!(vanilla is RecyclingCenter center))
            {
                Debug.LogWarning("[StacklandsProMax] 找不到原版回收中心 recycling_center，高级回收中心将没有图标。");
                return;
            }
            EmptyIcon = center.EmptyIcon;
            HalfFullIcon = center.HalfFullIcon;
            FullIcon = center.FullIcon;
        }

        public override void UpdateCardText()
        {
            if (IsOverflowing)
            {
                nameOverride = SokLoc.Translate("stacklandspro_advanced_recycling_center_overflow_name");
                descriptionOverride = SokLoc.Translate("stacklandspro_advanced_recycling_center_overflow_description");
                return;
            }

            nameOverride = null;
            if (StoredPollution <= 0)
            {
                descriptionOverride = SokLoc.Translate("stacklandspro_advanced_recycling_center_description",
                    LocParam.Create("amount", PollutionOverflowMin.ToString()));
            }
            else
            {
                descriptionOverride = SokLoc.Translate("stacklandspro_advanced_recycling_center_description_long",
                    LocParam.Create("amount", PollutionOverflowMin.ToString()),
                    LocParam.Create("current", StoredPollution.ToString()));
            }
        }
    }

    /// <summary>
    /// 高级集市（Advanced Market，0.5.19 新增）。
    ///
    /// 用户需求原话：「配方是一个铁条，一个金条，三个硬币，需要村民参与合成，高级集市有3个运输输入接口
    /// 和一个运输输出接口（和原版集市一样），可以连接资源箱的输出，卡牌运送到高级集市后，
    /// 经过10秒的读秒会自动被售出，所有堆叠在高级商店上的物品可以一次性全部售出（参考炖锅）」。
    ///
    /// ── 实现＝原版两个 Cities 卡的合体 ──────────────────────────────────────
    ///
    ///   - 收卡/售卖判定对齐 `ExportCenter`（出口贸易中心）：`CanHaveCard` 只收"整叠都可卖"的卡
    ///     （`WorldManager.CardCanBeSold` + `AllChildrenMatchPredicate`），`CanSelectOutput => true`
    ///     给输出方向箭头；`CanHaveCardsWhileHasStatus => true` 让带计时器的集市照常收卡。
    ///   - 卖法对齐原版 `Market` 的 `SellWithMarket`，但**一次卖一整条叠**（用户要求"参考炖锅"）：
    ///     `WorldManager.SellCard(pos, 整条子叠, 倍率)` 本来就会按整叠总价值结算并销毁整叠
    ///     （反编译核对：`GetStackValue(card)` + `DestroyStack(card)`），主大陆产金币、Cities 产纸币，
    ///     产出的钱币用 `StackSendCheckTarget` 沿运输输出送走（没接管道就弹到旁边 / 被附近箱子收走）。
    ///   - 3 个运输输入 + 1 个运输输出由 `Cards/advanced_market.json` 的 `_EnergyConnectors` 声明
    ///     （CardDirection 0=input / 1=output，ConnectionType 5=Transport），连接资源箱的运输输出即可进货。
    ///   - 混叠：材料自身不允许叠在别的材料上（木头不收苹果），会让"第二堆卡"进不了集市——
    ///     `AdvancedMarketStackPatch`（见 Patches）放行"已经叠在集市上的卡"，让不同种类的可卖卡
    ///     能在集市上摞成一摞，到点一次全卖。
    ///
    /// 售卖倍率 `_SellMultiplier` 默认 1（原版集市是 60 秒卖一张、双倍价；这里是 10 秒卖一整摞、原价）。
    /// </summary>
    public class AdvancedMarket : CardData
    {
        /// <summary>两次自动售卖之间的间隔（秒）。</summary>
        public float SellInterval = 10f;

        /// <summary>售卖价格倍率（1 = 按卡牌原价；原版集市是 2）。</summary>
        public float SellMultiplier = 1f;

        /// <summary>外观要镜像的原版卡 id（默认原版集市 `market`——0.5.21：卡牌配色/图标照原版集市的粉色）。</summary>
        public string VanillaLookCardId = "market";

        /// <summary>售卖倒计时的状态文字（本地化 term，见 localization.tsv）。</summary>
        [Term]
        public string SellStatusTerm = "stacklandspro_advanced_market_status";

        private bool triedLoadingVanillaLook;

        public override bool DetermineCanHaveCardsWhenIsRoot => true;

        public override bool CanHaveCardsWhileHasStatus()
        {
            return true; // 带计时器也要能收卡（同原版 Market/高级温室）
        }

        protected override bool CanSelectOutput()
        {
            return true; // 输出方向箭头（同原版 ExportCenter）
        }

        protected override bool CanHaveCard(CardData otherCard)
        {
            // 图鉴/蓝图里的演示卡没有 MyGameCard：只要卡本身有卖价就收
            if (otherCard.MyGameCard == null)
            {
                return otherCard.Value > 0;
            }
            // ExportCenter 同款：这张卡可卖，且它带着的整叠都要可卖
            if (!WorldManager.instance.CardCanBeSold(otherCard.MyGameCard))
            {
                return false;
            }
            return otherCard.AllChildrenMatchPredicate(
                (CardData x) => WorldManager.instance.CardCanBeSold(x.MyGameCard));
        }

        public override void UpdateCard()
        {
            ApplyVanillaMarketLook();
            // ⚠️ 0.5.20 修"多张卡叠上来不读条"：这里**必须**用 checkStatus: false。
            // `CardCanBeSold` 内部会用 `GetCardWithStatusInStack()` 从**根卡**开始找计时器——
            // 集市自己的读条一开始跑，第二张卡叠上来再按默认 checkStatus:true 检查，
            // 就会查到集市的计时器 → 判为"不可卖" → CancelTimer → 下一帧又 StartTimer 重置 →
            // 读条永远走不完（这就是"单张能读条、多张不读条"的根因；0.5.19 首版多写的
            // AllChildrenMatchPredicate 也是同一个坑，它默认 checkStatus:true，且
            // CardCanBeSold 本来就按整叠检查，纯属多余，已删）。
            GameCard child = MyGameCard.Child;
            bool canSell = child != null && child.CardData != null
                && (!MyGameCard.HasParent || MyGameCard.Parent.CardData is HeavyFoundation)
                && WorldManager.instance.CardCanBeSold(child, checkStatus: false);

            if (canSell)
            {
                // 原版 Market 同款：条件满足时每帧 StartTimer（已在跑就是空操作），不满足就撤掉
                MyGameCard.StartTimer(SellInterval, SellEverything,
                    SokLoc.Translate(SellStatusTerm), GetActionId("SellEverything"));
            }
            else
            {
                // 整叠里混进了不可卖的卡（或叠被拿空）：撤掉计时，等玩家处理
                MyGameCard.CancelTimer(GetActionId("SellEverything"));
            }
            base.UpdateCard();
        }

        /// <summary>
        /// 到点：把堆在集市上的**整条叠**一次性卖掉，钱币沿运输输出送走。
        ///
        /// ⚠️ 0.5.21 修"只卖了最下面一张"：之前对齐原版 ExportCenter 先 `child.RemoveFromStack()`
        /// 再卖——但 `RemoveFromStack()` 是 `SetParent(null); SetChild(null)`，会把这张卡从整条叠里
        /// **切出来单卖**（上面的卡失去挂点被弹开）。ExportCenter 那样写是因为它一次只卖一张、
        /// 还要把下一张接回集市。现在改成 `child.SetParent(null)`（只摘掉集市这一侧、保留下面的叠）
        /// 再整条结算；`WorldManager.SellCard(pos, card, …)` 本来就按 `card` 起的**整条叠**结算
        /// （`GetStackValue` + `DestroyStack` 沿 Child 链走）。
        /// </summary>
        [TimedAction("stacklandspro_advanced_market_sell")]
        public void SellEverything()
        {
            GameCard child = MyGameCard.Child;
            if (child == null)
            {
                return;
            }
            // 先把整条叠从集市上摘下来（SetParent(null) 会清掉集市侧的 Child 指针、
            // 但保留 child 自己下面的叠），再整条结算销毁——不留悬挂引用
            child.SetParent(null);
            GameCard money = WorldManager.instance.SellCard(Position, child, SellMultiplier, checkAddToStack: false);
            if (money != null)
            {
                money.RemoveFromParent();
                WorldManager.instance.StackSendCheckTarget(MyGameCard, money, OutputDir);
            }
        }

        /// <summary>
        /// 外观镜像原版集市：mod 卡加载时机早于原版卡的调色板就绪（和磁力信用卡同一类时序问题），
        /// 所以在第一次 UpdateCard 时从原版预制体把 `HasUniquePalette` / `MyPalette` / `Icon`
        /// 拷过来再 `UpdateCardPalette()` / `UpdateIcon()` 刷一次（写法同 `MagnetCreditcard`）。
        /// </summary>
        private void ApplyVanillaMarketLook()
        {
            if (triedLoadingVanillaLook)
            {
                return;
            }
            triedLoadingVanillaLook = true;

            string sourceId = string.IsNullOrEmpty(VanillaLookCardId) ? "market" : VanillaLookCardId;
            CardData vanilla = WorldManager.instance.GetCardPrefab(sourceId);
            if (vanilla == null)
            {
                Debug.LogWarning("[StacklandsProMax] 找不到原版集市 " + sourceId + "，高级集市将使用默认外观。");
                return;
            }

            // ⚠️ IsBuilding 也要抄：原版卡牌配色不只看调色板——ColorManager 的规则表里
            // 「Structures && IsBuilding」＝建筑粉、「Structures && !IsBuilding」＝结构深灰，
            // 原版集市的粉色来自 Building 规则（0.5.21 首版漏抄它，高级集市一直是深灰）。
            IsBuilding = vanilla.IsBuilding;
            HasUniquePalette = vanilla.HasUniquePalette;
            MyPalette = vanilla.MyPalette;
            Icon = vanilla.Icon;

            if (MyGameCard != null)
            {
                MyGameCard.UpdateCardPalette();
                MyGameCard.UpdateIcon(); // 配色在创建时才算一次要手动刷；Icon 同理，改完立即重画卡面
            }
        }
    }
}
