# Unity MCP (ProjectSD)

This folder provides a minimal bridge between Unity Editor and Codex MCP tools.

## What it adds

- Unity editor bridge: `Assets/Editor/UnityMcp/UnityMcpBridge.cs`
  - Starts an HTTP endpoint at the port defined in `ProjectSettings/UnityMcpPort.txt`
  - Default endpoint: `http://127.0.0.1:51234/`
  - Endpoints:
    - `GET /health`
    - `GET /scene/current`
    - `POST /play/start`
    - `POST /play/stop`
    - `GET /console/errors?limit=20`
- MCP stdio server: `tools/unity-mcp/server.js`
- Codex registration script: `tools/unity-mcp/register-codex-mcp.ps1`

## Prerequisites

- Unity project is open in Unity Editor.
- Node.js installed (`node -v`).
- Codex CLI installed (`codex --version`).

## Quick start

1. Open this Unity project in Unity Editor.
2. In Unity, confirm the bridge is running:
   - Menu: `Tools > Unity MCP > Print Status`
   - Optional test in PowerShell:
     - `Invoke-RestMethod ("http://127.0.0.1:{0}/health" -f (Get-Content .\ProjectSettings\UnityMcpPort.txt))`
3. Register MCP server in Codex:
   - `powershell -ExecutionPolicy Bypass -File .\tools\unity-mcp\register-codex-mcp.ps1`
4. Verify registration:
   - `codex mcp list`
   - `codex mcp get unity`

## Available MCP tools

- `unity_health`
- `unity_scene_current`
- `unity_play_start`
- `unity_play_stop`
- `unity_console_errors`

## Notes

- The bridge is editor-only and auto-starts on script reload.
- Change `ProjectSettings/UnityMcpPort.txt` if port `51234` is already in use.
- `register-codex-mcp.ps1` now registers without pinning `UNITY_MCP_BASE_URL` by default, so `server.js` follows `ProjectSettings/UnityMcpPort.txt` automatically at runtime.
- If you pass `-UnityBridgeUrl`, that explicit URL override is used instead of the port file.
