using HarmonyLib;
using UnityEngine;

namespace StacklandsProMaxNS
{
    /// <summary>
    /// 让磁力箱中间显示原版储物箱那种"方形内容框"。
    ///
    /// 原因：原版是在 GameCard.SetColors() 里**按类名硬判**第二图标（那层方框）的：
    ///     - `ResourceChest` / `FoodWarehouse` 拿方形框贴图，`ResourceMagnet` 拿圆形贴图，其余为 null；
    ///     - 再把这个"有没有第二图标"以及贴图本身写进材质属性。
    /// MagnetChest 两者都不是，所以拿不到方框，卡片中间只剩底层图标（看起来是个圆）。
    ///
    /// 这里在 SetColors 之后补一层：给磁力箱塞上和原版储物箱完全相同的第二图标贴图。
    /// 因为主图标(HeldCardId 的图标 / 空图标)与第二图标此时都和原版储物箱一致，
    /// 渲染结果也就和原版储物箱一致了。
    ///
    /// 用 Postfix 而不是去改类继承：不动已经验证过的存储/吸附逻辑，也不影响老存档里的字段。
    /// SetColors 每帧都会被 GameCard.Update 调用，所以这里也是每帧刷新，不会掉。
    /// </summary>
    [HarmonyPatch(typeof(GameCard), "SetColors")]
    internal static class MagnetChestLookPatch
    {
        private static readonly int HasSecondaryIconId = Shader.PropertyToID("_HasSecondaryIcon");
        private static readonly int SecondaryTexId = Shader.PropertyToID("_SecondaryTex");
        private static readonly MaterialPropertyBlock Block = new MaterialPropertyBlock();

        [HarmonyPostfix]
        internal static void Postfix(GameCard __instance)
        {
            if (__instance == null || !(__instance.CardData is MagnetChest))
            {
                return;
            }
            if (__instance.CardRenderer == null || SpriteManager.instance == null)
            {
                return;
            }

            Sprite chestFrame = SpriteManager.instance.ChestIconSecondary;
            if (chestFrame == null || chestFrame.texture == null)
            {
                return;
            }

            // 原版 SetColors 用的是材质索引 2 的 property block，这里照做
            __instance.CardRenderer.GetPropertyBlock(Block, 2);
            Block.SetFloat(HasSecondaryIconId, 1f);
            Block.SetTexture(SecondaryTexId, chestFrame.texture);
            __instance.CardRenderer.SetPropertyBlock(Block, 2);
        }
    }
}
