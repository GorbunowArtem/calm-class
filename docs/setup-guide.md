# Local Development Setup Guide & Runbook

This guide provides step-by-step instructions to set up, configure, run, and test **CalmClass** locally on macOS, Linux, or Windows.

---

## 1. Prerequisites

Ensure you have the following developer tools installed:

- **[.NET 10 SDK](https://dotnet.microsoft.com/download)**:
  Verify with:
  ```bash
  dotnet --version # Should be 10.0.x or higher
  ```
- **[Azure Functions Core Tools v4](https://learn.microsoft.com/azure/azure-functions/functions-run-local)**:
  Verify with:
  ```bash
  func --version # Should be 4.x
  ```
- **[Docker Desktop](https://www.docker.com/)** or Docker Engine *(optional if using In-Memory mode)*:
  Used for local Azurite (storage emulator) and Azure Cosmos DB Emulator.
- **[ngrok](https://ngrok.com/)** *(optional, only for live Telegram webhooks)*:
  Exposes your local port `7071` to Telegram servers.

---

## 2. Configuration & `local.settings.json`

The Azure Functions host reads configuration from `src/CalmClass.Functions/local.settings.json`.

Copy the provided example file:
```bash
cp src/CalmClass.Functions/local.settings.example.json src/CalmClass.Functions/local.settings.json
```

### Configuration Keys

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "Telegram__BotToken": "YOUR_TELEGRAM_BOT_TOKEN_HERE",
    "Telegram__SecretToken": "YOUR_RANDOM_SECRET_TOKEN_HERE",
    "Telegram__BaseUrl": "https://api.telegram.org",
    "CosmosDb__ConnectionString": "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==",
    "CosmosDb__DatabaseName": "CalmClassDb",
    "CosmosDb__ContainerName": "Polls",
    "CosmosDb__UseInMemory": "true",
    "QuietHours__StartHour": "20",
    "QuietHours__EndHour": "8",
    "QuietHours__TimeZoneId": "Europe/Kyiv",
    "Poll__DefaultDurationHours": "24",
    "Poll__ReminderHoursBeforeExpiry": "6"
  }
}
```

| Key | Description | Recommended Local Value |
| :--- | :--- | :--- |
| `AzureWebJobsStorage` | Connection string for Azure WebJobs storage (Azurite) | `UseDevelopmentStorage=true` |
| `FUNCTIONS_WORKER_RUNTIME` | Functions isolated worker runtime | `dotnet-isolated` |
| `Telegram__BotToken` | Telegram Bot API token from [@BotFather](https://t.me/BotFather) | Your real token (or mock string) |
| `Telegram__SecretToken` | Secret token verified via `X-Telegram-Bot-Api-Secret-Token` | Any alphanumeric string |
| `Telegram__BaseUrl` | Telegram Bot API base URL | `https://api.telegram.org` |
| `CosmosDb__UseInMemory` | Enables frictionless in-memory repository | `"true"` for local offline dev |
| `CosmosDb__ConnectionString` | Connection string to Azure Cosmos DB or local emulator | Emulator connection string |
| `CosmosDb__DatabaseName` | Cosmos DB database name | `CalmClassDb` |
| `CosmosDb__ContainerName` | Cosmos DB container name for polls, votes, and roster | `Polls` |
| `QuietHours__StartHour` | Hour when quiet hours begin (24h clock) | `20` (8:00 PM) |
| `QuietHours__EndHour` | Hour when quiet hours end (24h clock) | `8` (8:00 AM) |
| `QuietHours__TimeZoneId` | IANA / Windows Timezone ID | `Europe/Kyiv` |
| `Poll__DefaultDurationHours` | Default lifespan of a decision poll | `24` |
| `Poll__ReminderHoursBeforeExpiry`| Window before poll expiry when reminder is dispatched | `6` |

---

## 3. Storage Options: In-Memory vs. Cosmos DB Emulator

CalmClass supports two local development storage strategies:

### Option A: Zero-Dependency In-Memory Store (Fastest & Recommended)
Set `"CosmosDb__UseInMemory": "true"` in `local.settings.json`.
- Uses [`InMemoryPollRepository`](../src/CalmClass.Infrastructure/Persistence/InMemoryPollRepository.cs) registered as a `Singleton` in DI.
- No Docker or Cosmos DB installation required.
- State persists across webhook requests for the lifetime of the running Functions host.
- Includes automatic administrator bootstrapping: the first user to run an administrative command in a chat is automatically enrolled as `Admin`.

### Option B: Local Emulators via Docker Compose
Start Azurite and Cosmos DB Emulator:
```bash
# Start Azurite (for AzureWebJobs storage / TimerTrigger)
docker compose up azurite -d

# Start Azure Cosmos DB Linux Emulator (optional)
docker compose --profile cosmos up -d
```

---

## 4. Running the Functions Host

### Automated One-Command Runner
```bash
./scripts/run-local.sh
```
This script checks prerequisites, builds the solution, ensures `local.settings.json` exists, and starts `func start` directly from the compiled output directory.

### Manual Launch
```bash
# 1. Build Functions host
dotnet build -m:1 src/CalmClass.Functions/CalmClass.Functions.csproj

# 2. Navigate to build output and start host
cd src/CalmClass.Functions/bin/Debug/net10.0
func start
```

The host will start on `http://localhost:7071`:
- `POST http://localhost:7071/api/telegram/webhook`
- `TimerTrigger: PollMonitorFunction` (scheduled every 5 minutes)

---

## 5. Local Testing & Webhook Simulation

### Option 1: Webhook Simulator Script (No Internet / Telegram required)
Use [`scripts/simulate-webhook.sh`](../scripts/simulate-webhook.sh) to send simulated webhook payloads to `http://localhost:7071`:

```bash
# Create a new poll
./scripts/simulate-webhook.sh create

# Cast a vote for Option 0 (as user 200)
./scripts/simulate-webhook.sh vote 0

# Cast a vote for Option 1 (as user 300)
./scripts/simulate-webhook.sh vote 1 300 "Марія Шевченко"

# Retract a vote
./scripts/simulate-webhook.sh retract

# Manually close active poll and view aggregated Ukrainian tally
./scripts/simulate-webhook.sh close

# Cancel active poll
./scripts/simulate-webhook.sh cancel
```

### Option 2: Live Telegram Webhook Tunneling via ngrok
To test live in a real Telegram group with [@calm_class_bot](https://t.me/calm_class_bot):

1. Launch ngrok:
   ```bash
   ngrok http 7071
   ```
2. In a separate terminal, register the webhook:
   ```bash
   ./scripts/register-webhook.sh
   ```
   *(The script automatically queries the local ngrok API to find the active public URL and sets the webhook with Telegram).*
3. Verify webhook diagnostic information:
   ```bash
   ./scripts/register-webhook.sh --info
   ```
4. When finished, unregister the webhook:
   ```bash
   ./scripts/register-webhook.sh --delete
   ```

---

## 6. Running Tests & Code Quality Verification

### Unit Tests
Execute the unit test suite (using TUnit and Microsoft Testing Platform):
```bash
# Set environment variables if needed
export DOTNET_CLI_HOME=$PWD/.dotnet
export NUGET_PACKAGES=~/.nuget/packages
export DOTNET_SKIP_WORKLOAD_INTEGRITY_CHECK=true

# Build solution
dotnet build -m:1 --no-restore

# Run Application tests (35 tests)
dotnet exec tests/unit/CalmClass.ApplicationTests.Unit/bin/Debug/net10.0/CalmClass.ApplicationTests.Unit.dll --output Detailed

# Run Infrastructure tests (15 tests)
dotnet exec tests/unit/CalmClass.InfrastructureTests.Unit/bin/Debug/net10.0/CalmClass.InfrastructureTests.Unit.dll --output Detailed
```

### Architecture & Clean Code Audit
Run the automated static code audit:
```bash
python3 .agents/skills/clean-code-audit/scripts/audit.py
```
This validates:
- Exactly 1 type per `.cs` file matching the filename.
- `using` directives placed inside namespaces and sorted `System` first.
- Zero consecutive blank lines (`.editorconfig` compliance).
- No raw regex positional indexing.
- Zero references to `Newtonsoft.Json` (`System.Text.Json` only).

---

## 7. Cloud Deployment & Azure Onboarding

Once your changes are verified locally, consult the **[Azure Portal Setup & Cloud Provisioning Guide](azure-setup-guide.md)** to:
- Configure Azure Subscription and Entra ID App Registrations for GitHub Actions OIDC Workload Identity.
- Provision administrative storage for self-hosted Pulumi state (`azblob://pulumi-state`).
- Set up GitHub Environments (`dev` and `prod`) with required secrets and manual approval protection rules.
- Test automated PR deployments to `dev` and gated manual releases to `prod`.
