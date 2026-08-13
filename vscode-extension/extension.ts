import * as vscode from 'vscode';
import * as path from 'path';

export function activate(context: vscode.ExtensionContext) {
    console.log('LuaHelper MCP Server extension activated');

    const serverExe = path.join(context.extensionPath, 'LuaHelperMcpServer.exe');

    context.subscriptions.push(vscode.lm.registerMcpServerDefinitionProvider('luahelper', {
        provideMcpServerDefinitions: async () => [
            new vscode.McpStdioServerDefinition('luahelper', serverExe, []),
        ],
    }));
}

export function deactivate() {
    console.log('LuaHelper MCP Server extension deactivated');
}
