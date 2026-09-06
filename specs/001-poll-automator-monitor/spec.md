---
status: implemented
last_updated: 2026-09-06
---

# Feature Specification: Non-Anonymous Poll Automator & Follow-Up Monitor

**Feature Directory**: `specs/001-poll-automator-monitor`

**Created**: 2026-09-04

**Last Updated**: 2026-09-06

**Status**: implemented

**Input**: User description: "Automates the lifecycle of decision polls within the classroom group: initializes non-anonymous votes with preset options, captures individual voting events via webhooks, tracks voter participation against a known roster, and executes reminders for unresponsive members before closing."

## Clarifications

### Session 2026-09-04
- Q: Should the final results report published in the classroom group chat list individual voter names under each option, or only display aggregate totals? (FR-012) → A: Aggregate totals and percentages only in the group chat, with full voter breakdown viewable only by committee admins.
- Q: Can multiple polls be active simultaneously within the same classroom group, or is only one active poll allowed at a time? (FR-005) → A: Single active poll per group; reject new poll creation if an active poll is currently open.
- Q: How should the reminder message mention unresponsive members who do not have a public Telegram username? (FR-009) → A: Use inline text-link mentions (`[Name](tg://user?id=...)`) for members lacking usernames so they still receive notification alerts.
- Q: Should authorized committee admins have the ability to cancel an active poll before its deadline expires? (FR-005) → A: Support both `/cancel_poll` (voids poll without tallying results) and `/close_poll` (closes voting early and publishes current results immediately).
- Q: What primary language should the bot use for group chat announcements, reminders, and command responses? (FR-012) → A: Ukrainian language for all bot messages, reminders, summaries, and command responses.
- Q: What language and modeling conventions must be applied to all data models and code? → A: All code must be written in C# (.NET 10), and data models, DTOs, and entities must use C# 'record' types where possible for immutability.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Create and Publish a Non-Anonymous Decision Poll (Priority: P1)

As a classroom committee admin, I want to initiate a transparent poll directly within the group chat with defined options and a duration, so that parents can vote on classroom decisions and their identities are visible to prevent duplicate or unauthorized voting.

**Why this priority**: Initiating a structured, non-anonymous vote is the foundational entry point for the entire voting and decision lifecycle. Without poll creation, no voting or monitoring can take place.

**Independent Test**: Can be tested independently by an authorized committee admin sending a poll creation command with a question, options, and duration. The system successfully posts a non-anonymous poll to the group, stores the tracked poll details, and prevents unauthorized users from creating polls.

**Acceptance Scenarios**:

- [x] **Given** an authorized committee admin in the classroom group, **When** they submit `/create_poll "Select field trip venue" "Zoo" "Museum" 24`, **Then** a non-anonymous poll is published to the group with the specified question and options, set to expire in 24 hours.
- [x] **Given** a standard non-admin group member, **When** they attempt to run `/create_poll`, **Then** the command is rejected with an authorization error message, and no poll is published.
- [x] **Given** an admin provides less than 2 options or an invalid duration, **When** they submit the command, **Then** the system provides clear usage instructions and does not publish an incomplete poll.
- [x] **Given** an active poll is currently open in the classroom group, **When** an admin attempts to run `/create_poll`, **Then** the command is rejected with a message indicating a poll is already active, and no new poll is published.

---

### User Story 2 - Track and Ingest Individual Voter Participation (Priority: P1)

As a committee member, I want every member's voting action (choice selection, modification, or retraction) to be captured in real-time, so that the community maintains an accurate, verifiable tally of who has participated.

**Why this priority**: Real-time vote ingestion provides the core data integrity required to know who has voted and who remains unresponsive.

**Independent Test**: Can be tested independently by having users cast votes or change/retract votes on an active poll. The system records the voter's identity and choice, reflects retracted votes immediately, and remains idempotent if receiving duplicate updates.

**Acceptance Scenarios**:

- [x] **Given** an open tracked poll, **When** an eligible member selects an option, **Then** the system records the member's user identifier, display name, selected option, and voting timestamp.
- [x] **Given** an active vote recorded for a user, **When** the user changes their option, **Then** their previous selection is updated to the new selection with an updated timestamp.
- [x] **Given** an active vote recorded for a user, **When** the user retracts their vote completely, **Then** their vote record is revoked/removed so they are once again counted as pending.

---

### User Story 3 - Automated Follow-Up Reminders for Unresponsive Members (Priority: P2)

As a classroom committee, I want the system to automatically identify which members haven't voted as the deadline approaches and send a single batched reminder pinging the remaining members, so that we reach maximum participation before time runs out.

**Why this priority**: Classroom polls often stall without reminders. Automating follow-ups eliminates awkward manual chasing while respecting anti-spam and working-hour norms.

**Independent Test**: Can be tested by creating an active poll with some members voting and others idle. When the countdown threshold (e.g., 6 hours remaining) is reached, the system identifies the unresponsive members against the known active roster and sends a single aggregated reminder message to the group referencing the poll.

**Acceptance Scenarios**:

- [x] **Given** an open poll with 6 or fewer hours remaining until the deadline, **When** the periodic reminder check executes and a reminder has not yet been sent, **Then** the system compares the active roster against registered votes, compiles all pending members into a single batched notification, and tags/mentions them in the group (using `@username` where available and inline user links `[Name](tg://user?id=...)` for members without handles).
- [x] **Given** all active members have already cast their votes, **When** the reminder check runs, **Then** no reminder message is dispatched.
- [x] **Given** a reminder was already sent for the poll, **When** subsequent reminder checks run, **Then** no duplicate reminder is sent.
- [x] **Given** the reminder trigger fires during quiet hours (20:00 – 08:00 local time), **Then** the notification is sent silently (without audible alert) to respect members' sleep hours.

---

### User Story 4 - Automated Poll Closure and Final Distribution Summary (Priority: P2)

As a group member and committee admin, I want polls to automatically close at their designated deadline and publish a comprehensive results breakdown (winning option, percentages, and voter count), so that decisions are finalized promptly and transparently.

**Why this priority**: Closing polls on time prevents late votes from invalidating discussions and ensures closure on group decisions without manual admin intervention.

**Independent Test**: Can be tested by letting a poll reach its deadline. The system automatically stops the poll in the chat, tallies votes, announces the winning option(s) with participation statistics, and marks the poll status as closed.

**Acceptance Scenarios**:

- [x] **Given** an open or reminded poll whose deadline has passed, **When** the periodic evaluation runs, **Then** the poll is closed in the chat so no further votes can be submitted.
- [x] **Given** a closed poll with recorded votes, **When** closure completes, **Then** the system calculates the winning option, voter turnout percentage against the active roster, and publishes an aggregated summary message to the group containing option totals and percentages only without individual voter identities.
- [x] **Given** a closed poll, **When** an authorized committee admin requests the detailed audit record, **Then** the system provides the complete per-voter choice breakdown.
- [x] **Given** a closed poll, **When** any late vote updates arrive, **Then** they are ignored and do not alter the finalized outcome.
- [x] **Given** an open or reminded poll in the group, **When** an authorized admin issues `/close_poll`, **Then** voting is stopped immediately in the chat and the aggregated results summary is published without waiting for the scheduled deadline.
- [x] **Given** an open or reminded poll in the group, **When** an authorized admin issues `/cancel_poll`, **Then** the poll is stopped in the chat, marked as `Cancelled`, and a cancellation notice is published stating the poll was voided.
- [x] **Given** a standard non-admin member, **When** they attempt to run `/close_poll` or `/cancel_poll`, **Then** the command is rejected with an authorization error.

---

### Edge Cases (As-Built)

- **Retracted Votes**: A voter casts a vote and later withdraws it in the chat interface. Telegram delivers a `poll_answer` with empty `option_ids`. The system sets `IsRevoked = true` on the vote record and restores the member to the pending unresponsive reminder list.
- **Tied Winning Options**: If two or more options receive identical highest vote counts upon deadline expiry, the Ukrainian summary report identifies all tied options separated by commas (e.g. `Переможці: Варіант 1, Варіант 2`) rather than choosing an arbitrary single winner.
- **Zero Turnout**: If a poll expires with zero votes cast, the system reports `Опитування завершено без участі голосуючих` rather than throwing divide-by-zero exceptions during percentage calculations.
- **Unknown Voter Auto-Enrollment**: If an individual in the group chat casts a vote but does not exist in the pre-configured `GroupMember` roster, the system ingests their vote and automatically enrolls them into the group roster with `MemberRole.Member` and `IsActive = true`, ensuring accurate future roster tracking.
- **Initial Administrator Bootstrap**: In `InMemoryPollRepository`, if no active admin exists in a chat, the first user running an administrative command is auto-promoted to `MemberRole.Admin` to prevent lockout in newly created groups or local development.
- **MarkdownV2 Entity Escaping & Plain-Text Fallback**: Outbound Telegram messages are escaped via `MarkdownV2Helper.Escape`. If Telegram rejects a message with `can't parse entities`, `TelegramBotClient` automatically falls back to delivering the message as plain text.
- **Cyrillic & Quoted Multi-Word Arguments**: The `/create_poll` parser (`CreatePollArgsParser`) employs source-generated regex with named capture groups supporting both single quotes (`'...'`) and double quotes (`"..."`) around Cyrillic/Ukrainian questions and options.
- **Single Active Poll Concurrency**: The system strictly queries for any poll in `Open` or `Reminded` status for the target `chatId` and rejects `/create_poll` if one already exists.
- **Quiet Hours Reminders**: Reminders triggered between 20:00 and 08:00 (Kyiv timezone, `Europe/Kyiv`) explicitly set `disable_notification = true` on the Telegram `sendMessage` call.
- **Group Anti-Spam Rate Limits**: Outbound reminders batch all unresponsive roster members into a single message using `@username` where available and inline text-mention links (`[Name](tg://user?id=...)`) for members lacking handles.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST enforce role-based access control, permitting only authorized committee members to execute poll creation and administrative actions.
- **FR-002**: The system MUST enforce that all created polls are non-anonymous (`is_anonymous = false`), ensuring individual voter choices can be audited and tallied.
- **FR-003**: The system MUST support configurable multi-answer permission per poll (defaulting to single answer).
- **FR-004**: When `/create_poll` is invoked without a duration parameter, the system MUST apply a default expiration of 24 hours.
- **FR-005**: The system MUST track poll metadata including chat identifier, poll identifier, message identifier, question title, serialized options, expiration timestamp, reminder status, and lifecycle state (`Open`, `Reminded`, `Closed`, `Cancelled`).
- **FR-006**: The system MUST ingest real-time vote updates idempotently, associating voter identifier, voter display name, selected option indices, and voting timestamp with the corresponding poll.
- **FR-007**: When a voter update contains empty option selections, the system MUST interpret this as a vote revocation and remove or invalidate the active vote record.
- **FR-008**: The system MUST periodically check active polls and dispatch a reminder when a poll is within 6 hours of expiration and has not yet been reminded.
- **FR-009**: The reminder message MUST be a single aggregated message referencing the poll and mentioning only the active roster members who have not yet voted, using `@username` where present and inline text mentions (`[Name](tg://user?id=...)`) for members without public handles.
- **FR-010**: Any reminder dispatched outside working hours (20:00 – 08:00) MUST be sent as a silent notification.
- **FR-011**: The system MUST automatically close polls once their deadline has expired, stopping further vote submissions in the chat.
- **FR-012**: Upon poll closure, the system MUST publish an aggregated summary to the group chat containing only winning option(s), vote totals, percentages, and turnout against the active roster without exposing individual member choices publicly.
- **FR-013**: The system MUST provide authorized committee members with access to retrieve or view the full individual voter breakdown for audit purposes.
- **FR-014**: All outbound messaging MUST be resilient to transient network errors and rate-limiting throttling, utilizing jittered backoff respecting rate-limit retry intervals.
- **FR-015**: The system MUST enforce a concurrency limit of exactly one active (`Open` or `Reminded`) poll per classroom group at any given time, rejecting new poll creation until the active poll reaches `Closed` or `Cancelled` status.
- **FR-016**: The system MUST allow authorized committee members to execute `/close_poll` to finalize voting and publish results immediately ahead of the scheduled deadline.
- **FR-017**: The system MUST allow authorized committee members to execute `/cancel_poll` to void the active poll immediately, setting its state to `Cancelled` and notifying the group without publishing voting results.
- **FR-018**: All user-facing bot messages (including poll notifications, reminders, status updates, error notices, and final results summaries) MUST be localized in Ukrainian.
- **FR-019**: The system MUST support `/start` and `/help` commands to guide users and administrators on available operations, providing localized command syntax instructions.

### Key Entities & As-Built Persistence Schemas

All entities are implemented as immutable C# positional `record` types in `CalmClass.Application.Domain.Entities` and serialized to Azure Cosmos DB with partition key `/chatId` under container `Polls` using `System.Text.Json` camelCase properties:

- **TrackedPoll** (`TrackedPollDocument`):
  - Document ID: `poll_{pollId}`
  - Partition Key: `/chatId`
  - Attributes: `ChatId` (`string`), `PollId` (`string`), `MessageId` (`int`), `Question` (`string`), `Options` (`IReadOnlyList<string>`), `AllowsMultipleAnswers` (`bool`), `CreatedAtUtc` (`DateTime`), `ExpiresAtUtc` (`DateTime`), `Status` (`string`: `Open`, `Reminded`, `Closed`, `Cancelled`), `RemindedAtUtc` (`DateTime?`), `ClosedAtUtc` (`DateTime?`), `ETag` (`string?`), `Type` (`"TrackedPoll"`).
- **PollVote** (`PollVoteDocument`):
  - Document ID: `vote_{chatId}_{pollId}_{userId}`
  - Partition Key: `/chatId`
  - Attributes: `ChatId` (`string`), `PollId` (`string`), `UserId` (`long`), `DisplayName` (`string`), `Username` (`string?`), `SelectedOptionIndices` (`IReadOnlyList<int>`), `VotedAtUtc` (`DateTime`), `IsRevoked` (`bool`), `Type` (`"PollVote"`).
- **GroupMember** (`GroupMemberDocument`):
  - Document ID: `member_{chatId}_{userId}`
  - Partition Key: `/chatId`
  - Attributes: `ChatId` (`string`), `UserId` (`long`), `DisplayName` (`string`), `Username` (`string?`), `Role` (`string`: `Admin`, `Member`), `IsActive` (`bool`), `JoinedAtUtc` (`DateTime`), `Type` (`"GroupMember"`).

### Endpoints & Webhook Triggers

- **HTTP Webhook**: `POST /api/telegram/webhook` (`TelegramWebhookFunction` with Route = `telegram/webhook`).
  - Guarded by `TelegramSecretTokenMiddleware` validating `X-Telegram-Bot-Api-Secret-Token`.
  - Dispatches commands: `/create_poll`, `/close_poll`, `/cancel_poll`, `/start`, `/help`.
  - Ingests `poll_answer` events for vote casting, updating, and revocation.
- **Timer Trigger**: `PollMonitorFunction` running every 5 minutes (`0 */5 * * * *`).
  - Evaluates active polls for reminder windows ($\le 6$ hours) and passes silent notifications during quiet hours.
  - Automatically executes poll closure and posts summary results for expired polls.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of created polls are non-anonymous and correctly associated with the active group roster.
- **SC-002**: Vote ingestion reflects accurate participation state within 5 seconds of a user submitting or modifying their choice.
- **SC-003**: Unresponsive member reminders are sent exactly once per poll and batch all pending members into a single message, generating zero redundant per-user messages.
- **SC-004**: Polls close automatically within 15 minutes of reaching their configured expiration deadline.
- **SC-005**: 100% of outbound communications comply with platform rate limits without message drops or unhandled throttling errors.

## Assumptions

- **Roster Availability**: An active roster of classroom group members (`GroupMember`) is maintained or synchronized in storage to determine which members are pending votes.
- **Default Duration**: If an admin does not provide an explicit duration in hours when creating a poll, 24 hours is assumed.
- **Reminder Threshold**: Follow-up reminders are triggered at 6 hours prior to poll expiration; polls with an initial duration of 6 hours or less are reminded halfway through their duration.
- **Working Hours Definition**: Routine audible reminders are restricted to 08:00 – 20:00 in the classroom group's local timezone (`Europe/Kyiv`); reminders outside this window use silent notifications.
- **Poll Immutability**: Poll questions and options cannot be edited once published to the chat; if an admin makes a mistake, they can cancel or close the poll and create a new one.
- **Localization**: All user-facing bot communication and reporting within the classroom group are conducted in Ukrainian.
- **C# Records Convention**: All domain entities, data models, and DTOs are defined using C# `record` types.
- **System.Text.Json Exclusively**: All JSON operations use `System.Text.Json`; `Newtonsoft.Json` is forbidden.

---

## Final Implementation State & Verification

- **Implementation Complete**: 100% of functional requirements (FR-001 through FR-019) and user stories (US1–US4) implemented across `src/CalmClass.Application`, `src/CalmClass.Infrastructure`, and `src/CalmClass.Functions`.
- **Live Verification**: Fully tested and verified live with the Telegram Bot API (`@calm_class_bot`) via an active `ngrok` tunnel (`https://flashing-hazy-uproar.ngrok-free.dev`), verifying poll creation, `/start`, voting, and error reporting.
- **Resilience & Local Experience**:
  - Offline / Local Development Fallback: `InMemoryPollRepository` registered as `Singleton` in DI conditionally via `CosmosDb:UseInMemory = true` or `ConnectionString = "InMemory"`.
  - Initial Administrator Bootstrap: Auto-promotes the first user running `/create_poll` as `Admin` if no admins exist, preventing lockouts in newly initialized groups.
  - Plain-Text Fallback: `TelegramBotClient` transparently retries failed `MarkdownV2` deliveries as plain text if entity parsing errors occur.
- **Test Suite**: 50 automated unit tests (35 in Application, 15 in Infrastructure) passing with 0 warnings and 0 errors.
- **Tooling**: Added `scripts/run-local.sh`, `scripts/register-webhook.sh`, `scripts/simulate-webhook.sh`, and `.vscode/` debug profiles.

