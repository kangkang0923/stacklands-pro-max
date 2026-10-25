using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace StacklandsProMaxNS
{
    /// <summary>
    /// 左侧信息栏"加高 + 高度自适应"（迁移自创意工坊 **HigherSidebar** `2995428012`，
    /// id `com.maxsch.stacklands.highersidebar`，v0.4.1，作者 maxsch）。
    ///
    /// ── 原版为什么不够用 ──────────────────────────────────────────────────────
    ///
    /// 原版信息栏（`GameScreen.InfoBox`）高度写死在预制体里（≈300px），
    /// 而本项目 + 其它 mod 往 `InfoText` 里塞的内容（掉落概率、战斗详解、牌堆描述…）
    /// 一多就被裁掉。参考 mod 的做法：**动态**按文字实际高度重算信息栏高度。
    ///
    /// ── 参考 mod 的做法（反编译核实，本文件 参照其行为自行实现） ────────────────────────
    ///
    /// ① `GameScreen.Awake` 后置：把 `InfoBox` 里所有 `Image` / `TextMeshProUGUI` 的
    ///    `raycastTarget` 关掉 —— 信息栏加高后会盖住下面的卡片，这些图形如果能吃射线
    ///    就会导致"点不中卡"。
    /// ② `GameScreen.LateUpdate` 后置：算一次目标高度并写进 `InfoBox` 的 `sizeDelta.y`，
    ///    再 `ForceRebuildLayoutImmediate(SideTransform)` 让侧边栏重排。
    ///
    /// 高度算法（参照）：
    /// `Valuetext.preferredHeight + InfoText.preferredHeight + 72`
    /// 再加上所有**激活中**的分隔条（`dividerList` 里挂着 `VerticalLayoutGroup` 的那些）
    /// 的 `preferredHeight + spacing`，最后夹在 [最小, 最大] 之间。
    ///
    /// 两个"手感"细节也参照：
    ///   · 变化小于 1px 就什么都不做（避免每帧重排）；
    ///   · **变矮**要等 0.1 秒的冷却（`ShrinkCooldown`）—— 鼠标在两张卡之间快速移动时
    ///     内容高度会来回跳，冷却能防止信息栏疯狂抖动。
    ///
    /// ── 与原 mod 的差异（有意为之） ────────────────────────────────────────────
    ///
    /// · 原 mod 有 4 个数值设置（动态/固定高度、最小/最大、冷却）；本项目按"开关要克制"
    ///   只留 **1 个开关 + 2 个数值**（最小/最大高度），固定高度模式和冷却时长写死在这里。
    /// · 玩家配置里原本是 `Use Dynamic Height = true`，所以这里只实现动态模式；
    ///   开关关掉 = 完全回到原版（不碰 `sizeDelta`）。
    /// · `dividerList` 在**当前游戏版本里是私有字段**（原 mod 编译时还是公开的），
    ///   所以这里用反射取一次、缓存住（拿不到就只按文本高度算，不影响主体功能）。
    /// </summary>
    internal static class SidebarHeight
    {
        /// <summary>给"整块内容 + 内边距"留的余量（原 mod 的 72）。</summary>
        private const float Slack = 72f;

        /// <summary>变矮之前的冷却秒数（原 mod 默认 0.1）。</summary>
        private const float ShrinkCooldown = 0.1f;

        /// <summary>上一次真正写进去的高度；跟着开关关掉时要靠它复位。</summary>
        private static float lastHeight = -1f;
        private static float heightCooldown;

        /// <summary>原版 `GameScreen.dividerList`（私有字段）。</summary>
        private static FieldInfo dividerListField;

        /// <summary>
        /// `GameScreen.Awake` 后置：让信息栏里的图形不吃鼠标射线。
        /// 加高后的信息栏和卡片区重叠，`raycastTarget` 开着会挡住点卡。
        /// </summary>
        internal static void MakeInfoBoxClickThrough()
        {
            if (!ModConfig.SidebarHeight)
            {
                return;
            }
            GameScreen screen = GameScreen.instance;
            if (screen == null || screen.InfoBox == null)
            {
                return;
            }
            try
            {
                Image[] images = screen.InfoBox.GetComponentsInChildren<Image>(true);
                for (int i = 0; i < images.Length; i++)
                {
                    images[i].raycastTarget = false;
                }
                TextMeshProUGUI[] texts = screen.InfoBox.GetComponentsInChildren<TextMeshProUGUI>(true);
                for (int i = 0; i < texts.Length; i++)
                {
                    texts[i].raycastTarget = false;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[StacklandsProMax] 信息栏点击穿透设置失败（只影响信息栏，不影响其它功能）: " + ex.Message);
            }
        }

        /// <summary>
        /// `GameScreen.LateUpdate` 后置：每帧算一次目标高度。
        /// 放在 LateUpdate 是因为它跑在原版刷新 `InfoText` 之后，量到的是本帧最终的文本。
        ///
        /// 0.5.19 修"信息栏闪动"（用户反馈：悬停魔法收集器/鸡时介绍框像刷新网页一样闪）：
        /// 悬停带**计时器**的单卡时（鸡下蛋、收集器产出、魔法收集器状态…），原版每帧用
        /// `FormatTimeLeft`（0.1 秒精度）重写 InfoText → 文本每帧都变 → 量出的目标高度跟着
        /// 每帧抖动，一抖就写 `sizeDelta` + `ForceRebuildLayoutImmediate` → 整栏每帧重排＝闪动。
        /// 两条对策：① 目标高度**量化到 2px 网格**（±1px 的测量噪声被吸收，只有真实的高度变化
        /// 才触发重排）；② 文本没变时**不再重复赋值** `infoText.text`（TMP 的 text setter 会把
        /// 文本网格标记脏，重复赋值是无意义的重绘）。高级温室没事是因为它总带着种子、悬停走的是
        /// "整叠"分支，文本短且高度稳定——现在长文本路径也稳定了。
        /// </summary>
        internal static void UpdateHeights()
        {
            if (!ModConfig.SidebarHeight)
            {
                lastHeight = -1f; // 关掉后再打开要能重新生效
                return;
            }
            GameScreen screen = GameScreen.instance;
            if (screen == null || screen.InfoBox == null || screen.InfoText == null || screen.Valuetext == null)
            {
                return;
            }

            float target = Mathf.Clamp(CalculateHeight(screen),
                ModConfig.InfoBoxMinHeight, ModConfig.InfoBoxMaxHeight);
            target = Mathf.Round(target * 0.5f) * 2f; // 量化到 2px 网格：吸收计时器逐帧刷新带来的 ±1px 抖动

            if (Mathf.Abs(lastHeight - target) < 1f)
            {
                heightCooldown = 0f;
                return;
            }
            if (target < lastHeight && heightCooldown < ShrinkCooldown)
            {
                // 变矮先冷却：鼠标在两张卡之间扫过时不要抖
                heightCooldown += Time.deltaTime;
                return;
            }

            lastHeight = target;
            heightCooldown = 0f;

            RectTransform rect = screen.InfoBox.transform as RectTransform;
            if (rect == null)
            {
                return;
            }
            Vector2 size = rect.sizeDelta;
            size.y = target;
            rect.sizeDelta = size;

            if (screen.SideTransform != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(screen.SideTransform);
            }
        }

        /// <summary>文字 + 分隔条的真实高度（参照参考 mod 的算法）。</summary>
        private static float CalculateHeight(GameScreen screen)
        {
            TMP_Text infoText = screen.InfoText;
            TMP_Text valueText = screen.Valuetext;

            // 尾部空白会算进 preferredHeight，先掐掉（原 mod 就是这么做的）。
            // ⚠️ 文本和上次相同时不要重新赋值：TMP 的 text setter 会标记文本网格脏，
            // 悬停带计时器的卡时原版每帧都在重写文本，我们再设一次就是每帧两遍重绘（0.5.19 修闪动）。
            string trimmed = infoText.text == null ? "" : infoText.text.Trim();
            if (!string.Equals(trimmed, infoText.text))
            {
                infoText.text = trimmed;
            }

            float height = valueText.preferredHeight + infoText.preferredHeight + Slack;

            List<GameObject> dividers = GetDividers(screen);
            if (dividers != null)
            {
                for (int i = 0; i < dividers.Count; i++)
                {
                    GameObject divider = dividers[i];
                    if (divider == null || !divider.activeInHierarchy)
                    {
                        continue;
                    }
                    VerticalLayoutGroup layout;
                    if (divider.TryGetComponent(out layout))
                    {
                        height += layout.preferredHeight + layout.spacing;
                    }
                }
            }
            return height;
        }

        private static List<GameObject> GetDividers(GameScreen screen)
        {
            if (dividerListField == null)
            {
                dividerListField = AccessTools.Field(typeof(GameScreen), "dividerList");
            }
            if (dividerListField == null)
            {
                return null;
            }
            return dividerListField.GetValue(screen) as List<GameObject>;
        }
    }

    /// <summary>
    /// 信息栏加高：`GameScreen.LateUpdate` 后置（原 mod 同一个挂点）。
    /// 每帧算一次目标高度写进 `InfoBox.sizeDelta.y`。
    /// </summary>
    [HarmonyPatch(typeof(GameScreen), "LateUpdate")]
    internal static class SidebarHeightUpdatePatch
    {
        [HarmonyPostfix]
        internal static void Postfix()
        {
            SidebarHeight.UpdateHeights();
        }
    }

    /// <summary>
    /// 信息栏加高之后会盖住卡片区，所以把信息栏里的图形/文字设成"不吃鼠标射线"（原 mod 同一个挂点）。
    /// </summary>
    [HarmonyPatch(typeof(GameScreen), "Awake")]
    internal static class SidebarHeightAwakePatch
    {
        [HarmonyPostfix]
        internal static void Postfix()
        {
            SidebarHeight.MakeInfoBoxClickThrough();
        }
    }
}
