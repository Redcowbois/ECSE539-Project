using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AIDsl.Runtime
{
    public delegate IEnumerator AiActionExecutor(AiAgentBase agent, AiContext context, AiActionCall action);

    public sealed class AiActionDefinition
    {
        public string Id { get; private set; }
        public Func<AiPlanningState, AiActionCall, bool> CanPlan { get; private set; }
        public Action<AiPlanningState, AiActionCall> ApplyEffects { get; private set; }
        public AiActionExecutor Execute { get; private set; }

        public AiActionDefinition(
            string id,
            Func<AiPlanningState, AiActionCall, bool> canPlan,
            Action<AiPlanningState, AiActionCall> applyEffects,
            AiActionExecutor execute)
        {
            Id = id;
            CanPlan = canPlan;
            ApplyEffects = applyEffects;
            Execute = execute;
        }
    }

    public sealed class AiActionRegistry
    {
        private readonly Dictionary<string, AiActionDefinition> definitions = new Dictionary<string, AiActionDefinition>();

        public void Register(AiActionDefinition definition)
        {
            definitions[definition.Id] = definition;
        }

        public bool TryGet(string actionId, out AiActionDefinition definition)
        {
            return definitions.TryGetValue(actionId, out definition);
        }

        public AiActionDefinition Get(string actionId)
        {
            AiActionDefinition definition;
            if (!definitions.TryGetValue(actionId, out definition))
            {
                throw new InvalidOperationException("Unknown AI action: " + actionId);
            }

            return definition;
        }
    }

    public abstract class AiTask
    {
        public string Name { get; private set; }

        protected AiTask(string name)
        {
            Name = name;
        }
    }

    internal sealed class AiPrimitiveTask : AiTask
    {
        public string RuleName { get; private set; }
        public AiActionCall Action { get; private set; }
        public AiCondition InterruptCondition { get; private set; }
        public Func<AiPlanningState, bool> Preconditions { get; private set; }
        public Action<AiPlanningState> Effects { get; private set; }

        public AiPrimitiveTask(
            string name,
            string ruleName,
            AiActionCall action,
            AiCondition interruptCondition,
            Func<AiPlanningState, bool> preconditions,
            Action<AiPlanningState> effects)
            : base(name)
        {
            RuleName = ruleName;
            Action = action;
            InterruptCondition = interruptCondition;
            Preconditions = preconditions;
            Effects = effects;
        }
    }

    internal sealed class AiCompoundTask : AiTask
    {
        public Func<AiPlanningState, bool> Preconditions { get; private set; }
        public List<AiMethod> Methods { get; private set; }

        public AiCompoundTask(string name, Func<AiPlanningState, bool> preconditions = null)
            : base(name)
        {
            Preconditions = preconditions;
            Methods = new List<AiMethod>();
        }

        public AiCompoundTask AddMethod(AiMethod method)
        {
            Methods.Add(method);
            return this;
        }
    }

    internal sealed class AiMethod
    {
        public string Name { get; private set; }
        public Func<AiPlanningState, bool> Preconditions { get; private set; }
        public List<AiTask> Subtasks { get; private set; }

        public AiMethod(string name, Func<AiPlanningState, bool> preconditions = null)
        {
            Name = name;
            Preconditions = preconditions;
            Subtasks = new List<AiTask>();
        }

        public AiMethod AddSubtask(AiTask task)
        {
            Subtasks.Add(task);
            return this;
        }
    }

    public sealed class AiPlanner
    {
        public List<AiPlanStep> Plan(AiTask root, AiPlanningState initialState)
        {
            List<AiPlanStep> plan = new List<AiPlanStep>();
            AiPlanningState workingState = initialState.Clone();

            if (SeekPlan(root, workingState, plan))
            {
                return plan;
            }

            return null;
        }

        private bool SeekPlan(AiTask task, AiPlanningState state, List<AiPlanStep> plan)
        {
            AiPrimitiveTask primitiveTask = task as AiPrimitiveTask;
            if (primitiveTask != null)
            {
                if (primitiveTask.Preconditions != null && !primitiveTask.Preconditions(state))
                {
                    return false;
                }

                if (primitiveTask.Effects != null)
                {
                    primitiveTask.Effects(state);
                }

                plan.Add(new AiPlanStep(primitiveTask.Name, primitiveTask.RuleName, primitiveTask.Action, primitiveTask.InterruptCondition));
                return true;
            }

            AiCompoundTask compoundTask = task as AiCompoundTask;
            if (compoundTask == null)
            {
                return false;
            }

            if (compoundTask.Preconditions != null && !compoundTask.Preconditions(state))
            {
                return false;
            }

            for (int methodIndex = 0; methodIndex < compoundTask.Methods.Count; methodIndex++)
            {
                AiMethod method = compoundTask.Methods[methodIndex];
                if (method.Preconditions != null && !method.Preconditions(state))
                {
                    continue;
                }

                AiPlanningState savedState = state.Clone();
                int savedPlanCount = plan.Count;
                bool success = true;

                for (int subtaskIndex = 0; subtaskIndex < method.Subtasks.Count; subtaskIndex++)
                {
                    if (!SeekPlan(method.Subtasks[subtaskIndex], state, plan))
                    {
                        success = false;
                        break;
                    }
                }

                if (success)
                {
                    return true;
                }

                RestoreState(state, savedState);
                if (plan.Count > savedPlanCount)
                {
                    plan.RemoveRange(savedPlanCount, plan.Count - savedPlanCount);
                }
            }

            return false;
        }

        private void RestoreState(AiPlanningState target, AiPlanningState source)
        {
            target.CopyFrom(source);
        }
    }

    public static class AiBehaviorCompiler
    {
        public static AiTask Compile(AiBehaviorDefinition behavior, AiActionRegistry actionRegistry)
        {
            AiCompoundTask root = new AiCompoundTask(behavior.RoleName + "_root", delegate { return true; });

            for (int i = 0; i < behavior.Rules.Count; i++)
            {
                AiRuleDefinition rule = behavior.Rules[i];
                AiMethod method = new AiMethod(rule.Name, delegate(AiPlanningState state)
                {
                    return rule.Condition.Evaluate(state);
                });

                List<AiTask> subtasks = CompileStatement(rule.Name, rule.Name, rule.InterruptCondition, rule.Body, actionRegistry);
                for (int subtaskIndex = 0; subtaskIndex < subtasks.Count; subtaskIndex++)
                {
                    method.AddSubtask(subtasks[subtaskIndex]);
                }

                root.AddMethod(method);
            }

            return root;
        }

        private static List<AiTask> CompileStatement(
            string scopeName,
            string ruleName,
            AiCondition interruptCondition,
            AiStatement statement,
            AiActionRegistry actionRegistry)
        {
            List<AiTask> tasks = new List<AiTask>();

            AiActionStatement actionStatement = statement as AiActionStatement;
            if (actionStatement != null)
            {
                tasks.Add(CompilePrimitive(scopeName, ruleName, interruptCondition, actionStatement.Action, actionRegistry));
                return tasks;
            }

            AiSequenceStatement sequenceStatement = statement as AiSequenceStatement;
            if (sequenceStatement != null)
            {
                for (int i = 0; i < sequenceStatement.Actions.Count; i++)
                {
                    tasks.Add(CompilePrimitive(scopeName + "_step" + i, ruleName, interruptCondition, sequenceStatement.Actions[i], actionRegistry));
                }

                return tasks;
            }

            AiChooseStatement chooseStatement = statement as AiChooseStatement;
            if (chooseStatement != null)
            {
                AiCompoundTask choiceTask = new AiCompoundTask(scopeName + "_choice", delegate { return true; });
                for (int optionIndex = 0; optionIndex < chooseStatement.Options.Count; optionIndex++)
                {
                    AiChoiceOption option = chooseStatement.Options[optionIndex];
                    Func<AiPlanningState, bool> preconditions = BuildChoicePrecondition(option);
                    AiMethod optionMethod = new AiMethod(scopeName + "_option" + optionIndex, preconditions);
                    List<AiTask> optionTasks = CompileStatement(scopeName + "_option" + optionIndex, ruleName, interruptCondition, option.Statement, actionRegistry);
                    for (int subtaskIndex = 0; subtaskIndex < optionTasks.Count; subtaskIndex++)
                    {
                        optionMethod.AddSubtask(optionTasks[subtaskIndex]);
                    }

                    choiceTask.AddMethod(optionMethod);
                }

                tasks.Add(choiceTask);
                return tasks;
            }

            throw new InvalidOperationException("Unsupported AI statement type: " + statement.GetType().Name);
        }

        private static Func<AiPlanningState, bool> BuildChoicePrecondition(AiChoiceOption option)
        {
            if (!option.ChancePercent.HasValue)
            {
                return delegate { return true; };
            }

            return delegate
            {
                return UnityEngine.Random.value < option.ChancePercent.Value / 100f;
            };
        }

        private static AiPrimitiveTask CompilePrimitive(
            string scopeName,
            string ruleName,
            AiCondition interruptCondition,
            AiActionCall action,
            AiActionRegistry actionRegistry)
        {
            AiActionDefinition definition = actionRegistry.Get(action.Id);
            return new AiPrimitiveTask(
                scopeName + "_" + action.Id,
                ruleName,
                action,
                interruptCondition,
                delegate(AiPlanningState state)
                {
                    return definition.CanPlan(state, action);
                },
                delegate(AiPlanningState state)
                {
                    definition.ApplyEffects(state, action);
                });
        }
    }
}
