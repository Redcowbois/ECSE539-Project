import { beforeAll, describe, expect, test } from "vitest";
import { EmptyFileSystem, type LangiumDocument } from "langium";
import { expandToString as s } from "langium/generate";
import { parseHelper } from "langium/test";
import type { Diagnostic } from "vscode-languageserver-types";
import type { Model } from "htn-script-language";
import { createHtnScriptServices, isModel } from "htn-script-language";

let services: ReturnType<typeof createHtnScriptServices>;
let parse:    ReturnType<typeof parseHelper<Model>>;
let document: LangiumDocument<Model> | undefined;

beforeAll(async () => {
    services = createHtnScriptServices(EmptyFileSystem);
    const doParse = parseHelper<Model>(services.HtnScript);
    parse = (input: string) => doParse(input, { validation: true });

    // activate the following if your linking test requires elements from a built-in library, for example
    // await services.shared.workspace.WorkspaceManager.initializeWorkspace([]);
});

describe('Validating', () => {

    test('check no errors', async () => {
        document = await parse(`
            role OgreGuard {
                slots { player, base }

                rule chasePlayer {
                    when sees(player) and not at(base)
                    interrupt_when treasure_taken
                    do chase(player);
                }
            }
        `);

        expect(
            checkDocumentValid(document) || document?.diagnostics?.map(diagnosticToString)?.join('\n')
        ).toHaveLength(0);
    });

    test('check duplicate slots and invalid chance validation', async () => {
        document = await parse(`
            role OgreGuard {
                slots { player, player }

                rule idleRule {
                    when idle
                    choose {
                        chance 120 -> wait;
                    }
                }
            }
        `);

        expect(
            checkDocumentValid(document) || document?.diagnostics?.map(diagnosticToString)?.join('\n')
        ).toEqual(
            expect.stringContaining(s`
                Duplicate slot 'player'.
            `)
        );

        expect(
            checkDocumentValid(document) || document?.diagnostics?.map(diagnosticToString)?.join('\n')
        ).toEqual(
            expect.stringContaining(s`
                Chance must be between 0 and 100.
            `)
        );
    });

    test('check villager actions accept known slots', async () => {
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
                    when idle and at(home)
                    choose {
                        wander(home);
                        wait;
                    }
                }
            }
        `);

        expect(
            checkDocumentValid(document) || document?.diagnostics?.map(diagnosticToString)?.join('\n')
        ).toHaveLength(0);
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

function diagnosticToString(d: Diagnostic) {
    return `[${d.range.start.line}:${d.range.start.character}..${d.range.end.line}:${d.range.end.character}]: ${d.message}`;
}
