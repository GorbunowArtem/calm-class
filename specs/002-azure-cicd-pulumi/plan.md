# Implementation Plan: Azure CI/CD Pipeline & Pulumi Infrastructure as Code

**Branch**: `002-azure-cicd-pulumi` | **Date**: 2026-09-06 | **Spec**: [specs/002-azure-cicd-pulumi/spec.md](spec.md)

**Input**: Feature specification from `specs/002-azure-cicd-pulumi/spec.md`

---

## Summary

Establishes an automated, enterprise-grade continuous integration and continuous delivery (CI/CD) system and Infrastructure as Code (IaC) for CalmClass. The solution introduces a C# (.NET 10) Pulumi project utilizing the Azure Native provider to manage isolated `dev` and `prod` cloud environments, with state hosted in an Azure Blob Storage backend (`azblob://`). Pull requests execute automated Clean Code audits, .NET compilation, and TUnit test suites; upon passing, the pipeline publishes and deploys the artifact directly to the `dev` environment for live pre-merge cloud testing and registers the Telegram webhook. Deployments to `prod` occur from the default branch (`main`) and strictly require manual approval through GitHub Environment protection rules. GitHub Dependabot is configured to automatically scan and update both NuGet packages (application and Pulumi) and GitHub Actions.

---

## Technical Context

**Language/Version**: .NET 10 / C# latest major (all application code and Pulumi IaC authored in C#)  
**Primary Dependencies**:
- Pulumi (v3.x+, .NET SDK)
- Pulumi.AzureNative (v2.x+, Azure Native provider)
- GitHub Actions (v4/v5 runner actions)
- Azure Workload Identity Federation (OpenID Connect / `azure/login@v2`)

**Storage**:
- Azure Blob Storage (`azblob://pulumi-state` for self-hosted Pulumi state backend and concurrency locking)
- Azure Cosmos DB (Serverless NoSQL account, database `CalmClassDb`, container `Polls` with partition key `/chatId`)
- Azure Storage Account (Standard_LRS for Azure Functions host runtime, timer leases, and WebJobs)

**Testing**:
- Clean Code audit script (`python3 .agents/skills/clean-code-audit/scripts/audit.py`)
- Unit testing with TUnit via `dotnet exec` (avoiding sandboxed IPC `/tmp` pipe contention)
- Pulumi dry-run preview (`pulumi preview --stack dev`)

**Target Platform**: Microsoft Azure (Linux Consumption Function App, Serverless Cosmos DB, Key Vault, Log Analytics, Application Insights)  
**Project Type**: DevOps CI/CD Workflows & Infrastructure as Code  

**Performance Goals**:
- Pull request validation (build, audit, test, preview) completed in under 5 minutes
- End-to-end continuous deployment cycle to `dev` in under 15 minutes (SC-002)
- Zero downtime deployments and deterministic state synchronization

**Constraints**:
- Extreme Operational Frugality ($0 compute baseline on Consumption tier; self-hosted Azure Blob Storage state backend eliminating third-party SaaS costs)
- Strict environment segregation between `dev` and `prod` (independent databases, storage, keys)
- Zero plaintext secrets in source code or CI build logs; Key Vault references (`@Microsoft.KeyVault(...)`) exclusively
- Production deployment strictly halted behind manual authorization gate (SC-003)
- Unified package management via Central Package Management (`Directory.Packages.props`) and Dependabot

**Scale/Scope**:
- 2 isolated cloud environments: `dev` and `prod`
- 1 PR CI/CD workflow (`pr-ci-cd.yml`) and 1 production promotion workflow (`prod-deploy.yml`)
- 1 Dependabot configuration (`dependabot.yml`) tracking 2 ecosystems (`nuget` and `github-actions`)

---

## Constitution Check

*GATE: Evaluated before Phase 0 research and re-evaluated after Phase 1 design.*

| Principle / Rule | Requirement | Plan Compliance | Status |
| :--- | :--- | :--- | :---: |
| **1. Spec-First Engineering** | Dedicated spec & clarification before code | Spec completed and clarified (4/4 Q&A) in `spec.md` | **PASS** |
| **2. Living Spec Synchronization** | Synchronize spec with as-built reality | Plan and contracts strictly match all clarified requirements | **PASS** |
| **3. Extreme Operational Frugality** | Free/serverless tiers, zero persistent compute costs | Azure Functions Consumption (Linux Y1) + Cosmos DB Serverless + Azure Blob Storage state backend ($0 base cost) | **PASS** |
| **4. Privacy & Least Privilege** | Minimal PII, protected secrets, least privilege access | Azure Key Vault for all sensitive tokens; Azure OIDC passwordless authentication for GitHub Actions | **PASS** |
| **5. Idempotency & Resilience** | Idempotent operations, retry on transient errors | Declarative Pulumi state; retry policy on Telegram webhook registration | **PASS** |
| **6. C# Coding Standards** | Primary constructors, single type per file, .editorconfig | Pulumi C# code adheres to primary constructors, single type per file, and Roslyn using directives inside namespaces | **PASS** |
| **7. System.Text.Json Exclusively** | No Newtonsoft.Json | System.Text.Json used exclusively for serialization | **PASS** |
| **8. Testing Standards** | Automated tests pass without failure | Clean Code audit + unit tests executed via direct `dotnet exec` runner | **PASS** |

*Result: All gates pass with zero violations.*

---

## Project Structure

### Documentation (this feature)

```text
specs/002-azure-cicd-pulumi/
├── spec.md                  # Feature specification & clarifications
├── plan.md                  # Implementation plan (this file)
├── research.md              # Phase 0: Technical decisions & architecture
├── data-model.md            # Phase 1: Entity models, schemas & state machines
├── quickstart.md            # Phase 1: End-to-end verification & setup guide
├── contracts/               # Phase 1: Workflow, stack & Dependabot contracts
│   ├── github-workflows.md
│   ├── pulumi-stack-config.md
│   └── dependabot-config.md
└── checklists/
    └── requirements.md      # Quality checklist
```

### Source Code (repository root)

```text
.github/
├── dependabot.yml           # Automated weekly scans for NuGet and GitHub Actions
└── workflows/
    ├── pr-ci-cd.yml         # PR validation & pre-merge dev deployment pipeline
    └── prod-deploy.yml      # Production release pipeline with manual approval gate

infra/
└── CalmClass.IaC/
    ├── CalmClass.IaC.csproj # C# .NET 10 Pulumi project with Azure Native provider
    ├── Program.cs           # Entry point executing Deployment.RunAsync<CalmClassStack>()
    ├── CalmClassStack.cs    # Primary stack declaring Azure resource topology
    ├── StackOutputs.cs      # Positional record exporting stack URLs and endpoints
    ├── Pulumi.yaml          # Project definition and Azure Blob backend configuration
    ├── Pulumi.dev.yaml      # Development environment configuration values
    └── Pulumi.prod.yaml     # Production environment configuration values

Directory.Packages.props     # Central Package Management versions for Pulumi packages
CalmClass.slnx               # Solution definition registering /infra/CalmClass.IaC.csproj
```

**Structure Decision**: Infrastructure as Code is established in `infra/CalmClass.IaC/` as a C# .NET 10 project included in `CalmClass.slnx`. GitHub Actions workflows and Dependabot configuration reside in `.github/`. This ensures clean separation between application runtime code and cloud deployment assets while allowing unified builds, audits, and dependency tracking.

---

## Complexity Tracking

> **Zero violations**. No architectural deviations or unneeded complexity introduced.

| Violation | Why Needed | Simpler Alternative Rejected Because |
| :--- | :--- | :--- |
| *None* | N/A | Fully adheres to constitution and user constraints. |

---

## Phases & Deliverables

### Phase 0: Research & Architecture (Complete)
- Evaluated Pulumi C# Azure Native vs TypeScript / Terraform.
- Designed Azure Blob Storage backend (`azblob://`) for zero-cost self-hosted state.
- Designed Azure OIDC federated authentication.
- Inferred complete Azure topology from application code.
- Resolved all technical unknowns in [`research.md`](research.md).

### Phase 1: Design & Contracts (Complete)
- Established data models, pipeline entities, and state machines in [`data-model.md`](data-model.md).
- Authored GitHub Actions workflow contracts in [`contracts/github-workflows.md`](contracts/github-workflows.md).
- Authored Pulumi stack configuration contract in [`contracts/pulumi-stack-config.md`](contracts/pulumi-stack-config.md).
- Authored Dependabot configuration contract in [`contracts/dependabot-config.md`](contracts/dependabot-config.md).
- Created end-to-end verification and setup guide in [`quickstart.md`](quickstart.md).
- Re-evaluated and confirmed all Constitution Check gates pass.

### Phase 2: Tasks & Implementation Planning (Next Phase)
- Execute `/speckit-tasks` to decompose this plan into dependency-ordered, testable implementation tasks in `tasks.md`.
