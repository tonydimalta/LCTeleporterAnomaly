using HarmonyLib;
using System.Collections;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace LCTeleporterAnomaly.Patches
{
    [HarmonyPatch(typeof(EnemyAI))]
    internal static class EnemyAIPatch
    {
        [HarmonyPatch(nameof(EnemyAI.ShipTeleportEnemy))]
        [HarmonyPostfix]
        private static void ShipTeleportEnemyPostfix(EnemyAI __instance)
        {
            if (__instance is MaskedPlayerEnemy)
            {
                return;
            }

            if (__instance.TryGetComponent(out IEnemyAIPatch patch))
            {
                patch.StartTeleportSequence();
            }
        }
    }

    internal interface IEnemyAIPatch
    {
        public void StartTeleportSequence(bool bInterruptOngoingTeleport = true);
    }

    [DisallowMultipleComponent]
    internal abstract class EnemyAIPatch<T> : MonoBehaviour, IEnemyAIPatch where T : EnemyAI
    {
        private Coroutine _teleportCoroutine = null;
        protected T _enemyAI = null;

        private static readonly DialogueSegment[] TeleporterAnomalyDialogue = new DialogueSegment[]
        {
            new DialogueSegment
            {
                speakerText = "PILOT COMPUTER",
                bodyText = "Alert! Anomaly detected with the teleporter.",
                waitTime = 4f
            }
        };

        protected void Start()
        {
            Plugin.Logger?.LogDebug($"\"{typeof(T).Name}\" applies \"{GetType().Name}\"");
            _enemyAI = gameObject.GetComponent<T>();
        }

        public void StartTeleportSequence(bool bInterruptOngoingTeleport = true)
        {
            if (_enemyAI == null)
            {
                return;
            }

            if (_teleportCoroutine != null)
            {
                if (!bInterruptOngoingTeleport)
                {
                    return;
                }

                StopCoroutine(_teleportCoroutine);
            }

            _teleportCoroutine = StartCoroutine(TeleportSequence());
        }

        private IEnumerator TeleportSequence()
        {
            if (!ShipTeleporter.hasBeenSpawnedThisSession)
            {
                Plugin.Logger?.LogWarning($"\"{GetType().Name}\" ignoring teleport sequence as no teleporter has been spawned this session");
                yield break;
            }

            ShipTeleporter teleporter = FindObjectsOfType<ShipTeleporter>().Where(x => !x.isInverseTeleporter).First();
            if (teleporter == null)
            {
                Plugin.Logger?.LogWarning($"\"{GetType().Name}\" ignoring teleport sequence as no teleporter was found (inverse tp doesn't count)");
                yield break;
            }

            Vector3 cachedPosition = teleporter.teleporterPosition.position;
            yield return new WaitForSeconds(3f);

            if (!GameNetworkManager.Instance.localPlayerController.isInsideFactory)
            {
                HUDManager.Instance.ReadDialogue(TeleporterAnomalyDialogue);
            }

            _enemyAI.creatureSFX.PlayOneShot(teleporter?.beamUpPlayerBodySFX);
            yield return new WaitForSeconds(3f);

            if (StartOfRound.Instance.shipIsLeaving)
            {
                Plugin.Logger?.LogDebug($"\"{GetType().Name}\" interrupting teleport sequence as the ship is leaving");
                yield break;
            }

            if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer)
            {
                ulong localClientId = GameNetworkManager.Instance.localPlayerController.actualClientId;
                if (_enemyAI.OwnerClientId != localClientId)
                {
                    Plugin.Logger?.LogDebug($"\"{GetType().Name}\" Changing OwnershipOfEnemy to prevent server/client desync (e.g. invisible enemy) after teleporting");
                    _enemyAI.ChangeOwnershipOfEnemy(localClientId);
                }
            }

            SetOutside(outside: true);
            Vector3 targetPosition = (teleporter?.teleporterPosition.position ?? cachedPosition) + Vector3.up * 0.5f;
            TeleportToPosition(targetPosition);
            _enemyAI.isInsidePlayerShip = true;
            Plugin.Logger?.LogDebug($"\"{GetType().Name}\" Agent speed={_enemyAI.agent.speed}; Agent behaviour[{_enemyAI.currentBehaviourStateIndex}]={_enemyAI.currentBehaviourState.name}");
        }

        protected virtual bool SetOutside(bool outside)
        {
            if (_enemyAI.isOutside == outside)
            {
                return false;
            }

            Plugin.Logger?.LogInfo($"Changing \"{typeof(T).Name}\" isOutside=({_enemyAI.isOutside} => {outside})");
            _enemyAI.isOutside = outside;
            _enemyAI.allAINodes = outside ? RoundManager.Instance.outsideAINodes : RoundManager.Instance.insideAINodes;

            int newOutsideEnemyPower = RoundManager.Instance.currentOutsideEnemyPower + _enemyAI.enemyType.PowerLevel * (outside ? 1 : -1);
            int newInsideEnemyPower = RoundManager.Instance.currentEnemyPower + _enemyAI.enemyType.PowerLevel * (outside ? -1 : 1);
            RoundManager.Instance.currentOutsideEnemyPower = Mathf.Max(newOutsideEnemyPower, 0);
            RoundManager.Instance.currentEnemyPower = Mathf.Max(newInsideEnemyPower, 0);

            return true;
        }

        protected virtual void TeleportToPosition(Vector3 targetPosition)
        {
            _enemyAI.StopSearch(_enemyAI.currentSearch);
            // Make sure the position is on the navmesh, or the enemy won't be able to warp/move.
            targetPosition = RoundManager.Instance.GetNavMeshPosition(targetPosition, RoundManager.Instance.navHit);
            Plugin.Logger?.LogDebug($"Teleporting \"{typeof(T).Name}\" to {targetPosition}");

            if (_enemyAI.agent.enabled)
            {
                // Disabling the agent temporarily to change the location without conflicting with the AI navigation (Warp should work but does not here).
                _enemyAI.agent.enabled = false;
                _enemyAI.transform.position = targetPosition;
                _enemyAI.agent.enabled = true;
                _enemyAI.agent.ResetPath();
            }
            else
            {
                _enemyAI.transform.position = targetPosition;
            }

            _enemyAI.moveTowardsDestination = false;
            _enemyAI.movingTowardsTargetPlayer = false;
            _enemyAI.serverPosition = targetPosition;
        }
    }
}