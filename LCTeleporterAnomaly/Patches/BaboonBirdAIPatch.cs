using HarmonyLib;
using System.Collections;
using UnityEngine;

namespace LCTeleporterAnomaly.Patches
{
    [HarmonyPatch(typeof(BaboonBirdAI))]
    internal class BaboonBirdAIPatch : EnemyAIPatch<BaboonBirdAI>
    {
        protected override void TeleportToPosition(Vector3 targetPosition)
        {
            _enemyAI.StopKillAnimation();
            base.TeleportToPosition(targetPosition);
            _enemyAI.SwitchToBehaviourState(0); // 0=scout; 1=rest; 2=focus threat
        }

        [HarmonyPatch(nameof(BaboonBirdAI.Start))]
        [HarmonyPostfix]
        private static void StartPostfix(BaboonBirdAI __instance)
        {
            __instance.gameObject.AddComponent<BaboonBirdAIPatch>();
        }

        [HarmonyPatch(nameof(BaboonBirdAI.killPlayerAnimation))]
        [HarmonyPostfix]
        private static void KillPlayerAnimationPostfix(BaboonBirdAI __instance, ref IEnumerator __result)
        {
            static IEnumerator KillPlayerAnimationEnumerator(BaboonBirdAI __instance, IEnumerator __result)
            {
                bool bRedirectedToEnemy = false;
                while (__result.MoveNext())
                {
                    if (!bRedirectedToEnemy &&
                        __instance.killAnimationBody != null)
                    {
                        Plugin.Logger?.LogDebug($"\"{nameof(BaboonBirdAIPatch)}\" Redirecting player \"{__instance.killAnimationBody.playerScript.playerUsername}\" teleport to baboon hawk");
                        __instance.killAnimationBody.playerScript.redirectToEnemy = __instance;
                        bRedirectedToEnemy = true;
                    }

                    var current = __result.Current;
                    yield return current;
                }
            }

            __result = KillPlayerAnimationEnumerator(__instance, __result);
        }

        [HarmonyPatch(nameof(BaboonBirdAI.StopKillAnimation))]
        [HarmonyPrefix]
        private static void StopKillAnimationPrefix(BaboonBirdAI __instance)
        {
            if (__instance.killAnimationBody != null)
            {
                Plugin.Logger?.LogDebug($"\"{nameof(BaboonBirdAIPatch)}\" Redirecting teleport from baboon hawk back to player \"{__instance.killAnimationBody.playerScript.playerUsername}\"");
                __instance.killAnimationBody.playerScript.redirectToEnemy = null;
            }
        }
    }
}