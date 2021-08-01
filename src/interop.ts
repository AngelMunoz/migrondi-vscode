import axios from 'axios';

import { access, mkdir, } from 'fs/promises';
import { Extract } from 'unzipper';

import * as vscode from 'vscode';

export async function getOrCreatePath(context: vscode.ExtensionContext) {
    const path = context.globalStorageUri.fsPath;
    try {
        console.debug(`Migrondi: checking "${path}" exists`);
        await access(path);
    } catch (error) {
        console.debug(`Migrondi: "${path}" does not exist, creating`);
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