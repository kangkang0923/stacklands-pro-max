# 设计（实现契约）

> 本文档不含进度（见 `MEMORY.md`），不含理由（见 `DECISIONS.md`）。
> 迁移自旧《功能说明》《开发与交接说明》与旧《磁力系列卡牌》（后者已并入本文的调参 / 代码位置 / 口径对照三节），实现细节逐字保留；磁力系列卡牌的**玩法需求**见 `REQUIREMENTS.md`。

## 技术栈

- 平台：Stacklands Steam 版（含 Cities_DLC、Spirits_DLC）；游戏自带 mod 加载器，**不需要 BepInEx**。
- 语言/框架：C#，目标框架 `netstandard2.1`，.NET SDK 6.0.428（0 警告 0 错误）。
- 补丁：Harmony（随游戏的 HarmonyX）；补丁类在 `Mod.Ready()` 中统一 `PatchAll`。
- 数据：`Cards/` `Blueprints/` `Boosterpacks/*.json`（字段 `_Xxx` 映射到卡片脚本字段）+ `localization.tsv`（Term / Notes / English / Chinese 四列）。
- 入口：`Mod.cs` 的 `StacklandsProMax : Mod`（命名空间 `StacklandsProMaxNS`），一个 mod 只能有一个 `Mod` 子类。

## 文件地图

```
..\stacklands-pro-max\
├─ Mod.cs                    Mod 入口（StacklandsProMax : Mod）+ 磁力箱类 MagnetChest + **磁力容器类 MagneticContainer（0.6.0，城市专属）** + 硬币磁力箱类 CoinMagnetChest + 贝壳磁力箱类 ShellMagnetChest + 磁力信用卡类 MagnetCreditcard + 高级集市类 AdvancedMarket
├─ ModConfig.cs              ★ 游戏内设置界面的 41 项（8 卡总开关 + 20 功能 + 12 数值输入框 + 1 文本）+ 卡池/图鉴联动（含给 transpiler 用的运行时取值）
├─ ModSettings.cs            统一参数表（堆叠上限、开包间隔、村民边框颜色、双击归堆三个参数）
├─ Patches                  ★ 一个功能一个文件，79 个补丁类（见第四节）
│   ├─ StackLimitPatch.cs            堆叠上限 30→100（0.5.8 曾 999，0.5.14 定 100）（transpiler，运行时读开关）
│   ├─ MagnetTargetPatch.cs          磁铁/四种磁力卡（三种箱 + 信用卡）"截胡"入口（含原版磁铁上限）
│   ├─ StackDistancePatch.cs         自动归堆搜索距离 2→可调（StackSend + TrySendToChest 两个 transpiler）
│   ├─ MagnetChestLookPatch.cs       磁力箱外观：补原版储物箱的方形内容框
│   ├─ ChestMagnetCraftingPatch.cs   放行"资源箱/硬币箱/贝壳箱/信用卡叠到磁铁上"（否则配方做不出）
│   ├─ BoxSelectPatch.cs             框选整组搬运（含"点牌堆任意位置也能整组拖"的改写）
│   ├─ IdeaSearchPatch.cs            Alt+点击搜索主意（左键＝标题、右键＝描述；侧边栏在任务页时自动跳主意页），迁移 Extra Search Mod
│   ├─ SidebarHeightPatch.cs         左侧信息栏自动加高 + 不吃鼠标射线（迁移 HigherSidebar，0.5.6）
│   ├─ InfoDetailPatch.cs            ★ 详细信息（12 个补丁点：战斗详解/掉落百分比/卡包内容/装备/波次/牌堆描述/岛上贝壳金币），迁移 BetterInfo，0.5.6
│   ├─ ChestEject.cs                 「把一叠卡弹到旁边的空地」的共用弹射（资源箱点击取出 / 食物容器取出 / 磁力箱点击与拖出 / 松手回弹 共用；落点逐个候选点试"这一格没有别的卡"，0.6.7）
│   ├─ ChestDropRedirectPatch.cs     Ctrl/Shift 取牌后"只点一下"松手时别落回箱子里（改朝旁边弹）+ 记录"这叠从哪个箱子取出"和拖动偏移；拖出去再放回箱子的照常收下（0.5.17，文件内含 ChestTakeOutTickPatch）
│   ├─ AutoPausePatch.cs             自动暂停：读档后 + 换地点后（迁移 Auto Pause，3 个补丁类）
│   ├─ ConflictRowsPatch.cs          战斗多行排列（迁移 Lopi's Conflict Rows，3 个 Prefix + 私有方法反射）
│   ├─ RightDragStackPatch.cs        右键拖动（整叠 + 单张）+ 右键不被原版抢抓
│   ├─ DoubleClickRestackPatch.cs    双击同种卡自动归堆（合并 Better Mouse Controls）
│   ├─ SuperWarehousePatch.cs        高级仓库 +140 上限 + 仓库/高级仓库互相叠放（**界面名 0.5.6 从"超级仓库"改成"高级仓库"，id/代码名不动**）
│   ├─ HotpotCapacityPatch.cs        火锅（炖锅）食物容量改 int.MaxValue（参考 Stacks n Capacity，0.5.7）
│   ├─ BreedingPenPatch.cs           动物留在饲养栏：繁殖后亲代不弹出去、自动连着繁殖（参考 Stacks n Capacity，0.5.7）
│   ├─ ResourceChestCountPatch.cs    资源箱内的资源不按数量计入卡牌上限（Postfix + TargetMethod 显式绑定，参考 Stacks n Capacity，0.5.8 重写）
│   ├─ ResourceChestUpgradePatch.cs  资源箱对齐磁力箱：容量 10000 + 收卡类型扩到磁力箱白名单（0.5.8）
│   ├─ VanillaContainerCapacityPatch.cs 硬币箱/贝壳宝箱 10000 + 信用卡 100000（排除磁力子类，0.5.8）
│   ├─ FeedingPriorityPatch.cs       月末吃饭优先级：先吃炖锅、不吃叠在高级温室上的（Prefix 接替 `EndOfMonthCutscenes.GetFoodToUseUp`，0.5.9）
│   ├─ DebugScreenLocalizationPatch.cs K+O+F1 调试面板汉化：`label_debug_*` 词条注入（SokLoc.get_CurrentLocSet 后置）+ 硬编码英文按钮按表替换（DebugScreen.Start 后置，仅简体中文，2 个补丁类，0.5.9）
│   ├─ MagicCollector.cs             ★ 魔法收集器的卡片脚本 MagicCollectorCard：魔法师叠上去 30 秒产 1 个魔法尘埃 + 采石场同款输出方向（复用高级温室的箭头借用，0.5.8）
│   ├─ AdvancedGreenhousePatch.cs    高级温室：把"高级温室 × 6 个种子 → 收成 ×6"的生长配方追加进原版 `blueprint_growth`；另含"温室可互相叠放"（2 个补丁类，0.5.24）
│   ├─ QuickBuyPackPatch.cs          ★ Ctrl+点击购买箱快速购买卡包：散币优先→箱子/信用卡补足（Draggable.Clicked 前缀，0.5.16）
│   ├─ TooltipPositionPatch.cs        悬浮提示永远显示在鼠标右上方（原版长文案翻左边且截断，0.5.18）
│   ├─ AdvancedMarketPatch.cs         高级集市：放行不同种类可卖卡摞在集市上（CanHaveCardOnTop 后置，0.5.19）
│   ├─ AdvancedGreenhouseOutput.cs   高级温室输出：卡片脚本 `AdvancedGreenhouseCard`（借原版"输出方向"箭头）+ 产物送进旁边容器（1 个补丁类）
│   ├─ OpenWholePackPatch.cs         一次点开整个卡包
│   ├─ NoPortalAfterWitchPatch.cs    女巫死后关随机传送门
│   ├─ IdleVillagerBorderPatch.cs    空闲村民 + 空闲生产设施（菜园/农场/钓鱼点/捕鱼陷阱/温室/便便）红色边框
│   └─ CardGatePatch.cs              ★ 关掉的卡：配方不成立（每张卡一个总开关）
├─ Cards\                    magnet_chest.json / coin_magnet_chest.json / shell_magnet_chest.json / magnet_creditcard.json / super_warehouse.json / advanced_greenhouse.json / magic_collector.json / advanced_market.json / magnetic_container.json / advanced_recycling_center.json
├─ Blueprints\               上面对应的 10 张主意卡 JSON（*_idea.json）
├─ localization.tsv          中英文本地化（表头 1 行 + 127 条 term：卡牌 + 设置界面 + 界面按钮）
├─ build.ps1                 ★ 编译 + 部署（替代模板的 build.py，无需 Python）
├─ icon.png                  ★ Steam 创意工坊缩略图（512×512；作为发布资源被 build.ps1 同步进 Mods 目录）
├─ verify.ps1                ★ 不上游戏就能跑的离线校验（本地化/JSON/补丁注解/字段存在性）
├─ rename-mod.ps1            一键改 Mod ID / 名字（本工程已是最终名，基本用不到）
├─ （schemas\ 已移至 ..\..\stacklands-mod-common\schemas，.vscode 配置与各 JSON 的 $schema 已指向那里）
├─ .vscode\                  Schema 关联 + 推荐扩展
└─ 文档                      AGENTS.md（AI 入口）/ README / docs\（需求 / 设计 / 计划 / 状态 / 决策 / 发布指南）/ CHANGELOG；通用开发知识在 ..\..\stacklands-mod-common\
```

`Icons/`、`Sounds/`、`Boosterpacks/` 目录是**空的**（只放 `.gitkeep`）：七张卡都借用原版图标
（`"icon": "resource_chest"` / `"coin_chest"` / `"shell_chest"` / `"creditcard"` / `"warehouse"`），想换成自制图就把 png 放进 `Icons/` 并改 JSON 的 `icon`。
（`icon.png` 是工坊缩略图，和 `Icons/` 里的卡牌图标是两回事，别放混。）


## 注册与初始化（`Mod.Ready()`）

```csharp
public override void Ready()
{
    Logger.Log("Stacklands Pro Max is ready!");
    GameDataLoader loader = WorldManager.instance.GameDataLoader;
    // 每张卡的创意进卡包；逐个 try/catch（没装 Cities DLC 时 Cities_Ideas 卡包可能不存在）
    foreach ((string CardId, SetCardBagType BagType) entry in CardBagEntries)
    {
        loader.AddCardToSetCardBag(entry.BagType, entry.CardId, 1);
        ModConfig.RememberCardBag(entry.CardId, entry.BagType, 1); // 记一笔，卡总开关靠它重建卡池
    }
    ModConfig.Init(this);        // 注册游戏内设置界面的开关（必须在打补丁前：补丁会读这些开关）
    ModConfig.OnCardBagsReady(); // 按开关刷新一次抽卡池（关掉的卡立刻抽不到）
    ApplyPatches();              // 逐个 PatchAll + try/catch，单个失败只写日志
}
```
（`OnDestroy()` 里 `Harmony.UnpatchSelf()`。）

`CardBagEntries` 里登记的 16 条（9 张卡；每张卡进 1~2 个卡包）：

| 卡包 | 加了什么 |
| --- | --- |
| `AdvancedBuildingIdea` | 磁力箱创意、硬币磁力箱创意、贝壳磁力箱创意、高级仓库创意、高级温室创意、魔法收集器创意、高级集市创意 |
| `Cities_Ideas` | 磁力箱创意、硬币磁力箱创意、贝壳磁力箱创意、**磁力信用卡创意（0.6.2 起只进城市包）**、高级温室创意、魔法收集器创意、高级集市创意、**磁力容器创意（0.6.0）**、**高级回收中心创意（0.6.2）** |

> 磁力容器**只进 `Cities_Ideas`**：它的卡与创意都被标成 `CardUpdateType.Cities`（`Mod.Ready()` 里的
> `MagneticContainer.ApplyCitiesCardType()`），而主大陆的主意列表只显示 `!= Cities` 的知识，塞进
> `AdvancedBuildingIdea` 也看不到，反而会让卡包抽到一张"抽到就消失"的创意。


## 补丁清单（79 个类）

| 补丁类 | 目标 | 类型 | 作用 |
| --- | --- | --- | --- |
| `StackLimitPatch` | `CardData.CanHaveCardOnTop` | Transpiler | 把唯一硬上限常量 30 换成 `ModConfig.GetMaxStackSize()`（0.5.8 曾 999，0.5.14 起为 100，运行时可关） |
| `MagnetTargetPatch` | `WorldManager.TrySendToMagnet` | Prefix | ①原版磁铁上限放开到堆叠上限值（0.5.14 起 100） ②让磁力箱 / 硬币磁力箱 / 贝壳磁力箱 / 磁力信用卡参与"截胡"，实现**产出即被吸走** |
| `StackSendDistancePatch` | `WorldManager.StackSend` | Transpiler | 自动归堆搜索距离 `2f` → `ModConfig.GetStackSearchDistance()`（默认 4） |
| `TrySendToChestDistancePatch` | `WorldManager.TrySendToChest` | Transpiler | 自动"送进附近箱子"的搜索距离，用同一个值 |
| `MagnetChestLookPatch` | `GameCard.SetColors` | Postfix | 给磁力箱补 shader `_SecondaryTex`/`_HasSecondaryIcon`（原版储物箱那个方形内容框） |
| `ChestMagnetCraftingPatch` | `ResourceMagnet.CanHaveCard` | Postfix | 放行"资源箱 / 硬币箱 / 贝壳箱 / 信用卡叠到磁铁上"，四个配方才做得出来 |
| `RightDragStackPatch` | `WorldManager.Update` | Postfix | 右键按住拖动（自接管拖根卡，松手调原版 `DropCard()`） |
| `RightDragInputPatch` | `InputController.GetInputBegan` | Postfix | 阻止原版在右键按下时抢抓"鼠标下那一张" |
| `DoubleClickRestackPatch` | `WorldManager.Update` | Postfix | 双击把附近同种卡归成一堆（上限跟随堆叠上限开关） |
| `SuperWarehouseCapacityPatch` | `WorldManager.CardCapIncrease` | Postfix | 每个高级仓库 +140 场地卡牌上限 |
| `AdvancedGreenhouseGrowthPatch` | `BlueprintGrowth.Init` | Postfix | 高级温室：把每条"种子 × 普通温室"的生长配方复制成"**高级温室 + 1 个种子** → **产物 ×1、15 秒**"追回原版生长蓝图（0.5.10 起的口径，原为 6 种子/6 份/原版时长；**不消耗任何卡**，故种子留在温室上、每轮自动循环；幂等；`Mod.Ready()` 里也会调一次，防止 Init 时序不同） |
| `AdvancedGreenhouseOutputPatch` | `WorldManager.StackSendCheckTarget` | Prefix | 高级温室：产物送进旁边 `CanHaveCardOnTop` 认可的容器（配套箱子/炖锅…，优先输出方向那个、半径 `_OutputRadius`）；没有可放或已接管道就放行交回原版 |
| `WarehouseStackingPatch` | `CardData.CanHaveCardOnTop` | Prefix | 放行"仓库 + 仓库"、"仓库/高级仓库"任意两张互相叠放（配方要把 10 张叠成一叠；0.5.7 起高级仓库也纳入） |
| `GreenhouseStackingPatch` | `CardData.CanHaveCardOnTop` | Postfix | 放行"**普通温室 ← 普通温室**"（0.5.24 修用户报的 bug）：原版 `Greenhouse.CanHaveCard` 只收 Resources/Humans/Food，温室自己是 Structures，于是「6 张温室 → 高级温室」永远凑不齐。只在原版已返回 false 时补这一种组合（其余判定原样保留）；堆叠上限自己按 `ModConfig.GetMaxStackSize()` 再算一遍（原版那个上限就写在被改写返回值的方法里）；**不放行高级温室**（不算进 6 张配方，且会顶掉循环种子） |
| `HotpotCapacityPatch` | `Hotpot.UpdateCard` | Postfix | 火锅容量改为最大值（0.5.7）：反射写私有字段 `Hotpot.MaxFoodValue`（原版 50）为 `int.MaxValue`，每帧兜底（游戏/其它 mod 改回去也会被顶回来）；开关关闭时**只把等于 int.MaxValue 的退回 50**，不踩别的 mod 设的值（如 Stacks n Capacity 的 10000） |
| `BreedingPenKeepAnimalsPatch` | `BreedingPen.BreedAnimals` | Prefix（接管，0.5.7） | 动物留在饲养栏：生成幼崽 + 沿输出方向送出 + 触发任务进度后直接返回，**不再执行原版"把两只亲代弹出去"的两段**；亲代留在栏上（`ChildCount == 2`）→ `UpdateCard` 自动重启 120 秒繁殖计时＝一对动物持续繁殖。实现参照 Stacks n Capacity 的 `Patch_BreedingPen` |
| `ResourceChestNoCardCountPatch` | `WorldManager.GetCardCount()`（**无参重载**，`TargetMethod()` 显式绑定） | Postfix（0.5.8 重写） | 资源箱内的资源不按数量计入卡牌上限：原版在这里给 ResourceChest 特判 `num += (CurrentBoard.Id == "cities") ? 1 : ResourceCount`——主大陆/岛上按「1 + 箱内数量」计数、Cities 固定算 2。Postfix 把每个被计入的箱子**减回到 1 张**（非 Cities 减 `ResourceCount`、Cities 减 1；减前复刻原版计入条件：同场地、未装备、不在启航的船上）＝**只算箱子本身，同磁力箱口径**；开关运行时读，关掉立即恢复原版。⚠️ 前两版静默失效的根因：`GetCardCount` 有两个无参重载（普通 + 泛型 `GetCardCount<T>()`），binder 抛 AmbiguousMatchException → PatchAll 失败；现在 `TargetMethod()` 手动遍历选「0 参数、非泛型」方法，并在 F3 日志打「资源箱计数修正已生效」可验证（详见本文 17.4） |
| `OpenWholePackPatch` | `Boosterpack.Clicked` | Prefix+Postfix | 点击后 0.05s 链式再点，直到开完（含同包去重） |
| `ResourceChestMaxCapacityPatch` | `ResourceChest.UpdateCard` | Postfix（0.5.8） | 资源箱存储上限改 `int.MaxValue`（公开字段，原版 100），每帧兜底；关掉只把等于 int.MaxValue 的退回 100（不踩 Stacks n Capacity 的 10000） |
| `ResourceChestMoreTypesPatch` | `ResourceChest.CanHaveCard` | Postfix（0.5.8） | 收卡类型扩到磁力箱白名单（材料 + 食物 + 装备，排除金币/贝壳/便便/纸币；0.5.15 起磁力箱白名单放行便便、这里有意照旧排除便便；`HeldCardId` 一箱一种照旧）。**用 Postfix 全量重算而不是 Prefix**：Stacks n Capacity 也 Prefix 接管了这方法，HarmonyX 里所有 Prefix 都会执行、结果取决于谁后写入；Postfix 一定最后跑，我们的白名单就是最终结果 |
| `ChestCapacityPatch` | `Chest.UpdateCard` | Postfix（0.5.8） | 硬币箱/贝壳宝箱（同一个 `Chest` 类）存币上限改 **10000**（原版 100），每帧兜底。⚠️ 排除本 mod 的 `CoinMagnetChest`/`ShellMagnetChest` 子类（它们自己设 `int.MaxValue`，不能被钳） |
| `CreditcardCapacityPatch` | `Creditcard.UpdateCard` | Postfix（0.5.8） | 信用卡存钞上限改 **100000**（原版 1000），每帧兜底。⚠️ 排除本 mod 的 `MagnetCreditcard` 子类（无限容量） |
| `FeedingPriorityPatch` | `EndOfMonthCutscenes.GetFoodToUseUp`（私有静态，`TargetMethod()` 绑定） | Prefix 接替（0.5.9） | 月末吃饭的食物选择：① **先吃炖锅**——原版排序键 `-100 + FoodValue` 在"火锅 ≤ 50"前提下写的，本 mod 容量 10000 后键值变成大正数、炖锅反而最后吃，改成 `-100 + Math.Min(FoodValue, 50)` 恢复原意；② **不吃叠在高级温室上的食物**——那"种子"是自动循环的生产原料（0.5.10 起是 1 个），堆里含高级温室的直接从候选里排除（别的吃光了宁可饿着）。排序键写在 `OrderBy` 的闭包 lambda 里、transpiler 够不着 → 对齐原版逐行重写只动这两处（同 `MagnetTargetPatch` 的接替做法）。⚠️ 方法随游戏版本从 `Cutscenes` 挪到了 `EndOfMonthCutscenes`（本地反编译产物是旧版），`TargetMethod()` 优先绑新类、回退旧类 |
| `DebugScreenTermInjectionPatch` | `SokLoc.get_CurrentLocSet`（属性 getter） | Postfix（0.5.9） | K+O+F1 调试面板汉化（词条半）：官方中文列没翻 `label_debug_*` 全家（22 个词条 + 5 个每帧刷新的开关词条 + label_on/off，占位符 `[on_off]`），词表里有词条但 zh 为空 → Translate 回退英文。Postfix 在当前语言为简体中文时，把 27 条中文直接塞进 `TermLookup`/`AllTerms`（**覆盖**原版英文条目、静默）。⚠️ **不能用"词表里有没有某词条"当哨兵**——这些词条原版词表里本来就有（英文、zh 空），一查就误判成"已注入"导致永远不注入（0.5.9 首版就栽在这）；改成按 `LoadedLocSet` **实例**记 HashSet，每张词表只注入一次，语言切换重建词表（新实例）自然重新注入。**不用 mod 的 localization.tsv 覆盖**——ReadyUpMods 调 `LoadTermsFromFile` 没关警告，每个覆盖的原版词条都会打一条 LogError 刷日志；挂 getter 而不是挂事件是因为语言切换时词表整个重建，getter 保证任何时序都能拿到注入的词条。**仅简体**（繁体字体表可能缺简体字形） |
| `DebugScreenRawTextPatch` | `DebugScreen.Start` | Postfix（0.5.9） | K+O+F1 调试面板汉化（硬编码半）：面板上没挂 LocSetter 的写死英文（页签 Saves/Presets/Cutscenes、Save Game / Open Saves Directory / Clear Save / Make Debug Save / Select Save、Open Preset Directory / Make Board Preset、Search 占位符等）按英文原文查表替换成中文；**同一物体上有 LocSetter 的绝不碰**（归词条注入管）；存了原文，语言切走时逐个还原、切回简体重新替换（挂 `SokLoc.LanguageChanged`）。仅简体中文生效 |
| `QuickBuyPackPatch` | `Draggable.Clicked` | Prefix（0.5.16） | **Ctrl+点击购买箱快速购买卡包**：按住 Ctrl 左键点 `BuyBoosterBox` 时按原版 `CardDropped` 的扣款原语一键付清——场上散币优先（排除拖拽/飞行/带落点），不够从对应箱子/信用卡扣（含磁力箱系，离盒子近先扣），纸币凑整+找零对齐原版；付清后反射调私有 `CreateBoosterPack(null)` 并清 `StoredCostAmount`。⚠️ 挂 `Draggable.Clicked` 而不是 `Interactable.Click`（盒子在点击分发里走 `HoveredDraggable` 分支，后者根本不会被调到）；读键必须走 `InputController`（`UnityEngine.Input` 被剥离） |
| `TooltipPositionPatch` | `Tooltip.SetRectTransformToSafePosition`（静态） | Prefix 接管（0.5.18） | **悬浮提示永远显示在鼠标右上方**：原版依次试 4 个 pivot（(0,0)=右上 → (1,0)=左上…），长文案（「取出的牌往边上掉」「详细信息」等）从右上放不下就翻到左边，而且 `IsOverflowing` 量到的是旧文字的尺寸（text 同帧才赋值、布局还没算完），翻过去后**仍会截断**。这里 `LayoutRebuilder.ForceRebuildLayoutImmediate` 先算完布局，pivot 固定 (0,0)，右/上放不下就整体往回挪（右/上缘贴屏幕边缘）——绝不翻到左边。全局生效（设置界面/卡牌悬停/图鉴都走这一个方法），常开无开关。用户原话「有一部分文字就显示不出来了，正常的都是展示在右边的，你看一下其他的有没有往左边的，全部改成往右边」 |
| `ContainerCraftingStackPatch` | `CardData.CanHaveCardOnTop` | Postfix（0.6.0 / 0.6.1 加规则 B / 0.6.3 加规则 C、D） | **磁力容器配方堆**：A）放行「城市资源容器 `storage_container` 叠到 `iron_bar`/`copper_bar`/`science` 上」（原版材料类 `Resource.CanHaveCard` 拒绝建筑类叠上来）；B）放行「`educated_worker`/`genius`/`robot_worker`/`robot_genius` 叠到 `storage_container` 上」，**且只在容器不是堆底时放行**（容器当根会吞子卡 → 工人会被当材料吃掉）。**磁力信用卡配方堆（0.6.3）**：C）`creditcard` 可压到 3 张材料或 4 个教育型工人上；D）3 张材料 / 教育型工人可压回 `creditcard` 上（信用卡只吞纸币）。全部按 `ModConfig.GetMaxStackSize()` 复算总数，避免顺带捅穿堆叠上限 |
| `ContainerWorkerAbsorbGuardPatch` | `ResourceChest.UpdateCard` | Prefix（0.6.1） | **工人兜底**：容器变成堆底（或站在重型地基上）且上面压着受教育工人时，跳过这一帧的原版更新 → 原版吞卡循环不会把工人 `DestroyCard`。工人挪走后立刻恢复 |
| `AdvancedRecyclingCenterStackPatch` | `CardData.CanHaveCardOnTop` | Postfix（0.6.2） | **回收中心叠回收中心**：高级回收中心的配方要叠 5 张回收中心，而 `RecyclingCenter.CanHaveCard` 只收污染和工人。对齐 `WarehouseStackingPatch` 的思路，只放行「`recycling_center` ← `recycling_center`」这一种组合，并按堆叠上限复算总数 |
| `AdvancedRecyclingCenterBlueprintPatch` | `GameCard.StartBlueprintTimer` | Prefix（0.6.3 / 0.6.4 加硬限制） | **5 张回收中心叠好之后能不能读条**：0.6.4 起先查两条硬限制——堆里任何一张还接着 LV/HV 电、或堆里还有工人，就**不启动**（已在跑的立即取消）；条件满足才把 `skipWorkerEnergyCheck` 置真，绕过"堆底那张回收中心要 2 个工人 + 高压电全部通电"这两道方向相反的闸（`LoadModBlueprint` 不处理 JSON 的 `_字段`，`_IgnoreEnergyWorkerDemand` 写不进蓝图） |
| `ClaimedCardSendItPatch` | `GameCard.SendIt` | Prefix（0.6.4） | **已认领的卡不许被重新抛出**：有 `BounceTarget`（磁力卡/管道/目标牌堆）时跳过 `SendIt`，否则 Cities 场地会把速度改成 `OutputDir * 7f`（朝左），把"朝磁力卡飞"的飞行方向覆盖掉——信用卡吐钱"往左落地再消失"就是这条 bug |
| `AdvancedMarketStackPatch` | `CardData.CanHaveCardOnTop` | Postfix（0.5.19） | **高级集市混叠**：材料自身不允许叠在别的材料上（木头不收苹果），会让第二种可卖卡进不了集市。这里放行"根卡是高级集市、自己不是集市"的卡——任何可卖卡都能摞到集市已有的叠上；集市本体的收卡判定仍由 `AdvancedMarket.CanHaveCard`（ExportCenter 同款：整叠都可卖）把关。到点 `SellEverything` 按 `WorldManager.SellCard` 的整叠总价值一次结算 |
| `NoPortalAfterWitchPatch` | `EndOfMonthCutscenes.SpecialEvents` | Prefix | 女巫已死则关 `BoardOptions.CanSpawnPortals` |
| `IdleVillagerBorderPatch` | `GameCard.Update` | Postfix | 空闲村民 / **空着的生产设施**显示原版高亮框（红色）：村民＝`BaseVillager` + 未干活/未战斗/无父无子/未被拖；设施＝`Garden`/`FishingSpot`/`FishTrap`/`Greenhouse`/`Poop` + `Parent == null && Child == null`（合并 Show Idle Villager 两半，两个开关） |
| `CardGatePatch` | `CardData.FindMatchingPrint` | Postfix | 产物（或创意卡）被关掉时清空匹配结果 → 配方不成立 |
| `ConnectorDiagnosticsPatch` | `WorldManager.Start` | Postfix | 启动时把原版 `resource_chest` / `coin_chest` 与本箱的连接器配置打进 `F3` 日志（排查"三路运输输出"用） |
| `BoxSelectTickPatch` | `WorldManager.Update` | Postfix | 框选主循环：Ctrl+拖空白框选、选中后整组搬运（合并 Multi Select & Drag） |
| `BoxSelectCardClickPatch` | `GameCard.Clicked` | Prefix | Ctrl 按住时挡掉卡片自己的点击（如箱子取物）；**Ctrl+点击没有选中动作**（增减选择已删） |
| `BoxSelectBoosterClickPatch` | `Boosterpack.Clicked` | Prefix | 同上，卡包（否则会顺手开包） |
| `BoxSelectCameraPatch` | `GameCamera.StartDragging` | Prefix | Ctrl+拖空白时挡掉原版的镜头平移 |
| `HitTextTagPatch` | `Combatable.CreateHitText` | Postfix | 按预制体引用标记"这是治疗数字"（给下面那个用） |
| `HitTextSetPositionPatch` | `HitText.SetPosition` | Postfix | 伤害/治疗数字改画在**卡牌正中**（合并 Hit Text To Center） |
| `ChestDragOutPatch` | `GameCard.StartDragging` | Prefix（`Priority.First`） | Shift 拖出 10 张 / Ctrl 拖出 1 张（**只作用在本项目的磁力箱**，挂着鼠标上）；同时**屏蔽原版「Shift+左键拖整叠」**（改抓玩家点中的那张）。**原版资源箱我们一张都不取**（会和 Stacks n Capacity 重复扣数出双份，见 本文「从箱子里"拖"出材料」11.4） |
| `BoxSelectStackTakeoverPatch` | `GameCard.StartDragging` | Prefix（`Priority.Last`） | 框选多张后**点牌堆任意一张**都能整组拖：把原版这次"拖子卡"改写成"抓这一叠的根卡"（子卡此时还没被拆出来，`GetRootCard()` 拿到的就是选择集里存的那张）。只在"选了不止一张 + 左键拖 + 还没被别人接管"时生效，见 本文「Ctrl+拖空白框选多张卡」 |
| `ResourceChestEjectPatch` | `ResourceChest.RemoveResources` | Prefix（接替原实现） | 取出的牌**改成"往边上掉"**（原版 `StackSend` 里的 `TrySendToChest` 会把它送回自己箱子），用的是共用弹射（0.5.8 起为 `EjectToPileOrSide`：2 格内有同种牌堆就飞过去叠上——照磁力箱取出的堆叠行为，没有才往旁边弹；**0.6.7 起落点逐个候选点验证"这一格没有别的卡"**，优先空地）。资源箱与城市资源容器（`storage_container`）都是 `ResourceChest`，一处改动两者都生效。同时把"这一叠 ← 哪个箱子"记进 `ChestTakeOut` 供下面那个补丁用（Stacks n Capacity 的拖出走的就是这个方法）。开关「取出的牌往边上掉」关掉即回原版。见 本文「0.5.5 另外两个新交互」 |
| `ChestDropRedirectPatch` | `WorldManager.DropCard`（私有） | Prefix | **Ctrl/Shift 取牌后"只点一下"松手时别落回箱子里**：松手这一帧原版会走 `CheckIfCanAddOnStack` → 按重叠把这一叠 `SetParent` 回源箱子（＝被吞回去）。这里若"被拖的正是刚取出的那一叠 **且** 它和源箱子还重叠着"，就跳过原版那套，自己做原版 `DropCard` 的最后两行（`StopDragging()` + `SetDraggingDraggableToNull()`）再 `ChestEject.EjectToSide()` 弹到旁边；**已经拖开的照旧交回原版**。用户原话「卡牌似乎是往正上方弹出的，出来后又被吸回去了，把弹出的方向改一下」。见 本文 12.1。**0.5.17 起**："拖出去又拖回箱子上松手"也交回原版（＝主动放回，箱子照常收下）——判据是拖动全程的最大鼠标偏移（松手点就在取出点旁边、位移≈0，只能逐帧采样），超过 0.4 格视为"动过"；用户原话「还没松开的时候，如果不需要了，可以直接放回磁力箱。目前是无法放回去的，会被直接弹开」 |
| `ChestTakeOutTickPatch` | `WorldManager.Update` | Postfix（0.5.17） | `ChestTakeOut.Tick()`：手里正拖着的"取出记录"逐帧采样鼠标离取出点的最大偏移，供 `ChestDropRedirectPatch` 松手时区分「点一下」（偏移≈0 → 弹到旁边）和「拖出去又放回来」（动过 → 交回原版收下） |
| `FoodWarehouseEjectPatch` | `FoodWarehouse.RemoveFood` | Postfix（0.6.7） | 原版**食物容器**点击取出食物时走 `StackSend`，兜底是 `SendIt()`＝随机方向甩出去（可能压回容器自己或旁边的牌堆上）。这里在取出之后用同一个 `ChestEject.EjectToPileOrSide` 改落点（2 格内有同种牌堆就叠上，没有就找空地）；运输输出（`checkOutput = true`）那条路不动。开关「取出的牌往边上掉」关掉即回原版 |
| `GameSpeedRememberOnMonthEndPatch` | `WorldManager.EndOfMonth` | Prefix（0.6.7） | 月结（换月亮 / 下一回合）前记下 `SpeedUp`（原版 `EndOfMonth` 结尾写死 `SpeedUp = 1f`） |
| `GameSpeedRememberOnBoardChangePatch` | `WorldManager.GoToBoard` | Prefix（0.6.7） | 换地图（城市 / 海岛 / 森林 / 主大陆任意两处之间）前也记一次 `SpeedUp`（原版在过渡回调里同样写死 `SpeedUp = 1f`） |
| `GameSpeedRestorePatch` | `WorldManager.Update` | Postfix（0.6.7） | 月结协程 / 过场动画 / 地图过渡全部结束后把速度放回原值；**暂停中**（＝「换地点后自动暂停」刚触发）不改 `SpeedUp`，而是改 `GameScreen.prePauseSpeed`，解除暂停后照样是上一回合的倍数；离开游戏/读档时不还原。缘由与实现细节见 `Patches\GameSpeedKeepPatch.cs` |
| `SendToChestHotkeyPatch` | `WorldManager.Update` | Postfix | 快捷键（默认 `F`，0.5.9 前为 `R`，可在设置界面改）把鼠标指着的卡/牌堆交给原版 `StackSend`：被能收下它的箱子吸走，或飞到附近同类堆。见 本文「0.5.5 另外两个新交互」 |
| `SuppressVanillaTestFloatingTextPatch` | `WorldManager.CreateFloatingText` | Prefix | 屏蔽原版残留调试提示（0.5.25）：`WorldManager.Update` 里有一段 `GetKeyDown(Key.F)` → `CreateFloatingText(..., "Test hovered", Icons.Wellbeing, ...)` 的官方调试代码，只画绿笑脸浮字、不改数据；本功能默认键正好是 F，所以按 F 送卡时会一起冒出来。这里只挡 `descriptionText == "Test hovered"` 那一次调用（`__args[3]` 取文本，抗参数改名），其余浮字（月末结算/能源设施幸福度）原样放行 |
| `IdeaSearchKnowledgePatch` | `GameScreen.KnowledgeMatchesSearch`（私有） | Prefix | 主意搜索按模式匹配：`Vanilla` 放行原版，`Title`/`Description` 分别比 `KnowledgeName`/`KnowledgeText`（迁移 Extra Search Mod） |
| `IdeaSearchInputPatch` | `WorldManager.Update` | Postfix | **Alt+左键单击**卡＝按标题搜主意、**Alt+右键单击**＝按描述搜（Alt+点空白＝清空）：填 `IdeaSearchField.text` + `UpdateIdeasLog()` |
| `IdeaSearchCardClickPatch` | `GameCard.Clicked` | Prefix | Alt 手势按下时挡掉卡片自己的点击（别顺手取物） |
| `IdeaSearchBoosterClickPatch` | `Boosterpack.Clicked` | Prefix | 同上，卡包（别顺手开包） |
| `IdeaSearchButtonsPatch` | `GameScreen.Awake` | Postfix | 在搜索框旁加**三个固定按钮**「搜标题 / 搜描述 / 清除」（`Instantiate` 原版 `IdeasButton`；**不设 `TooltipText`**）。**字号用模板默认、宽度按标签文字自适应（`ButtonPadding = 8`）**，三按钮排一行合计 ≈380——实测 **≈380 正常、≈428 会把左侧面板撑宽**（用户定案就用这一版；想更多留白需小幅试探或改 2 列网格）。同时开局 30 秒内**扫掉 Extra Search Mod 自己那排按钮**（特征：正好两个 `CustomButton` 且文字是写死的 `Title`/`Description`/`Clear`）。整段 try/catch，见 本文 12.3 |
| `SidebarHeightUpdatePatch` | `GameScreen.LateUpdate` | Postfix | **信息栏自动加高**（迁移 HigherSidebar）：按 `Valuetext + InfoText + 72 + 激活分隔条` 算出目标高度写进 `InfoBox.sizeDelta.y`，再重排侧边栏；变化 <1px 不动、变矮有 0.1s 冷却。见 本文 13.1 |
| `SidebarHeightAwakePatch` | `GameScreen.Awake` | Postfix | 把 `InfoBox` 下所有 `Image`/`TextMeshProUGUI` 的 `raycastTarget` 关掉（信息栏变高后不挡点卡） |
| `InfoDetailCombatPatch` | `Combatable.GetCombatableDescriptionAdvanced` | Postfix | 战斗详解重写：攻击类型 + 攻速/命中/伤害/防御 + **估算每秒伤害**；动物补产出说明、Mob 补掉落表、敌人补"可能带的装备"（迁移 BetterInfo） |
| `InfoDetailPossibleDropsPatch` | `CardData.GetPossibleDrops` | Postfix | 普通 Mob（非 `Enemy`）也返回掉落表 |
| `InfoDetailStablePortalPatch` | `StablePortal.UpdateCard` | Prefix + Postfix | 传送门描述补"第 N 波" |
| `InfoDetailBoosterpackPatch` | `BoosterpackData.GetSummary` | Prefix（接管） | 卡包内容**带百分比**、每个 CardBag 一段、未发现卡合并成一行概率 |
| `InfoDetailHarvestablePatch` | `Harvestable.UpdateDescription` | Prefix（接管） | 地点/采集物掉落表带百分比；墓穴/洞穴/遗迹/古书/钓鱼点逐个特例 |
| `InfoDetailCombatableHarvestablePatch` | `CombatableHarvestable.UpdateCard` | Prefix | 会掉东西的战斗采集物同上 |
| `InfoDetailTravellingCartPatch` | `TravellingCart.UpdateCard` | Prefix | 旅行货车同上（不显示数量） |
| `InfoDetailCardopediaPatch` | `CardopediaScreen.GetDropSummaryFromCard` | Prefix | 掉落已写进描述 → 图鉴摘要不再重复 |
| `InfoDetailEquipablePatch` | `Equipable.GetAdvancedEquipableInfo`（私有） | Postfix | 装备攻击类型顶到最前面 |
| `InfoDetailInfoTextPatch` | `GameScreen.Update` | Postfix | ①卡包内容进信息栏 ②悬停牌堆显示该卡名字+描述 ③**岛上**侧栏显示"贝壳 + 金币" |
| `InfoDetailBlueprintPatch` | `Blueprint.GetText` | Postfix | 创意（蓝图）补产物的特殊效果与装备详解 |
| `InfoDetailAdvancedCombatPatch` | `AdvancedSettingsScreen.LoadSettings`（私有） | Postfix | 打开原版"详细战斗数值"选项（＝**战斗详解的总闸门**，`Combatable/BaseVillager.UpdateCardText()` 靠它决定要不要算高级数值）。它从 `PlayerPrefs` 读（本机默认关），而读它的界面是**懒创建**的 → 只挂那个界面 `Awake` 会完全失效（0.5.6 首次实测踩过）。现在 `ModConfig.Init()`（`Ready()` 阶段）兜底设一次 + 这里（真正读 pref 的方法之后）再设一次 |
| `AutoPauseLoadRoundPatch` | `WorldManager.LoadPreviousRound` | Postfix（0.5.9 改挂点） | 自动暂停（迁移 Auto Pause）：**加载上一回合存档时**记一个"下次进入游戏就暂停"的请求，真正的暂停在 `AutoPausePlayPatch`（`WorldManager.Play()` 后置，那时 `GameScreen.instance` 一定有效）。⚠️ 最早挂 `MainMenu.Awake` 给 ContinueButton 追加回调——但当前游戏版本是 `SceneBootstrapper.Awake` 里**先实例化各管理器（含主菜单 UI，Awake 连锁触发）最后才 ReadyUpMods()**，补丁生效时 MainMenu.Awake 早已跑完、该界面之后只 SetActive 切换不重建 → 订阅永远加不上（0.5.9 用户实测"返回菜单再继续，读档暂停失效"）。改挂 `LoadPreviousRound`：点「继续游戏」必经、新开局不走它不误暂停、调试强制重载也走它语义正好 |
| `AutoPausePlayPatch` | `WorldManager.Play` | Postfix | 自动暂停：真正进入游戏那一刻暂停（`Play()` 第一行已 `SetScreen<GameScreen>()`，所以 `GameScreen.instance` 一定有效）；新开一局不会误暂停 |
| `AutoPauseTransitionPatch` | `TransitionScreen.StartTransition(Action, TransitionType, float)`（**私有重载**） | Postfix | 自动暂停：把 `___onTransition` 包一层，换地点过渡结束后暂停；**只在 `CurrentBoard` 引用真的变了**且不在 `GameState.InMenu` 时暂停（否则剧情结束也会暂停） |
| `ConflictRowsPositionPatch` | `Conflict.GetPositionInConflict` | Prefix（接管） | 战斗多行（迁移 Lopi's Conflict Rows）：每张战斗卡的位置 —— 行内横向居中（最后一行不满按实际张数居中）、行号沿 Z 轴偏移，敌我朝相反方向排 |
| `ConflictRowsSizePatch` | `Conflict.GetConflictSize`（**私有**） | Prefix（接管） | 战斗多行：冲突框尺寸 —— 宽度＝两队"较宽那一行"，高度＝卡高 + 每多一行加一个 `HorizonalCombatOffset` |
| `ConflictRowsBoundsPatch` | `Conflict.GetBounds` | Prefix（接管） | 战斗多行：冲突框本体 —— 起始位置按两队"行数差"横向补偿，尺寸用上一条的结果。私有方法 `ClampStartPosition`/`GetConflictSize` 用 `AccessTools` + `MethodDelegate` 反射委托调用（本项目不能像原 mod 那样用 `IgnoresAccessChecksTo`），拿不到就整体放行回原版 |

**关闭某个功能**：两种方式，任选。
① **游戏内**（推荐，0.4.0 起）：主菜单 → **Mods** → 选中本 mod → 每个功能一个开关，点了**立即生效**（不用重启）。
② 改代码：把 `Mod.cs` 里 `PatchClasses` 数组对应那一行注释掉重新编译（连补丁都不打）。

### 游戏内设置界面（41 项：8 个卡总开关 + 20 个功能开关 + 12 个数值输入框 + 1 个文本框）

实现：`ModConfig.cs`。每个开关注册成一个 `ConfigEntry<T>`（`Mod.Config.GetEntry<T>(键, 默认值, new ConfigUI { NameTerm, TooltipTerm })`），
游戏会在 mod 界面里自动生成控件（`bool` → Yes/No 按钮，`int`/`float` → **数字输入框**（TMP_InputField），`string` → 文本输入框）；
UI 改写值会触发 `ConfigEntry.OnChanged`，我在那里更新静态字段，
**所有补丁和磁力箱逻辑都在运行时读这些字段**，所以改完立即生效。
（`StackLimitPatch` / `StackDistancePatch` 的 transpiler 也是调用 `ModConfig` 里的运行时取值，而不是塞死数字。）
0.5.18 起堆叠上限 / 各容量上限都是**数字输入框**——填回原版值（30/100/1000/50…）＝原版行为，
所以原来配对的"开/关"开关全部去掉了；三个货币磁力卡合并成一个「磁力货币箱」开关。

**8 个"每张卡一个总开关"**（关掉 = 这张卡"不存在"）：

| 设置项（config.json 键） | 界面名称 | 关掉之后会怎样 |
| --- | --- | --- |
| `Card: Magnet chest` | 卡牌：磁力箱 | 创意从卡包摘掉、图鉴隐藏、磁力箱配方做不出来；**旧存档里已有的磁力箱照旧能用** |
| `Card: Magnet currency chests` | 卡牌：磁力货币箱（0.5.18 合并） | **一个开关管三张卡**：硬币磁力箱 + 贝壳磁力箱 + 磁力信用卡。关掉后三张卡的创意都从卡包摘掉、图鉴隐藏、配方做不出来；旧存档里已有的照旧能用 |
| `Card: Super warehouse` | 卡牌：高级仓库 | 同上（高级仓库；**界面名 0.5.6 改名，config.json 的键名仍是不变的旧名**） |
| `Card: Advanced greenhouse` | 卡牌：高级温室 | 同上（高级温室） |
| `Card: Magic collector` | 卡牌：魔法收集器 | 同上（魔法收集器） |
| `Card: Advanced market` | 卡牌：高级集市（0.5.19） | 同上（高级集市） |
| `Card: Magnetic container` | 卡牌：磁力容器（0.6.0） | 同上（城市专属的磁力容器）。关掉后创意从 `Cities_Ideas` 摘掉、图鉴隐藏、配方做不出来；旧存档里已有的照旧能用 |
| `Card: Advanced recycling center` | 卡牌：高级回收中心（0.6.2） | 同上（城市专属）。关掉后创意从 `Cities_Ideas` 摘掉、图鉴隐藏、配方做不出来；旧存档里已有的照旧能用 |

**20 个功能开关 + 12 个数值输入框 + 1 个文本框**（卡总开关表见上）：

| 设置项（config.json 键） | 界面名称 | 关掉之后会怎样 |
| --- | --- | --- |
| `Stack limit` | 卡牌堆叠上限（**输入框**，默认 100） | 0.5.18 由开关改输入框：同一种卡最多叠多高（原版 30，原版磁铁上限跟着走）；**填 30 ＝原版行为** |
| `Stack search distance` | 自动归堆搜索距离（**输入框**，默认 4） | "多远"（单位＝场上格子）；0.5.18 起不再有「卡牌自动归堆更远」开关——**填 2 ＝原版行为** |
| `Open whole pack` | 点击一次开完整包 | 回到原版：一次点击只开一张 |
| `No portal after witch` | 女巫死后无随机传送门 | 回到原版：女巫死后月底仍可能刷奇怪传送门 |
| `Idle villager border` | 空闲村民红色边框 | 不再标红 |
| `Idle facility border` | 空闲生产设施红色边框 | 空着的菜园/农场、钓鱼点、捕鱼陷阱、温室、散落的便便不再标红（村民那条不受影响） |
| `Auto pause on load` | 读到存档后自动暂停 | 从主菜单「继续游戏」读到存档后不再自动暂停（新开一局本来就不会暂停） |
| `Auto pause on location change` | 切换地点后自动暂停 | 换场地（主大陆/岛/森林/Cities…）后不再自动暂停；剧情/其它过渡本来就不触发 |
| `Multi-row combat` | 战斗多行排列 | 回到原版：同一队的战斗卡只排一行（人多了横向拉长） |
| `Combat row size` | 每行战斗单位上限（数值，默认 7） | 一行最多放几张战斗卡（原 mod 默认 7）；只在「战斗多行排列」打开时起作用 |
| `Right-drag cards` | 右键拖动卡牌 | 右键不再拖动卡牌（右键拖空白处一直是平移镜头，不受影响）；打开着 Click to Drag 时也交回原版 |
| `Double-click restack` | 双击同种卡自动归堆 | 双击不再归堆（只是普通点击） |
| `Box select cards` | 框选多张卡 | Ctrl+拖空白不再框选（回到原版：Ctrl+拖空白仍是平移镜头） |
| `Hit text to center` | 数字显示在卡牌中间 | 伤害数字回到原版位置（压在卡片下沿）；**治疗数字始终居中**，不受此开关影响 |
| `Resource chest ejects sideways` | 取出的牌往边上掉 | 回到原版：取出的牌垂直弹起又落回箱子上（0.5.17 起"拖回去放回箱子"照常收下） |
| `Send card to chest hotkey` | 按快捷键把卡送往箱子 | 快捷键完全不响应（X 键那个是参考 mod 的，与本项无关） |
| `Send to chest key` | 送往箱子的快捷键（文本框，默认 `F`，0.5.9 前为 `R`） | 填 `Key` 枚举名（`R`/`X`/`F5`/`Space`/`Digit1`…，大小写不敏感）；认不出来回落到 `F` |
| `Quick buy pack on Ctrl+click` | Ctrl+点击快速购买卡包 | 按住 Ctrl 并左键点击购买箱（带价格的卡包预览盒）直接买下一包——付款优先用场上散落的金币/贝壳/纸币，不够自动从对应的箱子/信用卡（含磁力箱系）里扣。关掉后回到原版"把钱拖进购买箱"的方式 |
| `Idea search on Alt+click` | Alt+点击搜索主意 | Alt+左键/右键不再搜索主意（搜索回到原版：只匹配主意标题，并且只能手打搜索词） |
| `Sidebar: auto height` | 信息栏自动加高 | 回到原版：信息栏固定高度（约 300px），文字多了就被裁掉 |
| `Sidebar: min height` | 信息栏最小高度（数值，默认 300） | 自动加高的下限（像素）；原版就是 300 |
| `Sidebar: max height` | 信息栏最大高度（数值，默认 700） | 自动加高的上限（像素）；只在上面的开关打开时起作用 |
| `Detailed info` | 详细信息（战斗 / 掉落） | 关闭 = 12 个详细信息补丁点全部早退：战斗/装备描述、掉落百分比、卡包内容、岛上贝壳金币、原版"详细战斗数值"选项的强制打开**全部回到原版** |
| `Breeding pen keeps animals` | 动物留在饲养栏 | 回到原版：繁殖完成后两只亲代被弹出去，要重新抓回栏里才能继续繁殖 |
| `Resource chest cards don't count` | 资源箱不计入卡牌上限 | 回到原版：主大陆/岛上每个资源箱按「1 + 箱内数量」计入场地卡牌上限（打开时每个箱子固定只算 1 张＝箱子本身，同磁力箱） |
| `Resource chest more card types` | 资源箱可存更多卡牌类型 | 回到原版：资源箱只收资源类卡牌（打开时按磁力箱白名单：材料 + 食物 + 装备，仍一箱一种） |
| `Resource chest capacity` | 资源箱容量（**输入框**，默认 10000） | 0.5.18 由开关改输入框：原版资源箱最多存多少（原版 100）；**填 100 ＝原版行为** |
| `Coin/shell chest max coins` | 硬币箱/贝壳宝箱容量（**输入框**，默认 10000） | 0.5.18 由开关改输入框（原版 100）；**填 100 ＝原版行为** |
| `Credit card max dollars` | 信用卡容量（**输入框**，默认 100000） | 0.5.18 由开关改输入框（原版 1000）；**填 1000 ＝原版行为** |
| `Hotpot food capacity` | 火锅容量（**输入框**，默认 10000） | 0.5.18 由开关改输入框（原版 50）；**填 50 ＝原版行为** |
| `Magnet chest capacity` | 磁力箱容量（**输入框**，默认 10000） | 0.5.18 新增：磁力箱最多存多少材料，场上所有磁力箱立即生效 |
| `Coin/shell magnet chest capacity` | 硬币/贝壳磁力箱容量（**输入框**，默认 10000） | 0.5.18 新增：硬币磁力箱和贝壳磁力箱最多存多少枚，立即生效 |
| `Magnet credit card capacity` | 磁力信用卡容量（**输入框**，默认 100000） | 0.5.18 新增：磁力信用卡最多存多少面额，立即生效 |

> 0.5.8 新加的就是上面最后三项（资源箱容量 / 收卡类型 / 不占卡牌上限——后两项参考 Stacks n Capacity 的实现）。
> 0.5.6 新加的是信息栏自动加高 + 详细信息，做法与逐条差异见 本文「0.5.6 迁移：HigherSidebar / BetterInfo / Auto Pause / Conflict Rows」。
> 0.5.5 新加的是它上面那几项（资源箱取出方向 / 送往箱子开关 + 快捷键文本框 / Alt+点击搜索主意），
> 做法见 本文「0.5.5 另外两个新交互」。
> （0.5.1 曾有过「Shift 拖整叠」开关，0.5.3 按用户要求连同补丁一起删掉了，设置界面里没有这一项。）

**不再单独给开关、一律常开的 6 项**（0.5.0 按用户要求"每个新增的卡牌只留一个总开关，其他多余的开关去掉"）：
箱子自动吸附、取出后 10 分钟不被回吸、外观同原版、磁力箱/硬币磁力箱/贝壳磁力箱配方、高级仓库 +140、仓库/高级仓库可互相叠放。
它们在 `ModConfig.cs` 里是 **const = true** 的"开关"（保留命名，方便以后想关掉时改一处再编译），
不再出现在设置界面、也不再进 `config.json`。

**旧 config.json 的处理**：被去掉的键（`Magnet chest auto pull`、`Super warehouse capacity` 等）会**留在文件里但不再生效**（读不出来就忽略），
所以升级后设置界面只会显示上面这 31 项；想彻底清干净就删掉 `config.json`（各项回到默认：全开 + 距离 4 + 信息栏 300~700 + 每行 7）。

- 开关值持久化在 `Mods\StacklandsProMax\config.json`（形如 `{"Stack limit 999": true, ...}`）；
  想恢复默认就删掉这个文件（游戏会重新生成 `{}`，各项按默认 true / 4 处理）。
- **卡总开关为什么能在运行时生效**：抽卡池（`SetCardBagData.Chances`）是每次开卡包**现算**的，
  所以 `ModConfig.ApplyCardAvailability()` 直接改那张列表就够了（关掉的卡摘掉、重新打开的原样放回去；
  `Mod.Ready()` 用 `RememberCardBag` 记下了"每张卡原本在哪个卡包、权重多少"，所以反复开关不会丢失或重复）。
  图鉴走 `HideFromCardopedia`（产物 + 它的创意卡一起标）。
- 新增开关的步骤见第十节。


## 卡牌设计要点

### 磁力箱 `stacklandspro_magnet_chest`

- **可存**：`CardType.Resources` + `Food` + `Equipable`（白名单；排除 gold / shell / poop / 纸币 Dollar / 建筑 / 自身）。
  一个箱子只存一种：点击取 1（取空后类型自动清空）；**Shift+左键按住箱子＝拖出 10 张、Ctrl+左键＝拖出 1 张**
  （挂在鼠标上、松开才落地，这条不动类型；机制见 本文「从箱子里"拖"出材料」）。
- **吸附两条路**：① `TrySendToMagnet` 截胡（建筑一产出、卡还在空中就被改道进箱）② 场上散卡扫描（半径默认 999，间隔 0.1s）。
- **取出免疫**：`_RemovalImmunitySeconds` 默认 600（10 分钟）；免疫期内拿到"半径 × `_ImmunityKeepAwayFactor`"之外会提前解除
  （半径 999 时等于不会触发）；**堆叠状态永远不吸**。
- **外观**：运行时镜像原版 `resource_chest` 预制体的 `IsBuilding` / `HasUniquePalette` / `MyPalette` / `SpecialIcon`，
  并调 `UpdateCardPalette()` 刷新，做到与它一致。
- **容量**：`_MaxResourceCount` = **10000**（0.5.14 用户定值；历程：0.5.8~0.5.12 无限 → 0.5.13 原版 100 → 0.5.14 定 1 万）。
- **存档**：`[ExtraData("stacklandspro_resource_count")]` / `("stacklandspro_resource_id")`。
- **自愈**：若发现 `HeldCardId == 自己的 id`（0.2.1 及更早的"箱子互吞"bug 留下的坏状态），会清空类型并把被吞的箱子**如数归还**。
- **JSON 可调参数**：`_MaxResourceCount` / `_PullRadius` / `_PullInterval` / `_RemovalImmunitySeconds` / `_ImmunityKeepAwayFactor`。

### 磁力容器 `stacklandspro_magnetic_container`（0.6.0 新增，**城市场地专属**）

- **需求原话**：「在工业革命DLC，也就是 city 这个地图，加一种类卡片叫磁力容器，主意为：铁条，铜条，科学，资源容器，受教育工人。
  功能类似于主大陆的磁力资源箱，可以自动吸取卡片，一些操作快捷键也都参考磁力资源箱就行」。
- **实现＝磁力箱的城市版**：`MagneticContainer : MagnetChest`，吸附扫描 / `TrySendToMagnet` 截胡 / 取出 10 分钟免疫 /
  **Shift 拖 10、Ctrl 拖 1** / F 键送箱 / 取出往边上掉 / **3 路运输输出**全部继承，只覆写三处：
  ① `IsStorable`＝**对齐原版资源容器的收卡口径**（只收 `CardType.Resources`，城市里排除便便/铀，
  外加原版那条 `FoodValue ≤ 0` 的食物特例；**不收正常食物和装备**）；
  ② 外观镜像原版城市资源容器 `storage_container`（配色/角标/空图标）；
  ③ `WantsCard` 增加「**正在飞行的卡不抢**」。
- **配方**：铁条 `iron_bar` + 铜条 `copper_bar` + 科学 `science` + 资源容器 `storage_container` + 受教育工人
  `any_educated_worker`（5 秒，**工人不消耗**；`CardsToRemove` 只写前四张）。
- **摆放顺序**（0.6.1 起有两种都行）：
  ① 材料叠好 → **资源容器扣在最上面** → 再把**受教育工人叠到容器上**（`ContainerCraftingStackPatch` 规则 B，
     只在容器有父卡时放行，所以容器不会把工人吃掉）；
  ② 原来的顺序：**受教育工人先放最底下** → 铁条 / 铜条 / 科学依次叠上去 → 资源容器扣最上面。
  ⚠️ 唯一禁止的是「**资源容器垫在最底下**、再把材料往上拖」——容器当根卡时会把子卡当材料吞进肚子（原版行为）。
- **管道豁免**：`A 输出 → B 输入` 的运输链上的卡永远不被吸。原版送卡本身就"管道优先"
  （`StackSendCheckTarget` 先试 `TrySendWithPipe`，`StackSendTo` 覆盖磁力设的落点），这里再用
  `WantsCard` 显式拒绝"在飞的卡"，不依赖原版顺序。
- **城市配方/主意靠 `CardUpdateType.Cities`**：`Mod.Ready()` 里 `MagneticContainer.ApplyCitiesCardType()` 把卡与创意卡都标成
  Cities——否则 `FindMatchingPrint` 在城市场地会跳过 mod 蓝图（`CardUpdateType.Mod`），
  `UpdateIdeasLog` 也不会在城市显示它。**只对这张卡生效，其它 7 张卡维持原状。**
- **JSON 可调参数**：`_MaxResourceCount` / `_PullRadius` / `_PullInterval` / `_RemovalImmunitySeconds` /
  `_ImmunityKeepAwayFactor` / `_OutputInterval` / `_EnergyConnectors`（3 路）。决策依据见 [`DECISIONS.md`](DECISIONS.md)。

### 高级回收中心 `stacklandspro_advanced_recycling_center`（0.6.2 新增，**城市场地专属**）

- **需求原话**：「新增卡牌高级回收中心，由5个回收中心合成，原先的回收中心是每30秒清除10点污染，
  高级回收中心是每6秒清除10点污染，回收中心需要接1路下水道，高级回收中心需要5路。
  回收中心需要两个人，高级回收中心需要5个，回收中心有一个运输输出，高级回收中心也只有一个」。
- **实现＝继承原版 `RecyclingCenter`**（它本身就是 `Factory : EnergyConsumer : SewerCard`），只改四处（2b 为 0.6.4 的合成限制）：
  1. **周期 30 秒 → 6 秒**：原版把 `30f` 写死在 `RecyclingCenter.UpdateCard` 的 `StartTimer` 里。
     这里**不复写方法、不打 transpiler**：`base.UpdateCard()` 之后再补一次 `StartTimer(6f, DumpPollution, ...)`——
     原版 `StartTimer` 在 `TimerActionId` 相同的分支里**只把 `TargetTimerTime` 改小**（反编译核对），
     计时器结算看 `CurrentTimerTime >= TargetTimerTime`（24459）、进度条 fill 是
     `CurrentTimerTime / TargetTimerTime`（55747）→ 6 秒整点结算、进度条也对。
     计时器没在跑（工人/下水道不够、溢出中、拖拽中）时这段什么都不做，行为完全交回原版。
  2. **工人 2 → 5、高压电 1 → 3 路、下水道 1 → 5 路、运输输出 1 路**：全靠 JSON 字段，不写代码——
    `_WorkerAmount: 5` 由 `EnergyConsumer.UpdateCard` 的 `WorkerAmountMet()` 校验（不够就 `CancelAnyTimer`）；
    **`Sewer` 只算下水道、不算能源**（`CardConnector.IsEnergyConnector` 只认 LV/HV），所以两条各走各的校验：
    `_EnergyConnectors` 里 **3 路高压电输入**（`CardDirection 0` + `ConnectionType 3`）由 `HasEnergyInput()` →
    `AllInputConnectorsPowered()` 校验（每一路都要接上并通电）；
    **5 路下水道输出**（`CardDirection 1` + `ConnectionType 4`）由 `HasSewerConnected()` →
    `AllSewerOutputsFunctional()` 校验（它只认 `output` 方向，反编译核对），每一路都要接上；
    运输输出 1 路（`CardDirection 1` + `ConnectionType 5`），产物照原版沿 `OutputDir` 经 `StackSendCheckTarget` 送走（金属废料）。
    > 历程：0.6.2 把下水道误写成 `input` ×5（永远不被校验）；0.6.3 改成「高压电输入 ×5 + 下水道输出 ×5」；
    > 0.6.4 高压电 5 → 3；**0.6.6 定稿：高压电输入 3 路 + 下水道输出 5 路 + 运输输出 1 路**。
  2b. **合成时必须先"停摆"（0.6.4）**：堆里任何一张回收中心还接着 LV/HV 电、或还有工人（含已 `EquipWorker`
    上岗、挂在 `WorkerChildren` 上的），`AdvancedRecyclingCenterBlueprintPatch` 就不启动合成计时器。
    原版那两道闸（`ShouldStartTimerWorkers` 要 2 个工人、`ShouldStartTimerEnergy` 要通电）方向正好相反，
    所以判过之后强制 `skipWorkerEnergyCheck = true` 让路。
  3. **图标**：空/半满/满三个 Sprite 是原版 prefab 上的资产，JSON 填不了 → 运行时从原版 `recycling_center` 拷
     （`ApplyVanillaLook`，在 `OnInitialCreate` 与每帧 `UpdateCard` 开头各调一次，只拷一次）。名字/描述/溢出文案用自己的 term。
- **其余全部继承**：收污染卡（`CanHaveCard` 认 `pollution` 与 `Worker`）、存满溢出 + 120 秒结算、
  每周期产 1 个金属废料。溢出阈值 `_PollutionOverflowMin/Max` 默认 **200/300**（比原版宽松——清理快 5 倍，缓冲区给足）。
- **配方**：5 张 `recycling_center` 叠成一叠 → 20 秒 → 高级回收中心（5 张全消耗）。
  回收中心默认拒绝别的回收中心叠上来，所以需要 `AdvancedRecyclingCenterStackPatch` 放行这一种组合。
- **城市专属**：卡与创意都标 `CardUpdateType.Cities`（否则城市配方不触发、城市主意列表也看不到），只进 `Cities_Ideas` 卡包。

### 硬币磁力箱 `stacklandspro_coin_magnet_chest`（0.5.0 新增）

- **继承 `Chest`**（原版硬币箱）：金币存进 `CoinCount`、`HeldCardId` 固定 `"gold"`、点击一次取 5 枚、
  卡面显示箱内金币图标；因为这层继承，**原版自动捡金币的流程（`WorldManager.TrySendToChest` 里判 `card.CardData is Chest`）
  也会认得它**，金币会主动飞进箱子。
- **只收金币**：白名单只有 `gold`（不收贝壳、纸币、其它资源/食物/装备）；也不让自己叠自己
  （叠起来下面那只会停止吸附，所以 `CanHaveCard` 直接写死只收金币）。
- **吸附**：和磁力箱同一套逻辑（`TrySendToMagnet` 截胡 + 全场散金币扫描），代码在本类里重写了一遍
  （C# 单继承：既要 `Chest` 的存储、又要磁力箱的吸附，只能自己实现吸附那半）。
- **容量**：`MaxCoinCount` 每帧设成 **10000**（0.5.8~0.5.13 曾按用户要求"无限"`int.MaxValue`；
  0.5.14 用户定值 1 万，与原版箱子定值口径一致；原版硬币箱是 100）。超限只是不再吸新，已有余额照常保留（不下钳）。
- **取出免疫**：`Clicked()` 之后把刚取出的散金币登记进**和磁力箱共用的那张免疫表**，10 分钟内不被回吸。
- **外观**：镜像原版 `coin_chest` 的 `IsBuilding` / `HasUniquePalette` / `MyPalette`，角标用 `SpriteManager.CoinIcon`。
- **存档**：`CoinCount` 用原版的 `[ExtraData("coin_count")]`（`Chest` 自带）。
- **JSON 可调参数**：`_PullRadius` / `_PullInterval` / `_RemovalImmunitySeconds` / `_ImmunityKeepAwayFactor`。

### 贝壳磁力箱 `stacklandspro_shell_magnet_chest`（0.5.5 新增）

- **需求原话**：「参考硬币箱和硬币磁力箱，做一个贝壳宝箱的升级版，名字就叫贝壳磁力箱」。
- **实现方式＝纯数据驱动**：`ShellMagnetChest` 只是 `CoinMagnetChest` 的空子类，逻辑一行不写；
  区别全在 `Cards/shell_magnet_chest.json`：`_HeldCardId = "shell"`、`_CoinPullCardId = "shell"`、
  `_VanillaLookCardId = "shell_chest"`、`"icon": "shell_chest"`。
- **为什么原版机制天然支持**（反编译核实）：金币和贝壳走的是**同一条 `Chest` 线**——
  `WorldManager.TrySendToChest` 里是
  `Id == "gold" || Id == "shell" ? 找 chest.HeldCardId 匹配且未满的 Chest : 找 ResourceChest(...)`，
  而 `Chest.HeldCardId` 默认就是 `"gold"`，**原版贝壳宝箱 `shell_chest` 只是把它改成 `"shell"` 的那张卡**
  （`shell_chest` / `blueprint_shell_chest` 都是原版真实 id）。所以我们的箱子只要也是 `Chest` 且
  `HeldCardId == "shell"`，原版"自动捡贝壳"的流程就会认得它。
- **其余行为**（点击取 5、容量无限、吸附两条路、10 分钟取出免疫、卡面照原版箱子）全部继承硬币磁力箱。
- **配方**：**先放磁铁**，再把贝壳宝箱拖到磁铁上（5 秒，两个都消耗）；
  `ChestMagnetCraftingPatch` 里 `otherCard is Chest` 这一条**天然覆盖**了贝壳宝箱，不需要额外放行。
- **JSON 可调参数**：同硬币磁力箱，外加 `_HeldCardId` / `_CoinPullCardId` / `_VanillaLookCardId`。

### 磁力信用卡 `stacklandspro_magnet_creditcard`（0.5.5 新增；0.6.2 改为**城市场地专属**）

- **需求原话**：「再做一个信用卡的升级版，叫磁力信用卡」；0.6.2 追加：「主意和磁力容器相似，把容器换成信用卡就行，只在 city 里出现」。
- **0.6.2 的改动**：
  1. **配方改成和磁力容器同款**：铁条 `iron_bar` + 铜条 `copper_bar` + 科学 `science` + **信用卡 `creditcard`** + `any_educated_worker`
     （5 秒，工人不消耗；`CardsToRemove` 只写前四张）。原来的配方是 `resource_magnet + creditcard`。
  2. **改成城市专属**：卡与创意卡都标成 `CardUpdateType.Cities`（`CitiesCardSupport.MarkAsCitiesCard`），
     于是它在城市场地的配方能跑、城市的创意列表能看到；`AdvancedBuildingIdea` 卡包里的那条已撤掉，
     只留在 `Cities_Ideas`（不撤会抽到一张"抽到就消失"的创意）。
- **原版信用卡**（反编译 `public class Creditcard : CardData, ICurrency`，id `creditcard`）：
  钱存在 `DollarCount`（`[ExtraData("dollar_count")]`，会存档），上限 `MaxDollarCount = 1000`；
  `CanHaveCard` 只接受 `Dollar`（纸币）或另一张信用卡；`UpdateCard()` 把叠上来的 `Dollar` 按 `DollarValue`
  吞进 `DollarCount`（超出上限的部分会 `CreateDollarsFromValue` 再吐出来）；`Clicked()` 一次最多吐出 **100** 面额；
  因为实现 `ICurrency`，买东西时由原版 `BuyWithCreditcard` 扣钱。
- **本类**＝给它装上磁铁（和三种箱子同一套：`TrySendToMagnet` 截胡 + 扫描场上散落纸币 + 取出后 10 分钟免疫），
  重写一遍吸附那半是因为 C# 单继承、而这里要继承 `Creditcard` 的存储与结算。
- **与三种箱子的差异**：吸收入库直接用**原版 `Creditcard.UpdateCard()`**（所以面额拆分、超限溢出都保持原版行为），
  本类只额外做"吸过来 + 免疫"；`_MaxDollarCount` 默认放到 `2147483647`（想要原版 1000 改 JSON 即可，不用重编译）。
- **外观**：镜像原版 `creditcard`（`_VanillaLookCardId`），并把它的 `BankDescriptionTerm` 也接过来。
- **JSON 可调参数**：`_VanillaLookCardId` / `_MaxDollarCount` / `_PullRadius` / `_PullInterval` / `_RemovalImmunitySeconds` / `_ImmunityKeepAwayFactor`。

### 高级仓库 `stacklandspro_super_warehouse`（0.5.6 起界面名从"超级仓库"改成"高级仓库"，id/类名不动）

- 最朴素的 `CardData` + `_IsBuilding: true`（建筑配色），图标借原版 `warehouse`，行为全靠补丁。
- **两种配方**（同一个蓝图的两条 subprint，凑齐任一组即可）：
  1. **10 张仓库叠成一叠 → 20 秒 → 高级仓库**（`CardsToRemove` 写了 10 个 `warehouse`，10 张都消耗）；
  2. **10 个石头 + 10 个铁条 + 一名村民 → 120 秒 → 高级仓库**（`CardsToRemove` 只写石头和铁条，村民不消耗；村民用的是原版特殊 id `any_villager`，任何村民都行）。
- 每张给场地 **+140** 卡牌上限（= 10 × 原版仓库的 +14）。

### 高级温室 `stacklandspro_advanced_greenhouse`（0.5.6 新增）

用户原话：「新增卡牌"高级温室"，6个普通温室合成，卡牌外观和温室保持一致，主意卡牌也和温室的一致，高级温室支持放6个材料，可以一次产出6个材料，原版温室的6倍，已下载到mod（Stacklands Plus）中有个超级温室，功能就参考他的，但是外观用普通温室的」。

- **零新类**：JSON 里 `"script": "Greenhouse"` 直接复用原版空类（温室的行为全在生长蓝图里），图标借原版 `greenhouse`，`_IsBuilding: true`。
- 配方：**6 张温室叠成一叠 → 40 秒 → 高级温室**（6 张都消耗）。40 秒这个数是照 Stacklands Plus 的超级温室取的（需求里只给了高级仓库的 120 秒）。
- **生长**（`AdvancedGreenhousePatch.cs`）：把原版 `blueprint_growth` 里每条"种子 × 普通温室"的配方复制成
  **`[高级温室, 种子 ×1]` → 产物 ×1、时长固定 15 秒**（0.5.10 改版；状态文字、`ResultAction` 对齐原版；`CardsToRemove` 写成"什么都不删"的哨兵 `new string[1] { "" }`）。
  0.5.6 首版是「放 6 胡萝卜，产出 6 胡萝卜」（原版时长），0.5.10 用户改口径：「高级温室之前是放6个作物，然后90秒生产一次，一次生产6个，改成放一个作物，每15秒生产一次，每次生产一个」——
  机制不变：**种子永不消耗（`SeedCount = 1`、`YieldPerSeed = 1`、`GrowTimeSeconds = 15f`）**，产物飞出去，温室上仍然叠着那 1 个种子 ⇒ 配方立刻再次匹配 ⇒ **自动循环**。
- **运输输出（0.5.6 追加；用户纠正过我一次，见下）**：用户原话「高级温室要有运输输出，一个就行，产出的6个胡萝卜通过这个输出到指定地方（炖锅、箱子等）」。
  **⚠️ 原版有两套"输出"，别搞混**（第一版我只做了 ②，被用户一句「main场地也有运输输出，比如冶炼厂、锯木厂、采石场等等，你是不是用错了」纠正）：
  ① **输出方向**（冶炼厂/锯木厂/采石场/农田…**主大陆、岛上都有**）：`[ExtraData("output_direction")] OutputDir` + 卡片旁那个**可点的箭头**（`DirectionCircleElement.Clicked()` → `GameCard.ToggleDirection()`），产物走 `StackSendCheckTarget(温室, 产物, OutputDir, 温室)`（先管道 → 磁铁 → 2 格内配套箱子 → 朝方向抛出）；
  ② **储物箱的"运输输出"连接器**（`_EnergyConnectors`，**只在 Cities 场地显示/连接**，`GameCard.UpdateConnectors()` 里非 cities 全隐藏）。
  本项目现在**两套都给**：`Patches\AdvancedGreenhouseOutput.cs` 里 `AdvancedGreenhouseCard : Greenhouse`（重写 `CanSelectOutput() => true`，**运行时从原版卡预制体借一个 `DirectionCircleElement` 箭头**装到自己身上——mod 卡没有预制体）＋ `AdvancedGreenhouseOutputPatch`（Prefix `WorldManager.StackSendCheckTarget`：温室旁半径 `_OutputRadius`（默认 2.5 格）内有 `CanHaveCardOnTop` 认可的容器就把产物**直接 `StackSendTo` 送进去**，**优先箭头方向那个**；没有就原样交回原版往外弹；接了管道则让原版走管道）；JSON 另留 1 个 `_EnergyConnectors`（Cities 管道那套）。
- **图标**：JSON 里写 `"icon": "greenhouse"`（原版温室卡图）。**主意卡图标试了三次都没成**（JSON 写 `blueprint_greenhouse` 拿不到 → 退成灯泡；`Mod.Ready()` 里 `ApplyVanillaIdeaIcon()` 对齐原版主意卡的 `Icon` 字段、连 `null` 一起对齐 + `BlueprintPrefabs`/`CardDataPrefabs` 兜底，用户实测**还是灯泡**）→ 用户拍板「**主意的图片还是灯泡，算了就这样吧**」，**代码保持现状、别再追**。
- 参考 mod 的做法是**另建一张自己的生长蓝图卡**再把它塞进卡包；本项目直接改原版生长蓝图，玩家能种普通温室就能用高级温室（少一张卡、少一处联动）。
- ⚠️ 注入是**幂等**的，并在两处调用（`Mod.Ready()` + `BlueprintGrowth.Init` 后置）——因为 `Init()` 里会清空重建配方列表，时序不定（见 patche 注释）。

### 魔法收集器 `stacklandspro_magic_collector`（0.5.8 新增）

用户需求：「主意为 1 木板 1 石砖 1 铁条 1 金条 1 玻璃、3 魔法尘埃；将魔法师叠在魔法收集器上，30 秒产出一个魔法尘埃；魔法收集器有和采石场类似的运输输出」。

- **卡片脚本** `Patches\MagicCollector.cs` 里 `MagicCollectorCard : CardData`（JSON `"script"` 指过去），图标暂借原版 `magic_dust`，`_IsBuilding: true`。
- 配方：**1 木板（plank）+ 1 石砖（brick）+ 1 铁条（iron_bar）+ 1 金条（gold_bar）+ 1 玻璃（glass）+ 3 魔法尘埃（magic_dust）→ 60 秒 → 魔法收集器**（全消耗；60 秒是仿照高级仓库配方的量级取的）。
- **产出**：`CanHaveCard` 收 `wizard`（Island 魔法师）/`mage`（Cities 法师）两个 id，叠上去后 `UpdateCard` 启动 **30 秒**计时；`[TimedAction("make_magic_dust")] MakeMagicDust()` **对齐采石场 `CompleteHarvest` 的产出形状**——`CreateCard(位置, "magic_dust", faceUp: false, checkAddToStack: false)` + `StackSendCheckTarget(MyGameCard, 尘埃, OutputDir)`。**魔法师不消耗**，留在收集器上 → 计时自动重启 = 每 30 秒一个；把魔法师拖走，`UpdateCard` 里 `CancelTimer` 停产。
- **运输输出＝采石场那套管线**：vanilla `StackSendCheckTarget` 本身就是完整管线（Cities 管道 → 磁铁 → 2 格内能收它的箱子 → 朝 OutputDir 弹出），所以**不需要额外的输出补丁**（与高级温室不同：温室是 BlueprintComplete 特殊收尾才需要 Prefix 截一道）。**输出方向箭头已按用户要求去掉**（0.5.8 早期版本曾复用 `AdvancedGreenhouseOutput.EnsureOutputArrow()` 借原版箭头 + `CanSelectOutput() => true`，后来用户要求撤掉；产物固定朝默认方向弹出，旁边的箱子照常自动接住）。
- **管道运输输出（0.5.10 追加）**：用户需求「魔法收集器需要加一个和铁矿、高级温室、磁力箱一样的运输输出」——这三张卡共同的那套就是 `_EnergyConnectors`（Cities 管道连接器），而 `StackSendCheckTarget` 的第一步 `TrySendWithPipe` 本来就认这个连接器，所以只在 `Cards\magic_collector.json` 里声明 1 路（`EnergyConnectionType = 1 / EnergyConnectionStrength = 5 / EnergyConnectionAmount = 1`，与高级温室完全一样），**零 C# 改动**。⚠️ 连接器只在 Cities 场地显示/可接（原版规则）；没接管道时行为与以前完全一样。
- 图标以后想换：只改 `Cards\magic_collector.json` 与主意 JSON 里的 `"icon"` 字段。

### 配方速查

| 产物 | 材料与操作 | 备注 |
| --- | --- | --- |
| 磁力箱 | **先放磁铁**，再把资源箱拖到磁铁上（5 秒，两个都消耗） | 反方向不安全（箱子会吞掉磁铁）故未放行 |
| 硬币磁力箱 | **先放磁铁**，再把硬币箱拖到磁铁上（5 秒，两个都消耗） | 同上；硬币箱指原版 `coin_chest` |
| 贝壳磁力箱 | **先放磁铁**，再把贝壳宝箱拖到磁铁上（5 秒，两个都消耗） | 贝壳宝箱指原版 `shell_chest` |
| 磁力信用卡（0.6.2 改） | 铁条 + 铜条 + 科学 + 信用卡 + 受教育工人（5 秒，**工人不消耗**） | **只在城市场地**；旧配方"磁铁 + 信用卡"已废弃；卡与创意都标 Cities，只进 `Cities_Ideas` |
| 高级回收中心（0.6.2） | 5 张回收中心叠成一叠（20 秒，5 张全消耗） | **只在城市场地**；回收中心默认不能互相叠，靠 `AdvancedRecyclingCenterStackPatch` 放行；**合成前这 5 张必须先断电、取出工人，否则不读条**（0.6.4，`AdvancedRecyclingCenterBlueprintPatch`） |
| 磁力容器（0.6.0） | 铁条 + 铜条 + 科学 + 资源容器 + 受教育工人（5 秒，**工人不消耗**） | **只在城市场地**；材料叠好 → **资源容器扣上面** → 工人叠容器上（0.6.1 起支持），或工人垫最底下；**容器垫最底下会吞材料**；创意只进 `Cities_Ideas` |
| 高级仓库（配方一） | 10 张仓库叠成一叠（20 秒，10 张都消耗） | 需要 `WarehouseStackingPatch` 才叠得起来 |
| 高级仓库（配方二） | 10 石头 + 10 铁条 + 一名村民（120 秒，只消耗材料） | 村民不消耗；`any_villager`＝任何村民 |
| 高级温室 | 6 张温室叠成一叠（40 秒，6 张都消耗） | 之后放 **1 个**种子（0.5.10 起，原为 6 个）：每 **15 秒**飞出 **1 份**产物，种子不消耗、自动循环；产物**送进旁边的容器**（配套箱子/炖锅等，优先箭头方向、半径 `_OutputRadius` 2.5 格）；Cities 场地另有 1 路管道输出；**需要 `GreenhouseStackingPatch` 才叠得起来**（0.5.24 修） |
| 魔法收集器 | 1 木板 + 1 石砖 + 1 铁条 + 1 金条 + 1 玻璃 + 3 魔法尘埃（60 秒，全消耗） | 之后叠**魔法师**：每 30 秒产 1 个魔法尘埃，魔法师不消耗、拖走就停产；产物走采石场那套运输输出（2 格内箱子/炖锅自动接住，否则朝箭头方向弹出）；Cities 场地另有 1 路管道输出（0.5.10） |

```powershell
# 改代码 / 加资源 → 编译并部署（★ 必须完全退出游戏，否则 dll 被锁）
powershell -ExecutionPolicy Bypass -File .\build.ps1

# 只改 Cards/Blueprints/localization 等数据 → 秒同步，不编译
powershell -ExecutionPolicy Bypass -File .\build.ps1 -SkipBuild
```
- 部署目标固定为 `...\LocalLow\sokpop\Stacklands\Mods\StacklandsProMax\`。
- **任何改动都要重启游戏才生效**；排查看 `F3` 日志或 `Player.log`。
- 版本号写在 `manifest.json` 的 `version`；改完记得同步文档头部版本。
- `build.ps1` 必须保持**纯 ASCII**（PS 5.1 按 GBK 读无 BOM 脚本，中文会破坏语法）；中文说明一律写进 Markdown。
- 部署内容 = `Blueprints/` `Boosterpacks/` `Cards/` `Icons/` `Sounds/` + `manifest.json` + `localization.tsv` + **`icon.png`**（工坊缩略图）。
  **发布卫生**：`config.json`（本机设置）每次部署都会被主动从 Mods 目录删掉——游戏内 Upload 打的是**整个 mod 目录**，
  把本机设置带出去会污染订阅者。反过来 **`icon.png`（工坊缩略图）与 `workshop.txt`（工坊 item id）必须留在目录里**：
  前者是上传时的封面图，后者决定"更新已有条目"还是"新建一个重复条目"，所以它俩跟 `manifest.json`、`localization.tsv`
  一样，每次部署都会被同步/恢复（见 [`创意工坊发布指南.md`](创意工坊发布指南.md) §一 第 3/5 条）。


## 磁力系列调参

改 JSON **不用重新编译**：改完跑 `powershell -ExecutionPolicy Bypass -File .\build.ps1 -SkipBuild`（只同步资源），重启游戏生效。

| 字段（`Cards/magnet_chest.json`） | 默认 | 作用 | 建议 |
| --- | --- | --- | --- |
| `_MaxResourceCount` | **10000**（被设置界面「磁力箱容量」输入框逐帧覆盖，此处只在生成瞬间生效） | 箱子容量 | 改容量去设置界面（立即生效），不用动 JSON |
| `_PullRadius` | **999** | 吸附半径（格）。原版箱子自动收货半径是常数 `2f`，所以 2 就是"紧挨着才收"；**999 = 整块场地** | 8 小范围 / 30 一大片 / **999 整场**；设 0 等于关掉吸附 |
| `_PullInterval` | 0.1 | 两次吸附之间的最短间隔（秒）＝吞吐上限 | 0.03 疯狂（每秒 30 张）/ **0.1 舒适（每秒 10 张）** / 0.5 慢悠悠 |
| `_RemovalImmunitySeconds` | **600** | 取出后的免疫时长（秒）＝10 分钟 | 5 ~ 30 短免疫 / **600 长免疫** |
| `_ImmunityKeepAwayFactor` | 1.6 | 拿开到「半径 × 该系数」之外就解除免疫（半径 999 时实际不会触发） | 1.2 ~ 3 |
| `_OutputInterval` | **10** | "运输输出"的间隔（秒），和原版储物箱一致 | 想更勤就调小（3 = 每 3 秒一吐） |
| `_EnergyConnectors` | 3 个运输输出 | 连接器声明（`EnergyConnectionAmount` 就是"几路输出"） | 想要 5 路就把 amount 改成 5 |
| `_HasUniquePalette` / `_MyPalette` | 蓝色系 | 卡面配色（区分原版储物箱） | 随意改十六进制色值 |

## 磁力系列代码位置

`Mod.cs` 中的成员职责（改行为看这里）：

| 成员 | 作用 |
| --- | --- |
| `MagnetChest.CanHaveCard` / `IsStorable` | 谁能放进箱子（决定类型设定规则 + 白名单） |
| `MagnetChest.AbsorbStackedResources` | 把叠上来的材料吞掉并计数 |
| `MagnetChest.PullNearbyResources` | 扫描 + 吸附（含全部安全判定） |
| `MagnetChest.RemoveResources` / `Clicked` | 取出材料 + 标记"刚取出" |
| `MagnetChest.MarkRecentlyRemovedFrom` / `IsRecentlyRemoved` / `CleanupImmunity` | 取出免疫机制（三种箱子 + 磁力信用卡共用同一张表） |
| `MagnetChest.UpdateOutputTimer` / `OutputCard` / `HasConnectedTransportOutput` | 三路运输输出（每 10 秒给每一路已连接的输出各发一张） |
| `Patches\ConnectorDiagnosticsPatch.cs` | 启动时把原版 `resource_chest` / `coin_chest` 与本箱的连接器配置打进 `F3` 日志（排查用） |
| `CoinMagnetChest.CanHaveCard` | 硬币磁力箱只收金币 |
| `CoinMagnetChest.AbsorbStackedCoins` / `PullNearbyCoins` / `Clicked` | 硬币磁力箱的吸金币 + 取出免疫 |
| 贝壳磁力箱（`ShellMagnetChest : CoinMagnetChest`，空子类） | 只吸贝壳 + 外观镜像原版贝壳宝箱；差别全在 `shell_magnet_chest.json` |
| `StacklandsProMax.Ready` | 把每张卡的创意加进卡包（`CardBagEntries`） |
| `ModConfig.ApplyCardAvailability` | 卡总开关：从抽卡池摘掉 / 放回、图鉴隐藏 |

## 与原版资源箱的口径对照

**收卡范围**：原版判定在 `ResourceChest.CanHaveCard`，磁力箱在 `MagnetChest.IsStorable`。两边都是"一箱一种"（首张卡定类型 `HeldCardId`，之后只收同种）。

| 卡 | 原版资源箱 | 磁力箱 |
| --- | --- | --- |
| 材料（`CardType.Resources`：石头/木头/铁矿石/玻璃等） | ✅ | ✅ |
| 便便（poop，属于 Resources） | ✅ 主大陆/岛上；❌ Cities 场地（原版代码明文排除） | ✅ 任何场地 |
| 铀（quantum_entangled_uranium，属于 Resources） | ✅ 非 Cities；❌ Cities（原版明文排除） | ✅ 任何场地 |
| 食物（`CardType.Food`） | ❌（仅 Cities 收 `FoodValue ≤ 0` 的食物牌这一特例） | ✅ 全部 |
| 装备（`CardType.Equipable`） | ❌ | ✅ |
| 金币 / 贝壳 | ❌ | ❌ |
| 纸币（Dollar） | ❌ | ❌ |
| 另一个箱子 / 磁力箱 | ❌（箱子不是 Resources 类型） | ❌（显式排除，防"箱子吞箱子"） |

- **原版资源箱允许、磁力箱不允许的：没有**。磁力箱的收卡范围是原版资源箱的**严格超集**（货币两边都不收；Cities 场地原版排除的便便/铀，磁力箱反而收）。
- **磁力箱比原版资源箱多收**：食物（任何场地）、装备、便便、Cities 场地的铀。

**机制差异**（磁力箱多出来的，都是本 mod 加的）：

| 能力 | 原版资源箱 | 磁力箱 |
| --- | --- | --- |
| 自动吸附 | 无（掉在旁边的材料自己不会进来） | 有：默认整场（`_PullRadius` 999）扫散落的目标卡、`_PullInterval` 0.1 秒一张吸进来 |
| 产出截胡 | 无 | 有：挂 `WorldManager.TrySendToMagnet`，建筑产出的目标材料"一出生"直接飞进箱（`WantsCard`） |
| 取出免疫 | 无（点出来就是普通掉落） | 有：取出的卡 10 分钟（`_RemovalImmunitySeconds` 600）内不被回吸，拿远到半径×1.6（`_ImmunityKeepAwayFactor`）提前解除 |
| Cities 管道输出 | 1 路输出连接器 | **3 路**（`_EnergyConnectors` Amount 3）；输出节奏同为 10 秒（`_OutputInterval`） |
| 总开关 / 调参 | 无 | 每张卡一个总开关；上表参数全部 JSON 可调 |
| 存档 | `resource_count` / `resource_id` | 同款字段但带 `stacklandspro_` 前缀，不与原版/其它 mod 冲突 |

**对齐原版的部分**：方形内容框 + 右上数量角标（`SpecialValue`/`SpecialIcon`）、配色从原版储物箱预制体镜像、超限即停不吞多余材料的行为。
**容量口径**：原版资源箱本体 100（本 mod 把原版资源箱也定到 10000），磁力箱 10000。

## 功能总览

| # | 功能 | 补丁的游戏方法 | 类型 | 参考 mod | 游戏内开关（默认开） |
| --- | --- | --- | --- | --- | --- |
| 1 | 卡牌堆叠上限 30 → **100**（0.5.18 起是设置里的**数字输入框**，填 30＝原版） | `CardData.CanHaveCardOnTop` | Transpiler（换成运行时取值） | HigherStacks | 「卡牌堆叠上限」（输入框，默认 100） |
| 1b | 让原版磁铁也吃满新上限 | `WorldManager.TrySendToMagnet` | Prefix（接替原方法） | HigherStacks | 跟随上面那个输入框 |
| 1c | **归堆搜索距离 2 → 4** | `WorldManager.StackSend` + `WorldManager.TrySendToChest` | Transpiler ×2（换成运行时取值） | Stack Further | 「自动归堆搜索距离」（输入框，默认 4；0.5.18 起**没有开关**，填 2＝原版） |
| 2 | **点一下开完整个卡包** | `Boosterpack.Clicked` | Prefix + Postfix（0.05s 协程链） | PACKSPLOSION!!! | 「点击一次开完整包」 |
| 3 | **打败女巫后不再刷随机传送门** | `EndOfMonthCutscenes.SpecialEvents` | Prefix（关掉官方开关） | NoWitchNoPortal | 「女巫死后无随机传送门」 |
| 4 | **空闲村民 / 空闲生产设施显示红色边框** | `GameCard.Update` | Postfix（改原版高亮框颜色） | Show Idle Villager | 「空闲村民红色边框」+「空闲生产设施红色边框」 |
| 5 | **右键按住拖动**（整叠 + 单张） | `WorldManager.Update` + `InputController.GetInputBegan` | Postfix ×2 | ——（自研） | 「右键拖动卡牌」 |
| 6 | **双击把附近同种卡归成一堆** | `WorldManager.Update` | Postfix | Better Mouse Controls | 「双击同种卡自动归堆」 |
| 7 | **高级仓库**：+140 卡牌上限（0.5.6 由"超级仓库"改名） | `WorldManager.CardCapIncrease` | Postfix | ——（自研） | 常开（子开关已按用户要求撤掉） |
| 7b | 仓库可互相叠放（配方需要；0.5.7 起高级仓库也纳入，见 23） | `CardData.CanHaveCardOnTop` | Prefix | ——（自研） | 常开 |
| 8 | **磁力箱 / 硬币磁力箱 / 贝壳磁力箱 / 磁力信用卡**（卡牌本体） | ——（内容 JSON + `TrySendToMagnet` 截胡） | —— | ——（自研） | **每张卡一个总开关**；吸附/取出免疫/外观/配方 4 个子功能常开，见本文「磁力系列调参」「与原版资源箱的口径对照」 |
| 9 | **关掉的卡：配方不成立** | `CardData.FindMatchingPrint` | Postfix | ——（自研） | 跟随 4 个「卡牌：…」总开关 |
| 10 | **磁力箱三路运输输出** | ——（`_EnergyConnectors` 声明 + 10 秒计时器） | —— | ResourceChest 的"运输输出" | 常开；间隔用 `_OutputInterval` 调 |
| 11 | 启动时打印连接器配置（排查用） | `WorldManager.Start` | Postfix | ——（自研） | 常开，只写日志 |
| 12 | **Ctrl+拖空白框选多张卡 → 整组搬运** | `WorldManager.Update` + `GameCard/Bosterpack.Clicked` + `GameCamera.StartDragging` | Postfix ×1 + Prefix ×3 | Multi Select & Drag | 「框选多张卡」 |
| 13 | **伤害/治疗数字显示在卡牌中间** | `HitText.SetPosition`（+ `Combatable.CreateHitText` 打标记） | Postfix ×2 | Hit Text To Center | 「数字显示在卡牌中间」 |
| 14 | **从箱子里拖出材料**（Shift 10 张 / Ctrl 1 张，挂在鼠标上，松开才落地）＋ **屏蔽原版「Shift+拖整叠」** | `GameCard.StartDragging` | Prefix（`Priority.First`） | Stacks n Capacity 的机制 | 常开（**只作用在本项目的磁力箱**；原版资源箱的取牌仍归原版 / Stacks n Capacity，见第十一节） |
| 15 | **取出的牌"往边上掉"**（原版会弹起来又落回箱子上）：原版资源箱 / 资源容器 / 食物容器点击取出、以及**Ctrl/Shift 取牌后"只点一下"**时，都改成弹到旁边的**空地**（0.6.7 起落点逐个候选点验证"这格没被占"）；**拖出去再放回箱子的照常收下**（0.5.17，按拖动全程鼠标偏移区分）；**弹出的叠会自动飞到 2 格内的同种牌堆上归堆**（0.5.19，连续取出的多叠自动并堆） | `ResourceChest.RemoveResources` + `FoodWarehouse.RemoveFood` + `WorldManager.DropCard` | Prefix（接替原实现）+ Postfix（改落点）+ Prefix（接管松手）+ Postfix（采样偏移） | ——（自研） | 「取出的牌往边上掉」 |
| 16 | **快捷键（默认 F，0.5.9 前为 R）把鼠标指着的卡/牌堆送往箱子或同类堆** | `WorldManager.Update` | Postfix | Stacks n Capacity 的 X 键机制 | 「按快捷键把卡送往箱子」+「送往箱子的快捷键」（文本框，默认 `F`，0.5.9 前为 `R`） |
| 17 | **Alt+点击搜索主意**（Alt+左键＝按标题、Alt+右键＝按描述、Alt+点空白＝清空）＋ 侧边栏**三个固定按钮**（搜标题 / 搜描述 / 清除）＋ 侧边栏停在**任务**页时自动跳到**主意**页 | `GameScreen.KnowledgeMatchesSearch`（私有）＋`WorldManager.Update`＋`GameScreen.Awake`＋`GameCard`/`Boosterpack.Clicked` | Prefix ×3 + Postfix ×2 | Extra Search Mod | 「Alt+点击搜索主意」 |
| 18 | **左侧信息栏自动加高**（按文字高度自适应 + 不吃鼠标射线） | `GameScreen.Awake` + `GameScreen.LateUpdate` | Postfix ×2 | HigherSidebar | 「信息栏自动加高」+ 最小/最大高度两个数值 |
| 19 | **详细信息**（战斗详解 + 估算 DPS + 掉落百分比 + 卡包内容 + 装备攻击类型 + 波次 + 牌堆描述 + 岛上贝壳金币） | 12 个补丁点（见第十三节 13.2） | Prefix ×6 + Postfix ×6 | BetterInfo | 「详细信息（战斗 / 掉落）」 |
| 20 | **自动暂停**：读到存档后暂停 + 换地点后暂停 | `MainMenu.Awake` + `WorldManager.Play` + `TransitionScreen.StartTransition`（私有） | Postfix ×3 | Auto Pause | 「读到存档后自动暂停」+「切换地点后自动暂停」 |
| 21 | **战斗多行排列**（战斗框里每行最多 N 张，超出往后另起一行） | `Conflict.GetPositionInConflict` + `GetConflictSize`（私有）+ `GetBounds` | Prefix ×3（接管） | Lopi's Conflict Rows | 「战斗多行排列」+「每行战斗单位上限」（数值，默认 7） |
| 22 | **高级温室**（6 张温室合成；0.5.10 起：一次放 **1 个**种子 → 每 **15 秒**产出 **1 份**产物、种子不消耗、自动循环；产物**送进旁边的容器**，另带 1 路 Cities 管道输出） | `BlueprintGrowth.Init` + `WorldManager.StackSendCheckTarget` | Postfix ×1 + Prefix ×1 | Stacklands Plus 的超级温室（只参考机制，外观用原版温室）＋冶炼厂/锯木厂那套"输出方向" | 「卡牌：高级温室」 |
| 23 | **高级仓库可堆叠**：仓库 / 高级仓库任意两张互相叠放（0.5.7 扩展 7b 的判断） | `CardData.CanHaveCardOnTop` | Prefix（同 7b） | ——（自研） | 常开 |
| 23b | **温室可堆叠**（0.5.24 修）：普通温室之间可以互相叠放——原版温室只收材料/村民/食物，温室自己是建筑，所以「6 张温室 → 高级温室」的配方永远凑不齐 | `CardData.CanHaveCardOnTop` | Postfix（同 23，只补"温室←温室"一种组合，其余保留原版判定） | ——（自研，修本 mod 自己引入的问题） | 常开 |
| 24 | **火锅（炖锅）容量默认 10000**（原版 50；0.5.18 起是设置里的**数字输入框**，填 50＝原版） | `Hotpot.UpdateCard` | Postfix（反射写私有字段 `MaxFoodValue`，每帧兜底） | Stacks n Capacity | 「火锅容量」（输入框，默认 10000） |
| 25 | **动物留在饲养栏**：繁殖后亲代不弹出去、自动连着繁殖（参考 Stacks n Capacity） | `BreedingPen.BreedAnimals` | Prefix（接管） | Stacks n Capacity | 「动物留在饲养栏」 |
| 26 | **资源箱内的资源不按数量计入卡牌上限**（每个箱子固定只算 1 张，同磁力箱口径） | `WorldManager.GetCardCount()`（无参重载） | Postfix（把每个被计入的箱子减回 1，目标用 `TargetMethod()` 显式绑定） | Stacks n Capacity | 「资源箱不计入卡牌上限」 |
| 27 | **魔法收集器**（新卡牌：木板+石砖+铁条+金条+玻璃+3 魔法尘埃合成；叠魔法师每 30 秒产 1 个魔法尘埃，魔法师不消耗；产物自动送进旁边容器；0.5.10 起带 **1 路 Cities 管道运输输出**，同高级温室/磁力箱） | ——（卡片脚本 `MagicCollectorCard`，产出走原版 `StackSendCheckTarget` 管线） | —— | ——（自研） | 「卡牌：魔法收集器」 |
| 28 | **资源箱容量默认 10000**（原版 100；0.5.18 起是设置里的**数字输入框**，填 100＝原版） | `ResourceChest.UpdateCard` | Postfix（公开字段每帧兜底） | Stacks n Capacity（它设 10000） | 「资源箱容量」（输入框，默认 10000） |
| 28b | **硬币箱/贝壳宝箱容量默认 10000**（原版 100；同一个 `Chest` 类覆盖两张卡，排除磁力箱子类；0.5.18 起**数字输入框**） | `Chest.UpdateCard` | Postfix | Stacks n Capacity（它设 10000） | 「硬币箱/贝壳宝箱容量」（输入框，默认 10000） |
| 28c | **信用卡容量默认 100000**（原版 1000；排除磁力信用卡子类；0.5.18 起**数字输入框**） | `Creditcard.UpdateCard` | Postfix | ——（自研） | 「信用卡容量」（输入框，默认 100000） |
| 29 | **资源箱可存更多卡牌类型**（磁力箱白名单：材料+食物+装备；仍一箱一种） | `ResourceChest.CanHaveCard` | Postfix（全量重算——Stacks n Capacity 也 Prefix 了它，Postfix 一定最后跑） | Stacks n Capacity（它的白名单不含便便排除） | 「资源箱可存更多卡牌类型」 |
| 29b | **资源箱取出的卡自动堆叠**（2 格内有同种牌堆就飞过去叠上，照磁力箱取出的堆叠行为；没有才往边上弹） | `ChestEject.EjectToPileOrSide`（`RemoveResources` 的共用弹出升级） | —— | ——（自研） | 跟随「取出的牌往边上掉」 |
| 29c | **落点优先空地 + 食物容器纳入**（0.6.7）：取物落点逐个候选点验证"这格没被占"；原版食物容器的"随机方向甩出去"也改成同一套 | `ChestEject`（共用落点选址）+ `FoodWarehouse.RemoveFood` | Postfix | ——（自研） | 跟随「取出的牌往边上掉」 |
| 30 | **月末吃饭优先级**（0.5.9）：先吃**炖锅**里的，**不吃叠在高级温室上的**（那 6 个"种子"是自动循环的生产原料） | `EndOfMonthCutscenes.GetFoodToUseUp`（私有静态，`TargetMethod()` 绑定） | Prefix（接替原实现——排序键在闭包 lambda 里，transpiler 够不着） | ——（自研，修本 mod 自己引入的问题） | 常开（无开关） |
| 31 | **K+O+F1 调试面板汉化**（0.5.9）：缺失的 `label_debug_*` 中文词条注入当前语言词表（27 个）+ 面板上硬编码的英文按钮按表替换（仅简体中文生效，切语言自动还原） | `SokLoc.get_CurrentLocSet` + `DebugScreen.Start` | Postfix ×2 | ——（自研） | 常开（无开关） |
| 32 | **Ctrl+点击快速购买卡包**（0.5.16）：按住 **Ctrl** 并**左键点击购买箱**（带价格的卡包预览盒）直接买下一包——付款优先用**场上散落的**金币/贝壳/纸币，不够自动从**对应的箱子/信用卡**（含磁力箱系）里扣，离盒子近的先扣；纸币按原版算法凑整+找零 | `Draggable.Clicked` | Prefix | 原版 `BuyBoosterBox.CardDropped` 的扣款原语（散币堆→箱子→信用卡） | 「Ctrl+点击快速购买卡包」 |

| 33 | **悬浮提示永远显示在鼠标右上方**（0.5.18）：原版长文案（「取出的牌往边上掉」「详细信息」等）从右上放不下会**翻到鼠标左边**、且因 `IsOverflowing` 量到旧布局尺寸而**截断**；改为先 `ForceRebuildLayoutImmediate` 算完布局、pivot 固定 (0,0)（右上展开）、放不下整体往回挪（右/上缘贴屏幕边缘）。全局生效 | `Tooltip.SetRectTransformToSafePosition`（静态） | Prefix（接管） | ——（自研，修原版 bug） | 常开（无开关，全局生效） |
| 34 | **信息栏不闪了**（0.5.19）：悬停**带计时器的单卡**（魔法收集器/鸡…）时原版每帧用 0.1 秒精度的倒计时重写信息栏文本 → 高度测量逐帧抖 → 每帧重排整栏＝闪。修法：高度**量化到 2px 网格** + 文本未变不重复赋值（TMP text setter 会弄脏文本网格）+ 16j 追加块 `EndsWith` 判重 | `GameScreen.LateUpdate`（复用 SidebarHeight 补丁）+ `GameScreen.Update`（16j 判重） | —— | ——（自研，修本 mod 引入的抖动） | 常开（无开关） |
| 35 | **高级集市**（0.5.19 新卡牌：1 铁条 + 1 金条 + **5 硬币**（0.5.20 改）+ 村民合成；**3 运输输入 + 1 运输输出**；运到的卡 10 秒读秒后**整摞一次性售出**，钱币沿运输输出送走；不同种类可卖卡可混叠；0.5.20 修多卡不读条） | `CardData.CanHaveCardOnTop`（混叠放行）＋卡片脚本 `AdvancedMarket`（收卡/售卖对齐原版 `ExportCenter`/`Market`，`WorldManager.SellCard` 整叠结算） | Postfix ×1 | 原版 `Market` / `ExportCenter` | 「卡牌：高级集市」 |
| 36 | **时间倍数沿用**（0.6.7）：月结（换月亮 / 下一回合）与换地图后保持原来的速度倍数，不再被原版强制打回 1 倍；全地图生效；与「切换地点后自动暂停」配合（解除暂停后仍是原倍数） | `WorldManager.EndOfMonth` + `WorldManager.GoToBoard` + `WorldManager.Update` | Prefix ×2 + Postfix | ——（自研，修原版强制 1 倍） | 常开（无开关） |

**开关方式（0.4.0 起）**：主菜单 → **Mods** → 选中本 mod → **41 项（0.6.6）＝8 个卡总开关 + 20 个功能开关 + 12 个数值输入框 + 1 个文本框（快捷键）**，点了立即生效（不用重启游戏）。
0.5.18 起：堆叠上限 / 归堆距离 / 各容量上限都是**数字输入框**（int/float 条目原版就渲染成 TMP_InputField 输入框），
**填回原版值＝原版行为**，所以不再配"开/关"开关；硬币磁力箱 / 贝壳磁力箱 / 磁力信用卡三个开关合并成「卡牌：磁力货币箱」。
值持久化在 `Mods\StacklandsProMax\config.json`；删掉这个文件就恢复默认（全开）。
代码上：每个开关是 `ModConfig` 里的一个静态字段，补丁/逻辑开头 `if (!ModConfig.Xxx) return;`；
堆叠上限与归堆距离因为是 transpiler，改成了运行时调用 `ModConfig.GetMaxStackSize()` / `GetStackSearchDistance()`。

> 0.5.0 按用户要求「每个新增的卡牌只留一个总开关，**其他多余的开关去掉**」，把
> **箱子自动吸附 / 取出后不被回吸 / 外观同原版 / 磁力箱配方 / 高级仓库 +140 / 仓库可互相叠放** 这 6 个子开关
> 全部撤掉、**改为常开**（在 `ModConfig.cs` 里是 `const bool = true`，保留命名方便以后临时关掉再编译）。
> 旧 `config.json` 里遗留的键会留在文件里但不再生效。

如果连补丁都不想打，也可以把 `Mod.cs` 的 `PatchClasses` 里对应行注释掉（见 本文「补丁清单」）。
`Patches\` 目录仍是"一个功能一个文件"，逐个 try/catch 应用：**某个补丁失败不影响其它功能**，日志里会写明哪个失败。

---

## 功能实现原理

### 1. 堆叠上限（默认 100，0.5.18 起是设置里的数字输入框）

原版唯一的硬上限就在 `CardData.CanHaveCardOnTop` 里：它把「根卡现有子卡数 + 1」与「要叠上去那一叠的卡数」相加，两者之和超过 **30** 就拒绝叠放（预制卡按 1 张算）。

这个方法同时是**拖拽落卡**（`GameCard.Bounce`）、**自动堆叠**、**传送带输出**的判定入口，所以只改这一处常量就够。

我用 Mono.Cecil 直接读了 IL 确认：这个方法里 `ldc.i4.s 30` **只出现 1 次**（`IL_0046`），所以 transpiler 会精准命中，不会误改其它常量。

### 1b. 原版磁铁的上限（这里有个坑）

原版 `WorldManager.TrySendToMagnet` 里有 `resourceMagnet.MyGameCard.GetStackCount() < 30`，不处理的话磁铁只吸到 30 张。

**注意**：IL 实测表明这个 `30` **并不在 `TrySendToMagnet` 方法体内**，而在编译器为 lambda 生成的闭包方法
`<>c__DisplayClass204_0.<TrySendToMagnet>b__0` 里 —— 也就是说，对 `TrySendToMagnet` 做 transpiler 是**静默无效**的。
所以必须用 Prefix 接替整个方法（`HigherStacks` 用 Prefix 也是这个原因）。本实现把条件换成 `< MaxStackSize`（与原版结构一致，只换上限）。

### 2. 点一下开完整个卡包

原版 `Boosterpack.Clicked()` 一次只开一张。**我没有重写开卡逻辑**——那会漏掉首包特判、Cities 天气事件、村民补给、保底规则等等。

做法是"反复调用原版 `Clicked`"，效果与玩家手动连点完全相同：

- **Prefix**：`TimesOpened < TotalCardsInPack` 才放行 → 开完的包不再响应，同时天然防止无限递归
- **Postfix**：还没开完就排一个 `0.05` 秒后的下一次 `Clicked`

额外改进：加了一个"同一卡包只允许一条连锁"的去重（HashSet）。参考 mod 没有这层保护，玩家自己连点时会有多条连锁并行，开卡速度翻倍。

最后一张开完时，原版自己会播烟并销毁卡包，无需我们处理。

### 3. 打败女巫后不再刷随机传送门

**不去硬删传送门**，而是用官方开关：原版 `EndOfMonthCutscenes.SpecialEvents()` 里本来就是

原版那段判断是：月份大于 8 且能被 4 整除 → 候选为真；若 `CurrentBoard.BoardOptions.CanSpawnPortals` 为 false，候选直接置假；候选成立才刷 `strange_portal` / `rare_portal` 并播"奇怪传送门出现了"剧情。

所以在事件执行前把 `CanSpawnPortals` 置 false 即可 —— 整个分支连同剧情一起跳过，**不会留下"传送门出现了但什么都没有"的空剧情**。

判断"女巫已死"同时看两处，因此对**已经打死过女巫的老存档**也生效：

- 存档里的 `CurrentSave.LastPlayedRound.RunVariables.FinishedWickedWitch`
- 当前这局的 `CurrentRunVariables.FinishedWickedWitch`（参考 mod 只查了前者）

### 4. 空闲村民 / 空闲生产设施红色边框（0.5.6 起两半都合并了）

用的是**原版自带的卡片高亮框**，不新增任何渲染对象：

- `GameCard.HighlightRectangle`（Shapes 虚线方框）+ `GameCard.HighlightActive`
- 原版 `GameCard.Update` 会在 `HighlightActive` 为真时用它画框、颜色取场地的 `CardHighlightColor`
- 所以我们在 Update **之后**（Postfix）改成红色（`ModSettings.IdleBorderColor`），并自己把 `enabled` 置真（原版那行赋值已经跑过了，这帧不置真就看不到）

**A. 村民**（0.5.0 合并，设置项「空闲村民红色边框」）：

```
是 BaseVillager
且 玩家没在拖它（!BeingDragged）
且 没在建筑里干活（!IsWorking）   ← 与参考 mod 的差异，见下
且 没有父卡也没有子卡（没被叠着、手上没拿东西）
且 不在战斗中（!InConflict）
```

> **与参考 mod 的差异（有意为之）**：Show Idle Villager 只判断"无父无子无战斗"，因此**在建筑里工作的村民也会被标红**。
> 既然需求是"空闲村民"，我多加了 `!IsWorking` 一条。如果你希望和参考 mod 完全一致，删掉
> `IdleVillagerBorderPatch.IsIdleVillager` 里那一行即可。

**B. 生产设施**（0.5.6 合并，设置项「空闲生产设施红色边框」）——
用户原话：「把Show Idle Villager这个mod的生产设施高亮也合并过来」。

参考 mod 的 `IsFarmIdle()` 认**五类卡**，判定就一句 **`Parent == null && Child == null`**：

| 卡 | 是什么 | 为什么"空着"要提示 |
| --- | --- | --- |
| `Garden` | 菜园 / 农场 | 空着＝还能种；上面长出食物就是子卡，边框自动消失 |
| `FishingSpot` | 钓鱼点 | 派了渔夫或钓到鱼都是子卡 |
| `FishTrap` | 捕鱼陷阱 | 同上 |
| `Greenhouse` | 温室 | 同上 |
| `Poop` | 便便 | 它在原版 `growMethods = { soil, poop, garden, farm, greenhouse }` 里，是**能种东西的基质**，散在地上没被用掉就值得提醒 |

与原 mod 的差异：① 它五类卡**各一个开关**（外加村民一个），本项目按"开关要克制"收成**两个**（村民一个 + 设施一个）；
② 和村民那条一样额外排除"正被玩家拖着"的卡；③ 颜色统一取 `ModSettings.IdleBorderColor`（原 mod 两者都是 `Color.red`）。

## 右键按住拖动（整叠 + 单张）（0.3.0 起，0.3.1 起对单张也生效）

**玩法**：在一叠牌上的**任意位置**（最底下那张、中间的、最上面那张都行）按住**鼠标右键**并拖动 → 整叠跟着鼠标走；
**单张牌也一样**，右键按住直接拖动它。松开右键放下（落点靠近别的牌时会照常吸附/堆叠）。

**为什么需要自己实现**：原版拖拽由 `WorldManager.Update` 里的输入逻辑驱动，而 `InputController` 只有在可访问性选项
**"Click to Drag"（默认关闭）** 打开时才会把右键登记成拖拽输入，所以默认配置下右键完全不参与拖拽（右键拖动是**平移镜头**用的）。
另外原版其实已有"整叠拖动"的先例：按住 `LeftShift` 拖会抓 `GetRootCard()`。

**做法**（`Patches\RightDragStackPatch.cs`）：直接读原始鼠标状态 `Mouse.current.rightButton`，然后**复用原版拖拽**：

- 右键按下时：把 `WorldManager.DraggingDraggable` 设成**牌堆根卡**、算好 `grabOffset`、调用 `StartDragging()`
  （`HoveredCard` / `mouseWorldPosition` / `DraggingDraggable` / `grabOffset` 都是 public 字段/属性）
- 之后每帧原版逻辑会自己让卡片跟随鼠标、做落点吸附与高亮
- 松开右键时调用原版**私有** `DropCard()`（用 `AccessTools.Method` 取），落点吸附、自动堆叠、音效全部交给原版
- **单张牌和整叠都接管**：抓的始终是"根卡"，所以点牌堆任意一张都会拖动整叠；单张牌右键拖 = 直接移动它
  （右键拖**不会触发该卡的 `Clicked()`**，所以对"左键 = 取出物品"这类卡——磁力箱、储物箱——特别顺手）
- 额外一个 `InputController.GetInputBegan` 的 Postfix：当 "Click to Drag" 打开时，右键会被原版当成拖拽输入而抢先抓走"鼠标下那一张"（把牌堆拆开），
  这里让它在"右键刚按下"这帧返回 false，把接管权让给我（默认配置下这个补丁没有任何影响）

> 右键**空白处**仍然是原版的平移镜头 ✓ 只把"右键点在牌堆上"这一种情况接管了。
>
> **为什么不用 Better Mouse Controls 的做法**：它让 `InputController.GetKey(LeftShift)` 在右键按下时返回 true，
> 从而复用原版"LeftShift 拖整叠"。那条路依赖原版对 LeftShift 的处理，且**对单张牌的行为不如本实现可控**
> （本项目要的是"点到牌堆任意一张都拖整叠、单张也能拖"）。所以这一项保持自研实现，只合并了它的双击归堆。

## 高级仓库（0.3.0；0.5.6 界面名由"超级仓库"改来 + 加第二种配方）与高级温室（0.5.6 新增）

> **参考出处（用户 2026-09-14 明确说明）**：这两张卡都参考自 **Stacklands Plus**（创意工坊 `3355058679`，id `stacklands_plus`，程序集 `StickWorkshopMod.dll`）。
> 用户原话：「**高级温室的参考自mod：Stacklands Plus，这个mod的仓库工坊优化为高级仓库，其他东西没什么必要就不迁移了**」——
> ① 它的 **`WarehouseWorkshop`（"仓库工坊"：`base("warehouse", "warehouse", 10f, { iron_bar:1, stone:1 })`，用仓库 + 1 铁条 + 1 石头升级）** → 本项目的**「高级仓库」**；
> ② 它的 **`SuperGreenhouse`（超级温室）** → 本项目的**「高级温室」**（它另建自己的生长蓝图卡，我们改成直接改原版 `blueprint_growth`）；
> ③ 它还有 `SuperFarm` / `SuperGarden` / 各种"XX 工坊" / 技术研究中心等，**本项目都不迁移**（用户："其他东西没什么必要就不迁移了"）。
> 详细的补丁点对照见 本文 §9.1。

### 4.1 高级仓库

**玩法**：两种配方任选一种——
① 把 **10 张仓库**叠成一叠（**20 秒**，10 张都消耗）；
② **10 个石头 + 10 个铁条 + 一名村民**（**120 秒**，只消耗石头和铁条——村民按原版规则作为"建造者"不消耗；村民用的是原版特殊 id `any_villager`，任何村民都行）。
每个高级仓库给场地 **+140 张卡牌上限**。

**容量数字的来源**：原版的"仓库容量"**不是仓库自己存东西**，而是 `WorldManager.CardCapIncrease(board)` 里按卡 id 硬判累加的：

它按卡 id 硬判累加：`shed`（棚屋）**+4**，`warehouse`（仓库）**+14**（＝一个普通仓库等于 +14 张场地卡牌上限），`lighthouse`（灯塔）**+14**，其余卡不贡献。

所以 10 个仓库 = 140 ✓（与你给的数字一致）。高级仓库用**一个 Postfix 在这个结果上再加 140**，比起去改原版那串 `if` 更安全，也不会和别的 mod 抢同一个常量。

**两个必须处理的坑**（都在 `Patches\SuperWarehousePatch.cs`）：

1. **仓库得能互相叠**：原版合成是把材料叠成一条堆叠再匹配配方（`Subprint.StackMatchesSubprint`），而仓库默认不让自己叠自己。
   这里在 `CardData.CanHaveCardOnTop` 上放行"仓库 + 仓库"这一种组合——它是所有落卡/堆叠校验的最终入口，能覆盖拖拽落卡与堆叠有效性检查，其它组合不受影响。
2. **配方原料必须显式消耗**：`Subprint.GetAllCardsToRemove()` 默认**不消耗 `CardType.Structures`（枚举值 0）的卡**，
   而仓库正是 Structures → 不写 `CardsToRemove` 的话，10 张仓库不会消失（可无限白刷）。所以蓝图里把 10 个 `warehouse` 也写进了 `CardsToRemove`。

**卡牌本体**（`Cards\super_warehouse.json`）用最朴素的 `CardData` 脚本 + `_IsBuilding: true`（建筑配色），图标借用原版 `warehouse`，
容量效果全部由补丁提供；创意卡（配方）已加入 `AdvancedBuildingIdea` 卡包。
**0.5.6 的两处改动**：① 界面/卡片显示名从「超级仓库」改成「**高级仓库**」（**id、类名、config.json 的键名都不动**——按项目约定键名永不复用/改名，改名只动 `localization.tsv` 的 `_name` 文本）；② 蓝图里加了**第二条 subprint**（10 石头 + 10 铁条 + `any_villager`，120 秒），`CardsToRemove` 只写石头和铁条，所以**村民不会被消耗**。

### 4.2 高级温室（0.5.6 新增）

用户原话：「新增卡牌"高级温室"，6个普通温室合成，卡牌外观和温室保持一致，主意卡牌也和温室的一致，高级温室支持放6个材料，可以一次产出6个材料，原版温室的6倍，已下载到mod（Stacklands Plus）中有个超级温室，功能就参考他的，但是外观用普通温室的」。

- **零新类**：`Cards\advanced_greenhouse.json` 里 `"script": "Greenhouse"` 直接复用原版那个空类（**温室的行为全在生长蓝图里**，本体只重写 `CanHaveCard`），图标借原版 `greenhouse`、`_IsBuilding: true`。
  **图标口径（试了三次，最终由用户拍板"就这样"）**：卡牌用原版**温室卡**的图标（JSON 里 `"icon": "greenhouse"`，✅ 正确）；**主意卡图标至今仍是原版的灯泡兜底图**（`SpriteManager.IdeaIcon`）——① JSON 写 `"icon": "greenhouse"`＝温室**卡**的图（用户截图否掉）；② JSON 写 `"icon": "blueprint_greenhouse"`＝**拿不到**，界面回退成灯泡；③ `Mod.Ready()` 里 `ApplyVanillaIdeaIcon()` **把原版温室主意卡的 `Icon` 字段整个对齐（连 `null` 一起对齐）** + `BlueprintPrefabs`/`CardDataPrefabs` 兜底查找 —— 用户实测**还是灯泡**，随后说「**主意的图片还是灯泡，算了就这样吧**」。所以：**不要再在这方面花时间**（`CardData.Icon` 这条路已证明走不通；如果哪天真要改，只剩"自己画一张 `Icons\*.png`"或"运行期从某张活着的原版主意卡上取 `GameCard.IconRenderer.sprite`"两条路，且都要先问用户）。代码保持现状。
- **配方**：**6 张温室叠成一叠 → 40 秒 → 高级温室**（6 张都消耗）。40 秒是照 Stacklands Plus 的超级温室取的（需求里只指定了高级仓库的 120 秒）。
- **温室可堆叠（0.5.24 修）**：配方上线后用户报「高级温室可以通过堆叠温室合成，但是温室无法堆叠」——原因是原版 `Greenhouse.CanHaveCard` 只放行 **Resources / Humans / Food**，温室自己是 `Structures`，所以"温室 × 温室"恒为 false，6 张永远叠不到一起。
  修法与高级仓库（23）同源：`GreenhouseStackingPatch`（`CardData.CanHaveCardOnTop` **后置**）只在原版已返回 false、且"目标卡＝普通温室、拖上来的＝普通温室"时改为 true，并**自己补一次堆叠上限检查**（这个上限就写在被我们改写返回值的那个方法里）。
  只放行**普通温室 ↔ 普通温室**：高级温室不算进 6 张配方，且它身上叠的是永久循环的种子，放行只会让玩家失手把种子顶掉。
- **生长循环**（`Patches\AdvancedGreenhousePatch.cs`）：原版 `blueprint_growth`（`BlueprintGrowth`）在 `Init()` 里按"每种种子 × 每种基质（soil/poop/garden/farm/greenhouse）"生成配方；我们把每条"种子 × 普通温室"的配方**复制**成
  `RequiredCards = [高级温室, 种子 ×6]`、`ExtraResultCards = 原版产物 ×6`（时间/状态文字/`ResultAction` 对齐），`YieldPerSeed = 1`＝**每个种子产 1 份**。
  **消耗口径由用户拍板**（原话）：「高级温室放 6 胡萝卜，产出 6 胡萝卜，产出的胡萝卜要弹出来，产出后，温室里还是有 6 胡萝卜，会自动开始下一轮产出」——
  `CardsToRemove` 写成一个"什么都不删"的**哨兵数组** `new string[1] { "" }`（留空＝走原版默认规则去删种子，不行；`Subprint.GetAllCardsToRemove()` 只看它是否非空）。
  于是：种子**永不消耗**、6 份产物**各自弹出去**、温室上仍是那 6 个种子 ⇒ 配方下一帧立刻再匹配 ⇒ **自动开始下一轮**。
- **运输输出（0.5.6 用户追加；我第一版做错、被用户当场纠正）**：用户原话「**高级温室要有运输输出，一个就行，产出的6个胡萝卜通过这个输出到指定地方（炖锅、箱子等）**」，我第一版只加了原版 `_EnergyConnectors`（Cities 管道），用户随即说「**main场地也有运输输出，比如冶炼厂、锯木厂、采石场等等，你是不是用错了**」——**他说得对**：原版有**两套**"输出"：
  ① **输出方向**（冶炼厂 / 锯木厂 / 采石场 / 农田…**主大陆和岛上都有**）：卡上的 `[ExtraData("output_direction")] OutputDir` + **可点击的小箭头**（`DirectionCircleElement.Clicked()` → `GameCard.ToggleDirection()` 四方向循环），产物由 `Blueprint.BlueprintComplete()` 里的 `StackSendCheckTarget(温室, 产物, OutputDir, 温室)` 送出 → 依次试 管道 → 磁铁 → `StackSend`（`TrySendToChest`：2 格内**配套**箱子自动收）→ 朝方向抛出（`SendIt()` 里 `Velocity = OutputDir * 7f`，这句**只在 Cities 生效**）。
  ② **储物箱的"运输输出"连接器**（`_EnergyConnectors`）：`GameCard.UpdateConnectors()` 里 `CurrentBoard.Id != "cities"` 就把连接器**全部隐藏**，所以主大陆看不到、接不上（我第一版就是只做了这个）。
  本项目的最终做法（`Patches\AdvancedGreenhouseOutput.cs`）＝**① 为主 + ② 保留**：
  - `Cards\advanced_greenhouse.json` 的 `script` 换成 `StacklandsProMaxNS.AdvancedGreenhouseCard`（`Greenhouse` 子类）：重写 `CanSelectOutput() => true`（和 `Factory`/`EnergyConsumer`/`Farmland` 一致），并在 `UpdateCard()` 里**运行期把原版那张"输出方向箭头"从原版卡预制体上 `Instantiate` 一份装到自己身上**（mod 卡是运行时 `AddComponent` 建的、**没有预制体**，所以原版箭头借不过来就得自己造；本项目的既有做法就是"按 id 引用不可靠时，运行时从原版已经正常的对象上复制引用"）；
  - `AdvancedGreenhouseOutputPatch`（Prefix `WorldManager.StackSendCheckTarget`）：产物要往外送时，先在温室周围 `_OutputRadius`（默认 2.5 格，JSON 可调）里找 **`CanHaveCardOnTop` 认可的容器**（配套箱子 / 炖锅…，**优先箭头方向上那个**，排除种植基质避免"又种回去"、排除自己这堆），找到就 `StackSendTo` **直接送进去**；**找不到**或**已经接了管道**就原样放行交回原版（照旧往外弹）——纯增量。
  - 保留 `_EnergyConnectors`（1 路 output/Transport）＝Cities 场地仍可走原版管道。
- 参考 mod（Stacklands Plus 的超级温室）是**另建一张自己的生长蓝图卡**再把它塞进卡包；本项目直接改原版生长蓝图 → **玩家能种普通温室就能用高级温室，少一张卡、少一处联动**。
- ⚠️ 两个坑（都在补丁注释里）：① 注入必须**幂等**，且在 `Mod.Ready()` 与 `BlueprintGrowth.Init` 后置**两处**都调——因为 `Init()` 会 `PopulateSubprints()` 清空重建，时序不定；② **追加的 `Subprint` 必须自己补 `ParentBlueprint` 与 `SubprintIndex`**（原版只在 `Blueprint.Init()` 里统一设）。漏了 `ParentBlueprint` 会空引用——`CardData.FindMatchingPrint()` 会遍历**所有蓝图的所有配方**，而 `StackMatchesSubprint` 里 `if (ParentBlueprint.NeedsExactMatch && ...)` **只在堆叠张数 ≥ 本条配方的必需卡数（7）时才走到**，于是表现成用户实测到的"**一次从箱子里取出很多张（≥7）时最上面那张卡名错乱成 Rabbit、整堆还拖不动**"。

---

## "每张卡一个总开关"（0.5.0；0.5.6 起共六张卡；0.6.6 起共八张）

**玩法**：主菜单 → Mods → 本 mod，最上面八个开关分别管**磁力箱 / 磁力货币箱（硬币磁力箱 + 贝壳磁力箱 + 磁力信用卡） / 高级仓库 / 高级温室 / 魔法收集器 / 高级集市 / 磁力容器 / 高级回收中心**。
关掉某一张卡，这张卡就"不存在"了。

**关掉后到底发生什么**（这是与用户约定的边界）：

| 部位 | 行为 |
| --- | --- |
| 抽卡池 | 它的**创意卡**从所有 `SetCardBagData.Chances` 里摘掉（新卡包再也抽不到），重新打开原样放回去 |
| 卡牌图鉴 | 产物卡和创意卡都被标 `HideFromCardopedia`，图鉴里不显示 |
| 配方 | `CardGatePatch` 让 `FindMatchingPrint()` 返回 null → 计时器不启动；已经跑起来的计时器还会被原版取消 |
| **旧存档里已经存在的卡** | **照旧能用**（用户指定）。想让旧卡也失效，得在对应补丁里再加一道 `ModConfig.IsCardDisabled(...)` 早退 |

**为什么能立刻生效**：抽卡池不是预先算好的快照——`CardBag.GetRawCardChanges()` 每次开卡包都直接读
`loader.SetCardBags[*].Chances`，所以运行中改这张 List 就够了。
`Mod.Ready()` 登记卡包时用 `RememberCardBag(cardId, bagType, chance)` 记下"每张卡原本在哪个卡包、权重多少"，
`ModConfig.ApplyCardAvailability()` 就按这份记录"先全摘、再按开关放回"，
所以反复开关不会重复条目、也不会丢权重，并且**只动我们自己的卡**、不碰原版卡池。

## 双击把附近同种卡归成一堆（0.5.0）

**玩法**：双击一张牌 → `ModSettings.DoubleClickRestackRange`（4 格）内**同种**的散卡自动飞过来叠成一堆。
村民和村民算同种、装备和装备算同种（跟原版归堆的口径一致）。

**规则**（与参考 mod 一致）：

- 双击判定：两次点击间隔 < 0.5 秒、鼠标移动 < 5 像素、而且两次都点在同一张牌上
- 点到的牌必须是"干净的同种堆"：它往上（父卡链）或往下（子卡链）整条链都是同种卡
- 排除：装备中的卡、储物箱类（`ResourceChest` / `Chest` / `MagnetChest`——它们点击是"取物"，归堆会打架）
- 点到的牌如果在牌堆中间，会先把它翻到最上面，再把附近的同种卡接上去
- 点到的牌正在战斗时：把附近的同种卡直接拉进同一场战斗（参考 mod 的行为）

**与参考 mod 的差异（有意为之）**：归堆上限**跟随本项目的堆叠上限设置**
（`ModConfig.StackLimitSize`，默认 100，0.5.18 起是设置界面里的数字输入框；填 30 回原版），不再是参考 mod 写死的 30。
参考 mod 那 4 个设置项（开关 / 双击间隔 / 鼠标位移 / 归堆半径）没有搬进设置界面，
而是固定在 `ModSettings.DoubleClickMaxDelay` / `DoubleClickMaxDistance` / `DoubleClickRestackRange`，
只有总开关「双击同种卡自动归堆」是暴露出来的。

## 归堆搜索距离 2 → 4（Stack Further）（0.5.0）

**玩法**：建筑产出的卡（例如苹果树掉苹果）会去**更远**的地方找同类堆叠上去。原版只找 2 格，默认改成 4 格。

原版两处写死的距离：

两处用的是同一个判据：目标与自己的距离不超过 **2 格**、同时也不超过调用方给的半径，才认定为可送目标。

参考 mod 用两个 transpiler 把这两处 `ldc.r4 2` 换成它设置项的值。本项目做法一样，但**换成调用
`ModConfig.GetStackSearchDistance()`**——和 `StackLimitPatch` 一个思路，
所以游戏里改数值/关开关**立刻生效**，不用像参考 mod 那样重启游戏。

我用 Mono.Cecil 核对过：这两个方法里 `ldc.r4 2` **各只有一处**（分别是 `IL_0110` / `IL_0061`），
所以是无脑替换也不会误伤其它常量的安全改法（`ldc.r4 0 / 3.402823E+38 / 4 / 7` 都不会被碰到）。

> 原版还有第三处同款 `2f`——`WorldManager.GetBestCardInDirection`（传送带出口选目标）。
> 参考 mod 没有改它，本项目保持一致也不动，避免超出宣传范围地改变原版手感。

## 磁力箱三路运输输出（0.5.0）

**玩法**：磁力箱像原版储物箱一样接**运输管道**，接上后每 **10 秒**输出一次，
**每一路已连接的输出各发一张**（接三路就一次吐三张）。

**原版储物箱的机制**（对齐对象）：

- 连接器来自 JSON 的 `_EnergyConnectors`（反射写 `CardData.EnergyConnectors`，`GameCard.CreateCardConnectors()` 按它建）
- `UpdateCard` 里 `GetOutputConnector()` 只取**最后一个**运输输出，接上了就跑 `StartTimer(10f, OutputCard, ...)`
- `[TimedAction("output_card")] OutputCard()`：造一张卡 → `WorldManager.StackSendCheckTarget(MyGameCard, 卡, Vector3.right)` → 数量 -1
- `StackSendCheckTarget(..., outputIndex)` 里的 `outputIndex` 是"走第几路管道"（`TrySendWithPipe` 先按它选连接器，选不到才轮询）

**本项目的改动**（`MagnetChest.UpdateOutputTimer` / `OutputCard`）：

1. 用 `HasConnectedTransportOutput()` 判断"**任何**一路接上了" → 跑 `_OutputInterval`（默认 10 秒）计时器
2. `OutputCard()` 遍历所有"运输 + 输出 + 已连接"的连接器，**每个各造一张卡**，用 `outputIndex: i` 指定第 i 路管道发出，
   并发 `MarkAsRecentlyRemoved` 登记取出免疫（防止刚吐出来的卡被自己吸回去）
3. 声明 3 路输出：`"_EnergyConnectors": [{ "EnergyConnectionType": 1, "EnergyConnectionStrength": 5, "EnergyConnectionAmount": 3 }]`

> ⚠️ **踩坑记录**：这里必须用**枚举的数字值**（游戏反序列化用的是 `JsonSerializer.CreateDefault()`，没有字符串枚举转换器）。
> `CardDirection`：`input=0` / `output=1`；`ConnectionType`：`LV=1` / `HV=3` / `Sewer=4` / **`Transport=5`**。
> 我第一版把 `Transport` 当成了 3（`HV`），会被静默反序列化成"高压电连接器"而不是运输管道；
> 现在 `verify.ps1` 会对这两个字段做合法性校验，写错会在离线阶段就报 FAIL。

**限制**：连接器的 UI 只在 **Cities 场地**显示（原版 `GameCard.UpdateConnectors` 在非 cities 场地直接隐藏），
而本 mod 的配方在 Cities 不触发（见 本文「已知限制与不变量」限制 1），所以要在 Cities 上用调试菜单生成磁力箱。
新增的 `ConnectorDiagnosticsPatch` 会在启动时把原版 `resource_chest` / `coin_chest` 与本箱的连接器打进日志，方便对照。

**⚠️ 连带必须做的一件事**：加了计时器（状态条）之后，必须给这张卡重写
`CanHaveCardsWhileHasStatus() => true`，否则 `GameCard.Bounce()` 和拖拽落点判定会因为
"这张卡有状态时不许叠卡"（基类默认 false）而**拒绝所有落卡**——
症状就是"材料吸过来在箱子顶上弹跳、进不去"。原版 `ResourceChest` / `FoodWarehouse` 正是因此才重写它。
本项目的 `MagnetChest` 与 `CoinMagnetChest` 都已重写。

---

## Ctrl+拖空白框选多张卡（0.5.0 合并 Multi Select & Drag）

**玩法**：按住 **Ctrl 在空白处拖出一个框** → 框内的散卡全部被选中（松手时结算，每次框选都是**替换**整个选择）；
选中后**拖任意一张被选中的卡** → 整组按原来的相对位置一起走，松手各自落地；
**只有点击空白处才取消选中**——搬完一轮**选择与黄色高亮都保留**，可以接着拖第二趟、第三趟。
（Ctrl+单击单张增/减选择、Shift 框选追加这两个能力**已按用户要求删掉**——只保留"框选"这一种选中方式。）
（0.5.5 用户原话：「在选中并拖动一次后，高亮/选中状态就消失了，改成高亮/选中状态一直存在，可以再次拖动移动，
直到点击空白处取消高亮/选中状态」——**早期那版搬完就清掉、别再改回去**。）

> **0.5.5 补充：点牌堆的"任意位置"都能整组拖。**
> 用户原话：「这个功能在选中卡后，要想移动，只能点击单张卡或者牌堆中最下面的那张卡，我想去掉只能点牌堆最下面那张卡的限制，
> 改成"点击牌堆的任意位置"也能实现移动」。
> 原因与做法：选择集里存的是**一叠的根卡**，接管判据是 `Selected.Contains(DraggingCard)`；玩家点牌堆中间/上面那张时，
> 原版拖的是**子卡**（`StartDragging()` 会先把它从堆里拆出来），匹配不上 → 只把那一张拖走。
> 现在由 `BoxSelectStackTakeoverPatch`（Prefix `GameCard.StartDragging`，`Priority.Last`）在**拆堆之前**把它改写成
> "抓这一叠的根卡"（那时 `GetRootCard()` 还是真正的根），于是既匹配选择集、又天然带着整叠子卡。
> 只在**选了不止一张 + 左键拖 + 还没被别人接管**时生效；只选一张时保持原版语义（点子卡＝把上面那半拆下来）。

**怎么实现的**（`Patches\BoxSelectPatch.cs`，合并自创意工坊 `3780343990` / id `multidrag` v1.0.0 的机制）：

1. **框选判定**：Ctrl+左键按下时若鼠标不在卡上、不在 UI 上，就进入框选状态并记下起点；鼠标移动时实时记录终点，
   松手（或松键盘）时把屏幕矩形内的卡加进选择。只选"一叠的根卡"（`Parent == null`）、当前场地、可被拖动的卡
（筛选细则与四个真实踩坑见下面「⚠️ 四个真实踩坑」小节——**别只看 `CanBeDragged()`**）。
2. **整组拖动**：不自己写拖拽逻辑——等原版把选中卡之一拖起来（`WorldManager.DraggingCard` 落在那张卡上）时接管：
   记下每张同组卡相对主卡的偏移，给它们设 `TargetPosition = 主卡.TargetPosition + 偏移`（原版每帧会自己插值过去）。
3. **松手收尾**：只清同组卡的 `BeingDragged`/`ClickedObject`/`DragTag`（**连子卡一起**，因为
   `GameCard.StartDragging()` 会把整叠都置真），**不调** `DropCard()`。主卡的落点吸附、自动堆叠、音效
   仍由原版在松手那一帧的 `DropCard()` 负责，同组卡停在各自偏移后的落点。
   （第一版曾逐张调 `DropCard()`，是错的——理由见下面「⚠️ 坑 4」。）
4. **三个必须挡掉的原版行为**：Ctrl+拖空白本来是**平移镜头**（`GameCamera.StartDragging`）；
   Ctrl+点卡会触发卡片自己的 `Clicked()`（例如箱子取物）；Ctrl+点卡包会开包。三个都用 Prefix 早退挡掉。
5. **覆盖层**：选择框与已选卡描边画在 `OnGUI` 里（`Mod` 继承 `MonoBehaviour`，直接加一个 `OnGUI` 回调即可）。
   原版没有现成的框选 UI 可复用，所以这是纯屏幕层的几个矩形，不新增任何游戏内对象。
   为此给工程加了一个程序集引用：`UnityEngine.IMGUIModule`（`GUI`/`Event` 在这个 dll 里）。

**与参考 mod 的差异（有意为之）**：① 有总开关「框选多张卡」，关掉后四个补丁全部早退（零开销）；
② 收尾与它一致：只清 `BeingDragged`/`ClickedObject`/`DragTag`，不调 `DropCard()`（**坑 4** 就是第一版改错了这里）；
③ 加了"最多同时搬 200 张"的上限（`ModConfig.BoxSelectMaxCards`），防止框住上百张时每帧算一堆位置；
④ 出错时会**自己收摊并重置选择**，绝不每帧刷日志。

### ⚠️ 四个真实踩坑（0.5.0 全部修过；这套筛选规则别再改错方向）

**坑 1：装备件会被框进来 → 移动村民后装备"爆出来"**

- **装备件不在堆叠链里**：`GameCard.Equip()` 只做 `EquipmentChildren.Add(卡)` + 设 `EquipmentHolder` / `IsEquipped`，
  并且**先 `RemoveFromStack()`** —— 所以装备件 `Parent == null`、`HasParent` 为假，**看起来就像一张"散卡"**。
- **它的位置每帧由原版接管**：`DeterminePositionType()` 返回 `PositionType.IsEquipped`，随后
  `TargetPosition = EquipmentHolder.transform.position`（`GameCard.UpdatePosition()`）。
- **而 `GameCard.CanBeDragged()` 不检查 `IsEquipped`**：只看 `CardData.CanBeDragged && FaceUp && !BeingDragged`。
  于是装备件能通过"可拖"筛选 → 被框进选择 → 我们给它设 `TargetPosition`，**和原版那套定位打架** → 就是"爆出来"。
- **修法**：筛选里加 `if (card.IsEquipped) return false;`（装备件本身不参与）。

**坑 2（过度修正）：为了保装备，把"带装备的卡"整个排掉 → 村民再也不能被框选**

- 上一版写着 `if (card.EquipmentChildren.Count > 0) return false;`，结果**带装备的村民/勇士全都框不中、也不跟动**。
- 其实**不需要**：带装备的卡本身是正常可拖的，它的装备件会跟着 `EquipmentHolder.transform.position` 走；
  只要按坑 1 挡住"装备件自己"，就既保住了装备、又不影响村民。
- **修法**：删掉 `EquipmentChildren.Count > 0` 与 `WorkerChildren.Count > 0` 两条（它们不是问题所在）。

**坑 3：动态条件混进"每帧清理" → 正在拖的那张卡被自己删掉，整组永远不跟**

- 原版 `CanBeDragged()` 是 `if (!BeingDragged && CardData.CanBeDragged) return FaceUp;` ——
  **一张卡一旦被拖起来，`CanBeDragged()` 立刻变 false**。
- 而 `PruneSelection()` 每帧都会跑。第一版让它过 `IsSelectable()`（含 `CanBeDragged()`），
  于是"玩家刚开始拖的那张卡"在同一帧被判为不可选、**从选择里删掉** →
  接管条件 `Selected.Contains(被拖的卡)` 永远不成立（症状：拖一张、其他不跟，松开后高亮消失）。
- **修法**：把条件拆成两层 ——
  - `IsStaticallySelectable()`：**只查静态条件**（存在 / 一叠的根卡 / 不是装备件 / 没在建筑里干活），
    `PruneSelection()` **只能用这个**；
  - `IsSelectable()`：静态条件 **+** `card.CanBeDragged()`（动态），只用于"能不能被加进选择"（框选结算）。

当前最终规则（`BoxSelectPatch.cs`）：

```csharp
// 静态：清理选择时也安全
if (card == null || card.Destroyed || card.IsDemoCard) return false;
if (card.Parent != null) return false;      // 只选"一叠的根卡"
if (card.IsEquipped) return false;          // 装备件本身（坑 1）
if (card.IsWorking) return false;           // 在建筑里干活的工人
// 动态：只在"加进选择"时看，绝不参与每帧清理（坑 3）
if (!card.CanBeDragged()) return false;
```

另外 `EndDrag()` 末尾有"兜底清 `BeingDragged`"：万一某张卡没走完原版收尾，`BeingDragged` 会一直是 `true`，
那张卡从此再也拖不动——这里挨个确认一遍。

**坑 4（最隐蔽）：`BeginDrag()` 里漏了"接管标记置真"，整组从头到尾都没被接管**

- 参考 mod 的 `BeginDrag()` 结尾有一行 `dragging = dragCards.Count > 0;`。移植时**漏了这一行**，
  单看结构完全看不出来，但后果是两个症状一起出现：
  ① 主循环 `UpdateGroupDrag()` **每帧都重进"还没接管"分支** → 同组卡从没被设过 `TargetPosition`，只有主卡跟手；
  ② `EndDrag()` 的守卫也是 `dragging` → **永不执行** → 那些卡停在 `BeingDragged = true` 的半拖状态，
  选择集也从没被清掉（症状：拖完一张，它的**黄色高亮一直挂着**）。
- **修法**：`BeginDrag()` 里置真（`dragging = DragCards.Count > 0;`）。（早先那版还顺手"搬完清空选择集"，
  后来用户要求改成**保留选择**，见上面的玩法说明；现在只有点空白处 / 关掉开关才清。）
- **收尾也别用 `DropCard()`**（第一版这么写，是错的）：松手那一帧原版已经替主卡走完 `DropCard()`，
  我们再调是空转；而把 `DraggingDraggable` 逐张换成同组卡再调，用的是**主卡那一帧算出的** `NearbyCardTarget`，
  会把别的卡吸到不该去的地方；`DropCard()` 末尾的 `StopDragging()` 还会在"本次按压没达到拖拽阈值"时
  触发卡自己的 `Clicked()`（箱子会被打开）。正解＝只清 `BeingDragged`/`ClickedObject`/`DragTag`（连子卡），
  卡就停在各自落点（`GameCard.UpdatePosition()` 的 `PositionType.IsRoot` 分支是"朝 `TargetPosition` 插值"）。
- **排查手法**：读自己的代码找不到缺口时，**把参考 mod 的反编译源码调出来逐行对照**——
  这个 bug 就是"逐位打条件"排不掉的那种（条件都成立，是后面的状态没置位）。

> **三条通用教训**：
> ① 判断"一张卡能不能参与搬动"时**不能只信 `CanBeDragged()`**：装备件（`IsEquipped`）和干活的工人（`IsWorking`）
>    都"看起来像散卡"，但位置由原版的 `PositionType` 逻辑接管，谁动它们的 `TargetPosition` 谁就弄坏挂载关系。
> ② **别把"操作本身造成的状态变化"（`BeingDragged`）混进每帧的清理/校验条件里**——
>    否则逻辑会在自己触发的那一刻把自己淘汰掉。这一类 bug（筛选条件过紧 vs 过松）
>    最有效的定位方式是在屏幕上直接画诊断面板：把四个条件位（`A=有DraggingCard` / `B=非Shift拖` / `C=选中>1` / `D=被拖的卡在选中集里`）
>    直接画出来，一眼就能看出是哪一位为 0。（本项目那个 F8 面板是**临时诊断**，定位完已删。）
> ③ **接管/状态标记必须在"进入状态"的那一步置真**，只打条件位是查不出来的；
>    而从参考 mod 移植时，**要逐行对照参考实现**（尤其这种"一行置位"），只对照结构会漏掉决定成败的一行。

---
## 伤害/治疗数字显示在卡牌中间（0.5.0 合并 Hit Text To Center）

**玩法**：战斗时伤害数字画在**卡牌正中**（原版压在卡片下沿、会挡住当前血量），**治疗数字同样居中**。

**原版与参考 mod**（`3016077439` / id `HitTextToCenter` v1.1.0）：

原版 `HitText.SetPosition()`（私有）做三件事：取 `TargetCombatable.MyGameCard`；有 `VeryEffectiveText` 时把它的位置贴到目标卡位置；非 miss 时朝向与位置都取目标卡（位置用带偏移的锚点 `HitTextPosition`，压在卡片下沿），miss 时改成面向摄像机。

参考 mod 的做法就是给这个方法加 Postfix，把非 miss 的位置改成 `myGameCard.transform.position`（并在 `IsMiss` 时不动）。

**它没做、而用户要求补上的那一半：治疗数字**

治疗走的是另一条创建路径：`Combatable.CreateHitText("2", PrefabManager.instance.HealHitText)`（战斗治疗 / `HealLowest` / 吸血回复，以及被动回血）。
**难点是 `HitText` 实例上没有任何字段说明它是什么类型**——`TextMesh.text` 只是 "2"，颜色会随时间淡出，`TargetCombatable` 只说明挂在谁身上。
所以本项目在**创建那一刻**按预制体引用打标记：

```csharp
// Patches\HitTextCenterPatch.cs
[HarmonyPatch(typeof(Combatable), "CreateHitText")]     // public，可直接补
[HarmonyPostfix] … if (prefab == PrefabManager.instance.HealHitText) 记进 WeakTable

[HarmonyPatch(typeof(HitText), "SetPosition")]           // 私有 → 用字符串形式补
[HarmonyPostfix] … 非 miss 且（是治疗 || 伤害居中开关开着）→ position = 卡片正中
```

用 `ConditionalWeakTable` 当"标记表"：实例销毁后自动清掉，不需要自己维护生命周期。

**开关**：设置界面「数字显示在卡牌中间」（`Hit text to center`），默认开。
关掉后**伤害数字回到原版位置**；**治疗数字始终居中**（这正是用户要的那一半，不跟开关走）。

---

## 从箱子里"拖"出材料（Shift 10 张 / Ctrl 1 张）+ 屏蔽原版「Shift+拖整叠」

### 11.1 需求与玩法

**需求原话（0.5.5）**：「目前的资源箱，因为加了 Stacks n Capacity 这个 mod，支持"按 SHIFT 键将资源直接从箱子中拖出！
（CTRL 表示单个单元）"具体功能为：按住 SHIFT，左键对着资源箱按下（按住不松开），出现 10 张牌的牌堆，
左键一直按住，牌堆跟随鼠标，松开左键，牌堆落地。ctrl 则只有一张牌。现在有个问题，在资源箱堆叠在魔法胶水上后，
"按住 SHIFT，左键对着资源箱按下"，会被识别成拖动整个牌堆，这个是游戏本身的逻辑。你要做的：
1 屏蔽掉游戏本身的 shift+左键拖动牌堆的逻辑；2 将上文中的"具体功能"迁移到"磁力箱"中；
3 让"具体功能"可以对堆叠在魔法胶水上的资源箱也生效」。

| 操作 | 结果 |
| --- | --- |
| **Shift + 左键**对着箱子按下 | 取 **10** 张，成一叠**挂在鼠标上**，松开左键才落地 |
| **Ctrl + 左键**对着箱子按下 | 取 **1** 张，同样挂在鼠标上、松开落地 |
| 左键**快速点击**（不带 Ctrl/Shift） | 原样：取 **1** 张，自己弹到箱子旁边（磁力箱另有"10 分钟取出免疫"） |
| 左键**拖动箱子本体**（不带 Ctrl/Shift） | 原样：搬箱子 |
| **Shift + 左键**拖一张**普通的牌**（不是箱子） | **不再拖整叠**，改成拖"你点中的那一张"（＝普通拖动） |

生效对象：**只有本项目的磁力箱**。原版资源箱（Cities DLC 的储物箱）的"拖出"仍由 **Stacks n Capacity** 提供，
我们**一张都不从它里面取**（0.5.5 修掉的真 bug：两边都取 → 双份牌，见 11.3）；但因为判断依据是
"**玩家点中的那张卡**"，所以箱子**叠在谁身上都成立**——包括叠在魔法胶水上（这正是需求 3，
实现路径见 11.3 末尾）。

### 11.2 原版那条逻辑长什么样

`WorldManager.Update`（反编译核对）：

原版是：Shift 按下且鼠标下是 `GameCard` 时把 `IsShiftDragging` 置 true、`draggable` 取**整叠的根卡**（不是玩家点中的那张），点中那张寄存在 `draggable.ClickedObject`（全局只有这一处赋值）；否则 `IsShiftDragging` 置 false。随后统一记 `DraggingDraggable`、算 `grabOffset`（鼠标世界坐标与牌堆位置的差），最后调 `draggable.StartDragging()` —— 我们从最后这一步插手。

所以"资源箱叠在魔法胶水上"时：鼠标下是资源箱，`GetRootCard()` 却是底下的胶水 → 原版抓起整叠 → 就是用户看到的现象。

### 11.3 本项目的做法（**不需要 transpiler**）

在 `GameCard.StartDragging` 上插手（和 Stacks n Capacity 同一个挂点）。原版是"先决定好要抓哪张、再调
`StartDragging()`"，于是我们在 `StartDragging()` 里能看到全部真相：

- `__instance`＝原版准备抓的那张（Shift 整叠拖时是**根卡**）；
- `__instance.ClickedObject`＝**玩家真正点中的那张**；
- `WorldManager.IsShiftDragging`＝这次是不是 Shift 整叠拖。

于是三条需求一次满足：**① 屏蔽整叠拖**＝改抓"玩家点中的那张"；**② 拖出**＝点中的是**本项目的磁力箱**
就把牌取出来、挂到鼠标上，并 `return false` 拦掉原方法。之后**跟随鼠标、落点吸附、松开落地/入箱/音效
全部由原版负责**，本项目一行都不写（就用 `DraggingDraggable` / `grabOffset` / `StartDragging()` 三行，
和右键拖完全一致）。

### 11.3b ⚠️ 0.5.5 修掉的真 bug：原版资源箱"双份牌"（**别再把取牌加回原版资源箱**）

第一版这里还顺手实现了"原版 `ResourceChest` 的拖出"。用户实测：**Ctrl+左键出两张牌、只有一张跟手**；
牌多时 Shift 也会双份（叠在魔法胶水上反而正常）。

- **根因**：本机 `0Harmony.dll` 是 BepInEx **HarmonyX 2.10.1** —— **所有前缀都会执行**，
  `return false` 只决定"原版方法要不要跑"，**拦不住别人的前缀**（这个版本连 `__runOriginal` 都没有，
  前缀里没法知道别人是否已经接管）。而 **Stacks n Capacity 也**在 `GameCard.StartDragging` 上打前缀、
  也认 `ResourceChest`：

  ```csharp
  // Stacks n Capacity 的 Patch_DragItem（Prefix）
  ResourceChest val = __instance.CardData as ResourceChest;
  if (val != null && val.ResourceCount > 0) { …它自己扣一张并 StartDragging… return false; }
  ```

  两边各扣一份 → 双份。Shift 看着"正常"只是因为箱子常被我一次扣空（它的前缀里有 `ResourceCount > 0`）；
  叠在魔法胶水上时原版抓的是**根卡（胶水）**，它看到胶水不动作，所以那格只有一份。
- **修法（用户拍板："资源箱的功能不需要优化"）**：**原版资源箱我们一张都不取**，完全交回原版 / Stacks n Capacity。
  我们只保留 ① 的"改抓玩家点中的那张"。
- **那需求 3 怎么还能成立**：叠在魔法胶水上时原版本来要抓胶水 → 我们的 ① 把这次拖拽**改成抓箱子本身** →
  于是（在那个嵌套的 `StartDragging()` 调用里）Stacks n Capacity 的前缀终于看到的是 `ResourceChest`
  → **它自己**把 10 张拖出来。**没装 Stacks n Capacity 时**：原版资源箱就是原版行为（点击取 1、Shift+点击取 5、
  可拖动），叠在胶水上的箱子退回"把箱子从胶水上拖出来"——不会报错，只是没有"拖出 10 张"这个增强。

> 与 0.5.1~0.5.4 那版（已被整体回退）的区别：旧版是在 `MagnetChest.Clicked()` 里取牌、再由
> `WorldManager.Update` 的 Postfix **延后一帧**抓，于是必须额外处理"抓完同一瞬间被原版丢掉"
> （`InputController.JustEnded` 不重置）——整套要三个补丁。现在在**鼠标按下那一帧**就完成接管，简单得多。

### 11.5 ⚠️ 三个必须注意的坑（都写在 `ChestDragOutPatch.cs` 注释里）

1. **只认左键**：本项目自己的 `RightDragStackPatch` 也在调 `StartDragging()`；而装了 **Better Mouse Controls**
   时它会让 `GetKey(LeftShift)` 在"右键按下"时返回 `true`（它的 `ShiftIsPressedIfRightclick` 补丁）——
   不挡的话右键拖一下磁力箱就会变成"拖出 10 张"。所以用原始输入 `Mouse.current.leftButton.isPressed` 判断。
2. **框选整组拖要让路**：`BoxSelect.BeginDrag()` 会逐张调 `StartDragging()`，那时手里若按着 Shift/Ctrl
   会被误判成拖出 → 用 `BoxSelect.GroupDragging` 提前放行。
3. **`IsShiftDragging` 要复位**：它还是 `GameCard.DeterminePositionType`（悬停卡抬高）/`IsCollapsed`
   （整叠拖时不折叠）读的语义位。我们既然把这次拖拽改成了普通拖/拖出，就必须复位成 `false`；
   **但只在"我们确实改写了这次拖拽"的分支里复位**，普通拖一律不碰。

### 11.6 两个"取牌"路径的区别

| 方法 | 用途 | 关键差别 |
| --- | --- | --- |
| `MagnetChest.RemoveResources(count)` | 普通点击取出 | 走原版 `WorldManager.StackSend`：给牌堆设速度/弧线，让它自己弹出去 |
| `MagnetChest.TakeOutStack(count)` | **拖出**（挂在鼠标上） | **不调 `StackSend`**：否则一是给牌堆设了速度（马上要被鼠标拖，不需要），二是 `StackSend` 第一步 `TrySendToMagnet` 会把它改道送进旁边（**包括自己这个**）磁力箱 → "拖出来一下又回去了" |

（**原版资源箱不在这里**：它的取牌完全归原版 / Stacks n Capacity，我们一张都不取——原因见 11.4。）
两条路径都登记"刚取出免疫"，所以随手丢在箱子边上不会被立刻吸回去。

### 11.7 进游戏怎么验

1. **磁力箱**：Shift+左键按住箱子 → 出来一叠 **10 张**、跟着鼠标走，松开落地；Ctrl+左键 → **1 张**同样跟手；
2. **原版资源箱**（Cities 场地的储物箱）：Ctrl 与 Shift **都只出一份**（这份由 Stacks n Capacity 提供，
   我们不再重复扣数）；没装它时就是原版行为（点击取 1 / Shift+点击取 5）；
3. **资源箱叠在魔法胶水上**：Shift+左键按住**箱子** → 出的是**箱子里面的牌**（10 张跟手），
   胶水和箱子**留在原地不动**（这修的就是需求里那个 bug；Ctrl 在叠放时同样只出一份）；
4. **普通牌堆**：Shift+左键拖牌堆里的一张 → 只把**你点中的那张**拖出来（不再整叠跟着走）；
5. 不带 Ctrl/Shift 的普通点击/拖动箱子，行为与以前完全一样（点击取 1、拖动搬箱子）；
6. 设置界面关掉「卡牌：磁力箱」后，磁力箱的拖出行为随之消失（原版资源箱那条不受影响）。

---

## 0.5.5 另外两个新交互（资源箱取出方向 / 快捷键送往箱子）

### 12.1 取出的牌"往边上掉"（有开关）

**需求原话**：「原版的资源箱，左键点击后弹出牌，是垂直向上的，然后掉到箱子上，不方便，磁力箱的牌是往边上掉的，
你是不是改过，是的话把资源箱也改成往边上掉的，**这个要加个开关**」。

**先回答"是不是我改的"：不是。** 本项目**一行都没补过原版 `ResourceChest`**（`Patches\` 里没有任何
`typeof(ResourceChest)`）。"弹回箱子上"是原版自己造成的：

原版 `ResourceChest.RemoveResources(count)` 先把数量截到 `ResourceCount`、在箱子上方稍高处造出一叠，然后调 `WorldManager.StackSend(该叠根卡, Vector3.right)`，最后把箱子数量减掉。而 `StackSend` 的第一句就是三道截胡：磁铁优先（`TrySendToMagnet`）、其次送箱子（`TrySendToChest`，受 `sendToChest` 开关控制）、已有 `BounceTarget` 的直接返回。

刚取出来的那叠**就在箱子顶上、类型又完全匹配** → `TrySendToChest` 找到的首选目标就是**它自己这个箱子** →
卡被"送"回箱子 → 玩家看到的就是"垂直向上弹一下又落回箱子上"（落上去还会被 `ResourceChest.UpdateCard` 吞回数量）。
本项目的磁力箱没这毛病，纯粹因为 `MagnetChest : CardData` **不是原版的 `Chest`**，`TrySendToChest` 看不上它，
`StackSend` 于是走到最后那句 `SendIt()`（随机方向的小弧）→ 看起来就是"往边上掉"。

**实现**（`Patches\ResourceChestEjectPatch.cs`，Prefix 接替 `ResourceChest.RemoveResources`）：开关打开时自己实现取牌，
唯一的区别是**不走 `StackSend`**（所以既不会被送回自己，也不会被磁铁半路截走），改成一个**确定性的"朝场地外侧弹出"**：

- 方向＝从场地中心指向箱子的方向（再带 ±20° 随机偏角，连续取多张不会叠在同一个点）；
- 落点＝箱子位置 + `1.2` 格，并夹在场地范围内，别弹出界外；
- 初速度用原版 `Draggable.SendToPosition` 的同款公式 `(偏移.x*4, 5, 偏移.z*4)`，抛物线手感与原版一致；
- **不设 `BounceTarget`**，所以它不会自己去找箱子或别的卡。

（这套参数与公式来自 0.5.4 的磁力箱实现——那时用户实测"往边上掉"没问题，随整体回退被删掉了，这次搬来给资源箱用。）

设置项：**「取出的牌往边上掉」**（config.json 里的键仍是旧名 `Resource chest ejects sideways`，默认开；按项目约定**键名永不复用/改名**，只改了界面上的显示文字）。
**0.5.6 补充（同一个开关，同一份弹射代码）**：用户原话「资源箱和磁力箱，ctrl或shift+左键单击，目的是让箱子弹出卡牌，但是卡牌似乎是往正上方弹出的，出来后又被吸回去了，把弹出的方向改一下应该就行了吧」——
拖出是"按下那一帧把牌取出来挂到鼠标上"，玩家**只点一下**（没拖动）时，松手这一帧原版 `WorldManager.DropCard()` 里 `NearbyCardTarget` 是 null（箱子不是 `CardTarget`），
于是走 `CheckIfCanAddOnStack(这一叠)`：它按"**和谁重叠**"找能放上去的卡 → 首选就是它刚出来的那个箱子 → `SetParent(箱子)` → 箱子把它吞回存储。
修法＝取出的那一刻把"这一叠 ← 哪个箱子"记进 `ChestTakeOut`（30 秒有效），松手时若**这一叠还和源箱子重叠**就跳过原版那套，
自己做原版 `DropCard` 的最后两行（`StopDragging()` + `SetDraggingDraggableToNull()`，都是 public，行为一致）再 `ChestEject.EjectToSide()` 弹到旁边；
**已经拖开再松手的一律放行交回原版**（想放哪就放哪）。判据用"是否重叠"是为了对齐原版自己的判据（`GetOverlappingCards()` + `CanHaveCardOnTop()`）。
弹射代码抽到 `Patches\ChestEject.cs`（原来只写在 `ResourceChestEjectPatch` 里），三条路（资源箱点击 / 磁力箱拖出 / 松手回弹）共用一份。
关掉 = Prefix 直接放行 = 完全回到原版（又会弹回箱子上）。
本补丁只改**取出方向**，**不动存储/容量/类型/合成的任何规则**；也不会和 Stacks n Capacity 的"拖出"冲突
（它是 `RemoveResources` 的调用方，拿到牌堆后立刻 `StartDragging()`，而 `StartDragging()` 会清空 `Velocity`）。

**0.5.17 补充（同一个开关）：拖出去的牌可以放回箱子了。** 用户原话「Ctrl或者Shift+左键拖拽出来的卡牌，
还没松开的时候，如果不需要了，可以直接放回磁力箱。目前是无法放回去的，会被直接弹开」。原因：上面那条判据
"松手时还和源箱子重叠"覆盖不了意图——"拖出去再拖回来放回箱子"松手时**也**还压在箱子上。两种情况松手位置
都在箱子上（取出点就在箱子上），位移≈0，所以改用**拖动全程的最大鼠标偏移**区分：`ChestTakeOut` 的取出记录
多存"取出那一刻的鼠标位置"，新 `ChestTakeOutTickPatch`（`WorldManager.Update` 后置）逐帧采样手里这叠的
最大偏移；松手时偏移 **≥0.4 格**＝玩家主动拖动过 → 交回原版（`CheckIfCanAddOnStack` 把它叠回箱子，
`AbsorbStackedResources` 照常入库，数量加回去）；几乎没动＝点一下 → 维持"往边上弹"。
原版资源箱（Stacks n Capacity 取牌）走同一个 `DropCard` 补丁，行为一致。

**0.6.7 补充（同一个开关，同一份弹射代码）：落点改成"优先空地"，并把食物容器纳入。** 用户原话「原版的资源箱和资源容器、食物容器，取出物品时，卡片的掉落位置也要改一下，改成优先往空地掉，这个在资源箱或者磁力资源箱上，应该是已经改过的，你看一下」。

- 上面"朝外 1.2 格 + ±20° 随机"只看方向、**不看那一格有没有东西**：箱子挨着建筑、或旁边已经躺着一堆牌时，取出的卡就正好压上去（还会被自动归堆吞掉，看着像"点了一下东西没出来"）。现在 `ChestEject` 改为**逐候选点试空地**：距离 1.2 / 1.6 / 2.0 格 × 方位角 0°/±30°/±60°/±90°/±135°/180°（相对"朝外"方向），**先近后远、同一圈先朝外**，第一个"没被占"的就用它；圈内全被围死才退回旧的"朝外 1.2 格"。
- "有没有被占"用原版 `GameCard.GetOverlappingCardsInBox` 判：判定盒取卡牌实际尺寸（`GetWidth`/`GetHeight`）的 80%（擦边不算占用，否则相邻卡位之间永远找不到空位），高度给足 2 格；还在飞（`Velocity` 有值）的卡按它的 `TargetPosition` 再比一次距离——否则连点两下取牌时第一张还在半空，第二张会落到同一点。
- **食物容器（`FoodWarehouse`）此前完全没管**：原版 `RemoveFood` 把食物造在自己身上，点击取出走 `StackSend`，兜底是 `SendIt()`（随机方向甩出去），落点全看运气。0.6.7 新增 `FoodWarehouseEjectPatch`（Postfix），取出后交给同一个 `EjectToPileOrSide`；运输输出（`checkOutput = true`）那条路不动。
- 磁力箱 / 磁力容器的点击取出同理：`StackSend` 之后若没有磁力族或同种牌堆认领（`BounceTarget == null`），也改成找空地落。
- "2 格内有同种牌堆就飞过去叠上"（0.5.19 起）**优先级仍在最前**——连续 Shift 取出的多叠牌要自动并堆，那是另一条需求。

### 12.2 快捷键把目标卡/牌堆"送往箱子"（默认 F，0.5.9 前为 R；按键可在设置界面改）

**需求原话**：「Stacks n Capacity 插件有个功能是"按 X 将目标物品发送到宝箱/堆叠"，这个功能可以让单张卡牌或者牌堆弹起，
从而被吸入箱子，把这个功能拿过来，然后改下按键，改成 R，**mod 设置界面可以修改快捷键**」。

**参考 mod 那条逻辑（`3378143608` 的 `Patch_Hotkeys.Postfix`）核心就三行**：

它挂在 `WorldManager.Update` 的后置上：按下的键若是 X（`Key` 38），就取鼠标下那张卡的根卡；根卡存在、可拖动、当前又没有落点（`BounceTarget == null`）时，调一次 `StackSend` 把它朝右送出去。

本项目 `Patches\SendToChestHotkeyPatch.cs` 挂在 `WorldManager.Update` 的 Postfix 上做同一件事——
**"飞"的逻辑一行都不用写**，全交给原版 `StackSend`（先问 `TrySendToMagnet`，本项目磁力箱正好挂在那里"截胡"；
再问 `TrySendToChest`；都没有就找 2 格内同类堆；最后 `SendIt()`）。两处改动：

1. **按键可配置**：设置项「送往箱子的快捷键」（`Send to chest key`，**文本框**，默认 `F`，0.5.9 前为 `R`）填 `Key` 枚举名即可
   （`R` / `X` / `F5` / `Space` / `Digit1` …，大小写不敏感）；认不出来的会**回落到 `F`** 并在日志里提示。
   读键用 `InputController.GetKeyDown()` 而不是裸的 `Keyboard.current`，这样打开菜单/被 UI 吃掉输入时不会误触发。
2. **没有参照它的 Dollar 特例**（它对纸币先 `RemoveFromParent()`）：本项目磁力箱不收纸币，那句对普通牌堆也没意义。
   另外它同名的那个 transpiler（在 `WorldManager.Update` 里对 `SellCard` 附近动刀）是给它自己的"卖出"路径用的，
   与本功能效果无关，**不引入**。
3. **屏蔽原版残留的 F 键调试提示（0.5.25 修）**：用户报「按 F 把卡送进磁力箱时，卡片上方冒出一条小的悬浮提示、
   图标像绿色笑脸」。反编译核实：`WorldManager.Update()` 里留着一段**官方没删掉的调试代码**——
   `if (GetKeyDown(Key.F) && HoveredCard != null) CreateFloatingText(HoveredCard, true, 5, "Test hovered", Icons.Wellbeing, ...)`。
   它**只画提示**（不调 `AddWellbeing`、不改任何数据），而本功能默认键正好是 F → 每次送卡都同时触发。
   修法是 `SuppressVanillaTestFloatingTextPatch`：拦在唯一出口 `WorldManager.CreateFloatingText` 上，
   只挡 `descriptionText == "Test hovered"` 那一次调用（用 `__args` 取第 4 个参数，抗官方改参数名），
   月末结算 / 能源设施产幸福度等正常浮字不受影响。**没有**去动 `WorldManager.Update` 本体（太大、transpiler 删分支易误伤）。

配套开关：「按快捷键把卡送往箱子」（`Send card to chest hotkey`，默认开），关掉 = 快捷键完全不响应。

> 和已装的 Stacks n Capacity **同时开着不冲突**：它绑 X、我们绑 R（可改），各自触发各自的。

### 12.3 Alt+点击搜索主意（迁移并重新设计 Extra Search Mod）

**需求原话**：「迁移并重新设计已经下载的 Extra Search Mod 的功能，**alt+左键单击**：在左侧的主意中搜索并展示
有这个卡牌名称的主意，也就是原 mod 的 Title Search。**alt+右键单击**：在左侧的主意中搜索并展示描述中有这个卡牌名称的主意，
也就是原 mod 的 Description Search」。

**原版机制**（反编译核实）：左侧主意列表由 `GameScreen.UpdateIdeasLog()` 更新，过滤用的是**私有**方法
`KnowledgeMatchesSearch(IKnowledge, string)`，而原版**只比 `KnowledgeName`（标题）**：

它把 `KnowledgeName`（＝`SokLoc.Translate(NameTerm)`，即标题，**不含描述**）转小写、删掉所有空格，再看里面是否包含同样处理过的搜索词。

搜索框＝`GameScreen.instance.IdeaSearchField`（TMP 输入框）：改它的 `.text` 再调一次 `UpdateIdeasLog()` 就是"搜索"。

**参考 mod 的做法**：① Prefix 上面那个私有方法，按一个全局"模式"变量决定比 `KnowledgeName` 还是 `KnowledgeText`
（描述，`IKnowledge.KnowledgeText`）；② `WorldManager.Update` 后置里"抓住卡/按住右键"就把搜索框填成卡名；
③ `GameScreen.Awake` 后置里**用代码新建两个按钮**（Title/Description 切换 + Clear）挂到界面上。

**本项目（`Patches\IdeaSearchPatch.cs`，5 个补丁类）＝换个触发方式 + 换掉它的按钮设计**：

| 交互 | 效果 |
| --- | --- |
| **Alt + 左键单击**一张卡 | 搜索框填该卡名字、模式＝**标题**（Title Search） |
| **Alt + 右键单击**一张卡 | 搜索框填该卡名字、模式＝**描述**（Description Search） |
| **Alt + 单击空白处** | 清空搜索框（回到完整主意列表） |
| 搜索框旁**三个固定按钮** | **搜标题 / 搜描述 / 清除**——搜索框空着时点模式按钮会顺手拿鼠标下那张卡当搜索词 |

用户原话（第二轮）：「原 mod 的侧边栏中只有两个标签，第一个根据情况展示 title 或 description，第二个是 clear。
**改成固定三个标签：搜标题，搜描述，清除**。同时鼠标放在标签上后展示的**悬浮窗直接去掉**」。
所以：① 不做"一个按钮来回显示当前模式"，而是三个固定标签；② **不设 `TooltipText`**，悬停不弹提示；
③ 按钮文字走本地化 term（`SokLoc.Translate`），不像参考 mod 那样写死英文。

**第三轮修复（用户连续四次截图反馈，最后定案：宽度放宽、保字号）**：

- **删掉参考 mod 那排按钮**：我们改不了别人的 mod，但它的按钮就挂在同一个父节点下，特征很明显
  （一个容器 ✅ 正好两个 `CustomButton` ✅ 文字是写死的英文 `Title`/`Description`/`Clear`）。
  `RemoveForeignSearchButtons()` 按这三条命中才 `Destroy`；认不出来就什么都不做（宁可不删也不误删原版 UI）。
  因为它的按钮**可能比我们晚建出来**，所以开局 30 秒内每 30 帧扫一次（之后停，避免每帧遍历）。
- **宽度走过的弯路 + 最终做法**：

  | 版本 | 做法 | 用户反馈 |
  | --- | --- | --- |
  | 1 | 写死 130+130+100（≈376） | 「这次改动之后 ui **宽了一截**出来」 |
  | 2 | 写死 96+96+76 | 「**文字都挤出来了**」（3 字标签折行跑到框外；根因还有模板自带的 `LayoutElement` 让我们 `AddComponent` 的那个不生效） |
  | 3 | 缩字号到 20 塞进 280 | 「确实**文字太小**了」 |
  | **4（用户定案）** | **模板默认字号 + 按文字自适应宽度（留白 8）**，三按钮排一行，合计 ≈380 | 只是"右边有点挤"，但**不撑宽面板** |
  | 5 | 留白加到 32 + 保底宽 130，合计 ≈428 | 「宽度又不对了」（面板被撑宽，截图 `ui错乱5`） |
  | 6（试过） | 2 列网格（每行 ≤2 格） | —— 用户要求「**改回我让你加大留白之前的那个版本**」→ 回到 4 |

  > **实测边界**：这一排 **≈380 这一档正常、≈428 就会把左侧面板撑宽**（376 那次也被撑宽，所以稳妥区间大致是 ≤380~400）。
  > 「310」**不是游戏本身的限制**，只是参考 mod 那排按钮自己的宽度（190+120），我一度把它当上限，绕了几轮。
  > **当前常量**（`Patches\IdeaSearchPatch.cs`）：`ButtonPadding = 8`、`ButtonMinWidth = 96`、`ButtonHeight = 50`、容器 `spacing = 6`。
  > 想要更多留白时的两条路：① 小幅试探（+12 以内）；② 改成 2 列网格（每行 ≤2 格，天然不会撑宽）——但**用户当前选择是"右边略紧但稳定"这一版**，别再自作主张换方案。

**搜索词用卡牌的"类型名"**（`IdeaSearch.SearchableName`）——`CardData.Name` 是
`nameOverride ?? SokLoc.Translate(NameTerm)`，而装了东西的箱子会把名字改成 `nameOverride`
（「石头箱」/「石头磁力箱」），主意里写的却是通用名。所以搜索词取 `SokLoc.Translate(card.NameTerm)`：
**「石头箱」→ 按「资源箱」搜、「石头磁力箱」→ 按「磁力箱」搜**（用户原话就是这么要求的；
term 认不出来时回落到显示名）。

- `IdeaSearchKnowledgePatch`：Prefix 那个私有方法。模式是 `Vanilla` 时**直接放行** → **没用手势/按钮之前原版搜索手感完全不变**；
  用过一次后模式保留（和参考 mod 的"模式按钮"语义一致，之后手打搜索词也按该模式过滤）。
- `IdeaSearchInputPatch`：Postfix `WorldManager.Update`，按 `Mouse.current.leftButton/rightButton.wasPressedThisFrame` + Alt
  （原始输入读 Alt，不走 `InputController`，免得被别的 mod 的 `GetKey` 补丁影响）。
- `IdeaSearchButtonsPatch`：Postfix `GameScreen.Awake`，`Instantiate` 原版 `IdeasButton` 建那三个按钮
  （整段 try/catch：拼不出来最多是没按钮，绝不拖垮 `Awake`）。
- `IdeaSearchCardClickPatch` / `IdeaSearchBoosterClickPatch`：Alt 手势按下时**挡掉卡片/卡包自己的 `Clicked()`**
  ——不然 Alt+点箱子会顺手取物、点卡包会开包（用户最讨厌的"快捷键冲突"）。
- 另外 `RightDragStackPatch` 在 Alt 手势时**让路**：不然 Alt+右键会既搜索又把牌堆拖起来。
- 设置项：「**Alt+点击搜索主意**」（`Idea search on Alt+click`，默认开）——关掉＝完全回到原版搜索（那三个按钮也不会被建出来）。

> ⚠️ 已知边界：如果同时装着 **Better Mouse Controls**，它会把"右键按下"伪装成 Shift 按住、从而让原版抓起牌堆根卡；
> 那一层发生在原版输入层，本项目不拦（我们的右键拖已经让路了，但它的伪装仍会让原版抓一次）。实测不影响搜索本身。

---

## 0.5.6 迁移：信息栏自动加高（HigherSidebar）+ 详细信息（BetterInfo）+ 自动暂停（Auto Pause）+ 战斗多行（Lopi's Conflict Rows）

用户需求原话（前两个）：「迁移这两个mod的功能，已下载，HigherSidebar，Betterlnfo，迁移完成后帮我禁用这两个mod」；
（后两个）：「把这两个mod的功能也合并进来，lopi's Conflict Rows，Auto Pause」。

四个 mod 都先**逐条反编译核实**、再按行为自行实现进本项目；HigherSidebar 与 BetterInfo 迁移完成即在存档 `save_0.sav`
的 `DisabledMods` 里停用（**停用是按存档生效的**：换新存档要重新去 Mods 菜单关一次；想改回来就在 Mods 里点一下它名字）。
Auto Pause 与 Lopi's Conflict Rows 是用户新订阅下载的，**尚未停用**（要不要停用见第十四节）。

每个功能只留**一个总开关**（"开关要克制"），子功能一律常开；原 mod 的数值旋钮保留成一个数值设置：

| # | 功能 | 补丁的游戏方法 | 类型 | 参考 mod | 游戏内开关（默认开） |
| --- | --- | --- | --- | --- | --- |
| 18 | **左侧信息栏自动加高**：按文字实际高度自适应，不再被原版写死的高度（约 300px）裁掉；加高后信息栏内的图形/文字不吃鼠标射线，免得挡住下面的卡 | `GameScreen.Awake` + `GameScreen.LateUpdate` | Postfix ×2 | HigherSidebar | 「信息栏自动加高」+ 数值「信息栏最小高度」（300）/「信息栏最大高度」（700） |
| 19 | **详细信息**：战斗详解（攻速/伤害/命中/防御 **+ 攻击类型 + 估算每秒伤害**）、掉落**百分比**（地点/采集物/敌人/卡包）、装备攻击类型、传送门"第 N 波"、悬停牌堆显示该卡描述、岛上侧栏显示"贝壳 + 金币" | 12 个补丁点（见 13.2 表） | Prefix ×6 + Postfix ×6 | BetterInfo | 「详细信息（战斗 / 掉落）」 |
| 20 | **自动暂停**：读到存档后暂停、换地点后暂停 | `MainMenu.Awake` + `WorldManager.Play` + `TransitionScreen.StartTransition`（私有重载） | Postfix ×3 | Auto Pause | 「读到存档后自动暂停」+「切换地点后自动暂停」 |
| 21 | **战斗多行排列**：战斗框里每行最多 N 张，超出往后另起一行（框也跟着变大） | `Conflict.GetPositionInConflict` + `GetConflictSize`（**私有**）+ `GetBounds` | Prefix ×3（全部接管） | Lopi's Conflict Rows | 「战斗多行排列」+ 数值「每行战斗单位上限」（默认 7） |

### 13.1 信息栏自动加高 —— HigherSidebar 0.4.1 迁移

原理（参照原 mod）：

1. **`GameScreen.Awake` 后置**：把 `InfoBox` 下所有 `Image` / `TextMeshProUGUI` 的 `raycastTarget` 关掉。
   信息栏变高之后会和卡片区重叠，这些图形如果能吃射线就会导致"点不中卡"。
2. **`GameScreen.LateUpdate` 后置**：每帧算一次目标高度并写进 `InfoBox` 的 `sizeDelta.y`，
   再 `LayoutRebuilder.ForceRebuildLayoutImmediate(SideTransform)` 让侧边栏重排。

高度算法（参照）：

```
Valuetext.preferredHeight + InfoText.preferredHeight + 72
  + Σ（激活中的分隔条 divider 的 VerticalLayoutGroup.preferredHeight + spacing）
再 Clamp 到 [最小高度, 最大高度]
```

两个"手感"细节也参照：变化不足 1px 就不动（避免每帧重排）；**变矮**要等 0.1 秒冷却
（鼠标在两张卡之间快速扫过时内容高度会来回跳，冷却防抖）。

与原 mod 的差异：

- 原 mod 有 4 个数值设置（动态/固定高度、最小/最大、冷却），本项目按"开关要克制"只留
  **1 个开关 + 2 个数值**（固定高度模式与冷却写死）；
- 你原来的配置就是 `Use Dynamic Height = true`，所以只实现动态模式；**开关关掉 = 完全不碰 `sizeDelta`**（回到原版）；
- `GameScreen.dividerList` 在**当前游戏版本里是私有字段**（原 mod 编译时还是公开的），
  这里用反射取一次缓存住（拿不到就只按文本高度算，不影响主体功能）。

### 13.2 详细信息 —— BetterInfo 1.5.5 迁移

12 个补丁点（全部挂在「详细信息」总开关下，关掉即全部早退、完全回到原版）：

| # | 补丁点 | 类型 | 效果 |
| --- | --- | --- | --- |
| 16a | `Combatable.GetCombatableDescriptionAdvanced` | Postfix | 战斗详解重写：攻击类型 + 攻速/命中/伤害/防御 + **估算每秒伤害**（特殊命中按倍率折算，狂热按触发机会加权改攻速）；动物补"每 N 秒产出一张 X"；Mob 补掉落表；敌人补"可能带的装备"（已有的正常显示、没有的加删除线） |
| 16b | `CardData.GetPossibleDrops` | Postfix | 普通 Mob（非 `Enemy`）也有掉落表（原版返回空） |
| 16c | `StablePortal.UpdateCard` | Prefix + Postfix | 传送门描述补"第 N 波"（只在原本描述为空、现在有内容、且不是图鉴演示卡时加） |
| 16d | `BoosterpackData.GetSummary` | Prefix（接管） | 卡包内容**带百分比**、每个 CardBag 一段、未发现的卡合并成一行概率 |
| 16e | `Harvestable.UpdateDescription` | Prefix（接管） | 地点/可采集物掉落表带百分比；墓穴（多一张金币杯）/洞穴（宝图）/遗迹（青春之泉蓝图）/古书（未发现的创意，没有就退化成地图）/钓鱼点（普通袋+渔夫袋）逐个特例处理 |
| 16f | `CombatableHarvestable.UpdateCard` | Prefix | 会掉东西的战斗采集物同上 |
| 16g | `TravellingCart.UpdateCard` | Prefix | 旅行货车同上（不显示数量） |
| 16h | `CardopediaScreen.GetDropSummaryFromCard` | Prefix | 掉落已经写进描述里了 → 图鉴那份摘要不再重复 |
| 16i | `Equipable.GetAdvancedEquipableInfo` | Postfix | 装备攻击类型（近战/远程/魔法…）顶到最前面 |
| 16j | `GameScreen.Update` | Postfix | ①鼠标指着卡包→信息栏显示卡包内容；②悬停牌堆→信息栏显示该卡名字+描述（清掉字号标签与 `\d` 折行标记）；③**在岛上**把侧栏金钱改成"贝壳 + 金币"（贝壳 = 贝壳箱里的 + 地上的） |
| 16k | `Blueprint.GetText` | Postfix | 创意（蓝图）上补产物的特殊效果与装备详解 |
| 16l | `AdvancedSettingsScreen.LoadSettings`（私有） | Postfix | 把原版"详细战斗数值"选项（`AdvancedSettingsScreen.AdvancedCombatStatsEnabled`）打开 —— **它是「战斗详解」的总闸门**：`Combatable/BaseVillager.UpdateCardText()` 都是 `if (这个开关 || 图鉴) 才追加 GetCombatableDescriptionAdvanced()`，它关着连**原版那四项数值**都不显示。它的值从 `PlayerPrefs` 读（本机从没存过这个键 → 默认关），读它的那行代码在一个**玩家点进 Options→Advanced 才懒创建**的界面里，所以：原 mod 只在 `Ready()` 里设（能用，但玩家打开那个设置页会被关回去）；**只挂那个界面的 `Awake` 后置则完全失效**（0.5.6 首次实测就是这么踩的：村民的攻速/伤害整块不显示）。现在＝`ModConfig.Init()`（`Ready()` 阶段）兜底设一次 ＋ 挂它 `LoadSettings()`（真正读 `PlayerPrefs` 的方法）后置再设一次 |

与原 mod 的差异（都是有意为之）：

- 原 mod 没有开关（装上就生效），这里只留一个总开关；
- 原作写死的英文（`Estimated dmg/s`、`Makes one X every Ns.`）改成走本地化 term
  （`stacklandspro_info_estimated_dps` / `stacklandspro_info_makes_one`）→ 中文界面显示中文；
- 原作用 `(int)枚举 == 3` 这类数字比较，这里改成 `CardType.Ideas` / `EquipableType.Head` 这类枚举名（数值逐个核对过）；
- 补了几处**空引用保护**（掉落袋为 null、`GetCardPrefab` 找不到卡、概率总和为 0、蓝图没有 subprint、
  敌人没有可带装备时不再留一行空表头）—— 只防崩，不改变正常情况下的输出；
- 原作用 Harmony 注入的私有字段 `___descriptionOverride`，当前版本 `CardData.descriptionOverride` **本来就是 public**，
  这里直接写它（同样效果，少一层魔法）；原作未使用的 `ToCardIdWithEquipment()` 没搬。

> ⚠️ 迁移时核对了**当前游戏版本**的反编译源码：原版现在已经自带一部分同类信息
> （`GetPossibleDrops` 支持 FishingSpot/Harvestable/Enemy、卡包摘要列名字、地点描述列掉落表、图鉴掉落摘要、
> `GetCombatableDescriptionAdvanced` 的四项数值），所以本文件补的主要是
> **百分比、估算 DPS、攻击类型、波次、牌堆描述、贝壳金币计数**这些原版仍然没有的东西 ——
> 停用 BetterInfo 之后玩家看到的信息量不会变少。

### 13.3 自动暂停 —— Auto Pause 1.1.1 迁移

原 mod 描述原话（工坊页）：「Automatically pauses the game when a save is loaded and also when the player changes locations.」

| 行为 | 原 mod 的做法（反编译核实） | 本项目的做法 |
| --- | --- | --- |
| **读到存档后暂停** | Postfix `MainMenu.Awake` → 给 `ContinueButton.Clicked` 挂回调 → 立刻调 `GameScreen.TimePause()` | **点 Continue 只记一个请求**，真正暂停放在 `WorldManager.Play()` 的后置（见下） |
| **换地点后暂停**（原 mod 唯一的开关） | Postfix `TransitionScreen.StartTransition(Action, TransitionType, float)`（**私有重载**）→ 把 `___onTransition` 包一层，原回调跑完再暂停 | 同左（换地点 `WorldManager.GoToBoard` 正是走这个重载），另加"**场地真的变了**才暂停"的判据 |

**为什么第 1 条不能对齐**（本项目读通的时序）：`GameScreen` 是 `GameCanvas` 的子对象、屏幕**按需激活**
（未激活的屏幕不会跑 `Awake`，静态 `GameScreen.instance` 也就没被赋值），所以"点 Continue 那一帧"
`GameScreen.instance` 很可能是 null，`TimePause()` 无从调用 —— 表现为"有时暂停、有时不暂停"。
挂在 `WorldManager.Play()` 后置就没有这个问题：`Play()` 第一行就是 `GameCanvas.instance.SetScreen<GameScreen>()`，
那时 `instance` 一定有效。顺带好处：**新开一局不会误暂停**（新局不走 Continue），与原 mod 语义一致。

⚠️ `GameScreen.TimePause()` 是**开关式**的（`SpeedUp >= 1` 就暂停、否则恢复），所以两条路径都可能触发时，
调用前必须先判断 `WorldManager.instance.SpeedUp != 0f`（原 mod 的 `PauseGame()` 就是这么写的），
否则同一次读档里被调用两次会"暂停又恢复"。另外本项目的"换地点暂停"只在 `CurrentBoard` 引用真的变了时才触发，
跳过 `GameState.InMenu` —— 不然每段剧情结束都会暂停一次。

### 13.4 战斗多行排列 —— Lopi's Conflict Rows 1.0.0 迁移

原 mod 描述原话（工坊页）：「Allows multiple rows in combat boxes」。原版一场冲突里同一队的卡只排**一行**
（人越多横向拉得越长），这个 mod 改成**每行最多 N 张（默认 7，可在设置里改）**，超出就**往后方另起一行**，
并把冲突框（bounds）与尺寸算大以包住多行。三个补丁点全部是 Prefix **接管原实现**：

| 补丁点 | 作用 |
| --- | --- |
| `Conflict.GetPositionInConflict(Combatable)` | 每张战斗卡的位置：行内横向居中（最后一行不满就按实际张数居中），行号沿 Z 轴偏移；敌方与我方分别朝相反方向排 |
| `Conflict.GetConflictSize()`（**私有**） | 冲突框尺寸：宽度＝两队中"较宽那一行"，高度＝卡高 + 每多一行加一个 `HorizonalCombatOffset` |
| `Conflict.GetBounds()` | 冲突框本体：起始位置按两队"行数差"横向补偿，尺寸用上一条的结果 |

与原 mod 的差异：
- 我们补了一个**总开关**「战斗多行排列」（原 mod 没有开关，装上就生效）；
- 原 mod 的 dll 带 `[assembly: IgnoresAccessChecksTo("GameScripts")]`（它绕过了可见性检查）才能直接调用
  `ClampStartPosition` / `GetConflictSize` 两个私有方法；**本项目不能绕检查**，改用
  `AccessTools.Method` + `MethodDelegate` 反射委托调用 —— 反射建不出来（游戏改名/改私有）就**整体放行回原版单行排列**
  并在日志里说明一次，不会把战斗搞坏；
- 原 mod 唯一的数值设置（每行上限，默认 7）保留为数值项「每行战斗单位上限」。

> ⚠️ 这两个功能都**只在战斗/读档时生效**，上手验证要看实测（见第十六节第 34、35 条）。

---


## 0.5.7 新功能（参考 Stacks n Capacity 的实现）

### 17.1 高级仓库可堆叠（`SuperWarehousePatch.cs` 里的 `WarehouseStackingPatch`）

原版 `CardData.CanHaveCardOnTop` 是所有落卡/堆叠校验的最终入口。0.5.6 为了"10 张仓库叠一叠"的配方，
已经在那里放行了"仓库 + 仓库"；0.5.7 把判断扩成集合：**{仓库, 高级仓库} 里任意两张都放行**（双向）。
配方匹配按卡 id 数数，混着叠凑不齐 10 张"仓库"，不会误触发合成；
`SuperWarehouseCapacityPatch` 按场上实际卡数统计 +140，叠在一起照常每个生效（不依赖堆叠方式）。

### 17.2 火锅（炖锅）容量（`Patches\HotpotCapacityPatch.cs`；0.5.18 起默认 10000、可在设置里改）

原版 `Hotpot` 里是 `private int MaxFoodValue = 50;`——满了 `CookFood` 就不再吞食物
（判断式 `FoodValue + childCard.SpecialValue <= MaxFoodValue` 不成立）。
没有公开入口，所以挂在 `Hotpot.UpdateCard` 的 **Postfix** 上用反射写字段（`FieldInfo` 静态缓存），
每帧把 `MaxFoodValue` 顶成设置里的「火锅容量」输入框值（默认 10000，0.5.8 定值；0.5.18 起可自由填写，
填 50 ＝原版行为）。0.5.9 之前这里曾是"无限"（int.MaxValue），0.5.8 改成定值、0.5.18 改输入框。

与 Stacks n Capacity 的关系：它走"遍历卡牌 prefab 改 `MaxFoodValue` + 一个 Postfix 兜底"的路线；
我们直接改实例字段、且在 `UpdateCard` 后置（每帧）执行，两个都装时以我们的设置值为准。

> ⚠️ 0.5.9 补充：容量 10000 暴露了一个连带问题——原版月末吃饭的排序键 `-100 + FoodValue`
> 是按"火锅 ≤ 50"写的，FoodValue 上千后炖锅反而最后才被吃。已在 `FeedingPriorityPatch` 里修掉，
> 见第十八节。

### 17.3 动物留在饲养栏（`Patches\BreedingPenPatch.cs`）

原版 `BreedingPen.BreedAnimals`（反编译核对）的逻辑是：生成幼崽 → `StackSendCheckTarget` 送往输出方向 →
**把两只亲代依次 `RemoveFromStack` + `StackSend(OutputDir)` 弹出去**——栏被清空，得重新抓一对放回去。
本项目 Prefix 接管：生成幼崽（`CreateCard(..., checkAddToStack: false)`，不让新生卡在原地尝试自动入堆）
+ 送往输出方向 + 触发 `breed_xxx` 任务进度，然后 `return false`，**原版弹亲代的两段不再执行**。

亲代留在栏上（`ChildCount == 2`）→ 原版 `UpdateCard` 下一帧自动重启 120 秒繁殖计时，
效果＝一对动物放进栏里就持续繁殖（每 2 分钟自动产一只，幼崽照常飞出）。
实现参照 Stacks n Capacity 的 `Patch_BreedingPen`（它在游戏里验证过这套改法）；两个 mod 同时装时
都是 Prefix 接管同一个方法，行为一致，谁先执行都一样。

### 17.4 资源箱内的资源不按数量计入卡牌上限（`Patches\ResourceChestCountPatch.cs`）

原版场地卡牌上限只在月底"卖卡腾地方"结算里强制执行，走的是**无参** `WorldManager.GetCardCount()`
（反编译核对），它给资源箱留了个特判：

它遇到 `ResourceChest` 时特判：Cities 场地的箱子按 **1** 张算，其它场地按箱内数量（`ResourceCount`）算；其余每张卡各加 1。

即主大陆/岛上每个资源箱按「1 + 箱内数量」计数（囤一箱 50 木头＝占 51 张上限），
而 **Cities 场地本来就固定按 2 张算**（箱内数量不计）。

实现（**0.5.8 重写为 Postfix**）：`GetCardCount()` 的后置补丁把每个被计入的资源箱**减回到 1 张**——
非 Cities 减 `ResourceCount`、Cities 减 1——即**只算箱子本身，和磁力箱口径完全一致**
（第一版按 Stacks n Capacity 的 transpiler 思路做的是"箱子算 2 张"，且实测没生效，见下）。
减掉前复刻了原版的计入条件（同场地、未装备、不在正在启航的船上），确保不会把总数减虚。
钩子运行时读开关（关掉立即恢复原版计数，不用重启）。踩坑记录：

- ⚠️ **重载歧义是根因**：`GetCardCount` 有**两个无参重载**（普通 + 泛型 `GetCardCount<T>()`，
  Cecil 核对过），无论 attribute 传 `new Type[0]` 还是 `GetMethod(..., Type.EmptyTypes, null)`，
  binder 都会在两者间抛 AmbiguousMatchException → PatchAll 失败被 ApplyPatches 的 try/catch 吞掉
  → 功能静默失效（前两版都是这么挂的）。`TargetMethod()` 必须**手动遍历**选
  「名为 GetCardCount、0 参数、`!ContainsGenericParameters`」的方法，唯一确定，并在绑定成功时打一条日志；
- 补丁首次生效时还会打一条「资源箱计数修正已生效：场上 N 个资源箱…」日志——
  F3 / Player.log 里搜「资源箱计数修正」即可验证补丁真的在跑；
- 与本 mod 自己的 `SuperWarehouseCapacityPatch`（+140 上限）互不影响——一个改的是上限、一个改的是当前计数。

## 月末吃饭优先级（0.5.9，`Patches\FeedingPriorityPatch.cs`）

需求原话：**「吃饭的时候不应该吃高级温室上的物品，应该先吃炖锅的」**。

### 18.1 原版怎么选"吃哪份"

村民吃饭只发生在**月末动画**里（`FeedVillagers()` 给每个村民逐份扣食物），取哪份由私有静态方法
`GetFoodToUseUp()` 决定：把场上所有 `Food` 卡按一个**排序键**升序排（越小越先吃）——

| 排序键 | 条件 |
| --- | --- |
| `-1000 + 堆内下标` | 堆里有 MessHall（食堂，Cities）→ 最先 |
| `-100 + FoodValue` | 是 Hotpot（炖锅/火锅）→ 次之 |
| `-5` | 在变质且堆里没卡在计时 |
| `5` | 当前 Demand（Cities 需求）正要这张卡 |
| `4` | 堆里有卡在计时（生产中，`GetCardWithStatusInStack()` 认 `TimerRunning`） |
| `3` | 全场仅剩 1 张且是生食 |
| `2` / `0` | 生食 / 熟食 |

排完再按份量（`FoodValue` 小的先）、价值细分，取第一张 `FoodValue > 0` 的。

### 18.2 两个问题都是本 mod 自己引入的

① **炖锅反而最后吃**：键值 `-100 + FoodValue` 是在"火锅最多囤 50"的前提下写的
（50 − 100 = −50，稳居熟食 0 / 生食 2 之前）。0.5.8 的「火锅容量 10000」把 `FoodValue` 顶到几千
→ 键值变成几千的正数，炖锅从"仅次于食堂"跌到"几乎最后吃"。
**修法**：`-100 + Math.Min(FoodValue, 50)`，把 FoodValue 夹回原版假设的范围（多个炖锅按份量细分）。

② **高级温室的"种子"被吃掉**：高级温室自动循环靠"种子一直叠在温室上"（见 22 号功能；0.5.10 起是 **1 个**）。
生长中的温室**有计时器** → 键值 4——别处没熟食/生食时，月末一顿饭就把温室上的种子吃掉，生产线就断了。
**修法**：堆里含高级温室（`HasCardInStack` 判 `AdvancedGreenhouse.CardId`）的食物**整体从候选里排除**
——用户口径是"不应该吃"，别的食物吃光时宁可让村民饿着这一个月。

### 18.3 实现要点与坑

- **Prefix 接替整个方法**：排序键写在 `OrderBy` 的**闭包 lambda**里（编译器生成的嵌套方法），
  对外层方法做 transpiler 够不着——和 1b 号功能接替 `TrySendToMagnet` 同一个原因。
  Prefix 里逐行对齐原版逻辑、只动上面两处；异常时放行交回原版（try/catch 兜底）。
- ⚠️ **这个方法随游戏版本挪过窝**：本地反编译产物（旧版）里在 `Cutscenes`，实测当前游戏 DLL
  （ilspycmd 核实）已挪到 **`EndOfMonthCutscenes`**，方法名和逻辑都没变。所以不用 `typeof(...)` 写死，
  用 `TargetMethod()` 动态绑定：优先 `EndOfMonthCutscenes`，找不到再回退 `Cutscenes`。
  **教训：本地 `stacklands-mod-common\decompiled\` 的类布局可能滞后于实际游戏，写补丁前先对实际 DLL 核一遍**
  （工程引用的就是游戏目录里的 `GameScripts.dll`，编译能过不代表方法还在那个类里——attribute 里的
  方法名是运行时才解析的，绑不上会在 `PatchAll` 时抛异常、被 `Mod.cs` 的 try/catch 吞掉）。
- ⚠️ **范围**：只动月末吃饭。Cities 的需求/住房消耗走另一条 `CitiesManager.GetFoodToUse()`
  （按份量从大到小、计时中的排最后），不是"村民吃饭"，不碰。
- 无新开关（常开）。进游戏验证：攒一锅炖菜 + 温室上叠 6 个同种生食，月末吃饭应该**只扣炖锅**，
  温室上的种子原样留着、下一轮照常产出。

## K+O+F1 调试面板汉化（0.5.9，`Patches\DebugScreenLocalizationPatch.cs`）

需求原话：**「对游戏内K+O+F1的面板做一下汉化，这个能通过mod做吗」**——能，而且两半都能。

### 19.1 面板是什么、文字从哪来

按住 K+O+F1（非编辑器）打开的是游戏的 **`DebugScreen`**（`WorldManager.DebugUpdate()` 里判断组合键）。
面板文字两种来源（对当前游戏 DLL 逐一核实）：

1. **走本地化系统**：按钮挂着 SokLoc 的 `LocSetter` 组件（同物体、`Term` 字段存词条 id，
   `Awake`/语言切换时 `SokLoc.Translate(Term)` 填 TMP 文本）。面板上的页签（General/Cards/Effects/Shortcuts）、
   结束月份、各场景按钮、快捷键提示（"Hover + C: Copy card" 等）都是这类，
   词条 id 是 `label_debug_*`（从 sharedassets1.assets 里枚举出全集 22 个），
   另有 `DebugScreen.Update()` 每帧刷新的 5 个开关词条（无尽之月/和平模式/无需食物/需要村民/无需能量，
   占位符 `[on_off]`）和 `label_on`/`label_off`。
   **官方中文列没翻这些词条**——词条存在但 zh 列为空 → `SokTerm.GetText()` 返回空串 →
   `SokLoc.Translate` 判空白 → 回退英文兜底表，这就是面板显示英文的原因。
2. **硬编码英文**：没有 LocSetter、TMP 文本写死——页签 Saves/Presets/Cutscenes（这三个没有对应词条）、
   存档区（Save Game / Open Saves Directory / Clear Save / Make Debug Save / Select Save）、
   预设区（Open Preset Directory / Make Board Preset / Right click to load preset / Waiting for input..）、
   搜索框占位符 Search、Actions。

### 19.2 两半各怎么修

- **词条①（纯数据能修，但不用 tsv）**：`ModManager.ReadyUpMods` 会把 mod 的 localization.tsv
  合并进当前语言词表、重复词条"以后写的为准"——所以 tsv 加 `label_debug_*` 行**也能**修。
  但 ReadyUpMods 那次调用没关 `disableWarning`，每覆盖一个原版词条就打一条
  `LogError("Term ... found more than once")`，27 个词条 = 27 条报错刷日志。
  所以改成**代码注入**：`SokLoc.get_CurrentLocSet` getter 的 Postfix，当前语言是简体中文且词表里
  没有注入过的词表里，把 27 个词条直接塞进 `TermLookup`（**覆盖**原版英文条目、静默）。
  ⚠️ 哨兵**不能用`词表里有没有某词条`判断**——这些词条在原版词表里本来就存在（英文、zh 为空），
  首版就因此漏注入（表现为`过场动画/存档`这类硬编码的翻译了，而走词条的 General/Cards/Effects/Shortcuts 没翻）；
  改成按词表**实例**记 HashSet，每张词表只注入一次，语言切换重建词表后自然重新注入。
  挂 **getter** 而不是挂一次事件，是因为语言切换时词表整个重建（getter 里 `currentLocSet = new ...`），
  挂 getter 保证"谁取词表谁就能拿到注入的词条"，没有时序问题。
- **硬编码②**：`DebugScreen.Start` 的 Postfix 遍历面板下所有 `TMP_Text`，
  **同一物体上没有 LocSetter** 的按英文原文查表替换（有 LocSetter 的绝不碰，避免和词条①打架）。

### 19.3 注意点

- **只对「Chinese (Simplified)」生效**：英文玩家完全无感；繁体不注入（只有简体文案，繁体字体表可能缺字形）。
- **语言切换**：存了每个被替换文本的原文，切回非中文逐个还原、切回中文重新替换（挂 `SokLoc.LanguageChanged`）。
- Cards 页签的卡牌按钮用卡名（官方已翻）、Effects 页签用效果名（同）、Cutscenes 页签列过场动画 id——都不管。
- 无开关（常开）。验证：进游戏按 K+O+F1（要按住 K 和 O 再按 F1），面板应是中文。

## 时间倍数沿用：月结 / 换地图（0.6.7，`Patches\GameSpeedKeepPatch.cs`）

**需求原话**：「在城市地图中，如果在一个回合（月亮年）中将速度调快了，下一个回合中速度会自动变成 1 倍。这个需要改成和上一回合速度一样」→「下一个-月-年沿用上一年的时间倍数，好像还有个自动暂停，这两个功能要在城市、海岛等多个地图全部生效」。

**原版只有两处会把速度打回 1 倍**（都在 `WorldManager` 上、与地图无关，所以补丁一挂全地图生效）：
① `EndOfMonth` 启动结算协程后立刻 `SpeedUp = 1f`；② `GoToBoard` 的地图过渡回调里同样写死 `SpeedUp = 1f`。
结算 / 过渡结束时都只清 `currentAnimationRoutine`（或结束过渡），**不会**把速度还回来。

**做法**：三个补丁类共用 `GameSpeedKeep` 一份状态——
`GameSpeedRememberOnMonthEndPatch` / `GameSpeedRememberOnBoardChangePatch`（两处前缀，`Remember()` 记下 >1 的速度）
＋ `GameSpeedRestorePatch`（`WorldManager.Update` 后置 `Tick()`）。

`Tick()` 只在"玩家又能操作"时还原，四个闸门缺一不可：`IsPlaying`、`currentAnimationRoutine == null && currentAnimation == null`、
`TransitionScreen.InTransition == false`。**不能在这两个方法的后置里直接改回来**——那样月结弹窗 / 换图过渡期间就是 5 倍，
背景音乐音高（`AudioManager` 按 `SpeedUp` 变速）等会被带着跑，观感与原版不一致。

**和「切换地点后自动暂停」的配合**：Auto Pause 在过渡结束调 `GameScreen.TimePause()`，把当时的速度存进 `prePauseSpeed` 再设 `SpeedUp = 0`，
而那一刻正是原版刚设的 1 倍。所以 `Tick()` 发现自己在**暂停中**时不去动 `SpeedUp`（那等于替玩家解除暂停），
改为把 `GameScreen.prePauseSpeed` 覆盖成记住的倍数——玩家照常按暂停键继续，恢复出来就是上一回合的倍数。
（`TimePause()` 解除暂停读的正是这个字段。）

**边界**：只记 **>1** 的速度（1 倍是原版行为；也避免"月结流程里又发生换图"把刚记住的倍数冲成 1）；`IsPlaying == false`（回主菜单 / 读档）时丢弃记录，不带进下一局。
**不做的**：`RestartGame` / `FinishDemand`（Greed 许愿）那两处的重置不管——不是"换回合 / 换地图"，且需求没提。

## 已知限制与不变量

**已知限制**

1. 在 **Cities 场地**上不会触发 mod 配方（原版 `FindMatchingPrint` 跳过 `CardUpdateType != Cities` 的蓝图，mod 蓝图被固定为 `Mod`）。
   主大陆/岛上正常。**例外是三张城市专属卡**：磁力容器（0.6.0）、高级回收中心（0.6.2）、磁力信用卡（0.6.2 起改为城市专属）——
   它们的卡与创意都被 `CitiesCardSupport` 标成 `CardUpdateType.Cities`，所以城市里能做、城市的创意列表也看得到；
   其余 6 张卡照旧（城市里不做）。
2. 磁力箱 / 硬币磁力箱配方**只能**"磁铁当根"（先放磁铁再叠箱子）；反方向会让 `ResourceChest`/`Chest` 把磁铁当材料销毁，故未放行。
3. "取出免疫"是**运行时状态、不存档**，读档后免疫名单清空。
4. 磁力箱的"输入免疫"只覆盖自己产出的卡；**从原版储物箱取出的材料也会被截胡**（原版磁铁就是这行为）。
   要改可给 `ResourceChest.RemoveResources` 加一条免疫登记。
5. 关掉的**功能**开关见第四节（0.5.6 起共 14 个）：改完不会回滚已经发生的存档状态，
   例如关掉"双击归堆"不会把已经叠好的堆拆开（但原版会自行校验并拆掉不合法的堆叠，符合预期）。
6. **卡总开关的边界**（与用户约定）：关掉一张卡只影响"新局还能不能得到它"——创意从抽卡池摘掉、图鉴隐藏、配方不成立；
   **旧存档里已经存在的这张卡照旧能用**。想让旧卡也失效，得在对应补丁里再加一道 `ModConfig.IsCardDisabled(...)` 早退。
7. 硬币磁力箱**不能自己叠自己**（原版硬币箱可以）：叠起来下面那只会停止吸附。所以它靠 `int.MaxValue` 的单箱容量扩容，
   而不是靠"叠一叠箱子"。
8. **有计时器/状态条的卡必须重写 `CanHaveCardsWhileHasStatus()` 返回 true**，否则 `GameCard.Bounce()`（落地吸附）
   和拖拽落点判定会因为基类默认的 false 而拒绝所有落卡——症状是"材料吸过来在卡顶上弹跳、进不去"。
   原版 `ResourceChest` / `FoodWarehouse` 就是因为自带计时器才重写它；本项目给磁力箱加运输输出时踩了这个坑，已修。
9. 硬币磁力箱的**卡面显示完全照原版硬币箱**（一个"金币图标+数量"，即 `GameCard.CoinText`/`CoinIcon`，由 `Chest.UpdateCard` 的
   `Value = CoinCount` 驱动）；**不要**再给它设 `GameCard.SpecialValue`/`SpecialIcon`（那是另一套数字），
   否则两个数字会叠在一起（0.5.0 修过一次，见本文「磁力系列代码位置」）。
10. **搬动类功能的筛选有两个反向陷阱**（框选功能各踩过一次，修法见 本文「Ctrl+拖空白框选多张卡」）：
    ① **不能只信 `CanBeDragged()`**：装备件（`IsEquipped`，不在堆叠链里、`Parent` 为 null）和在建筑里干活的工人
    （`IsWorking`）都"看起来像散卡"，但位置由原版 `PositionType` 接管（装备是
    `TargetPosition = EquipmentHolder.transform.position`），谁动它们的 `TargetPosition` 谁就把挂载关系弄坏；
    ② **也不能过度排除**：把"带装备的卡"整个排掉会让村民无法框选，而装备其实会自己跟着走；
    ③ **动态条件别混进每帧清理**：`CanBeDragged()` 在 `BeingDragged` 时返回 false，
    若让每帧的选择清理也过它，正在拖的那张会被自己删掉（症状：拖一张、其他不跟，松开后高亮消失）。
11. `Stack search distance` 是**运行时可调**的（transpiler 调 `ModConfig.GetStackSearchDistance()`），
   但参考 mod Stack Further 那个设置项标注了 `RestartAfterChange = true`；本项目有意做成"改完立即生效"，
   代价是每次归堆判定都多一次方法调用（可忽略）。

12. **半径 999 时远处材料会"飞一半就落地"再被吸附**：原版弧线落地时间固定约 0.47 秒、与距离无关，超远距离必然"落短"。这是原版机制的正常表现，不是 bug；嫌观感跳就把半径调到 20~30。
13. **材料飞过来停在箱顶却不被吞**：说明落地判定被拒（例如那一刻箱子正在处理上一张），表现是箱顶堆着材料；把 `_PullInterval` 调大一点（如 0.6）观察。
14. **磁力箱配方依赖 Cities DLC 的两张卡**：没有该 DLC 时只能靠调试菜单生成，或自己改配方。

## 参考 mod（十五个）与其它 Mod 的重叠关系

### 9.1 本项目的参考 mod（十五个）

这些都是你订阅过、并在本机反编译读过补丁点的 mod。我从它们**反编译出的补丁点**（关于游戏本身的事实）里学到了做法，
代码**自行实现**（不复制源码/DLL，对方授权不明）；反编译产物放在仓库外的 `..\..\stacklands-mod-common\decompiled\refmods\`，不公开分享。

| 名称 | 创意工坊 id | 它怎么做的（补丁点） | 本项目对应实现 | 备注 |
| --- | --- | --- | --- | --- |
| **HigherStacks** | `3018266162`（id `HigherStacks` v1.0.0） | ① Transpiler 把 `CardData.CanHaveCardOnTop` 里的 `Ldc_I4_S 30` 换成 `int.MaxValue` ② Prefix 重写 `WorldManager.TrySendToMagnet`，去掉 `< 30` | `StackLimitPatch`（换成调用 `ModConfig.GetMaxStackSize()`，上限 999 可开关）+ `MagnetTargetPatch` | 关键启示：`TrySendToMagnet` 里的 30 在**闭包方法**里，对它做 transpiler 无效，必须用 Prefix |
| **PACKSPLOSION!!!** | `2964515075`（id `packsplosion`） | Prefix `Boosterpack.Clicked` 返回 `TimesOpened != TotalCardsInPack`；Postfix 启动协程，等 0.05s 再点一次 | `OpenWholePackPatch` | 我把"0.05s 链式点击"参照，另加了"同一卡包只允许一条连锁"去重（避免玩家连点时速度翻倍） |
| **NoWitchNoPortal** | `3100307021`（id `NoWitchNoPortal` v1.0.0） | Prefix `EndOfMonthCutscenes.SpecialEvents`：若 `CurrentSave.LastPlayedRound.RunVariables.FinishedWickedWitch` 则把 `CurrentBoard.BoardOptions.CanSpawnPortals = false` | `NoPortalAfterWitchPatch` | 参照"用官方开关"的思路，并**同时**读当前局的 `CurrentRunVariables`（对没存档的当前局也生效） |
| **Show Idle Villager** | `2995639602`（id `IdleVillager` v2.2.1，源码 github.com/NBKRedSpy/StacklandsIdleVillager） | Postfix `GameCard.Update`：村民若"无冲突、无父卡、无子卡"就把 `HighlightRectangle` 置为显示并染红 | `IdleVillagerBorderPatch` | **未合并**它额外的"闲置生产设施高亮"（农场/菜园、钓鱼点、捕鱼陷阱、温室、便便）与它的设置界面；本项目有意多查了 `!IsWorking`，所以**在建筑里干活的村民不会被标红** |
| **Better Mouse Controls** | `3012078700`（id `BetterMouseControls` v1.2.5，作者 benediktwerner） | ① Postfix `WorldManager.Update` 做**双击归堆**（含 4 个设置项：开关 / 双击间隔 0.5s / 鼠标位移 5px / 归堆半径 4）② 「右键拖整叠」用它自己的做法：Postfix `InputController.GetKey` 让"右键按下时 LeftShift 视为按下"，从而复用原版 `LeftShift 拖 GetRootCard()` | `DoubleClickRestackPatch`（功能取代：**归堆上限跟随本项目的堆叠上限**，不再是写死 30）+ 本项目原有的 `RightDragStackPatch`/`RightDragInputPatch`（右键拖的**自研**实现，见第五节） | **只合并了双击归堆**（用户指定）：右键拖本项目已有更完整的实现（对单张牌也生效，且不会在"Click to Drag"开着时被原版抢抓）；**未合并**它那 4 个设置项，参数固定在本项目的 `ModSettings` 里 |
| **Multi Select & Drag** | `3780343990`（id `multidrag` v1.0.0，"姓名"字段是 `Id`/`Name`/`Version`/`Assembly`，和别的 mod 写法不同） | ① `WorldManager.Update` Postfix 跑框选主循环；`GameCamera.StartDragging` Prefix 挡掉 Ctrl+拖的镜头平移；`GameCard.Clicked` / `Boosterpack.Clicked` Prefix 挡掉 Ctrl+点击的自身行为 ② 框选时接管 `WorldManager.DraggingDraggable`，给每张同组卡设 `TargetPosition = 主卡 + 偏移`，收尾自己清 `BeingDragged`/`ClickedObject`/`DragTag` ③ 覆盖层走 `OnGUI` + `GUI.DrawTexture`（工程需引用 `UnityEngine.IMGUIModule`） | `BoxSelectPatch.cs`（5 个补丁类，见本文「Ctrl+拖空白框选多张卡」） | 本项目做得更收敛：有总开关、**松手收尾与它一致（只清 `BeingDragged`/`ClickedObject`/`DragTag`，不调 `DropCard()`）**、加了"最多搬 200 张"上限、出错自动收摊重置，并**砍掉了** Ctrl+单击增减选择与 Shift 框选追加（只留 Ctrl 框选 + 点空白取消）。**未合并**它的中键/其它键位（它只用 Ctrl / Ctrl+Shift） |
| **Hit Text To Center** | `3016077439`（id `HitTextToCenter` v1.1.0） | Postfix `HitText.SetPosition`：把这个数字的位置从原版的 `myGameCard.HitTextPosition`（带偏移的锚点，压在卡片下沿）改成 `myGameCard.transform.position`（卡片正中），并把"非常有效"角标抬到卡片上方 0.11；`IsMiss` 时不动 | `HitTextSetPositionPatch`（+ `HitTextTagPatch`） | 本项目**补上了它只改伤害、没改治疗**的缺口：治疗文字在 `HitText` 实例上没有任何类型标记，所以我们在 `Combatable.CreateHitText(txt, prefab)` 的后置里按 `prefab == PrefabManager.instance.HealHitText` 打标记（`ConditionalWeakTable`），`SetPosition` 里治疗无条件居中。**未合并**：它本来也没有其它功能 |
| **Stack Further** | `2995935585`（id `StackFurther` v2.0.3，作者 NBKRedSpy） | 两个 Transpiler：`WorldManager.StackSend` 里匹配 `Ldloca_S V6 / Call magnitude / Ldc_R4 2 / Bgt_Un` 把那个 `2f` 换成设置值；`WorldManager.TrySendToChest` 里匹配 `Call magnitude / Ldc_R4 2` 换成同一个设置值 | `StackSendDistancePatch` + `TrySendToChestDistancePatch` | 本项目做得更"运行时"：把常量换成调用 `ModConfig.GetStackSearchDistance()`，所以**改数值/开关立刻生效**（参考 mod 要重启），并且只替换**唯一的那个 `2f`**（已用 Mono.Cecil 核对过：这两个方法里 `ldc.r4 2` 各只有一处） |
| **Stacks n Capacity** | `3378143608`（id `AutoStack` v1.0.4） | ①「从箱子里拖出材料」：Prefix `GameCard.StartDragging` —— 被拖的是 `ResourceChest` 且 `ResourceCount > 0` 时，按 `GetKey((Key)55)`（LeftCtrl）取 1 张、`GetKey((Key)51)`（LeftShift）取 `DragCount = 10` 张，`ResourceChest.RemoveResources(n)` 拿到牌堆后设 `DraggingDraggable` + `StartDragging()` 并 `return false` ②「按 X 把目标物品送往宝箱/堆叠」：`WorldManager.Update` 的后置里 `GetKeyDown((Key)38)` → `HoveredCard.GetRootCard()` → `StackSend(root, Vector3.right, null, true)` | `Patches\ChestDragOutPatch.cs` + `Patches\SendToChestHotkeyPatch.cs`（见 本文 第十一、十二节） | 按用户要求把这两套机制**都搬到本项目**：① 拖出机制迁到本项目的磁力箱（**原版资源箱的取牌仍归它**；0.5.5 踩过坑：我们一开始也实现了一份 → 两边都取 → **双份牌**，根因是 HarmonyX 里**所有前缀都会执行**、`return false` 拦不住别人，详见本文 11.4）② 快捷键功能**改成默认 R、且按键可在设置界面配置**（它写死 X），并且不引入它那个 `SellCard` 附近的 transpiler。**未合并**它的堆叠上限/自动堆叠/火锅/收割等其余功能 |
| **Extra Search Mod** | `3013114897`（id `extra_search_mod`） | ① Prefix **私有**方法 `GameScreen.KnowledgeMatchesSearch(IKnowledge, string)`：按一个全局"模式"变量决定比 `KnowledgeName` 还是 `KnowledgeText`（都做 `ToLower().Replace(" ","")`）② Postfix `WorldManager.Update`：`HoveredCard != null && (StartedGrabbing() \|\| 右键按住)` 就把 `GameScreen.instance.IdeaSearchField.text` 填成卡名并 `UpdateIdeasLog()` ③ Postfix `GameScreen.Awake`：**用代码新建** Title/Description 切换按钮 + Clear 按钮接到界面上（按钮文字写死英文、带 `TooltipText`） | `Patches\IdeaSearchPatch.cs`（5 个补丁类，见 本文「0.5.5 另外两个新交互」 12.3） | 用户要求「迁移并**重新设计**」：① 两个手势——**Alt+左键单击＝标题搜索、Alt+右键单击＝描述搜索**（Alt+点空白＝清空）；② 侧边栏按钮改成**三个固定标签**「搜标题 / 搜描述 / 清除」并**去掉 `TooltipText`**（原 mod 是一个按钮在 Title/Description 间来回显示 + 悬停提示）；③ 搜索词改用卡牌的**类型名**（`NameTerm`）——「石头箱」按「资源箱」搜、「石头磁力箱」按「磁力箱」搜；④ Alt 手势按下时挡掉卡片/卡包自己的 `Clicked()` 并让路给搜索（不触发右键拖） |

| **Stacklands Plus** | `3355058679`（id `stacklands_plus` v0.8.2，程序集 `StickWorkshopMod.dll`，作者 Zjave） | ① **`WarehouseWorkshop`（"仓库工坊"）**：`: ZjaveWorkshop`，`base("warehouse", "warehouse", 10f, { iron_bar: 1, stone: 1 })`＝**用仓库 + 1 铁条 + 1 石头升级** ② **`SuperGreenhouse`（超级温室）**：`: ZjaveGreenhouse`，**另建一张自己的生长蓝图卡**（`zjave_blueprint_super_growth_workshop`）再塞进卡包，配方吃 6 个同种种子、产物翻倍；卡上还加 `_EnergyConnectors`（Transport ×3 进/出） ③ 另有 `SuperFarm` / `SuperGarden` / 一堆"XX 工坊"（奶昔工坊等）/ `TechnicalResearchCenter`… | 本项目的**「高级仓库」**（配方一 10 张仓库 20 秒；配方二 10 石头 + 10 铁条 + 一名村民 120 秒；每个 +140 场地卡牌上限）与**「高级温室」**（6 张温室 40 秒；0.5.10 起放 1 个种子 → 每 15 秒 1 份产物、**种子不消耗**、自动循环；带输出方向箭头 + 产物送进旁边容器） | 用户原话：「**高级温室的参考自mod：Stacklands Plus，这个mod的仓库工坊优化为高级仓库，其他东西没什么必要就不迁移了**」——**只借了这两张卡的思路**，其余一概不迁移；差异：它做超级温室靠"自己的生长蓝图卡"，本项目**直接改原版 `blueprint_growth`**（玩家能种普通温室就能用高级温室，少一张卡、少一处联动），40 秒也是照它的超级温室取的 |
| **HigherSidebar** | `2995428012`（id `com.maxsch.stacklands.highersidebar` v0.4.1，作者 maxsch） | ① Postfix `GameScreen.LateUpdate`：每帧按 `Valuetext.preferredHeight + InfoText.preferredHeight + 72 + 激活分隔条的高度` 算目标高度，写进 `InfoBox.sizeDelta.y` 再 `ForceRebuildLayoutImmediate(SideTransform)`；变化 <1px 不动、变矮有 0.1s 冷却 ② Postfix `GameScreen.Awake`：把 `InfoBox` 下所有 `Image`/`TextMeshProUGUI` 的 `raycastTarget` 关掉 | `Patches\SidebarHeightPatch.cs`（2 个补丁类，见 本文 13.1） | **0.5.6 完整迁移**（用户要求），迁移后已在本存档停用。差异：原 mod 4 个数值设置收敛成 1 开关 + 最小/最大高度；`GameScreen.dividerList` 在当前版本是私有字段，本项目用反射取 |
| **BetterInfo** | `2997317807`（id `BetterInfo` v1.5.5） | 12 个补丁点：`Combatable.GetCombatableDescriptionAdvanced`（战斗详解 + 估算 DPS + 攻击类型 + 动物产出 + Mob 掉落 + 敌人可带装备）、`CardData.GetPossibleDrops`（普通 Mob）、`StablePortal.UpdateCard`（第 N 波）、`BoosterpackData.GetSummary`（带百分比的卡包内容）、`Harvestable.UpdateDescription`（带百分比的掉落表 + 墓穴/洞穴/遗迹/古书/钓鱼点特例）、`CombatableHarvestable.UpdateCard`、`TravellingCart.UpdateCard`、`CardopediaScreen.GetDropSummaryFromCard`（去重）、`Equipable.GetAdvancedEquipableInfo`（攻击类型）、`GameScreen.Update`（卡包内容进信息栏 / 牌堆描述 / 岛上贝壳+金币）、`Blueprint.GetText`（产物装备详解）、`AdvancedSettingsScreen` 强制打开"详细战斗数值" | `Patches\InfoDetailPatch.cs`（12 个补丁类，见 本文 13.2） | **0.5.6 完整迁移**（用户要求），迁移后已在本存档停用。差异：只留一个总开关；写死的英文改成走本地化 term；`(int)枚举` 比较改成枚举名；补了若干空引用保护；原作未使用的 `ToCardIdWithEquipment()` 没搬 |

| **Auto Pause** | `3017796084`（id `AutoPauseOnLoad` v1.1.1，作者 NBKRedSpy，**有公开源码** github.com/NBKRedSpy/Stacklands-AutoPauseOnLoad） | ① Postfix `MainMenu.Awake`：给 `ContinueButton.Clicked` 挂回调，点了就 `GameScreen.TimePause()` ② Postfix `TransitionScreen.StartTransition(Action, TransitionType, float)`（**私有重载**）：把 `___onTransition` 包一层，过渡结束再暂停（这个行为有它唯一的开关 `Pause On Board Change`） | `Patches\AutoPausePatch.cs`（3 个补丁类，见 本文 13.3） | **0.5.6 完整迁移**（用户要求「把这两个mod的功能也合并进来」）。差异：① 读档那条**不对齐** —— 原 mod 在点击那一帧就 `TimePause()`，但那时 `GameScreen.instance` 可能还是 null（屏幕是按需激活的），我们改成"点 Continue 只记请求 + `WorldManager.Play()` 后置里暂停"（那时 `instance` 一定有效）；② 换地点那条加了"**场地真的变了才暂停**"，避免剧情结束也暂停；③ 原 mod 一个开关 → 本项目两个开关 |
| **Lopi's Conflict Rows** | `3100344684`（id `ConflictRows` v1.0.0，作者 lopi——就是 Hit Text To Center 的作者） | 三个 Prefix **接管**：`Conflict.GetPositionInConflict`（每张战斗卡位置：行内居中 + 行号沿 Z 偏移）、`Conflict.GetConflictSize`（**私有**：框尺寸）、`Conflict.GetBounds`（框本体）；每行上限＝它的设置项 `maxUnitsInRow`（默认 7） | `Patches\ConflictRowsPatch.cs`（3 个补丁类，见 本文 13.4） | **0.5.6 完整迁移**。差异：① 本项目补了一个总开关（原 mod 只有数值、没有开关）；② 原 mod 的 dll 用 `[assembly: IgnoresAccessChecksTo("GameScripts")]` 绕过可见性检查来调两个私有方法，本项目改用 `AccessTools.Method` + `MethodDelegate` 反射委托，拿不到就整体放行回原版单行排列 |

### 9.2 与本机其它 Mod 的重叠

同功能同时开一般不会崩（Harmony 会叠加补丁），但有两个副作用：① **开包的连锁会变成两条并行** → 开包速度翻倍甚至更乱；② **堆叠上限由"谁先打补丁"决定**（两个 transpiler 都找 `ldc.i4.s 30`，第一个改掉后第二个找不到，日志会报"没找到常量"），归堆距离同理（都找 `ldc.r4 2`）。建议在游戏内停用重复项（主菜单 → Mods → 底部 **Disable/Enable Mods**，按存档生效；或直接在 Steam 退订）。本机创意工坊目录为 `...\workshop\content\1948280\`（Stacklands AppID 1948280）：

| Mod | 关系 | 建议 |
| --- | --- | --- |
| HigherStacks / PACKSPLOSION!!! / NoWitchNoPortal | 功能完全重复 | 停用 |
| Show Idle Villager | **两半都已被本项目合并**（村民红框 0.5.0 + 生产设施红框 0.5.6） | 停用 |
| Auto Pause（`3017796084`） | **已被本项目完整迁移**（读档后暂停 + 换地点后暂停，见 本文 13.3） | **用户已退订**（2026-09-14）；此前存档 `DisabledMods` 里也已停用 |
| Lopi's Conflict Rows（`3100344684`） | **已被本项目完整迁移**（战斗多行排列，见 本文 13.4） | **用户已退订**（2026-09-14）；此前存档 `DisabledMods` 里也已停用 |
| Stacks n Capacity（`AutoStack`） | **部分重叠**：也提升堆叠上限，另有自动堆叠/火锅容量等；**「箱子拖出」它本来就做**（本项目把机制用到自己的磁力箱上，并让它对"箱子叠在魔法胶水上"也生效，见 §9.1） | **建议保留**：原版资源箱的拖出现在靠它 |
| Stacklands Plus | 也补 `EndOfMonthCutscenes`（用途不同，只是同样把 `CanSpawnPortals` 置 false） | 可以留着 |
| Better Mouse Controls（`3012078700`） | **右键拖整叠**与我们的实现重复；**双击归堆**已被本项目合并 | 可停用（本项目两者都有了） |
| Stack Further（`2995935585`） | **完全重复**（本项目已合并，且运行时生效更彻底） | 停用 |
| Multi Select & Drag（`multidrag`，`3780343990`） | **框选多张卡已被本项目合并**，两个 mod 都会在 Ctrl 按下时抢拖拽输入 | 停用 |
| Hit Text To Center（`3016077439`） | **已被本项目合并**（并且我们把治疗数字也居中了；它自己会一直开着挡住伤害） | 停用 |
| HigherSidebar（`2995428012`） | **已被本项目完整迁移**（信息栏自动加高；两边都会改 `InfoBox.sizeDelta`，同时开就会互相打架） | **已在本存档停用** |
| BetterInfo（`2997317807`） | **已被本项目完整迁移**（12 个补丁点；两边都挂 `GetCombatableDescriptionAdvanced` / `GetSummary` 等同一个方法，输出会互相覆盖） | **已在本存档停用** |

详见 本文「参考 mod 与其它 Mod 的重叠关系」。


## 扩展指南

**加一张新卡**：`Cards/<id>.json`（`id` 用 `stacklandspro_` 前缀）→ 需要图标就放 `Icons/` 或借原版卡 id →
`localization.tsv` 加 `_name` / `_description` 两个 term → 要逻辑就写一个 `CardData` 子类并把 `"script"` 写成 `命名空间.类名`
→ **想让它也受"每张卡一个总开关"控制**：在 `ModConfig` 里加一个 `CardXxx` 开关 + 把 `(产物id, 创意id)` 加进对应的 id 数组
（`RebuildDisabledCards` 会把它们放进禁用集合，`ApplyCardAvailability` / `RefreshCardopedia` / `CardGatePatch` 自动生效）。

**加一个配方**：`Blueprints/<id>_idea.json`（`RequiredCards` / `ResultCard` / `Time` / `StatusTerm`）→
**建筑类原料务必写 `CardsToRemove`** → 原料之间若叠不起来，先解决叠放（或者调顺序）→
在 `Ready()` 的 `CardBagEntries` 里加一条 `(创意卡id, 卡包类型)` 让它能抽到（别忘了同步 `ModConfig` 的 id 数组）。

**加一个体验功能（补丁）**：读 `堆叠大陆Mod开发知识.md` 第六、七节 →
先用 Mono.Cecil **预检目标方法/字段**（存在性、签名、是否 public）→ 写一个 `Patches\XxxPatch.cs`
（一个功能一个文件 + 注释写清"为什么补这里"）→ 加进 `Mod.cs` 的 `PatchClasses` → 编译 →
`verify.ps1` 离线校验 → 反编译自己的 dll 复核注解 → 部署。

**加一个设置开关**：在 `ModConfig.cs` 里加一个静态字段 → 在 `Init()` 里
`Register(config, "<config.json 的键>", "<term 后缀>", delegate (bool v) { Xxx = v; })`（数值型用带默认值的 float 重载）→
在对应补丁/逻辑开头加 `if (!ModConfig.Xxx) return ...;`（补丁里早退，逻辑里跳过）→
`localization.tsv` 加 `stacklandspro_cfg_<后缀>_name` / `_tip` 两个 term → 编译部署。
（若这个功能是 transpiler 型、无法"运行时早退"，就照 `StackLimitPatch` / `StackDistancePatch` 的做法：
把常量换成对 `ModConfig` 里某个静态方法的调用。）
→ 加完跑一遍 `verify.ps1`，它会检查每个后缀的两个 term 是否都在 `localization.tsv` 里。

**改原版数值之前先找原版开关**（本项目已复用的挂点见 `堆叠大陆Mod开发知识.md` 第八节），不要急着自己造。


## 素材来源与版权声明
- **容量**：JSON 里的 `_MaxDollarCount`（历程：0.5.8 无限 → 0.5.13 原版 1000 → **0.5.14 用户定值 100000**）——想改回无限改 JSON 即可，不用重编译。
> 准备发布到创意工坊时，这一节就是"合规说明"；直接可用的商店页文案与发布清单见 [`创意工坊发布指南.md`](创意工坊发布指南.md)。

| 对象 | 本项目的处理方式 | 风险 |
| --- | --- | --- |
| **游戏本体**（`GameScripts.dll` / `SokLoc.dll` 等） | 只用反编译**读**机制来写补丁（方法名、IL 常量、字段可访问性）。反编译产物放在**仓库外** `..\..\stacklands-mod-common\decompiled\`，不进 git、不进 Mods 目录、不随 Mod 发布 | 低——官方 modding wiki 明确要求不要公开分享反编译内容，我们没有分享 |
| **参考的 10 个 mod** | 只读取"补丁打在哪个游戏方法上""它怎么控制某个状态"这类**关于游戏本身的事实**；**代码全部自行实现**；不复制对方源码、不打包对方 DLL。逐一对照见 §9.1 | 低——本项目 `README` 与本文都写明了出处与"哪些没合并" |
| **游戏美术/音频资源** | **一份都没复制**。六张卡的 `icon` 都是借用**原版卡 id**（`resource_chest` / `coin_chest` / `shell_chest` / `creditcard` / `warehouse` / `greenhouse`），图标本身由游玩者的游戏本体提供 | 低——这是 mod 的常规做法 |
| 本 Mod 自己的代码 / JSON / 文档 | 全部原创 | —— |

**发布时建议在创意工坊描述里保留的署名句**（已含在 `创意工坊发布指南.md` §3 的文案里）：

> 本 Mod 的所有代码都是独立实现；部分功能的"思路与补丁落点"参考了上述 mod（只读取"改的是哪个游戏方法"这类关于游戏本身的事实，未复制其源码或 DLL）。

**本项目自身的许可证**：工程里**没有 LICENSE 文件**（`Mod.csproj` 来自官方模板 `stacklandsdev/mod-template`，模板未见许可证声明）。
默认状态下即"保留所有权利"——别人可以玩，但不能合法再发布你的代码。若想让别人自由二次开发，可加 MIT；若想允许修改但禁止商用/要求同样开源，可用 CC BY-NC-SA 4.0。
（决定后我可以补 LICENSE 文件并同步 `README.md` 的许可段落。）


## 验证方式

> 下面是 0.5.x 时代逐项验收时留下的两份清单（离线自检 + 进游戏回归）。0.5.7 之后新增功能的逐项验收记录已压缩进 `CHANGELOG.md`，细节查 git 历史中的旧《开发与交接说明》第八节。

## 离线验证结果（不需要进游戏）

- ✅ `dotnet build`：**0 警告 0 错误**
- ✅ 从编译产物里复核补丁类的注解目标，与本表一致（54 个补丁类）
- ✅ 用 Mono.Cecil 逐个确认目标方法**唯一**：`Boosterpack.Clicked`=1、`EndOfMonthCutscenes.SpecialEvents`=1、`CardData.CanHaveCardOnTop`=1、`GameCard` 内的 `Update`=1 → 不会触发 Harmony 的歧义异常
- ✅ `ldc.i4.s 30` 在 `CanHaveCardOnTop` 里恰好 1 处 → transpiler 必然命中
- ✅ `ldc.r4 2` 在 `WorldManager.StackSend` / `TrySendToChest` 里各恰好 1 处 → 归堆距离 transpiler 必然命中，且不会误伤
- ✅ 发现并规避了 `TrySendToMagnet` 的闭包陷阱（改用 Prefix）
- ✅ `CardData.FindMatchingPrint` 是**私有**方法 → 用 `[HarmonyPatch(typeof(CardData), "FindMatchingPrint")]` 字符串形式打（Harmony 能补私有方法），已从编译产物复核注解
- ✅ 0.5.6：**29 个设置项**全部注册、`localization.tsv` 表头 1 行 + **84 条 term** 全部 4 列且无重复、
  设置界面引用的 **58 个 term** 0 缺失、
  JSON 里每个 `_字段`（`_PullRadius` 等）在对应脚本类上都存在，**54 个补丁类全部登记在 `Mod.PatchClasses`**
  （最后这条是踩过坑加的：漏登记 = 补丁静默失效）—— 这些都由 `verify.ps1` 自动检查
  （2026-09-13 校正：早先这里写的"44 个 term"是把表头行也算进去了）
  `CardData` 上**没有** `CanBePushed()`，所以只能重写前者）
  transpiler 匹配失败只会表现为"开关没效果"，离线预检比进游戏排查便宜得多

## 进游戏后的验证清单

> ✅ **第 1~16 条（0.5.0 的全部内容）已由用户实测通过（2026-09-13）**，保留作**回归测试**用。
> ✅ **第 17~43 条已全部由用户实测验收通过**（0.5.5 一批于 2026-10-05「其他的全部验收通过」落账；其中第 36~39 条为 0.5.6/0.5.7 期间验收、第 42 条魔法收集器为 0.5.10 验收）。
> 其中**合成文案已由用户确认「左下角提示正常了」**；**磁力信用卡用户说"暂时无法验证，后面再说"**。
> ✅ **第 26~37 条（0.5.6：迁移 HigherSidebar / BetterInfo / Auto Pause / Lopi's Conflict Rows + Alt+点击自动跳主意页 + 两个 mod 已停用 + 取牌方向 + 空闲设施红框 + 高级仓库改名与第二配方 + 高级温室）已由用户实测通过（2026-09-14「要验证的这些都正常了」）**，保留作**回归测试**用。
> 其中**高级温室主意卡图标按用户决定保持"灯泡兜底图"**（「主意的图片还是灯泡，算了就这样吧」）；**Auto Pause / Lopi's Conflict Rows 用户已退订**。

1. ✅ 主菜单 → Mods：显示 **0.5.0** 且没被划掉；点进本 mod 能看到 **29 项**；`F3` 日志里能看到 **54 行** `patched XxxPatch`
2. ✅ **开关生效**：随便切一个（例如关掉「空闲村民红色边框」），进游戏后行为立刻跟着变，不需要重启
3. ✅ **卡总开关**：关掉「卡牌：硬币磁力箱」→ 抽卡池里再也抽不到它的创意、配方做不出来、图鉴里看不到；再打开又能抽到
4. ✅ **硬币磁力箱**：合成（先放磁铁再叠硬币箱）→ 场上散金币自己飞进箱子 → 点击取 5 枚 → 取出的金币 10 分钟内不被吸回
5. ✅ **双击归堆**：双击一张散卡，附近的同种卡叠过来；双击储物箱**不应**触发
6. ✅ **归堆距离**：苹果树产出的苹果会去找更远的同类堆；把「自动归堆搜索距离」改成 8 更明显；关掉开关回到 2 格
7. ✅ **堆叠**：把 40 张以上同类卡堆一起，能超过原版的 30（关掉开关则应回到 30）
8. ✅ **卡包**：点一下卡包，卡会一张张自己飞出来直到开完（不是一次性瞬间全出，是有节奏的连锁）
9. ✅ **传送门**：打死女巫之后的月底，不再出现"奇怪传送门出现了"
10. ✅ **空闲村民**：站着没事干的村民有红框；**在建筑里工作的村民应该没有红框**（这是与参考 mod 的差异，重点看一眼是否符合你的预期）
11. ✅ **三路运输输出**：Cities 场地给磁力箱接 2~3 路管道 → 出现「正在运输输出」计时器 →
    每 10 秒**每一路各吐 1 张**；只接 1 路就只吐 1 张；把管道全拔掉计时器立刻消失
12. ✅ **输出期间照样能收卡**（修过的回归）：计时器在跑的时候，飞过来的材料仍应被吸进箱子，
    而不是在箱子顶上弹跳；手动拖材料上去也能放进去
13. ✅ **框选多张卡**：Ctrl+拖空白出现蓝框 → 松手框内散卡被黄框标出 → 拖任意一张选中卡整组移动 →
    松手各自落地、**黄框保留**（选择一直留着，点空白处才取消）；**Ctrl+点击不应有任何选中反应**（箱子也不应取物）；
    **Shift+框选不追加**（等价于替换）；Ctrl+拖时镜头**不应**平移；关掉开关后 Ctrl+拖恢复成原版平移镜头
13b. ✅ **框选对装备/工人正确**（两个修过的回归）：① `Ctrl+框选`**带装备的村民应当被选中并能整组一起搬**，
    移动后装备**仍挂在身上**；② **装备件本身不应被单独选中**（不应出现"装备自己跑出来"）；
    ③ 在建筑里干活的工人不被搬走
14. ✅ **卡面重叠**：硬币磁力箱中间是箱子图、只有一个数量、左下不再出现第二个数字
15. ✅ **数字居中**：战斗中受到的伤害数字画在**卡牌正中**；**治疗**（营地/治疗卡、吸血回复、被动回血）的数字也**居中**；
    设置界面关掉「数字显示在卡牌中间」后，**伤害**回到原来压在卡片下沿的位置，**治疗仍然居中**；miss 仍照旧飞出去不居中
16. ✅ 日志里没有 `failed to patch` 字样
17. ✅ **箱子拖出（0.5.5）**：① 磁力箱 Shift+左键按住 → 出 **10 张**跟手、松开落地；Ctrl+左键 → **1 张**同样跟手；
    ② **原版资源箱**：Ctrl 与 Shift **都只出一份**（Shift＝10 张、Ctrl＝1 张）；③ **资源箱叠在魔法胶水上**：
    Shift+左键按住箱子 → 出的是**箱子里面的牌**，胶水与箱子**留在原地**（Ctrl 同样只出一份）；
    ④ Shift+左键拖普通牌堆里的一张 → 只拖出**你点中的那张**（不再整叠跟着走）；
    ⑤ 不带 Ctrl/Shift 的点击/拖动箱子行为不变（点击取 1、拖动搬箱子）；⑥ 右键拖整叠仍然正常；
    ⑦ 关掉「卡牌：磁力箱」后磁力箱不再能拖出（原版资源箱的行为不受影响）
18. ✅ **资源箱取出方向（0.5.5）**：在 Cities 场地放一个原版资源箱、放进材料 → 左键点击取 1 张 →
    牌应该**朝旁边弹到地上**（不是垂直弹起又落回箱子上）；把设置项「资源箱取出的牌往边上掉」关掉 → 回到原版那副样子
19. ✅ **快捷键送往箱子（0.5.5）**：鼠标指着散落的材料，按 **R** → 卡自己弹起来飞进能收下它的箱子（磁力箱/资源箱）；
    指着**一叠**按 F → 整叠飞走；鼠标指着空处按 R → 什么都不发生；把设置项「送往箱子的快捷键」改成别的名字（例如 `X`）→ 新键生效；
    填一个认不出来的名字（例如 `QQQ`）→ 回落到 `R`（日志里有一行提示）
20. ✅ **贝壳磁力箱（0.5.5）**：设置界面能看到第 4 个卡总开关「卡牌：贝壳磁力箱」；
    用**磁铁 + 贝壳宝箱**合成（先放磁铁，再把贝壳宝箱叠上去，5 秒，两个都消耗）→ 得到贝壳磁力箱；
    场上散落的**贝壳**自己飞进箱子（建筑刚产出的贝壳也直接吸走）→ 点击一次取 **5 枚** →
    取出的贝壳 10 分钟内不会被吸回去；卡面外观/图标与**原版贝壳宝箱**一致（不是硬币箱的外观）；
    把「卡牌：贝壳磁力箱」关掉 → 创意从卡包消失、配方做不出、图鉴里看不到（旧存档里的照旧能用）
21. ✅ **磁力信用卡（0.5.5；0.5.11 修卡面中间图标；0.5.12 悬停显示余额；0.5.13 上限改回原版 1000）**：设置界面能看到「卡牌：磁力信用卡」这一项；
    用**磁铁 + 信用卡**合成（先放磁铁，再把信用卡叠上去，5 秒，两个都消耗）→ 得到磁力信用卡；
    场上散落的**纸币**自己飞进卡里（卖卡得到的、建筑产出的也直接吸走）→ 点击一次最多吐出 **100 面额** →
    吐出的纸币 10 分钟内不会被吸回去；**买东西照旧按原版结算扣钱**（原版 `BuyWithCreditcard`）；
    卡面外观/图标与**原版信用卡**一致
22. ✅ **框选后点牌堆任意位置也能整组拖（0.5.5）**：Ctrl 框选 ≥2 张（其中至少一叠是多张摞起来的）→
    点那一叠的**中间/最上面那张**拖动 → **整组一起走**（原来只把那一张拖出来）；只选 1 张时保持原版（点子卡＝拆下上面那半）
23. ✅ **框选后选择保留、可反复拖（0.5.5）**：框选后拖动一轮 → 松手后**黄色高亮仍在**、不用重新框选就能**再拖第二趟**；
    只有**点击空白处**才取消高亮/选中（关掉「框选多张卡」开关也会清空）
24. ✅ **合成状态文案（0.5.5）**：把材料叠到磁铁上开始合成时，左下角应显示
    「正在制作磁力箱 / 正在制作硬币磁力箱 / 正在制作贝壳磁力箱 / 正在制作磁力信用卡」（不再是"正在吸附…"）— **已确认「左下角提示正常了」**
25. ✅ **Alt+点击搜索主意 + 三个按钮（0.5.5）**：鼠标指着**石头**这类卡，**Alt+左键单击** → 左侧主意只剩**标题**里含"石头"的；
    换一张卡 **Alt+右键单击** → 按**描述**筛（列表内容和标题搜不同即为正常）；**Alt+点空白处** → 搜索框清空、列表恢复完整；
    搜索框旁应有**三个固定按钮**「搜标题 / 搜描述 / 清除」，**悬停不弹提示**，点「搜标题」能切回标题模式；
    **箱子类卡片要按类型名搜**（「石头箱」→ 按「资源箱」搜、「石头磁力箱」→ 按「磁力箱」搜）；
    Alt+左键点**箱子**不应取物、点**卡包**不应开包；把设置项「Alt+点击搜索主意」关掉 → 完全回到原版（只能手打搜索词、只匹配标题）

26. ✅ **信息栏自动加高（0.5.6，迁移 HigherSidebar）**：把鼠标放到一张"信息很多"的卡上（例如敌人、卡包、古书）→
    左侧信息栏应该**自己变高**到刚好放下文字（不再被约 300px 的高度裁掉）；鼠标移开后**变回矮的**（变矮有 0.1 秒防抖，不应疯狂抖动）；
    悬停信息栏区域时**下面的卡片仍然点得中**（信息栏不吃鼠标射线）；
    把设置项「信息栏自动加高」关掉 → 立刻回到原版固定高度；「信息栏最小/最大高度」两个数值改了应该马上生效
27. ✅ **详细信息：战斗详解（0.5.6，迁移 BetterInfo）**：悬停一个敌人/村民 → 描述末尾应有
    攻击类型（Melee/Ranged/…）+ 攻速 + 命中 + 伤害 + 防御 + **估算每秒伤害**（原来只有四项数值、没有 DPS）；
    悬停**动物**（牛/羊这类）→ 多一行"每 N 秒产出一张 X"；悬停 Mob → 有掉落表（带百分比）；
    悬停**会带装备的敌人** → 多一段"可能掉落的装备："（已有的正常显示、没的加删除线）
28. ✅ **详细信息：掉落百分比 / 卡包内容（0.5.6）**：悬停一个采集点（苹果树/洞穴/墓穴/古书）→ 掉落表**每行都有百分比**；
    悬停一个**卡包**（在商店里或拿着它）→ 信息栏显示卡包内容，同样带百分比；**墓穴**应比原版多一条金币杯的行；
    进入**图鉴**里看同一个采集物 → 掉落**不应重复出现两份**（描述里那份 + 图鉴那份）
29. ✅ **详细信息：岛上贝壳 / 金币计数（0.5.6）**：进到**岛上**场地 → 侧栏金钱那行应显示
    「贝壳数 🐚  金币数 🪙」（原版只显示金币）；贝壳数 = 贝壳箱里存的 + 地上散落的
30. ✅ **Alt+点击后自动跳到主意页（0.5.6）**：先把左侧侧边栏切到**任务**页 → 鼠标指着一张卡按 **Alt+左键** →
    侧边栏应**自动跳到主意页**并显示搜索结果（原来停在任务页时"搜了也看不见"）；
    侧边栏已最小化时按 Alt+点击 → 再展开时应停在**主意页**
31. ✅ **两个迁移 mod 已停用（0.5.6）**：主菜单 → Mods → 列表里 **BetterInfo** 与 **HigherSidebar** 应显示为**灰色删除线**（已停用）；
    `F3` 日志里不应再有它们的 `patched ...` 行，也不应有报错（这两条功能现在由本 mod 提供）
32. ✅ **取牌不再落回箱子（0.5.6）**：对着**磁力箱**或**原版资源箱**按住 **Ctrl+左键单击**（点一下就好，别拖动）→
    取出的牌应该**朝箱子外侧的地上弹出去**，**不是**在箱子上方冒一下又被吞回去；**Shift+左键单击**同理（10 张一起弹出去）；
    按住 **Ctrl/Shift 真的拖动**时，牌堆照旧跟随鼠标、松手落在你放的地方（这一条 0.5.5 已验收过，别退化）；
    把设置项「取出的牌往边上掉」关掉 → 回到原版那副样子（弹起又落回箱子 / 被吞回去）
33. ✅ **空闲生产设施红框（0.5.6，合并 Show Idle Villager 的另一半）**：场上**空着的**菜园/农场、钓鱼点、捕鱼陷阱、温室，
    以及**散在地上没被用掉的便便**，应该显示红色高亮框；**一旦上面长出东西 / 派了村民 / 放了种子**，红框立刻消失；
    设置界面里能看到新的一项「空闲生产设施红色边框」（`Idle facility border`，默认开），关掉它 → 这些卡不再标红（村民那条不受影响，
    反之亦然）；玩家**拖着**这类卡时不应标红
34. ✅ **自动暂停（0.5.6，合并 Auto Pause）**：主菜单点「继续游戏」→ 读到存档后游戏应该是**暂停**的（左下角速度条是暂停态、
    时间不走），再点一下速度/空格才继续；**新开一局不应被暂停**；
    点地图上的地点切换（主大陆↔岛↔森林↔Cities）→ 过渡结束后应**自动暂停**；
    **剧情/过场结束后不应暂停**（这是本项目相对原 mod 的改进：只有场地真的变了才暂停）；
    设置界面关掉「切换地点后自动暂停」→ 切地点后不再暂停（读档那条仍生效）；关掉「读到存档后自动暂停」→ 读档后不暂停
35. ✅ **战斗多行排列（0.5.6，合并 Lopi's Conflict Rows）**：让一队**超过 7 张**卡同时参战（例如把一堆村民/士兵堆在敌人旁边，
    或用调试菜单多造几张）→ 战斗框里应该**排成多行**（第 8 张开始出现在后面一行），框（bounds）也包得住；
    把设置项「每行战斗单位上限」改成 3 → 每行只放 3 张、行数相应变多；
    关掉「战斗多行排列」→ 回到原版"一队只排一行"的样子；`F3` 里不应出现
    "战斗多行排列：反射私有方法失败"（出现＝说明游戏版本变了、该功能已自动退回原版）
36. ✅ **高级仓库改名 + 第二种配方（0.5.6）**：卡片与创意上应显示「**高级仓库**」（原来是"超级仓库"）；
    造法二——堆 **10 个石头 + 10 个铁条**，再叠**一名村民**上去 → 出现计时器、**120 秒**后变成高级仓库，
    **村民留在原地不消失**（石头和铁条消耗掉）；造法一（10 张仓库，20 秒）仍照旧；
    两个高级仓库叠一起时上限仍是每个 +140（场地卡牌上限累加）
37. ✅ **高级温室（0.5.6，2026-09-14 用户实测通过「要验证的这些都正常了」；0.5.10 改版）**：**6 张温室**叠成一叠 → 40 秒 → 「**高级温室**」；
    它的**卡面图标与普通温室卡一致**；**主意卡图标＝原版灯泡兜底图（用户已拍板"算了就这样吧"，别再当 bug 追）**；
    往上放 **1 个种子**（例如 1 个胡萝卜）→ 出现生长计时器（**15 秒**）→
    长好后**弹出 1 份产物**（1 个胡萝卜 → 1 个胡萝卜产物），而**种子仍然叠在高级温室上**、
    并且**立刻自动开始下一轮**（0.5.10 用户口径：放一个作物，每 15 秒生产一次，每次生产一个）；
    设置界面里能看到第 6 个卡总开关「卡牌：高级温室」，
    关掉它 → 创意从卡包里消失、配方做不出来、图鉴里看不到（旧存档里已有的照旧能用）；
    **运输输出（0.5.6 追加，已实测通过）**：卡片上有一个**可点击的"输出方向"小箭头**（和冶炼厂/锯木厂/采石场一样，点它转方向）——
    箭头是运行期从原版卡预制体借来的（"借子部件"这条路已确认可行）；
    把**配套的箱子**（里面放一张同种物品＝设定成收胡萝卜）或**炖锅**放在温室旁边 **2.5 格内**（尤其箭头指的那一侧）→
    每轮产出的产物**直接送进那个容器**；附近没有可放的容器时照旧往外弹；
    Cities 场地仍可接 1 路管道输出（接上后走管道，优先级高于上面这条；连接器只在 Cities 场地显示，原版规则）；
    **回归重点**：从箱子里一次取出多张卡（Shift 拖出 10 张）→ 卡名必须全部正确、牌堆能正常拖动
    （0.5.6 第一次就是因为追加的生长配方漏了 `ParentBlueprint` 而在这里崩）
38. ✅ **高级仓库可堆叠（0.5.7，2026-09-27 用户实测「堆叠和火锅功能正常」）**：任意两张"仓库 / 高级仓库"都能互相叠放（拖一张到另一张上应正常叠住，不再弹开）；
    叠着的每个高级仓库照常给场地 **+140** 上限（放两个高级仓库在场上，卡牌上限应 +280）；
    **混着放不会误触发合成**：一叠里混进高级仓库后，"10 张仓库"配方不应再匹配；
    关掉卡总开关「卡牌：高级仓库」不影响普通仓库之间的互叠
39. ✅ **火锅容量无限（0.5.7，2026-09-27 用户实测「堆叠和火锅功能正常」）**：往火锅里塞食物，**超过 50 点后仍继续吞**（卡面食物数字一直涨，不再封顶）；
    设置界面关掉「火锅容量改为最大值」→ **新生成**的火锅回到原版 50 点上限
    （旧火锅若已被本 mod 设成无限会退回 50；装着 Stacks n Capacity 时它设的 10000 不受影响）
40. ✅ **动物留在饲养栏（0.5.7，待测试）**：饲养栏里放两只同种动物 → 120 秒后产崽、**幼崽沿输出方向飞出**，
    而**两只亲代留在栏上**（不再像原版那样被弹出去）；再过 120 秒**自动又开始下一轮**繁殖（不用重新放动物）；
    月底结算时亲代也不该散落在栏外；设置界面关掉「动物留在饲养栏」→ 回到原版"繁殖完把亲代弹出去"的行为
41. ✅ **资源箱不计入卡牌上限（0.5.8 重写，待测试）**：原版侧栏的卡牌计数和月底"卡牌超限、强制卖卡"的结算都会把
    **资源箱里的每个资源都算进上限**（主大陆/岛上：箱子 1 张 + 箱内数量；Cities 场地固定算 2 张）；
    0.5.8 重写后**每个资源箱固定只算 1 张**（＝箱子本身，**和磁力箱口径一致**——用户需求原话「卡牌放入资源箱后，卡牌数 -1」）；
    验证方法：看**侧栏的卡牌计数**——往资源箱里塞材料时计数应该不再上涨（只有箱子本身那 1 张）；
    第一版实测没生效：`GetCardCount` 有**两个无参重载**（普通 + 泛型），binder 抛 AmbiguousMatchException → 补丁静默死掉，
    已改成 `TargetMethod()` 显式绑定无参重载 + Postfix；设置界面关掉「资源箱不计入卡牌上限」→ 立即恢复原版计数
42. ✅ **魔法收集器（0.5.8 新增；0.5.10 加管道输出；2026-10-05 用户实测「功能正常」）**：把 **1 木板 + 1 石砖 + 1 铁条 + 1 金条 + 1 玻璃 + 3 魔法尘埃**叠成一叠
    → 60 秒 → 「魔法收集器」（图标暂借原版魔法尘埃）；把**魔法师**叠上去 → 每 **30 秒**产 1 个**魔法尘埃**，
    产物**自动被 2 格内的箱子/炖锅接住**（没有就往外弹出）；
    **0.5.10 追加**：Cities 场地可接 **1 路运输管道输出**（和铁矿/高级温室/磁力箱一样，`_EnergyConnectors` 声明；
    接上后魔法尘埃顺着管道送走，没接管道行为不变）；
    **魔法师不消耗**、留在收集器上自动循环；把魔法师拖走 → 计时停止、不再产出；重新叠上 → 恢复生产；
    设置界面里能看到第 7 个卡总开关「卡牌：魔法收集器」，关掉它 → 创意从卡包消失、配方做不出来、图鉴隐藏（旧存档里已有的照旧能用）
43. ✅ **资源箱对齐磁力箱三连（0.5.8，待测试）**：
    ① **容量**：往资源箱里塞材料，**超过原版 100 后仍继续收**（侧栏卡牌计数照常扣减）；关掉「资源箱容量改为最大值」→
    本 mod 设过无限量的箱子退回 100（Stacks n Capacity 设的 10000 不受影响）；
    ② **类型**：把**食物（如苹果、炖菜）和装备（如斧头）**拖到资源箱上应该能存进去（仍一箱一种；
    金币/贝壳/纸币/便便拒绝）；关掉「资源箱可存更多卡牌类型」→ 只收资源类；
    ③ **取出堆叠**：连续点击资源箱取出材料 → 取出的卡应该**飞去叠到 2 格内的同种牌堆上**（照磁力箱取出的堆叠行为），
    附近没有同种堆才散落到旁边；
    ⚠️ 与 Stacks n Capacity 的兼容：它的拖出（Shift/Ctrl）走的是同一个 `RemoveResources`，取出的牌堆
    由我们的弹出逻辑接管，行为一致；它的收卡类型 Prefix 会被我们的 Postfix 全量重算覆盖（结果以我们为准）
44. ⏳ **时间倍数沿用（0.6.7，待验收）**：任意地图（城市/海岛/森林/主大陆）把速度调到 **5 倍** → 过一个月结 →
    下个回合仍是 **5 倍**（月结弹窗期间应仍是 1 倍速，弹窗结束后才恢复）；换地图 → 到新地图仍是 5 倍；
    开着设置项「切换地点后自动暂停」→ 到新地图是**暂停**状态，**解除暂停后是 5 倍**；
    回主菜单再读档 → 不会把上一局的 5 倍带进来
45. ⏳ **取出优先落空地（0.6.7，待验收）**：把**资源箱 / 城市资源容器 / 食物容器**紧挨着一堆卡或建筑摆（挡住"朝外 1.2 格"这个落点）→
    点击取物 → 卡应落到**另一块空地**上，不压在别的卡/建筑上、也不被自动归堆立刻吞掉；周围本来就有空地时行为与旧版一致（朝外 1.2 格）；
    连续 Shift 取出多叠时仍然**优先并到 2 格内的同种牌堆**上

> 排查提示：若日志通道失效（`Player.log` 运行期不落日志，本项目遇到过），
> 用 `OnGUI` 画一个临时屏幕诊断面板来取运行时状态，比反复改代码猜测快得多（做法见第九节的踩坑记录）。

---


