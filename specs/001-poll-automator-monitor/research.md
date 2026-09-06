# Phase 0 Research: Non-Anonymous Poll Automator & Follow-Up Monitor

**Feature**: `specs/001-poll-automator-monitor`  
**Status**: Completed  
**Date**: 2026-09-04  

---

## 1. Background & Technical Context

The **Non-Anonymous Poll Automator & Follow-Up Monitor** operates as part of the CalmClass school chat automation system. It runs on **.NET 10** in **Azure Functions** (Isolated Worker), integrates with the **Telegram Bot API** via webhooks, and persists data in **Azure Cosmos DB** under Clean Architecture principles.

---

## 2. Research Decisions & Architecture

### Decision 1: Azure Functions Triggers and Execution Model
- **Decision**: Use Azure Functions Isolated Worker on .NET 10 with two distinct triggers:
  1. `HttpTrigger` (`POST /api/telegram/webhook`): Ingests Telegram `Update` payloads (`message` for commands, `poll_answer` for non-anonymous vote tracking).
  2. `TimerTrigger` (`cron: 0 */5 * * * *` — every 5 minutes): Runs the `PollMonitorService` to check for active polls requiring a reminder (6 hours prior to expiration) or automatic closure (expiration timestamp passed).
- **Rationale**: 
  - Serverless consumption model adheres to Extreme Operational Frugality ($0 baseline).
  - Isolated worker model in .NET 10 provides first-class dependency injection, middleware pipeline, and full control over JSON serialization.
  - A 5-minute timer comfortably satisfies the requirement SC-004 (polls close within 15 minutes of expiry) with minimal RU/compute consumption.
- **Alternatives Considered**:
  - *Azure Container Apps with long polling*: Rejected due to persistent hosting costs violating operational frugality.
  - *Azure Logic Apps*: Rejected due to complex business logic, lack of type safety, and vendor lock-in.

---

### Decision 2: Telegram Webhook Ingestion & Vote Event Handling
- **Decision**: 
  - Register webhook with secret token header `X-Telegram-Bot-Api-Secret-Token`.
  - Validate the secret token in middleware before processing any payload.
  - Telegram sends `poll_answer` updates whenever a user interacts with a non-anonymous poll. Payload contains:
    - `poll_id`: String matching the created poll.
    - `user`: Telegram `User` (id, first_name, last_name, username).
    - `option_ids`: Array of integers indicating selected zero-based option indices.
  - When `option_ids` is empty, the user has retracted their vote; mark the active vote record as revoked/pending.
- **Rationale**:
  - Direct update ingestion via `poll_answer` ensures real-time accuracy ($\le 5$s, SC-002) without polling Telegram.
  - Non-anonymous polls (`is_anonymous: false`) are required by Telegram for `poll_answer` updates to include the voter's `user` object.
- **Alternatives Considered**:
  - *Polling Telegram `getPoll`*: Rejected because `getPoll` does not return individual voter identities, only aggregated counts.

---

### Decision 3: Cosmos DB Data Modeling & Partitioning
- **Decision**:
  - Single container `Polls` partitioned by `/chatId` (string).
  - Multi-entity storage within the same container using hierarchical document types (`Type = "TrackedPoll" | "PollVote" | "GroupMember"`):
    - `TrackedPoll`: Keyed by `id = $"poll_{pollId}"`. Stores question, options, timestamps, lifecycle status, and `_etag`.
    - `PollVote`: Keyed by `id = $"vote_{pollId}_{userId}"`. Stores voter details and selected options.
    - `GroupMember`: Keyed by `id = $"member_{chatId}_{userId}"`. Stores role (Admin/Member), names, and active flag.
- **Rationale**:
  - All operations for a classroom group are scoped to the group's `chatId`, providing $100\%$ partition-scoped queries with single-digit millisecond latency and minimal RU consumption (1-3 RUs per read).
  - Optimistic concurrency control via Cosmos DB `ETag` (`_etag`) prevents concurrent execution conflicts during reminder evaluation and closure.
- **Alternatives Considered**:
  - *Partition by `pollId`*: Rejected because roster queries (`GroupMember`) and group active poll concurrency checks require partition-wide access across the chat.
  - *Separate containers for each entity*: Rejected because multiple containers multiply minimum provisioned throughput / serverless container overhead.

---

### Decision 4: Bot Commands & RBAC Guarding
- **Decision**:
  - Supported commands:
    - `/create_poll "<question>" "<opt1>" "<opt2>" ... [hours]`: Creates a transparent poll.
    - `/close_poll`: Stops voting early and posts results.
    - `/cancel_poll`: Voids active poll without results.
  - Command guard middleware checks `sender.id` against `GroupMember` where `chatId == update.chatId && role == "Admin" && isActive == true`.
  - Rejection with an authorization notice if unauthorized.
  - Enforce single active poll concurrency: Query `TrackedPoll` where `chatId == update.chatId && status in ("Open", "Reminded")`. If found, reject `/create_poll`.
- **Rationale**:
  - Satisfies FR-001, FR-015, FR-016, FR-017, and Constitution Section 3.2.

---

### Decision 5: Outbound Messaging, Anti-Spam & Resilience
- **Decision**:
  - Telegram Bot HTTP client wrapped with `Polly` resilience pipeline:
    - Retry on HTTP 408, 429, 5xx.
    - Extract `retry_after` seconds from Telegram 429 response body (`parameters.retry_after`) to drive backoff delay.
  - Group notification pings batched into a single aggregated message:
    - If user has `@username`: format as `@username`.
    - If user lacks `@username`: format as `[First Name](tg://user?id=12345678)`.
  - Quiet hours check: If current time in `Europe/Kyiv` timezone is between 20:00 and 08:00, pass `disable_notification: true` in `sendMessage`.
- **Rationale**:
  - Complies with Constitution Sections 2.1, 3.1, and FR-009, FR-010, FR-014.

---

### Decision 6: Localization & Text Formatting
- **Decision**:
  - All bot messages, prompts, error notices, reminders, and summary reports are localized in Ukrainian (`uk-UA`).
  - Text escaping helper strictly escapes special characters (`_`, `*`, `[`, `]`, `(`, `)`, `~`, `` ` ``, `>`, `#`, `+`, `-`, `=`, `|`, `{`, `}`, `.`, `!`) when rendering Telegram `MarkdownV2`.
- **Rationale**:
  - Satisfies FR-018 and Constitution Section 4.
