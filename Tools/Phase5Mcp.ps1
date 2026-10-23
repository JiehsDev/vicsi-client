param([string]$Method, [string]$Params = '{}', [string]$ParamsPath)
$ErrorActionPreference = 'Stop'
if ($ParamsPath) { $Params = Get-Content -LiteralPath $ParamsPath -Raw }
$headers = @{Accept='application/json, text/event-stream'}
$url = 'http://127.0.0.1:8080/mcp'
function Send-Mcp($methodName, $parameters, $id) {
    $body = @{jsonrpc='2.0';method=$methodName;params=$parameters}
    if ($null -ne $id) { $body.id=$id }
    $response = Invoke-WebRequest -Uri $url -Method Post -ContentType 'application/json' -Headers $headers -Body ($body | ConvertTo-Json -Depth 30 -Compress) -TimeoutSec 45 -UseBasicParsing
    if ($response.Headers['Mcp-Session-Id']) { $headers['Mcp-Session-Id']=$response.Headers['Mcp-Session-Id'] }
    if ($response.Content.TrimStart().StartsWith('{')) {
        return ($response.Content | ConvertFrom-Json)
    }
    foreach ($line in ($response.Content -split "`n")) {
        if ($line.StartsWith('data: ')) {
            $value = $line.Substring(6) | ConvertFrom-Json
            if ($null -ne $value.error) { return $value }
            if ($value.id -eq $id -and $null -ne $id) { return $value }
        }
    }
}
$null = Send-Mcp 'initialize' @{protocolVersion='2024-11-05';capabilities=@{};clientInfo=@{name='phase5-local';version='1.0'}} 1
$headers['MCP-Protocol-Version']='2024-11-05'
$null = Send-Mcp 'notifications/initialized' @{} $null
Send-Mcp $Method ($Params | ConvertFrom-Json) 2 | ConvertTo-Json -Depth 40
