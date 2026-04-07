# Overview of Changed Files

This document summarizes the main new or modified files related to the AI DSL refactoring in this project, along with their roles in the system. The focus is on code, DSL files, generators, and documentation — Unity `.meta` files, scene assets, prefab assets, and temporary build artifacts are not covered.

## Language Definition & Validation

- `model/htn-script/packages/language/src/htn-script.langium`

  The Langium grammar definition file, which defines the core syntax of the `.htns` DSL. The language currently supports constructs such as `role`, `slots`, `rule`, `when`, `interrupt_when`, `do`, `choose`, `sequence`, and `chance`, and also defines the forms in which cues and actions can be used directly in the DSL.

- `model/htn-script/packages/language/src/htn-script-validator.ts`

  DSL validation logic. It checks for duplicate definitions, unknown slots, unknown builtins, illegal probability values, empty `choose` / `sequence` blocks, and other issues, helping to catch errors in DSL files early before C# generation.

- `model/htn-script/packages/language/test/*`

  Language-layer test files for verifying grammar, linking, and validation behavior. They serve the Langium language package itself and are not used directly by Unity.

## Transformer & CLI Generator

- `model/htn-script/packages/cli/src/generator.ts`

  The core `.htns -> C#` generator. It reads the AST produced by Langium and converts a DSL role into a Unity `MonoBehaviour` agent script. The generated code declares slot fields and registers rules, conditions, actions, and `interrupt_when` logic via the builder API.

- `model/htn-script/packages/cli/src/util.ts`

  CLI utility helpers, including path handling for default output directories. By default, generated C# files are written to `Assets/Scripts/AIDsl/Generated/` in the Unity project, so that modifying a DSL file and regenerating is immediately reflected in Unity.

- `model/htn-script/packages/cli/src/main.ts`

  CLI command entry point. After editing a `.htns` file, the `generate` command exposed here is the primary way to regenerate Unity C# scripts.

- `model/htn-script/packages/extension/esbuild.mjs`

  Build script for the VS Code extension. It is not involved in Unity runtime and does not perform `.htns -> C#` generation. Its purpose is to bundle the Langium VS Code plugin code into the extension output directory.

## DSL Example Files

- `model/htn-script/examples/OgreGuard.htns`

  DSL behavior definition for the Ogre. It describes a guard-type Ogre: when the `player` is visible, the Ogre prioritizes attacking the player; when the player is not visible but a specified `villager` is, it attacks the villager; attack behavior is chosen probabilistically between chasing and picking up and throwing rocks; when no target is present, the Ogre returns near a `post` and performs idle behavior.

- `model/htn-script/examples/VillagerForager.htns`

  DSL behavior definition for the Villager. It describes a forager-type villager: the villager idles near `home`; when mushrooms are available it performs `eat_nearest_shroom`; if it wanders away from `home` it prioritizes returning.

## Unity AI DSL Runtime

- `DemoFiles/Assets/Scripts/AIDsl/Runtime/AiAgentBase.cs`

  The Unity base class for all generated agents. It handles slot binding, perception checks, the planning loop, the execution loop, action interrupt checking, plan UI text refresh, and basic integration with Unity components such as `NavMeshAgent` and `Rigidbody`.

- `DemoFiles/Assets/Scripts/AIDsl/Runtime/AiBehaviorModel.cs`

  The runtime intermediate model and builder API for the AI DSL. It defines structures for conditions, action calls, statements, rules, and plan steps, and provides factory methods such as `Actions.*`, `Cues.*`, and `Statements.*` that are called by generated code.

- `DemoFiles/Assets/Scripts/AIDsl/Runtime/AiPlanner.cs`

  The planner. It converts the compiled structure of DSL rules and statements into strongly-typed plan steps, replacing the string-concatenation and `switch`-dispatch approach used in the old system.

- `DemoFiles/Assets/Scripts/AIDsl/Runtime/AiCoreBuiltins.cs`

  Implementations of general-purpose built-in cues and actions. This file contains behaviors that are loosely coupled from specific game assets, such as `chase`, `move_to`, `go_home`, `jump`, `spin`, `wait`, `wander`, `startle`, and `flee`.

- `DemoFiles/Assets/Scripts/AIDsl/Game/AiGameBuiltins.cs`

  Game-project-specific built-in behaviors and queries. This file handles project-specific logic such as `treasure_taken`, `pick_rock`, `throw_at`, and `eat_nearest_shroom`, and accesses world resources like mushroom and boulder lists via `GameManager.shrooms` and `GameManager.boulders`.

## Generated Unity Agents

- `DemoFiles/Assets/Scripts/AIDsl/Generated/OgreGuardAgent.cs`

  The Unity agent script generated from `OgreGuard.htns`. It exposes Inspector slot fields for `player`, `villager`, `post`, etc., and reconstructs the Ogre's behavior rules from the DSL inside `DefineBehavior(...)` using the builder API.

- `DemoFiles/Assets/Scripts/AIDsl/Generated/VillagerForagerAgent.cs`

  The Unity agent script generated from `VillagerForager.htns`. It exposes a `home` slot field and registers rules for villager foraging, returning home, and idling.

## Documentation

- `README.md`

  Quick-start guide at the project root. Briefly introduces the location of `.htns` files, the basic DSL syntax, and how to run the CLI to generate Unity C# files.

- `Project_Design_Document.md`

  Chinese design document covering the project goals, DSL language design, transformation pipeline, Unity runtime architecture, example characters, and key design decisions.

- `Language_Engineering_Transformation_Environments.md`

  English report section draft focusing on the language engineering and transformation environments — including Langium, the TypeScript CLI, Unity C# runtime integration, and lessons learned during implementation.

## Current Implementation Limitations

- The DSL does not currently support automatically identifying "all villagers," "all Ogres," or "a class of enemies." Agent targets are still declared explicitly via `slots` and wired up by dragging specific `GameObjects` into the fields exposed by the generated script in the Unity Inspector.
- Multiple villagers or multiple Ogres can be realized by duplicating agent objects, but each `OgreGuardAgent` currently only attacks the specific villager assigned to its `villager` slot.
- `GameManager` is still used as a world-resource registry (e.g., for mushroom and boulder lists); however, villager objects themselves do not need to be registered with `GameManager` for `eat_nearest_shroom` to execute.
