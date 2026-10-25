using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;

namespace StacklandsProMaxNS
{
    /// <summary>
    /// 右键按住 = 拖动整个牌堆（点在牌堆上的任意一张都行），松开右键放下；单张牌也支持。
    ///
    /// 为什么这么做：
    ///   原版拖拽由 WorldManager.Update 里的输入逻辑驱动，而 InputController 只在
    ///   "Click to Drag"（可访问性选项，默认关）打开时才会把右键登记成一个拖拽输入，
    ///   所以默认情况下右键完全不参与拖拽（右键拖是用来平移镜头的）。
    ///   另外原版已经有"整叠拖动"的先例：按住 LeftShift 拖会抓 GetRootCard()。
    ///
    /// 这里的做法是从原始鼠标状态（Mouse.current.rightButton）自己判断，然后复用原版拖拽：
    ///   - 在 Update 之后（Postfix）把 DraggingDraggable 设成 **牌堆根卡**、设好 grabOffset 并 StartDragging()
    ///   - 之后每帧原版逻辑会自己让 TargetPosition 跟随鼠标、做落点吸附
    ///   - 松开右键时调用原版的私有 DropCard()，落点吸附/堆叠/音效都由原版处理
    ///
    /// 对单张牌同样生效：右键拖单张 = 直接移动它（不会触发该卡片的 Clicked()，
    /// 所以对"左键=取出物品"这类卡（比如磁力箱、储物箱）特别顺手）。
    /// </summary>
    [HarmonyPatch(typeof(WorldManager), "Update")]
    internal static class RightDragStackPatch
    {
        private static readonly MethodInfo DropCardMethod = AccessTools.Method(typeof(WorldManager), "DropCard");

        // 我们正在用右键拖的那一叠（单张牌时根卡就是它自己）的根卡
        private static GameCard draggingRoot;

        [HarmonyPostfix]
        internal static void Postfix(WorldManager __instance)
        {
            if (__instance == null || Mouse.current == null || InputController.instance == null)
            {
                return;
            }
            if (!ModConfig.RightDrag)
            {
                // 设置界面里刚关掉这个功能：如果正在拖，先按原版方式放下，再交还给原版
                if (draggingRoot != null)
                {
                    DropCardMethod?.Invoke(__instance, null);
                    draggingRoot = null;
                }
                return;
            }
            if (!InputController.instance.CurrentSchemeIsMouseKeyboard)
            {
                return; // 只有键鼠方案下才有右键
            }
            if (IdeaSearch.GestureActive)
            {
                // Alt+右键是"按描述搜主意"的手势（见 `IdeaSearchPatch.cs`），别顺手把牌堆拖起来
                return;
            }

            bool pressed = Mouse.current.rightButton.wasPressedThisFrame;
            bool released = Mouse.current.rightButton.wasReleasedThisFrame;

            if (pressed && __instance.DraggingDraggable == null)
            {
                GameCard hovered = __instance.HoveredCard;
                // 单张牌和整叠都接管：抓的始终是"根卡"，所以点牌堆任意一张都会拖动整叠
                if (hovered != null)
                {
                    GameCard root = hovered.GetRootCard();
                    if (root != null && root.CanBeDragged())
                    {
                        __instance.DraggingDraggable = root;
                        __instance.grabOffset = __instance.mouseWorldPosition - root.transform.position;
                        root.StartDragging();
                        draggingRoot = root;
                    }
                }
            }

            if (draggingRoot == null)
            {
                return;
            }

            if (released)
            {
                // 用原版收尾：落点吸附、自动堆叠、音效都在 DropCard 里
                DropCardMethod?.Invoke(__instance, null);
                draggingRoot = null;
            }
            else if (__instance.DraggingCard != draggingRoot)
            {
                draggingRoot = null; // 拖拽被别的逻辑结束了（比如进了过场）
            }
        }
    }

    /// <summary>
    /// 当 "Click to Drag" 打开时，右键会被 InputController 登记成一次拖拽输入，
    /// 于是原版会抢先把鼠标下那一张牌抓起来（把牌堆拆开），右键拖整叠就失效了。
    /// 这里在这种时刻让 GetInputBegan(0) 返回 false，把接管权让给 RightDragStackPatch。
    /// 默认配置（Click to Drag 关）下这个补丁不会有任何影响。
    /// </summary>
    [HarmonyPatch(typeof(InputController), "GetInputBegan")]
    internal static class RightDragInputPatch
    {
        [HarmonyPostfix]
        internal static void Postfix(int i, ref bool __result)
        {
            if (!ModConfig.RightDrag)
            {
                return; // 功能关掉时不要干扰原版（Click to Drag 开启时原版会自己处理右键）
            }
            if (!__result || i != 0)
            {
                return;
            }
            if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
            {
                __result = false;
            }
        }
    }
}
