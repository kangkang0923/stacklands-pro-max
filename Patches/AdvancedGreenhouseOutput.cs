using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace StacklandsProMaxNS
{
    /// <summary>
    /// **高级温室的卡片脚本**（0.5.6 追加：输出那一半）。
    ///
    /// 用户需求原话：「高级温室要有运输输出，一个就行，产出的6个胡萝卜通过这个输出到指定地方（炖锅、箱子等）」，
    /// 我第一版只给卡加了原版 `_EnergyConnectors`（Cities 管道），用户随即指出：
    /// 「main场地也有运输输出，比如冶炼厂、锯木厂、采石场等等，你是不是用错了」——**他说得对**。
    ///
    /// 反编译核实，原版其实是**两套不同的东西**：
    ///   ① **冶炼厂 / 锯木厂 / 采石场 / 农田…**（主大陆、岛上都能用）：卡上有"输出方向"——
    ///      卡上有个带 `[ExtraData("output_direction")]` 的 `Vector3 OutputDir` 字段（存档里能看到它）＋卡片旁边那个
    ///      **可点击的小箭头**（`DirectionCircleElement.Clicked()` → `GameCard.ToggleDirection()` 循环转方向）。
    ///      产物由 `WorldManager.StackSendCheckTarget(MyGameCard, 产物, OutputDir, …)` 送出去：
    ///      先试管道（Cities 才有）→ 再试磁铁 → 再 `StackSend`（`TrySendToChest`：**2 格内配套的箱子自动收**）→
    ///      最后朝 `OutputDir` 抛出去（`GameCard.SendIt()` 里 `Velocity = GetRootCard().CardData.OutputDir * 7f`，
    ///      注意这一句在非 Cities 场地**不生效**，所以主大陆上那个箭头主要是"外观 + 决定送进哪个方向的容器"）。
    ///   ② **储物箱的"运输输出"连接器**（`_EnergyConnectors`，Cities 场地的管道）：`GameCard.UpdateConnectors()`
    ///      里 `CurrentBoard.Id != "cities"` 就把连接器**全部隐藏**，所以主大陆上根本看不到、也接不上。
    ///
    /// 所以本文件给高级温室补上 ① 的那一套：
    ///   - `CanSelectOutput()` 重写成 true（和 `Factory`/`EnergyConsumer`/`Farmland` 一致）；
    ///   - 运行时**从原版卡上把那个箭头借过来**装到自己身上（mod 卡是运行时 `AddComponent` 建出来的、
    ///     没有预制体，原版箭头是挂在各卡预制体里的子物体，所以只能运行期从原版卡的预制体上 `Instantiate` 一份，
    ///     本项目早有同类做法：磁力箱的方形内容框、信用卡的外观都是从原版对象上复制引用的）；
    ///   - 产物**直接送进温室旁边的容器**（配套箱子 / 炖锅等，`CanHaveCardOnTop` 认可的就算），
    ///     优先选箭头方向上那个；没有可放的地方就完全交回原版（照旧往外弹）。
    /// </summary>
    public class AdvancedGreenhouseCard : Greenhouse
    {
        /// <summary>
        /// "运输输出"的投放半径（格）：JSON 里 `_OutputRadius` 可调（默认 2.5，比原版 `TrySendToChest` 的 2.0 略宽一点点）。
        /// </summary>
        public float OutputRadius = 2.5f;

        /// <summary>和冶炼厂/锯木厂/采石场一样：允许玩家点箭头选输出方向。</summary>
        protected override bool CanSelectOutput()
        {
            return true;
        }

        public override void UpdateCard()
        {
            base.UpdateCard();
            AdvancedGreenhouseOutput.EnsureOutputArrow(MyGameCard);
        }
    }

    /// <summary>高级温室"运输输出"的实现（箭头借用 + 产物投放）。</summary>
    internal static class AdvancedGreenhouseOutput
    {
        /// <summary>已经装好箭头的卡（按 `UniqueId` 记，避免每帧重复找）。</summary>
        private static readonly HashSet<string> ArrowReady = new HashSet<string>();

        /// <summary>这些卡是"种植基质/生产设施"：产物掉上去等于又种回去，绝不能当输出目标。</summary>
        private static readonly HashSet<string> GrowMatrices = new HashSet<string>
        {
            "soil", "poop", "garden", "farm", "farmland", "greenhouse",
            AdvancedGreenhouse.CardId, "compost", "composter", "brickyard", "breeding_pen"
        };

        /// <summary>从哪几张原版卡上找"输出方向箭头"模板（这些卡预制体里带 `DirectionCircleElement`；找不到就放弃、只打一条警告）。</summary>
        private static readonly string[] ArrowTemplateIds =
        {
            "greenhouse", "sawmill", "smelter", "quarry", "farmland", "composter", "brickyard", "breeding_pen"
        };

        private static float nextTryTime;
        private static bool loggedNoTemplate;

        /// <summary>
        /// 让这张高级温室拥有"可点击的输出方向箭头"（幂等；每 1 秒最多试一次，装好了就不再试）。
        /// </summary>
        internal static void EnsureOutputArrow(GameCard card)
        {
            if (card == null || card.CardData == null)
            {
                return;
            }
            string id = card.CardData.UniqueId;
            if (!string.IsNullOrEmpty(id) && ArrowReady.Contains(id))
            {
                return;
            }
            if (Time.unscaledTime < nextTryTime)
            {
                return;
            }
            nextTryTime = Time.unscaledTime + 1f;

            try
            {
                // 万一这张卡自己就带着箭头（例如原版哪天把它放进通用 GameCard 预制体），直接点亮它。
                DirectionCircleElement existing = card.GetComponentInChildren<DirectionCircleElement>(true);
                if (existing != null)
                {
                    existing.ParentCard = card;
                    if (!existing.gameObject.activeSelf)
                    {
                        existing.gameObject.SetActive(true);
                    }
                    Remember(id);
                    return;
                }

                DirectionCircleElement template = FindArrowTemplate();
                if (template == null)
                {
                    if (!loggedNoTemplate)
                    {
                        loggedNoTemplate = true;
                        Debug.LogWarning("[StacklandsProMax] 高级温室：没能在原版卡预制体里找到输出方向箭头"
                            + "（找过：" + string.Join(", ", ArrowTemplateIds) + "），这张卡暂时没有输出方向箭头。");
                    }
                    return;
                }

                DirectionCircleElement clone = UnityEngine.Object.Instantiate(template, card.CardData.transform);
                clone.transform.localPosition = template.transform.localPosition;
                clone.transform.localRotation = template.transform.localRotation;
                clone.transform.localScale = template.transform.localScale;
                clone.ParentCard = card;
                clone.gameObject.SetActive(true);
                Remember(id);
                Debug.Log("[StacklandsProMax] 高级温室：已从原版卡上借用输出方向箭头（点它可以转输出方向）。");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[StacklandsProMax] 高级温室：装输出方向箭头失败（不影响其它功能）：" + ex.Message);
            }
        }

        private static void Remember(string id)
        {
            if (!string.IsNullOrEmpty(id))
            {
                ArrowReady.Add(id);
            }
        }

        private static DirectionCircleElement FindArrowTemplate()
        {
            WorldManager world = WorldManager.instance;
            if (world == null)
            {
                return null;
            }
            for (int i = 0; i < ArrowTemplateIds.Length; i++)
            {
                CardData prefab = world.GetCardPrefab(ArrowTemplateIds[i], false);
                if (prefab == null)
                {
                    continue;
                }
                DirectionCircleElement element = prefab.GetComponentInChildren<DirectionCircleElement>(true);
                if (element != null)
                {
                    return element;
                }
            }
            return null;
        }

        /// <summary>
        /// 找"产物该送进哪个容器"：同一场地、半径内、`CanHaveCardOnTop` 认可、且不是种植基质/自己这堆。
        /// 优先返回**箭头方向上**最近的那个；方向上没有就退而取半径内最近的（和原版 `TrySendToChest` 的口径一致）。
        /// </summary>
        internal static GameCard FindOutputTarget(GameCard origin, GameCard product, float radius)
        {
            WorldManager world = WorldManager.instance;
            if (world == null || origin == null || product == null || product.CardData == null || origin.CardData == null)
            {
                return null;
            }
            if (radius <= 0f)
            {
                radius = 2.5f;
            }
            Vector3 originPos = origin.transform.position;
            Vector3 dir = origin.CardData.OutputDir;
            List<GameCard> cards = world.AllCards;
            GameCard bestAny = null;
            GameCard bestInDirection = null;
            float bestAnyDistance = float.MaxValue;
            float bestDirectionDistance = float.MaxValue;

            for (int i = 0; i < cards.Count; i++)
            {
                GameCard candidate = cards[i];
                if (candidate == null || candidate.CardData == null || candidate == origin)
                {
                    continue;
                }
                if (candidate.MyBoard == null || candidate.MyBoard != origin.MyBoard)
                {
                    continue;
                }
                if (GrowMatrices.Contains(candidate.CardData.Id))
                {
                    continue;
                }
                try
                {
                    if (candidate.IsPartOfSameStack(origin))
                    {
                        continue;
                    }
                }
                catch (Exception)
                {
                    continue;
                }
                if (!candidate.CardData.CanHaveCardOnTop(product.CardData))
                {
                    continue;
                }
                Vector3 delta = candidate.transform.position - originPos;
                delta.y = 0f;
                float distance = delta.magnitude;
                if (distance > radius)
                {
                    continue;
                }
                if (distance < bestAnyDistance)
                {
                    bestAnyDistance = distance;
                    bestAny = candidate;
                }
                if (dir != Vector3.zero && delta.sqrMagnitude > 0.0001f
                    && Vector3.Dot(new Vector3(dir.x, 0f, dir.z), delta) > 0f && distance < bestDirectionDistance)
                {
                    bestDirectionDistance = distance;
                    bestInDirection = candidate;
                }
            }
            return bestInDirection != null ? bestInDirection : bestAny;
        }

        /// <summary>本卡有没有任何一路"运输输出"真的接上了管道？（挂上了就交回原版走管道，别抢。）</summary>
        internal static bool HasConnectedTransportOutput(GameCard card)
        {
            if (card == null || card.CardConnectorChildren == null)
            {
                return false;
            }
            for (int i = 0; i < card.CardConnectorChildren.Count; i++)
            {
                CardConnector connector = card.CardConnectorChildren[i];
                if (connector != null && connector.ConnectionType == ConnectionType.Transport
                    && connector.CardDirection == CardDirection.output && connector.ConnectedNode != null)
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>
    /// **高级温室的产物投放**：原版 `Blueprint.BlueprintComplete()` 收尾时会对"牌堆里有运输输出的那张卡"调用
    /// `StackSendCheckTarget(温室, 产物叠, OutputDir, 温室)`。这里在这句话之前插一手：
    /// 温室旁边（半径内、优先箭头方向）有能放的容器（配套箱子 / 炖锅…）就直接送进去，
    /// 送不进去就原样交回原版（照旧往外弹）——所以这是纯增量，不改任何原版行为。
    /// </summary>
    [HarmonyPatch(typeof(WorldManager), "StackSendCheckTarget")]
    internal static class AdvancedGreenhouseOutputPatch
    {
        private static bool loggedError;

        private static bool Prefix(WorldManager __instance, GameCard origin, GameCard myCard, GameCard initialParent)
        {
            try
            {
                if (__instance == null || origin == null || myCard == null || origin.CardData == null)
                {
                    return true;
                }
                if (origin.CardData.Id != AdvancedGreenhouse.CardId || initialParent != origin)
                {
                    return true;
                }
                // 管道接上了就让原版按管道送（Cities 场景）
                if (AdvancedGreenhouseOutput.HasConnectedTransportOutput(origin))
                {
                    return true;
                }
                AdvancedGreenhouseCard greenhouse = origin.CardData as AdvancedGreenhouseCard;
                float radius = greenhouse != null ? greenhouse.OutputRadius : 2.5f;
                GameCard target = AdvancedGreenhouseOutput.FindOutputTarget(origin, myCard, radius);
                if (target == null)
                {
                    return true;
                }
                __instance.StackSendTo(myCard, target.GetLeafCard());
                return false;
            }
            catch (Exception ex)
            {
                if (!loggedError)
                {
                    loggedError = true;
                    Debug.LogWarning("[StacklandsProMax] 高级温室输出补丁异常（已放行交回原版）：" + ex);
                }
                return true;
            }
        }
    }
}
