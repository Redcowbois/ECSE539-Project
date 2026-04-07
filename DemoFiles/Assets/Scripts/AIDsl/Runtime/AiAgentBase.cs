using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;

namespace AIDsl.Runtime
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(NavMeshAgent))]
    public abstract class AiAgentBase : MonoBehaviour
    {
        [Header("Planning")]
        [SerializeField] private float planningInterval = 0.1f;
        [SerializeField] private bool verboseLogging = true;

        [Header("UI")]
        [SerializeField] private TextMeshProUGUI planText;

        [Header("Perception")]
        [SerializeField] private float visionRange = 20f;
        [SerializeField] private float fieldOfViewDegrees = 120f;
        [SerializeField] private float slotArrivalDistance = 1.5f;
        [SerializeField] private float navSampleRadius = 2f;

        [Header("Idle Actions")]
        [SerializeField] private float waitDuration = 2f;
        [SerializeField] private float jumpForce = 10f;
        [SerializeField] private float jumpDuration = 3f;
        [SerializeField] private float spinSpeed = 180f;
        [SerializeField] private float spinDuration = 2f;

        [Header("Villager Actions")]
        [SerializeField] private float startleDuration = 0.6f;
        [SerializeField] private float startleTurnSpeed = 360f;
        [SerializeField] private float fleeDistance = 8f;
        [SerializeField] private float fleeSafeDistance = 10f;
        [SerializeField] private float fleeRepathInterval = 0.25f;
        [SerializeField] private float fleeTimeout = 6f;
        [SerializeField] private float wanderRadius = 4f;
        [SerializeField] private int wanderSampleAttempts = 8;

        [Header("Game Actions")]
        [SerializeField] private Transform rockHoldPoint;
        [SerializeField] private Transform obstacleParent;
        [SerializeField] private int treasureId = 1;
        [SerializeField] private float pickupDistance = 2f;
        [SerializeField] private float throwForce = 15f;
        [SerializeField] private float throwUpwardForce = 5f;
        [SerializeField] private float throwDistanceAmplifier = 2f;
        [SerializeField] private float minThrowForce = 5f;
        [SerializeField] private float maxThrowForce = 30f;
        [SerializeField] private float upwardForceMultiplier = 0.5f;

        private readonly Queue<AiPlanStep> currentPlan = new Queue<AiPlanStep>();
        private readonly List<Func<string, GameObject, bool>> visibilityFilters = new List<Func<string, GameObject, bool>>();

        private AiActionRegistry actionRegistry;
        private AiBehaviorDefinition behaviorDefinition;
        private AiTask compiledRootTask;
        private AiPlanner planner;
        private AiContext context;
        private bool isExecutingAction;
        private AiActionResult lastActionResult = AiActionResult.Failure;
        private AiPlanStep currentStep;
        private bool immediateReplanRequested = true;
        private float nextPlanningTime;
        private string currentPlanText = string.Empty;
        private string currentActionText = string.Empty;

        public Rigidbody Body { get; private set; }
        public NavMeshAgent NavAgent { get; private set; }
        public AiContext Context { get { return context; } }
        public bool IsExecutingAction { get { return isExecutingAction; } }
        public float VisionRange { get { return visionRange; } }
        public float FieldOfViewDegrees { get { return fieldOfViewDegrees; } }
        public float SlotArrivalDistance { get { return slotArrivalDistance; } }
        public float NavSampleRadius { get { return navSampleRadius; } }
        public float WaitDuration { get { return waitDuration; } }
        public float JumpForce { get { return jumpForce; } }
        public float JumpDuration { get { return jumpDuration; } }
        public float SpinSpeed { get { return spinSpeed; } }
        public float SpinDuration { get { return spinDuration; } }
        public float StartleDuration { get { return startleDuration; } }
        public float StartleTurnSpeed { get { return startleTurnSpeed; } }
        public float FleeDistance { get { return fleeDistance; } }
        public float FleeSafeDistance { get { return fleeSafeDistance; } }
        public float FleeRepathInterval { get { return fleeRepathInterval; } }
        public float FleeTimeout { get { return fleeTimeout; } }
        public float WanderRadius { get { return wanderRadius; } }
        public int WanderSampleAttempts { get { return wanderSampleAttempts; } }
        public Transform RockHoldPoint { get { return rockHoldPoint; } }
        public Transform ObstacleParent { get { return obstacleParent; } }
        public int TreasureId { get { return treasureId; } }
        public float PickupDistance { get { return pickupDistance; } }
        public float ThrowForce { get { return throwForce; } }
        public float ThrowUpwardForce { get { return throwUpwardForce; } }
        public float ThrowDistanceAmplifier { get { return throwDistanceAmplifier; } }
        public float MinThrowForce { get { return minThrowForce; } }
        public float MaxThrowForce { get { return maxThrowForce; } }
        public float UpwardForceMultiplier { get { return upwardForceMultiplier; } }

        protected virtual void Awake()
        {
            Body = GetComponent<Rigidbody>();
            NavAgent = GetComponent<NavMeshAgent>();

            actionRegistry = new AiActionRegistry();
            planner = new AiPlanner();
            context = new AiContext(this);

            AiCoreBuiltins.Register(actionRegistry);
            AIDsl.Game.AiGameBuiltins.Register(this, actionRegistry, context);

            AiSlotRegistry slots = new AiSlotRegistry();
            BindSlots(slots);
            context.AttachSlots(slots);

            AiBehaviorBuilder builder = new AiBehaviorBuilder();
            DefineBehavior(builder);
            behaviorDefinition = builder.Build();
            compiledRootTask = AiBehaviorCompiler.Compile(behaviorDefinition, actionRegistry);
        }

        protected virtual void Start()
        {
            RefreshPlanText();
            StartCoroutine(PlanningLoop());
            StartCoroutine(ExecutionLoop());
        }

        protected virtual void Update()
        {
            context.RefreshObservationMemory();
        }

        public void RegisterVisibilityFilter(Func<string, GameObject, bool> filter)
        {
            visibilityFilters.Add(filter);
        }

        public bool PassesVisibilityFilters(string slotName, GameObject target)
        {
            for (int i = 0; i < visibilityFilters.Count; i++)
            {
                if (!visibilityFilters[i](slotName, target))
                {
                    return false;
                }
            }

            return true;
        }

        public void CompleteAction(bool succeeded)
        {
            CompleteAction(succeeded ? AiActionResult.Success : AiActionResult.Failure);
        }

        public void CompleteAction(AiActionResult result)
        {
            lastActionResult = result;
        }

        public bool IsCurrentActionInterrupted()
        {
            if (!isExecutingAction || currentStep == null || currentStep.InterruptCondition == null)
            {
                return false;
            }

            return currentStep.InterruptCondition.Evaluate(context.BuildPlanningState());
        }

        public void RequestImmediateReplan()
        {
            immediateReplanRequested = true;
            nextPlanningTime = 0f;
        }

        public bool SampleNavigation(Vector3 desiredPosition, out Vector3 sampledPosition)
        {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(desiredPosition, out hit, navSampleRadius, NavMesh.AllAreas))
            {
                sampledPosition = hit.position;
                return true;
            }

            sampledPosition = desiredPosition;
            return false;
        }

        public bool HasReached(Vector3 destination)
        {
            Vector3 flattenedDestination = new Vector3(destination.x, transform.position.y, destination.z);
            Vector3 flattenedSelf = new Vector3(transform.position.x, transform.position.y, transform.position.z);
            return Vector3.Distance(flattenedSelf, flattenedDestination) <= slotArrivalDistance;
        }

        public bool CanSeeTarget(string slotName, GameObject target)
        {
            if (target == null)
            {
                return false;
            }

            if (!PassesVisibilityFilters(slotName, target))
            {
                return false;
            }

            Vector3 eyePosition = transform.position + Vector3.up;
            Vector3 directionToTarget = target.transform.position - transform.position + Vector3.up;
            float distance = directionToTarget.magnitude;

            if (distance > visionRange)
            {
                return false;
            }

            if (Vector3.Angle(transform.forward, directionToTarget) > fieldOfViewDegrees * 0.5f)
            {
                return false;
            }

            RaycastHit hit;
            if (!Physics.Linecast(eyePosition, target.transform.position, out hit))
            {
                return false;
            }

            return hit.transform == target.transform;
        }

        protected abstract void BindSlots(AiSlotRegistry slots);
        protected abstract void DefineBehavior(AiBehaviorBuilder builder);

        private IEnumerator PlanningLoop()
        {
            while (true)
            {
                if (isExecutingAction || currentPlan.Count > 0)
                {
                    yield return null;
                    continue;
                }

                if (!immediateReplanRequested && planningInterval > 0f && Time.time < nextPlanningTime)
                {
                    yield return null;
                    continue;
                }

                immediateReplanRequested = false;
                nextPlanningTime = planningInterval > 0f ? Time.time + planningInterval : Time.time;

                AiPlanningState state = context.BuildPlanningState();
                List<AiPlanStep> plan = planner.Plan(compiledRootTask, state);

                if (plan != null && plan.Count > 0)
                {
                    currentPlan.Clear();
                    for (int i = 0; i < plan.Count; i++)
                    {
                        currentPlan.Enqueue(plan[i]);
                    }

                    currentPlanText = DescribePlan(plan);
                    RefreshPlanText();
                    if (verboseLogging)
                    {
                        Debug.Log(behaviorDefinition.RoleName + " plan: " + currentPlanText, gameObject);
                    }
                }
                else if (currentPlan.Count == 0)
                {
                    currentPlanText = string.Empty;
                    RefreshPlanText();
                }

                yield return null;
            }
        }

        private IEnumerator ExecutionLoop()
        {
            while (true)
            {
                if (isExecutingAction || currentPlan.Count == 0)
                {
                    yield return null;
                    continue;
                }

                AiPlanStep step = currentPlan.Dequeue();
                currentStep = step;
                currentActionText = step.Action.Id;
                currentPlanText = DescribePlan(currentPlan);
                RefreshPlanText();
                AiActionDefinition definition = actionRegistry.Get(step.Action.Id);
                isExecutingAction = true;
                lastActionResult = AiActionResult.Failure;

                if (verboseLogging)
                {
                    Debug.Log(behaviorDefinition.RoleName + " executing: " + step.Name, gameObject);
                }

                yield return StartCoroutine(definition.Execute(this, context, step.Action));
                isExecutingAction = false;
                currentActionText = string.Empty;
                currentStep = null;

                if (lastActionResult == AiActionResult.Interrupted)
                {
                    currentPlan.Clear();
                    currentPlanText = string.Empty;
                    RefreshPlanText();
                    RequestImmediateReplan();
                    if (verboseLogging)
                    {
                        Debug.Log(behaviorDefinition.RoleName + " interrupted: " + step.RuleName, gameObject);
                    }
                }
                else if (lastActionResult == AiActionResult.Failure)
                {
                    currentPlan.Clear();
                    currentPlanText = string.Empty;
                    RefreshPlanText();
                }
                else
                {
                    if (currentPlan.Count == 0)
                    {
                        currentPlanText = string.Empty;
                    }

                    RefreshPlanText();
                }
            }
        }

        private string DescribePlan(IEnumerable<AiPlanStep> plan)
        {
            List<string> stepNames = new List<string>();
            foreach (AiPlanStep step in plan)
            {
                stepNames.Add(step.Action.Id);
            }

            return string.Join(" -> ", stepNames.ToArray());
        }

        private void RefreshPlanText()
        {
            if (planText == null)
            {
                return;
            }

            string displayText = currentPlanText;

            if (!string.IsNullOrEmpty(currentActionText))
            {
                displayText = string.IsNullOrEmpty(currentPlanText)
                    ? currentActionText
                    : currentActionText + " -> " + currentPlanText;
            }

            planText.text = string.IsNullOrEmpty(displayText)
                ? gameObject.name + ": "
                : gameObject.name + ": " + displayText;
        }
    }

    public sealed class AiSlotRegistry
    {
        private readonly Dictionary<string, GameObject> slots = new Dictionary<string, GameObject>();

        public void Set(string slotName, GameObject value)
        {
            slots[slotName] = value;
        }

        public bool TryGet(string slotName, out GameObject value)
        {
            return slots.TryGetValue(slotName, out value);
        }

        public IEnumerable<KeyValuePair<string, GameObject>> Entries()
        {
            return slots;
        }
    }

    public sealed class AiAgentMemory
    {
        private readonly HashSet<string> items = new HashSet<string>();
        private readonly Dictionary<string, Vector3> lastSeenPositions = new Dictionary<string, Vector3>();

        public bool HasItem(string itemName)
        {
            return items.Contains(itemName);
        }

        public void AddItem(string itemName)
        {
            items.Add(itemName);
        }

        public void RemoveItem(string itemName)
        {
            items.Remove(itemName);
        }

        public IEnumerable<string> Items()
        {
            return items;
        }

        public void RememberLastSeen(string slotName, Vector3 position)
        {
            lastSeenPositions[slotName] = position;
        }

        public bool TryGetLastSeen(string slotName, out Vector3 position)
        {
            return lastSeenPositions.TryGetValue(slotName, out position);
        }
    }

    public sealed class AiContext
    {
        private readonly Dictionary<string, Func<List<GameObject>>> kindProviders = new Dictionary<string, Func<List<GameObject>>>();
        private Func<bool> treasureTakenResolver;

        public AiAgentBase Agent { get; private set; }
        public AiSlotRegistry Slots { get; private set; }
        public AiAgentMemory Memory { get; private set; }

        public AiContext(AiAgentBase agent)
        {
            Agent = agent;
            Memory = new AiAgentMemory();
            treasureTakenResolver = delegate { return false; };
        }

        public void AttachSlots(AiSlotRegistry slots)
        {
            Slots = slots;
        }

        public void SetTreasureTakenResolver(Func<bool> resolver)
        {
            treasureTakenResolver = resolver;
        }

        public void RegisterKindProvider(string kindName, Func<List<GameObject>> provider)
        {
            kindProviders[kindName] = provider;
        }

        public void RefreshObservationMemory()
        {
            if (Slots == null)
            {
                return;
            }

            foreach (KeyValuePair<string, GameObject> slot in Slots.Entries())
            {
                if (slot.Value == null)
                {
                    continue;
                }

                if (Agent.CanSeeTarget(slot.Key, slot.Value))
                {
                    Memory.RememberLastSeen(slot.Key, slot.Value.transform.position);
                }
            }
        }

        public AiPlanningState BuildPlanningState()
        {
            AiPlanningState state = new AiPlanningState();
            state.IsBusy = Agent.IsExecutingAction;
            state.IsTreasureTaken = treasureTakenResolver();

            if (Slots != null)
            {
                foreach (KeyValuePair<string, GameObject> slot in Slots.Entries())
                {
                    bool canSee = slot.Value != null && Agent.CanSeeTarget(slot.Key, slot.Value);
                    state.SetSeen(slot.Key, canSee);

                    if (canSee && slot.Value != null)
                    {
                        Memory.RememberLastSeen(slot.Key, slot.Value.transform.position);
                    }

                    bool isAt = slot.Value != null && Agent.HasReached(slot.Value.transform.position);
                    state.SetAt(slot.Key, isAt);
                }
            }

            foreach (KeyValuePair<string, Func<List<GameObject>>> provider in kindProviders)
            {
                state.SetExists(provider.Key, CountLiveObjects(provider.Value()) > 0);
            }

            foreach (string item in Memory.Items())
            {
                state.AddItem(item);
            }

            return state;
        }

        public bool TryResolveTarget(AiTargetSpec targetSpec, out GameObject target)
        {
            target = null;

            if (targetSpec == null)
            {
                return false;
            }

            if (targetSpec.Kind == AiTargetKind.Slot)
            {
                if (Slots == null)
                {
                    return false;
                }

                if (!Slots.TryGet(targetSpec.Value, out target))
                {
                    target = null;
                    return false;
                }

                return target != null;
            }

            if (targetSpec.Kind == AiTargetKind.NearestKind)
            {
                target = FindNearest(targetSpec.Value);
                return target != null;
            }

            return false;
        }

        public bool HasKind(string kindName)
        {
            List<GameObject> objects = GetKindObjects(kindName);
            return CountLiveObjects(objects) > 0;
        }

        public GameObject FindNearest(string kindName)
        {
            List<GameObject> objects = GetKindObjects(kindName);
            GameObject nearest = null;
            float shortestDistance = float.PositiveInfinity;
            Vector3 currentPosition = Agent.transform.position;

            for (int i = 0; i < objects.Count; i++)
            {
                GameObject candidate = objects[i];
                if (!IsUsable(candidate))
                {
                    continue;
                }

                float distance = Vector3.SqrMagnitude(candidate.transform.position - currentPosition);
                if (distance < shortestDistance)
                {
                    shortestDistance = distance;
                    nearest = candidate;
                }
            }

            return nearest;
        }

        public bool TryGetLastSeenPosition(string slotName, out Vector3 position)
        {
            return Memory.TryGetLastSeen(slotName, out position);
        }

        private List<GameObject> GetKindObjects(string kindName)
        {
            Func<List<GameObject>> provider;
            if (!kindProviders.TryGetValue(kindName, out provider))
            {
                return new List<GameObject>();
            }

            return provider() ?? new List<GameObject>();
        }

        private static int CountLiveObjects(List<GameObject> objects)
        {
            int count = 0;
            for (int i = 0; i < objects.Count; i++)
            {
                if (IsUsable(objects[i]))
                {
                    count++;
                }
            }

            return count;
        }

        private static bool IsUsable(GameObject value)
        {
            return value != null && value.activeInHierarchy;
        }
    }
}
