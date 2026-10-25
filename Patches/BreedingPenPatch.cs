using HarmonyLib;

namespace StacklandsProMaxNS
{
    /// <summary>
    /// 动物留在饲养栏：繁殖完成后不再把两只亲代弹出去。
    ///
    /// 原版 `BreedingPen.BreedAnimals`（反编译核对）在做完"生成幼崽、送往输出方向"之后，
    /// 会把栏上的两只亲代动物**依次 RemoveFromStack + StackSend(OutputDir) 弹出去**——
    /// 栏清空，得重新把一对动物放回去才能继续繁殖。
    /// （弹出去这步还有个副作用：月底动物散落一地，和自动归堆逻辑互相抢。）
    ///
    /// 这里用 Prefix 整个接替原方法：生成幼崽 + 送往输出方向 + 触发任务进度，
    /// 然后**不动亲代**直接返回 false。亲代还留在栏上（ChildCount == 2），
    /// `UpdateCard` 下一帧就会重新启动 120 秒的繁殖计时——一对动物放进栏里就能一直繁殖。
    ///
    /// 实现参照 Stacks n Capacity 的 `Patch_BreedingPen`（它验证过这个改法在游戏里稳定）。
    /// </summary>
    [HarmonyPatch(typeof(BreedingPen), "BreedAnimals")]
    internal static class BreedingPenKeepAnimalsPatch
    {
        [HarmonyPrefix]
        internal static bool Prefix(BreedingPen __instance)
        {
            if (!ModConfig.BreedingPenKeepAnimals || __instance == null
                || __instance.MyGameCard == null || __instance.MyGameCard.Child == null)
            {
                return true; // 开关关闭 / 存档异常时走原版逻辑
            }

            // 生成幼崽。checkAddToStack 传 false：不让新生卡在原地尝试自动入堆
            //（栏上已有两只，往栏上叠会被 CanHaveCard 拒绝，反而可能弹到奇怪的位置），
            // 反正下一步就把它送往输出方向。
            CardData baby = WorldManager.instance.CreateCard(__instance.transform.position,
                __instance.MyGameCard.Child.CardData.Id, faceUp: true, checkAddToStack: false);
            if (baby == null)
            {
                return true;
            }
            WorldManager.instance.StackSendCheckTarget(__instance.MyGameCard, baby.MyGameCard,
                __instance.OutputDir);
            QuestManager.instance.SpecialActionComplete("breed_" + baby.Id);
            return false; // 亲代留在栏上，原版"弹出去"的两段不再执行
        }
    }
}
