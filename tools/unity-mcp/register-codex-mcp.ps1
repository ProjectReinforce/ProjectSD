param(
    [string]$ServerName = "unity",
    [string]$UnityBridgeUrl = "http://127.0.0.1:51234"
)

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$serverScript = Join-Path $scriptDir "server.js"

if (-not (Test-Path $serverScript)) {
    Write-Error "MCP server script not found: $serverScript"
    exit 1
}

$resolvedServerScript = (Resolve-Path $serverScript).Path

& codex mcp remove $ServerName *> $null

Write-Host "Registering MCP server '$ServerName'..."
& codex mcp add $ServerName --env "UNITY_MCP_BASE_URL=$UnityBridgeUrl" -- node $resolvedServerScript
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Write-Host ""
Write-Host "Current MCP server config:"
& codex mcp get $ServerName
