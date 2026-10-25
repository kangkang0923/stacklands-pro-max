using UnityEngine;

namespace StacklandsProMaxNS
{
    /// <summary>
    /// 所有功能的开关和可调参数集中放这里，方便一处修改。
    /// 补丁类是否启用，由 Mod.cs 里的 PatchClasses 列表决定（把对应项注释掉即可禁用该功能）。
    /// </summary>
    internal static class ModSettings
    {
        // ---- 1) 卡牌堆叠上限 ----
        // 0.5.18 起移到 ModConfig.StackLimitSize（设置界面数字输入框，默认 100），这里不再保存

        // ---- 2) 点击一次开完整个卡包 ----
        // 参考 mod：PACKSPLOSION!!!（它用 0.05 秒的协程链逐张开，这样有"炸开"的动画）
        internal const float PackOpenDelay = 0.05f;

        // ---- 3) 打败女巫后不再刷随机传送门 ----
        // 参考 mod：NoWitchNoPortal
        // 生效方式：关闭 EndOfMonthCutscenes.SpecialEvents 判定时的 BoardOptions.CanSpawnPortals

        // ---- 4) 空闲提示红框（村民 / 生产设施）----
        // 参考 mod：Show Idle Villager（它两者都用 Color.red，这里保持红色）
        // 用在 IdleVillagerBorderPatch：村民（0.5.0 合并）与空着的生产设施（0.5.6 合并）
        internal static readonly Color IdleBorderColor = Color.red;

        // ---- 5) 双击把附近同种卡归成一堆（参考 mod：Better Mouse Controls）----
        internal const float DoubleClickMaxDelay = 0.5f;       // 两次点击最长间隔（秒）
        internal const float DoubleClickMaxDistance = 5f;      // 两次点击之间鼠标最多移动多少像素
        internal const float DoubleClickRestackRange = 4f;     // 归堆半径：多远以内的同种卡会被吸过来

        // ---- 6) 磁力箱 / 硬币磁力箱 ----
        // 参数在 Cards/magnet_chest.json、Cards/coin_magnet_chest.json 里用 _ 开头的字段调整，不在这里
    }
}
