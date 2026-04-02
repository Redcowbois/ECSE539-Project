using System;
using System.Collections.Generic;
using UnityEngine;

public class WorldState
{
    public bool isTreasureTaken;
    public bool isPlayerInvis;
    public bool isPlayerSeen;
    public bool isIdle;
    public bool isAtBase;
    public bool isAtCenter;
    public bool hasRock;

    public List<GameObject> shrooms;
    public List<GameObject> boulders;

    public WorldState Clone()
    {
        return new WorldState
        {
            isTreasureTaken = this.isTreasureTaken,
            isIdle = this.isIdle,
            isPlayerInvis = this.isPlayerInvis,
            isPlayerSeen = this.isPlayerSeen,
            isAtBase = this.isAtBase,
            isAtCenter = this.isAtCenter,
            hasRock = this.hasRock,
            shrooms = new List<GameObject>(this.shrooms),
            boulders = new List<GameObject>(this.boulders)
        };
    }
}

//tasks
public abstract class Task
{
    public string Name { get; }
    protected Task(string name) { Name = name; }
    public override string ToString() => Name;
}

public class PrimitiveTask : Task
{
    public Func<WorldState, bool> Preconditions { get; set; }
    public Action<WorldState> Effects { get; set; }

    public PrimitiveTask(string name) : base(name) { }

    public PrimitiveTask SetPreconds(Func<WorldState, bool> pre)
    {
        Preconditions = pre;
        return this;
    }

    public PrimitiveTask SetEffects(Action<WorldState> eff)
    {
        Effects = eff;
        return this;
    }
}

public class CompoundTask : Task
{
    public Func<WorldState, bool> Preconditions { get; set; }
    public List<Method> Methods { get; } = new List<Method>();
    public CompoundTask(string name) : base(name) { }

    public CompoundTask AddMethod(Method m)
    {
        Methods.Add(m);
        return this;
    }

    public CompoundTask SetPreconds(Func<WorldState, bool> pre)
    {
        Preconditions = pre;
        return this;
    }
}

public class Method
{
    public string Name { get; }
    public Func<WorldState, bool> Preconditions { get; set; }
    public List<Task> Subtasks { get; } = new List<Task>();

    public Method(string name) { Name = name; }

    public Method SetPreconds(Func<WorldState, bool> pre)
    {
        Preconditions = pre;
        return this;
    }

    public Method AddSubtask(Task t)
    {
        Subtasks.Add(t);
        return this;
    }
}


public class BacktrackingHTNPlanner
{
    public List<PrimitiveTask> Plan(Task root, WorldState world)
    {
        var plan = new List<PrimitiveTask>();

        // clone the world once at the start so we don't mutate the actual game data.
        WorldState workingWorld = world.Clone();

        // pass workingWorld by 'ref' so updates persist down the tree
        if (SeekPlan(root, ref workingWorld, plan))
        {
            return plan;
        }
        return null;
    }

    // find a plan
    private bool SeekPlan(Task task, ref WorldState world, List<PrimitiveTask> plan)
    {
        // primitive task
        if (task is PrimitiveTask pt)
        {
            if (pt.Preconditions != null && !pt.Preconditions(world))
            {
                return false;
            }

            // Apply effects to the world immediately
            pt.Effects?.Invoke(world);
            plan.Add(pt);
            return true;
        }

        // compound task
        if (task is CompoundTask ct)
        {
            if (ct.Preconditions != null && !ct.Preconditions(world))
            {
                // If the high-level goal isn't allowed, fail immediately.
                return false;
            }

            foreach (var method in ct.Methods)
            {
                if (method.Preconditions != null && !method.Preconditions(world))
                    continue;

                // Save state and plan length before trying this method
                WorldState savedState = world.Clone();
                int savedPlanCount = plan.Count;

                bool methodSuccess = true;
                foreach (var sub in method.Subtasks)
                {
                    // Recurse: We pass the reference. If subtasks succeed, 'world' and 'plan' get updated.
                    if (!SeekPlan(sub, ref world, plan))
                    {
                        methodSuccess = false;
                        break;
                    }
                }

                if (methodSuccess)
                {
                    return true;
                }
            
                world = savedState; // Revert the world state

                // remove tasks added during this failed attempt
                if (plan.Count > savedPlanCount)
                {
                    plan.RemoveRange(savedPlanCount, plan.Count - savedPlanCount);
                }
            }

            // If we tried all methods and none worked:
            return false;
        }

        return false;
    }
}