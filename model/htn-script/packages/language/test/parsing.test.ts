import { beforeAll, describe, expect, test } from "vitest";
import { EmptyFileSystem, type LangiumDocument } from "langium";
import { expandToString as s } from "langium/generate";
import { parseHelper } from "langium/test";
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

describe('Parsing tests', () => {

    test('parse role with rules and choices', async () => {
        document = await parse(`
            role OgreGuard {
                slots { player, villager, base, center }

                rule attackPlayer {
                    when sees(player)
                    interrupt_when treasure_taken
                    choose {
                        chance 60 -> chase(player);
                        sequence {
                            pick_rock;
                            throw_at(player);
                        }
                    }
                }
            }
        `);

        expect(
            checkDocumentValid(document) || s`
                Role:
                  ${document.parseResult.value?.role?.name}
                Slots:
                  ${document.parseResult.value?.role?.slots?.map(slot => slot.name)?.join('\n  ')}
                Rules:
                  ${document.parseResult.value?.role?.rules?.map(rule => rule.name)?.join('\n  ')}
            `
        ).toBe(s`
            Role:
              OgreGuard
            Slots:
              player
              villager
              base
              center
            Rules:
              attackPlayer
        `);
    });

    test('parse villager role with startle flee and wander', async () => {
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

        expect(checkDocumentValid(document)).toBeUndefined();
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
