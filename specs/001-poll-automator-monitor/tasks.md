# Tasks: Non-Anonymous Poll Automator & Follow-Up Monitor

**Feature Directory**: `specs/001-poll-automator-monitor`  
**Date**: 2026-09-04  
**Status**: Complete & Verified Live  
**Specification**: [specs/001-poll-automator-monitor/spec.md](file:///Users/Artem_Horbunov1/EPAM/calm-class/specs/001-poll-automator-monitor/spec.md)  
**Implementation Plan**: [specs/001-poll-automator-monitor/plan.md](file:///Users/Artem_Horbunov1/EPAM/calm-class/specs/001-poll-automator-monitor/plan.md)  

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization, NuGet packages, configuration, and shared helpers.

- [x] T001 Configure project references and NuGet package versions in `Directory.Packages.props`, `src/CalmClass.Infrastructure/CalmClass.Infrastructure.csproj`, and `src/CalmClass.Functions/CalmClass.Functions.csproj`
- [x] T002 [P] Define application configuration options and local settings models in `src/CalmClass.Application/Common/Options/CalmClassOptions.cs` and `src/CalmClass.Functions/local.settings.json`
- [x] T003 [P] Implement deterministic Telegram MarkdownV2 escaping and link formatting utility in `src/CalmClass.Infrastructure/Telegram/MarkdownV2Helper.cs`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core domain records, repository contracts, Cosmos DB repository, Polly HTTP pipeline, and webhook authorization middleware.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [x] T004 [P] Create domain enums (`PollStatus`, `MemberRole`) in `src/CalmClass.Application/Domain/Enums/PollStatus.cs` and `src/CalmClass.Application/Domain/Enums/MemberRole.cs`
- [x] T005 [P] Create domain entities as C# records (`TrackedPoll`, `PollVote`, `GroupMember`) in `src/CalmClass.Application/Domain/Entities/TrackedPoll.cs`, `src/CalmClass.Application/Domain/Entities/PollVote.cs`, and `src/CalmClass.Application/Domain/Entities/GroupMember.cs`
- [x] T006 [P] Define repository interface `IPollRepository` and `IDateTimeProvider` in `src/CalmClass.Application/Common/Interfaces/IPollRepository.cs` and `src/CalmClass.Application/Common/Interfaces/IDateTimeProvider.cs`
- [x] T007 [P] Define `ITelegramBotClient` interface for outbound API calls in `src/CalmClass.Application/Common/Interfaces/ITelegramBotClient.cs`
- [x] T008 Implement Cosmos DB document models as C# records (`TrackedPollDocument`, `PollVoteDocument`, `GroupMemberDocument`) in `src/CalmClass.Infrastructure/Persistence/Documents/TrackedPollDocument.cs`
- [x] T009 Implement `CosmosPollRepository` with partition-scoped Cosmos DB queries in `src/CalmClass.Infrastructure/Persistence/CosmosPollRepository.cs`
- [x] T010 Implement `TelegramBotClient` with Polly resilience pipeline handling 429 (`retry_after`), 408, and 5xx in `src/CalmClass.Infrastructure/Telegram/TelegramBotClient.cs`
- [x] T011 [P] Implement Ukrainian localization dictionary and template formatters in `src/CalmClass.Application/Features/Polls/Localization/UkrainianPollMessages.cs`
- [x] T012 Implement Telegram secret token validation middleware in `src/CalmClass.Functions/Middleware/TelegramSecretTokenMiddleware.cs`
- [x] T013 Create base webhook HTTP trigger skeleton in `src/CalmClass.Functions/Functions/TelegramWebhookFunction.cs`

**Checkpoint**: Foundation ready — user story implementation can now begin.

---

## Phase 3: User Story 1 - Create and Publish a Non-Anonymous Decision Poll (Priority: P1) 🎯 MVP

**Goal**: Enable authorized committee admins to initiate a transparent poll directly within the classroom group with defined options and duration, rejecting unauthorized attempts, malformed options, and concurrent active polls.

**Independent Test**: Send an authorized `/create_poll` command via webhook. Verify that a non-anonymous poll (`is_anonymous = false`) is created, tracked in Cosmos DB with status `Open`, and duplicate poll creations are rejected.

### Tests for User Story 1 ⚠️
- [x] T014 [P] [US1] Unit tests for `CreatePollCommand` validation and execution in `tests/unit/CalmClass.ApplicationTests.Unit/Features/CreatePollTests.cs`
- [x] T015 [P] [US1] Unit tests for `MarkdownV2Helper` escaping in `tests/unit/CalmClass.InfrastructureTests.Unit/Telegram/MarkdownV2HelperTests.cs`
- [x] T016 [P] [US1] Unit tests for admin authorization check and active poll rejection in `tests/unit/CalmClass.ApplicationTests.Unit/Features/CreatePollAuthorizationTests.cs`

### Implementation for User Story 1
- [x] T017 [US1] Implement `CreatePollCommand` and handler with admin authorization, concurrency check, options (2–10) and duration (1–168h, default 24h) validation in `src/CalmClass.Application/Features/Polls/Commands/CreatePoll/CreatePollCommandHandler.cs`
- [x] T018 [US1] Integrate `/create_poll` command routing and execution into `src/CalmClass.Functions/Functions/TelegramWebhookFunction.cs`

**Checkpoint**: User Story 1 is fully functional and testable as an MVP.

---

## Phase 4: User Story 2 - Track and Ingest Individual Voter Participation (Priority: P1)

**Goal**: Ingest real-time `poll_answer` webhook events to record who voted, update changed choices, and handle vote retractions idempotently against the tracked poll.

**Independent Test**: Dispatch simulated `poll_answer` updates for casting, changing, and retracting a vote. Verify that Cosmos DB reflects the latest voter state and retracted votes restore the pending status.

### Tests for User Story 2 ⚠️
- [x] T019 [P] [US2] Unit tests for `IngestVoteCommand` casting, updating, and revoking in `tests/unit/CalmClass.ApplicationTests.Unit/Features/IngestVoteTests.cs`
- [x] T020 [P] [US2] Unit tests for idempotent vote updates and unknown voter handling in `tests/unit/CalmClass.ApplicationTests.Unit/Features/IngestVoteIdempotencyTests.cs`

### Implementation for User Story 2
- [x] T021 [US2] Implement `IngestVoteCommand` and handler to idempotently upsert voter choices and handle empty option revocations in `src/CalmClass.Application/Features/Polls/Commands/IngestVote/IngestVoteCommandHandler.cs`
- [x] T022 [US2] Integrate `poll_answer` webhook update routing into `src/CalmClass.Functions/Functions/TelegramWebhookFunction.cs`

**Checkpoint**: User Stories 1 AND 2 are fully functional and integrated.

---

## Phase 5: User Story 3 - Automated Follow-Up Reminders for Unresponsive Members (Priority: P2)

**Goal**: Automatically identify unresponsive roster members when a poll has $\le 6$ hours remaining, sending a single aggregated reminder pinging them (with silent notifications during quiet hours 20:00–08:00 Kyiv time).

**Independent Test**: Advance time on an active poll to within 6 hours of expiry. Trigger the monitor function to verify that all unvoted members receive a single batched notification and poll status becomes `Reminded`.

### Tests for User Story 3 ⚠️
- [ ] T023 [P] [US3] Unit tests for `PollMonitorService` reminder threshold calculation and quiet hours detection in `tests/unit/CalmClass.ApplicationTests.Unit/Features/PollMonitorReminderTests.cs`
- [ ] T024 [P] [US3] Unit tests verifying reminder deduplication and silent notification flag behavior in `tests/unit/CalmClass.ApplicationTests.Unit/Features/PollReminderExecutionTests.cs`

### Implementation for User Story 3
- [ ] T025 [US3] Implement `PollMonitorService.ProcessRemindersAsync` identifying unvoted active members and batching mentions (`@username` or `[Name](tg://user?id=...)`) with quiet hours (20:00–08:00 Kyiv) silent notifications in `src/CalmClass.Application/Features/Polls/Services/PollMonitorService.cs`
- [ ] T026 [US3] Implement `PollMonitorFunction` with `TimerTrigger` (cron: `0 */5 * * * *`) in `src/CalmClass.Functions/Functions/PollMonitorFunction.cs`

**Checkpoint**: Automated follow-up reminders execute reliably without spam or duplicate alerts.

---

## Phase 6: User Story 4 - Automated Poll Closure, Early Closure/Cancel, and Final Distribution Summary (Priority: P2)

**Goal**: Automatically finalize expired polls (or allow admin early `/close_poll` or `/cancel_poll`), stop voting in Telegram, publish an aggregated Ukrainian summary report, and provide an admin audit view.

**Independent Test**: Trigger closure on an expired poll or send `/close_poll`. Verify that voting stops in Telegram, the aggregate summary is posted with turnout percentage, and individual voter breakdown is accessible only to admins.

### Tests for User Story 4 ⚠️
- [ ] T027 [P] [US4] Unit tests for vote tallying, percentage calculations, tie resolution, and summary formatting in `tests/unit/CalmClass.ApplicationTests.Unit/Features/PollClosureTallyTests.cs`
- [ ] T028 [P] [US4] Unit tests for `/close_poll` and `/cancel_poll` command handlers in `tests/unit/CalmClass.ApplicationTests.Unit/Features/ManualPollClosureTests.cs`

### Implementation for User Story 4
- [ ] T029 [US4] Implement `ClosePollCommand` and `CancelPollCommand` handlers for early admin termination in `src/CalmClass.Application/Features/Polls/Commands/ClosePoll/ClosePollCommandHandler.cs` and `src/CalmClass.Application/Features/Polls/Commands/CancelPoll/CancelPollCommandHandler.cs`
- [ ] T030 [US4] Implement `PollMonitorService.ProcessClosuresAsync` for automatic deadline closure and Ukrainian aggregated summary publication in `src/CalmClass.Application/Features/Polls/Services/PollMonitorService.cs`
- [ ] T031 [US4] Integrate `/close_poll` and `/cancel_poll` commands into `src/CalmClass.Functions/Functions/TelegramWebhookFunction.cs`
- [ ] T032 [US4] Implement admin audit query service for retrieving detailed per-voter choices in `src/CalmClass.Application/Features/Polls/Services/PollAuditService.cs`

**Checkpoint**: All user stories are implemented, operational, and verifiable.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Cleanup, DI service registration, logging, and full test suite verification.

- [x] T033 [P] Remove skeleton dummy files (`src/CalmClass.Application/Dummy.cs`, `src/CalmClass.Infrastructure/InDum.cs`) and register all services in `ApplicationServiceExtensions.cs` and `InfrastructureServiceExtensions.cs`
- [x] T034 [P] Configure Serilog structured logging and Application Insights telemetry in `src/CalmClass.Functions/Program.cs` ensuring no raw tokens or sensitive PII are logged
- [x] T035 Run end-to-end verification and test suite execution (`dotnet test`) across all projects per `specs/001-poll-automator-monitor/quickstart.md`
- [x] T036 Reconcile and update `specs/001-poll-automator-monitor/spec.md` (and contracts/) to reflect as-built implementation behavior, edge cases, and schema details
- [x] T037 [P] Implement `/start` and `/help` command routing with Ukrainian guidance in `src/CalmClass.Functions/Functions/TelegramWebhookFunction.cs`
- [x] T038 [P] Implement `InMemoryPollRepository` fallback and initial chat admin bootstrap in `src/CalmClass.Infrastructure/Persistence/InMemoryPollRepository.cs` and `CosmosPollRepository.cs`
- [x] T039 [P] Create local developer scripts (`scripts/run-local.sh`, `scripts/register-webhook.sh`, `scripts/simulate-webhook.sh`, `docker-compose.yml`) and VS Code debug profiles in `.vscode/`

---

## Dependencies & Execution Order

### Phase Dependencies
- **Phase 1 (Setup)**: Can start immediately.
- **Phase 2 (Foundational)**: Depends on Phase 1 completion. **BLOCKS** all user stories.
- **Phase 3 (User Story 1 - P1)**: Depends on Phase 2. Enables MVP delivery.
- **Phase 4 (User Story 2 - P1)**: Depends on Phase 2. Can be worked on after or alongside US1.
- **Phase 5 (User Story 3 - P2)**: Depends on Phase 3 and Phase 4 (requires tracked polls and votes).
- **Phase 6 (User Story 4 - P2)**: Depends on Phase 3 and Phase 4 (requires tracked polls and votes).
- **Phase 7 (Polish)**: Depends on all user stories being implemented.

### Parallel Opportunities
- In Phase 1: `T002` and `T003` can run in parallel.
- In Phase 2: `T004`, `T005`, `T006`, `T007`, and `T011` can run in parallel.
- In Phase 3: Tests `T014`, `T015`, `T016` can run in parallel before implementation.
- In Phase 4: Tests `T019` and `T020` can run in parallel before implementation.
- In Phase 5: Tests `T023` and `T024` can run in parallel before implementation.
- In Phase 6: Tests `T027` and `T028` can run in parallel before implementation.
- In Phase 7: `T033` and `T034` can run in parallel; `T036` runs after verification.

---

## Implementation Strategy

### MVP Scope (User Story 1 Only)
1. Complete **Phase 1 (Setup)** and **Phase 2 (Foundational)**.
2. Complete **Phase 3 (User Story 1)**.
3. Validate `/create_poll` with admin authorization and single-poll enforcement.
4. **MVP Milestone Achieved**: Committee admins can publish verified, transparent polls in the classroom chat.

### Incremental Feature Expansion
1. Add **User Story 2** → Ingest and track individual member voting participation in real time.
2. Add **User Story 3** → Enable automated follow-up reminders with quiet-hours protection.
3. Add **User Story 4** → Enable automated deadline closure, early `/close_poll` and `/cancel_poll`, and Ukrainian results reports.
4. Complete **Phase 7 (Polish)** → Finalize logging, cleanup, run full test suites, and reconcile spec.md (T036).
