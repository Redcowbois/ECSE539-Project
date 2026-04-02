using System;
using System.Collections.Generic;
using UnityEngine;

namespace AIDsl.Runtime
{
    public enum AiActionResult
    {
        Success,
        Failure,
        Interrupted
    }

    public enum AiTargetKind
    {
        Slot,
        NearestKind
    }

    public sealed class AiTargetSpec
    {
        public AiTargetKind Kind { get; private set; }
        public string Value { get; private set; }

        public AiTargetSpec(AiTargetKind kind, string value)
        {
            Kind = kind;
            Value = value;
        }
    }

    public sealed class AiActionCall
    {
        public string Id { get; private set; }
        public AiTargetSpec Target { get; private set; }

        public AiActionCall(string id, AiTargetSpec target = null)
        {
            Id = id;
            Target = target;
        }
    }

    public sealed class AiCondition
    {
        private readonly Func<AiPlanningState, bool> evaluator;

        public AiCondition(Func<AiPlanningState, bool> evaluator)
        {
            this.evaluator = evaluator;
        }

        public bool Evaluate(AiPlanningState state)
        {
            return evaluator(state);
        }

        public AiCondition And(AiCondition other)
        {
            return new AiCondition(delegate(AiPlanningState state)
            {
                return Evaluate(state) && other.Evaluate(state);
            });
        }

        public AiCondition Or(AiCondition other)
        {
            return new AiCondition(delegate(AiPlanningState state)
            {
                return Evaluate(state) || other.Evaluate(state);
            });
        }
    }

    public static class Cues
    {
        public static AiCondition Idle()
        {
            return new AiCondition(delegate(AiPlanningState state) { return !state.IsBusy; });
        }

        public static AiCondition TreasureTaken()
        {
            return new AiCondition(delegate(AiPlanningState state) { return state.IsTreasureTaken; });
        }

        public static AiCondition Sees(string slotName)
        {
            return new AiCondition(delegate(AiPlanningState state) { return state.IsSeen(slotName); });
        }

        public static AiCondition At(string slotName)
        {
            return new AiCondition(delegate(AiPlanningState state) { return state.IsAt(slotName); });
        }

        public static AiCondition Has(string itemName)
        {
            return new AiCondition(delegate(AiPlanningState state) { return state.HasItem(itemName); });
        }

        public static AiCondition Exists(string kindName)
        {
            return new AiCondition(delegate(AiPlanningState state) { return state.Exists(kindName); });
        }

        public static AiCondition Not(AiCondition inner)
        {
            return new AiCondition(delegate(AiPlanningState state) { return !inner.Evaluate(state); });
        }
    }

    public abstract class AiStatement
    {
    }

    public sealed class AiActionStatement : AiStatement
    {
        public AiActionCall Action { get; private set; }

        public AiActionStatement(AiActionCall action)
        {
            Action = action;
        }
    }

    public sealed class AiSequenceStatement : AiStatement
    {
        public List<AiActionCall> Actions { get; private set; }

        public AiSequenceStatement(IEnumerable<AiActionCall> actions)
        {
            Actions = new List<AiActionCall>(actions);
        }
    }

    public sealed class AiChoiceOption
    {
        public float? ChancePercent { get; private set; }
        public AiStatement Statement { get; private set; }

        public AiChoiceOption(AiStatement statement, float? chancePercent = null)
        {
            Statement = statement;
            ChancePercent = chancePercent;
        }
    }

    public sealed class AiChooseStatement : AiStatement
    {
        public List<AiChoiceOption> Options { get; private set; }

        public AiChooseStatement(IEnumerable<AiChoiceOption> options)
        {
            Options = new List<AiChoiceOption>(options);
        }
    }

    public static class Targets
    {
        public static AiTargetSpec Slot(string slotName)
        {
            return new AiTargetSpec(AiTargetKind.Slot, slotName);
        }

        public static AiTargetSpec Nearest(string kindName)
        {
            return new AiTargetSpec(AiTargetKind.NearestKind, kindName);
        }
    }

    public static class Actions
    {
        public static AiActionCall Chase(string slotName)
        {
            return new AiActionCall("chase", Targets.Slot(slotName));
        }

        public static AiActionCall MoveTo(AiTargetSpec target)
        {
            return new AiActionCall("move_to", target);
        }

        public static AiActionCall GoHome()
        {
            return new AiActionCall("go_home");
        }

        public static AiActionCall Jump()
        {
            return new AiActionCall("jump");
        }

        public static AiActionCall Spin()
        {
            return new AiActionCall("spin");
        }

        public static AiActionCall Wait()
        {
            return new AiActionCall("wait");
        }

        public static AiActionCall PickRock()
        {
            return new AiActionCall("pick_rock");
        }

        public static AiActionCall ThrowAt(string slotName)
        {
            return new AiActionCall("throw_at", Targets.Slot(slotName));
        }

        public static AiActionCall EatNearestShroom()
        {
            return new AiActionCall("eat_nearest_shroom");
        }

        public static AiActionCall Startle(string slotName)
        {
            return new AiActionCall("startle", Targets.Slot(slotName));
        }

        public static AiActionCall Flee(string slotName)
        {
            return new AiActionCall("flee", Targets.Slot(slotName));
        }

        public static AiActionCall Wander(string slotName)
        {
            return new AiActionCall("wander", Targets.Slot(slotName));
        }
    }

    public static class Statements
    {
        public static AiActionStatement Action(AiActionCall action)
        {
            return new AiActionStatement(action);
        }

        public static AiSequenceStatement Sequence(params AiActionCall[] actions)
        {
            return new AiSequenceStatement(actions);
        }

        public static AiChoiceOption Option(AiStatement statement)
        {
            return new AiChoiceOption(statement);
        }

        public static AiChoiceOption Chance(float percent, AiStatement statement)
        {
            return new AiChoiceOption(statement, percent);
        }

        public static AiChooseStatement Choose(params AiChoiceOption[] options)
        {
            return new AiChooseStatement(options);
        }
    }

    public sealed class AiRuleDefinition
    {
        public string Name { get; private set; }
        public AiCondition Condition { get; private set; }
        public AiStatement Body { get; private set; }
        public AiCondition InterruptCondition { get; private set; }

        public AiRuleDefinition(string name, AiCondition condition, AiStatement body, AiCondition interruptCondition = null)
        {
            Name = name;
            Condition = condition;
            Body = body;
            InterruptCondition = interruptCondition;
        }
    }

    public sealed class AiBehaviorDefinition
    {
        public string RoleName { get; private set; }
        public List<string> Slots { get; private set; }
        public List<AiRuleDefinition> Rules { get; private set; }

        public AiBehaviorDefinition(string roleName, IEnumerable<string> slots, IEnumerable<AiRuleDefinition> rules)
        {
            RoleName = roleName;
            Slots = new List<string>(slots);
            Rules = new List<AiRuleDefinition>(rules);
        }
    }

    public sealed class AiBehaviorBuilder
    {
        private readonly List<string> slots = new List<string>();
        private readonly List<AiRuleDefinition> rules = new List<AiRuleDefinition>();
        private string roleName = "UnnamedRole";

        public void SetRole(string value)
        {
            roleName = value;
        }

        public void DeclareSlot(string slotName)
        {
            if (!slots.Contains(slotName))
            {
                slots.Add(slotName);
            }
        }

        public void AddRule(string name, AiCondition condition, AiStatement body, AiCondition interruptCondition = null)
        {
            rules.Add(new AiRuleDefinition(name, condition, body, interruptCondition));
        }

        public AiBehaviorDefinition Build()
        {
            return new AiBehaviorDefinition(roleName, slots, rules);
        }
    }

    public sealed class AiPlanStep
    {
        public string Name { get; private set; }
        public string RuleName { get; private set; }
        public AiActionCall Action { get; private set; }
        public AiCondition InterruptCondition { get; private set; }

        public AiPlanStep(string name, string ruleName, AiActionCall action, AiCondition interruptCondition)
        {
            Name = name;
            RuleName = ruleName;
            Action = action;
            InterruptCondition = interruptCondition;
        }
    }

    public sealed class AiPlanningState
    {
        private readonly Dictionary<string, bool> seenSlots = new Dictionary<string, bool>();
        private readonly Dictionary<string, bool> atSlots = new Dictionary<string, bool>();
        private readonly Dictionary<string, bool> existingKinds = new Dictionary<string, bool>();
        private readonly HashSet<string> items = new HashSet<string>();

        public bool IsBusy { get; set; }
        public bool IsTreasureTaken { get; set; }

        public AiPlanningState Clone()
        {
            AiPlanningState clone = new AiPlanningState();
            clone.CopyFrom(this);
            return clone;
        }

        public void CopyFrom(AiPlanningState source)
        {
            IsBusy = source.IsBusy;
            IsTreasureTaken = source.IsTreasureTaken;

            seenSlots.Clear();
            foreach (KeyValuePair<string, bool> pair in source.seenSlots)
            {
                seenSlots[pair.Key] = pair.Value;
            }

            atSlots.Clear();
            foreach (KeyValuePair<string, bool> pair in source.atSlots)
            {
                atSlots[pair.Key] = pair.Value;
            }

            existingKinds.Clear();
            foreach (KeyValuePair<string, bool> pair in source.existingKinds)
            {
                existingKinds[pair.Key] = pair.Value;
            }

            items.Clear();
            foreach (string item in source.items)
            {
                items.Add(item);
            }
        }

        public bool IsSeen(string slotName)
        {
            bool value;
            return seenSlots.TryGetValue(slotName, out value) && value;
        }

        public void SetSeen(string slotName, bool value)
        {
            seenSlots[slotName] = value;
        }

        public bool IsAt(string slotName)
        {
            bool value;
            return atSlots.TryGetValue(slotName, out value) && value;
        }

        public void SetAt(string slotName, bool value)
        {
            atSlots[slotName] = value;
        }

        public void ClearAtFlags()
        {
            List<string> keys = new List<string>(atSlots.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                atSlots[keys[i]] = false;
            }
        }

        public bool Exists(string kindName)
        {
            bool value;
            return existingKinds.TryGetValue(kindName, out value) && value;
        }

        public void SetExists(string kindName, bool value)
        {
            existingKinds[kindName] = value;
        }

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
    }
}
