using HarmonyLib;
using UnityEngine;

namespace LCTeleporterAnomaly.Patches
{
    [HarmonyPatch(typeof(FlowermanAI))]
    internal class FlowermanAIPatch : EnemyAIPatch<FlowermanAI>
    {
        protected override bool SetOutside(bool outside)
        {
            if (!base.SetOutside(outside))
            {
                return false;
            }

            _enemyAI.mainEntrancePosition = RoundManager.FindMainEntrancePosition(getOutsideEntrance: outside);
            return true;
        }

        protected override void TeleportToPosition(Vector3 targetPosition)
        {
            _enemyAI.DropPlayerBody();
            base.TeleportToPosition(targetPosition);
            _enemyAI.SwitchToBehaviourState(0); // 0=Sneaking
        }

        [HarmonyPatch(nameof(FlowermanAI.Start))]
        [HarmonyPostfix]
        private static void StartPostfix(FlowermanAI __instance)
        {
            __instance.gameObject.AddComponent<FlowermanAIPatch>();
        }

        [HarmonyPatch(nameof(FlowermanAI.FinishKillAnimation))]
        [HarmonyPostfix]
        private static void FinishKillAnimationPostfix(FlowermanAI __instance)
        {
            if (__instance.bodyBeingCarried != null)
            {
                Plugin.Logger?.LogDebug($"\"{nameof(FlowermanAIPatch)}\" Redirecting player \"{__instance.bodyBeingCarried.playerScript.playerUsername}\" teleport to bracken");
                __instance.bodyBeingCarried.playerScript.redirectToEnemy = __instance;
            }
        }

        [HarmonyPatch(nameof(FlowermanAI.DropPlayerBody))]
        [HarmonyPrefix]
        private static void DropPlayerBodyPrefix(FlowermanAI __instance)
        {
            if (__instance.bodyBeingCarried != null)
            {
                Plugin.Logger?.LogDebug($"\"{nameof(FlowermanAIPatch)}\" Redirecting teleport from bracken back to player \"{__instance.bodyBeingCarried.playerScript.playerUsername}\"");
                __instance.bodyBeingCarried.playerScript.redirectToEnemy = null;
            }
        }
    }
}