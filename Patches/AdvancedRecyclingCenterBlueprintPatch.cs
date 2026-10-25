using HarmonyLib;

namespace StacklandsProMaxNS
{
    /// <summary>
    /// 高级回收中心的合成计时器：**只在这堆回收中心全部"停摆"时才允许读条**。
    ///
    /// ── 用户定的规矩（0.6.4）────────────────────────────────────────────────
    ///   「回收中心在合成高级回收中心时需要有限制：要断开电力、要取出回收中心里的人物，
    ///     不然的话就不读进度条。」
    ///   → 堆里**任何一张**回收中心还接着电（LV/HV 有 `ConnectedNode`），或者堆里**还有工人**，
    ///     就不许起计时器（已在读的也立刻停掉）。玩家拆线/拿走工人的瞬间，
    ///     `NotifyEnergyConsumers` / 堆叠变化会把 `StackUpdate` 置真 → `CheckBlueprintInStack` 重新判定，
    ///     所以不需要任何轮询，条件一满足进度条就自己出来。
    ///
    /// ── 为什么还要 `skipWorkerEnergyCheck`（0.6.3 修的"5 张叠好没反应"）─────────
    ///   `StartBlueprintTimer` 会拿**堆底那张原版回收中心**去过两道闸：
    ///     ① `ShouldStartTimerWorkers` → `WorkerAmount = 2`，堆上没工人就永远起不来；
    ///     ② `ShouldStartTimerEnergy` → 声明了高压电输入就要求每一路都接上并通电。
    ///   这两道闸的语义和我们要的正好相反（它要工人、我们要没工人；它要通电、我们要断电），
    ///   所以本补丁自己判（上面那两条硬限制），判过了就把 `skipWorkerEnergyCheck` 强制置真，
    ///   让原版那两道闸让路。`SkipCitiesChecks` 同时挡住 `EnergyConsumer.UpdateCard` /
    ///   `CardData.UpdateCard` 里"工人或能源不达标就 `CancelAnyTimer`"的判定 → 20 秒读条能走完。
    ///
    ///   ⚠️ 只认本 mod 这条蓝图的 id，别的配方（包括其它 mod 的）完全不受影响。
    ///   ⚠️ 为什么不写进蓝图 JSON：`GameDataLoader.LoadModBlueprint` 根本不处理 JSON 里的
    ///      `_字段`（只有 `LoadModCard` 才用反射写），写 `_IgnoreEnergyWorkerDemand` 只会被忽略。
    /// </summary>
    [HarmonyPatch(typeof(GameCard), "StartBlueprintTimer")]
    internal static class AdvancedRecyclingCenterBlueprintPatch
    {
        [HarmonyPrefix]
        internal static bool Prefix(GameCard __instance, string blueprintId, ref bool skipWorkerEnergyCheck)
        {
            if (blueprintId != AdvancedRecyclingCenter.BlueprintId)
            {
                return true; // 别的配方，原样交给原版
            }

            if (__instance == null)
            {
                return true;
            }

            // 硬限制：这堆回收中心必须已经停摆——电断开、里面的人取出来
            if (HasPowerConnected(__instance) || HasWorkerInStack(__instance))
            {
                CancelBlueprintTimer(__instance);
                return false; // 不启动计时器 = 不读进度条
            }

            skipWorkerEnergyCheck = true;
            return true;
        }

        /// <summary>堆里有没有任何一张卡还接着电（LV / HV 连接器已连到节点）。</summary>
        private static bool HasPowerConnected(GameCard root)
        {
            foreach (GameCard card in root.GetAllCardsInStack())
            {
                if (card == null || card.CardConnectorChildren == null)
                {
                    continue;
                }
                foreach (CardConnector connector in card.CardConnectorChildren)
                {
                    if (connector != null && connector.IsEnergyConnector && connector.ConnectedNode != null)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// 这堆卡里还有没有工人——**两条路都要查**：
        ///
        ///   ① 还压在堆里的 `Worker` 卡（`GetAllCardsInStack()` 里能看到）；
        ///   ② **已经"上岗"的工人**：把工人拖到回收中心上时，原版 `CardData.UpdateCard` 会调
        ///      `GameCard.EquipWorker(worker, index)`（反编译核对），里面做的第一件事是
        ///      `worker.MyGameCard.RemoveFromStack()` —— 工人被**从牌堆里摘出去**、只挂在
        ///      所属卡的 `GameCard.WorkerChildren` 列表上（`WorkerHolder` 指回去、`IsWorking = true`）。
        ///      所以 0.6.4 第一版只查 `GetAllCardsInStack()` 漏掉了全部"已上岗"的工人，
        ///      表现就是"放了人物之后还是能读条"。
        /// </summary>
        private static bool HasWorkerInStack(GameCard root)
        {
            foreach (GameCard card in root.GetAllCardsInStack())
            {
                if (card == null)
                {
                    continue;
                }
                if (card.CardData is Worker)
                {
                    return true; // ① 还在堆里的工人
                }
                if (card.WorkerChildren == null)
                {
                    continue;
                }
                for (int i = 0; i < card.WorkerChildren.Count; i++)
                {
                    GameCard worker = card.WorkerChildren[i];
                    if (worker != null && !worker.Destroyed)
                    {
                        return true; // ② 已经在这张卡上"上岗"的工人
                    }
                }
            }
            return false;
        }

        /// <summary>条件在读数途中被破坏（例如玩家中途把电接回去）时，停掉已经在跑的合成计时器。</summary>
        private static void CancelBlueprintTimer(GameCard card)
        {
            if (!card.TimerRunning)
            {
                return;
            }
            // 注意：`GetActionId` 在 `CardData` 上（`finish_blueprint` 是 `CardData.FinishBlueprint` 的 TimedAction）
            string actionId = card.CardData.GetActionId("FinishBlueprint");
            if (card.TimerActionId == actionId)
            {
                card.CancelTimer(actionId);
            }
        }
    }
}
