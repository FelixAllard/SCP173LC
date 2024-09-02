using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace SCP173.SCP173;

public class Scp173AI : ModEnemyAI
{
    [SerializeField] 
    public AudioClip[] horror;
    [SerializeField] 
    public AudioClip[] snapNeck; 
    public BoxCollider visionCollider;
    
    public override void Start()
    {
        self = this;
        InitialState = new RoamingPhase();
        self.agent.isStopped = true;
        self.agent.acceleration = 0;
        // agent.radius = 0.5f;
        base.Start();

    }
    //Useless
    public override void HitEnemy(
        int force = 1,
        PlayerControllerB? playerWhoHit = null,
        bool playHitSFX = false,
        int hitID = -1
    )
    {
        base.HitEnemy(force, playerWhoHit, playHitSFX, hitID);
    }

    public override void DoAIInterval()
    {
        base.DoAIInterval(); 
        Plugin.Logger.LogInfo("Current behaviour : " + ActiveState.GetType().Name);
    }

    private class RoamingPhase : AIBehaviorState
    {
        public override List<AIStateTransition> Transitions { get; set; } =
        [
            new FoundSomeone(), 
            new NoOnelooked()
        ];

        public override void OnStateEntered(Animator creatureAnimator)
        {
            if (self.NetworkManager.IsClient)
            {
                return;
            }
            self.ResetTargetPlayerClientRpc();
            agent.ResetPath();
            GameObject[] aiNode = RoundManager.Instance.insideAINodes;
            Vector3 possibleTp;
            try
            {
                 possibleTp =  aiNode
                    [UnityEngine.Random.Range(0, RoundManager.Instance.insideAINodes.Length)].transform.position;
            }
            catch (Exception e)
            {
                possibleTp = RoundManager.Instance.insideAINodes[0].transform.position;
                Plugin.Logger.LogError("We had some problem finding specifics");
                throw;
            }
            
            if (self.AnyPlayerHasLineOfSightToPosition(possibleTp))
            {
                OnStateEntered(creatureAnimator);
            }
            else
            {
                agent.Warp(possibleTp);
                self.SyncPositionToClients();
            }
            
        }

        public override void AIInterval(Animator creatureAnimator)
        {
            
        }

        public override void OnStateExit(Animator creatureAnimator)
        {

        }
        internal class FoundSomeone : AIStateTransition
        {
            public override bool CanTransitionBeTaken()
            {
                //IfIsClose enough to destination
                if (!self.IsHost)
                {
                    return false;
                }
                self.targetPlayer = self.CheckIfAPlayerHasLineOfSightToCollider();
                if (self.targetPlayer != null)
                {
                    Plugin.Logger.LogInfo($"Player {self.targetPlayer.name} has a line of sight");
                    self.SetTargetClientRpc(self.targetPlayer.playerClientId);
                    return true;
                }
                return false;
            }

            public override AIBehaviorState NextState()
            {
                
                return new ChasePhase();
            }
        }
        internal class NoOnelooked : AIStateTransition
        {
            private int _tickWaiting = 1500;
            private int _time = 1500;
            public override bool CanTransitionBeTaken()
            {
                if (_time <= 0)
                {
                    return true;
                }
                _time -= 1;
                return false;
            }
            public override AIBehaviorState NextState()
            {
                self.OverrideState(new RoamingPhase());
                return new RoamingPhase();
            }
        }
    }
        private class ChasePhase : AIBehaviorState
        {
            public override List<AIStateTransition> Transitions { get; set; } =
                [
                    new SnappingNeck(),
                    new LostTarget()
                ];

            public override void OnStateEntered(Animator creatureAnimator)
            {
                self.creatureSFX.PlayOneShot(self.horror[UnityEngine.Random.Range(0, self.horror.Length)]);
            }

            public override void AIInterval(Animator creatureAnimator)
            {
                if (!self.IsHost)
                {
                    return;
                }
                if (!self.targetPlayer.HasLineOfSightToPosition(self.transform.position))
                {
                    PlayerControllerB? playerWhoSaw = self.CheckIfAPlayerHasLineOfSightToCollider();
                    if (playerWhoSaw != null) 
                    {
                        self.targetPlayer = playerWhoSaw;
                        self.SetTargetClientRpc(playerWhoSaw.playerClientId);
                    }
                }
                self.SetDestinationToPosition(self.targetPlayer.transform.position);
            }

            public override void OnStateExit(Animator creatureAnimator)
            {
                
            } 
        
            internal class SnappingNeck : AIStateTransition 
            {
                private bool shouldSync = true; 
                public float interval = 1f;
                public int maxIteration = 100;
            
                 public override bool CanTransitionBeTaken()
                {
                    if (!self.IsHost || self.AnyPlayerHasLineOfSightToCollider())
                    {
                        return false;
                    }
                    int currentIteration = 0;
                    Vector3 startPosition = self.agent.transform.position;
                    Vector3 lastUnseenPosition = startPosition;
                    float remainingDistance = Vector3.Distance(self.transform.position, self.agent.destination);

                    while (remainingDistance > 0 || currentIteration < maxIteration )
                    {
                        currentIteration++;
                        float moveDistance = Mathf.Min(interval, remainingDistance);
                        Vector3 nextPosition = Vector3.MoveTowards(startPosition, self.agent.destination, moveDistance);
                        remainingDistance -= moveDistance;

                        if (NavMesh.SamplePosition(nextPosition, out NavMeshHit hit, interval, NavMesh.AllAreas))
                        {
                            nextPosition = hit.position;
                        }
                        else
                        {
                            Plugin.Logger.LogError("Next position is not on the NavMesh.");
                            break;
                        }

                        // Check if the AI has a line of sight from this new position
                        if (!self.AnyPlayerHasLineOfSightToPosition(nextPosition))
                        {
                            lastUnseenPosition = nextPosition;
                            shouldSync = true;
                        }
                        else
                        {
                            // If the AI is in line of sight, stop moving and sync position
                            if (shouldSync)
                            {
                                self.agent.Warp(lastUnseenPosition);
                                RotateAgentTowardsTarget(self.targetPlayer.transform.position);
                                self.SyncPositionToClients();
                                shouldSync = false;
                            }
                            return false; // Stop the chase but don't transition to kill
                        }

                        if (Vector3.Distance(nextPosition, self.targetPlayer.transform.position) < 0.3f && !self.AnyPlayerHasLineOfSightToCollider())
                        {
                            // Sync before returning true to indicate stopping the chase
                            if (shouldSync)
                            {
                                self.agent.Warp(lastUnseenPosition);
                                RotateAgentTowardsTarget(self.targetPlayer.transform.position);
                                self.SyncPositionToClients();
                                shouldSync = false;
                            }
                            Plugin.Logger.LogInfo("Went to second phase through First condition");
                            return true;
                        }

                        startPosition = nextPosition;
                    }
                    //I DOn't believe we need this!
                    /*// Sync if movement stopped naturally (e.g., reached the destination)
                    if (remainingDistance <= 0 && shouldSync)
                    {
                        Plugin.Logger.LogInfo("Went to second phase through second condition");
                        self.agent.Warp(lastUnseenPosition);
                        RotateAgentTowardsTarget(self.targetPlayer.transform.position);
                        self.SyncPositionToClients();
                        shouldSync = false;
                        return true;
                    }*/

                    return false;
                }

            private void RotateAgentTowardsTarget(Vector3 targetPosition)
            {
                Vector3 direction = (targetPosition - self.agent.transform.position).normalized;
                direction.y = 0; // Ignore vertical component
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                self.agent.transform.rotation = Quaternion.Euler(0, targetRotation.eulerAngles.y, 0);
            }

            public override AIBehaviorState NextState()
            {
                // Introduce a short delay or condition before transitioning to the kill state
                //if (Vector3.Distance(self.agent.transform.position, self.targetPlayer.transform.position) < 0.3f && !self.AnyPlayerHasLineOfSightToCollider())
                {
                    Plugin.Logger.LogInfo("We somehow triggered!");
                    self.creatureSFX.PlayOneShot(self.horror[UnityEngine.Random.Range(0, self.horror.Length)]);
                    self.targetPlayer.DamagePlayer(100, false, true, CauseOfDeath.Strangulation, 1);
                    return new JustKilledSomeone();
                }
                // Return to idle or another appropriate state if the kill condition isn't met
            }
        }

        internal class LostTarget : AIStateTransition
        {
            public override bool CanTransitionBeTaken()
            {
                if (!self.IsHost)
                {
                    return false;
                }
                if (self.targetPlayer == null)
                {
                    return true;
                }
                PlayerControllerB? targetPlayer = self.targetPlayer;
                if (!targetPlayer.isPlayerControlled ||  !targetPlayer.isInsideFactory)// Removed : targetPlayer.isPlayerDead ||
                {
                    targetPlayer = self.CheckIfAPlayerHasLineOfSightToCollider();
                    if (targetPlayer != null)
                    {
                        self.targetPlayer = targetPlayer;
                        return false;
                    }
                    return true;
                }
                return false;
            }
            public override AIBehaviorState NextState()
            {
                self.ResetTargetPlayerClientRpc();
                return new RoamingPhase();
            }
        }
    }
    private class JustKilledSomeone : AIBehaviorState
    {
        public override List<AIStateTransition> Transitions { get; set; } =
            [
                new Waiting()
            ];

        public override void OnStateEntered(Animator creatureAnimator)
        {
            //self.agent.ResetPath();
        }

        public override void AIInterval(Animator creatureAnimator)
        {
        }

        public override void OnStateExit(Animator creatureAnimator)
        {

        }
        internal class Waiting : AIStateTransition
        {
            int timeRemaining = 1000;
            public override bool CanTransitionBeTaken()
            {
                //IfIsClose enough to destination
                if (timeRemaining<=0)
                    return true;
                timeRemaining -= 1;
                return false;
            }
            public override AIBehaviorState NextState()
            {
                return new RoamingPhase();
            }
        }
    }
    private PlayerControllerB? CheckIfAPlayerHasLineOfSightToCollider()
    {
        Bounds bounds = visionCollider.bounds;
        Vector3[] checkPoints = GetBoundingBoxCheckPoints(bounds);

        foreach (var player in RoundManager.Instance.playersManager.allPlayerScripts.Where(p => p.isPlayerControlled && !p.isPlayerDead))
        {
            foreach (var point in checkPoints)
            {
                if (player.HasLineOfSightToPosition(point))
                {
                    return player;
                }
            }
        }
        return null;
    }

    private bool AnyPlayerHasLineOfSightToCollider()
    {
        Bounds bounds = visionCollider.bounds;
        Vector3[] checkPoints = GetBoundingBoxCheckPoints(bounds);

        foreach (var player in RoundManager.Instance.playersManager.allPlayerScripts.Where(p => p.isPlayerControlled && !p.isPlayerDead))
        {
            foreach (var point in checkPoints)
            {
                if (player.HasLineOfSightToPosition(point))
                {
                    return true;
                }
            }
        }
        return false;
    }
    private bool AnyPlayerHasLineOfSightToPosition(Vector3 targetPosition)
    {
        foreach (var player in RoundManager.Instance.playersManager.allPlayerScripts.Where(p => p.isPlayerControlled && !p.isPlayerDead))
        {

            if (player.HasLineOfSightToPosition(targetPosition))
            {
                return true;
            }

        }
        return false;
    }

    private Vector3[] GetBoundingBoxCheckPoints(Bounds bounds)
    {
        // Define the 8 corners of the bounding box
        return new Vector3[]
        {
            bounds.center, // Center of the bounds
            bounds.min, // Bottom-left corner
            bounds.max, // Top-right corner
            new Vector3(bounds.min.x, bounds.min.y, bounds.max.z), // Bottom-left front
            new Vector3(bounds.min.x, bounds.max.y, bounds.min.z), // Top-left back
            new Vector3(bounds.max.x, bounds.min.y, bounds.min.z), // Bottom-right back
            new Vector3(bounds.max.x, bounds.max.y, bounds.min.z), // Top-right back
            new Vector3(bounds.min.x, bounds.max.y, bounds.max.z)  // Top-left front
        };
    }

    [ClientRpc]
    private void SetTargetClientRpc(ulong clientId)
    {
        targetPlayer =
            RoundManager.Instance.playersManager.allPlayerScripts.FirstOrDefault(p => p.isPlayerControlled && !p.isPlayerDead && p.playerClientId == clientId);
    }
    [ClientRpc]
    private void ResetTargetPlayerClientRpc()
    {
        targetPlayer = null;
    }
}