using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace StacklandsProMaxNS
{
    /// <summary>搜索模式：这次搜索要匹配主意的哪个字段。</summary>
    internal enum IdeaSearchMode
    {
        /// <summary>没被我们的手势设置过 → 走原版（原版只匹配**名字**）。</summary>
        Vanilla,

        /// <summary>匹配主意的**标题**（原版的字段）。</summary>
        Title,

        /// <summary>匹配主意的**描述**。</summary>
        Description,
    }

    /// <summary>
    /// 主意搜索（迁移并重新设计自创意工坊 **Extra Search Mod** `3013114897` / id `extra_search_mod`）。
    ///
    /// 用户需求原话（0.5.5）：「迁移并重新设计已经下载的 Extra Search Mod 的功能，
    /// **alt+左键单击**：在左侧的主意中搜索并展示有这个卡牌名称的主意，也就是原 mod 的 Title Search。
    /// **alt+右键单击**：在左侧的主意中搜索并展示描述中有这个卡牌名称的主意，也就是原 mod 的 Description Search」。
    ///
    /// ── 原版机制（反编译核实） ────────────────────────────────────────────────
    ///
    /// `GameScreen.UpdateIdeasLog()` 用 **私有** 方法 `KnowledgeMatchesSearch(IKnowledge, string)` 过滤左侧主意列表：
    ///
    /// 它只比 `KnowledgeName`（= `SokLoc.Translate(NameTerm)`，即标题，**不看描述**）：
    /// 两边都转小写、删掉所有空格，再看标题里是否包含搜索词。
    ///
    /// 搜索框本身是 `GameScreen.instance.IdeaSearchField`（TMP 输入框），改它的 `.text` + 调一次 `UpdateIdeasLog()` 就是"搜索"。
    ///
    /// ── 参考 mod 的做法与本项目的重新设计 ──────────────────────────────────────
    ///
    /// 参考 mod：① Prefix 那个私有方法，按一个全局"模式"变量决定比 `KnowledgeName` 还是 `KnowledgeText`；
    /// ② 在 `WorldManager.Update` 后置里，"抓住卡片/按住右键"就把搜索框填成那张卡的名字；
    /// ③ 在 `GameScreen.Awake` 后置里**用代码新建两个按钮**（Title/Description 切换 + Clear）挂到界面上。
    ///
    /// 本项目的重新设计（用户 0.5.5 两次要求，以第二次为准）：
    ///
    /// | 交互 | 效果 |
    /// | --- | --- |
    /// | **Alt + 左键单击** 一张卡 | 搜索框填该卡名字、模式＝**标题**（Title Search） |
    /// | **Alt + 右键单击** 一张卡 | 搜索框填该卡名字、模式＝**描述**（Description Search） |
    /// | **Alt + 单击空白处** | 清空搜索框（回到完整主意列表） |
    /// | 当前侧边栏停在**任务**页 | 自动跳到**主意**页（否则搜了也看不见 —— 用户 0.5.6 要求） |
    /// | 侧边栏**三个固定按钮**「搜标题 / 搜描述 / 清除」 | 手动切模式 / 清空（不是参考 mod 那种"一个按钮显示当前模式"，用户要求固定三个标签） |
    ///
    /// 用户原话（第二次）：「原 mod 的侧边栏中只有两个标签，第一个根据情况展示 title 或 description，
    /// 第二个是 clear。改成固定三个标签：搜标题，搜描述，清除。同时鼠标放在标签上后展示的悬浮窗直接去掉」。
    /// 所以我们的按钮是**三个固定标签**，并且**不设 `TooltipText`**（悬停不弹提示）。
    ///
    /// 三个补丁各管一段：`IdeaSearchInputPatch`（Alt 手势 → 填词 + 选模式）、
    /// `IdeaSearchButtonsPatch`（建那三个按钮）、`IdeaSearchKnowledgePatch`（模式 → 过滤规则）、
    /// 两个 Click 前缀（Alt 按下时别让卡片/卡包执行自己的点击）。
    ///
    /// ⚠️ 刻意**不新建任何 UI 对象**：参考 mod 那套按钮要 `Instantiate` 原版按钮、手动算布局，
    /// 既容易随游戏版本失效，也违背本项目"能在游戏内开关就别加界面元素"的取向。
    /// </summary>
    internal static class IdeaSearch
    {
        internal static IdeaSearchMode Mode = IdeaSearchMode.Vanilla;

        /// <summary>那三个侧边栏按钮是不是已经建好了（`GameScreen` 只会 Awake 一次，这里防重复建）。</summary>
        private static bool buttonsCreated;

        /// <summary>我们那排按钮的容器（用来在清理时把"自己"排除掉）。</summary>
        private static Transform ourContainer;

        /// <summary>开局这段时间内定期扫一遍、摘掉参考 mod 那排按钮（它可能比我们晚建出来）。</summary>
        private static float cleanupUntil = -1f;

        /// <summary>Alt 是否按着（用原始输入读，不走 `InputController` —— 免得被别的 mod 的 GetKey 补丁影响）。</summary>
        internal static bool AltDown()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return false;
            }
            return keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed;
        }

        /// <summary>
        /// Alt 手势生效中吗？（功能开着 + Alt 按着）
        /// 给"别让卡片自己响应这次点击"的两个前缀、以及右键拖动补丁用。
        /// </summary>
        internal static bool GestureActive
        {
            get { return ModConfig.IdeaSearch && AltDown(); }
        }

        /// <summary>
        /// 把"搜索词 + 模式"落到游戏界面上：填 `IdeaSearchField.text` 再让 `UpdateIdeasLog()` 重新过滤。
        /// 传空字符串就是清空搜索。
        ///
        /// 落之前先保证**主意页是显示着的**：见 <see cref="EnsureIdeasTabVisible"/>。
        /// </summary>
        internal static void Apply(string searchTerm, IdeaSearchMode mode)
        {
            GameScreen screen = GameScreen.instance;
            if (screen == null || screen.IdeaSearchField == null)
            {
                return;
            }
            EnsureIdeasTabVisible(screen);
            Mode = mode;
            screen.IdeaSearchField.text = searchTerm ?? "";
            screen.UpdateIdeasLog();
        }

        /// <summary>原版 `GameScreen.questTabOpen`（私有字段：true＝侧边栏显示任务页）。</summary>
        private static System.Reflection.FieldInfo questTabOpenField;

        /// <summary>
        /// 侧边栏现在停在**任务**页时，切到**主意**页。
        ///
        /// 用户 0.5.6 原话：「alt点击功能，如果侧边栏展示的是任务页面，则点击后自动跳转到主意页面」。
        /// 搜索框和主意列表都在主意页里，停在任务页时搜索是"搜了但看不见"。
        ///
        /// 做法完全照原版 `IdeasButton.Clicked` 那三步（反编译核实）：
        /// `questTabOpen = false; QuestsTab.SetActive(false); IdeasTab.SetActive(true);`
        /// `questTabOpen` 是私有字段，所以走 `AccessTools.Field` 拿一次缓存住。
        /// **必须**把它设成 false —— 原版 `SetMinimize(false)` 会按这个字段决定显示哪页，
        /// 只切 `SetActive` 的话"最小化再展开"会跳回任务页。
        ///
        /// 侧边栏被最小化时两页都是 `activeInHierarchy == false`，这里照样切（切完还是看不见），
        /// 展开时原版按 `questTabOpen=false` 恢复成主意页 —— 正是我们想要的结果。
        /// </summary>
        private static void EnsureIdeasTabVisible(GameScreen screen)
        {
            if (screen.IdeasTab == null || screen.QuestsTab == null)
            {
                return;
            }
            if (screen.IdeasTab.gameObject.activeInHierarchy)
            {
                return; // 已经显示着主意页，什么都不用做
            }
            if (questTabOpenField == null)
            {
                questTabOpenField = AccessTools.Field(typeof(GameScreen), "questTabOpen");
            }
            if (questTabOpenField != null)
            {
                questTabOpenField.SetValue(screen, false);
            }
            screen.QuestsTab.gameObject.SetActive(false);
            screen.IdeasTab.gameObject.SetActive(true);
        }

        /// <summary>
        /// 拿一张卡去搜主意时用的**搜索词**：用卡牌的"类型名"（`NameTerm` 翻出来的名字），
        /// **不用当前显示名**。
        ///
        /// 为什么（用户 0.5.5 原话）：「点击后取到的卡片名中，对资源箱和磁力箱要特殊处理，
        /// 比如"石头箱"要改成"资源箱"，"石头磁力箱"改成"磁力箱"」——装了东西的箱子会把显示名
        /// 改成 `nameOverride`（「石头箱」/「石头磁力箱」），而主意/蓝图里写的是通用名
        /// （资源箱 / 磁力箱），拿显示名去搜什么也搜不到。
        /// `CardData.Name` 就是 `nameOverride ?? SokLoc.Translate(NameTerm)`，所以这里绕过 override 直接翻 term。
        /// </summary>
        internal static string SearchableName(CardData card)
        {
            if (card == null)
            {
                return null;
            }
            if (!string.IsNullOrEmpty(card.NameTerm))
            {
                string termName = SokLoc.Translate(card.NameTerm);
                // 认不出这个 term 时 SokLoc 会给个 MISSING 标记，那就老老实实用显示名
                if (!string.IsNullOrEmpty(termName) && termName.IndexOf("MISSING", System.StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return termName;
                }
            }
            return card.Name;
        }

        /// <summary>
        /// 侧边栏按钮用：切到某个模式再重新过滤一次。
        /// 搜索框**空着**的时候顺手把鼠标下那张卡的名字填进去——否则点了按钮看不出任何变化。
        /// </summary>
        internal static void SwitchMode(IdeaSearchMode mode)
        {
            GameScreen screen = GameScreen.instance;
            string term = screen != null && screen.IdeaSearchField != null ? screen.IdeaSearchField.text : "";
            if (string.IsNullOrEmpty(term))
            {
                WorldManager world = WorldManager.instance;
                GameCard hovered = world == null ? null : world.HoveredCard;
                if (hovered != null)
                {
                    term = SearchableName(hovered.CardData);
                }
            }
            Apply(term ?? "", mode);
        }

        /// <summary>
        /// 建那三个固定按钮（幂等），并**顺手清掉别的 mod 在我这排旁边的同类按钮**。
        /// 任何异常都吞掉：按钮只是便利，绝不能拖垮 `GameScreen.Awake`。
        /// </summary>
        internal static void EnsureButtons()
        {
            if (!buttonsCreated && ModConfig.IdeaSearch)
            {
                if (TryCreateButtons())
                {
                    buttonsCreated = true;
                    cleanupUntil = Time.time + 30f; // 参考 mod 的按钮可能在我们之后才建出来，留一段清理窗口
                }
            }
            // 只在开局那 30 秒里、每 30 帧扫一次：把 Extra Search Mod 自己那两个按钮摘掉
            if (buttonsCreated && cleanupUntil > 0f && Time.time <= cleanupUntil && Time.frameCount % 30 == 0)
            {
                RemoveForeignSearchButtons();
            }
        }

        private static bool TryCreateButtons()
        {
            GameScreen screen = GameScreen.instance;
            if (screen == null || screen.IdeaSearchField == null || screen.IdeasButton == null
                || screen.IdeaSearchField.transform.parent == null
                || screen.IdeaSearchField.transform.parent.parent == null)
            {
                return false;
            }

            try
            {
                GameObject container = new GameObject("StacklandsProMaxIdeaSearchButtons");
                HorizontalLayoutGroup layout = container.AddComponent<HorizontalLayoutGroup>();
                layout.childForceExpandHeight = false;
                layout.childForceExpandWidth = false;
                layout.childControlHeight = true;
                layout.childControlWidth = true;
                layout.spacing = 6f;
                container.transform.SetParent(screen.IdeaSearchField.transform.parent.parent);
                container.transform.SetSiblingIndex(1);

                // ⚠️ 三个按钮**排一行**、宽度按标签文字算（见 AddButton）。
                // 宽度上限的实测边界（用户多次截图反馈）：
                //   · 376（写死 130/130/100）→ 面板被撑宽；
                //   · 380~400（本版：默认字号 + 自适应 + 留白 8）→ **正常**，只是右边留白略紧；
                //   · 428（留白加到 32）→ 又把面板撑宽。
                // 所以本版是"稳"的那一档；想再宽一点只能小幅试探，或者改成 2 列网格（每行 ≤2 格）。
                AddButton(container.transform, screen.IdeasButton, "stacklandspro_idea_search_title",
                    delegate { SwitchMode(IdeaSearchMode.Title); });
                AddButton(container.transform, screen.IdeasButton, "stacklandspro_idea_search_description",
                    delegate { SwitchMode(IdeaSearchMode.Description); });
                AddButton(container.transform, screen.IdeasButton, "stacklandspro_idea_search_clear",
                    delegate { Apply("", IdeaSearchMode.Vanilla); });

                ourContainer = container.transform;
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[StacklandsProMax] 主意搜索按钮没建出来（不影响 Alt+点击搜索）: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 删掉**参考 mod（Extra Search Mod）自己那排按钮**。
        ///
        /// 为什么由我们来删：用户原话「旧的两个标签还在，要删掉」——那个 mod 和本项目功能完全重复，
        /// 两排按钮叠在一起既占地方又把面板撑宽（见 `TryCreateButtons` 的注释）。
        /// 我们没法改别人的 mod，但它的按钮就挂在我们同一个父节点下，且特征很明显：
        /// **一个容器、正好两个 `CustomButton`、文字是写死的英文 `Title`/`Description`/`Clear`**。
        /// 按这三条命中的才删；认不出来就什么都不做（宁可不删也不误删原版 UI）。
        /// </summary>
        private static void RemoveForeignSearchButtons()
        {
            if (ourContainer == null || ourContainer.parent == null)
            {
                return;
            }
            Transform parent = ourContainer.parent;
            try
            {
                for (int i = parent.childCount - 1; i >= 0; i--)
                {
                    Transform child = parent.GetChild(i);
                    if (child == ourContainer)
                    {
                        continue;
                    }
                    CustomButton[] buttons = child.GetComponentsInChildren<CustomButton>(true);
                    if (buttons == null || buttons.Length != 2)
                    {
                        continue; // 参考 mod 那排正好两个按钮
                    }

                    bool looksLikeExtraSearch = false;
                    for (int b = 0; b < buttons.Length; b++)
                    {
                        TMP_Text label = buttons[b].TextMeshPro;
                        string text = label == null ? "" : label.text;
                        if (text == "Title" || text == "Description" || text == "Clear")
                        {
                            looksLikeExtraSearch = true;
                        }
                    }
                    if (looksLikeExtraSearch)
                    {
                        Debug.Log("[StacklandsProMax] 摘掉 Extra Search Mod 自带的一排搜索按钮（本项目的三个按钮已取代它们）");
                        UnityEngine.Object.Destroy(child.gameObject);
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[StacklandsProMax] 清理 Extra Search Mod 按钮失败（不影响本项目功能）: " + ex.Message);
            }
        }

        /// <summary>
        /// 按钮左右额外留白（像素）。`TMP_Text.preferredWidth + margin` 只够"刚好放下字"，这里再宽一点。
        /// ⚠️ 用户定案：**就用 8**。加到 32 时三个按钮合计 ≈428，会把左侧面板撑宽（用户截图 `ui错乱5`），
        /// 所以宁可右边留白略紧也不撑宽面板（用户原话「改回我让你加大留白之前的那个版本」）。
        /// </summary>
        private const float ButtonPadding = 8f;

        /// <summary>单个按钮的最小宽度（模板原版按钮量级）。</summary>
        private const float ButtonMinWidth = 96f;

        /// <summary>按钮高度（和原版 `IdeasButton` 一个量级）。</summary>
        private const float ButtonHeight = 50f;

        /// <summary>
        /// 造一个按钮：宽度＝`preferredWidth` + 左右 `margin` + `ButtonPadding`，并不低于 `ButtonMinWidth`。
        ///
        /// 字号**用模板自己的**（不缩字号——缩到 20 时用户说"确实文字太小了"）；
        /// 关掉自动折行（折行会让文字跑到按钮框外，用户截图反馈过"文字都挤出来了"）。
        ///
        /// 宽度口径的来龙去脉：早先我拿"参考 mod 那排按钮的宽度（190+120=310）"当上限，
        /// 想让这一排**不比装那个 mod 时更宽**（用户第一次反馈是"UI 宽了一截出来"）。
        /// 但 **310 不是游戏本身的限制**，只是那个 mod 自己写死的数。多轮实测出的边界是：
        /// **~380 这一档正常、428 就会把面板撑宽**——所以留白定在 8。
        /// 可调旋钮：`ButtonPadding`（留白）、`ButtonMinWidth`（保底宽）、`ButtonHeight`、容器 `spacing`。
        ///
        /// 注意 LayoutElement 要**先 GetComponent 再补**：模板上可能本来就有一个，
        /// 直接 AddComponent 会变成两个，Unity 只认先找到的那个，我们设的宽度就白设了。
        /// </summary>
        private static void AddButton(Transform parent, CustomButton template, string term, System.Action onClick)
        {
            GameObject go = UnityEngine.Object.Instantiate(template.gameObject);
            go.transform.SetParent(parent);

            CustomButton button = go.GetComponent<CustomButton>();
            TMP_Text label = button.TextMeshPro;
            label.text = SokLoc.Translate(term);
            label.enableWordWrapping = false;
            label.ForceMeshUpdate();

            float needed = label.preferredWidth + label.margin.x + label.margin.z + ButtonPadding;
            LayoutElement element = go.GetComponent<LayoutElement>();
            if (element == null)
            {
                element = go.AddComponent<LayoutElement>();
            }
            element.minWidth = Mathf.Max(ButtonMinWidth, needed);
            element.preferredWidth = element.minWidth;
            element.preferredHeight = ButtonHeight;

            button.Image.color = ColorManager.instance.DisabledColor;
            // ⚠️ 故意**不设** button.TooltipText：用户要求"鼠标放在标签上后展示的悬浮窗直接去掉"
            button.Clicked += delegate { onClick(); };
        }
    }

    /// <summary>
    /// 按模式过滤主意：Prefix 接替 `GameScreen.KnowledgeMatchesSearch`（私有方法 → 用字符串形式补）。
    ///
    /// 模式是 `Vanilla` 时直接放行（交回原版：只匹配名字），所以**没用手势之前，原版搜索手感完全不变**；
    /// 用过一次 Alt+左键/右键之后，模式会一直保留到下次手势（和参考 mod"模式按钮"的语义一致，
    /// 也就是说之后手动在搜索框里打字，也会按上次的模式过滤）。
    ///
    /// 归一化与参考 mod 一致（转小写 + 去掉空格）。差别：原版在非中日韩语言下还会 `RemoveAccents` 去掉变音符号，
    /// 那是 `GameScreen` 的私有方法，本项目不额外搬运——中英文环境下与原版行为一致。
    /// </summary>
    [HarmonyPatch(typeof(GameScreen), "KnowledgeMatchesSearch")]
    internal static class IdeaSearchKnowledgePatch
    {
        [HarmonyPrefix]
        internal static bool Prefix(IKnowledge knowledge, string searchTerm, ref bool __result)
        {
            if (!ModConfig.IdeaSearch || IdeaSearch.Mode == IdeaSearchMode.Vanilla || knowledge == null)
            {
                return true; // 交回原版
            }

            string haystack = IdeaSearch.Mode == IdeaSearchMode.Description
                ? knowledge.KnowledgeText
                : knowledge.KnowledgeName;
            if (haystack == null)
            {
                __result = false;
                return false;
            }

            string needle = (searchTerm ?? "").ToLower().Replace(" ", "");
            __result = haystack.ToLower().Replace(" ", "").Contains(needle);
            return false;
        }
    }

    /// <summary>
    /// Alt + 左键单击 = 标题搜索；Alt + 右键单击 = 描述搜索；Alt + 点空白 = 清空搜索。
    /// 挂在 `WorldManager.Update` 的后置上（本项目读鼠标/键盘的补丁都走这个每帧挂点）。
    /// </summary>
    [HarmonyPatch(typeof(WorldManager), "Update")]
    internal static class IdeaSearchInputPatch
    {
        [HarmonyPostfix]
        internal static void Postfix(WorldManager __instance)
        {
            if (!ModConfig.IdeaSearch || __instance == null)
            {
                return;
            }
            IdeaSearch.EnsureButtons(); // 万一是"进游戏后才把设置打开"，这里补建那三个按钮

            if (!IdeaSearch.AltDown())
            {
                return;
            }
            if (GameScreen.instance == null || GameScreen.instance.IdeaSearchField == null)
            {
                return;
            }

            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            bool left = mouse.leftButton.wasPressedThisFrame;
            bool right = mouse.rightButton.wasPressedThisFrame;
            if (!left && !right)
            {
                return;
            }

            GameCard hovered = __instance.HoveredCard;
            if (hovered == null || hovered.CardData == null)
            {
                // Alt + 点空白处：清空搜索（对齐参考 mod 的 Clear 按钮）
                IdeaSearch.Apply("", IdeaSearch.Mode);
                return;
            }

            // 左键＝标题搜索，右键＝描述搜索（用户指定的两个手势）；搜索词用"类型名"，
            // 所以「石头箱」会按「资源箱」搜、「石头磁力箱」按「磁力箱」搜。
            IdeaSearch.Apply(IdeaSearch.SearchableName(hovered.CardData),
                left ? IdeaSearchMode.Title : IdeaSearchMode.Description);
        }
    }

    /// <summary>
    /// 在搜索框旁边加**三个固定按钮**：「搜标题」「搜描述」「清除」。
    ///
    /// 做法照参考 mod（`Instantiate` 原版 `IdeasButton` 预制体、塞进一个 `HorizontalLayoutGroup` 容器、
    /// 挂到 `IdeaSearchField.transform.parent.parent` 下），但有两点按用户要求改了：
    ///   ① **三个固定标签**，不是参考 mod 那种"一个按钮在 Title/Description 之间来回显示"；
    ///   ② **不设 `TooltipText`** → 悬停不弹提示（用户原话："鼠标放在标签上后展示的悬浮窗直接去掉"）。
    /// 按钮文字走本地化 term（`SokLoc.Translate`），不像参考 mod 那样写死英文。
    ///
    /// 这几个按钮是"照原版 UI 拼控件"，比框选覆盖层那种纯屏幕绘制更容易受游戏 UI 改动影响；
    /// 所以整段包在 try/catch 里——拼不出来最多是没按钮，绝不能把 `GameScreen.Awake` 搞崩。
    /// </summary>
    [HarmonyPatch(typeof(GameScreen), "Awake")]
    internal static class IdeaSearchButtonsPatch
    {
        [HarmonyPostfix]
        internal static void Postfix()
        {
            IdeaSearch.EnsureButtons();
        }
    }

    /// <summary>
    /// Alt 手势按下时，别让卡片执行自己的 `Clicked()`（箱子取物、开包那类副作用）。
    /// 和框选的 Ctrl 前缀同一个理由——用户明确讨厌"快捷键顺手触发卡片行为"。
    /// </summary>
    [HarmonyPatch(typeof(GameCard), "Clicked")]
    internal static class IdeaSearchCardClickPatch
    {
        [HarmonyPrefix]
        internal static bool Prefix()
        {
            return !IdeaSearch.GestureActive;
        }
    }

    /// <summary>Alt 手势按下时，卡包同理（否则 Alt+左键顺带开包）。</summary>
    [HarmonyPatch(typeof(Boosterpack), "Clicked")]
    internal static class IdeaSearchBoosterClickPatch
    {
        [HarmonyPrefix]
        internal static bool Prefix()
        {
            return !IdeaSearch.GestureActive;
        }
    }
}
