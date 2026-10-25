using HarmonyLib;
using UnityEngine;

namespace StacklandsProMaxNS
{
    /// <summary>
    /// 一次性诊断：打印原版 `resource_chest` 和本 mod `magnet_chest` 的**连接器**配置。
    ///
    /// 为什么需要它：游戏的卡牌 JSON（包括 `_EnergyConnectors`）被打包在 asset bundle 里，
    /// 离线阶段读不到原版是怎么配的。有了这行日志，就能直接看出：
    ///   - 原版储物箱有几个"运输输出"连接器
    ///   - 我们声明的 3 个输出连接器有没有真的建出来
    /// 关卡内每当 CurrentBoard 变化时打一次，不会刷屏。
    /// </summary>
    [HarmonyPatch(typeof(WorldManager), "Start")]
    internal static class ConnectorDiagnosticsPatch
    {
        [HarmonyPostfix]
        internal static void Postfix(WorldManager __instance)
        {
            try
            {
                LogConnectors(__instance, "resource_chest");
                LogConnectors(__instance, "coin_chest");
                LogConnectors(__instance, ModConfig.MagnetChestId);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[StacklandsProMax] 连接器诊断失败: " + ex.Message);
            }
        }

        private static void LogConnectors(WorldManager world, string cardId)
        {
            CardData prefab = world.GetCardPrefab(cardId);
            if (prefab == null)
            {
                Debug.Log("[StacklandsProMax] 连接器诊断: 找不到卡 " + cardId);
                return;
            }
            if (prefab.EnergyConnectors == null || prefab.EnergyConnectors.Count == 0)
            {
                Debug.Log("[StacklandsProMax] 连接器诊断: " + cardId + " 没有声明任何连接器");
                return;
            }
            for (int i = 0; i < prefab.EnergyConnectors.Count; i++)
            {
                CardConnectorData data = prefab.EnergyConnectors[i];
                Debug.Log("[StacklandsProMax] 连接器诊断: " + cardId + " [" + i + "] 方向=" + data.EnergyConnectionType
                    + " 类型=" + data.EnergyConnectionStrength + " 数量=" + data.EnergyConnectionAmount);
            }
        }
    }
}
