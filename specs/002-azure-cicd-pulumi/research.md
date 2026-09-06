# Technical Research & Architecture Decisions: Azure CI/CD Pipeline & Pulumi IaC

**Feature**: `specs/002-azure-cicd-pulumi`  
**Date**: 2026-09-06  
**Status**: Completed  

---

## 1. Overview & Research Scope

This document captures the technical evaluations, architecture decisions, and operational trade-offs for establishing an end-to-end continuous integration and deployment (CI/CD) system and Infrastructure as Code (IaC) foundation for CalmClass.

The system encompasses:
1. Automated CI quality checks on pull requests (Clean Code audit, .NET 10 compilation, TUnit unit test execution).
2. Infrastructure as Code using Pulumi in C# with the Azure Native provider.
3. Self-contained state storage in Azure Blob Storage without third-party SaaS dependencies.
4. Pre-merge continuous deployment to the development (`dev`) environment upon passing PR checks.
5. Manual-gated production (`prod`) deployment from the default branch (`main`).
6. Automated post-deployment Telegram webhook configuration.
7. Automated dependency scanning and pull requests via GitHub Dependabot.

---

## 2. Architectural Decisions

### 2.1 Infrastructure as Code Engine & Language

- **Decision**: Author the Infrastructure as Code project in **C# (.NET 10)** using the **Pulumi Azure Native provider (`Pulumi.AzureNative`)**.
- **Rationale**:
  - The CalmClass backend is built entirely in C# (.NET 10). A C# Pulumi project keeps the codebase unified in a single programming language and runtime.
  - Pulumi dependencies are standard NuGet packages (`Pulumi`, `Pulumi.AzureNative`). They integrate seamlessly with Central Package Management (`Directory.Packages.props`) and allow GitHub Dependabot to track both application and IaC dependencies in a single ecosystem.
  - Strong typing in C# eliminates syntax errors in Azure resource definitions at compile time.
  - Azure Native communicates directly with the Azure Resource Manager (ARM) API, providing zero-day support for all Azure resource types and features without wrapping Terraform.
- **Alternatives Considered**:
  - *Pulumi with TypeScript / Node.js*: Standard in some DevOps teams, but would introduce Node.js, `package.json`, `npm`, and duplicate toolchains into an otherwise pure .NET solution.
  - *Terraform / OpenTofu*: Mentioned as legacy tooling in constitution Section 2.3, but explicitly superseded by user requirement for Pulumi.
  - *Azure Bicep / ARM Templates*: Azure-proprietary DSL, lacks general-purpose language capabilities and cannot be managed via NuGet.

---

### 2.2 Pulumi State Backend & Concurrency Locking

- **Decision**: Use an **Azure Blob Storage backend (`azblob://<container-name>`)** hosted in an administrative Azure Storage account for storing Pulumi stack state and concurrency locks.
- **Rationale**:
  - Aligns with the platform constitution principle of **Extreme Operational Frugality**: zero monthly subscription costs for external SaaS platforms.
  - All infrastructure data, secrets metadata, and state files remain strictly self-contained within the organization's Azure tenant, complying with data governance and privacy rules.
  - Pulumi provides native support for `azblob://` with built-in blob lease-based distributed locking to prevent concurrent state corruption.
- **Alternatives Considered**:
  - *Pulumi Cloud (Managed SaaS)*: Excellent developer experience and UI, but requires external account registration, token management (`PULUMI_ACCESS_TOKEN`), and introduces third-party dependency.
  - *Local Filesystem State*: Ephemeral in GitHub Actions runners; not viable for distributed multi-job CI/CD.

---

### 2.3 Continuous Integration & Pre-Merge Cloud Verification Flow

- **Decision**: Trigger the PR CI/CD workflow (`pr-ci-cd.yml`) on `pull_request` targeting `main`. When all verification jobs (Clean Code audit, .NET build, unit tests, Pulumi preview) pass, the pipeline automatically compiles the release artifact, provisions/updates the `dev` stack via `pulumi up`, deploys the artifact to the `dev` Function App, and registers the `dev` Telegram webhook.
- **Rationale**:
  - Directly satisfies the user requirement: *"If the pull request passed, the final artefact must be published to Azure."*
  - Pre-merge cloud deployment ensures changes are tested and verified against real cloud dependencies (Cosmos DB, Key Vault, Telegram Webhook) before being merged into `main`.
  - Eliminates "works on my machine" issues prior to code review sign-off.
- **Alternatives Considered**:
  - *Deploy to dev only upon merge to main*: Standard GitFlow, but misses the explicit requirement to publish passing PR artifacts to Azure for live pre-merge testing.
  - *Ephemeral per-PR environments*: Creates a full set of Azure resources per PR branch. Rejected due to cost, provisioning latency, and quota exhaustion on free/consumption tiers.

---

### 2.4 Production Release & Manual Approval Gate

- **Decision**: Trigger the production promotion workflow (`prod-deploy.yml`) on `push` to `main` (when a PR is merged) or via `workflow_dispatch`. The deployment job is bound to a GitHub Environment named `prod` configured with **Required Reviewers**.
- **Rationale**:
  - Satisfies the user requirement: *"All deployments to `prod` must be happening only after the manual approval."*
  - GitHub Environments provides tamper-proof, auditable human gates where designated approvers must review the deployment summary and approve before execution resumes.
  - Keeps the production release deterministic: the exact code merged into `main` is packaged, `prod` infrastructure is reconciled via Pulumi, and deployed with zero manual portal clicks.
- **Alternatives Considered**:
  - *Issue-based or PR comment-based ChatOps approval*: Fragile, requires custom GitHub bot permissions, and lacks the audit trail built into GitHub Environments.
  - *Manual Azure Portal deployment*: Violates Infrastructure as Code standards and creates configuration drift.

---

### 2.5 Authentication & Security: OpenID Connect (OIDC)

- **Decision**: Authenticate GitHub Actions runners to Microsoft Azure using **OpenID Connect (OIDC)** and Azure Workload Identity Federation (`azure/login@v2`).
- **Rationale**:
  - Passwordless authentication: no permanent Azure Service Principal client secrets (`client_secret`) stored in GitHub repository secrets.
  - Ephemeral short-lived access tokens minted by Azure Active Directory (Entra ID) matching specific GitHub repository and branch/environment claims.
  - Complies with Least Privilege and eliminates secret rotation overhead.
- **Alternatives Considered**:
  - *Static Azure Service Principal credentials (`AZURE_CREDENTIALS` JSON)*: Poses credential leakage risk if secrets are exposed in logs or compromised; requires annual certificate/secret rotation.

---

### 2.6 Inferred Azure Resources Topology

Based on `CalmClass.Functions`, `CalmClass.Infrastructure`, and `src/CalmClass.Application/Common/Options/`:

| Inferred Azure Resource | SKU / Tier | Purpose in CalmClass |
| :--- | :--- | :--- |
| **Resource Group** | N/A | Resource lifecycle grouping (`rg-calmclass-dev`, `rg-calmclass-prod`). |
| **Azure Storage Account** | Standard_LRS (StorageV2) | Required for Azure Functions runtime host, Timer Trigger lease blob containers, and WebJobs. |
| **Azure Cosmos DB Account** | Serverless (NoSQL) | Operational database (`CalmClassDb`) and container (`Polls`, partition key `/chatId`) with zero idle cost. |
| **Azure Key Vault** | Standard | Houses secrets (`Telegram--BotToken`, `Telegram--SecretToken`, `CosmosDb--ConnectionString`, `ApplicationInsights--ConnectionString`). |
| **Log Analytics & App Insights** | PerGB2018 / Workspace-based | Captures structured Serilog logs, telemetry, exception traces, and execution performance. |
| **App Service Plan** | Linux Consumption (Y1) | Serverless hosting tier with dynamic auto-scaling and $0 base cost. |
| **Azure Function App** | Linux, .NET 10 Isolated | Hosts `TelegramWebhookFunction` (HTTP trigger) and `PollMonitorFunction` (Timer trigger: `0 */5 * * * *`). Configured with System-Assigned Managed Identity. |

---

### 2.7 Automated Telegram Webhook Registration

- **Decision**: Execute an automated post-deployment step in the workflow that calls the Telegram Bot API `setWebhook` method with the deployed Function App's HTTPS URL (`https://<app-name>.azurewebsites.net/api/telegram/webhook`) and the environment's `secret_token`.
- **Rationale**:
  - Eliminates manual execution of `register-webhook.sh` after deployment.
  - Ensures the environment is 100% operational immediately upon pipeline completion.
  - Step includes exponential retry (up to 3 attempts) with jitter to handle transient Telegram API rate limits.
- **Alternatives Considered**:
  - *Manual webhook script execution*: Error-prone; leads to forgotten webhook updates when Function URLs change.

---

### 2.8 Dependabot Configuration

- **Decision**: Configure `.github/dependabot.yml` to track:
  1. `nuget`: Scans root (`/`) where `Directory.Packages.props` and solution files reside, checking for updates weekly.
  2. `github-actions`: Scans `.github/workflows/`, checking for updated action versions weekly.
- **Rationale**:
  - Satisfies user requirement: *"dependatabot must be present in the solution to keep NuGet and pulumi dependencies up to date."*
  - Because Pulumi is written in C#, its packages (`Pulumi`, `Pulumi.AzureNative`) are tracked directly under the `nuget` ecosystem in Central Package Management (`Directory.Packages.props`).
- **Alternatives Considered**:
  - *Renovate Bot*: More customizable, but requires third-party GitHub App installation; Dependabot is natively built into GitHub with zero external setup.

---

### 2.9 Concurrency & State Locking Strategy

- **Decision**: Apply GitHub Actions workflow concurrency groups:
  - For `dev` pre-merge deployments: `concurrency: group: dev_environment, cancel-in-progress: false`.
  - For `prod` deployments: `concurrency: group: prod_environment, cancel-in-progress: false`.
- **Rationale**:
  - Multiple PRs or commits passing concurrently must not execute `pulumi up` or overwrite `dev` simultaneously.
  - Setting `cancel-in-progress: false` ensures in-flight deployments run to completion cleanly without leaving half-provisioned resources or dangling state locks.

---

## 3. Summary of Resolved Clarifications

| Item | Decision | Rationale |
| :--- | :--- | :--- |
| **Pulumi State Storage** | Azure Blob Storage (`azblob://`) | Self-hosted, $0 cost, zero SaaS dependencies. |
| **Pulumi Language** | C# (.NET 10) | Unifies solution, central NuGet package management. |
| **Telegram Webhook** | Automated post-deploy step | Hands-off continuous deployment. |
| **Dev Deploy Trigger** | Passing PR checks (pre-merge) | User requirement: publish passing PR artifact to Azure. |
| **Prod Deploy Trigger** | Merge to `main` + manual approval | Safe release management via GitHub Environments. |
