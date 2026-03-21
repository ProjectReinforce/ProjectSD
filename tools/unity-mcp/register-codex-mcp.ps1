param(
    [string]$ServerName = "unity",
    [string]$UnityBridgeUrl = ""
)

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$serverScript = Join-Path $scriptDir "server.js"
$projectRoot = (Resolve-Path (Join-Path $scriptDir "..\..")).Path
$portConfigPath = Join-Path $projectRoot "ProjectSettings\UnityMcpPort.txt"
$defaultBridgeUrl = "http://127.0.0.1:51234"
$currentProjectBridgeUrl = $defaultBridgeUrl

if (-not (Test-Path $serverScript)) {
    Write-Error "MCP server script not found: $serverScript"
    exit 1
}

if (Test-Path $portConfigPath) {
    $configuredPort = (Get-Content $portConfigPath -Raw).Trim()
    if ($configuredPort -match '^\d+$') {
        $currentProjectBridgeUrl = "http://127.0.0.1:$configuredPort"
    }
    elseif (-not [string]::IsNullOrWhiteSpace($configuredPort)) {
        Write-Warning "Invalid port in ${portConfigPath}: '$configuredPort'. Falling back to $defaultBridgeUrl"
    }
}

$resolvedServerScript = (Resolve-Path $serverScript).Path

& codex mcp remove $ServerName *> $null

Write-Host "Registering MCP server '$ServerName'..."
Write-Host "Current project bridge URL: $currentProjectBridgeUrl"

if ([string]::IsNullOrWhiteSpace($UnityBridgeUrl)) {
    Write-Host "Registering without explicit UNITY_MCP_BASE_URL override so the server follows ProjectSettings\\UnityMcpPort.txt at runtime."
    & codex mcp add $ServerName -- node $resolvedServerScript
}
else {
    Write-Host "Using explicit Unity bridge URL override: $UnityBridgeUrl"
    & codex mcp add $ServerName --env "UNITY_MCP_BASE_URL=$UnityBridgeUrl" -- node $resolvedServerScript
}

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Write-Host ""
Write-Host "Current MCP server config:"
& codex mcp get $ServerName
