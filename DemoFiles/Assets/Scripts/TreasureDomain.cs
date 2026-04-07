using System.Collections.Generic;
using UnityEngine;

public static class TreasureDomain
{
    public static CompoundTask BuildDomain(out WorldState world, List<GameObject> shrooms, List<GameObject> boulders, bool isPlayerSeen, bool isTreasureTaken, bool isIdle, bool isAtBase, bool hasRock, bool isAtCenter)
    {
        world = new WorldState
        {
            isTreasureTaken = isTreasureTaken,
            isPlayerInvis = GameManager.Instance.isPlayerInvis,
            isPlayerSeen = isPlayerSeen,
            isIdle = isIdle,
            isAtBase = isAtBase,
            isAtCenter = isAtCenter,
            hasRock = hasRock,
            shrooms = new List<GameObject>(GameManager.Instance.shrooms),
            boulders = new List<GameObject>(GameManager.Instance.boulders)
        };


        //Define primitives
        var jump = new PrimitiveTask("jump")
            .SetPreconds(w => w.isIdle && UnityEngine.Random.Range(0f, 1f) < 0.3 && !w.isTreasureTaken && !w.isPlayerSeen)
            .SetEffects(w => w.isIdle = false);

        var spin = new PrimitiveTask("spin")
            .SetPreconds(w => w.isIdle && UnityEngine.Random.Range(0f, 1f) < 0.5 && !w.isPlayerSeen)
            .SetEffects(w => w.isIdle = false);

        var pathToShroom = new PrimitiveTask("pathToShroom")
            .SetPreconds(w => w.shrooms.Count != 0 && !w.isTreasureTaken && !w.isPlayerSeen)
            .SetEffects(w => { w.isIdle = false; w.isAtBase = false; });

        var waitAround = new PrimitiveTask("waitAround")
            .SetPreconds(w => w.isAtBase && w.isIdle)
            .SetEffects(w => w.isIdle = false);

        var goHome = new PrimitiveTask("goHome")
            .SetPreconds(w => w.isIdle && !w.isAtBase && !w.isTreasureTaken && !w.isPlayerSeen)
            .SetEffects(w => { w.isIdle = false; w.isAtBase = true; });

        var chasePlayer = new PrimitiveTask("chasePlayer")
            .SetPreconds(w => w.isPlayerSeen && UnityEngine.Random.Range(0f, 1f) < 0.6)
            .SetEffects(w => { w.isIdle = false; w.isAtBase = false; w.isAtCenter = false; });

        var pathToRock = new PrimitiveTask("pathToRock")
            .SetPreconds(w => w.boulders.Count != 0 && w.isPlayerSeen)
            .SetEffects(w => { w.isIdle = false; w.isAtBase = false; w.hasRock = true; w.isAtCenter = false; });

        var throwRock = new PrimitiveTask("throwRock")
            .SetPreconds(w => w.hasRock)
            .SetEffects(w => { w.isIdle = false; w.hasRock = false; });

        var pathToLastSeen = new PrimitiveTask("pathToCenter")
            .SetPreconds(w => w.isIdle && !w.isPlayerSeen && w.isTreasureTaken && !w.isAtCenter)
            .SetEffects(w => { w.isIdle = false; w.isAtBase = false; });



        //define methods and compoundtasks
        var idleActions = new CompoundTask("doIdleAction")
            .SetPreconds(w => w.isIdle && !w.isTreasureTaken && !w.isPlayerSeen)
            .AddMethod(new Method("jump").AddSubtask(jump))
            .AddMethod(new Method("spin").AddSubtask(spin))
            .AddMethod(new Method("shroomEatingSequenceIdle").AddSubtask(pathToShroom))
            .AddMethod(new Method("waitAround").AddSubtask(waitAround)
            );

        var attackActions = new CompoundTask("doAttackAction")
            .SetPreconds(w => w.isPlayerSeen)
            .AddMethod(new Method("chaseAction").AddSubtask(chasePlayer))
            .AddMethod(new Method("throwRockSequence")
                .AddSubtask(pathToRock)
                .AddSubtask(throwRock)
            );

        var afterTreasureTakenIdle = new CompoundTask("afterTreasureTakenIdle")
            .SetPreconds(w => w.isTreasureTaken)
            .AddMethod(new Method("returnToCenter").AddSubtask(pathToLastSeen))
            .AddMethod(new Method("spinCenter").AddSubtask(spin)
            );

        var rootAction = new CompoundTask("root")
            .SetPreconds(w => true)
            .AddMethod(new Method("attackActions").AddSubtask(attackActions))
            .AddMethod(new Method("afterTreasureTakenIdleMethod").AddSubtask(afterTreasureTakenIdle))
            .AddMethod(new Method("rootGoHome").AddSubtask(goHome))
            .AddMethod(new Method("idleActions").AddSubtask(idleActions)
            );

        return rootAction;
    }
}

public static class HTNVisualizer
{
    public static string BuildTaskTree(Task root)
    {
        return Build(root, "", true);
    }

    private static string Build(Task task, string indent, bool last)
    {
        string result = indent;

        result += last ? "└─ " : "├─ ";
        result += task.Name + "\n";

        string childIndent = indent + (last ? "   " : "│  ");

        if (task is CompoundTask ct)
        {
            for (int m = 0; m < ct.Methods.Count; m++)
            {
                var method = ct.Methods[m];
                bool lastMethod = (m == ct.Methods.Count - 1);

                result += childIndent + (lastMethod ? "└─ " : "├─ ");
                result += $"[{method.Name}]\n";

                string subIndent = childIndent + (lastMethod ? "   " : "│  ");

                for (int s = 0; s < method.Subtasks.Count; s++)
                {
                    bool lastSubtask = (s == method.Subtasks.Count - 1);
                    result += Build(method.Subtasks[s], subIndent, lastSubtask);
                }
            }
        }

        return result;
    }
}
