#Requires -Version 5.1
<#
.SYNOPSIS
    Register the Telegram bot webhook URL.

.DESCRIPTION
    Calls the Telegram Bot API setWebhook method.
    Reads credentials from environment variables or prompts for them.

.EXAMPLE
    $env:TELEGRAM_BOT_TOKEN = "123:abc"
    $env:TELEGRAM_WEBHOOK_URL = "https://example.com/api/telegram/webhook"
    $env:TELEGRAM_WEBHOOK_SECRET = "my_secret"
    .\set-webhook.ps1
#>

param(
    [string]$BotToken      = $env:TELEGRAM_BOT_TOKEN,
    [string]$WebhookUrl    = $env:TELEGRAM_WEBHOOK_URL,
    [string]$WebhookSecret = $env:TELEGRAM_WEBHOOK_SECRET
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($BotToken)) {
    Write-Error "TELEGRAM_BOT_TOKEN is required. Set the env var or pass -BotToken."
    exit 1
}
if ([string]::IsNullOrWhiteSpace($WebhookUrl)) {
    Write-Error "TELEGRAM_WEBHOOK_URL is required. Set the env var or pass -WebhookUrl."
    exit 1
}

Write-Host "Setting Telegram webhook..."
Write-Host "  URL: $WebhookUrl"

$body = @{ url = $WebhookUrl }
if (-not [string]::IsNullOrWhiteSpace($WebhookSecret)) {
    $body["secret_token"] = $WebhookSecret
    Write-Host "  Secret token: [set]"
} else {
    Write-Host "  Secret token: [not set — endpoint is publicly accessible]"
}

$apiUrl = "https://api.telegram.org/bot$BotToken/setWebhook"

try {
    $response = Invoke-RestMethod -Uri $apiUrl -Method Post -Body ($body | ConvertTo-Json) `
        -ContentType "application/json"

    if ($response.ok) {
        Write-Host "Webhook registered successfully." -ForegroundColor Green
        Write-Host "  Result: $($response.description)"
    } else {
        Write-Error "Telegram API returned ok=false: $($response | ConvertTo-Json)"
        exit 1
    }
} catch {
    Write-Error "Request failed: $_"
    exit 1
}
