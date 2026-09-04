# System Constitution: School Chat Automation Platform

## 1. Core Principles & Philosophy

- **Spec-First Engineering:** Every feature, data schema modification, or integration contract must originate from a dedicated specification before code is written. Specifications are living source-of-truth documents.
- **Living Spec Synchronization (MUST):** Specifications are living source-of-truth documents. Upon completing implementation for any feature or phase, the agent MUST review and synchronize `spec.md` (and related contracts) with the as-built reality—incorporating any newly handled edge cases, schema adjustments, or refined behaviors. An implementation is not considered complete if the specification is out of date.
- **Extreme Operational Frugality:** The platform operates strictly within free or near-zero-cost serverless tiers (Azure Consumption, Azure Table Storage, zero-cost Telegram Bot API). Architectural choices that introduce persistent compute costs or unneeded premium tiers are rejected by default.
- **Privacy & Least Privilege:** Student identity, parental phone numbers, and payment details are sensitive. Data stored must be minimal, stripped of unnecessary PII, encrypted at rest, and never exposed to unauthorized group members.
- **Idempotency & Resilience:** Network flakiness, webhook re-deliveries from Telegram, and transient bank API timeouts are treated as normal conditions. Every ingestion endpoint must be strictly idempotent.

---

## 2. Architectural Guidelines & Technical Standards

### 2.1 Backend & Runtime

- **Runtime & Language:** .NET 10 using C# latest language features. All backend code and data models must be written in C#.
- **Data Models & Types:** Domain entities, data contracts, DTOs, value objects, and Cosmos DB document models must leverage C# `record` types (`record class` or `record struct`) where possible to enforce immutability and concise value semantics.
- **Execution Model:** Azure Functions.
- **Design Pattern:** Clean Architecture
- **Resilience:** Outbound HTTP calls (Telegram API, external webhooks) must use `Polly` policies handling transient HTTP failures (408, 429, 5xx) with jittered exponential backoff respecting Telegram's `retry_after` response header.

### 2.2 Persistence & State Management

- **Primary Data Store:** Azure Cosmos Db.
- **Schema Evolution:** Schemas must remain backwards compatible. Entities use optimistic concurrency (`ETag`) where updates may collide.

### 2.3 Infrastructure as Code (IaC)

- **Tooling:** Terraform (v1.5+).
- **State & Secrets:** Sensitive values (Telegram Bot Token, Webhook Secret Tokens, Connection Strings) must reside in Azure Key Vault and be referenced via Key Vault References (`@Microsoft.KeyVault(...)`) or injected securely via CI/CD secrets.
- **Environment Isolation:** Parameterized variables allowing clean teardown and recreation of environments without manual Azure Portal intervention.

---

## 3. Communication & Integration Invariants

### 3.1 Telegram API Rules

- **Webhooks Only:** Production must use HTTPS webhooks (`SetWebhookAsync`) with strict validation of the `X-Telegram-Bot-Api-Secret-Token` header. Long-polling is restricted to local development environments only.
- **Anti-Spam & Rate Limiting:**
  - Adhere to Telegram's limits: $\le 1$ msg/sec per group, $\le 20$ msgs/min per group, $\le 30$ msgs/sec globally.
  - Group notification pings must be batched into single aggregated messages rather than individual per-user mentions.
  - Silent notifications (`disable_notification = true`) must be used for administrative acknowledgments and routine updates outside working hours (20:00 – 08:00).
- **Non-Anonymous Voting:** All tracked decision polls must explicitly enforce `is_anonymous = false` to enable voter identification.

### 3.2 Security & Authorization (RBAC)

- **Role Scoping:**
  - `Admin / Committee Member`: Can create funds, close polls, view complete financial rosters, and trigger manual reminders.
  - `Parent / Member`: Can submit payment proofs, answer polls, and query personal balance.
- **Command Guarding:** Every administrative command must pass through an authorization middleware/filter verifying the sender's Telegram User ID against the configured Committee Roster before processing.

---

## 4. Quality & Verification Standards

- **Testing:** Unit testing for domain logic (balance calculations, poll winner determination, fuzzy matching algorithms) using TUnit.
- **Deterministic Formatting:** Output messages to Telegram must use valid MarkdownV2 or HTML parsing modes with rigorous escaping helper functions to prevent parse breakages caused by user input.
- **Observability:** Application Insights telemetry enabled with structured logging (Serilog via default `ILogger`). No raw banking tokens or user credentials logged in plain text.
