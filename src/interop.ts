import axios from 'axios';
import { spawn } from 'child_process';

import { access, mkdir, } from 'fs/promises';
import { Extract } from 'unzipper';

import { ExtensionContext, OutputChannel, WorkspaceFolder } from 'vscode';
import * as vscode from 'vscode';

export async function getOrCreatePath(context: ExtensionContext, channel: OutputChannel) {
    const path = context.globalStorageUri.fsPath;
    try {
        channel.appendLine(`Migrondi: checking "${path}" exists`);
        await access(path);
    } catch (error) {
        channel.appendLine(`Migrondi: "${path}" does not exist, creating`);
        await mkdir(path, { recursive: true });
    }
    return path;
}


export async function downloadAndExtract(url: string, extractTo: string) {
    console.time(`Migrondi: downloading ${url}`);
    const res = await axios.get(url, {
        headers: { accept: 'application/octet-stream' },
        responseType: 'stream'
    });
    return new Promise((resolve, reject) => {
        res.data.pipe(Extract({ path: extractTo }))
            .on('data', () => {
                console.clear();
                process.stdout.write("Migrondi in progress...");
            })
            .on('error', reject)
            .on('close', () => {
                console.timeEnd(`Migrondi: downloading ${url}`);
                resolve(undefined);
            });
    });
}

export function getMigrondiExecFn(getExecPath: (context: ExtensionContext) => string, getCWD: (workspaceFolders: readonly WorkspaceFolder[] | undefined) => string) {
    return async (args: [context: ExtensionContext, cmdArgs: readonly string[]]) => {
        const [context, cmdArgs] = args;
        const binFilePath = getExecPath(context);
        const cwd = getCWD(vscode.workspace.workspaceFolders);
        const process = spawn(`${binFilePath}`, cmdArgs, { cwd });
        const stdout = [];
        const stderr = [];
        if (process.stdout) {
            for await (const chunk of process.stdout) {
                const chunkLines = chunk?.toString?.().trim?.().replace?.(/(?:\r\n|\r|\n)/g, ",");
                stdout.push(chunkLines);
            }
        }
        if (process.stderr) {
            for await (const chunk of process.stderr) {
                const chunkLines = chunk?.toString?.();
                stderr.push(chunkLines);
            }
        }
        const stdoutResult = `[${stdout.filter(x => !!(x.trim())).join(",")}]`;
        const stderrResult = stderr.join("");

        if (stderr.length > 0) {
            return Promise.reject(stderrResult);
        }
        try {
            return JSON.parse(stdoutResult);
        } catch (error) {
            return Promise.reject(error.message);
        }
    };
}
