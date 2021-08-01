import * as vscode from 'vscode';
import { access, mkdir, } from 'fs/promises';
import { createWriteStream } from 'fs';
import axios from 'axios';


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


export function downloadFile(url: string, name: string) {
    return axios.request({
        url,
        headers: { accept: 'application/octet-stream' },
        responseType: 'stream'
    }).then(res => {
        const stream = createWriteStream(name);
        return new Promise((resolve, reject) => {
            res.data.pipe(stream);
            stream.on('error', (err) => {
                stream.close();
                reject(err);
            });
            stream.on('finish', resolve);
        });
    }).catch(console.error);
}