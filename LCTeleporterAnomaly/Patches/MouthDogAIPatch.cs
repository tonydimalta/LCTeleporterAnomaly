using HarmonyLib;
using UnityEngine;

namespace LCTeleporterAnomaly.Patches
{
    [HarmonyPatch(typeof(MouthDogAI))]
    internal class MouthDogAIPatch : EnemyAIPatch<MouthDogAI>
    {
        private void SwitchToSlightSuspicion(Vector3 heardNoisePosition)
        {
            _enemyAI.SwitchToBehaviourState(1); // 0=Roaming?; 1=Suspicious; 2=Chasing; 3=Lunging?
            _enemyAI.lastHeardNoisePosition = heardNoisePosition;
            _enemyAI.lastHeardNoiseDistanceWhenHeard = Vector3.Distance(_enemyAI.transform.position, heardNoisePosition);
            _enemyAI.noisePositionGuess = _enemyAI.roundManager.GetRandomNavMeshPositionInRadius(heardNoisePosition, _enemyAI.lastHeardNoiseDistanceWhenHeard / _enemyAI.noiseApproximation);
            _enemyAI.AITimer = 3f;
            _enemyAI.hearNoiseCooldown = 1f;
            _enemyAI.suspicionLevel = 2;
        }

        protected override void TeleportToPosition(Vector3 targetPosition)
        {
            _enemyAI.DropCarriedBody();
            SwitchToSlightSuspicion(targetPosition);
            base.TeleportToPosition(targetPosition);
            _enemyAI.previousPosition = _enemyAI.transform.position;
        }

        [HarmonyPatch(nameof(MouthDogAI.Start))]
        [HarmonyPostfix]
        private static void StartPostfix(MouthDogAI __instance)
        {
            __instance.gameObject.AddComponent<MouthDogAIPatch>();
        }

        [HarmonyPatch(nameof(MouthDogAI.TakeBodyInMouth))]
        [HarmonyPostfix]
        private static void TakeBodyInMouthPostfix(MouthDogAI __instance, DeadBodyInfo body)
        {
            Plugin.Logger?.LogDebug($"\"{nameof(MouthDogAIPatch)}\" Redirecting player \"{body.playerScript.playerUsername}\" teleport to eyeless dog");
            body.playerScript.redirectToEnemy = __instance;
        }

        [HarmonyPatch(nameof(MouthDogAI.DropCarriedBody))]
        [HarmonyPrefix]
        private static void DropCarriedBodyPrefix(MouthDogAI __instance)
        {
            if (__instance.carryingBody != null)
            {
                Plugin.Logger?.LogDebug($"\"{nameof(MouthDogAIPatch)}\" Redirecting teleport from eyeless dog back to player \"{__instance.carryingBody.playerScript.playerUsername}\"");
                __instance.carryingBody.playerScript.redirectToEnemy = null;
            }
        }
    }
}