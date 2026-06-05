#!/usr/bin/env bash
# Register the Telegram bot webhook URL.
#
# Usage:
#   TELEGRAM_BOT_TOKEN="123:abc" \
#   TELEGRAM_WEBHOOK_URL="https://example.com/api/telegram/webhook" \
#   TELEGRAM_WEBHOOK_SECRET="my_secret" \
#   ./set-webhook.sh

set -euo pipefail

BOT_TOKEN="${TELEGRAM_BOT_TOKEN:-}"
WEBHOOK_URL="${TELEGRAM_WEBHOOK_URL:-}"
WEBHOOK_SECRET="${TELEGRAM_WEBHOOK_SECRET:-}"

if [[ -z "$BOT_TOKEN" ]]; then
  echo "Error: TELEGRAM_BOT_TOKEN is required." >&2
  exit 1
fi

if [[ -z "$WEBHOOK_URL" ]]; then
  echo "Error: TELEGRAM_WEBHOOK_URL is required." >&2
  exit 1
fi

echo "Setting Telegram webhook..."
echo "  URL: $WEBHOOK_URL"

if [[ -n "$WEBHOOK_SECRET" ]]; then
  echo "  Secret token: [set]"
  PAYLOAD=$(printf '{"url":"%s","secret_token":"%s"}' "$WEBHOOK_URL" "$WEBHOOK_SECRET")
else
  echo "  Secret token: [not set — endpoint is publicly accessible]"
  PAYLOAD=$(printf '{"url":"%s"}' "$WEBHOOK_URL")
fi

RESPONSE=$(curl -s -X POST \
  "https://api.telegram.org/bot${BOT_TOKEN}/setWebhook" \
  -H "Content-Type: application/json" \
  -d "$PAYLOAD")

OK=$(echo "$RESPONSE" | grep -o '"ok":[^,}]*' | cut -d: -f2 | tr -d ' ')

if [[ "$OK" == "true" ]]; then
  echo "Webhook registered successfully."
  echo "  Response: $RESPONSE"
else
  echo "Error: Telegram API did not return ok=true" >&2
  echo "  Response: $RESPONSE" >&2
  exit 1
fi
