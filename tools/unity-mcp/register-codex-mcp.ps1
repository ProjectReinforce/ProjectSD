param(
    [string]$ServerName = "unity",
    [string]$UnityBridgeUrl = ""
)

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$serverScript = Join-Path $scriptDir "server.js"
$projectRoot = (Resolve-Path (Join-Path $scriptDir "..\..")).Path
$portConfigPath = Join-Path $projectRoot "ProjectSettings\UnityMcpPort.txt"
$defaultBridgeUrl = "http://127.0.0.1:51234"

if (-not (Test-Path $serverScript)) {
    Write-Error "MCP server script not found: $serverScript"
    exit 1
}

if ([string]::IsNullOrWhiteSpace($UnityBridgeUrl)) {
    $UnityBridgeUrl = $defaultBridgeUrl

    if (Test-Path $portConfigPath) {
        $configuredPort = (Get-Content $portConfigPath -Raw).Trim()
        if ($configuredPort -match '^\d+$') {
            $UnityBridgeUrl = "http://127.0.0.1:$configuredPort"
        }
        elseif (-not [string]::IsNullOrWhiteSpace($configuredPort)) {
            Write-Warning "Invalid port in ${portConfigPath}: '$configuredPort'. Falling back to $defaultBridgeUrl"
        }
    }
}

$resolvedServerScript = (Resolve-Path $serverScript).Path

& codex mcp remove $ServerName *> $null

Write-Host "Registering MCP server '$ServerName'..."
Write-Host "Using Unity bridge URL: $UnityBridgeUrl"
& codex mcp add $ServerName --env "UNITY_MCP_BASE_URL=$UnityBridgeUrl" -- node $resolvedServerScript
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Write-Host ""
Write-Host "Current MCP server config:"
& codex mcp get $ServerName
