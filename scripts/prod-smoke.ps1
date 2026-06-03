param(
    [string]$ApiUrl = "http://localhost:5089",
    [string]$Login = "admin",
    [switch]$RequireProductionReadiness
)

$ErrorActionPreference = "Stop"

function Invoke-Json {
    param(
        [Parameter(Mandatory = $true)][string]$Uri,
        [string]$Method = "GET",
        [hashtable]$Headers = @{},
        [object]$Body = $null
    )

    $parameters = @{
        Uri = $Uri
        Method = $Method
        Headers = $Headers
        TimeoutSec = 30
    }

    if ($null -ne $Body) {
        $parameters.ContentType = "application/json"
        $parameters.Body = ($Body | ConvertTo-Json -Compress -Depth 10)
    }

    Invoke-RestMethod @parameters
}

function As-Array {
    param([object]$Value)

    if ($null -eq $Value) {
        return @()
    }

    if ($Value -is [System.Array]) {
        return $Value
    }

    return @($Value)
}

function Read-DotEnvSecret {
    param([Parameter(Mandatory = $true)][string[]]$Keys)

    foreach ($key in $Keys) {
        $value = [Environment]::GetEnvironmentVariable($key)
        if (-not [string]::IsNullOrWhiteSpace($value)) {
            return $value
        }
    }

    $envPath = Join-Path (Get-Location) ".env"
    if (-not (Test-Path $envPath)) {
        return $null
    }

    foreach ($line in Get-Content -Path $envPath) {
        foreach ($key in $Keys) {
            if ($line -match "^$([regex]::Escape($key))=") {
                return $line.Substring($line.IndexOf("=") + 1).Trim().Trim('"')
            }
        }
    }

    return $null
}

$adminPassword = Read-DotEnvSecret @("SYSASSIST_SMOKE_ADMIN_PASSWORD", "SysAssist__BootstrapAdminPassword", "SYSASSIST_BOOTSTRAP_ADMIN_PASSWORD")
if ([string]::IsNullOrWhiteSpace($adminPassword)) {
    throw "Set SYSASSIST_SMOKE_ADMIN_PASSWORD or SysAssist__BootstrapAdminPassword before running the smoke check."
}

$ready = Invoke-Json "$ApiUrl/health/ready"
if ($ready.status -ne "Ready") {
    throw "Readiness failed: $($ready.status)"
}

$loginResponse = Invoke-Json "$ApiUrl/api/auth/login" "POST" @{} @{
    login = $Login
    password = $adminPassword
}
$headers = @{ Authorization = "Bearer $($loginResponse.accessToken)" }

$me = Invoke-Json "$ApiUrl/api/auth/me" "GET" $headers
$dashboard = Invoke-Json "$ApiUrl/api/dashboard" "GET" $headers
$modules = As-Array (Invoke-Json "$ApiUrl/api/modules" "GET" $headers)
$diagnosticsRun = Invoke-Json "$ApiUrl/api/diagnostics/run" "POST" $headers
$diagnostics = Invoke-Json "$ApiUrl/api/diagnostics" "GET" $headers
$productionReadiness = Invoke-Json "$ApiUrl/api/production-readiness" "GET" $headers
$logs = As-Array (Invoke-Json "$ApiUrl/api/logs" "GET" $headers)
$supportBundle = Invoke-Json "$ApiUrl/api/support-bundle" "GET" $headers

$enabledModules = @($modules | Where-Object { $_.isEnabled })
$unhealthyEnabled = @($enabledModules | Where-Object { $_.healthStatus -ne "Healthy" })
$failedRequiredGates = @(As-Array $productionReadiness.gates | Where-Object { $_.required -and -not $_.passed })

if ($dashboard.enabledModulesCount -ne $enabledModules.Count) {
    throw "Dashboard enabled module count does not match module registry."
}

if ($diagnostics.status -ne "Healthy") {
    throw "Diagnostics is not Healthy: $($diagnostics.status)"
}

if ($unhealthyEnabled.Count -gt 0) {
    throw "Enabled modules are not healthy: $($unhealthyEnabled.key -join ',')"
}

if ($RequireProductionReadiness -and $productionReadiness.status -ne "Ready") {
    throw "Production readiness blocked: $($failedRequiredGates.key -join ',')"
}

[pscustomobject]@{
    Ready = $ready.status
    User = $me.login
    EnabledModules = $enabledModules.Count
    DisabledModules = @($modules | Where-Object { -not $_.isEnabled }).Count
    Diagnostics = $diagnostics.status
    ProductionReadiness = $productionReadiness.status
    ProductionBlockers = $failedRequiredGates.Count
    DiagnosticsRun = $diagnosticsRun.message
    LogsReturned = $logs.Count
    SupportProduct = $supportBundle.product
} | ConvertTo-Json -Compress
