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
            if (!self.IsHost)
            {
                return;
            }
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
                if (!self.IsHost)
                {
                    return false;
                }
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
            if (!self.IsHost)
            {
                return;
            }
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
            public float interval = 0.5f;
            public override bool CanTransitionBeTaken()
            {
                if (!self.IsHost)
                {
                    return false;
                }
                if (self.CheckIfAPlayerHasLineOfSight())
                {
                    return false;
                }

                int iterationNumber = 0;
                Vector3 startPosition = self.agent.transform.position;
                Vector3 lastUnseenPosition = startPosition;
                float remainingDistance = self.agent.remainingDistance;

                while (remainingDistance > 0)
                {
                    iterationNumber += 1;

                    if (iterationNumber > 100)
                    {
                        break;
                    }
                    float moveDistance = Mathf.Min(interval, remainingDistance);
                    Vector3 newPosition = Vector3.MoveTowards(startPosition, self.agent.destination, moveDistance);
                    remainingDistance -= moveDistance;

                    if (NavMesh.SamplePosition(newPosition, out NavMeshHit hit, interval, NavMesh.AllAreas))
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

                    if (Vector3.Distance(newPosition, self.targetPlayer.transform.position) < 1)
                    {
                        return true;
                    }

                    startPosition = newPosition;
                    shouldSync = true;
                }

                if (shouldSync)
                {
                    shouldSync = false;
                    self.agent.Warp(lastUnseenPosition);
                    RotateAgentTowardsTarget(self.agent.destination);
                    // Ensure position is synced immediately after warping
                    self.SyncPositionToClients();
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
                self.creatureSFX.PlayOneShot(self.horror[UnityEngine.Random.Range(0, self.horror.Length)]);
                self.targetPlayer.DamagePlayer(100, false, true, CauseOfDeath.Strangulation, 1);
                return new JustKilledSomeone();
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
        foreach (var player in RoundManager.Instance.playersManager.allPlayerScripts.Where(p =>p.isPlayerControlled && !p.isPlayerDead))
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
        foreach (var player in RoundManager.Instance.playersManager.allPlayerScripts.Where(p => p.isPlayerControlled&& !p.isPlayerDead))
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
        foreach (var player in RoundManager.Instance.playersManager.allPlayerScripts.Where(p =>p.isPlayerControlled&& !p.isPlayerDead))
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