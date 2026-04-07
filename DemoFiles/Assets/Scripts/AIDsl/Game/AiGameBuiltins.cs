using System.Collections;
using System.Collections.Generic;
using AIDsl.Runtime;
using UnityEngine;

namespace AIDsl.Game
{
    public static class AiGameBuiltins
    {
        public static void Register(AiAgentBase agent, AiActionRegistry registry, AiContext context)
        {
            context.SetTreasureTakenResolver(delegate
            {
                return GameManager.Instance != null && GameManager.Instance.isTreasureTaken(agent.TreasureId);
            });

            context.RegisterKindProvider("rock", delegate
            {
                return GameManager.Instance != null
                    ? new List<GameObject>(GameManager.Instance.boulders)
                    : new List<GameObject>();
            });

            context.RegisterKindProvider("shroom", delegate
            {
                return GameManager.Instance != null
                    ? new List<GameObject>(GameManager.Instance.shrooms)
                    : new List<GameObject>();
            });

            agent.RegisterVisibilityFilter(delegate(string slotName, GameObject target)
            {
                return !(GameManager.Instance != null
                    && GameManager.Instance.player == target
                    && GameManager.Instance.isPlayerInvis);
            });

            registry.Register(new AiActionDefinition(
                "pick_rock",
                delegate(AiPlanningState state, AiActionCall action)
                {
                    return state.Exists("rock");
                },
                delegate(AiPlanningState state, AiActionCall action)
                {
                    state.IsBusy = true;
                    state.AddItem("rock");
                    state.SetExists("rock", false);
                },
                ExecutePickRock));

            registry.Register(new AiActionDefinition(
                "throw_at",
                delegate(AiPlanningState state, AiActionCall action)
                {
                    return action.Target != null && action.Target.Kind == AiTargetKind.Slot && state.HasItem("rock");
                },
                delegate(AiPlanningState state, AiActionCall action)
                {
                    state.IsBusy = true;
                    state.RemoveItem("rock");
                },
                ExecuteThrowAt));

            registry.Register(new AiActionDefinition(
                "eat_nearest_shroom",
                delegate(AiPlanningState state, AiActionCall action)
                {
                    return state.Exists("shroom");
                },
                delegate(AiPlanningState state, AiActionCall action)
                {
                    state.IsBusy = true;
                    state.SetExists("shroom", false);
                },
                ExecuteEatNearestShroom));
        }

        private static IEnumerator ExecutePickRock(AiAgentBase agent, AiContext context, AiActionCall action)
        {
            GameObject rock = context.FindNearest("rock");
            if (rock == null || agent.RockHoldPoint == null)
            {
                if (rock == null)
                {
                    Debug.LogWarning(agent.gameObject.name + " could not pick rock: no available rock found.", agent.gameObject);
                }
                else
                {
                    Debug.LogWarning(agent.gameObject.name + " could not pick rock: Rock Hold Point is not assigned.", agent.gameObject);
                }
                agent.CompleteAction(false);
                yield break;
            }

            Debug.Log(agent.gameObject.name + " moving to pick rock: " + rock.name, agent.gameObject);
            float timeoutAt = Time.time + 8f;
            while (rock != null && !IsWithinPickupRange(agent, rock))
            {
                if (agent.IsCurrentActionInterrupted())
                {
                    agent.NavAgent.ResetPath();
                    agent.CompleteAction(AiActionResult.Interrupted);
                    yield break;
                }

                Vector3 sampledPosition;
                if (!TryGetRockApproachPosition(agent, rock, out sampledPosition))
                {
                    Debug.LogWarning(agent.gameObject.name + " could not pick rock: no valid NavMesh position near rock.", agent.gameObject);
                    agent.CompleteAction(false);
                    yield break;
                }

                agent.NavAgent.SetDestination(sampledPosition);

                if (Time.time >= timeoutAt)
                {
                    Debug.LogWarning(agent.gameObject.name + " could not pick rock: timed out while approaching " + rock.name + ".", agent.gameObject);
                    agent.CompleteAction(false);
                    yield break;
                }

                yield return null;
            }

            if (agent.IsCurrentActionInterrupted())
            {
                agent.NavAgent.ResetPath();
                agent.CompleteAction(AiActionResult.Interrupted);
                yield break;
            }

            if (rock == null)
            {
                Debug.LogWarning(agent.gameObject.name + " could not pick rock: target rock disappeared during pickup.", agent.gameObject);
                agent.CompleteAction(false);
                yield break;
            }

            agent.NavAgent.ResetPath();
            if (GameManager.Instance != null)
            {
                GameManager.Instance.boulders.Remove(rock);
            }

            rock.transform.SetParent(agent.RockHoldPoint, true);
            rock.transform.position = agent.RockHoldPoint.position;
            rock.transform.rotation = agent.RockHoldPoint.rotation;

            Rigidbody rockBody = rock.GetComponent<Rigidbody>();
            if (rockBody != null)
            {
                rockBody.isKinematic = true;
                rockBody.useGravity = false;
                rockBody.linearVelocity = Vector3.zero;
                rockBody.angularVelocity = Vector3.zero;
            }

            context.Memory.AddItem("rock");
            Debug.Log(agent.gameObject.name + " picked up rock: " + rock.name, agent.gameObject);
            agent.CompleteAction(true);
        }

        private static IEnumerator ExecuteThrowAt(AiAgentBase agent, AiContext context, AiActionCall action)
        {
            if (agent.RockHoldPoint == null)
            {
                Debug.LogWarning(agent.gameObject.name + " could not throw rock: Rock Hold Point is not assigned.", agent.gameObject);
                agent.CompleteAction(false);
                yield break;
            }

            Transform heldRockTransform = agent.RockHoldPoint.childCount > 0 ? agent.RockHoldPoint.GetChild(0) : null;
            if (heldRockTransform == null)
            {
                Debug.LogWarning(agent.gameObject.name + " could not throw rock: no rock is currently held.", agent.gameObject);
                agent.CompleteAction(false);
                yield break;
            }

            Vector3 targetPosition;
            GameObject targetObject;
            if (context.TryResolveTarget(action.Target, out targetObject))
            {
                targetPosition = targetObject.transform.position;
            }
            else if (!context.TryGetLastSeenPosition(action.Target.Value, out targetPosition))
            {
                Debug.LogWarning(agent.gameObject.name + " could not throw rock: no visible or remembered target for '" + action.Target.Value + "'.", agent.gameObject);
                agent.CompleteAction(false);
                yield break;
            }

            if (agent.ObstacleParent == null)
            {
                Debug.LogWarning(agent.gameObject.name + " could not throw rock: Obstacle Parent is not assigned.", agent.gameObject);
                agent.CompleteAction(false);
                yield break;
            }

            heldRockTransform.SetParent(agent.ObstacleParent, true);

            Rigidbody rockBody = heldRockTransform.GetComponent<Rigidbody>();
            if (rockBody == null)
            {
                rockBody = heldRockTransform.gameObject.AddComponent<Rigidbody>();
            }

            Collider rockCollider = heldRockTransform.GetComponent<Collider>();
            if (rockCollider == null)
            {
                heldRockTransform.gameObject.AddComponent<BoxCollider>();
            }

            rockBody.isKinematic = false;
            rockBody.useGravity = true;

            if (heldRockTransform.GetComponent<RockUpright>() == null)
            {
                heldRockTransform.gameObject.AddComponent<RockUpright>();
            }

            Vector3 direction = (targetPosition - heldRockTransform.position).normalized;
            float distance = Vector3.Distance(targetPosition, heldRockTransform.position);
            float scaledForce = Mathf.Clamp(
                agent.ThrowForce * Mathf.Sqrt(Mathf.Max(distance, 0.01f) / Mathf.Max(agent.ThrowDistanceAmplifier, 0.01f)),
                agent.MinThrowForce,
                agent.MaxThrowForce);
            float upwardForce = agent.ThrowUpwardForce + distance * agent.UpwardForceMultiplier;

            rockBody.AddForce(direction * scaledForce + Vector3.up * upwardForce, ForceMode.Impulse);
            context.Memory.RemoveItem("rock");
            Debug.Log(agent.gameObject.name + " threw rock at " + action.Target.Value, agent.gameObject);
            agent.CompleteAction(true);

            yield return null;
        }

        private static bool TryGetRockApproachPosition(AiAgentBase agent, GameObject rock, out Vector3 sampledPosition)
        {
            Vector3 desiredPosition = GetRockClosestPoint(agent, rock);
            return agent.SampleNavigation(desiredPosition, out sampledPosition);
        }

        private static bool IsWithinPickupRange(AiAgentBase agent, GameObject rock)
        {
            Vector3 closestPoint = GetRockClosestPoint(agent, rock);
            Vector3 flatAgentPosition = new Vector3(agent.transform.position.x, 0f, agent.transform.position.z);
            Vector3 flatClosestPoint = new Vector3(closestPoint.x, 0f, closestPoint.z);
            return Vector3.Distance(flatAgentPosition, flatClosestPoint) <= agent.PickupDistance;
        }

        private static Vector3 GetRockClosestPoint(AiAgentBase agent, GameObject rock)
        {
            Collider rockCollider = rock.GetComponent<Collider>();
            if (rockCollider != null)
            {
                return rockCollider.ClosestPoint(agent.transform.position);
            }

            return rock.transform.position;
        }

        private static IEnumerator ExecuteEatNearestShroom(AiAgentBase agent, AiContext context, AiActionCall action)
        {
            GameObject shroom = context.FindNearest("shroom");
            if (shroom == null)
            {
                agent.CompleteAction(false);
                yield break;
            }

            Vector3 sampledPosition;
            if (!agent.SampleNavigation(shroom.transform.position, out sampledPosition))
            {
                agent.CompleteAction(false);
                yield break;
            }

            agent.NavAgent.SetDestination(sampledPosition);

            while (shroom != null && Vector3.Distance(agent.transform.position, shroom.transform.position) > agent.PickupDistance)
            {
                if (agent.IsCurrentActionInterrupted())
                {
                    agent.NavAgent.ResetPath();
                    agent.CompleteAction(AiActionResult.Interrupted);
                    yield break;
                }

                yield return null;
            }

            if (agent.IsCurrentActionInterrupted())
            {
                agent.NavAgent.ResetPath();
                agent.CompleteAction(AiActionResult.Interrupted);
                yield break;
            }

            if (shroom == null)
            {
                agent.CompleteAction(false);
                yield break;
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.shrooms.Remove(shroom);
            }

            shroom.SetActive(false);
            agent.CompleteAction(true);
        }
    }
}
