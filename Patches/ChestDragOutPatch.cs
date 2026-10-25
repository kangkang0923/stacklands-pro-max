using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;

namespace StacklandsProMaxNS
{
    /// <summary>
    /// 箱子的"拖出"交互 + 屏蔽原版的「Shift + 左键拖整叠」。
    ///
    /// 用户需求原话（0.5.5）：
    ///   「目前的资源箱，因为加了 Stacks n Capacity 这个 mod，支持"按 SHIFT 键将资源直接从箱子中拖出！
    ///     （CTRL 表示单个单元）"具体功能为：按住 SHIFT，左键对着资源箱按下（按住不松开），出现 10 张牌的牌堆，
    ///     左键一直按住，牌堆跟随鼠标，松开左键，牌堆落地。ctrl 则只有一张牌。
    ///     现在有个问题，在资源箱堆叠在魔法胶水上后，"按住 SHIFT，左键对着资源箱按下"，会被识别成拖动整个牌堆，
    ///     这个是游戏本身的逻辑。你要做的：1 屏蔽掉游戏本身的 shift+左键拖动牌堆的逻辑；
    ///     2 将上文中的"具体功能"迁移到"磁力箱"中；3 让"具体功能"可以对堆叠在魔法胶水上的资源箱也生效」
    ///
    /// ── 原版那条逻辑长什么样（`WorldManager.Update`，反编译核对） ──────────────
    ///
    /// Shift 按下且鼠标下是 `GameCard` 时：`IsShiftDragging` 置 true、`draggable` 取**整叠的根卡**
    /// （不是玩家点中的那张），点中那张寄存在 `draggable.ClickedObject`，并记下 `grabOffset`（鼠标与牌堆位置之差）；
    /// 否则 `IsShiftDragging` 置 false。最后统一调 `draggable.StartDragging()` —— 我们从这一步插手。
    ///
    /// 所以"资源箱叠在魔法胶水上"时：鼠标下是资源箱、`GetRootCard()` 却是底下的胶水 →
    /// 原版抓起整叠 → 就是用户看到的现象。
    ///
    /// ── 本项目的做法：在 `GameCard.StartDragging` 上插手（和 Stacks n Capacity 同一个挂点） ──
    ///
    /// 好处是**不需要 transpiler**：原版是"先决定好要抓哪张、再调 `StartDragging()`"，
    /// 我们在 `StartDragging()` 里就能看到全部真相：
    ///   - `__instance`＝原版准备抓的那张（Shift 整叠拖时是**根卡**）；
    ///   - `__instance.ClickedObject`＝**玩家真正点中的那张**（原版只在 Shift 整叠拖时设置它，
    ///     全局只有 `WorldManager.Update` 42109 一处赋值，`StopDragging()` 里清空）；
    ///   - `WorldManager.IsShiftDragging`＝这次是不是 Shift 整叠拖。
    ///
    /// 于是：① **屏蔽整叠拖**＝改抓"玩家点中的那张"（＝普通拖动，把那张从牌堆里拿出来）；
    /// ② **拖出**＝如果点中的是**本项目的磁力箱**，就把修饰键对应的张数取出来、挂到鼠标上，
    /// 并拦掉原方法（`return false`）。因为判断依据是"玩家点中的那张"，**箱子叠在谁身上都成立**
    /// （魔法胶水 / 普通牌堆都行）。
    ///
    /// ── 为什么**不**动原版资源箱（0.5.5 修掉的一个真实 bug） ────────────────────────
    ///
    /// 第一版这里还实现了"原版 `ResourceChest` 的拖出"（对齐它 `RemoveResources` 的形状）。结果用户实测：
    /// **Ctrl+左键出两张牌、只有一张跟手**；Shift 在独立箱子上也是双份（叠在魔法胶水上时反而正常）。
    ///
    /// 根因：本机 `0Harmony.dll` 是 BepInEx **HarmonyX 2.10.1** —— **所有前缀都会执行**，
    /// `return false` 只决定"原版方法要不要跑"，**拦不住别人的前缀**（这个版本连 `__runOriginal` 都没有，
    /// 前缀里根本没法知道别人是否已经接管）。而 Stacks n Capacity（`3378143608`）**也**在
    /// `GameCard.StartDragging` 上打前缀，它认的正是 `ResourceChest`：
    ///
    /// 它的 `Patch_DragItem`（Prefix）认的正是 `ResourceChest`：卡数据能转成 `ResourceChest` 且箱内数量大于 0 时，
    /// 它自己扣一张、开始拖动，并让原版方法不再执行。
    ///
    /// 于是原版资源箱上两边**各扣一份** → 双份。Shift 看着"正常"只是因为箱子常常被我一次扣空
    /// （它的前缀里有 `ResourceCount > 0` 的判断）；箱子货多时照样双份。叠在魔法胶水上时，
    /// 原版抓的是**根卡（胶水）**，它的前缀看到胶水 → 不动作，所以那格反而只有我扣一份 = 正常。
    ///
    /// 修法（也是用户的要求："资源箱的功能不需要优化"）：**原版资源箱我们一张都不取**，
    /// 取牌完全交回原版 / Stacks n Capacity。我们只保留上面那条 ① 的"改抓玩家点中的那张"——
    /// 而这条恰好就是"箱子叠在魔法胶水上时也能拖出"的钥匙：原版本来要抓胶水，我们把这次拖拽
    /// **改成抓箱子本身**，于是（在那个嵌套的 `StartDragging` 调用里）Stacks n Capacity 的前缀
    /// 终于看到的是 `ResourceChest` → 它自己把 10 张拖出来。**没装 Stacks n Capacity 时**：
    /// 原版资源箱就是原版行为（点击取 1、Shift+点击取 5、可拖动），叠在胶水上的箱子退回
    /// "把箱子从胶水上拖出来"——不会报错，只是没有"拖出 10 张"这个增强。
    ///
    /// ── 和 0.5.1~0.5.4 那版实现的区别（那时被整体回退掉了） ──────────────────────
    ///
    /// 旧版是在 `MagnetChest.Clicked()` 里取牌、再由 `WorldManager.Update` 的 Postfix 延后一帧抓，
    /// 于是要额外处理"抓完同一瞬间被原版丢掉"（`InputController.JustEnded` 不重置）——整套要三个补丁。
    /// 现在在鼠标**按下那一帧**就完成接管（和 Stacks n Capacity 一样），原版随后每帧自己让
    /// `DraggingDraggable.TargetPosition` 跟鼠标、松开时自己 `DropCard()` 落地，**一行都不用写**。
    ///
    /// ── 三个必须注意的坑 ─────────────────────────────────────────────────────
    ///
    /// ① **只认左键**：本项目自己的 `RightDragStackPatch` 也在调 `StartDragging()`；
    ///    而装了 **Better Mouse Controls** 时它会让 `GetKey(LeftShift)` 在"右键按下"时返回 true
    ///    （它的 `ShiftIsPressedIfRightclick` 补丁）→ 不挡的话右键拖一下磁力箱就会变成"拖出 10 张"。
    ///    所以这里用原始输入 `Mouse.current.leftButton.isPressed` 判断，而不是只看修饰键。
    /// ② **框选整组拖要让路**：`BoxSelect.BeginDrag()` 会逐张调 `StartDragging()`，
    ///    那时如果手里按着 Shift，会被这里误判成"拖出"。所以 `BoxSelect.GroupDragging` 为真时直接放行。
    /// ③ **`IsShiftDragging` 复位**：它是原版"整叠拖"的语义位，还被
    ///    `GameCard.DeterminePositionType`（悬停卡抬高）/ `IsCollapsed`（整叠拖时不折叠）读。
    ///    我们既然把拖拽改成了普通拖/拖出，就必须把它复位成 false，否则渲染还按"整叠拖"走。
    ///    （注意：只在"我们确实改写了这次拖拽"的分支里复位，普通拖一律不碰。）
    /// </summary>
    [HarmonyPatch(typeof(GameCard), "StartDragging")]
    internal static class ChestDragOutPatch
    {
        /// <summary>Shift 拖出多少张（原版 Shift 点击取 5 张；用户要求这里给 10 张，与 Stacks n Capacity 一致）。</summary>
        internal const int DragOutCountWithShift = 10;


        /// <summary>
        /// 优先级设为 First 只是为了"先看清情况再决定"（我们做的事和 Stacks n Capacity 不再重叠：
        /// 原版资源箱的取牌完全交给它，我们只做"屏蔽整叠拖 / 改抓玩家点中的那张"）。
        /// ⚠️ 注意它**挡不住** Stacks n Capacity 的前缀：HarmonyX 里所有前缀都会执行。
        /// </summary>
        [HarmonyPriority(Priority.First)]
        [HarmonyPrefix]
        internal static bool Prefix(GameCard __instance)
        {
            WorldManager world = WorldManager.instance;
            if (world == null || __instance == null || __instance.Destroyed)
            {
                return true;
            }
            if (BoxSelect.GroupDragging)
            {
                return true; // 坑 ②：框选整组拖，别插手
            }
            if (Mouse.current == null || !Mouse.current.leftButton.isPressed)
            {
                return true; // 坑 ①：只认左键拖（右键拖有自己的补丁，且别的 mod 会让 Shift 谎报为按下）
            }

            bool shift = IsDown(Key.LeftShift) || IsDown(Key.RightShift);
            bool ctrl = IsDown(Key.LeftCtrl) || IsDown(Key.RightCtrl);

            // 玩家真正点中的那张：Shift 整叠拖时 `__instance` 是整叠根卡，点中的那张在 `ClickedObject` 上
            bool shiftStackDrag = world.IsShiftDragging || __instance.ClickedObject != null;
            GameCard clicked = __instance;
            if (__instance.ClickedObject is GameCard clickedCard && !clickedCard.Destroyed && clickedCard != __instance)
            {
                clicked = clickedCard;
            }

            // Ctrl 优先于 Shift（和 Stacks n Capacity 一致）：Ctrl＝1 张（"单个单元"），Shift＝10 张
            int count = ctrl ? 1 : (shift ? DragOutCountWithShift : 0);

            // ① 拖出：从"点中的那张"里取一叠出来挂到鼠标上
            if (count > 0)
            {
                GameCard stack = TryTakeOut(clicked, count);
                if (stack != null)
                {
                    EndShiftStackDrag(world, __instance);
                    AttachToMouse(world, stack);
                    return false; // 拦掉原方法：原版本来要抓的是 __instance（Shift 时＝整叠根卡）
                }
            }

            // ② 没取到东西：那就只是"屏蔽原版的 Shift 整叠拖"
            if (shiftStackDrag)
            {
                EndShiftStackDrag(world, __instance);
                if (clicked != __instance)
                {
                    // 原版要抓整叠根卡 → 改成抓玩家点中的那张（＝普通拖动，把这张从牌堆里拿出来）
                    AttachToMouse(world, clicked);
                    return false;
                }
                // 点中的就是根卡（例如整叠就一张）：拖它本来就是普通拖的语义，交回原版即可
            }

            return true;
        }

        private static bool IsDown(Key key)
        {
            InputController input = InputController.instance;
            return input != null && input.GetKey(key);
        }

        /// <summary>
        /// 从这张卡里取出一叠准备挂到鼠标上。
        ///
        /// ⚠️ **只认本项目的 `MagnetChest`**：原版 `ResourceChest` 我们**一张都不取**，
        /// 因为 Stacks n Capacity 也在同一个 `GameCard.StartDragging` 上打前缀、也认 `ResourceChest`，
        /// 而 HarmonyX 里**所有前缀都会执行** → 两边各扣一份就是用户实测到的"双份牌"（见类注释）。
        /// 取不到（不是我们的箱子 / 空箱子 / 没设定类型）返回 null，交给原版或别的 mod。
        /// </summary>
        private static GameCard TryTakeOut(GameCard card, int count)
        {
            if (card == null || card.Destroyed || card.CardData == null)
            {
                return null;
            }

            MagnetChest magnet = card.CardData as MagnetChest;
            if (magnet != null)
            {
                GameCard stack = magnet.TakeOutStack(count);
                // 记住"这一叠是从这个箱子取出来的"：玩家只是点一下（没拖动）时，松手那一帧靠它
                // 把落点改成"往旁边弹到地上"，而不是落回箱子里（见 ChestDropRedirectPatch）。
                ChestTakeOut.Remember(stack, card);
                return stack;
            }

            return null;
        }

        /// <summary>
        /// 撤销原版这次"整叠拖"的语义（坑 ③）：复位 `IsShiftDragging`、清掉根卡上寄存的 `ClickedObject`。
        /// 只在"我们确实改写了这次拖拽"（拖出 / 改成抓点中的那张）的分支里调用。
        /// </summary>
        private static void EndShiftStackDrag(WorldManager world, GameCard root)
        {
            if (world.IsShiftDragging)
            {
                world.IsShiftDragging = false;
            }
            if (root != null && !root.Destroyed && root.ClickedObject != null)
            {
                root.ClickedObject = null;
            }
        }

        /// <summary>
        /// 把某张卡挂到鼠标上：复用原版三行（`DraggingDraggable` / `grabOffset` / `StartDragging()`）。
        /// 之后跟随鼠标、落点吸附、松开落地/入箱/音效全部由原版负责。
        ///
        /// 这三行和本项目右键拖的 `RightDragStackPatch` 完全一致，是同一个已验证的做法。
        /// 注意 `StartDragging()` 会再次进本 Prefix：那时 `ClickedObject` 已清、`IsShiftDragging` 已复位，
        /// 且取数的对象（资源牌堆）不是箱子，所以会正常放行，不会递归。
        /// </summary>
        private static void AttachToMouse(WorldManager world, GameCard card)
        {
            world.DraggingDraggable = card;
            world.grabOffset = world.mouseWorldPosition - card.transform.position;
            card.StartDragging();
        }
    }
}
