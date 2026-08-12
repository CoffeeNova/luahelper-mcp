# Prompt: Architect — LuaHelper MCP Server

> Используй этого агента: `senior-software-architect`
> Контекст: `.github\docs\research-luahelper-mcp-server.md`

## Задача

Создать архитектуру и план разработки MCP-сервера, который оборачивает `lualsp.exe` (LuaHelper — Go LSP-сервер для Lua) и предоставляет AI-ассистентам (GitHub Copilot, Claude Desktop и др.) интерфейс для получения диагностик Lua-кода (ворнинги, ошибки).

## Чёткое описание продукта

**LuaHelper MCP Server** — это MCP-сервер на .NET (C#), который:

1. Запускает `lualsp.exe` как дочерний процесс в LSP-режиме (`-mode=1`)
2. Общается с ним по LSP-протоколу (JSON-RPC 2.0 через stdin/stdout)
3. Предоставляет AI-ассистенту MCP tools/resources для:
   - Проверки одного `.lua` файла
   - Проверки всего проекта
   - Получения списка доступных проверок
   - Управления конфигурацией (`luahelper.json`)
4. Публикуется как VS Code Extension (опционально) и/или standalone MCP-сервер

## Ключевые факты из исследования

### lualsp.exe
- Go-бинарник, ~10 MB, единственный файл, без зависимостей
- Режимы: `0` (cmd — не работает, вывод пустой), `1` (LSP — работает), `2` (socket)
- LSP-протокол: JSON-RPC 2.0 с `Content-Length` заголовками — **тот же формат, что и MCP**
- Инициализация требует ~30 параметров (флаги проверок, игнорируемые файлы, PluginPath)
- Диагностики приходят через `textDocument/publishDiagnostics`
- **Stateful** — держит проект в памяти, нужно открывать файлы через `textDocument/didOpen`

### MCP SDK (C#)
- Пакет: `ModelContextProtocol` v2.1.0 (NuGet), Microsoft-maintained
- Атрибуты: `[McpServerToolType]`, `[McpServerTool]`, `[Description]`
- Транспорт: `WithStdioServerTransport()` — stdin/stdout
- DI: `Host.CreateEmptyApplicationBuilder()` + `AddMcpServer()`
- NativeAOT: `dotnet publish -r win-x64 -p:PublishAot=true` → single .exe

### VS Code Extension
- `contributes.mcpServers` — встроенная точка расширения (VS Code сам запускает MCP-сервер)
- Публикация: `vsce package` / `vsce publish`
- PATs retired с декабря 2026 — нужна Entra ID аутентификация

## Что нужно сделать архитектору

### 1. Архитектура системы

Нарисуй/опиши:

- **Компонентную диаграмму** MCP-сервера: какие классы/модули, их ответственность, зависимости
- **Диаграмму последовательности** для типичного сценария: AI-ассистент → MCP tool → LSP-клиент → lualsp.exe → диагностики → ответ
- **Data flow**: как диагностики путешествуют от lualsp.exe до AI-ассистента
- **State machine**: жизненный цикл lualsp.exe процесса (spawn → init → open → diagnostics → shutdown → crash → respawn)

### 2. План разработки (по фазам)

Разбей на фазы с чёткими критериями готовности (DoD):

| Фаза | Что делаем | Результат |
|---|---|---|
| **Phase 0: Proof of Concept** | .NET консольное приложение, которое запускает lualsp.exe, шлёт LSP-запросы, получает диагностики | Рабочий LSP-клиент на C# |
| **Phase 1: Core MCP Server** | Обёртка LSP-клиента в MCP-сервер с 1-2 tools | MCP-сервер, который можно подключить к VS Code Copilot |
| **Phase 2: Full Tool Set** | Все tools/resources/prompts из исследования | Полнофункциональный сервер |
| **Phase 3: Configuration** | Поддержка `luahelper.json`, кастомные флаги проверок | Гибкая настройка под проекты |
| **Phase 4: VS Code Extension** | Расширение-обёртка для Marketplace | Установка в 1 клик |
| **Phase 5: NativeAOT + Distribution** | AOT-компиляция, CI/CD, публикация | Релиз |

### 3. Детали реализации

Для каждого компонента укажи:

- **Класс/интерфейс**: имя, методы, свойства
- **Обработка ошибок**: что если lualsp.exe упал, что если LSP-сообщение пришло не в том порядке
- **Threading model**: асинхронность, cancellation, таймауты
- **Конфигурация**: что должно быть в `appsettings.json`, что в `luahelper.json`
- **Тестирование**: как тестировать LSP-клиент без реального lualsp.exe

### 4. Открытые вопросы (требуют решения)

1. **Бандлить lualsp.exe или качать?** Бандлинг = +10 MB на платформу, но работает offline. Скачивание = нужно сетевое соединение при первом запуске.

2. **Все 22 типа проверок или подмножество?** Для WoW-аддонов актуальны только type 18 (аннотации). Но универсальный инструмент должен поддерживать всё.

3. **VS Code Extension с первого дня или потом?** Быстрее начать как standalone MCP, расширение добавить позже.

4. **Режим `-mode=0` (cmd) — починить или забыть?** Если cmd-mode можно заставить работать, он проще LSP (не нужно управлять состоянием процесса).

5. **HTTP transport?** `ModelContextProtocol.AspNetCore` позволяет сделать Streamable HTTP — полезно для CI.

6. **PluginPath — что туда передавать?** В VS Code это путь к расширению. В standalone сервере — путь к папке с `lualsp.exe`.

### 5. Структура проекта

Предложи конкретную структуру папок и файлов, например:

```
LuaHelperMcpServer/
├── src/
│   ├── LuaHelperMcpServer/
│   │   ├── Program.cs
│   │   ├── Tools/
│   │   ├── Services/
│   │   └── Models/
│   └── LuaHelperMcpServer.Tests/
├── lualsp/
│   ├── win-x64/lualsp.exe
│   ├── linux-x64/lualsp
│   └── osx-x64/lualsp
├── vscode-extension/
│   ├── package.json
│   └── extension.js
├── LuaHelperMcpServer.sln
└── README.md
```

## Формат результата

Результат сохрани в файл: `.github\docs\arch-luahelper-mcp-server.md`

Используй Markdown с Mermaid-диаграммами для визуализации архитектуры.

## Ссылки

- Исследование: `.github\docs\research-luahelper-mcp-server.md`
- MCP C# SDK: https://github.com/modelcontextprotocol/csharp-sdk
- MCP C# SDK Docs: https://csharp.sdk.modelcontextprotocol.io/
- LuaHelper: https://github.com/Tencent/LuaHelper
- LuaHelper Config: https://github.com/Tencent/LuaHelper/blob/master/docs/manual/config.md
- VS Code Extension Publishing: https://code.visualstudio.com/api/working-with-extensions/publishing-extension
