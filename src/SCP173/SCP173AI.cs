using System.Collections;
using System.Collections.Generic;
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
    

    private class RoamingPhase : AIBehaviorState
    {
        public override List<AIStateTransition> Transitions { get; set; } =
        [
            new FoundSomeone(), 
            new NoOnelooked()
        ];

        public override void OnStateEntered(Animator creatureAnimator)
        {
            self.targetPlayer = null;
            agent.ResetPath();
            Vector3 possibleTp =  RoundManager.Instance.insideAINodes
                [UnityEngine.Random.Range(0, RoundManager.Instance.insideAINodes.Length)].transform.position;
            if (self.CheckIfAPlayerHasLineOfSight(possibleTp) != null)
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
                self.targetPlayer = self.CheckIfAPlayerHasLineOfSight();
                if(self.targetPlayer!=null)
                    return true;
                return false;
            }

            public override AIBehaviorState NextState()
            {
                
                return new ChasePhase();
            }
        }
        internal class NoOnelooked : AIStateTransition
        {
            private int _tickWaiting = 500;
            private int _time = 500;
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
            if (!self.targetPlayer.HasLineOfSightToPosition(self.transform.position))
            {
                PlayerControllerB? playerWhoSaw = self.CheckIfAPlayerHasLineOfSight();
                if (playerWhoSaw != null) 
                {
                    self.targetPlayer = playerWhoSaw;
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
            public override bool CanTransitionBeTaken()
            {
                if (self.CheckIfAPlayerHasLineOfSight())
                {
                    return false;
                }

                NavMeshPath path = new NavMeshPath();
                self.agent.CalculatePath(self.targetPlayer.transform.position, path);

                if (path.corners.Length < 2)
                {
                    return false; // No valid path
                }

                Vector3 lastUnseenPosition = self.agent.transform.position;

                for (int i = 0; i < path.corners.Length - 1; i++)
                {
                    Vector3 start = path.corners[i];
                    Vector3 end = path.corners[i + 1];
                    Vector3 direction = (end - start).normalized;
                    float segmentDistance = Vector3.Distance(start, end);
                    float remainingDistance = segmentDistance;

                    while (remainingDistance > 0)
                    {
                        float moveDistance = Mathf.Min(0.5f, remainingDistance);
                        Vector3 newPosition = start + direction * (segmentDistance - remainingDistance + moveDistance);
                        remainingDistance -= moveDistance;

                        if (NavMesh.SamplePosition(newPosition, out NavMeshHit hit, 0.5f, NavMesh.AllAreas))
                        {
                            newPosition = hit.position;
                        }
                        else
                        {
                            Plugin.Logger.LogError("New position is not on the NavMesh.");
                            break;
                        }

                        if (!self.AnyPlayerHasLineOfSightToPosition(newPosition))
                        {
                            lastUnseenPosition = newPosition;
                        }
                        else
                        {
                            break;
                        }
                    }

                    if (self.AnyPlayerHasLineOfSightToPosition(lastUnseenPosition))
                    {
                        break;
                    }
                }

                if (lastUnseenPosition != self.agent.transform.position)
                {
                    shouldSync = true;
                    self.agent.Warp(lastUnseenPosition);
                    RotateAgentTowardsTarget(self.targetPlayer.transform.position);
                    return true;
                }

                return false;
            }
            void RotateAgentTowardsTarget(Vector3 targetPosition)
            {
                Vector3 direction = targetPosition - self.agent.transform.position;
                direction.y = 0; // Ignore vertical component
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                self.agent.transform.rotation = Quaternion.Euler(0, targetRotation.eulerAngles.y, 0);
            }

            public override AIBehaviorState NextState()
            {
                self.creatureSFX.PlayOneShot(self.snapNeck[UnityEngine.Random.Range(0, self.snapNeck.Length)]);
                self.targetPlayer.DamagePlayerServerRpc(100,0);
                //self.targetPlayer.KillPlayer(new Vector3(),true, CauseOfDeath.Strangulation, 1);
                self.targetPlayer = null;
                return new JustKilledSomeone();
            }
        }
        internal class LostTarget : AIStateTransition
        {
            public override bool CanTransitionBeTaken()
            {
                if (self.targetPlayer == null)
                {
                    return true;
                }
                PlayerControllerB? targetPlayer = self.targetPlayer;
                if (!targetPlayer.isPlayerControlled || targetPlayer.isPlayerDead || !targetPlayer.isInsideFactory)
                {
                    targetPlayer = self.CheckIfAPlayerHasLineOfSight();
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
                self.targetPlayer = null;
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
            self.agent.ResetPath();
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

    private PlayerControllerB? CheckIfAPlayerHasLineOfSight()
    {
        foreach (var player in RoundManager.Instance.playersManager.allPlayerScripts.Where(p =>p.isPlayerControlled))
        {
            if (player.HasLineOfSightToPosition(transform.position))
            {
                //Plugin.Logger.LogInfo(" I am seen");
                return player;
            }
        }
        return null;
    }
    private PlayerControllerB? CheckIfAPlayerHasLineOfSight(Vector3 position)
    {
        foreach (var player in RoundManager.Instance.playersManager.allPlayerScripts.Where(p => p.isPlayerControlled))
        {
            if (player.HasLineOfSightToPosition(position))
            {
                //Plugin.Logger.LogInfo(" I am seen");
                return player;
            }
        }
        return null;
    }
    private bool AnyPlayerHasLineOfSightToPosition(Vector3 position)
    {
        foreach (var player in RoundManager.Instance.playersManager.allPlayerScripts.Where(p =>p.isPlayerControlled))
        {
            if (player.HasLineOfSightToPosition(position))
            {
                //Plugin.Logger.LogInfo(" I am seen");
                return true;
            }
        }
        return false;
    }
}