using AIDsl.Runtime;
using UnityEngine;

public class VillagerForagerAgent : AiAgentBase
{
    [Header("Slots")]
    [SerializeField] private GameObject home;

    protected override void BindSlots(AiSlotRegistry slots)
    {
        slots.Set("home", home);
    }

    protected override void DefineBehavior(AiBehaviorBuilder builder)
    {
        builder.SetRole("VillagerForager");
        builder.DeclareSlot("home");
        builder.AddRule(
    "returnHome",
    Cues.Idle().And(Cues.Not(Cues.At("home"))),
    Statements.Action(Actions.MoveTo(Targets.Slot("home")))
);
        builder.AddRule(
    "forage",
    Cues.Idle().And(Cues.At("home")).And(Cues.Exists("shroom")),
    Statements.Choose(
    Statements.Chance(60f, Statements.Action(Actions.EatNearestShroom())),
    Statements.Option(Statements.Action(Actions.Wander("home"))),
    Statements.Option(Statements.Action(Actions.Wait()))
)
);
        builder.AddRule(
    "idleRule",
    Cues.Idle().And(Cues.At("home")),
    Statements.Choose(
    Statements.Option(Statements.Action(Actions.Wander("home"))),
    Statements.Option(Statements.Action(Actions.Wait()))
)
);
    }
}
