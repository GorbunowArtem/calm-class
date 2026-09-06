# Workflow Contracts: GitHub Actions CI/CD Pipelines

**Feature**: `specs/002-azure-cicd-pulumi`  
**Date**: 2026-09-06  
**Status**: Completed  

---

## 1. Overview

This document specifies the schema, trigger events, job dependency graphs, OIDC permissions, and execution steps for the CalmClass GitHub Actions workflows.

---

## 2. Pull Request Pipeline Contract: `.github/workflows/pr-ci-cd.yml`

### 2.1 Trigger & Concurrency Contract

```yaml
name: PR CI/CD & Dev Deployment

on:
  pull_request:
    branches:
      - main
    paths:
      - "src/**"
      - "tests/**"
      - "infra/**"
      - "Directory.Build.props"
      - "Directory.Packages.props"
      - "CalmClass.slnx"
      - ".github/workflows/pr-ci-cd.yml"

concurrency:
  group: dev_environment
  cancel-in-progress: false

permissions:
  id-token: write   # Required for Azure OIDC Workload Identity
  contents: read    # Required to checkout repository
  pull-requests: write # Required for posting Pulumi preview comments
```

### 2.2 Job Dependency Graph

```text
[validate-and-test] (Lint audit, .NET build, unit tests)
        │
        ▼
[pulumi-preview] (Dry-run preview against dev stack)
        │
        ▼
[deploy-dev] (Package artifact, pulumi up dev, deploy Function, register webhook)
```

### 2.3 Job Specifications

#### Job 1: `validate-and-test`
- **Runs on**: `ubuntu-latest`
- **Steps**:
  1. `actions/checkout@v4`
  2. `actions/setup-python@v5` with Python 3.11+
  3. Run Clean Code audit: `python3 .agents/skills/clean-code-audit/scripts/audit.py`
  4. `actions/setup-dotnet@v4` with .NET 10 SDK
  5. Restore dependencies: `dotnet restore CalmClass.slnx`
  6. Compile solution: `dotnet build CalmClass.slnx --no-restore -c Release`
  7. Execute unit tests directly via `dotnet exec` (avoiding sandboxed IPC /tmp socket limits):
     - `dotnet exec tests/unit/CalmClass.ApplicationTests.Unit/bin/Release/net10.0/CalmClass.ApplicationTests.Unit.dll --output Detailed`
     - `dotnet exec tests/unit/CalmClass.InfrastructureTests.Unit/bin/Release/net10.0/CalmClass.InfrastructureTests.Unit.dll --output Detailed`

#### Job 2: `pulumi-preview`
- **Needs**: `validate-and-test`
- **Runs on**: `ubuntu-latest`
- **Steps**:
  1. `actions/checkout@v4`
  2. `azure/login@v2` with OIDC credentials (`client-id`, `tenant-id`, `subscription-id`)
  3. `pulumi/actions@v5` setup
  4. Pulumi Login to Azure Blob Storage backend: `pulumi login azblob://<container>`
  5. Run preview: `pulumi preview --stack dev --cwd infra/CalmClass.IaC`

#### Job 3: `deploy-dev`
- **Needs**: `[validate-and-test, pulumi-preview]`
- **Environment**: `dev`
- **Runs on**: `ubuntu-latest`
- **Steps**:
  1. `actions/checkout@v4`
  2. `actions/setup-dotnet@v4` with .NET 10 SDK
  3. Publish Functions package: `dotnet publish src/CalmClass.Functions/CalmClass.Functions.csproj -c Release -o ./publish/functions`
  4. Create deployment zip package: `zip -r functions.zip .` inside `./publish/functions`
  5. `azure/login@v2` with OIDC credentials
  6. `pulumi/actions@v5` login and reconcile: `pulumi up --stack dev --yes --cwd infra/CalmClass.IaC`
  7. Deploy artifact to Azure Function App: `az functionapp deployment source config-zip --resource-group rg-calmclass-dev --name func-calmclass-dev --src ./publish/functions/functions.zip`
  8. Register Telegram Webhook via post-deployment step:
     - Invoke Telegram API `https://api.telegram.org/bot<TOKEN>/setWebhook` with `url: https://func-calmclass-dev.azurewebsites.net/api/telegram/webhook`, `secret_token: <SECRET>`.

---

## 3. Production Release Pipeline Contract: `.github/workflows/prod-deploy.yml`

### 3.1 Trigger & Concurrency Contract

```yaml
name: Production Release

on:
  workflow_dispatch:

concurrency:
  group: prod_environment
  cancel-in-progress: false

permissions:
  id-token: write
  contents: read
```

### 3.2 Job Dependency Graph & Approval Gate

```text
[build-package] (Build & package release artifact from main)
        │
        ▼
[deploy-prod] (Environment: 'prod' with Required Reviewers gate)
        │
        ├── Manual Approval Granted?
        │   ├── YES ──► Pulumi Up (prod) ──► Deploy Function ──► Register Webhook
        │   └── NO  ──► Abort safely; prod remains on previous stable release
```

### 3.3 Job Specifications

#### Job 1: `build-package`
- **Runs on**: `ubuntu-latest`
- **Steps**:
  1. `actions/checkout@v4`
  2. `actions/setup-dotnet@v4` with .NET 10 SDK
  3. Compile & publish: `dotnet publish src/CalmClass.Functions/CalmClass.Functions.csproj -c Release -o ./publish/functions`
  4. Zip artifact: `zip -r functions-prod.zip .` in `./publish/functions`
  5. `actions/upload-artifact@v4` storing `functions-prod.zip`

#### Job 2: `deploy-prod`
- **Needs**: `build-package`
- **Environment**: `prod` *(Protected with Required Reviewers in GitHub repository settings)*
- **Runs on**: `ubuntu-latest`
- **Steps**:
  1. `actions/checkout@v4`
  2. `actions/download-artifact@v4` retrieving `functions-prod.zip`
  3. `azure/login@v2` with OIDC credentials configured in `prod` environment
  4. Pulumi Login to Azure Blob Storage backend: `pulumi login azblob://<container>`
  5. Apply infrastructure changes: `pulumi up --stack prod --yes --cwd infra/CalmClass.IaC`
  6. Deploy verified artifact: `az functionapp deployment source config-zip --resource-group rg-calmclass-prod --name func-calmclass-prod --src functions-prod.zip`
  7. Register Production Telegram Webhook:
     - Invoke Telegram API `setWebhook` with `url: https://func-calmclass-prod.azurewebsites.net/api/telegram/webhook`, `secret_token: <PROD_SECRET>`.

---

## 4. Environment Variables & Secret Inputs

| Variable / Secret Key | Scope | Description |
| :--- | :--- | :--- |
| `AZURE_CLIENT_ID` | Environment | Application (client) ID of Azure App Registration for OIDC. |
| `AZURE_TENANT_ID` | Environment | Azure Active Directory Tenant ID. |
| `AZURE_SUBSCRIPTION_ID` | Environment | Target Azure Subscription ID. |
| `AZURE_STORAGE_ACCOUNT` | Environment / Repository | Administrative storage account hosting the `pulumi-state` blob container. |
| `PULUMI_BACKEND_URL` | Environment / Repository | `azblob://pulumi-state?storage_account=<account-name>`. |
| `TELEGRAM_BOT_TOKEN` | Environment Secret | Bot token for the environment's Telegram bot (`dev` or `prod`). |
| `TELEGRAM_SECRET_TOKEN` | Environment Secret | Secret token header validated by `TelegramSecretTokenMiddleware`. |
