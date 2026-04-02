import { afterEach, beforeAll, describe, expect, test } from "vitest";
import { EmptyFileSystem, type LangiumDocument } from "langium";
import { expandToString as s } from "langium/generate";
import { clearDocuments, parseHelper } from "langium/test";
import type { Model } from "htn-script-language";
import { createHtnScriptServices, isModel } from "htn-script-language";

let services: ReturnType<typeof createHtnScriptServices>;
let parse:    ReturnType<typeof parseHelper<Model>>;
let document: LangiumDocument<Model> | undefined;

beforeAll(async () => {
    services = createHtnScriptServices(EmptyFileSystem);
    parse = parseHelper<Model>(services.HtnScript);

    // activate the following if your linking test requires elements from a built-in library, for example
    // await services.shared.workspace.WorkspaceManager.initializeWorkspace([]);
});

afterEach(async () => {
    document && clearDocuments(services.shared, [ document ]);
});

describe('Linking tests', () => {

    test('linking of slot references', async () => {
        document = await parse(`
            role OgreGuard {
                slots { player, base }

                rule chasePlayer {
                    when sees(player) and not at(base)
                    interrupt_when treasure_taken or sees(player)
                    do chase(player);
                }
            }
        `);

        const conjunction = document.parseResult.value.role.rules[0].condition.disjuncts[0] as any;
        const ruleStatement = document.parseResult.value.role.rules[0].statement as any;
        const interruptCondition = document.parseResult.value.role.rules[0].interruptCondition as any;

        expect(
            checkDocumentValid(document) || [
                conjunction.operands[0].slot.ref?.name,
                conjunction.operands[1].operand.slot.ref?.name,
                ruleStatement.statement.action.target.ref?.name,
                interruptCondition.disjuncts[1].operands[0].slot.ref?.name
            ].join('\n')
        ).toBe(s`
            player
            base
            player
            player
        `);
    });

    test('linking of villager action slot references', async () => {
        document = await parse(`
            role Villager {
                slots { ogre, home }

                rule escapeOgre {
                    when sees(ogre)
                    do sequence {
                        startle(ogre);
                        flee(ogre);
                    }
                }

                rule idleRule {
                    when idle and at(home) and not sees(ogre)
                    interrupt_when sees(ogre)
                    choose {
                        wander(home);
                        wait;
                    }
                }
            }
        `);

        const escapeSequence = document.parseResult.value.role.rules[0].statement as any;
        const idleRule = document.parseResult.value.role.rules[1] as any;

        expect(
            checkDocumentValid(document) || [
                escapeSequence.statement.actions[0].action.target.ref?.name,
                escapeSequence.statement.actions[1].action.target.ref?.name,
                idleRule.interruptCondition.disjuncts[0].operands[0].slot.ref?.name,
                idleRule.statement.options[0].statement.action.target.ref?.name
            ].join('\n')
        ).toBe(s`
            ogre
            ogre
            ogre
            home
        `);
    });
});

function checkDocumentValid(document: LangiumDocument): string | undefined {
    return document.parseResult.parserErrors.length && s`
        Parser errors:
          ${document.parseResult.parserErrors.map(e => e.message).join('\n  ')}
    `
        || document.parseResult.value === undefined && `ParseResult is 'undefined'.`
        || !isModel(document.parseResult.value) && `Root AST object is a ${document.parseResult.value.$type}, expected a 'Model'.`
        || undefined;
}
