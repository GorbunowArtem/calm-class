# Quickstart & Verification Guide: Non-Anonymous Poll Automator & Follow-Up Monitor

**Feature**: `specs/001-poll-automator-monitor`  
**Status**: Ready for Implementation  
**Date**: 2026-09-04  

---

## 1. Prerequisites & Environment Setup

- **.NET SDK**: .NET 10.0+ (`dotnet --version`)
- **Azure Functions Core Tools**: v4.x (for local execution of Isolated Worker functions)
- **Azurite / Cosmos DB Emulator**: Local storage or Azure Cosmos DB connection string
- **Test Runner**: Microsoft Testing Platform / TUnit (`dotnet test`)

---

## 2. Local Configuration (`local.settings.json`)

Configure local settings for running the function app:
```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "Telegram__BotToken": "<mock-or-dev-token>",
    "Telegram__WebhookSecretToken": "test-secret-token-123",
    "CosmosDb__ConnectionString": "AccountEndpoint=https://localhost:8081/;AccountKey=...",
    "CosmosDb__DatabaseName": "CalmClassDb",
    "CosmosDb__ContainerName": "Polls"
  }
}
```

---

## 3. End-to-End Validation Scenarios

### Scenario 1: Poll Creation via Telegram Command
1. **Action**: Send HTTP POST to `/api/telegram/webhook` simulating an admin running `/create_poll`:
   ```bash
   curl -X POST http://localhost:7071/api/telegram/webhook \
     -H "Content-Type: application/json" \
     -H "X-Telegram-Bot-Api-Secret-Token: test-secret-token-123" \
     -d '{
       "update_id": 1,
       "message": {
         "message_id": 100,
         "from": { "id": 111, "first_name": "Admin", "username": "admin_user" },
         "chat": { "id": -1001234567890, "type": "supergroup" },
         "text": "/create_poll \"Тестове опитування\" \"Варіант А\" \"Варіант Б\" 24"
       }
     }'
   ```
2. **Expected Outcome**:
   - HTTP 200 OK returned.
   - Outbound call to Telegram `sendPoll` executed with `is_anonymous = false`.
   - `TrackedPoll` document persisted in Cosmos DB with status `Open`.

### Scenario 2: Ingestion of Non-Anonymous Votes
1. **Action**: Send HTTP POST simulating user vote:
   ```bash
   curl -X POST http://localhost:7071/api/telegram/webhook \
     -H "Content-Type: application/json" \
     -H "X-Telegram-Bot-Api-Secret-Token: test-secret-token-123" \
     -d '{
       "update_id": 2,
       "poll_answer": {
         "poll_id": "<poll-id-from-scenario-1>",
         "user": { "id": 222, "first_name": "Maria", "username": "maria_m" },
         "option_ids": [0]
       }
     }'
   ```
2. **Expected Outcome**:
   - HTTP 200 OK returned.
   - `PollVote` record stored in Cosmos DB with `userId = 222`, `selectedOptionIndices = [0]`, `isRevoked = false`.

### Scenario 3: Automated Reminder Execution (TimerTrigger)
1. **Action**: Trigger the timer function `PollMonitorFunction` when remaining time is $\le 6$ hours and active members have not voted.
2. **Expected Outcome**:
   - `PollMonitorFunction` identifies pending members by diffing active `GroupMember` roster against `PollVote`.
   - Single aggregated message sent mentioning pending members (using `@username` or `[Name](tg://user?id=...)`).
   - Poll state updated to `Reminded` in Cosmos DB.

### Scenario 4: Automated and Manual Poll Closure
1. **Action**: Simulate `/close_poll` by admin or let timer trigger close expired poll.
2. **Expected Outcome**:
   - Outbound `stopPoll` called on Telegram API.
   - Group summary message published in Ukrainian with turnout percentage and winning option.
   - Poll state updated to `Closed`. Late votes rejected/ignored.

---

## 4. Running Automated Unit & Component Tests

Execute all TUnit tests across application and infrastructure projects:
```bash
dotnet test tests/unit/CalmClass.ApplicationTests.Unit/
dotnet test tests/unit/CalmClass.InfrastructureTests.Unit/
```

Reference contracts:
- [Telegram Webhook Contract](file:///Users/Artem_Horbunov1/EPAM/calm-class/specs/001-poll-automator-monitor/contracts/telegram-webhook.md)
- [Bot Commands Contract](file:///Users/Artem_Horbunov1/EPAM/calm-class/specs/001-poll-automator-monitor/contracts/bot-commands.md)
- [Cosmos DB Documents](file:///Users/Artem_Horbunov1/EPAM/calm-class/specs/001-poll-automator-monitor/contracts/cosmos-documents.md)
