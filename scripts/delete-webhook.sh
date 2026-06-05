#!/usr/bin/env bash
# Remove the registered Telegram bot webhook.
#
# Usage:
#   TELEGRAM_BOT_TOKEN="123:abc" ./delete-webhook.sh
#   TELEGRAM_BOT_TOKEN="123:abc" DROP_PENDING=true ./delete-webhook.sh

set -euo pipefail

BOT_TOKEN="${TELEGRAM_BOT_TOKEN:-}"
DROP_PENDING="${DROP_PENDING:-false}"

if [[ -z "$BOT_TOKEN" ]]; then
  echo "Error: TELEGRAM_BOT_TOKEN is required." >&2
  exit 1
fi

echo "Deleting Telegram webhook (drop_pending_updates=${DROP_PENDING})..."

PAYLOAD=$(printf '{"drop_pending_updates":%s}' "$DROP_PENDING")

RESPONSE=$(curl -s -X POST \
  "https://api.telegram.org/bot${BOT_TOKEN}/deleteWebhook" \
  -H "Content-Type: application/json" \
  -d "$PAYLOAD")

OK=$(echo "$RESPONSE" | grep -o '"ok":[^,}]*' | cut -d: -f2 | tr -d ' ')

if [[ "$OK" == "true" ]]; then
  echo "Webhook deleted successfully."
  echo "  Response: $RESPONSE"
else
  echo "Error: Telegram API did not return ok=true" >&2
  echo "  Response: $RESPONSE" >&2
  exit 1
fi
