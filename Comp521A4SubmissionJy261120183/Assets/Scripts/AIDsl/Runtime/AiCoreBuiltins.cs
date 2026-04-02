using System.Collections;
using UnityEngine;

namespace AIDsl.Runtime
{
    public static class AiCoreBuiltins
    {
        public static void Register(AiActionRegistry registry)
        {
            registry.Register(new AiActionDefinition(
                "chase",
                delegate(AiPlanningState state, AiActionCall action)
                {
                    return action.Target != null && action.Target.Kind == AiTargetKind.Slot && state.IsSeen(action.Target.Value);
                },
                delegate(AiPlanningState state, AiActionCall action)
                {
                    state.IsBusy = true;
                    state.ClearAtFlags();
                },
                ExecuteChase));

            registry.Register(new AiActionDefinition(
                "move_to",
                delegate(AiPlanningState state, AiActionCall action)
                {
                    if (action.Target == null)
                    {
                        return false;
                    }

                    return action.Target.Kind == AiTargetKind.Slot || state.Exists(action.Target.Value);
                },
                delegate(AiPlanningState state, AiActionCall action)
                {
                    state.IsBusy = true;
                    if (action.Target != null && action.Target.Kind == AiTargetKind.Slot)
                    {
                        state.ClearAtFlags();
                        state.SetAt(action.Target.Value, true);
                    }
                },
                ExecuteMoveTo));

            registry.Register(new AiActionDefinition(
                "go_home",
                delegate(AiPlanningState state, AiActionCall action)
                {
                    return true;
                },
                delegate(AiPlanningState state, AiActionCall action)
                {
                    state.IsBusy = true;
                    state.ClearAtFlags();
                    state.SetAt("base", true);
                },
                ExecuteGoHome));

            registry.Register(new AiActionDefinition(
                "jump",
                delegate(AiPlanningState state, AiActionCall action)
                {
                    return !state.IsBusy;
                },
                delegate(AiPlanningState state, AiActionCall action)
                {
                    state.IsBusy = true;
                },
                ExecuteJump));

            registry.Register(new AiActionDefinition(
                "spin",
                delegate(AiPlanningState state, AiActionCall action)
                {
                    return !state.IsBusy;
                },
                delegate(AiPlanningState state, AiActionCall action)
                {
                    state.IsBusy = true;
                },
                ExecuteSpin));

            registry.Register(new AiActionDefinition(
                "wait",
                delegate(AiPlanningState state, AiActionCall action)
                {
                    return !state.IsBusy;
                },
                delegate(AiPlanningState state, AiActionCall action)
                {
                    state.IsBusy = true;
                },
                ExecuteWait));

            registry.Register(new AiActionDefinition(
                "startle",
                delegate(AiPlanningState state, AiActionCall action)
                {
                    return action.Target != null && action.Target.Kind == AiTargetKind.Slot;
                },
                delegate(AiPlanningState state, AiActionCall action)
                {
                    state.IsBusy = true;
                },
                ExecuteStartle));

            registry.Register(new AiActionDefinition(
                "flee",
                delegate(AiPlanningState state, AiActionCall action)
                {
                    return action.Target != null && action.Target.Kind == AiTargetKind.Slot;
                },
                delegate(AiPlanningState state, AiActionCall action)
                {
                    state.IsBusy = true;
                    state.ClearAtFlags();
                },
                ExecuteFlee));

            registry.Register(new AiActionDefinition(
                "wander",
                delegate(AiPlanningState state, AiActionCall action)
                {
                    return action.Target != null && action.Target.Kind == AiTargetKind.Slot;
                },
                delegate(AiPlanningState state, AiActionCall action)
                {
                    state.IsBusy = true;
                },
                ExecuteWander));
        }

        private static IEnumerator ExecuteChase(AiAgentBase agent, AiContext context, AiActionCall action)
        {
            GameObject target;
            if (!context.TryResolveTarget(action.Target, out target))
            {
                agent.CompleteAction(false);
                yield break;
            }

            while (target != null && agent.CanSeeTarget(action.Target.Value, target))
            {
                if (agent.IsCurrentActionInterrupted())
                {
                    agent.NavAgent.ResetPath();
                    agent.CompleteAction(AiActionResult.Interrupted);
                    yield break;
                }

                Vector3 sampledPosition;
                if (agent.SampleNavigation(target.transform.position, out sampledPosition))
                {
                    agent.NavAgent.SetDestination(sampledPosition);
                }

                yield return null;
            }

            agent.NavAgent.ResetPath();
            agent.CompleteAction(true);
        }

        private static IEnumerator ExecuteMoveTo(AiAgentBase agent, AiContext context, AiActionCall action)
        {
            GameObject target;
            if (!context.TryResolveTarget(action.Target, out target))
            {
                agent.CompleteAction(false);
                yield break;
            }

            Vector3 sampledPosition;
            if (!agent.SampleNavigation(target.transform.position, out sampledPosition))
            {
                agent.CompleteAction(false);
                yield break;
            }

            agent.NavAgent.SetDestination(sampledPosition);

            while (!agent.HasReached(target.transform.position))
            {
                if (agent.IsCurrentActionInterrupted())
                {
                    agent.NavAgent.ResetPath();
                    agent.CompleteAction(AiActionResult.Interrupted);
                    yield break;
                }

                yield return null;
            }

            agent.CompleteAction(true);
        }

        private static IEnumerator ExecuteGoHome(AiAgentBase agent, AiContext context, AiActionCall action)
        {
            GameObject home;
            if (!context.Slots.TryGet("base", out home) || home == null)
            {
                agent.CompleteAction(false);
                yield break;
            }

            Vector3 sampledPosition;
            if (!agent.SampleNavigation(home.transform.position, out sampledPosition))
            {
                agent.CompleteAction(false);
                yield break;
            }

            agent.NavAgent.SetDestination(sampledPosition);

            while (!agent.HasReached(home.transform.position))
            {
                if (agent.IsCurrentActionInterrupted())
                {
                    agent.NavAgent.ResetPath();
                    agent.CompleteAction(AiActionResult.Interrupted);
                    yield break;
                }

                yield return null;
            }

            agent.CompleteAction(true);
        }

        private static IEnumerator ExecuteJump(AiAgentBase agent, AiContext context, AiActionCall action)
        {
            float startTime = Time.time;
            float nextJumpAt = Time.time;
            agent.NavAgent.enabled = false;

            while (Time.time < startTime + agent.JumpDuration)
            {
                if (agent.IsCurrentActionInterrupted())
                {
                    agent.NavAgent.enabled = true;
                    agent.CompleteAction(AiActionResult.Interrupted);
                    yield break;
                }

                if (Time.time >= nextJumpAt && Physics.Raycast(agent.transform.position + Vector3.up * 0.1f, Vector3.down, 0.2f))
                {
                    agent.Body.AddForce(Vector3.up * agent.JumpForce, ForceMode.Impulse);
                    nextJumpAt = Time.time + 0.25f;
                }

                yield return null;
            }

            agent.NavAgent.enabled = true;
            agent.CompleteAction(true);
        }

        private static IEnumerator ExecuteSpin(AiAgentBase agent, AiContext context, AiActionCall action)
        {
            float startTime = Time.time;
            while (Time.time < startTime + agent.SpinDuration)
            {
                if (agent.IsCurrentActionInterrupted())
                {
                    agent.CompleteAction(AiActionResult.Interrupted);
                    yield break;
                }

                agent.transform.Rotate(Vector3.up, agent.SpinSpeed * Time.deltaTime);
                yield return null;
            }

            agent.CompleteAction(true);
        }

        private static IEnumerator ExecuteWait(AiAgentBase agent, AiContext context, AiActionCall action)
        {
            float elapsed = 0f;
            while (elapsed < agent.WaitDuration)
            {
                if (agent.IsCurrentActionInterrupted())
                {
                    agent.CompleteAction(AiActionResult.Interrupted);
                    yield break;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            agent.CompleteAction(true);
        }

        private static IEnumerator ExecuteStartle(AiAgentBase agent, AiContext context, AiActionCall action)
        {
            GameObject target;
            if (!context.TryResolveTarget(action.Target, out target))
            {
                agent.CompleteAction(false);
                yield break;
            }

            agent.NavAgent.ResetPath();
            float elapsed = 0f;
            while (elapsed < agent.StartleDuration)
            {
                if (agent.IsCurrentActionInterrupted())
                {
                    agent.CompleteAction(AiActionResult.Interrupted);
                    yield break;
                }

                if (target != null)
                {
                    Vector3 flatDirection = target.transform.position - agent.transform.position;
                    flatDirection.y = 0f;
                    if (flatDirection.sqrMagnitude > 0.001f)
                    {
                        Quaternion targetRotation = Quaternion.LookRotation(flatDirection.normalized);
                        agent.transform.rotation = Quaternion.RotateTowards(
                            agent.transform.rotation,
                            targetRotation,
                            agent.StartleTurnSpeed * Time.deltaTime);
                    }
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            agent.CompleteAction(true);
        }

        private static IEnumerator ExecuteFlee(AiAgentBase agent, AiContext context, AiActionCall action)
        {
            GameObject target;
            if (!context.TryResolveTarget(action.Target, out target))
            {
                agent.CompleteAction(false);
                yield break;
            }

            Vector3 currentDestination = agent.transform.position;
            bool hasDestination = false;
            float timeoutAt = Time.time + agent.FleeTimeout;
            float nextRepathAt = Time.time;

            while (Time.time < timeoutAt)
            {
                if (agent.IsCurrentActionInterrupted())
                {
                    agent.NavAgent.ResetPath();
                    agent.CompleteAction(AiActionResult.Interrupted);
                    yield break;
                }

                if (target == null)
                {
                    agent.CompleteAction(false);
                    yield break;
                }

                bool canSeeTarget = agent.CanSeeTarget(action.Target.Value, target);
                float distanceToTarget = Vector3.Distance(agent.transform.position, target.transform.position);

                if (!hasDestination || Time.time >= nextRepathAt)
                {
                    if (!TryGetFleeDestination(agent, target.transform.position, out currentDestination))
                    {
                        agent.NavAgent.ResetPath();
                        agent.CompleteAction(false);
                        yield break;
                    }

                    agent.NavAgent.SetDestination(currentDestination);
                    hasDestination = true;
                    nextRepathAt = Time.time + agent.FleeRepathInterval;
                }

                if (agent.HasReached(currentDestination))
                {
                    if (!canSeeTarget || distanceToTarget >= agent.FleeSafeDistance)
                    {
                        agent.NavAgent.ResetPath();
                        agent.CompleteAction(true);
                        yield break;
                    }

                    hasDestination = false;
                }

                yield return null;
            }

            agent.NavAgent.ResetPath();
            agent.CompleteAction(false);
        }

        private static IEnumerator ExecuteWander(AiAgentBase agent, AiContext context, AiActionCall action)
        {
            GameObject anchor;
            if (!context.TryResolveTarget(action.Target, out anchor))
            {
                agent.CompleteAction(false);
                yield break;
            }

            Vector3 destination;
            if (!TryGetWanderDestination(agent, anchor.transform.position, out destination))
            {
                agent.CompleteAction(false);
                yield break;
            }

            agent.NavAgent.SetDestination(destination);

            while (!agent.HasReached(destination))
            {
                if (agent.IsCurrentActionInterrupted())
                {
                    agent.NavAgent.ResetPath();
                    agent.CompleteAction(AiActionResult.Interrupted);
                    yield break;
                }

                yield return null;
            }

            agent.NavAgent.ResetPath();
            agent.CompleteAction(true);
        }

        private static bool TryGetWanderDestination(AiAgentBase agent, Vector3 anchorPosition, out Vector3 destination)
        {
            for (int attempt = 0; attempt < agent.WanderSampleAttempts; attempt++)
            {
                Vector2 randomOffset = UnityEngine.Random.insideUnitCircle * agent.WanderRadius;
                Vector3 candidate = new Vector3(
                    anchorPosition.x + randomOffset.x,
                    anchorPosition.y,
                    anchorPosition.z + randomOffset.y);

                if (agent.SampleNavigation(candidate, out destination))
                {
                    return true;
                }
            }

            destination = anchorPosition;
            return false;
        }

        private static bool TryGetFleeDestination(AiAgentBase agent, Vector3 threatPosition, out Vector3 destination)
        {
            Vector3 away = agent.transform.position - threatPosition;
            away.y = 0f;
            if (away.sqrMagnitude < 0.001f)
            {
                away = -agent.transform.forward;
                away.y = 0f;
            }

            away.Normalize();
            if (away.sqrMagnitude < 0.001f)
            {
                away = Vector3.back;
            }

            Vector3[] directions = new Vector3[]
            {
                away,
                Quaternion.Euler(0f, 35f, 0f) * away,
                Quaternion.Euler(0f, -35f, 0f) * away,
                Quaternion.Euler(0f, 70f, 0f) * away,
                Quaternion.Euler(0f, -70f, 0f) * away
            };

            for (int i = 0; i < directions.Length; i++)
            {
                Vector3 candidate = agent.transform.position + directions[i].normalized * agent.FleeDistance;
                if (agent.SampleNavigation(candidate, out destination))
                {
                    return true;
                }
            }

            destination = agent.transform.position;
            return false;
        }
    }
}
