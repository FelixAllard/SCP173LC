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
            private int tickWaiting = 500;
            private int time = 500;
            public override bool CanTransitionBeTaken()
            {
                if (time <= 0)
                {
                    return true;
                }
                time -= 1;
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
                new SnappingNeck()
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
            agent.ResetPath();
            self.targetPlayer = null;
            
        }
        internal class SnappingNeck : AIStateTransition
        {
            private bool shouldSync = true;
            public override bool CanTransitionBeTaken()
            {
                var transform1 = self.agent.transform;
                var position = transform1.position;
                var destination1 = self.agent.destination;
                Vector3 direction = (destination1 - position).normalized;
                float remainingDistance = Vector3.Distance(position, destination1);
                float moveDistance = UnityEngine.Mathf.Min(0.3f, remainingDistance); // Ensure we don't move past the destination
                Vector3 newPosition = position + direction * moveDistance;

                if (NavMesh.SamplePosition(newPosition, out NavMeshHit hit, 0.3f, NavMesh.AllAreas))
                {
                    newPosition = hit.position;
                }
                else
                {
                    Plugin.Logger.LogError("New position is not on the NavMesh.");
                    return false;
                }

                if (self.CheckIfAPlayerHasLineOfSight() == null && !self.AnyPlayerHasLineOfSightToPosition(newPosition))
                {
                    shouldSync = true;
                    self.agent.Warp(newPosition);
                    self.agent.transform.rotation = Quaternion.LookRotation(direction);
                    if (Vector3.Distance(self.agent.transform.position, self.agent.destination) < 0.3f)
                    {
                        return true;
                    }
                }
                else
                {
                    if (shouldSync == true)
                    {
                        self.SyncPositionToClients();
                    }
                    shouldSync = false;
                    
                }
                return false;
            }

            public override AIBehaviorState NextState()
            {
                self.creatureSFX.PlayOneShot(self.snapNeck[UnityEngine.Random.Range(0, self.snapNeck.Length)]);
                self.targetPlayer = null;
                return new JustKilledSomeone();
            }
        }
        internal class LostTarget : AIStateTransition
        {
            public override bool CanTransitionBeTaken()
            {
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
                Plugin.Logger.LogInfo(" I am seen");
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
                Plugin.Logger.LogInfo(" I am seen");
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
                Plugin.Logger.LogInfo(" I am seen");
                return true;
            }
        }
        return false;
    }
}