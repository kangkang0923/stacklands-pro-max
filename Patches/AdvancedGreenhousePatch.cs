using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace StacklandsProMaxNS
{
    /// <summary>
    /// **高级温室**（`stacklandspro_advanced_greenhouse`）：一次放 **1 个**种子、**种子不消耗**、
    /// 每 **15 秒**产出 1 份并自动循环。
    ///
    /// 用户需求原话（0.5.6）：先是「新增卡牌"高级温室"，6个普通温室合成，卡牌外观和温室保持一致，
    /// 主意卡牌也和温室的一致，高级温室支持放6个材料，可以一次产出6个材料，原版温室的6倍，
    /// 已下载到mod（Stacklands Plus）中有个超级温室，功能就参考他的，但是外观用普通温室的」；
    /// 第一版做完后他纠正了产出方式：「放6胡萝卜，产出6胡萝卜，产出的胡萝卜要弹出来，
    /// 产出后，温室里还是有6胡萝卜，会自动开始下一轮产出」。
    /// **0.5.9 改版**：「高级温室之前是放6个作物，然后90秒生产一次，一次生产6个，
    /// 改成放一个作物，每15秒生产一次，每次生产一个」——只是把配方规模/产出/时长改小，
    /// "种子不消耗、自动循环、产物运输输出"这套机制原封不动（时长不再对齐原版，固定 15 秒）。
    ///
    /// ── 原版的"生长"是怎么实现的（反编译核实） ────────────────────────────────
    ///
    /// 温室本体是个**空类**（`Greenhouse : CardData` 只重写 `CanHaveCard` 允许资源/村民/食物），
    /// 真正的产出全在 **`BlueprintGrowth`**（游戏里那张 `blueprint_growth` 卡）里：
    /// 它 `Init()` 时按"每种种子 × 每种种植基质（soil / poop / garden / farm / greenhouse）"
    /// 生成一堆 `Subprint`，例如 `RequiredCards = [apple, greenhouse]`、`ExtraResultCards = [apple_tree]`、
    /// `Time = 120 × 0.5`（温室的 0.5 倍时长系数）；`Subprint.StackMatchesSubprint` 是**按堆叠里的卡逐个配**
    /// （顺序无关、每张卡只能用一次、堆叠张数要 ≥ RequiredCards 条数），所以"把种子叠到温室上"就等于开了一条配方。
    ///
    /// ── 参考 mod 的做法与本项目的取舍 ─────────────────────────────────────────
    ///
    /// Stacklands Plus（`3355058679`）的超级温室做法是：**再建一张自己的生长蓝图卡**
    /// （`SuperBlueprintGrowth : BlueprintGrowth`，JSON 里 `subprints: []` 由代码填），
    /// 在 `Init()` 里先跑原版逻辑，再把每条"种子 × 温室"配方**改成"6 个同样种子 + 超级温室"**，
    /// 并把 `ExtraResultCards` **翻倍**（他们要 ×2）—— 注意它那条配方**会消耗那 6 个种子**。
    ///
    /// 本项目不新建蓝图卡（那还要把它塞进卡包、玩家得先抽到它才能用），而是**直接把配方追加进原版 `blueprint_growth`**：
    /// 玩家能种普通温室，就一定能用高级温室 —— 少一张卡、少一处联动。配方＝
    ///
    /// ```text
    /// RequiredCards    = [ 高级温室, 种子 ×1 ]     // 1 个种子（0.5.9 起从 6 个改成 1 个）
    /// ExtraResultCards = 该种子的产物 ×1           // 每轮产 1 份（0.5.9 起从 6 份改成 1 份）
    /// Time             = 15 秒（0.5.9 起固定值，不再对齐原版时长）
    /// StatusTerm / ResultAction = 对齐原版那条
    /// CardsToRemove    = [""]（哨兵）              // ★ 什么都不消耗：种子留在温室上 → 配方下一帧仍然成立 → 自动下一轮
    /// ```
    ///
    /// ⚠️ 三个细节：
    /// ① **注入必须幂等**：`BlueprintGrowth.Init()` 每次都会 `PopulateSubprints()` **清空重建**，
    ///    所以我们在 `Mod.Ready()`（保证跑得到）和 `BlueprintGrowth.Init` 的**后置**里各调一次，
    ///    用"列表里有没有高级温室的配方"当哨兵防重复；
    /// ② 追加的 `Subprint` **必须自己补 `ParentBlueprint` 与 `SubprintIndex`**（原版只在 `Blueprint.Init()`
    ///    里统一设，我们是在它之后追加的）。漏了 `ParentBlueprint` 会出空引用：`StackMatchesSubprint` 里
    ///    `if (ParentBlueprint.NeedsExactMatch && ...)` 只在**堆叠张数 ≥ 本条配方的 RequiredCards 数**时才走到，
    ///    于是表现为"**一次从箱子里取出很多张（≥7）时卡名错乱、整堆拖不动**"（0.5.6 实测踩到并由用户截图报出）；
    /// ③ **不需要**动 `BlueprintGrowth.BlueprintComplete`：原版那段"把种子样产物拉回基质"的逻辑只认
    ///    `greenhouse`，我们的卡不匹配；而我们要的效果恰恰是"产物飞出去、种子留着"，所以什么后处理都不用加
    ///    （第一版加过一个"把产物留在温室上"的后置，被用户否掉并删掉了）。
    ///
    /// ── "运输输出"（0.5.6 用户追加）────────────────────────────────────────────
    ///
    /// 需求原话：「高级温室要有运输输出，一个就行，产出的6个胡萝卜通过这个输出到指定地方（炖锅、箱子等）」。
    /// **一行 C# 都不用写**：原版 `Blueprint.BlueprintComplete()`（反编译核实）本来就有这段——
    ///
    /// 它先从参与合成的卡里挑一张"有输出连接器"的当目标，产物造好后先 `Restack` 成一叠：
    /// 有那张卡就 `StackSendCheckTarget` 往它的输出方向送，没有就退化成 `StackSend` 朝输出方向抛。
    ///
    /// `involvedCards` ＝ `MyGameCard.GetAllCardsInStack()`（根卡 + 全部子卡 ＝ 高级温室 + 6 个种子），
    /// 所以**只要高级温室自己有"运输输出"连接器**，`gameCard3` 就是它 → 产物走 `TrySendWithPipe` 送进
    /// 管道另一头的卡（箱子 / 炖锅…）。连接器由 `Cards\advanced_greenhouse.json` 的 `_EnergyConnectors`
    /// 声明（`EnergyConnectionType = 1`＝`CardDirection.output`、`EnergyConnectionStrength = 5`＝
    /// `ConnectionType.Transport`、`EnergyConnectionAmount = 1`）。
    /// **没接管道时** `TrySendWithPipe` 返回 false → 落回 `StackSend`（＝和以前完全一样地往外弹），所以这是纯增量。
    /// ⚠️ 游戏里连接器/管道**只在 Cities 场地显示与连接**（`GameCard.UpdateConnectors()` 里
    /// `if (WorldManager.instance.CurrentBoard.Id != "cities") 全部 SetActive(false)`）——这是原版规则，不是我们的限制。
    /// </summary>
    internal static class AdvancedGreenhouse
    {
        /// <summary>高级温室的卡 id。</summary>
        internal const string CardId = "stacklandspro_advanced_greenhouse";

        /// <summary>高级温室**主意卡**（蓝图）的 id。</summary>
        internal const string BlueprintCardId = "stacklandspro_blueprint_advanced_greenhouse";

        /// <summary>原版温室**主意卡**的 id（只用来复制它的图标）。</summary>
        internal const string VanillaIdeaId = "blueprint_greenhouse";

        /// <summary>一次要放的种子数（0.5.9 起用户要求 1：放一个作物）。</summary>
        internal const int SeedCount = 1;

        /// <summary>
        /// 一轮产出的份数（0.5.9 起用户要求 1：每次生产一个）。
        /// 0.5.6 首版是「放6胡萝卜，产出6胡萝卜」＝ 6 颗种子 × 每颗 1 份；0.5.9 改版后
        /// 「放一个作物，每15秒生产一次，每次生产一个」＝ 1 颗种子 × 1 份。
        /// </summary>
        internal const int YieldPerSeed = 1;

        /// <summary>
        /// 一轮生长时长（秒）。0.5.9 起用户要求固定 15 秒（「每15秒生产一次」），
        /// 不再对齐原版那条配方的时长（原版是 生长基础时长 × 温室 0.5 系数）。
        /// </summary>
        internal const float GrowTimeSeconds = 15f;

        /// <summary>
        /// "什么都不消耗"的哨兵：`Subprint.GetAllCardsToRemove()` 是
        /// "CardsToRemove 非空就照它销毁、否则按 RequiredCards 里**除结构/村民**之外的都销毁"，
        /// 所以想"一个都不消耗"不能传空数组（空数组会走 else 分支把种子吃掉），只能传一个**匹配不到任何卡**的 id
        /// （没有卡 id 是空字符串 → `FirstOrDefault` 返回 null → 谁都不销毁）。
        /// 这正是本卡"种子留着、自动循环产出"的关键。
        /// </summary>
        private static readonly string[] ConsumeNothing = new string[1] { "" };

        /// <summary>原版温室 / 生长蓝图的 id。</summary>
        private const string VanillaGreenhouseId = "greenhouse";
        private const string GrowthBlueprintId = "blueprint_growth";

        /// <summary>
        /// 把原版**温室主意卡**的图标复制到我们的主意卡上（在 `Mod.Ready()` 里调一次）。
        ///
        /// 为什么不能直接在 JSON 里写 `"icon": "blueprint_greenhouse"`：`icon` 的解析方式是
        /// "在**已加载的卡表**里按 id 找到那张卡、把它的 `Icon` 复制过来"，而加载 mod 卡的那个时机
        /// **蓝图类卡还查不到**（0.5.6 实测：这么写之后我们主意卡的 `Icon` 变成 null，
        /// 界面回退成通用的"灯泡"图标，用户连续两次截图指出"主意图标不对"）。
        /// 运行时（`Mod.Ready()` 之后）原版主意卡已经在卡表里了，直接复制引用即可 ——
        /// 和本项目 `_VanillaLookCardId`（磁力箱镜像原版储物箱、信用卡镜像原版信用卡）是同一套做法。
        /// **注意连 `null` 一起对齐**（原版那张卡自己也可能是 null，靠 `GameCard.UpdateIcon()` 的
        /// `SpriteManager.IdeaIcon` 兜底；我们只保证"Icon 字段和原版一模一样"）。
        /// </summary>
        internal static void ApplyVanillaIdeaIcon()
        {
            WorldManager world = WorldManager.instance;
            if (world == null)
            {
                return;
            }
            CardData ours = world.GetCardPrefab(BlueprintCardId, false);
            if (ours == null)
            {
                Debug.LogWarning("[StacklandsProMax] 高级温室：找不到自己的主意卡 " + BlueprintCardId);
                return;
            }
            CardData vanilla = world.GetCardPrefab(VanillaIdeaId, false);
            if (vanilla == null)
            {
                // 兜底：`GetCardPrefab` 查的是 `GameDataLoader.idToCard` 字典，正常情况（`InitializeLoadedData()`
                // 已经把全部卡，含蓝图，灌进去了）一定有；这里再直接在"蓝图 prefab 表 / 卡表"里按 id 找一遍，
                // 纯粹是防"字典里缺这条"时图标悄悄退化成 JSON 里那个温室卡图标（图标问题已经来回两次了）。
                GameDataLoader loader = world.GameDataLoader;
                if (loader != null)
                {
                    vanilla = loader.BlueprintPrefabs.Find(b => b.Id == VanillaIdeaId);
                    if (vanilla == null)
                    {
                        vanilla = loader.CardDataPrefabs.Find(c => c.Id == VanillaIdeaId);
                    }
                    if (vanilla != null)
                    {
                        Debug.Log("[StacklandsProMax] 高级温室：idToCard 里没有 " + VanillaIdeaId
                            + "，但在 prefab 表里找到了，用它拷图标。");
                    }
                }
            }
            if (vanilla == null)
            {
                Debug.LogWarning("[StacklandsProMax] 高级温室：找不到原版主意卡 " + VanillaIdeaId
                    + "，主意卡继续用 JSON 里的图标");
                return;
            }
            // ★ 连 null 一起对齐，不要"只有非 null 才拷"：
            //   原版那张主意卡的 `Icon` 有可能是 null（那就靠 GameCard.UpdateIcon() 的兜底图 =
            //   SpriteManager.IdeaIcon：蓝图类卡的 CardUpdateType 是 Mod → 走 else 分支，和原版 Main 卡
            //   拿到的是同一张图）。我们只要让"自己的 Icon 字段 == 原版那张卡的 Icon 字段"，
            //   渲染结果就必然一致；反过来如果只拷非 null 值，原版为 null 时我们的卡会一直留着
            //   JSON 里那个"温室卡图标"，看起来就和原版主意卡不一样（0.5.6 用户两次截图就是这个问题）。
            string before = ours.Icon == null ? "<null>" : ours.Icon.name;
            ours.Icon = vanilla.Icon;
            Debug.Log("[StacklandsProMax] 高级温室主意图标 <- " + VanillaIdeaId + "："
                + (vanilla.Icon == null ? "<null>（走 IdeaIcon 兜底图）" : vanilla.Icon.name)
                + "（本来是 " + before + "）");
        }

        /// <summary>
        /// 把高级温室的生长配方追加进 `BlueprintGrowth`。取不到 `blueprint_growth` 就只打一句警告。
        /// </summary>
        internal static void InjectGrowthSubprints()
        {
            WorldManager world = WorldManager.instance;
            GameDataLoader loader = world == null ? null : world.GameDataLoader;
            if (loader == null)
            {
                return;
            }
            BlueprintGrowth growth = loader.GetCardFromId(GrowthBlueprintId, false) as BlueprintGrowth;
            if (growth == null)
            {
                Debug.LogWarning("[StacklandsProMax] 高级温室：找不到 " + GrowthBlueprintId
                    + "（生长蓝图），本卡暂时不能种植");
                return;
            }
            Inject(growth);
        }

        /// <summary>把"高级温室 × 6 个种子"的配方追加进生长蓝图（幂等）。</summary>
        internal static void Inject(BlueprintGrowth growth)
        {
            if (growth == null)
            {
                return;
            }
            List<Subprint> subprints = growth.Subprints;
            if (subprints == null)
            {
                return;
            }

            // 哨兵：已经注入过就别再来一遍（Init 可能被调用多次）
            for (int i = 0; i < subprints.Count; i++)
            {
                if (ContainsCard(subprints[i].RequiredCards, CardId))
                {
                    return;
                }
            }

            List<Subprint> additions = new List<Subprint>();
            for (int i = 0; i < subprints.Count; i++)
            {
                Subprint vanilla = subprints[i];
                string seed = SeedOf(vanilla);
                if (seed == null)
                {
                    continue; // 不是"种子 × 温室"那条配方
                }
                additions.Add(BuildAdvanced(seed, vanilla));
            }
            if (additions.Count == 0)
            {
                return;
            }
            for (int i = 0; i < additions.Count; i++)
            {
                Subprint added = additions[i];
                // ⚠️⚠️ 这两个字段**必须自己补**：原版只在 `Blueprint.Init()` 里统一设
                //       （`subprint.ParentBlueprint = this; subprint.SubprintIndex = i;`），
                //       而我们是在 Init 跑完之后才追加的，不补就会留下 ParentBlueprint == null。
                //       危害（0.5.6 实测踩到，用户报"批量取出 10 张牌时最上面那张变成 Rabbit、整堆还拖不动"）：
                //       `Subprint.StackMatchesSubprint` 里有一句 `if (ParentBlueprint.NeedsExactMatch && ...)`，
                //       只要**堆叠张数 ≥ RequiredCards.Length（我们这条是 7）**就会走到那一句 → 空引用异常，
                //       异常从配方匹配里抛出去会把当帧的卡牌/堆叠处理打断（表现成卡名错乱、牌堆拖不动）。
                added.SubprintIndex = subprints.Count; // 必须是它在列表里的下标：计时结束后原版用 Subprints[TimerSubprintIndex] 取回这条配方
                added.ParentBlueprint = growth;
                subprints.Add(added);
            }
            Debug.Log("[StacklandsProMax] 高级温室：已追加 " + additions.Count + " 条生长配方（"
                + SeedCount + " 个种子 → " + (SeedCount * YieldPerSeed) + " 份产出、" + GrowTimeSeconds
                + " 秒一轮，种子不消耗、自动循环）");
        }

        /// <summary>这条配方是不是"种子 × 普通温室"？是就返回种子的卡 id，不是返回 null。</summary>
        private static string SeedOf(Subprint print)
        {
            string[] required = print == null ? null : print.RequiredCards;
            if (required == null || required.Length != 2)
            {
                return null;
            }
            if (required[0] == VanillaGreenhouseId)
            {
                return required[1];
            }
            if (required[1] == VanillaGreenhouseId)
            {
                return required[0];
            }
            return null;
        }

        /// <summary>
        /// 照原版那条配方生成"高级温室"版本：基质换成高级温室、种子要 1 个、每轮产 1 份、
        /// 时长固定 15 秒，并且**什么都不消耗**（种子留在温室上 → 配方下一帧仍然成立 → 自动开始下一轮）。</summary>
        private static Subprint BuildAdvanced(string seed, Subprint vanilla)
        {
            string[] required = new string[SeedCount + 1];
            required[0] = CardId;
            for (int i = 1; i < required.Length; i++)
            {
                required[i] = seed;
            }

            // 原版这条配方的产物：`PopulateSubprints` 里是"同一个产物 id 重复 ResultCount 次"，
            // 所以取第 0 个就是这个种子长出来的东西。
            string[] vanillaResults = vanilla.ExtraResultCards ?? new string[0];
            string item = vanillaResults.Length > 0 ? vanillaResults[0] : null;
            string[] results = new string[0];
            if (!string.IsNullOrEmpty(item))
            {
                results = new string[SeedCount * YieldPerSeed];
                for (int i = 0; i < results.Length; i++)
                {
                    results[i] = item;
                }
            }

            return new Subprint
            {
                RequiredCards = required,
                CardsToRemove = ConsumeNothing, // ★ 种子不消耗：温室一直保持 6 个种子、自动循环产出
                ExtraResultCards = results,
                StatusTerm = vanilla.StatusTerm,
                Time = GrowTimeSeconds, // 0.5.9 起固定 15 秒，不再对齐原版时长
                ResultAction = vanilla.ResultAction,
            };
        }

        private static bool ContainsCard(string[] cards, string cardId)
        {
            if (cards == null)
            {
                return false;
            }
            for (int i = 0; i < cards.Length; i++)
            {
                if (cards[i] == cardId)
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>
    /// 把高级温室的生长配方追加进 `BlueprintGrowth`。
    /// ⚠️ `Init()` 里会 `PopulateSubprints()` **清空重建**，所以这里挂后置；`Mod.Ready()` 里也会调一次
    /// （两处都是幂等的），这样无论"蓝图 Init 早于/晚于 mod Ready"都能生效（见 `AdvancedGreenhouse` 类注释）。
    /// </summary>
    [HarmonyPatch(typeof(BlueprintGrowth), "Init")]
    internal static class AdvancedGreenhouseGrowthPatch
    {
        [HarmonyPostfix]
        internal static void Postfix()
        {
            AdvancedGreenhouse.InjectGrowthSubprints();
        }
    }

    /// <summary>
    /// 允许**普通温室之间互相叠放** —— 这是「6 张温室 → 高级温室」配方能凑齐的前提。
    ///
    /// ── 问题（用户报的 bug）──────────────────────────────────────────────────
    /// 主意卡上写着「6 张温室 → 高级温室」，但游戏里**温室拖不到另一张温室上**，配方永远凑不齐。
    /// 原因（反编译核实）：原版 `Greenhouse` 是个空类，只重写了 `CanHaveCard`——
    ///
    /// 它的 `CanHaveCard` 只放行三类：资源、村民、食物（其余类型一律 false）。
    ///
    /// 也就是只收**材料 / 村民 / 食物**；温室自己是 `Structures`，所以"温室 × 温室"恒为 false。
    /// （这是原版行为，不是我们改坏的：原版根本没有需要叠温室的配方，所以没人踩到。）
    ///
    /// ── 做法 ─────────────────────────────────────────────────────────────
    /// 对齐本项目已有的 `WarehouseStackingPatch`（高级仓库配方要叠 10 张仓库，是同一个坑）：
    /// 在 `CardData.CanHaveCardOnTop`（拖拽落卡 / 自动堆叠 / 堆叠有效性复查的**最终入口**）上挂**后置**，
    /// 只在原版已经返回 `false`、且"目标卡＝普通温室、拖上来的卡＝普通温室"时改成 `true`。
    ///
    /// 为什么用后置而不是前缀：后置**完整保留原版所有判定**（损坏卡、箱子特例、村民/种子/食物的
    /// 正常收卡路径……），我们只补这一种原本被拒绝的组合；前缀会把整段逻辑短路掉，容易误伤。
    ///
    /// ── 两个"不要扩大范围"的理由 ──────────────────────────────────────────────
    /// ① 只放行 **普通温室 ↔ 普通温室**（`greenhouse`）。配方 `RequiredCards` 就是 6 个 `greenhouse`，
    ///    高级温室（`stacklandspro_advanced_greenhouse`）不算数，让它们互相叠只会误导玩家；
    /// ② 高级温室身上叠的是"**永久循环的种子**"（`CardsToRemove = [""]`，见上面 `AdvancedGreenhouse`），
    ///    要是普通温室也能压到高级温室上，玩家一失手就把自己的种子顶掉 / 把生长配方挤掉。
    ///    所以高级温室继续维持"只收材料/村民/食物"的原版口径。
    ///
    /// ⚠️ 堆叠张数上限**要自己再算一遍**：原版这个上限（30；0.5.8 起被 `StackLimitPatch` 换成
    /// `ModConfig.GetMaxStackSize()`）就写在 `CanHaveCardOnTop` 里，我们用后置改写它的返回值等于绕开了它，
    /// 不补的话温室可以无限往上叠（视觉上是一根无底柱，也没必要）。
    /// 口径照原版：`(目标堆张数) + (拖过来的那叠张数) > 上限` 就不让叠。
    /// </summary>
    [HarmonyPatch(typeof(CardData), "CanHaveCardOnTop")]
    internal static class GreenhouseStackingPatch
    {
        private const string GreenhouseId = "greenhouse";

        [HarmonyPostfix]
        internal static void Postfix(CardData __instance, CardData otherCard, ref bool __result)
        {
            if (__result || __instance == null || otherCard == null)
            {
                return;
            }
            // 只补"普通温室 ← 普通温室"这一种组合，其余全部交回原版结论
            if (__instance.Id != GreenhouseId || otherCard.Id != GreenhouseId)
            {
                return;
            }
            GameCard targetCard = __instance.MyGameCard;
            if (targetCard == null)
            {
                return;
            }
            GameCard draggedCard = otherCard.MyGameCard;
            // 原版口径：目标堆张数 = 根卡 + 子卡；拖过来的那叠同理（没有 GameCard 时按 1 张算，和 isPrefab 一致）
            int targetCount = targetCard.GetRootCard().GetChildCount() + 1;
            int draggedCount = draggedCard == null ? 1 : (draggedCard.GetRootCard().GetChildCount() + 1);
            if (targetCount + draggedCount > ModConfig.GetMaxStackSize())
            {
                return;
            }
            __result = true;
        }
    }
}
