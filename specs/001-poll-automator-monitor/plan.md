# Implementation Plan: Non-Anonymous Poll Automator & Follow-Up Monitor

**Branch**: `001-poll-automator-monitor` | **Date**: 2026-09-04 | **Spec**: [specs/001-poll-automator-monitor/spec.md](file:///Users/Artem_Horbunov1/EPAM/calm-class/specs/001-poll-automator-monitor/spec.md)

**Input**: Feature specification from `specs/001-poll-automator-monitor/spec.md`

---

## Summary

Automates the complete lifecycle of transparent, non-anonymous decision polls for school classroom groups via Telegram Bot API and Azure Functions on .NET 10. The system accepts administrative commands (`/create_poll`, `/close_poll`, `/cancel_poll`) via HTTPS webhook, stores poll and vote state in Azure Cosmos DB, captures real-time voting updates from Telegram `poll_answer` events, executes batched reminders for unresponsive members 6 hours before deadline (with silent notifications during quiet hours 20:00–08:00), automatically closes expired polls, and publishes aggregate Ukrainian results summaries.

---

## Technical Context

**Language/Version**: .NET 10 / C# latest major (All code in C#; domain entities, data models, DTOs, and Cosmos DB document models use C# `record` types where possible)  
**Primary Dependencies**: 
- Microsoft.Azure.Functions.Worker (v2.x, Isolated Worker Model)
- Polly (Resilience pipelines for Telegram HTTP requests)
- Serilog (Structured logging via Microsoft.Extensions.Logging)
- System.Text.Json (Serialization)
- Microsoft.Azure.Cosmos (Cosmos DB client)

**Storage**: Azure Cosmos DB (Container: `Polls`, Partition Key: `/chatId`, Multi-entity partition storage with optimistic concurrency via `_etag`)  
**Testing**: TUnit (`net10.0`), AutoFixture, Moq  
**Target Platform**: Azure Functions (Serverless Consumption Plan, Linux runtime)  
**Project Type**: Serverless Web Service & Background Monitor  
**Performance Goals**: 
- Real-time vote ingestion within 5 seconds of cast/retract (SC-002)
- Automated poll closure within 15 minutes of expiration (SC-004)
- Outbound group messages rate-limited to $\le 1$ msg/sec per chat

**Constraints**:
- Extreme Operational Frugality ($0 compute baseline on Consumption tier)
- Strict single active poll concurrency per classroom group
- Ukrainian localization (`uk-UA`) for all user-facing messages
- Quiet hours (20:00–08:00 Kyiv time) enforced with silent notifications

**Scale/Scope**: 
- 20–50 members per classroom group
- Typically 1–3 polls per week per group
- Low query volume, high data integrity requirement

---

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle / Rule | Requirement | Plan Compliance | Status |
|------------------|-------------|-----------------|--------|
| **1. Spec-First Engineering** | Dedicated spec & clarification before code | Spec completed and clarified (5/5 Q&A) in `spec.md` | **PASS** |
| **2. Extreme Operational Frugality** | Free/serverless tiers, zero persistent compute costs | Azure Functions Consumption + Cosmos DB Free/Serverless Tier | **PASS** |
| **3. Privacy & Least Privilege** | Minimal PII, protected sensitive data | Final summary aggregates votes; individual voter names kept for admin audit only | **PASS** |
| **4. Idempotency & Resilience** | Idempotent ingestion, Polly transient retry | Unique document ID for votes (`vote_{pollId}_{userId}`); Polly retry with `retry_after` | **PASS** |
| **5. Clean Architecture** | Domain in Application, adapters in Infrastructure, triggers in Functions | Solution follows `Application`, `Infrastructure`, `Functions` separation | **PASS** |
| **6. Telegram Anti-Spam** | $\le 1$ msg/s, batch pings, quiet hours silent notifications | Batched comma-separated pings; silent messages between 20:00 and 08:00 | **PASS** |
| **7. Telegram Non-Anonymous** | `is_anonymous = false` enforced | All polls created with `is_anonymous: false` | **PASS** |
| **8. Testing Standards** | TUnit for domain & unit testing | TUnit test projects configured in solution | **PASS** |

*Result: All gates pass with zero violations.*

---

## Project Structure

### Documentation (this feature)

```text
specs/001-poll-automator-monitor/
├── spec.md              # Feature specification & clarifications
├── plan.md              # Implementation plan (this file)
├── research.md          # Phase 0: Research decisions & architecture
├── data-model.md        # Phase 1: Entities, schema, state machine
├── quickstart.md        # Phase 1: End-to-end verification guide
├── contracts/           # Phase 1: Webhook, bot commands, Cosmos contracts
│   ├── telegram-webhook.md
│   ├── bot-commands.md
│   └── cosmos-documents.md
└── checklists/
    └── requirements.md  # Quality checklist
```

### Source Code (repository root)

```text
src/
├── CalmClass.Application/
│   ├── Common/
│   │   ├── Interfaces/
│   │   │   ├── IPollRepository.cs
│   │   │   ├── ITelegramBotClient.cs
│   │   │   ├── TelegramPollResult.cs
│   │   │   └── IDateTimeProvider.cs
│   │   └── Options/
│   │       ├── CalmClassOptions.cs
│   │       ├── TelegramOptions.cs
│   │       ├── CosmosDbOptions.cs
│   │       ├── QuietHoursOptions.cs
│   │       └── PollOptions.cs
│   ├── Domain/
│   │   ├── Entities/
│   │   │   ├── TrackedPoll.cs
│   │   │   ├── PollVote.cs
│   │   │   └── GroupMember.cs
│   │   └── Enums/
│   │       ├── PollStatus.cs
│   │       └── MemberRole.cs
│   ├── Features/Polls/
│   │   ├── Commands/
│   │   │   ├── CreatePoll/
│   │   │   │   ├── CreatePollCommand.cs
│   │   │   │   ├── CreatePollResult.cs
│   │   │   │   └── CreatePollCommandHandler.cs
│   │   │   ├── ClosePoll/
│   │   │   │   ├── ClosePollCommand.cs
│   │   │   │   ├── ClosePollResult.cs
│   │   │   │   └── ClosePollCommandHandler.cs
│   │   │   ├── CancelPoll/
│   │   │   │   ├── CancelPollCommand.cs
│   │   │   │   ├── CancelPollResult.cs
│   │   │   │   └── CancelPollCommandHandler.cs
│   │   │   └── IngestVote/
│   │   │       ├── IngestVoteCommand.cs
│   │   │       ├── IngestVoteResult.cs
│   │   │       └── IngestVoteCommandHandler.cs
│   │   └── Services/
│   │       ├── PollMonitorService.cs
│   │       ├── PollAuditService.cs
│   │       ├── PollAuditReport.cs
│   │       ├── VoterAuditRecord.cs
│   │       └── Localization/
│   │           └── UkrainianPollMessages.cs
│   └── ApplicationServiceExtensions.cs
│
├── CalmClass.Infrastructure/
│   ├── Persistence/
│   │   ├── CosmosPollRepository.cs
│   │   ├── InMemoryPollRepository.cs
│   │   └── Documents/
│   │       ├── TrackedPollDocument.cs
│   │       ├── PollVoteDocument.cs
│   │       └── GroupMemberDocument.cs
│   ├── Telegram/
│   │   ├── TelegramBotClient.cs
│   │   ├── TelegramResiliencePipeline.cs
│   │   └── MarkdownV2Helper.cs
│   └── InfrastructureServiceExtensions.cs
│
└── CalmClass.Functions/
    ├── Functions/
    │   ├── TelegramWebhookFunction.cs    # HttpTrigger for Telegram updates (/start, /create_poll, etc.)
    │   └── PollMonitorFunction.cs        # TimerTrigger (cron: every 5 min)
    ├── Middleware/
    │   └── TelegramSecretTokenMiddleware.cs
    ├── local.settings.example.json
    ├── Program.cs
    └── host.json

scripts/
├── run-local.sh
├── register-webhook.sh
└── simulate-webhook.sh

docker-compose.yml

tests/unit/
├── CalmClass.ApplicationTests.Unit/
│   ├── Domain/
│   │   └── TrackedPollTests.cs
│   ├── Features/
│   │   ├── CreatePollTests.cs
│   │   ├── IngestVoteTests.cs
│   │   └── PollMonitorServiceTests.cs
│   └── Localization/
│       └── UkrainianPollMessagesTests.cs
└── CalmClass.InfrastructureTests.Unit/
    ├── Telegram/
    │   ├── MarkdownV2HelperTests.cs
    │   └── TelegramResiliencePipelineTests.cs
    └── Persistence/
        └── CosmosMappingTests.cs
```

**Structure Decision**: 
Leverages existing 3-layer Clean Architecture solution (`CalmClass.Application`, `CalmClass.Infrastructure`, `CalmClass.Functions`) and corresponding TUnit test projects, aligning with the Constitution without adding unnecessary complexity.

---

## Complexity Tracking

*No constitution violations. Table left blank.*

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| None | N/A | N/A |
