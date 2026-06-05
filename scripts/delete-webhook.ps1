#Requires -Version 5.1
<#
.SYNOPSIS
    Remove the registered Telegram bot webhook.

.EXAMPLE
    $env:TELEGRAM_BOT_TOKEN = "123:abc"
    .\delete-webhook.ps1
#>

param(
    [string]$BotToken = $env:TELEGRAM_BOT_TOKEN,
    [switch]$DropPendingUpdates
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($BotToken)) {
    Write-Error "TELEGRAM_BOT_TOKEN is required. Set the env var or pass -BotToken."
    exit 1
}

Write-Host "Deleting Telegram webhook..."

$body = @{ drop_pending_updates = $DropPendingUpdates.IsPresent }
$apiUrl = "https://api.telegram.org/bot$BotToken/deleteWebhook"

try {
    $response = Invoke-RestMethod -Uri $apiUrl -Method Post -Body ($body | ConvertTo-Json) `
        -ContentType "application/json"

    if ($response.ok) {
        Write-Host "Webhook deleted successfully." -ForegroundColor Green
        Write-Host "  Result: $($response.description)"
    } else {
        Write-Error "Telegram API returned ok=false: $($response | ConvertTo-Json)"
        exit 1
    }
} catch {
    Write-Error "Request failed: $_"
    exit 1
}
