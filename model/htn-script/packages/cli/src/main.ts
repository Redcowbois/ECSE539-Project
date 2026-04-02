import type { Model } from 'htn-script-language';
import { createHtnScriptServices, HtnScriptLanguageMetaData } from 'htn-script-language';
import chalk from 'chalk';
import { Command } from 'commander';
import { extractAstNode } from './util.js';
import { generateCSharp } from './generator.js';
import { NodeFileSystem } from 'langium/node';
import * as url from 'node:url';
import * as fs from 'node:fs/promises';
import * as path from 'node:path';
const __dirname = url.fileURLToPath(new URL('.', import.meta.url));

const packagePath = path.resolve(__dirname, '..', 'package.json');
const packageContent = await fs.readFile(packagePath, 'utf-8');

export const generateAction = async (fileName: string, opts: GenerateOptions): Promise<void> => {
    const services = createHtnScriptServices(NodeFileSystem).HtnScript;
    const model = await extractAstNode<Model>(fileName, services);
    const generatedFilePath = generateCSharp(model, fileName, opts.destination);
    console.log(chalk.green(`C# code generated successfully: ${generatedFilePath}`));
};

export type GenerateOptions = {
    destination?: string;
}

export default function(): void {
    const program = new Command();

    program.version(JSON.parse(packageContent).version);

    const fileExtensions = HtnScriptLanguageMetaData.fileExtensions.join(', ');
    program
        .command('generate')
        .argument('<file>', `source file (possible file extensions: ${fileExtensions})`)
        .option('-d, --destination <dir>', 'destination directory of generating')
        .description('generates a Unity C# AI agent from a .htns DSL file')
        .action(generateAction);

    program.parse(process.argv);
}
