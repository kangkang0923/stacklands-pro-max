using System;
using HarmonyLib;
using UnityEngine;

namespace StacklandsProMaxNS
{
    /// <summary>
    /// **魔法收集器**（`stacklandspro_magic_collector`，0.5.8 新增卡牌）。
    ///
    /// 用户需求：「主意为 1 木板 1 石砖 1 铁条 1 金条 1 玻璃、3 魔法尘埃；
    /// 将魔法师叠在魔法收集器上，30 秒产出一个魔法尘埃；魔法收集器有和采石场类似的运输输出」。
    ///
    /// ── 实现对照（全部反编译核实） ────────────────────────────────────────────
    /// - **产卡 + 送出**：对齐原版采石场 `CombatableHarvestable.CompleteHarvest()` 的形状——
    ///   `CreateCard(位置, 产物, faceUp: false, checkAddToStack: false)` +
    ///   `WorldManager.StackSendCheckTarget(MyGameCard, 产物, OutputDir)`。
    ///   `StackSendCheckTarget` 就是原版"运输输出"的完整管线：先试 Cities 管道 → 再试磁铁 →
    ///   `TrySendToChest`（2 格内能收它的箱子自动收）→ 都没有才朝 OutputDir 弹出去。
    ///   （0.5.8 早期版本曾照采石场加过"可点击的输出方向箭头"，按用户要求去掉了。）
    /// - **管道运输输出**（0.5.9 用户追加：「加一个和铁矿、高级温室、磁力箱一样的运输输出」）：
    ///   管道那一路不需要写代码——`StackSendCheckTarget` 的第一步 `TrySendWithPipe` 只认
    ///   `origin.CardConnectorChildren` 里的 Transport 输出连接器，所以只要在
    ///   `Cards\magic_collector.json` 里声明一路 `_EnergyConnectors`
    ///   （`EnergyConnectionType = 1`＝`CardDirection.output`、`EnergyConnectionStrength = 5`＝
    ///   `ConnectionType.Transport`、`EnergyConnectionAmount = 1`，和高级温室完全一样），
    ///   接上管道后魔法尘埃就会顺着管道送过去。
    ///   ⚠️ 连接器只在 Cities 场地显示/可接（原版 `GameCard.UpdateConnectors()` 的规则）；
    ///   没接管道时行为和以前完全一样（磁铁 → 配套箱子 → 朝 OutputDir 弹出）。
    /// - **魔法师不消耗**：需求只说"叠上去 30 秒产一个"，产出后魔法师留在收集器上，
    ///   `UpdateCard` 下一帧重新启动计时 → 每 30 秒自动产一个；把魔法师拖走计时就停。
    ///   接受 `wizard`（Island 魔法师）和 `mage`（Cities 法师）两个 id——中文界面里两者都可能显示为"魔法师"。
    /// </summary>
    public class MagicCollectorCard : CardData
    {
        /// <summary>卡 id（ModConfig 的总开关与卡包登记都用它，别处不要写死字符串）。</summary>
        internal const string CardId = "stacklandspro_magic_collector";

        /// <summary>一次产出周期（秒）。</summary>
        internal const float CraftTimeSeconds = 30f;

        /// <summary>vanilla 魔法师的卡 id：`wizard`（Island 的魔法师）；`mage`（Cities 的法师）中文界面也可能显示为"魔法师"，一并收下。</summary>
        private static readonly string[] WizardIds = { "wizard", "mage" };

        /// <summary>产物：vanilla 魔法尘埃（`CardIds.magic_dust`）。</summary>
        private const string MagicDustId = "magic_dust";

        /// <summary>和原版饲养栏一个道理：自己是建筑、要允许卡往身上叠（基类默认按"资源堆"处理会拒绝）。</summary>
        public override bool DetermineCanHaveCardsWhenIsRoot => true;

        protected override bool CanHaveCard(CardData otherCard)
        {
            if (otherCard == null)
            {
                return false;
            }
            foreach (string wizardId in WizardIds)
            {
                if (otherCard.Id == wizardId)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>计时期间也允许把魔法师放上来/取下来（原版 Hotpot 同款做法）。</summary>
        public override bool CanHaveCardsWhileHasStatus()
        {
            return true;
        }

        public override void UpdateCard()
        {
            bool hasWizard = MyGameCard.HasChild && MyGameCard.Child.CardData != null
                && (Array.IndexOf(WizardIds, MyGameCard.Child.CardData.Id) >= 0);
            if (hasWizard && !MyGameCard.TimerRunning)
            {
                MyGameCard.StartTimer(CraftTimeSeconds, MakeMagicDust,
                    SokLoc.Translate("stacklandspro_magic_collector_status"), GetActionId("MakeMagicDust"));
            }
            else if (!hasWizard && MyGameCard.TimerRunning)
            {
                MyGameCard.CancelTimer(GetActionId("MakeMagicDust"));
            }

            base.UpdateCard();
        }

        [TimedAction("make_magic_dust")]
        public void MakeMagicDust()
        {
            // 对齐采石场 CompleteHarvest 的产出形状；魔法师留在收集器上，下一轮自动开始
            CardData dust = WorldManager.instance.CreateCard(MyGameCard.transform.position,
                MagicDustId, faceUp: false, checkAddToStack: false);
            if (dust == null)
            {
                return;
            }
            WorldManager.instance.StackSendCheckTarget(MyGameCard, dust.MyGameCard, OutputDir);
            QuestManager.instance.SpecialActionComplete("magic_collector_produced");
        }
    }
}
