#!/usr/bin/env bash
# ==============================================================================
# CalmClass - Register Telegram Bot Webhook
# Usage: ./scripts/register-webhook.sh [BOT_TOKEN] [WEBHOOK_URL] [SECRET_TOKEN]
# If arguments are omitted, attempts to read from src/CalmClass.Functions/local.settings.json
# ==============================================================================

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
SETTINGS_FILE="$ROOT_DIR/src/CalmClass.Functions/local.settings.json"

BOT_TOKEN="${1:-}"
WEBHOOK_URL="${2:-}"
SECRET_TOKEN="${3:-}"

if [[ -z "$BOT_TOKEN" ]] && [[ -f "$SETTINGS_FILE" ]]; then
  BOT_TOKEN=$(grep -o '"Telegram__BotToken": *"[^"]*"' "$SETTINGS_FILE" | head -n 1 | cut -d'"' -f4 || true)
fi

if [[ -z "$SECRET_TOKEN" ]] && [[ -f "$SETTINGS_FILE" ]]; then
  SECRET_TOKEN=$(grep -o '"Telegram__SecretToken": *"[^"]*"' "$SETTINGS_FILE" | head -n 1 | cut -d'"' -f4 || true)
fi

if [[ -z "$BOT_TOKEN" || "$BOT_TOKEN" == *"YOUR_TELEGRAM_BOT_TOKEN"* ]]; then
  echo "Error: BOT_TOKEN is required. Provide as arg or configure in local.settings.json"
  echo "Usage: $0 <BOT_TOKEN> <WEBHOOK_URL> [SECRET_TOKEN]"
  exit 1
fi

if [[ -z "$WEBHOOK_URL" ]]; then
  read -r -p "Enter public webhook URL (e.g. https://xxxx.ngrok-free.app/api/telegram/webhook): " WEBHOOK_URL
fi

if [[ -z "$SECRET_TOKEN" ]]; then
  SECRET_TOKEN="calmclass_secret_$(date +%s)"
fi

echo "Registering webhook with Telegram API..."
echo "Webhook URL:  $WEBHOOK_URL"
echo "Secret Token: $SECRET_TOKEN"

PAYLOAD=$(cat <<EOF
{
  "url": "$WEBHOOK_URL",
  "secret_token": "$SECRET_TOKEN",
  "allowed_updates": ["message", "poll_answer"],
  "drop_pending_updates": false
}
EOF
)

RESPONSE=$(curl -s -X POST "https://api.telegram.org/bot${BOT_TOKEN}/setWebhook" \
  -H "Content-Type: application/json" \
  -d "$PAYLOAD")

echo "Response from Telegram:"
echo "$RESPONSE"

if echo "$RESPONSE" | grep -q '"ok":true'; then
  echo ""
  echo "✅ Webhook successfully registered!"
else
  echo ""
  echo "❌ Failed to register webhook. Please check your BOT_TOKEN and URL."
  exit 1
fi
