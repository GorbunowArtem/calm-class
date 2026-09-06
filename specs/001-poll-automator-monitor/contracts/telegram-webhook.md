# Contract: Telegram Webhook Ingestion

**Feature**: `specs/001-poll-automator-monitor`  
**Endpoint**: `POST /api/telegram/webhook`  
**Security**: Header `X-Telegram-Bot-Api-Secret-Token` must match configured secret.

---

## 1. Request Headers
```http
POST /api/telegram/webhook HTTP/1.1
Host: <function-app-host>
Content-Type: application/json
X-Telegram-Bot-Api-Secret-Token: <configured-secret-token>
```

---

## 2. Inbound Update Types

### 2.1 Bot Command Update (`message`)
Dispatched when an admin enters a command in the classroom group chat.

```json
{
  "update_id": 10001,
  "message": {
    "message_id": 450,
    "from": {
      "id": 123456789,
      "is_bot": false,
      "first_name": "Olena",
      "last_name": "Kovalenko",
      "username": "olena_k"
    },
    "chat": {
      "id": -1001234567890,
      "type": "supergroup",
      "title": "5-A Classroom Committee"
    },
    "date": 1757012400,
    "text": "/create_poll \"Екскурсія восени\" \"Зоопарк\" \"Музей\" \"Планетарій\" 24"
  }
}
```

### 2.2 Voting Action Update (`poll_answer`)
Dispatched by Telegram when any member casts, changes, or retracts a vote in a non-anonymous poll.

```json
{
  "update_id": 10002,
  "poll_answer": {
    "poll_id": "5987654321098765432",
    "user": {
      "id": 987654321,
      "is_bot": false,
      "first_name": "Taras",
      "last_name": "Shevchenko",
      "username": "taras_sh"
    },
    "option_ids": [1]
  }
}
```

*Note: If `option_ids` is `[]`, the member has retracted their vote.*

---

## 3. Webhook Responses

- **Success (`200 OK`)**: Immediately returned upon receiving and queueing/processing the update.
  ```json
  { "ok": true }
  ```
- **Unauthorized (`401 Unauthorized`)**: Missing or invalid `X-Telegram-Bot-Api-Secret-Token`.
  ```json
  { "error": "Invalid webhook secret token" }
  ```
- **Bad Request (`400 Bad Request`)**: Malformed JSON payload.
