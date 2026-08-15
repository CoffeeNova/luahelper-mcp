# Changelog

All notable changes to the LuaHelper MCP Server VS Code extension are documented here.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Marketplace readiness: real publisher id, gallery banner, extension icon, keywords, this changelog.
- `publish` npm script and a Marketplace publish job in the release workflow (requires a `VSCE_PAT` secret).

### Fixed

- `build-vsix.ps1` now cleans the previous server publish output before packaging, so stale self-contained `.dll` files no longer bloat the `.vsix`.
- Removed the obsolete `--allow-missing-repository` flag (the `repository` field is now set).

## [0.1.2] - 2026-08-14

### Fixed

- Integration suite: resolve the newest server binary deterministically, use machine-independent golden files, and order `check_lua_project` results deterministically.

## [0.1.1] - 2026-08-14

### Added

- `examples/mcp-test/` — a runnable example Lua project plus a QA script that exercises every MCP tool.
- `check_lua_project` diagnostics now expose per-file results with stable ordering.

### Changed

- `get_luahelper_config` and `create_luahelper_json` improvements (merged project defaults, richer template).
- Extension `README.md` expanded with full tool and configuration documentation.

### Fixed

- `ConfigService` and `LuaDiagnosticTools` bugfixes (config resolution edge cases).

## [0.1.0] - 2026-08-13

### Added

- First release.
- Model Context Protocol server wrapping LuaHelper (`lualsp.exe`): `check_lua_file`, `check_lua_project`, `get_supported_checks`, `get_luahelper_version`, `get_server_version`, `get_luahelper_config`, `create_luahelper_json`.
- MCP resources (`luahelper://diagnostics/{+filePath}`, `luahelper://config`) and prompts (`fix_lua_warnings`, `configure_luahelper`).
- VS Code extension bundling the server as a NativeAOT single-file binary with the `lualsp` binary included — no .NET runtime or extra setup required.
- Per-project `luahelper.json` configuration.
- CI (`ci.yml`) and release (`release.yml`) GitHub Actions workflows; semver continuous delivery (patch bump on every push to `main`, tag pushes pin major/minor).
