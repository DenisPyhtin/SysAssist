param(
    [string]$ApiUrl = "http://localhost:5089",
    [string]$WebUrl = "http://localhost:5173",
    [int]$PollSeconds = 25,
    [switch]$Once
)

$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent $PSScriptRoot
$RunLogDir = Join-Path $RepoRoot ".run-logs"
$StatePath = Join-Path $RunLogDir "telegram-bot-state.json"
$ApiOutLog = Join-Path $RunLogDir "api-telegram-launcher.out.log"
$ApiErrLog = Join-Path $RunLogDir "api-telegram-launcher.err.log"
$WebOutLog = Join-Path $RunLogDir "web-telegram-launcher.out.log"
$WebErrLog = Join-Path $RunLogDir "web-telegram-launcher.err.log"
$BotRuntimeLog = Join-Path $RunLogDir "telegram-control.runtime.log"
New-Item -ItemType Directory -Force -Path $RunLogDir | Out-Null

function Write-BotLog {
    param([string]$Message)

    $stamp = (Get-Date).ToUniversalTime().ToString("o")
    Add-Content -LiteralPath $BotRuntimeLog -Value "$stamp $Message" -Encoding UTF8
}

function Read-DotEnv {
    $values = @{}
    foreach ($fileName in @(".env", ".env.local")) {
        $path = Join-Path $RepoRoot $fileName
        if (-not (Test-Path -LiteralPath $path)) {
            continue
        }

        foreach ($line in Get-Content -LiteralPath $path) {
            $trimmed = $line.Trim()
            if ($trimmed.Length -eq 0 -or $trimmed.StartsWith("#") -or -not $trimmed.Contains("=")) {
                continue
            }

            $parts = $trimmed.Split("=", 2)
            if (-not $values.ContainsKey($parts[0])) {
                $values[$parts[0]] = $parts[1].Trim().Trim('"').Trim("'")
            }
        }
    }

    return $values
}

function DotEnvValue {
    param([hashtable]$EnvValues, [string[]]$Keys)

    foreach ($key in $Keys) {
        $environmentValue = [Environment]::GetEnvironmentVariable($key)
        if (-not [string]::IsNullOrWhiteSpace($environmentValue)) {
            return $environmentValue
        }

        if ($EnvValues.ContainsKey($key) -and -not [string]::IsNullOrWhiteSpace($EnvValues[$key])) {
            return $EnvValues[$key]
        }
    }

    return $null
}

function Update-DotEnvValue {
    param([string]$Key, [string]$Value)

    $path = Join-Path $RepoRoot ".env"
    $lines = if (Test-Path -LiteralPath $path) { @(Get-Content -LiteralPath $path) } else { @() }
    $updated = $false
    $newLines = foreach ($line in $lines) {
        if ($line -match "^$([regex]::Escape($Key))=") {
            $updated = $true
            "$Key=$Value"
        }
        else {
            $line
        }
    }

    if (-not $updated) {
        $newLines += "$Key=$Value"
    }

    Set-Content -LiteralPath $path -Value $newLines -Encoding UTF8
}

function Load-State {
    if (Test-Path -LiteralPath $StatePath) {
        try {
            return Get-Content -LiteralPath $StatePath -Raw | ConvertFrom-Json
        }
        catch {
        }
    }

    return [pscustomobject]@{
        offset = 0
        allowedChatIds = @()
        firstRegisteredAt = $null
    }
}

function Save-State {
    param([object]$State)
    $State | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $StatePath -Encoding UTF8
}

function Telegram-Request {
    param(
        [string]$Method,
        [hashtable]$Body = $null,
        [int]$TimeoutSec = 35,
        [int]$Retries = 1
    )

    for ($attempt = 0; $attempt -le $Retries; $attempt++) {
        $configPath = $null
        $bodyPath = $null
        try {
            $json = if ($null -ne $Body) { $Body | ConvertTo-Json -Compress -Depth 10 } else { "{}" }
            $utf8NoBom = [System.Text.UTF8Encoding]::new($false)
            $configPath = [System.IO.Path]::GetTempFileName()
            $bodyPath = [System.IO.Path]::GetTempFileName()
            [System.IO.File]::WriteAllText($bodyPath, $json, $utf8NoBom)

            $bodyCurlPath = $bodyPath -replace "\\", "/"
            $url = "https://api.telegram.org/bot$Script:BotToken/$Method"
            $escapedUrl = $url.Replace("\", "\\").Replace('"', '\"')
            $escapedBodyPath = $bodyCurlPath.Replace("\", "\\").Replace('"', '\"')
            $config = @(
                "url = `"$escapedUrl`"",
                "request = `"POST`"",
                "header = `"Content-Type: application/json`"",
                "data-binary = `"@$escapedBodyPath`"",
                "max-time = `"$TimeoutSec`"",
                "silent",
                "show-error",
                "write-out = `"\n%{http_code}`""
            ) -join "`n"
            [System.IO.File]::WriteAllText($configPath, $config, $utf8NoBom)

            $curlOutput = & curl.exe --config $configPath 2>&1
            $exitCode = $LASTEXITCODE
            $raw = ([string]::Join("`n", @($curlOutput))).TrimEnd()
            if ($exitCode -ne 0) {
                throw "curl exit ${exitCode}: $raw"
            }

            if ($raw.Length -lt 3) {
                throw "curl returned an empty response."
            }

            $httpCode = $raw.Substring($raw.Length - 3)
            $responseBody = $raw.Substring(0, $raw.Length - 3).TrimEnd("`r", "`n")
            if (-not ($httpCode -match "^\d{3}$") -or [int]$httpCode -lt 200 -or [int]$httpCode -ge 300) {
                throw "HTTP ${httpCode}: $responseBody"
            }

            if ([string]::IsNullOrWhiteSpace($responseBody)) {
                return $null
            }

            return $responseBody | ConvertFrom-Json
        }
        catch {
            $attemptNumber = $attempt + 1
            $maxAttempts = $Retries + 1
            Write-BotLog "Telegram $Method failed ($attemptNumber/$maxAttempts): $($_.Exception.Message)"

            if ($attempt -ge $Retries) {
                throw
            }

            Start-Sleep -Milliseconds ([Math]::Min(5000, 750 * $attemptNumber))
        }
        finally {
            if ($null -ne $configPath -and (Test-Path -LiteralPath $configPath)) {
                Remove-Item -LiteralPath $configPath -Force
            }

            if ($null -ne $bodyPath -and (Test-Path -LiteralPath $bodyPath)) {
                Remove-Item -LiteralPath $bodyPath -Force
            }
        }
    }
}

function Send-TelegramMessage {
    param([string]$ChatId, [string]$Text)
    if ($Text.Length -gt 3900) {
        $Text = $Text.Substring(0, 3900) + "`n..."
    }

    try {
        Telegram-Request "sendMessage" @{ chat_id = $ChatId; text = $Text; disable_web_page_preview = $true } 25 2 | Out-Null
        return $true
    }
    catch {
        Write-BotLog "Telegram sendMessage to chat $ChatId failed permanently: $($_.Exception.Message)"
        return $false
    }
}

function Test-TcpPort {
    param([string]$HostName, [int]$Port, [int]$TimeoutMs = 800)
    $client = [System.Net.Sockets.TcpClient]::new()
    try {
        $connect = $client.BeginConnect($HostName, $Port, $null, $null)
        $ok = $connect.AsyncWaitHandle.WaitOne($TimeoutMs)
        if ($ok) {
            $client.EndConnect($connect)
        }

        return $ok -and $client.Connected
    }
    catch {
        return $false
    }
    finally {
        $client.Dispose()
    }
}

function Test-HttpOk {
    param([string]$Url, [int]$TimeoutSec = 3)
    try {
        Invoke-RestMethod -Uri $Url -TimeoutSec $TimeoutSec | Out-Null
        return $true
    }
    catch {
        return $false
    }
}

function Ensure-Backend {
    if (Test-HttpOk "$ApiUrl/health/ready" 4) {
        return "API is already ready at $ApiUrl."
    }

    Start-Process -FilePath "dotnet" `
        -ArgumentList @("run", "--project", "src\SysAssist.Api\SysAssist.Api.csproj", "--urls", $ApiUrl) `
        -WorkingDirectory $RepoRoot `
        -RedirectStandardOutput $ApiOutLog `
        -RedirectStandardError $ApiErrLog `
        -WindowStyle Hidden | Out-Null

    for ($i = 0; $i -lt 90; $i++) {
        Start-Sleep -Seconds 1
        if (Test-HttpOk "$ApiUrl/health/ready" 4) {
            return "API started and is ready at $ApiUrl."
        }
    }

    return "API start was requested, but readiness did not become Ready within 90 seconds. Check $ApiOutLog and $ApiErrLog."
}

function Ensure-Frontend {
    if (Test-TcpPort "127.0.0.1" 5173 800) {
        return "Web UI is already listening at $WebUrl."
    }

    $npm = "npm.cmd"
    Start-Process -FilePath $npm `
        -ArgumentList @("run", "dev", "--", "--host", "127.0.0.1", "--port", "5173") `
        -WorkingDirectory (Join-Path $RepoRoot "src\SysAssist.Web") `
        -RedirectStandardOutput $WebOutLog `
        -RedirectStandardError $WebErrLog `
        -WindowStyle Hidden | Out-Null

    for ($i = 0; $i -lt 45; $i++) {
        Start-Sleep -Seconds 1
        if (Test-TcpPort "127.0.0.1" 5173 800) {
            return "Web UI started at $WebUrl."
        }
    }

    return "Web UI start was requested, but port 5173 did not open within 45 seconds. Check $WebOutLog and $WebErrLog."
}

function Login-Api {
    $password = DotEnvValue $Script:EnvValues @("SYSASSIST_SMOKE_ADMIN_PASSWORD", "SysAssist__BootstrapAdminPassword", "SYSASSIST_BOOTSTRAP_ADMIN_PASSWORD")
    if ([string]::IsNullOrWhiteSpace($password)) {
        throw "Admin bootstrap password is not configured."
    }

    $login = Invoke-RestMethod "$ApiUrl/api/auth/login" -Method Post -ContentType "application/json" -Body (@{
        login = "admin"
        password = $password
    } | ConvertTo-Json -Compress)

    return @{ Authorization = "Bearer $($login.accessToken)" }
}

function Sync-TelegramModule {
    param([string]$ChatId)

    Update-DotEnvValue "SYSASSIST_MODULE_TELEGRAM_BOT_DEFAULTCHATID" $ChatId
    if (-not (Test-HttpOk "$ApiUrl/health/ready" 3)) {
        return "Chat registered locally. API is not ready, so Telegram module settings will sync after SysAssist starts."
    }

    try {
        $headers = Login-Api
        $modules = Invoke-RestMethod "$ApiUrl/api/modules" -Headers $headers
        $module = @($modules | Where-Object { $_.key -eq "telegram-bot" })[0]
        if ($null -eq $module) {
            return "Chat registered, but telegram-bot module was not found."
        }

        Invoke-RestMethod "$ApiUrl/api/modules/$($module.id)/settings/BotToken/secret" -Method Put -Headers $headers -ContentType "application/json" -Body (@{ value = $Script:BotToken } | ConvertTo-Json -Compress) | Out-Null
        Invoke-RestMethod "$ApiUrl/api/modules/$($module.id)/settings" -Method Put -Headers $headers -ContentType "application/json" -Body (@{
            settings = @(
                @{ key = "DefaultChatId"; value = $ChatId },
                @{ key = "Enabled"; value = "true" },
                @{ key = "UseFallbackMode"; value = "false" },
                @{ key = "SafeMode"; value = "true" }
            )
        } | ConvertTo-Json -Compress -Depth 5) | Out-Null
        Invoke-RestMethod "$ApiUrl/api/modules/$($module.id)/enable" -Method Post -Headers $headers | Out-Null
        return "Chat registered and telegram-bot module was enabled."
    }
    catch {
        return "Chat registered, but API sync failed: $($_.Exception.Message)"
    }
}

function Get-ConfiguredChatIds {
    $raw = @(
        DotEnvValue $Script:EnvValues @("TELEGRAM_BOT_ALLOWED_CHAT_IDS", "TelegramBot__AllowedChatIds"),
        DotEnvValue $Script:EnvValues @("SYSASSIST_MODULE_TELEGRAM_BOT_DEFAULTCHATID")
    ) -join ","

    return @($raw -split "[,; ]+" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
}

function Ensure-Authorized {
    param([string]$ChatId, [object]$State)

    $allowed = @(Get-ConfiguredChatIds) + @($State.allowedChatIds)
    if ($allowed -contains $ChatId) {
        return $true
    }

    $autoRegister = (DotEnvValue $Script:EnvValues @("TELEGRAM_BOT_AUTO_REGISTER_FIRST_CHAT", "TelegramBot__AutoRegisterFirstChat")) -eq "true"
    if ($autoRegister -and $allowed.Count -eq 0) {
        $State.allowedChatIds = @($ChatId)
        $State.firstRegisteredAt = (Get-Date).ToUniversalTime().ToString("o")
        Save-State $State
        $sync = Sync-TelegramModule $ChatId
        Send-TelegramMessage $ChatId "This chat is now registered for local SysAssist control.`n$sync"
        return $true
    }

    Send-TelegramMessage $ChatId "Unauthorized chat. Add this chat id to TELEGRAM_BOT_ALLOWED_CHAT_IDS or clear the local bot state for first-chat registration.`nChat id: $ChatId"
    return $false
}

function Get-StatusText {
    $apiReady = Test-HttpOk "$ApiUrl/health/ready" 4
    $webReady = Test-TcpPort "127.0.0.1" 5173 800
    $lines = @(
        "SysAssist status",
        "API: " + ($(if ($apiReady) { "Ready" } else { "Down" })),
        "Web: " + ($(if ($webReady) { "Listening" } else { "Down" })),
        "API URL: $ApiUrl",
        "Web URL: $WebUrl"
    )

    if ($apiReady) {
        try {
            $ready = Invoke-RestMethod "$ApiUrl/health/ready" -TimeoutSec 5
            $lines += "Database: $($ready.components.database.status)"
            $lines += "License: $($ready.components.license.status) / $($ready.components.license.edition)"
        }
        catch {
        }
    }

    return ($lines -join "`n")
}

function Invoke-DiagnosticsCommand {
    Ensure-Backend | Out-Null
    $headers = Login-Api
    $run = Invoke-RestMethod "$ApiUrl/api/diagnostics/run" -Method Post -Headers $headers
    $diagnostics = Invoke-RestMethod "$ApiUrl/api/diagnostics" -Headers $headers
    $warnings = @($diagnostics.components | Where-Object { $_ -like "diagnosticWarning=*" } | Select-Object -First 8)
    $text = @(
        "Diagnostics: $($diagnostics.status)",
        $run.message
    )
    if ($warnings.Count -gt 0) {
        $text += "Warnings:"
        $text += @($warnings | ForEach-Object { "- " + $_.Substring("diagnosticWarning=".Length) })
    }

    return ($text -join "`n")
}

function Get-ModulesText {
    Ensure-Backend | Out-Null
    $headers = Login-Api
    $modules = Invoke-RestMethod "$ApiUrl/api/modules" -Headers $headers
    $lines = @("Modules:")
    $lines += @($modules | Sort-Object key | ForEach-Object {
        "- $($_.key): $($_.healthStatus), enabled=$($_.isEnabled)"
    })
    return ($lines -join "`n")
}

function Get-ActionsText {
    Ensure-Backend | Out-Null
    $headers = Login-Api
    $actions = Invoke-RestMethod "$ApiUrl/api/actions" -Headers $headers
    $enabled = @($actions | Where-Object { $_.isEnabled })
    $approval = @($enabled | Where-Object { $_.requiresApproval })
    return "Actions: $($enabled.Count) enabled / $($approval.Count) require approval."
}

function Handle-Command {
    param([string]$ChatId, [string]$Text)

    $command = ($Text.Trim() -split "\s+")[0].ToLowerInvariant()
    $command = $command -replace "@.+$", ""
    switch ($command) {
        "/start" {
            $sync = Sync-TelegramModule $ChatId
            return "SysAssist Telegram control is ready.`n$sync`n`nCommands:`n/up - start API and Web`n/status - readiness`n/diagnostics - run diagnostics`n/modules - module health`n/actions - action count`n/whoami - show chat id"
        }
        "/help" {
            return "Commands:`n/up, /start_sysassist - start API and Web`n/status - readiness`n/diagnostics - run diagnostics`n/modules - module health`n/actions - action count`n/whoami - show chat id"
        }
        "/up" { return (Ensure-Backend) + "`n" + (Ensure-Frontend) + "`n" + (Sync-TelegramModule $ChatId) }
        "/start_sysassist" { return (Ensure-Backend) + "`n" + (Ensure-Frontend) + "`n" + (Sync-TelegramModule $ChatId) }
        "/status" { return Get-StatusText }
        "/diagnostics" { return Invoke-DiagnosticsCommand }
        "/modules" { return Get-ModulesText }
        "/actions" { return Get-ActionsText }
        "/whoami" { return "Chat id: $ChatId" }
        default { return "Unknown command. Send /help." }
    }
}

$Script:EnvValues = Read-DotEnv
$Script:BotToken = DotEnvValue $Script:EnvValues @("TELEGRAM_BOT_TOKEN", "TelegramBot__BotToken", "SYSASSIST_MODULE_TELEGRAM_BOT_BOTTOKEN")
if ([string]::IsNullOrWhiteSpace($Script:BotToken)) {
    throw "Telegram bot token is not configured. Set SYSASSIST_MODULE_TELEGRAM_BOT_BOTTOKEN or TELEGRAM_BOT_TOKEN."
}

$state = Load-State
Write-BotLog "Telegram launcher bot starting. api=$ApiUrl web=$WebUrl once=$Once pollSeconds=$PollSeconds"
try {
    Telegram-Request "deleteWebhook" @{ drop_pending_updates = $false } 25 2 | Out-Null
}
catch {
    Write-BotLog "Telegram deleteWebhook failed, polling will still continue: $($_.Exception.Message)"
}

do {
    try {
        $updates = Telegram-Request "getUpdates" @{
            offset = [int64]$state.offset
            timeout = $PollSeconds
            allowed_updates = @("message")
        } ($PollSeconds + 20) 2
    }
    catch {
        Write-BotLog "Telegram getUpdates failed permanently; polling will retry: $($_.Exception.Message)"
        Start-Sleep -Seconds ([Math]::Min(15, [Math]::Max(2, [int]($PollSeconds / 2))))
        continue
    }

    $resultCount = @($updates.result).Count
    if ($resultCount -gt 0) {
        Write-BotLog "Telegram getUpdates returned $resultCount update(s)."
    }

    foreach ($update in @($updates.result)) {
        $state.offset = [int64]$update.update_id + 1
        Save-State $state
        $message = $update.message
        if ($null -eq $message -or [string]::IsNullOrWhiteSpace($message.text)) {
            continue
        }

        $chatId = [string]$message.chat.id
        if (-not (Ensure-Authorized $chatId $state)) {
            Write-BotLog "Rejected unauthorized message from chat $chatId."
            continue
        }

        try {
            $commandName = (($message.text.Trim() -split "\s+")[0].ToLowerInvariant()) -replace "@.+$", ""
            Write-BotLog "Handling command $commandName from chat $chatId."
            $response = Handle-Command $chatId $message.text
            $sent = Send-TelegramMessage $chatId $response
            Write-BotLog "Command $commandName completed. responseSent=$sent"
        }
        catch {
            Write-BotLog "Command handling failed for chat ${chatId}: $($_.Exception.Message)"
            Send-TelegramMessage $chatId "Command failed: $($_.Exception.Message)"
        }
    }

    Save-State $state
} while (-not $Once)

Write-BotLog "Telegram launcher bot stopped. once=$Once"
