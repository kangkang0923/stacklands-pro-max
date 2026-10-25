using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace StacklandsProMaxNS
{
    /// <summary>
    /// 悬浮提示（Tooltip）**永远显示在鼠标右上方**，不再翻到左边。
    ///
    /// 用户需求原话（0.5.18）：「鼠标放在取出的牌往边上掉、详细信息 这两个开关时，悬浮窗展示在鼠标的左边，
    /// 有一部分文字就显示不出来了，正常的都是展示在右边的，你看一下其他的有没有往左边的，全部改成往右边」。
    ///
    /// ── 原版为什么会翻到左边（反编译 `Tooltip.SetRectTransformToSafePosition`） ──
    ///
    /// 所有悬浮提示最终都汇到 `Tooltip.Text`，由 `Tooltip.LateUpdate` 每帧调
    /// `SetRectTransformToSafePosition(rectTransform)` 定位。原版做法是依次试 4 个 pivot：
    ///
    /// 它依次把 pivot 试成左下、右下、左上、右上四个角，位置取"被夹到屏幕内的鼠标位置"换算出的本地坐标，
    /// 一遇到不越界的就采用。
    ///
    /// pivot (0,0) ＝悬浮窗**左下角**贴着鼠标（往右上展开，正常情况）；pivot (1,0) ＝**右下角**贴着鼠标
    /// （往左上展开，就是用户看到的"显示在鼠标左边"）。长文案（「取出的牌往边上掉」「详细信息」的说明
    /// 都是几整行）从 (0,0) 放不下，就翻到了 (1,0)。而且它翻过去之后**仍然可能被截断**：
    /// `TextMesh.text` 是同帧才赋值的，`IsOverflowing` 量到的还是旧文字的尺寸，等布局算完实际更宽，
    /// 左边那截就出屏了——正是"有一部分文字显示不出来"。
    ///
    /// ── 做法：Prefix 接管整个方法 ─────────────────────────────────────────
    ///
    ///   1. `LayoutRebuilder.ForceRebuildLayoutImmediate` 先把布局算完（修掉"量旧尺寸"的根因）；
    ///   2. pivot 固定 (0,0)＝永远从鼠标往**右上**展开；
    ///   3. 右边/上边真放不下时**整体往回挪**（右缘贴屏幕右缘），保证完整可见——绝不翻到左边。
    ///
    /// 这是全局行为（设置界面、卡牌悬停、图鉴……全都走这一个方法），短提示的表现和原版完全一致，
    /// 长提示从"翻左边且截断"变成"贴着右边完整显示"。常开、无开关（UI bug 修复）。
    /// </summary>
    [HarmonyPatch(typeof(Tooltip), nameof(Tooltip.SetRectTransformToSafePosition))]
    internal static class TooltipPositionPatch
    {
        /// <summary>悬浮窗与屏幕边缘至少留这几个像素。</summary>
        private const float ScreenMargin = 4f;

        [HarmonyPrefix]
        internal static bool Prefix(RectTransform rect)
        {
            if (rect == null || GameCanvas.instance == null || InputController.instance == null)
            {
                return false; // 依赖不在（理论上不会）：什么都不做，也别让原版跑
            }

            Vector3 originalScale = rect.transform.localScale;
            rect.transform.localScale = Vector3.one;

            // 原版的坑：text 是本帧刚设置的，不强制算一次布局，量到的还是旧尺寸
            LayoutRebuilder.ForceRebuildLayoutImmediate(rect);

            // 固定"左下角贴鼠标"＝悬浮窗永远往右上方展开（原版翻左边用的 pivot.x=1 一律不用）
            rect.pivot = new Vector2(0f, 0f);
            rect.localPosition = GameCanvas.instance.ScreenPosToLocalPos(
                InputController.instance.ClampedMousePosition());

            // 右上放不下就整体往回挪（右缘/上缘贴屏幕边缘），完整显示优先
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            float overflowX = corners[2].x - ((float)Screen.width - ScreenMargin);
            float overflowY = corners[1].y - ((float)Screen.height - ScreenMargin);
            Vector3 position = rect.localPosition;
            if (overflowX > 0f)
            {
                position.x -= overflowX / rect.lossyScale.x;
            }
            if (overflowY > 0f)
            {
                position.y -= overflowY / rect.lossyScale.y;
            }
            rect.localPosition = position;

            rect.transform.localScale = originalScale;
            return false;
        }
    }
}
