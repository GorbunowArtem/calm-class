#!/usr/bin/env bash
# ==============================================================================
# CalmClass - Register Telegram Bot Webhook
# Usage:
#   ./scripts/register-webhook.sh [options]
#   ./scripts/register-webhook.sh --url https://your-ngrok.ngrok-free.dev/api/telegram/webhook
#   ./scripts/register-webhook.sh --info
#   ./scripts/register-webhook.sh --delete
# ==============================================================================

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
SETTINGS_FILE="$ROOT_DIR/src/CalmClass.Functions/local.settings.json"

BOT_TOKEN=""
WEBHOOK_URL=""
SECRET_TOKEN=""
ACTION="register"

# Read defaults from local.settings.json
if [[ -f "$SETTINGS_FILE" ]]; then
  BOT_TOKEN=$(python3 -c "import json; print(json.load(open('$SETTINGS_FILE'))['Values'].get('Telegram__BotToken', ''))" 2>/dev/null || true)
  SECRET_TOKEN=$(python3 -c "import json; print(json.load(open('$SETTINGS_FILE'))['Values'].get('Telegram__SecretToken', ''))" 2>/dev/null || true)
fi

# Parse CLI arguments
while [[ $# -gt 0 ]]; do
  case "$1" in
    --token|-t)
      BOT_TOKEN="$2"
      shift 2
      ;;
    --url|-u)
      WEBHOOK_URL="$2"
      shift 2
      ;;
    --secret|-s)
      SECRET_TOKEN="$2"
      shift 2
      ;;
    --info|-i)
      ACTION="info"
      shift
      ;;
    --delete|-d)
      ACTION="delete"
      shift
      ;;
    --help|-h)
      echo "Usage: $0 [options]"
      echo ""
      echo "Options:"
      echo "  --url, -u <URL>      Public HTTPS webhook URL (auto-detected from ngrok if running)"
      echo "  --token, -t <TOKEN>  Telegram Bot Token (default: read from local.settings.json)"
      echo "  --secret, -s <TOKEN> Telegram Secret Token (default: read from local.settings.json)"
      echo "  --info, -i           Get current webhook status and errors"
      echo "  --delete, -d         Remove current webhook"
      exit 0
      ;;
    *)
      if [[ -z "$WEBHOOK_URL" ]]; then
        WEBHOOK_URL="$1"
      fi
      shift
      ;;
  esac
done

if [[ -z "$BOT_TOKEN" || "$BOT_TOKEN" == *"YOUR_TELEGRAM_BOT_TOKEN"* ]]; then
  echo "❌ Error: Telegram__BotToken is not set in $SETTINGS_FILE or provided via --token."
  exit 1
fi

CURL_CMD=(curl -k -s -S)

# Handle --info action
if [[ "$ACTION" == "info" ]]; then
  echo "🔍 Querying Telegram webhook status..."
  RESPONSE=$("${CURL_CMD[@]}" "https://api.telegram.org/bot${BOT_TOKEN}/getWebhookInfo")
  echo "$RESPONSE" | python3 -m json.tool 2>/dev/null || echo "$RESPONSE"
  exit 0
fi

# Handle --delete action
if [[ "$ACTION" == "delete" ]]; then
  echo "🗑️  Deleting Telegram webhook..."
  RESPONSE=$("${CURL_CMD[@]}" -X POST "https://api.telegram.org/bot${BOT_TOKEN}/deleteWebhook")
  echo "$RESPONSE" | python3 -m json.tool 2>/dev/null || echo "$RESPONSE"
  exit 0
fi

# Auto-detect public URL from local ngrok if not supplied
if [[ -z "$WEBHOOK_URL" ]]; then
  NGROK_URL=$(curl -s http://127.0.0.1:4040/api/tunnels 2>/dev/null | python3 -c "import sys, json; data=json.load(sys.stdin); print(data['tunnels'][0]['public_url'])" 2>/dev/null || true)
  if [[ -n "$NGROK_URL" ]]; then
    echo "✓ Detected active ngrok tunnel: $NGROK_URL"
    WEBHOOK_URL="$NGROK_URL"
  fi
fi

if [[ -z "$WEBHOOK_URL" ]]; then
  if [[ -t 0 ]]; then
    read -r -p "Enter public webhook URL (e.g. https://xxxx.ngrok-free.dev/api/telegram/webhook): " WEBHOOK_URL
  else
    echo "❌ Error: Webhook URL is required. Start ngrok (ngrok http 7071) or provide --url <HTTPS_URL>."
    exit 1
  fi
fi

# Ensure URL has /api/telegram/webhook path
if [[ "$WEBHOOK_URL" != */api/telegram/webhook ]]; then
  WEBHOOK_URL="${WEBHOOK_URL%/}/api/telegram/webhook"
fi

if [[ "$WEBHOOK_URL" != https://* ]]; then
  echo "❌ Error: Telegram requires a public HTTPS URL (e.g. https://xxx.ngrok-free.dev/api/telegram/webhook)."
  echo "Telegram will reject plain http:// or localhost URLs."
  exit 1
fi

echo "=================================================="
echo "Registering Webhook with Telegram"
echo "Webhook URL:  $WEBHOOK_URL"
echo "Secret Token: ${SECRET_TOKEN:0:4}**** (length: ${#SECRET_TOKEN})"
echo "=================================================="

PAYLOAD=$(python3 -c "
import json
print(json.dumps({
    'url': '$WEBHOOK_URL',
    'secret_token': '$SECRET_TOKEN',
    'allowed_updates': ['message', 'poll_answer'],
    'drop_pending_updates': False
}))
")

RESPONSE=$("${CURL_CMD[@]}" -X POST "https://api.telegram.org/bot${BOT_TOKEN}/setWebhook" \
  -H "Content-Type: application/json" \
  -d "$PAYLOAD")

echo ""
echo "Response from Telegram:"
echo "$RESPONSE" | python3 -m json.tool 2>/dev/null || echo "$RESPONSE"

if echo "$RESPONSE" | grep -q '"ok":true'; then
  echo ""
  echo "✅ Webhook successfully registered!"
else
  echo ""
  echo "❌ Webhook registration failed."
  exit 1
fi
