using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace StacklandsProMaxNS
{
    /// <summary>
    /// **K+O+F1 调试面板（DebugScreen）汉化**（0.5.9）：面板大部分文字是英文，本补丁把它们补成中文。
    ///
    /// ── 面板上的文字有两种来源（对当前游戏 DLL 逐一核实过） ────────────────────
    ///
    /// ① **走本地化系统的**：按钮上挂着 SokLoc 的 `LocSetter` 组件，运行时
    ///    `SokLoc.Translate(Term)` 填文字。这些词条（`label_debug_general`、`label_debug_cards`、
    ///    `label_debug_end_moon`、各快捷键提示等 22 个，另加 5 个代码每帧刷新的开关词条和
    ///    `label_on`/`label_off`）**官方中文列没有翻译**——词条存在但 zh 列为空时
    ///    `SokLoc.Translate` 会回退英文（`SokTerm.GetText()` 返回空串 → `Translate` 判空白 → 取英文兜底表），
    ///    这就是面板显示英文的原因。**mod 的 localization.tsv 理论上也能补**（`ModManager.ReadyUpMods`
    ///    会把 mod 词条合并进当前语言词表，重复词条"以后写的为准"），但每次覆盖原版词条都会在日志里
    ///    打一条 `LogError("Term ... found more than once")`（ReadyUpMods 那次调用没关警告），
    ///    24 个词条就是 24 条报错——所以这里改成**代码注入**：在 `SokLoc.CurrentLocSet` getter 的后置里，
    ///    当前语言是简体中文且词表里还没有这些词条时，把它们直接塞进 `TermLookup`（静默、幂等）。
    ///    挂 getter 而不是挂一次事件，是因为语言切换时词表会整个重建（`CurrentLocSet` 的 getter 里
    ///    直接 new 一个 `LoadedLocSet` 顶掉旧的），挂 getter 能保证"谁取词表谁就能拿到注入的词条"，
    ///    不存在先后顺序问题。
    ///
    /// ② **不走本地化的硬编码英文**：没有 `LocSetter`、TMP 文本就是写死的——页签
    ///    「Saves / Presets / Cutscenes」、存档区的「Save Game / Open Saves Directory / Clear Save /
    ///    Make Debug Save」、预设区的「Open Preset Directory / Make Board Preset / Select Save」、
    ///    搜索框占位符「Search」等。这些只能改文本：`DebugScreen.Start` 的后置遍历面板下所有
    ///    `TMP_Text`，凡是**同一物体上没有 `LocSetter`** 的，按英文原文查表替换成中文
    ///    （有 `LocSetter` 的绝不碰——那是词条①的事，重复改会在语言切换时打架）。
    ///
    /// ── 注意 ─────────────────────────────────────────────────────────────────
    /// - **只对简体中文生效**（`CurrentLanguage == "Chinese (Simplified)"`）：英文玩家完全无感；
    ///   繁中也先不注入（我们只有简体文案，繁体字体表可能缺简体字形，会显示成方块）。
    /// - 语言切换：存了每个被改文本的原文，切回非中文语言时**逐个还原**，切回中文再重新替换
    ///   （挂 `SokLoc.LanguageChanged` 事件）。
    /// - Cards 页签里的卡牌按钮、Effects 页签的状态效果按钮用的是卡名/效果名（官方已翻译），不用管；
    ///   Cutscenes 页签列的是过场动画 id（如 `death_middle_villager`），保持原样。
    /// - 这是给玩家开 K+O+F1 调试面板用的体验优化，**没有开关**（常开，和"信息栏自动加高"同类）。
    /// </summary>
    [HarmonyPatch(typeof(SokLoc), "get_CurrentLocSet")]
    internal static class DebugScreenTermInjectionPatch
    {
        /// <summary>只处理简体中文（理由见类注释）。</summary>
        internal const string ChineseSimplified = "Chinese (Simplified)";

        /// <summary>
        /// ⚠️ 不能用"词表里有没有某词条"当哨兵——`label_debug_*` 在**原版词表里本来就存在**
        /// （英文文本、中文列为空），一查就误判成"已注入"。改成按 <see cref="LoadedLocSet"/> **实例**记：
        /// 每张词表只注入一次；语言切换时 getter 会重建词表（新实例）→ 自然重新注入。
        /// </summary>
        private static readonly HashSet<LoadedLocSet> InjectedSets = new HashSet<LoadedLocSet>();

        /// <summary>
        /// 面板上挂 LocSetter 的词条 → 中文。前 22 个是从游戏资产里枚举出的 `label_debug_*` 全集；
        /// 最后 7 个是 `DebugScreen.Update()` 每帧用 `SokLoc.Translate` 刷新的 5 个开关词条 + 开/关。
        /// 占位符是方括号格式（`LocCache.FillIn` 替换 `[on_off]`）。
        /// </summary>
        internal static readonly Dictionary<string, string> TermZh = new Dictionary<string, string>
        {
            { "label_debug_general", "通用" },
            { "label_debug_cards", "卡牌" },
            { "label_debug_effects", "状态效果" },
            { "label_debug_shortcuts", "快捷键" },
            { "label_debug_end_moon", "结束当前月份" },
            { "label_debug_reset_run", "重置本局变量" },
            { "label_debug_spawn_enemies", "生成敌人" },
            { "label_debug_scenario_demon", "生成恶魔" },
            { "label_debug_scenario_demon_lord", "生成恶魔领主" },
            { "label_debug_scenario_kraken", "生成克拉肯" },
            { "label_debug_scenario_island", "前往小岛" },
            { "label_debug_scenario_witch_forest", "女巫森林" },
            { "label_debug_coin_chest", "生成装满金币的硬币箱" },
            { "label_debug_unlock_ideas", "解锁全部主意" },
            { "label_debug_unlock_quests", "解锁全部任务" },
            { "label_debug_unlock_boosters", "解锁全部卡包" },
            { "label_debug_start_cities_run", "开始城市新局" },
            { "label_debug_shortcut_copy", "悬停 + C：复制卡牌" },
            { "label_debug_shortcut_delete", "悬停 + R：移除/击杀卡牌" },
            { "label_debug_shortcut_heal", "悬停 + H：治疗卡牌" },
            { "label_debug_shortcut_status", "悬停 + U：移除状态效果" },
            { "label_debug_shortcut_progress", "悬停 + Z：立即完成进度" },
            { "label_debug_endless_moon", "无尽之月：[on_off]" },
            { "label_debug_toggle_peaceful_mode", "和平模式：[on_off]" },
            { "label_debug_toggle_no_food", "无需食物：[on_off]" },
            { "label_debug_need_villagers", "需要村民：[on_off]" },
            { "label_debug_no_energy", "无需能量：[on_off]" },
            { "label_on", "开" },
            { "label_off", "关" },
        };

        [HarmonyPostfix]
        internal static void Postfix(ref LoadedLocSet __result)
        {
            try
            {
                if (__result == null || __result.TermLookup == null)
                {
                    return;
                }
                if (__result.Language != ChineseSimplified || InjectedSets.Contains(__result))
                {
                    return;
                }
                InjectedSets.Add(__result);
                foreach (KeyValuePair<string, string> kv in TermZh)
                {
                    SokTerm term = new SokTerm(__result, kv.Key, kv.Value);
                    // LoadedLocSet 装载时把词条 id 统一转小写、GetTerm 查询时也会转小写，这里保持一致。
                    // 这些词条在原版词表里已存在（英文），直接覆盖；AllTerms 里的旧条目也一并换掉。
                    __result.TermLookup[kv.Key.ToLowerInvariant()] = term;
                    __result.AllTerms.RemoveAll(delegate (SokTerm x) { return x != null && x.Id == kv.Key; });
                    __result.AllTerms.Add(term);
                }
            }
            catch (System.Exception)
            {
                // 词表注入失败只影响这个调试面板的显示，绝不能影响游戏正常取词
            }
        }
    }

    /// <summary>
    /// 调试面板上**没有 LocSetter 的硬编码英文** → 中文（见 <see cref="DebugScreenTermInjectionPatch"/> 类注释②）。
    /// </summary>
    [HarmonyPatch(typeof(DebugScreen), "Start")]
    internal static class DebugScreenRawTextPatch
    {
        /// <summary>硬编码英文 → 中文（在 DebugScreen 子树内精确匹配整段文本才替换）。</summary>
        internal static readonly Dictionary<string, string> RawTextZh = new Dictionary<string, string>
        {
            // 页签（这三个没有对应的 label_debug_* 词条，是写死的）
            { "Saves", "存档" },
            { "Presets", "预设" },
            { "Cutscenes", "过场动画" },
            // 存档页
            { "Save Games", "存档" },
            { "Open Saves Directory", "打开存档文件夹" },
            { "Clear Save", "清空存档" },
            { "Make Debug Save", "生成调试存档" },
            { "Select Save", "选择存档" },
            { "Right click to archive save", "右键点击归档存档" },
            // 预设页
            { "Open Preset Directory", "打开预设文件夹" },
            { "Make Board Preset", "生成场地预设" },
            { "Right click to load preset", "右键点击载入预设" },
            { "Waiting for input..", "等待输入…" },
            // 其它
            { "Search", "搜索" },
            { "Actions", "操作" },
            { "Status effects", "状态效果" },
            // ↓ 这些按理由词条①负责（面板上有 LocSetter），但兜底放一份：万一哪个按钮没挂 LocSetter，
            //   原文精确匹配才会替换，多放不会误伤
            { "General", "通用" },
            { "Cards", "卡牌" },
            { "Effects", "状态效果" },
            { "Shortcuts", "快捷键" },
            { "End current moon", "结束当前月份" },
            { "To Next Moon", "跳到下个月" },
            { "Reset Run vars", "重置本局变量" },
            { "Spawn enemies", "生成敌人" },
            { "Spawn demon", "生成恶魔" },
            { "Spawn demon lord", "生成恶魔领主" },
            { "Spawn kraken", "生成克拉肯" },
            { "Witch forest", "女巫森林" },
            { "Go to Island", "前往小岛" },
            { "Spawn full coin chest", "生成装满金币的硬币箱" },
            { "Unlock all ideas", "解锁全部主意" },
            { "Unlock base game ideas", "解锁原版主意" },
            { "Unlock all quests", "解锁全部任务" },
            { "Unlock all booster packs", "解锁全部卡包" },
            { "Start Cities Run", "开始城市新局" },
        };

        /// <summary>被替换过的文本（组件 → 原文），切回非中文语言时逐个还原。</summary>
        private static readonly Dictionary<TMP_Text, string> OriginalTexts = new Dictionary<TMP_Text, string>();

        private static bool languageHooked;

        [HarmonyPostfix]
        internal static void Postfix(DebugScreen __instance)
        {
            try
            {
                if (__instance == null || SokLoc.instance == null)
                {
                    return;
                }
                HookLanguageChangedOnce();
                ApplyOrRestore(__instance);
            }
            catch (System.Exception)
            {
                // 调试面板汉化失败不影响游戏
            }
        }

        private static void HookLanguageChangedOnce()
        {
            if (languageHooked)
            {
                return;
            }
            languageHooked = true;
            SokLoc.instance.LanguageChanged += OnLanguageChanged;
        }

        private static void OnLanguageChanged()
        {
            try
            {
                if (DebugScreen.instance != null)
                {
                    ApplyOrRestore(DebugScreen.instance);
                }
            }
            catch (System.Exception)
            {
            }
        }

        /// <summary>中文 → 把硬编码英文换成中文；非中文 → 把之前换过的还原成英文原文。</summary>
        private static void ApplyOrRestore(DebugScreen screen)
        {
            bool chinese = SokLoc.instance != null && SokLoc.instance.CurrentLanguage == DebugScreenTermInjectionPatch.ChineseSimplified;
            if (!chinese)
            {
                foreach (KeyValuePair<TMP_Text, string> kv in OriginalTexts)
                {
                    // Unity 假 null：物体已销毁就跳过（字典条目留给 GC）
                    if (kv.Key != null)
                    {
                        kv.Key.text = kv.Value;
                    }
                }
                OriginalTexts.Clear();
                return;
            }

            TMP_Text[] texts = screen.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text text = texts[i];
                if (text == null || string.IsNullOrEmpty(text.text))
                {
                    continue;
                }
                // 挂了 LocSetter 的归词条注入管（语言切换时 LocSetter 会自己重刷），这里绝不碰
                if (text.GetComponent<LocSetter>() != null)
                {
                    continue;
                }
                string zh;
                if (!RawTextZh.TryGetValue(text.text, out zh))
                {
                    continue;
                }
                if (!OriginalTexts.ContainsKey(text))
                {
                    OriginalTexts[text] = text.text;
                }
                text.text = zh;
            }
        }
    }
}
