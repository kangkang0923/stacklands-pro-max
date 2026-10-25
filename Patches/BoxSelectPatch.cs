using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;

namespace StacklandsProMaxNS
{
    /// <summary>
    /// 框选多张卡一起搬运。合并自 mod **Multi Select &amp; Drag**（创意工坊 `3780343990`，id `multidrag`，v1.0.0）。
    ///
    /// 我读了它的 DLL 确认机制与补丁点（关于游戏本身的事实），代码在本项目里按自己的结构重写，
    /// 并做了几处适配：① 用 `ModConfig.BoxSelect` 总开关控制（关掉后四个补丁都早退、零开销）；
    /// ② 松手收尾按参考 mod 的做法清 `BeingDragged`/`ClickedObject`/`DragTag`（**不**调原版
    ///   `DropCard()`/`StopDragging()`，理由见 `EndDrag()` 注释）：主卡的落点吸附、自动堆叠、音效
    ///   仍由原版那一帧的 `DropCard()` 负责，同组卡停在各自偏移后的落点；
    /// ③ 额外加了"最多同时搬 N 张"的上限，避免框住上百张时每帧算一堆 `TargetPosition`。
    ///
    /// 玩法（按用户要求**砍到只剩基本功能**）：
    ///   - **Ctrl + 在空白处按住左键拖出一个框** → 框内的散卡全部被选中（松手时结算，每次框选**替换**整个选择）
    ///   - 选中后**拖任意一张被选中的卡** → 整组按原来的相对位置一起走，松手各自落地
    ///   - **不按 Ctrl 点击空白处 → 清空选择**；整组搬完一次后选择也会自动清掉（黄色高亮不会留在卡上）
    ///
    /// 用户原话：「把从"Multi Select Drag"迁移来的逻辑砍一砍，因为快捷键会和其他正在做的功能冲突，
    /// 只留基本的ctrl+鼠标左键拖动画框选中、点击空白处取消选中，其他都去掉」。
    /// 所以**真的去掉了**：Ctrl+点击单张增/减选择（`Toggle()` 已删）、Shift 框选追加
    /// （`ApplyBoxSelection()` 不再收 `additive`）。⚠️ 但 `GameCard.Clicked` / `Boosterpack.Clicked`
    /// 那两个 Ctrl 前缀**必须保留**：它们是用来挡掉卡片自身行为的（箱子取物、卡包开包），
    /// 不然 Ctrl+点一下就会顺手把箱子打开——这正是用户说的"快捷键冲突"来源。
    ///
    /// Ctrl+拖空白时原版会平移镜头，所以还要挡掉 `GameCamera.StartDragging`（否则框选的同时镜头也在跑）。
    /// </summary>
    internal static class BoxSelect
    {
        private const float ClickSlopPixels = 6f;   // 小于这个位移算"点击"而不是"框选"
        private const float BorderThickness = 2f;

        private static readonly Color BoxFill = new Color(0.3f, 0.7f, 1f, 0.15f);
        private static readonly Color BoxLine = new Color(0.45f, 0.8f, 1f, 0.9f);
        private static readonly Color SelectedLine = new Color(1f, 0.85f, 0.25f, 0.95f);

        /// <summary>当前被框选中的卡（每张都是它那一叠的根卡）。</summary>
        private static readonly List<GameCard> Selected = new List<GameCard>();

        // 正在一起搬的卡（不含主卡）与它们相对主卡的偏移
        private static readonly List<GameCard> DragCards = new List<GameCard>();
        private static readonly List<Vector3> DragOffsets = new List<Vector3>();
        private static bool dragging;
        private static GameCard primary;

        // 框选状态
        private static bool boxing;
        private static Vector2 boxStart;
        private static Vector2 boxNow;

        // 一次左键按下的记账（用来区分"点击"和"框选"）
        private static bool pressActive;
        private static Vector2 pressStart;
        private static bool pressOnEmpty;

        private static Texture2D whiteTexture;
        private static Camera cachedCamera;

        // ------------------------------------------------------------------
        //  每帧主循环（在 WorldManager.Update 之后跑）
        // ------------------------------------------------------------------

        internal static void Tick()
        {
            if (!ModConfig.BoxSelect)
            {
                if (dragging || boxing)
                {
                    EndDrag(); // 设置界面里刚关掉：把状态收干净，别留下"半拖着"的卡
                    boxing = false;
                }
                ClearSelectionImmediate();
                return;
            }

            WorldManager world = WorldManager.instance;
            InputController input = InputController.instance;
            if (world == null || input == null)
            {
                return;
            }

            try
            {
                PruneSelection();
                UpdateGroupDrag(world);
                UpdateInput(world, input);
            }
            catch (Exception ex)
            {
                // 框选是纯体验功能：出错就收摊，绝不能每帧刷日志拖慢游戏
                EndDrag();
                boxing = false;
                ClearSelectionImmediate();
                Debug.LogWarning("[StacklandsProMax] 框选出错，已重置选择: " + ex.Message);
            }
        }

        /// <summary>
        /// 本帧是不是正在做"框选整组拖"？给 `ChestDragOutPatch` 用：
        /// `BeginDrag()` 会逐张调 `card.StartDragging()`，那时如果玩家手里还按着 Shift/Ctrl，
        /// 箱子拖出的补丁会把它误判成"从箱子里拖出材料"，所以它要先问一句这里。
        /// </summary>
        internal static bool GroupDragging
        {
            get { return dragging; }
        }

        /// <summary>
        /// `BoxSelectStackTakeoverPatch` 的判据：这次 `StartDragging()` 要不要被改写成"抓这一叠的根卡"。
        ///
        /// 只处理"点的是被选中牌堆里的**子卡**"这一种情况——点根卡时原版抓的本来就是根卡，
        /// 走常规的接管路径（`UpdateGroupDrag` 里 `Selected.Contains(WorldManager.DraggingCard)`）即可。
        /// 详细边界见那个补丁类的注释。
        /// </summary>
        internal static bool CanTakeOverSelectedStackDrag(GameCard card)
        {
            if (card == null || card.Destroyed)
            {
                return false;
            }
            if (!ModConfig.BoxSelect || Selected.Count <= 1)
            {
                return false; // 只选了一张时保持原版语义（点子卡＝把上面那半拆下来）
            }
            if (card.Parent == null)
            {
                return false; // 点的是根卡：原版抓的就是它
            }
            if (Mouse.current == null || !Mouse.current.leftButton.isPressed)
            {
                return false; // 只认左键拖（右键拖有自己的补丁，它抓的本来就是根卡）
            }

            WorldManager world = WorldManager.instance;
            if (world == null)
            {
                return false;
            }
            // 这次拖拽已经被别人接管了（最典型：ChestDragOutPatch 的"Ctrl+点箱子＝拖出材料"把
            // DraggingDraggable 换成了取出来的那叠）→ 让路，别把它抢回来。
            if (world.DraggingDraggable != card)
            {
                return false;
            }

            GameCard root = card.GetRootCard();
            return root != null && !root.Destroyed && root != card && Selected.Contains(root);
        }

        /// <summary>当前按住的是 Ctrl 吗？（Ctrl+拖空白＝框选）</summary>
        internal static bool CtrlDown()
        {
            InputController input = InputController.instance;
            if (input == null)
            {
                return false;
            }
            return input.GetKey(Key.LeftCtrl) || input.GetKey(Key.RightCtrl);
        }

        // ------------------------------------------------------------------
        //  输入
        // ------------------------------------------------------------------

        private static void UpdateInput(WorldManager world, InputController input)
        {
            bool ctrl = CtrlDown();
            bool overUi = GameCanvas.instance != null && GameCanvas.instance.MousePositionIsOverUI();
            Vector2 mouse = input.ClampedMousePosition();

            if (input.GetLeftMouseBegan())
            {
                pressActive = true;
                pressStart = mouse;
                pressOnEmpty = world.HoveredDraggable == null;

                if (ctrl && pressOnEmpty && !overUi && world.CanInteract)
                {
                    boxing = true;
                    boxStart = mouse;
                    boxNow = mouse;
                }
            }

            if (boxing)
            {
                boxNow = mouse;
                if (input.GetLeftMouseEnded() || !input.GetInput(0))
                {
                    ApplyBoxSelection();
                    boxing = false;
                }
            }

            if (!input.GetLeftMouseEnded())
            {
                return;
            }

            // 松手时如果几乎没动，就当"点击"处理：**不按 Ctrl 点空白处 = 取消全部选择**。
            // Ctrl+点一张卡**没有任何选中动作**（按用户要求只保留"框选"这一种选中方式）；
            // 卡片自己的 `Clicked()` 仍被 `BoxSelectCardClickPatch` 挡着——不然这一下会顺手把箱子打开。
            //
            // ⚠️ 注意这里**没有**"拖完之后清空选择"的分支了：0.5.5 用户明确要求
            // 「选中并拖动一次后，高亮/选中状态就消失了，改成高亮/选中状态一直存在，可以再次拖动移动，
            //   直到点击空白处取消」——所以拖完只让牌落地，选择集原样保留，方便连续搬第二趟。
            if (pressActive && !overUi)
            {
                Vector2 moved = mouse - pressStart;
                if (moved.magnitude < ClickSlopPixels && !ctrl && pressOnEmpty)
                {
                    Selected.Clear();
                }
            }

            pressActive = false;
        }

        /// <summary>
        /// 这张卡能不能参与框选 / 整组搬运？
        ///
        /// 为什么不能只看 `GameCard.CanBeDragged()`：它**不检查装备状态**，只看"卡数据允许拖 + 正面朝上 +
        /// 当前没在被拖"（`GameCard.CanBeDragged()` 里 `CardData.CanBeDragged && FaceUp`）。
        /// 而装备件是单独挂在 `GameCard.EquipmentChildren` 上的（`Equip()` 还会先把它 `RemoveFromStack()`，
        /// 所以它**不在堆叠链里、Parent 为 null**），位置每帧由
        /// `PositionType.IsEquipped` → `TargetPosition = EquipmentHolder.transform.position` 决定。
        /// 一旦把它们框进来，我们就会去设它们的 `TargetPosition`，和原版那套定位打架 →
        /// 表现就是"拖走村民后，村民身上的装备爆出来"。所以这里显式排掉：
        ///   - `IsEquipped`：已装备在别张卡上的装备件
        ///   - `EquipmentChildren.Count > 0`：身上带着装备的卡（村民/勇士）——整组搬它会拆散装备
        ///   - `IsWorking` / `WorkerChildren.Count > 0`：在建筑里干活的卡（同理，避免把它从建筑里"拔"出来）
        /// </summary>
        private static bool IsSelectable(GameCard card)
        {
            if (!IsStaticallySelectable(card))
            {
                return false;
            }
            // 只有"能不能被加进选择"才看动态的拖拽状态；清理选择时绝不能看它（见下）
            if (!card.CanBeDragged()) return false;   // 战斗中等情况
            return true;
        }

        /// <summary>
        /// 只检查**静态**条件（不会因为卡自己"正在被拖"而变假）：存在、是一叠的根卡、不是装备件、
        /// 没在建筑里干活、身上没挂装备或工人。
        ///
        /// ⚠️ 这里**故意不调用 `card.CanBeDragged()`**。原版实现是
        ///     `CardData.CanBeDragged` 本身就带着"不在拖拽中"（`!BeingDragged`）这一条。
        /// 也就是说**一张卡一旦被拖起来，`CanBeDragged()` 立刻变成 false**。
        /// 第一版 `PruneSelection()` 每帧都过 `IsSelectable()`（含 `CanBeDragged()`），
        /// 于是"玩家刚开始拖的那张卡"会在同一帧被当成不可选、从选择里删掉 →
        /// 整组接管条件 `Selected.Contains(被拖的卡)` 永远不成立。
        /// 表现：拖一张、其他不跟，松开后高亮消失（当时靠临时屏幕诊断面板打出的 `接管条件: A=1 B=1 C=1 D=0` 定位）。
        /// </summary>
        private static bool IsStaticallySelectable(GameCard card)
        {
            if (card == null || card.Destroyed || card.IsDemoCard)
            {
                return false;
            }
            if (card.Parent != null) return false;    // 只选"一叠的根卡"
            if (card.IsEquipped) return false;        // 装备件本身不参与（它们的位置由原版 IsEquipped 定位接管）
            if (card.IsWorking) return false;         // 在建筑里干活的工人不参与
            // 注意：**不要**排除"身上带装备的卡"（`EquipmentChildren.Count > 0`）。
            // 上一版为了不让装备爆出，把带装备的村民整个排掉了，结果村民根本没法被框选/整组搬运——
            // 而那次"装备爆出"的真实原因是"装备件自己被框进来、被设了 TargetPosition"，
            // 已经由上面那句 `card.IsEquipped` 挡住。带装备的卡本身是正常可拖的，
            // 它的装备件会跟着 `EquipmentHolder.transform.position` 走，不需要我们额外做什么。
            return true;
        }

        /// <summary>框选结算：把屏幕矩形内的散卡加进选择（每次框选都**替换**整个选择，没有追加）。</summary>
        private static void ApplyBoxSelection()
        {
            Selected.Clear();
            Camera camera = Cam();
            if (camera == null)
            {
                return;
            }

            Rect box = ScreenRect(boxStart, boxNow);
            WorldManager world = WorldManager.instance;
            List<GameCard> allCards = world == null ? null : world.AllCards;
            if (allCards == null)
            {
                return;
            }
            for (int i = 0; i < allCards.Count; i++)
            {
                GameCard card = allCards[i];
                if (!IsSelectable(card))
                {
                    continue;
                }
                if (card.MyBoard != world.CurrentBoard) continue; // 只管当前场地
                if (Selected.Contains(card)) continue;

                Vector3 screen = camera.WorldToScreenPoint(card.transform.position);
                if (screen.z < 0f) continue;
                if (box.Contains(new Vector2(screen.x, screen.y)))
                {
                    Selected.Add(card);
                }
            }
        }

        /// <summary>清掉已经不存在的卡；选择里若混进了子卡，就换成它那一叠的根卡。</summary>
        private static void PruneSelection()
        {
            for (int i = Selected.Count - 1; i >= 0; i--)
            {
                GameCard card = Selected[i];
                if (card == null || card.Destroyed)
                {
                    Selected.RemoveAt(i);
                    continue;
                }
                GameCard root = card.GetRootCard();
                if (root == null || root.Destroyed)
                {
                    Selected.RemoveAt(i);
                    continue;
                }
                // 变成子卡了就换成它那一叠的根卡；根卡本身若不再可选（例如中途穿上了装备、被放进建筑干活），
                // 就把整个选择项丢掉，免得拖的时候把装备/工人拆下来。
                // 清理时只看静态条件：一旦用了 IsSelectable（含 CanBeDragged），正在被拖的那张会被自己删掉
                if (root != card)
                {
                    Selected[i] = IsStaticallySelectable(root) ? root : null;
                }
                else if (!IsStaticallySelectable(card))
                {
                    Selected[i] = null;
                }
            }
            for (int i = Selected.Count - 1; i >= 0; i--)
            {
                if (Selected[i] == null)
                {
                    Selected.RemoveAt(i);
                }
            }
            // 去重（根卡可能被填进来两次）
            for (int i = Selected.Count - 1; i >= 0; i--)
            {
                if (Selected.IndexOf(Selected[i]) != i)
                {
                    Selected.RemoveAt(i);
                }
            }
        }

        // ------------------------------------------------------------------
        //  整组拖动（复用原版拖拽）
        // ------------------------------------------------------------------

        private static void UpdateGroupDrag(WorldManager world)
        {
            if (!dragging)
            {
                GameCard dragged = world.DraggingCard;
                bool inSelection = dragged != null && Selected.Contains(dragged);

                // 玩家开始拖的是"被选中的那一张"，而且原版没在做 Shift 整叠拖 → 接成整组拖
                if (dragged != null && !world.IsShiftDragging && Selected.Count > 1 && inSelection)
                {
                    BeginDrag(dragged);
                }
                return;
            }

            if (world.DraggingDraggable == null || primary == null || primary.Destroyed)
            {
                EndDrag();
                return;
            }

            Vector3 anchor = primary.TargetPosition;
            for (int i = 0; i < DragCards.Count; i++)
            {
                GameCard card = DragCards[i];
                if (card == null || card.Destroyed)
                {
                    continue;
                }
                card.TargetPosition = anchor + DragOffsets[i];
            }
        }

        private static void BeginDrag(GameCard draggedCard)
        {
            primary = draggedCard;
            DragCards.Clear();
            DragOffsets.Clear();

            // 先把"整组拖"标志置真再逐张 StartDragging()：下面那个循环里如果玩家手里还按着
            // Shift/Ctrl，`ChestDragOutPatch` 会把"拖箱子"误判成"从箱子里拖出材料"（见 GroupDragging）。
            // 循环结束后按实际情况（是否真收到同组卡）再定一次。
            dragging = true;

            Vector3 anchor = primary.transform.position;
            for (int i = 0; i < Selected.Count; i++)
            {
                GameCard card = Selected[i];
                if (card == null || card == primary || !IsSelectable(card))
                {
                    continue; // 装备件本身 / 在建筑里干活的 / 战斗中等，一律不跟着搬
                }
                if (DragCards.Count >= ModConfig.BoxSelectMaxCards)
                {
                    break; // 上限，防止一次搬太多
                }
                DragCards.Add(card);
                DragOffsets.Add(card.transform.position - anchor);
                card.StartDragging();
            }

            // ⚠️ 必须在这里把 dragging 置真：否则下一帧 UpdateGroupDrag 仍走"未接管"分支，
            // 这些卡就永远没人给它们设 TargetPosition（症状：只有被拖的那张跟手、其他都不动），
            // 而且 EndDrag 也不会被触发（因为它的守卫也是 dragging）。
            dragging = DragCards.Count > 0;
            if (!dragging)
            {
                primary = null;
            }
        }

        /// <summary>
        /// 结束整组拖动：只把同组卡的拖拽标记收干净，剩下的都交回原版。
        ///
        /// ⚠️ 这里**故意不调原版的 `WorldManager.DropCard()` / `Draggable.StopDragging()`**（第一版调过，是错的）：
        ///   ① 松手那一帧，原版 `WorldManager.Update` 已经把主卡走完了 `DropCard()`
        ///      （落点吸附 + 自动堆叠 + 音效），并且 `SetDraggingDraggableToNull()` 会把**所有**
        ///      `Draggable.BeingDragged` 清成 false。我们这时候再调一次 `DropCard()` 是空转
        ///      （`DraggingDraggable` 已是 null）。
        ///   ② 若为了"让同组卡也各自吸附"而把 `DraggingDraggable` 逐张换成同组卡再调 `DropCard()`，
        ///      用的是**主卡那一帧算出来的** `WorldManager.NearbyCardTarget`（一个落点目标），
        ///      会把别的卡吸到不该去的地方；而且 `DropCard()` 末尾的 `StopDragging()` 在
        ///      "本次按压没达到拖拽阈值"时会顺手触发卡自己的 `Clicked()`——箱子的 `Clicked()` 就是取物，
        ///      框选搬个家顺手把箱子打开是不可接受的。
        ///   所以同组卡就保持各自偏移后的落点（与参考 mod **Multi Select & Drag** 的做法一致）。
        ///
        /// 为什么清标记就够了：`GameCard.UpdatePosition()` 里 `PositionType.IsRoot` 分支是
        /// "朝 `TargetPosition` 插值"，而同组卡的 `TargetPosition` 正是我们每帧设的偏移落点，
        /// `BeingDragged` 一清，它们就停在原地不再跟手。
        /// </summary>
        private static void EndDrag()
        {
            for (int i = 0; i < DragCards.Count; i++)
            {
                ClearDragState(DragCards[i]);
            }

            // 主卡正常由原版收尾（松手那一帧它已经 `StopDragging()` 过了）；只有"拖到一半关掉开关 /
            // 抛异常"这种原版没来得及收尾的路径才会走到下面，兜底清一下，否则那张卡会一直是
            // `BeingDragged`，以后就再也拖不动了。
            if (primary != null && !primary.Destroyed && primary.BeingDragged
                && (WorldManager.instance == null || WorldManager.instance.DraggingDraggable != primary))
            {
                ClearDragState(primary);
            }

            DragCards.Clear();
            DragOffsets.Clear();
            dragging = false;
            primary = null;
            // ⚠️ 这里**不清**选择集：0.5.5 用户要求"搬完一轮后高亮/选中状态保留，可以再拖一趟，
            // 直到点空白处才取消"（早先那版是搬完就清掉，属于同一交互被改口，别再改回去）。
        }

        /// <summary>
        /// 清掉一张卡（连同叠在它上面的子卡）的拖拽标记。
        /// 为什么要连子卡一起清：`GameCard.StartDragging()` 会把整叠卡的 `BeingDragged` 都置真。
        /// 为什么不用原版的 `StopDragging()`：它在"本次按压没达到拖拽阈值"时会顺手调卡自己的
        /// `Clicked()`（箱子取物、开包），框选搬家时误触发这些副作用是不可接受的。
        /// </summary>
        private static void ClearDragState(GameCard card)
        {
            GameCard current = card;
            while (current != null && !current.Destroyed)
            {
                current.BeingDragged = false;
                current.ClickedObject = null;
                current.DragTag = null;
                current = current.Child;
            }
        }

        /// <summary>关掉功能或出错时用的"直接清干净"，不走原版收起（避免在异常路径里再抛）。</summary>
        private static void ClearSelectionImmediate()
        {
            for (int i = 0; i < Selected.Count; i++)
            {
                GameCard card = Selected[i];
                if (card != null && !card.Destroyed)
                {
                    card.BeingDragged = false;
                }
            }
            Selected.Clear();
        }

        // ------------------------------------------------------------------
        //  覆盖层
        // ------------------------------------------------------------------

        internal static void DrawOverlay()
        {
            if (!ModConfig.BoxSelect || Event.current == null || Event.current.type != EventType.Repaint)
            {
                return;
            }
            try
            {
                EnsureWhiteTexture();
                Camera camera = Cam();
                if (camera != null && Selected.Count > 0)
                {
                    for (int i = 0; i < Selected.Count; i++)
                    {
                        GameCard card = Selected[i];
                        if (card == null || card.Destroyed)
                        {
                            continue;
                        }
                        Rect rect;
                        if (TryCardRect(camera, card, out rect))
                        {
                            DrawBorder(rect, BorderThickness, SelectedLine);
                        }
                    }
                }
                if (boxing)
                {
                    Rect rect = ToGui(ScreenRect(boxStart, boxNow));
                    DrawRect(rect, BoxFill);
                    DrawBorder(rect, BorderThickness, BoxLine);
                }
            }
            catch
            {
                // OnGUI 里出错会被 Unity 反复调用，这里静默吞掉（下一次 Tick 会重置状态）
            }
        }

        private static void EnsureWhiteTexture()
        {
            if (whiteTexture != null)
            {
                return;
            }
            whiteTexture = new Texture2D(1, 1);
            whiteTexture.SetPixel(0, 0, Color.white);
            whiteTexture.Apply();
            whiteTexture.hideFlags = HideFlags.HideAndDontSave;
        }

        private static Camera Cam()
        {
            if (cachedCamera == null && GameCamera.instance != null)
            {
                cachedCamera = GameCamera.instance.GetComponent<Camera>();
            }
            if (cachedCamera == null)
            {
                cachedCamera = Camera.main;
            }
            return cachedCamera;
        }

        /// <summary>一张卡在屏幕上的包围盒（8 个角投影后取 min/max）。</summary>
        private static bool TryCardRect(Camera camera, GameCard card, out Rect rect)
        {
            rect = default(Rect);
            Bounds bounds;
            try
            {
                bounds = card.GetBounds();
            }
            catch
            {
                return false;
            }

            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = new Vector3(
                    (i & 1) == 0 ? bounds.min.x : bounds.max.x,
                    (i & 2) == 0 ? bounds.min.y : bounds.max.y,
                    (i & 4) == 0 ? bounds.min.z : bounds.max.z);
                Vector3 screen = camera.WorldToScreenPoint(corner);
                if (screen.z < 0f)
                {
                    return false;
                }
                minX = Mathf.Min(minX, screen.x);
                minY = Mathf.Min(minY, screen.y);
                maxX = Mathf.Max(maxX, screen.x);
                maxY = Mathf.Max(maxY, screen.y);
            }
            rect = ToGui(new Rect(minX, minY, maxX - minX, maxY - minY));
            return true;
        }

        private static Rect ScreenRect(Vector2 a, Vector2 b)
        {
            return new Rect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
        }

        /// <summary>屏幕坐标（左下原点）→ GUI 坐标（左上原点）。</summary>
        private static Rect ToGui(Rect rect)
        {
            return new Rect(rect.x, Screen.height - rect.y - rect.height, rect.width, rect.height);
        }

        private static void DrawRect(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, whiteTexture);
            GUI.color = previous;
        }

        private static void DrawBorder(Rect rect, float thickness, Color color)
        {
            DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }
    }

    /// <summary>`WorldManager.Update` 之后跑框选主循环（和本项目其它 Update 后置补丁同一套路）。</summary>
    [HarmonyPatch(typeof(WorldManager), "Update")]
    internal static class BoxSelectTickPatch
    {
        [HarmonyPostfix]
        internal static void Postfix()
        {
            if (!ModConfig.BoxSelect)
            {
                return;
            }
            BoxSelect.Tick();
        }
    }

    /// <summary>
    /// 点牌堆**任意一张**都能拖动整个选中组（而不只是点最底下那张根卡）。
    ///
    /// 用户原话（0.5.5）：「"ctrl+左键 框选并拖动多张卡"，这个功能在选中卡后，要想移动，只能点击单张卡
    /// 或者牌堆中最下面的那张卡，我想去掉只能点牌堆最下面那张卡的限制，改成"点击牌堆的任意位置"也能实现移动」。
    ///
    /// 为什么原来只能点根卡：`BoxSelect` 的选择集里存的是**一叠的根卡**（`Parent == null`），
    /// 整组接管的判据是 `Selected.Contains(WorldManager.DraggingCard)`。玩家点牌堆**中间/上面**那张时，
    /// 原版拖的是**那张子卡**（`GameCard.StartDragging()` 会把它 `SetParent(null)` 从堆里拆出来），
    /// 它不在选择集里 → 接管条件不成立 → 只是把那一张拖出来。点根卡时才是原版抓整叠、也能匹配上选择集。
    ///
    /// 做法：**在 `StartDragging()` 之前把它改写成"抓这一叠的根卡"**——这时 `__instance.Parent` 还没被解除，
    /// `GetRootCard()` 拿到的就是原版那一叠真正的根（也就是选择集里存的那张）。改写之后
    /// `DraggingCard` 就是根卡 → 既落在选择集里，又天然带着整叠的子卡 → 下一帧 `UpdateGroupDrag` 正常接管整组拖。
    ///
    /// 三条边界：
    ///  - **只在"选了不止一张"时改写**（`Selected.Count > 1`）：这正是用户说的"框选并拖动多张卡"；
    ///    只选了一张时保持原版语义（点子卡 = 把上面那半拆下来）。
    ///  - **只认左键拖**：右键拖是本项目自己的 `RightDragStackPatch`（它抓的本来就是根卡）。
    ///  - **`Priority.Last` + "别人已经改过就不插手"**：本项目 `ChestDragOutPatch` 也在这同一个方法上打前缀
    ///    （且优先级是 `First`）。HarmonyX 里前缀**全都会跑**，所以谁最后设 `DraggingDraggable` 谁生效；
    ///    我们排在最后跑、并检查 `DraggingDraggable` 还是不是 `__instance`——如果它已经被换掉
    ///    （例如"Ctrl+点箱子=拖出材料"那条路径已经接管），就说明这次拖拽另有其人，直接让路。
    /// </summary>
    [HarmonyPatch(typeof(GameCard), "StartDragging")]
    internal static class BoxSelectStackTakeoverPatch
    {
        [HarmonyPriority(Priority.Last)]
        [HarmonyPrefix]
        internal static bool Prefix(GameCard __instance)
        {
            if (!ModConfig.BoxSelect || __instance == null || __instance.Destroyed)
            {
                return true;
            }
            if (BoxSelect.GroupDragging)
            {
                return true; // 已经在整组拖了（这是 BeginDrag 逐张 StartDragging 的那几次调用）
            }
            if (!BoxSelect.CanTakeOverSelectedStackDrag(__instance))
            {
                return true;
            }

            WorldManager world = WorldManager.instance;
            GameCard root = __instance.GetRootCard();
            if (world == null || root == null || root.Destroyed || root == __instance)
            {
                return true;
            }

            // 把原版这次"拖子卡"改写成"拖这一叠的根卡"（`StartDragging()` 之后子卡才会被拆出来，
            // 所以此刻仍能取到真正的根）。之后跟随鼠标/落点吸附/松开落地全部由原版负责。
            world.DraggingDraggable = root;
            world.grabOffset = world.mouseWorldPosition - root.transform.position;
            root.StartDragging(); // 对根卡再进一次本前缀：`root == __instance`，会直接放行
            return false;
        }
    }

    /// <summary>Ctrl 按住时挡掉卡片自身的 `Clicked()`（例如箱子取物）。</summary>
    /// <remarks>
    /// 注意这里**不是**为了做"Ctrl+点击增减选择"——那个能力已按用户要求删掉了（见 `BoxSelect` 类注释）。
    /// 保留它是为了避免 Ctrl+点一下就把箱子打开/把卡包开掉，那正是用户说的"快捷键冲突"来源。
    /// </remarks>
    [HarmonyPatch(typeof(GameCard), "Clicked")]
    internal static class BoxSelectCardClickPatch
    {
        [HarmonyPrefix]
        internal static bool Prefix()
        {
            return !ModConfig.BoxSelect || !BoxSelect.CtrlDown();
        }
    }

    /// <summary>Ctrl 按住时挡掉卡包自身的 `Clicked()`（否则会顺手开包）。</summary>
    [HarmonyPatch(typeof(Boosterpack), "Clicked")]
    internal static class BoxSelectBoosterClickPatch
    {
        [HarmonyPrefix]
        internal static bool Prefix()
        {
            return !ModConfig.BoxSelect || !BoxSelect.CtrlDown();
        }
    }

    /// <summary>Ctrl+拖空白本来是"平移镜头"，框选时要挡掉（原版这个方法就是处理平移起手的）。</summary>
    [HarmonyPatch(typeof(GameCamera), "StartDragging")]
    internal static class BoxSelectCameraPatch
    {
        [HarmonyPrefix]
        internal static bool Prefix()
        {
            return !ModConfig.BoxSelect || !BoxSelect.CtrlDown();
        }
    }
}
