#!/usr/bin/env bash
# ==============================================================================
# CalmClass - Simulate Telegram Webhook Events Locally
# Usage: ./scripts/simulate-webhook.sh [create|vote|retract|close|cancel|timer]
# ==============================================================================

set -euo pipefail

BASE_URL="${BASE_URL:-http://localhost:7071}"
SECRET_TOKEN="${SECRET_TOKEN:-mock_secret}"
CHAT_ID="${CHAT_ID:--1001234567890}"
ADMIN_ID="${ADMIN_ID:-111}"
VOTER_ID="${VOTER_ID:-222}"
POLL_ID="${POLL_ID:-5987654321098765432}"

ACTION="${1:-help}"

case "$ACTION" in
  create)
    echo "Simulating /create_poll by Admin in chat $CHAT_ID..."
    curl -i -X POST "$BASE_URL/api/telegram/webhook" \
      -H "Content-Type: application/json" \
      -H "X-Telegram-Bot-Api-Secret-Token: $SECRET_TOKEN" \
      -d "{
        \"update_id\": $(date +%s),
        \"message\": {
          \"message_id\": 100,
          \"from\": { \"id\": $ADMIN_ID, \"first_name\": \"Admin\", \"username\": \"admin_user\" },
          \"chat\": { \"id\": $CHAT_ID, \"type\": \"supergroup\" },
          \"text\": \"/create_poll \\\"Екскурсія восени\\\" \\\"Зоопарк\\\" \\\"Музей\\\" \\\"Планетарій\\\" 24\"
        }
      }"
    ;;

  vote)
    OPTION="${2:-0}"
    echo "Simulating voter $VOTER_ID voting for option index $OPTION on poll $POLL_ID..."
    curl -i -X POST "$BASE_URL/api/telegram/webhook" \
      -H "Content-Type: application/json" \
      -H "X-Telegram-Bot-Api-Secret-Token: $SECRET_TOKEN" \
      -d "{
        \"update_id\": $(date +%s),
        \"poll_answer\": {
          \"poll_id\": \"$POLL_ID\",
          \"user\": { \"id\": $VOTER_ID, \"first_name\": \"Тарас\", \"last_name\": \"Шевченко\", \"username\": \"taras_sh\" },
          \"option_ids\": [$OPTION]
        }
      }"
    ;;

  retract)
    echo "Simulating voter $VOTER_ID retracting vote on poll $POLL_ID..."
    curl -i -X POST "$BASE_URL/api/telegram/webhook" \
      -H "Content-Type: application/json" \
      -H "X-Telegram-Bot-Api-Secret-Token: $SECRET_TOKEN" \
      -d "{
        \"update_id\": $(date +%s),
        \"poll_answer\": {
          \"poll_id\": \"$POLL_ID\",
          \"user\": { \"id\": $VOTER_ID, \"first_name\": \"Тарас\" },
          \"option_ids\": []
        }
      }"
    ;;

  close)
    echo "Simulating /close_poll by Admin in chat $CHAT_ID..."
    curl -i -X POST "$BASE_URL/api/telegram/webhook" \
      -H "Content-Type: application/json" \
      -H "X-Telegram-Bot-Api-Secret-Token: $SECRET_TOKEN" \
      -d "{
        \"update_id\": $(date +%s),
        \"message\": {
          \"message_id\": 105,
          \"from\": { \"id\": $ADMIN_ID, \"first_name\": \"Admin\" },
          \"chat\": { \"id\": $CHAT_ID },
          \"text\": \"/close_poll\"
        }
      }"
    ;;

  cancel)
    echo "Simulating /cancel_poll by Admin in chat $CHAT_ID..."
    curl -i -X POST "$BASE_URL/api/telegram/webhook" \
      -H "Content-Type: application/json" \
      -H "X-Telegram-Bot-Api-Secret-Token: $SECRET_TOKEN" \
      -d "{
        \"update_id\": $(date +%s),
        \"message\": {
          \"message_id\": 106,
          \"from\": { \"id\": $ADMIN_ID, \"first_name\": \"Admin\" },
          \"chat\": { \"id\": $CHAT_ID },
          \"text\": \"/cancel_poll\"
        }
      }"
    ;;

  timer)
    echo "Triggering PollMonitorFunction via Azure Functions admin API..."
    curl -i -X POST "$BASE_URL/admin/functions/PollMonitorFunction" \
      -H "Content-Type: application/json" \
      -d "{}"
    ;;

  *)
    echo "Usage: $0 [create|vote|retract|close|cancel|timer]"
    echo ""
    echo "Commands:"
    echo "  create        Simulate /create_poll command from group admin"
    echo "  vote [opt]    Simulate voter submitting choice (default option 0)"
    echo "  retract       Simulate voter unselecting/retracting vote"
    echo "  close         Simulate /close_poll command by admin (generates summary)"
    echo "  cancel        Simulate /cancel_poll command by admin (voids poll)"
    echo "  timer         Trigger the 5-minute PollMonitorFunction cycle immediately"
    ;;
esac
