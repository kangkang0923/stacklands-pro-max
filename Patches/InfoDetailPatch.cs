using System.Collections.Generic;
using System.Text.RegularExpressions;
using HarmonyLib;
using UnityEngine;

namespace StacklandsProMaxNS
{
    /// <summary>
    /// "详细信息"（迁移自创意工坊 **BetterInfo** `2997317807`，id `BetterInfo`，v1.5.5）。
    ///
    /// 用户要求原话（0.5.6）：「迁移这两个mod的功能，已下载，HigherSidebar，Betterlnfo，
    /// 迁移完成后帮我禁用这两个mod」。
    ///
    /// ── 这个 mod 到底加了什么（反编译核实，本文件逐条对照后自行实现） ────────────────────
    ///
    /// | # | 原作补丁点 | 效果 |
    /// | --- | --- | --- |
    /// | 1 | `Combatable.GetCombatableDescriptionAdvanced` 后置 | 战斗详解：攻速/命中/伤害/防御 **+ 攻击类型 + 估算每秒伤害**，动物补"每 N 秒产出一张 X"，Mob 补掉落表、敌人补可能装备（已有的不打删除线） |
    /// | 2 | `CardData.GetPossibleDrops` 后置 | 普通 Mob（非 Enemy）也有掉落表（原版返回空） |
    /// | 3 | `StablePortal.UpdateCard` 前置+后置 | 传送门描述里补"第 N 波" |
    /// | 4 | `BoosterpackData.GetSummary` 前置（接管） | 卡包内容**带百分比**、每个 CardBag 一段、未发现的卡合并成一行概率 |
    /// | 5 | `Harvestable.UpdateDescription` 前置（接管） | 地点/可采集物的掉落表带百分比；墓穴/洞穴/遗迹/古书/钓鱼点有专门处理 |
    /// | 6 | `CombatableHarvestable.UpdateCard` 前置 | 同上（会掉东西的战斗采集物） |
    /// | 7 | `TravellingCart.UpdateCard` 前置 | 同上（旅行货车） |
    /// | 8 | `CardopediaScreen.GetDropSummaryFromCard` 前置 | 上面这些已经把掉落写进描述了，图鉴里那份就不要重复 |
    /// | 9 | `Equipable.GetAdvancedEquipableInfo` 后置 | 装备攻击类型（近战/远程…）顶到最前面 |
    /// | 10 | `GameScreen.Update` 后置 | 悬停牌堆时把这张牌的名字+描述显示在信息栏；鼠标指着卡包时显示卡包内容；**在岛上**把侧栏金钱改成"贝壳 + 金币" |
    /// | 11 | `Blueprint.GetText` 后置 | 蓝图（创意）上显示产物的特殊效果与装备详解 |
    /// | 12 | 启动时 | 把原版"详细战斗数值"选项（`AdvancedSettingsScreen.AdvancedCombatStatsEnabled`）打开 |
    ///
    /// ── 与原 mod 的差异（都是有意为之，逐条列出） ─────────────────────────────
    ///
    /// · **只留一个总开关** `ModConfig.DetailInfo`（"开关要克制"）：关掉 = 这些补丁全部早退，
    ///   完全回到原版。原 mod 没有开关（装上就生效）。
    /// · 原作写死的英文（`Estimated dmg/s`、`Makes one X every Ns.`）改成走本地化 term
    ///   （`stacklandspro_info_estimated_dps` / `stacklandspro_info_makes_one`），中文界面显示中文。
    /// · 原作用 `(int)枚举 == 3` 这类数字比较，这里改成 `CardType.Ideas` / `EquipableType.Head`
    ///   这类枚举名（值逐个核对过反编译源码）。
    /// · 补了几处**空引用保护**（原作没有）：掉落袋为 null、`GetCardPrefab` 找不到卡、
    ///   概率总和为 0、蓝图没有 subprint。只防崩，不改变任何正常情况下的输出。
    /// · 原作用 `AccessTools`-注入的私有字段 `___descriptionOverride`；当前版本的
    ///   `CardData.descriptionOverride` **本来就是 public**，这里直接写它（同样效果，少一层魔法）。
    /// · 原作的 `CardIdWithEquipmentCombat.ToCardIdWithEquipment()` 没有任何地方调用，没搬。
    /// · 原作在"敌人没有可能装备"时仍会先写下"可掉落装备："这行空表头再返回；这里改成直接不写。
    ///
    /// ⚠️ 迁移时核对了**当前游戏版本**的反编译源码：原版现在已经自带一部分同类信息
    /// （`GetPossibleDrops` 支持 FishingSpot/Harvestable/Enemy、`GetSummaryFromAllCards` 列名字、
    /// 地点描述列掉落表、图鉴掉落摘要、`GetCombatableDescriptionAdvanced` 的四项数值），
    /// 所以本文件"补"的主要是 **百分比、估算 DPS、攻击类型、波次、牌堆描述、贝壳金币计数** 这些
    /// 原版仍然没有的东西 —— 关掉 BetterInfo 之后玩家看到的信息量不会变少。
    /// </summary>
    internal static class InfoDetail
    {
        /// <summary>
        /// 一个"可能出现的敌人 + 它身上可能带的装备"组合，用来算敌人卡包的真实概率分布
        /// （原作内部类，只在 <see cref="CardBagToChance"/> 里用）。
        /// </summary>
        private class CardIdWithEquipmentCombat
        {
            internal string Id;
            internal List<string> Equipment;
            internal float TotalCombatLevel;

            internal CardIdWithEquipmentCombat(string id, List<string> equipment, float totalCombatLevel)
            {
                Id = id;
                Equipment = equipment;
                TotalCombatLevel = totalCombatLevel;
            }
        }

        /// <summary>
        /// 把一个 `CardBag` 摊平成"卡 id + 百分比"的列表（原作的核心工具方法）。
        ///
        /// 处理四种卡包类型（`CardBagType`）：
        ///   · `Chances`    —— 直接就是 Chances 列表；
        ///   · `SetPack`    —— `SetPackCards` 每张权重 1；
        ///   · `SetCardBag` —— 查 `CardBag.GetChancesForSetCardBag`；
        ///   · `Enemies`    —— 先把敌人卡包展开成"敌人池 × 可带装备"的全组合，
        ///                    再按 `StrengthLevel` 过滤掉打不过的。
        ///
        /// 之后按原版"这张卡现在拿不到"的规则过滤（已达上限 / 前置卡没给过 / 和平模式下的敌人与墓穴 /
        /// 已发现的创意与传闻），算出百分比；敌人条目再按"每个敌人的概率 ÷ 可能出现的敌人数量"摊开。
        /// </summary>
        internal static List<CardChance> CardBagToChance(CardBag bag)
        {
            List<CardChance> chances = new List<CardChance>();
            if (bag == null || WorldManager.instance == null || WorldManager.instance.GameDataLoader == null)
            {
                return chances;
            }

            GameDataLoader loader = WorldManager.instance.GameDataLoader;
            switch (bag.CardBagType)
            {
                case CardBagType.SetCardBag:
                    chances = CardBag.GetChancesForSetCardBag(loader, bag.SetCardBag);
                    break;

                case CardBagType.SetPack:
                    if (bag.SetPackCards != null)
                    {
                        for (int i = 0; i < bag.SetPackCards.Count; i++)
                        {
                            chances.Add(new CardChance(bag.SetPackCards[i], 1));
                        }
                    }
                    break;

                case CardBagType.Chances:
                    if (bag.Chances != null)
                    {
                        chances = bag.Chances;
                    }
                    break;

                case CardBagType.Enemies:
                    SetCardBagType enemyBag = loader.GetSetCardBagForEnemyCardBag(bag.EnemyCardBag);
                    chances = CardBag.GetChancesForSetCardBag(loader, enemyBag);
                    if (chances == null)
                    {
                        chances = new List<CardChance>();
                        break;
                    }
                    chances.RemoveAll(delegate (CardChance chance)
                    {
                        CardData prefab = loader.GetCardFromId(chance.Id, true);
                        Combatable combatable = prefab as Combatable;
                        return combatable != null
                            && combatable.ProcessedCombatStats != null
                            && combatable.ProcessedCombatStats.CombatLevel > bag.StrengthLevel;
                    });
                    break;
            }

            // ---- 过滤掉"这张卡现在抽不到"的条目（参照原作的三个条件）----
            List<CardChance> available = new List<CardChance>();
            for (int i = 0; i < chances.Count; i++)
            {
                CardChance chance = chances[i];
                if (chance == null)
                {
                    continue;
                }
                if (chance.IsEnemy)
                {
                    available.Add(chance); // 敌人条目后面单独摊开
                    continue;
                }
                if (chance.HasMaxCount && CountOnBoard(chance.Id) >= chance.MaxCountToGive)
                {
                    continue;
                }
                if (chance.HasPrerequisiteCard && !WorldManager.instance.GivenCards.Contains(chance.PrerequisiteCardId))
                {
                    continue;
                }
                CardData prefab = WorldManager.instance.GetCardPrefab(chance.Id, true);
                if (prefab == null)
                {
                    continue; // 防御：卡池里写了不存在的卡时别把整个描述搞崩
                }
                bool blockedByPeaceful = WorldManager.instance.CurrentRunOptions.IsPeacefulMode
                    && (prefab is Enemy || prefab.Id == "catacombs");
                bool blockedByDiscovery = (prefab.MyCardType == CardType.Ideas || prefab.MyCardType == CardType.Rumors)
                    && WorldManager.instance.CurrentSave.FoundCardIds.Contains(chance.Id);
                if (!blockedByPeaceful && !blockedByDiscovery)
                {
                    available.Add(chance);
                }
            }

            // ---- 算百分比（百分比写在原对象上，和原作一致）----
            float total = 0f;
            for (int i = 0; i < available.Count; i++)
            {
                total += available[i].Chance;
            }
            for (int i = 0; i < available.Count; i++)
            {
                available[i].PercentageChance = total > 0f ? available[i].Chance / total : 0f;
            }

            // ---- 敌人条目：展开成"具体哪个敌人"（按装备组合算完战力再过滤）----
            List<CardChance> result = new List<CardChance>();
            for (int i = 0; i < available.Count; i++)
            {
                CardChance chance = available[i];
                if (!chance.IsEnemy)
                {
                    result.Add(chance);
                    continue;
                }

                SetCardBagType bagType = loader.GetSetCardBagForEnemyCardBag(chance.EnemyBag);
                List<CardIdWithEquipmentCombat> pool = GetAllPossibleEnemiesWithEquipment(
                    SpawnHelper.GetEnemyPoolFromCardbags(new List<SetCardBagType> { bagType }, true));
                pool.RemoveAll(x => x.TotalCombatLevel > chance.Strength);
                pool.Sort((a, b) => b.TotalCombatLevel.CompareTo(a.TotalCombatLevel));

                List<string> ids = new List<string>();
                for (int p = 0; p < pool.Count; p++)
                {
                    if (!ids.Contains(pool[p].Id))
                    {
                        ids.Add(pool[p].Id);
                    }
                }

                List<CardChance> spread = new List<CardChance>();
                for (int d = 0; d < ids.Count; d++)
                {
                    spread.Add(new CardChance(ids[d], 1));
                }
                for (int s = 0; s < spread.Count; s++)
                {
                    spread[s].PercentageChance = spread.Count > 0
                        ? spread[s].Chance * chance.PercentageChance / spread.Count
                        : 0f;
                }
                result.AddRange(spread);
            }

            result.Sort((a, b) => b.PercentageChance.CompareTo(a.PercentageChance));
            return result;
        }

        /// <summary>这块场地上现在有几张这个 id 的卡（原版"已达上限"判断用的口径）。</summary>
        private static int CountOnBoard(string cardId)
        {
            int count = 0;
            List<GameCard> all = WorldManager.instance.AllCards;
            for (int i = 0; i < all.Count; i++)
            {
                GameCard card = all[i];
                if (card != null && card.MyBoard != null && card.MyBoard.IsCurrent
                    && card.CardData != null && card.CardData.Id == cardId)
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// 把卡包渲染成一段带百分比的文本（原作的核心工具方法）。**逐字参照它的输出格式**：
        ///
        /// ```
        /// 可能包含：
        /// 3x
        /// ● 45%: 石头
        /// ● 55%: 木头
        /// ● 20%: 还有 2 张没发现的卡
        /// ```
        ///
        /// `prefix` 传空字符串就不打表头；`customNumber` 传 -1 用 `CardsInPack`，
        /// 传 &gt;0 用这个数字，传 0 就不打"Nx"这行（旅行货车就是这么用的）。
        /// </summary>
        internal static string GetSummaryFromAllCards(CardBag cardBag, string prefix = "label_may_contain",
            int customNumber = -1)
        {
            List<CardChance> chances = CardBagToChance(cardBag);
            if (chances.Count == 0 || cardBag == null)
            {
                return "";
            }

            List<string> lines = new List<string>();
            int undiscovered = 0;
            float undiscoveredChance = 0f;

            for (int i = 0; i < chances.Count; i++)
            {
                CardChance chance = chances[i];
                CardData prefab = WorldManager.instance.GetCardPrefab(chance.Id, true);
                if (prefab == null)
                {
                    continue;
                }
                string name = prefab.FullName;
                if (prefab.MyCardType == CardType.Ideas)
                {
                    name = SokLoc.Translate("label_an_idea");
                }
                if (prefab.MyCardType == CardType.Rumors)
                {
                    name = SokLoc.Translate("label_a_rumor");
                }

                string line = Icons.Circle + (chance.PercentageChance * 100f).ToString("0") + "%: " + name;
                if (!WorldManager.instance.CurrentSave.FoundCardIds.Contains(chance.Id))
                {
                    // 没发现过的卡不剧透名字，只把概率并成一行（原版图鉴的口径）
                    undiscovered++;
                    undiscoveredChance += chance.PercentageChance * 100f;
                }
                else if (!lines.Contains(line))
                {
                    lines.Add(line);
                }
            }

            string head = "";
            if (!string.IsNullOrEmpty(prefix))
            {
                head = SokLoc.Translate(prefix) + "\n";
            }
            if (customNumber == -1)
            {
                head = head + cardBag.CardsInPack + "x\n";
            }
            else if (customNumber > 0)
            {
                head = head + customNumber + "x\n";
            }
            if (undiscovered > 0)
            {
                head = head + Icons.Circle + undiscoveredChance.ToString("0") + "%: <size=90%>"
                    + SokLoc.Translate("label_undiscovered_cards", LocParam.Plural("count", undiscovered))
                    + "</size>\n";
            }
            return head + string.Join("\n", lines) + "\n";
        }

        /// <summary>取出装备列表里某个部位的所有装备（原作工具方法）。</summary>
        private static List<Equipable> GetEquipableOfType(List<Equipable> equipables, EquipableType type)
        {
            List<Equipable> result = new List<Equipable>();
            if (equipables == null)
            {
                return result;
            }
            for (int i = 0; i < equipables.Count; i++)
            {
                if (equipables[i] != null && equipables[i].EquipableType == type)
                {
                    result.Add(equipables[i]);
                }
            }
            return result;
        }

        /// <summary>
        /// 把敌人池摊平成"敌人 × 头/武器/身体三件装备（每件都可以没有）"的所有组合，
        /// 每个组合都算一遍战力 —— 这样算了装备加成之后才知道哪些敌人真的打得过。
        /// </summary>
        private static List<CardIdWithEquipmentCombat> GetAllPossibleEnemiesWithEquipment(List<Combatable> enemyPool)
        {
            List<CardIdWithEquipmentCombat> result = new List<CardIdWithEquipmentCombat>();
            if (enemyPool == null)
            {
                return result;
            }

            for (int i = 0; i < enemyPool.Count; i++)
            {
                Combatable enemy = enemyPool[i];
                if (enemy == null || enemy.RealBaseCombatStats == null)
                {
                    continue;
                }
                result.Add(new CardIdWithEquipmentCombat(enemy.Id, new List<string>(),
                    enemy.RealBaseCombatStats.CombatLevel));

                if (!enemy.HasInventory)
                {
                    continue;
                }

                List<Equipable> heads = GetEquipableOfType(enemy.PossibleEquipables, EquipableType.Head);
                heads.Add(null);
                List<Equipable> weapons = GetEquipableOfType(enemy.PossibleEquipables, EquipableType.Weapon);
                weapons.Add(null);
                List<Equipable> torsos = GetEquipableOfType(enemy.PossibleEquipables, EquipableType.Torso);
                torsos.Add(null);

                for (int h = 0; h < heads.Count; h++)
                {
                    for (int w = 0; w < weapons.Count; w++)
                    {
                        for (int t = 0; t < torsos.Count; t++)
                        {
                            List<string> equipment = new List<string>();
                            CombatStats stats = new CombatStats();
                            stats.InitStats(enemy.RealBaseCombatStats);
                            if (heads[h] != null)
                            {
                                stats.AddStats(heads[h].MyStats);
                                equipment.Add(heads[h].Id);
                            }
                            if (weapons[w] != null)
                            {
                                stats.AddStats(weapons[w].MyStats);
                                equipment.Add(weapons[w].Id);
                            }
                            if (torsos[t] != null)
                            {
                                stats.AddStats(torsos[t].MyStats);
                                equipment.Add(torsos[t].Id);
                            }
                            if (equipment.Count > 0)
                            {
                                result.Add(new CardIdWithEquipmentCombat(enemy.Id, equipment, stats.CombatLevel));
                            }
                        }
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// 场上一共有多少贝壳：**贝壳箱里存的** + 散在场地上的贝壳
        /// （岛上侧栏的"贝壳 + 金币"计数用它，参照原作口径）。
        /// </summary>
        internal static int GetShellCount()
        {
            int count = 0;
            WorldManager world = WorldManager.instance;
            if (world == null || world.AllCards == null)
            {
                return count;
            }
            for (int i = 0; i < world.AllCards.Count; i++)
            {
                GameCard card = world.AllCards[i];
                if (card == null || card.MyBoard == null || !card.MyBoard.IsCurrent)
                {
                    continue;
                }
                Chest chest = card.CardData as Chest;
                if (chest != null && chest.HeldCardId == "shell")
                {
                    count += chest.CoinCount;
                    continue;
                }
                if (card.CardData is Shell)
                {
                    count++;
                }
            }
            return count;
        }
    }

    /// <summary>
    /// ① 战斗详解（迁移 BetterInfo）：原版 `GetCombatableDescriptionAdvanced` 只给
    /// 攻速/命中/伤害/防御四项，这里**整个替换**成更详细的一版：
    /// 顶上补攻击类型（近战/远程/魔法…）、把攻速与伤害带上具体数值、加"估算每秒伤害"
    /// （把特殊命中按各自倍率折算进去），动物再补一条产出说明。
    ///
    /// Mob 还补掉落表；敌人（`Enemy`）再补"可能带的装备"（已经拥有的正常显示，
    /// 没有的用删除线标记）。图鉴里渲染的**就是这张卡本尊**时不补掉落，
    /// 免得和图鉴自己那条路径的摘要重复（见 Patch 里的详细说明）。
    /// </summary>
    [HarmonyPatch(typeof(Combatable), "GetCombatableDescriptionAdvanced")]
    internal static class InfoDetailCombatPatch
    {
        [HarmonyPostfix]
        internal static void Postfix(Combatable __instance, ref string __result)
        {
            if (!ModConfig.DetailInfo || __instance == null || __instance.ProcessedCombatStats == null)
            {
                return;
            }

            CombatStats stats = __instance.ProcessedCombatStats;
            string speedLabel = SokLoc.Translate("label_combat_speed");
            string hitLabel = SokLoc.Translate("label_hit_chance");
            string damageLabel = SokLoc.Translate("label_damage");
            string defenceLabel = SokLoc.Translate("label_defence");

            // "命中时平均伤害"：原作用 攻击力 与 攻击力+1 的均值再减 1（等于把伤害的随机浮动考虑进去）
            float attackDamage = stats.AttackDamage;
            float bumpedDamage = CombatStats.IncrementAttackDefence(stats.AttackDamage, 1);
            float averageHit = attackDamage * 0.5f + bumpedDamage * 0.5f - 1f;

            float specialDamage = 0f;
            float specialChanceSum = 0f;
            float hitChance = stats.HitChance;
            float attackSpeed = stats.AttackSpeed;

            List<SpecialHit> specialHits = stats.SpecialHits;
            if (specialHits != null)
            {
                for (int i = 0; i < specialHits.Count; i++)
                {
                    SpecialHit hit = specialHits[i];
                    specialChanceSum += hit.Chance;
                    float damage = 0f;

                    // 只有"打在敌人身上"的特殊命中才算进 DPS
                    if (hit.Target == SpecialHitTarget.Target
                        || hit.Target == SpecialHitTarget.RandomEnemy
                        || hit.Target == SpecialHitTarget.AllEnemy)
                    {
                        switch (hit.HitType)
                        {
                            case SpecialHitType.Poison:
                                damage += hit.Chance / 100f * (averageHit + 0.1f);
                                break;
                            case SpecialHitType.Stun:
                            case SpecialHitType.LifeSteal:
                            case SpecialHitType.Damage:
                                damage += hit.Chance / 100f * averageHit;
                                break;
                            case SpecialHitType.Bleeding:
                                damage += hit.Chance / 100f * (averageHit + 2.5f);
                                break;
                            case SpecialHitType.Crit:
                                damage += hit.Chance / 100f * (averageHit * 2f);
                                break;
                            case SpecialHitType.Sick:
                                damage += hit.Chance / 100f * (averageHit + 0.2f);
                                break;
                        }
                    }
                    // 打全体的按 2.5 个目标折算
                    if (hit.Target == SpecialHitTarget.AllEnemy)
                    {
                        damage *= 2.5f;
                    }
                    // 狂热：提高自己的攻速，按触发机会加权得到"平均攻速"
                    if (hit.Target == SpecialHitTarget.Self && hit.HitType == SpecialHitType.Frenzy)
                    {
                        float weight = Mathf.Clamp(10f / attackSpeed * (hit.Chance / 100f), 0f, 1f);
                        attackSpeed = (1f - weight) * attackSpeed
                            + weight * CombatStats.IncrementAttackSpeed(stats.AttackSpeed, 1);
                    }
                    specialDamage += damage;
                }
            }
            // 没被特殊命中覆盖的那部分普攻
            specialDamage += averageHit * (100f - specialChanceSum) / 100f;

            float damageReduction = Mathf.CeilToInt(stats.Defence * 0.5f);
            string speedText = stats.GetAttackSpeedTranslation() + " (" + stats.AttackSpeed + "s)";
            string hitText = stats.GetHitChanceTranslation() + " (" + (hitChance * 100f).ToString("0") + "%)";
            string damageText = stats.GetAttackDamageTranslation() + " (" + attackDamage + "dmg)";
            string defenceText = stats.GetDefenceTranslation() + " (" + damageReduction + "dmg)";
            string dpsLabel = SokLoc.Translate("stacklandspro_info_estimated_dps");
            float dps = attackSpeed > 0f ? specialDamage / attackSpeed * hitChance : 0f;

            // 演示卡（图鉴里那张临时卡）没有 MyGameCard，用基础攻击类型
            string attackType = __instance.MyGameCard != null
                ? __instance.ProcessedAttackType.ToString()
                : __instance.BaseAttackType.ToString();

            __result = "</i><size=90%>"
                + attackType
                + "\n" + speedLabel + " " + speedText
                + "\n" + hitLabel + " " + hitText
                + "\n" + damageLabel + " " + damageText
                + "\n" + defenceLabel + ": " + defenceText
                + "\n" + dpsLabel + ": " + dps.ToString("0.00")
                + "</size>";

            // 动物：补"每 N 秒产出一张 X"
            Animal animal = __instance as Animal;
            if (animal != null && !string.IsNullOrEmpty(animal.CreateCard))
            {
                CardData product = WorldManager.instance.GetCardPrefab(animal.CreateCard, true);
                if (product != null)
                {
                    __result = __result + "\n\n" + SokLoc.Translate("stacklandspro_info_makes_one",
                        LocParam.Create("card", product.Name),
                        LocParam.Create("time", animal.CreateTime.ToString()));
                }
            }

            // Mob：补掉落表。**图鉴里正在渲染这张卡本尊**（`HoveredEntry.MyCardData == __instance`）时跳过 ——
            // 那种情况图鉴自己那条路径会显示掉落摘要，补了就是两份。
            // （图鉴里那张演示卡是 `Instantiate` 出来的副本，和 prefab 不是同一个对象，
            //  所以正常在图鉴里悬停时**不会**命中这个早退，掉落照常显示 —— 与原 mod 行为一致。）
            Mob mob = __instance as Mob;
            if (mob == null)
            {
                return;
            }
            CardopediaScreen cardopedia = CardopediaScreen.instance;
            if (cardopedia != null && cardopedia.HoveredEntry != null
                && cardopedia.HoveredEntry.MyCardData == __instance)
            {
                return;
            }
            __result = __result + "\n\n" + InfoDetail.GetSummaryFromAllCards(mob.Drops, "label_can_drop");

            // 敌人：再补"可能带的装备"（已有 = 正常，没有 = 删除线）
            Enemy enemy = mob as Enemy;
            if (enemy == null || !enemy.CanHaveInventory)
            {
                return;
            }
            List<Equipable> possible = enemy.PossibleEquipables;
            if (possible == null || possible.Count == 0)
            {
                return;
            }

            __result = __result + "\n" + SokLoc.Translate("label_can_drop").TrimEnd(':') + " "
                + SokLoc.Translate("cardtype_equipable") + ":";

            bool demo = enemy.MyGameCard == null || enemy.MyGameCard.IsDemoCard;
            List<Equipable> owned = demo ? new List<Equipable>() : enemy.GetAllEquipables();
            for (int i = 0; i < possible.Count; i++)
            {
                Equipable equip = possible[i];
                if (equip == null)
                {
                    continue;
                }
                bool hasIt = demo || owned.Exists(e => e != null && e.Id == equip.Id);
                __result = __result + "\n" + Icons.Circle
                    + (hasIt ? equip.Name : "<s>" + equip.Name + "</s>");
            }
        }
    }

    /// <summary>
    /// ② 普通 Mob（非 `Enemy`）也有掉落表：原版 `GetPossibleDrops` 对 Mob 返回空列表，
    /// 这里补上"掉落袋里的卡 + 可带装备"（迁移 BetterInfo）。
    /// </summary>
    [HarmonyPatch(typeof(CardData), "GetPossibleDrops")]
    internal static class InfoDetailPossibleDropsPatch
    {
        [HarmonyPostfix]
        internal static void Postfix(CardData __instance, ref List<string> __result)
        {
            if (!ModConfig.DetailInfo || __result == null || __result.Count != 0)
            {
                return;
            }
            Mob mob = __instance as Mob;
            if (mob == null || __instance is Enemy)
            {
                return; // Enemy 原版已经处理了
            }

            CardBag drops = mob.Drops;
            if (drops != null)
            {
                List<string> cards = drops.GetCardsInBag();
                if (cards != null && cards.Count > 0)
                {
                    __result.AddRange(cards);
                }
            }
            if (mob.CanHaveInventory && mob.PossibleEquipables != null)
            {
                for (int i = 0; i < mob.PossibleEquipables.Count; i++)
                {
                    Equipable equip = mob.PossibleEquipables[i];
                    if (equip != null)
                    {
                        __result.Add(equip.Id);
                    }
                }
            }
        }
    }

    /// <summary>
    /// ③ 传送门的描述里补一句"第 N 波"（迁移 BetterInfo）。
    /// 前置记下"原本没有描述"这个状态，后置再往描述后面追加 —— 只在**原本为空**、
    /// 现在有内容、而且不是图鉴演示卡的时候加。
    /// </summary>
    [HarmonyPatch(typeof(StablePortal), "UpdateCard")]
    internal static class InfoDetailStablePortalPatch
    {
        [HarmonyPrefix]
        internal static void Prefix(out bool __state, CardData __instance)
        {
            __state = __instance == null || string.IsNullOrWhiteSpace(__instance.descriptionOverride);
        }

        [HarmonyPostfix]
        internal static void Postfix(StablePortal __instance, bool __state)
        {
            if (!ModConfig.DetailInfo || !__state || __instance == null)
            {
                return;
            }
            if (string.IsNullOrWhiteSpace(__instance.descriptionOverride)
                || (__instance.MyGameCard != null && __instance.MyGameCard.IsDemoCard))
            {
                return;
            }
            __instance.descriptionOverride = __instance.descriptionOverride + "\n"
                + SokLoc.Translate("label_wave",
                    LocParam.Create("wave", WorldManager.instance.CurrentRunVariables.ForestWave.ToString()));
        }
    }

    /// <summary>
    /// ④ 卡包内容带百分比（迁移 BetterInfo）：接管原版 `BoosterpackData.GetSummary`。
    /// 原版只列名字，这里每张卡都给"发现过的卡名 + 百分比"，未发现的卡合并成一行概率，
    /// 而且**每个 CardBag 一段**（一个卡包可能由多个袋子拼成）。
    /// </summary>
    [HarmonyPatch(typeof(BoosterpackData), "GetSummary")]
    internal static class InfoDetailBoosterpackPatch
    {
        [HarmonyPrefix]
        internal static bool Prefix(BoosterpackData __instance, out string __result)
        {
            __result = "";
            if (!ModConfig.DetailInfo || __instance == null || __instance.CardBags == null)
            {
                return true;
            }
            for (int i = 0; i < __instance.CardBags.Count; i++)
            {
                __result = __result + InfoDetail.GetSummaryFromAllCards(__instance.CardBags[i]) + "\n";
            }
            return false;
        }
    }

    /// <summary>
    /// ⑤ 地点/可采集物的描述里带百分比的掉落表（迁移 BetterInfo，接管 `Harvestable.UpdateDescription`）。
    ///
    /// 四个特例（原作逐个写死的）：
    ///   · **墓穴**：额外多一张"金币杯"（数量 &gt; 1 时概率翻倍）；
    ///   · **洞穴**：宝图同理；
    ///   · **遗迹**：青春之泉蓝图同理；
    ///   · **古书**：列出"还没发现、且不是幽灵/岛屿限定"的创意，一张都没有时退化成"地图"；
    ///   · **钓鱼点**：普通鱼袋和渔夫袋分开列。
    ///
    /// `customNumber` 用 `IsUnlimited ? 0 : Amount`：限量采集物显示真实数量，无限的就不显示 Nx。
    /// </summary>
    [HarmonyPatch(typeof(Harvestable), "UpdateDescription")]
    internal static class InfoDetailHarvestablePatch
    {
        [HarmonyPrefix]
        internal static bool Prefix(Harvestable __instance)
        {
            if (!ModConfig.DetailInfo || __instance == null)
            {
                return true;
            }

            int amount = __instance.IsUnlimited ? 0 : __instance.Amount;
            __instance.descriptionOverride = SokLoc.Translate(__instance.DescriptionTerm,
                LocParam.Create("required_count", __instance.RequiredVillagerCount.ToString())) + "\n\n";

            if (__instance.Id == "catacombs")
            {
                // 墓穴：参照原作 —— 复制一份掉落袋，**再追加**一条金币杯（数量 > 1 时权重 2，否则 0）
                CardBag bag = CopyChancesBag(__instance.MyCardBag);
                bag.Chances.Add(new CardChance("goblet", __instance.Amount > 1 ? 2 : 0));
                __instance.descriptionOverride += InfoDetail.GetSummaryFromAllCards(bag, "label_can_drop", amount) + "\n";
            }
            else if (__instance.Id == "cave")
            {
                // 洞穴：宝图的权重按数量改写（原作是"替换"，不是追加）
                CardBag bag = CopyChancesBag(__instance.MyCardBag);
                bag.Chances = ReplaceChance(bag.Chances, "treasure_map", __instance.Amount > 1 ? 2 : 0);
                __instance.descriptionOverride += InfoDetail.GetSummaryFromAllCards(bag, "label_can_drop", amount) + "\n";
            }
            else if (__instance.Id == "ruins")
            {
                // 遗迹：青春之泉蓝图同理
                CardBag bag = CopyChancesBag(__instance.MyCardBag);
                bag.Chances = ReplaceChance(bag.Chances, "blueprint_fountain_of_youth", __instance.Amount > 1 ? 2 : 0);
                __instance.descriptionOverride += InfoDetail.GetSummaryFromAllCards(bag, "label_can_drop", amount) + "\n";
            }
            else if (__instance.Id == "old_tome")
            {
                __instance.descriptionOverride += InfoDetail.GetSummaryFromAllCards(
                    BuildOldTomeBag(), "label_can_drop", amount) + "\n";
            }
            else
            {
                FishingSpot fishingSpot = __instance as FishingSpot;
                if (fishingSpot != null)
                {
                    __instance.descriptionOverride += InfoDetail.GetSummaryFromAllCards(
                        fishingSpot.NormalCardBag, "label_can_drop", amount) + "\n";
                    __instance.descriptionOverride += InfoDetail.GetSummaryFromAllCards(
                        fishingSpot.FisherCardBag, "card_fisher_name", amount) + "\n";
                }
                else
                {
                    List<CardBag> bags = __instance.GetCardBags();
                    if (bags != null)
                    {
                        for (int i = 0; i < bags.Count; i++)
                        {
                            __instance.descriptionOverride += InfoDetail.GetSummaryFromAllCards(
                                bags[i], "label_can_drop", amount) + "\n";
                        }
                    }
                }
            }
            return false; // 原版那份不带百分比的描述不要了
        }

        /// <summary>复制一个卡包的掉落表（原作在墓穴/洞穴/遗迹三处就是这么干的）。</summary>
        private static CardBag CopyChancesBag(CardBag source)
        {
            CardBag bag = new CardBag();
            bag.CardBagType = CardBagType.Chances;
            bag.CardsInPack = 1;
            bag.Chances = new List<CardChance>();
            if (source != null && source.Chances != null)
            {
                bag.Chances.AddRange(source.Chances);
            }
            return bag;
        }

        /// <summary>把掉落表里某张卡的权重换掉（找不到就原样返回）。</summary>
        private static List<CardChance> ReplaceChance(List<CardChance> chances, string cardId, int chance)
        {
            List<CardChance> result = new List<CardChance>();
            for (int i = 0; i < chances.Count; i++)
            {
                CardChance original = chances[i];
                result.Add(original != null && original.Id == cardId
                    ? new CardChance(cardId, chance)
                    : original);
            }
            return result;
        }

        /// <summary>
        /// 古书能开出的创意：还没发现、不在图鉴里藏起来、也不是幽灵/岛屿限定的
        /// （`CardUpdateType.Spirit` 永远排除；没去过岛时把 `CardUpdateType.Island` 也排除）。
        /// 一张都不剩就退化成"地图"。
        /// </summary>
        private static CardBag BuildOldTomeBag()
        {
            CardBag bag = new CardBag();
            bag.CardBagType = CardBagType.Chances;
            bag.CardsInPack = 1;
            bag.Chances = new List<CardChance>();

            List<CardData> ideas = new List<CardData>();
            List<CardData> prefabs = WorldManager.instance.CardDataPrefabs;
            if (prefabs != null)
            {
                for (int i = 0; i < prefabs.Count; i++)
                {
                    CardData prefab = prefabs[i];
                    if (prefab == null)
                    {
                        continue;
                    }
                    if (prefab.MyCardType != CardType.Ideas || prefab.HideFromCardopedia)
                    {
                        continue;
                    }
                    if (WorldManager.instance.HasFoundCard(prefab.Id))
                    {
                        continue;
                    }
                    if (prefab.CardUpdateType == CardUpdateType.Spirit)
                    {
                        continue;
                    }
                    if (!WorldManager.instance.CurrentRunVariables.VisitedIsland
                        && prefab.CardUpdateType == CardUpdateType.Island)
                    {
                        continue;
                    }
                    ideas.Add(prefab);
                }
            }

            if (ideas.Count == 0)
            {
                bag.Chances.Add(new CardChance("map", 1));
            }
            else
            {
                for (int i = 0; i < ideas.Count; i++)
                {
                    bag.Chances.Add(new CardChance(ideas[i].Id, 1));
                }
            }
            return bag;
        }
    }

    /// <summary>
    /// ⑥ 会掉东西的战斗采集物：描述里补带百分比的掉落表（迁移 BetterInfo）。
    /// 已经有 `descriptionOverride` 的（比如别的地方写过）就不动。
    /// </summary>
    [HarmonyPatch(typeof(CombatableHarvestable), "UpdateCard")]
    internal static class InfoDetailCombatableHarvestablePatch
    {
        [HarmonyPrefix]
        internal static void Prefix(CombatableHarvestable __instance)
        {
            if (!ModConfig.DetailInfo || __instance == null)
            {
                return;
            }
            if (!string.IsNullOrEmpty(__instance.descriptionOverride))
            {
                return;
            }
            int amount = __instance.IsUnlimited ? 0 : __instance.Amount;
            __instance.descriptionOverride = SokLoc.Translate(__instance.DescriptionTerm) + "\n\n";
            List<CardBag> bags = __instance.GetCardBags();
            if (bags == null)
            {
                return;
            }
            for (int i = 0; i < bags.Count; i++)
            {
                __instance.descriptionOverride += InfoDetail.GetSummaryFromAllCards(bags[i], "label_can_drop", amount) + "\n";
            }
        }
    }

    /// <summary>
    /// ⑦ 旅行货车：描述里补带百分比的掉落表（迁移 BetterInfo）。
    /// 和⑥的区别是**不显示数量**（原作用 `customNumber = 0`，货车是无限的）。
    /// </summary>
    [HarmonyPatch(typeof(TravellingCart), "UpdateCard")]
    internal static class InfoDetailTravellingCartPatch
    {
        [HarmonyPrefix]
        internal static void Prefix(TravellingCart __instance)
        {
            if (!ModConfig.DetailInfo || __instance == null)
            {
                return;
            }
            if (!string.IsNullOrEmpty(__instance.descriptionOverride))
            {
                return;
            }
            __instance.descriptionOverride = SokLoc.Translate(__instance.DescriptionTerm) + "\n\n";
            List<CardBag> bags = __instance.GetCardBags();
            if (bags == null)
            {
                return;
            }
            for (int i = 0; i < bags.Count; i++)
            {
                __instance.descriptionOverride += InfoDetail.GetSummaryFromAllCards(bags[i], "label_can_drop", 0) + "\n";
            }
        }
    }

    /// <summary>
    /// ⑧ 图鉴的掉落摘要：可采集物 / 战斗采集物 / Mob 的掉落**已经写进描述里了**
    /// （⑤⑥①），所以图鉴自己那份就别再重复一次（迁移 BetterInfo）。
    /// </summary>
    [HarmonyPatch(typeof(CardopediaScreen), "GetDropSummaryFromCard")]
    internal static class InfoDetailCardopediaPatch
    {
        [HarmonyPrefix]
        internal static bool Prefix(CardData cardData, out string __result)
        {
            __result = "";
            if (!ModConfig.DetailInfo)
            {
                return true;
            }
            return !(cardData is Harvestable)
                && !(cardData is CombatableHarvestable)
                && !(cardData is Mob);
        }
    }

    /// <summary>
    /// ⑨ 装备详解最前面补上攻击类型（近战/远程/魔法…）（迁移 BetterInfo）。
    /// </summary>
    [HarmonyPatch(typeof(Equipable), "GetAdvancedEquipableInfo")]
    internal static class InfoDetailEquipablePatch
    {
        [HarmonyPostfix]
        internal static void Postfix(Equipable __instance, ref string __result)
        {
            if (!ModConfig.DetailInfo || __instance == null || __instance.AttackType == AttackType.None)
            {
                return;
            }
            __result = __instance.AttackType + "\n" + __result;
        }
    }

    /// <summary>
    /// ⑩ 信息栏三件事（迁移 BetterInfo，挂在 `GameScreen.Update` 后置）：
    ///
    /// 1. **鼠标指着卡包**（没在指卡）→ 信息栏追加这个卡包的内容（带百分比）；
    /// 2. **悬停的卡属于某个牌堆**（`IsPartOfStack`）→ 追加"这张牌自己的名字 + 描述"，
    ///    并把描述里的字号标签和 `\d` 折行标记清掉（否则信息栏里字号会乱）；
    /// 3. **在岛上时**把侧栏金钱从"金币"改成"贝壳 + 金币"（贝壳数见 `InfoDetail.GetShellCount`）。
    /// </summary>
    [HarmonyPatch(typeof(GameScreen), "Update")]
    internal static class InfoDetailInfoTextPatch
    {
        [HarmonyPostfix]
        internal static void Postfix(GameScreen __instance)
        {
            if (!ModConfig.DetailInfo || __instance == null || WorldManager.instance == null)
            {
                return;
            }

            GameCard card = WorldManager.instance.DraggingCard;
            if (card == null)
            {
                card = WorldManager.instance.HoveredCard;
            }

            if (card == null && __instance.InfoText != null)
            {
                Boosterpack pack = WorldManager.instance.DraggingDraggable as Boosterpack;
                if (pack == null)
                {
                    pack = WorldManager.instance.HoveredDraggable as Boosterpack;
                }
                if (pack != null && pack.PackData != null)
                {
                    __instance.InfoText.text = __instance.InfoText.text + "\n\n" + pack.PackData.GetSummary();
                }
            }

            if (card != null && card.CardData != null && card.IsPartOfStack() && __instance.InfoText != null)
            {
                string description = card.CardData.Description ?? "";
                description = Regex.Replace(description, "<(/|)size(=.*?%|)>", "");
                description = description.Replace("\\d", "\n\n");
                string suffix = "\n<u>_    __   _       _     _ _          _</u>\n\n"
                    + card.CardData.FullName
                    + "\n<size=90%>" + description + "</size>";
                // 0.5.19 防重复追加：原版大多数分支每帧都会重写 InfoText（我们的追加跟着每帧来一次，
                // 没问题），但悬停的卡若走了"不重写"的分支，InfoText 会保留上一帧的内容——
                // 这里再不判重，追加块就会每帧叠一层、文本无限增长（表现＝信息栏闪个不停）。
                if (!__instance.InfoText.text.EndsWith(suffix))
                {
                    __instance.InfoText.text += suffix;
                }
            }

            // 岛上：侧栏金钱显示成"贝壳 + 金币"（原版只显示金币）
            GameBoard board = WorldManager.instance.CurrentBoard;
            if (board != null && board.Id == "island" && __instance.MoneyText != null)
            {
                __instance.MoneyText.text = InfoDetail.GetShellCount() + " " + Icons.Shell
                    + "  " + WorldManager.instance.GetGoldCount(true) + " " + Icons.Gold;
            }
        }
    }

    /// <summary>
    /// ⑪ 蓝图（创意）上补产物的信息（迁移 BetterInfo）：如果产物是装备，
    /// 追加特殊效果说明与装备详解（含攻击类型，见⑨）。
    ///
    /// 原作用 `Subprints[0]`；这里加了一个"没有子配方就跳过"的保护（原作会直接越界崩）。
    /// </summary>
    [HarmonyPatch(typeof(Blueprint), "GetText")]
    internal static class InfoDetailBlueprintPatch
    {
        [HarmonyPostfix]
        internal static void Postfix(Blueprint __instance, ref string __result)
        {
            if (!ModConfig.DetailInfo || __instance == null || __instance.Subprints == null
                || __instance.Subprints.Count == 0)
            {
                return;
            }
            Subprint subprint = __instance.Subprints[0];
            if (subprint == null)
            {
                return;
            }
            string resultCard = subprint.ResultCard;
            if (string.IsNullOrEmpty(resultCard) && subprint.ExtraResultCards != null
                && subprint.ExtraResultCards.Length != 0)
            {
                resultCard = subprint.ExtraResultCards[0];
            }
            if (string.IsNullOrEmpty(resultCard))
            {
                return;
            }

            CardData prefab = WorldManager.instance.GetCardPrefab(resultCard, true);
            if (prefab == null)
            {
                return;
            }
            prefab.UpdateCardText();
            Equipable equip = prefab as Equipable;
            if (equip != null && equip.MyStats != null)
            {
                __result = __result + "\n\n<size=90%>" + equip.MyStats.SummarizeSpecialHits()
                    + "\n\n" + equip.GetEquipableInfoAdvanced();
            }
        }
    }

    /// <summary>
    /// ⑫ 把原版"详细战斗数值"选项打开（迁移 BetterInfo 的 `AdvancedSettingsScreen.AdvancedCombatStatsEnabled = true`）。
    ///
    /// ⚠️ **这个选项是"战斗详解"的总闸门**（不是可选项）：`Combatable.UpdateCardText()` 与
    /// `BaseVillager.UpdateCardText()` 里都是
    /// `if (AdvancedSettingsScreen.AdvancedCombatStatsEnabled || 当前界面是图鉴) 才追加 GetCombatableDescriptionAdvanced()`。
    /// 也就是说它关着的时候，① 那张卡的**原版四项数值**和 ② 我们 ① 号补丁加的详解 **都不会算、不会显示**。
    ///
    /// 它的值来自 `PlayerPrefs`（键名同名，本机**根本没存过这个键 → 默认 0＝关**），
    /// 而**读取它的那行代码在 `AdvancedSettingsScreen.Awake()` 里**，
    /// 那个界面是**玩家点进 Options → Advanced 才懒创建的**（本次实测：注册表里从来没有这个键 →
    /// 说明那个界面在本机从没被创建过）。所以：
    ///
    /// | 只写在哪边 | 后果 |
    /// | --- | --- |
    /// | 只写 `Mod.Ready()`（**原 mod 的做法**） | 能用；但玩家一旦打开那个设置页，它会按 `PlayerPrefs` 把闸门**关回去** |
    /// | 只挂那个界面 `Awake` 后置（我第一版改成的写法） | **完全失效**——玩家不打开那个设置页，后置永远不会跑（本次用户报"村民攻速/伤害不显示"就是这么来的） |
    /// | **现在：`Mod.Ready()` 兜底 + 挂它的 `LoadSettings()`（读 `PlayerPrefs` 的那个方法）后置** | 两个时机都覆盖：启动时开一次；万一玩家真打开了设置页，读完 pref 立刻再开一次 |
    ///
    /// 关掉「详细信息」总开关就完全不管这个选项（回到玩家自己的设置）。
    /// 玩家如果**在设置里手动把原版这项关掉**，本会话内详解就会消失（重启即恢复）——
    /// 这是有意的：原版那个开关本来就是这个功能的开关，我们只在"启动 + 读设置"两个时机强制打开。
    /// </summary>
    [HarmonyPatch(typeof(AdvancedSettingsScreen), "LoadSettings")]
    internal static class InfoDetailAdvancedCombatPatch
    {
        [HarmonyPostfix]
        internal static void Postfix()
        {
            if (ModConfig.DetailInfo)
            {
                AdvancedSettingsScreen.AdvancedCombatStatsEnabled = true;
            }
        }
    }
}
