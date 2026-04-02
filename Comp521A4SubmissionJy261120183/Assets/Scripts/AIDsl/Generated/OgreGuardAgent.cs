using AIDsl.Runtime;
using UnityEngine;

public class OgreGuardAgent : AiAgentBase
{
    [Header("Slots")]
    [SerializeField] private GameObject player;
    [SerializeField] private GameObject villager;
    [SerializeField] private GameObject post;

    protected override void BindSlots(AiSlotRegistry slots)
    {
        slots.Set("player", player);
        slots.Set("villager", villager);
        slots.Set("post", post);
    }

    protected override void DefineBehavior(AiBehaviorBuilder builder)
    {
        builder.SetRole("OgreGuard");
        builder.DeclareSlot("player");
        builder.DeclareSlot("villager");
        builder.DeclareSlot("post");
        builder.AddRule(
    "attackPlayer",
    Cues.Sees("player"),
    Statements.Choose(
    Statements.Chance(60f, Statements.Action(Actions.Chase("player"))),
    Statements.Option(Statements.Sequence(
    Actions.PickRock(),
    Actions.ThrowAt("player")
))
)
);
        builder.AddRule(
    "attackVillager",
    Cues.Sees("villager").And(Cues.Not(Cues.Sees("player"))),
    Statements.Choose(
    Statements.Chance(60f, Statements.Action(Actions.Chase("villager"))),
    Statements.Option(Statements.Sequence(
    Actions.PickRock(),
    Actions.ThrowAt("villager")
))
)
);
        builder.AddRule(
    "returnToPost",
    Cues.Idle().And(Cues.Not(Cues.At("post"))).And(Cues.Not(Cues.Sees("player"))).And(Cues.Not(Cues.Sees("villager"))),
    Statements.Action(Actions.MoveTo(Targets.Slot("post"))),
            Cues.Sees("player").Or(Cues.Sees("villager"))
);
        builder.AddRule(
    "guardIdle",
    Cues.Idle().And(Cues.At("post")).And(Cues.Not(Cues.Sees("player"))).And(Cues.Not(Cues.Sees("villager"))),
    Statements.Choose(
    Statements.Chance(30f, Statements.Action(Actions.Jump())),
    Statements.Chance(50f, Statements.Action(Actions.Spin())),
    Statements.Option(Statements.Action(Actions.Wait()))
),
            Cues.Sees("player").Or(Cues.Sees("villager"))
);
    }
}
