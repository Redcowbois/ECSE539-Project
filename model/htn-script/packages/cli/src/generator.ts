import type {
    Action,
    ActionStatement,
    AtCue,
    ChooseStatement,
    ChoiceOption,
    Conjunction,
    Condition,
    DoStatement,
    ExistsCue,
    HasCue,
    IdleCue,
    Model,
    MoveToAction,
    NegatedCondition,
    NearestTarget,
    ParenthesizedCondition,
    PrimaryCondition,
    RuleDecl,
    RuleStatement,
    SeesCue,
    SequenceStatement,
    SlotDecl,
    SlotTarget,
    TargetExpr,
    TreasureTakenCue
} from 'htn-script-language';
import { expandToNode, joinToNode, toString } from 'langium/generate';
import * as fs from 'node:fs';
import * as path from 'node:path';
import { extractDestinationAndName } from './util.js';

type GeneratedCondition =
    | Condition
    | Conjunction
    | AtCue
    | ExistsCue
    | HasCue
    | IdleCue
    | NegatedCondition
    | ParenthesizedCondition
    | PrimaryCondition
    | SeesCue
    | TreasureTakenCue;

type GeneratedStatement =
    | RuleStatement
    | DoStatement
    | ChooseStatement
    | ActionStatement
    | SequenceStatement;

export function generateCSharp(model: Model, filePath: string, destination: string | undefined): string {
    const role = model.role;
    const className = `${role.name}Agent`;
    const data = extractDestinationAndName(className, destination);
    const generatedFilePath = `${path.join(data.destination, data.name)}.cs`;
    const fileNode = expandToNode`
        using AIDsl.Runtime;
        using UnityEngine;

        public class ${className} : AiAgentBase
        {
            [Header("Slots")]
            ${joinToNode(role.slots, slot => `[SerializeField] private GameObject ${toFieldName(slot)};`, { appendNewLineIfNotEmpty: true })}

            protected override void BindSlots(AiSlotRegistry slots)
            {
                ${joinToNode(role.slots, slot => `slots.Set("${slot.name}", ${toFieldName(slot)});`, { appendNewLineIfNotEmpty: true })}
            }

            protected override void DefineBehavior(AiBehaviorBuilder builder)
            {
                builder.SetRole("${role.name}");
                ${joinToNode(role.slots, slot => `builder.DeclareSlot("${slot.name}");`, { appendNewLineIfNotEmpty: true })}
                ${joinToNode(role.rules, emitRule, { appendNewLineIfNotEmpty: true })}
            }
        }
    `.appendNewLineIfNotEmpty();

    if (!fs.existsSync(data.destination)) {
        fs.mkdirSync(data.destination, { recursive: true });
    }
    fs.writeFileSync(generatedFilePath, toString(fileNode));
    return generatedFilePath;
}

function emitRule(rule: RuleDecl): string {
    const interruptArgument = rule.interruptCondition ? `,\n            ${emitCondition(rule.interruptCondition)}` : '';
    return toString(expandToNode`
        builder.AddRule(
            "${rule.name}",
            ${emitCondition(rule.condition)},
            ${emitStatement(rule.statement)}${interruptArgument}
        );
    `);
}

function emitCondition(condition: GeneratedCondition): string {
    switch (condition.$type) {
        case 'Condition':
            return chainExpressions(condition.disjuncts.map(emitCondition), 'Or');
        case 'Conjunction':
            return chainExpressions((condition as Conjunction).operands.map(emitCondition), 'And');
        case 'NegatedCondition':
            return `Cues.Not(${emitCondition((condition as NegatedCondition).operand!)})`;
        case 'ParenthesizedCondition':
            return emitCondition((condition as ParenthesizedCondition).condition!);
        case 'IdleCue':
            return 'Cues.Idle()';
        case 'TreasureTakenCue':
            return 'Cues.TreasureTaken()';
        case 'SeesCue':
            return `Cues.Sees("${(condition as SeesCue).slot?.ref?.name}")`;
        case 'AtCue':
            return `Cues.At("${(condition as AtCue).slot?.ref?.name}")`;
        case 'HasCue':
            return `Cues.Has("${(condition as HasCue).item}")`;
        case 'ExistsCue':
            return `Cues.Exists("${(condition as ExistsCue).kind}")`;
        default:
            throw new Error('Unsupported condition type.');
    }
}

function emitStatement(statement: GeneratedStatement): string {
    switch (statement.$type) {
        case 'DoStatement':
            return emitStatement((statement as DoStatement).statement!);
        case 'ActionStatement':
            return `Statements.Action(${emitAction((statement as ActionStatement).action!)})`;
        case 'SequenceStatement':
            return toString(expandToNode`
                Statements.Sequence(
                    ${joinToNode((statement as SequenceStatement).actions, action => emitAction(action.action!), { appendNewLineIfNotEmpty: true, separator: ',' })}
                )
            `);
        case 'ChooseStatement':
            return toString(expandToNode`
                Statements.Choose(
                    ${joinToNode((statement as ChooseStatement).options, emitChoiceOption, { appendNewLineIfNotEmpty: true, separator: ',' })}
                )
            `);
        default:
            throw new Error('Unsupported statement type.');
    }
}

function emitChoiceOption(option: ChoiceOption): string {
    if (option.chance) {
        return `Statements.Chance(${option.chance.percent}f, ${emitStatement(option.statement)})`;
    }

    return `Statements.Option(${emitStatement(option.statement)})`;
}

function emitAction(action: Action): string {
    switch (action.$type) {
        case 'ChaseAction':
            return `Actions.Chase("${action.target?.ref?.name}")`;
        case 'MoveToAction':
            return `Actions.MoveTo(${emitTarget((action as MoveToAction).target!)})`;
        case 'GoHomeAction':
            return 'Actions.GoHome()';
        case 'JumpAction':
            return 'Actions.Jump()';
        case 'SpinAction':
            return 'Actions.Spin()';
        case 'WaitAction':
            return 'Actions.Wait()';
        case 'PickRockAction':
            return 'Actions.PickRock()';
        case 'ThrowAtAction':
            return `Actions.ThrowAt("${action.target?.ref?.name}")`;
        case 'EatNearestShroomAction':
            return 'Actions.EatNearestShroom()';
        case 'StartleAction':
            return `Actions.Startle("${action.target?.ref?.name}")`;
        case 'FleeAction':
            return `Actions.Flee("${action.target?.ref?.name}")`;
        case 'WanderAction':
            return `Actions.Wander("${action.target?.ref?.name}")`;
        default:
            throw new Error('Unsupported action type.');
    }
}

function emitTarget(target: TargetExpr): string {
    switch (target.$type) {
        case 'SlotTarget':
            return `Targets.Slot("${(target as SlotTarget).slot?.ref?.name}")`;
        case 'NearestTarget':
            return `Targets.Nearest("${(target as NearestTarget).kind}")`;
        default:
            throw new Error('Unsupported target type.');
    }
}

function toFieldName(slot: SlotDecl): string {
    if (slot.name === 'base') {
        return 'baseSlot';
    }

    if (slot.name === 'params') {
        return 'paramsSlot';
    }

    return slot.name;
}

function chainExpressions(parts: string[], methodName: 'And' | 'Or'): string {
    if (parts.length === 0) {
        throw new Error(`Cannot emit empty ${methodName} expression.`);
    }

    return parts.slice(1).reduce((left, right) => `${left}.${methodName}(${right})`, parts[0]);
}
