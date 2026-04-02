# Unity AI DSL Quick Guide

This project uses a small DSL to describe AI behavior and generate Unity C# agent scripts.

## Where to edit

Edit `.htns` files in:

- `model/htn-script/examples/`

Current examples:

- `OgreGuard.htns`
- `VillagerForager.htns`

## DSL syntax

A DSL file defines one role:

```txt
role OgreGuard {
  slots { player, villager, post }

  rule attackPlayer {
    when sees(player)
    choose {
      chance 60 -> chase(player);
      sequence {
        pick_rock;
        throw_at(player);
      }
    }
  }
}
```

Main concepts:

- `role`: one AI role per file
- `slots`: Unity object references exposed in the generated script
- `rule`: one behavior rule
- `when`: rule condition
- `interrupt_when`: optional condition that interrupts the current rule while executing
- `do`: a rule with one body
- `choose`: try options from top to bottom
- `sequence`: run several actions in order
- `chance N ->`: optional probability gate inside `choose`

Common cues:

- `idle`
- `sees(slot)`
- `at(slot)`
- `exists(kind)`
- `treasure_taken`

Common actions:

- `chase(slot)`
- `move_to(slot)`
- `pick_rock`
- `throw_at(slot)`
- `eat_nearest_shroom`
- `jump`
- `spin`
- `wait`
- `wander(slot)`

## Generate Unity code

From `model/htn-script`, generate a role with:

```bash
node packages/cli/bin/cli.js generate examples/OgreGuard.htns
```

Or:

```bash
node packages/cli/bin/cli.js generate examples/VillagerForager.htns
```

Generated C# files are written to:

- `Comp521A4SubmissionJy261120183/Assets/Scripts/AIDsl/Generated/`

## When to rebuild the Langium workspace

If you only changed a `.htns` file, just run `generate`.

If you changed the language, validator, or generator code, run:

```bash
npm run build
```

Then run `generate` again.
