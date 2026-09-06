# Quickstart & Verification Guide: Azure CI/CD Pipeline & Pulumi IaC

**Feature**: `specs/002-azure-cicd-pulumi`  
**Date**: 2026-09-06  
**Status**: Completed  

---

## 1. Overview

This quickstart guide provides step-by-step verification procedures to validate the CI/CD workflows, Pulumi Infrastructure as Code program, environment segregation, manual approval gates, and Dependabot configuration.

---

## 2. Prerequisites & Initial Cloud Setup

### 2.1 Developer Workstation Tools

Ensure the following CLI tools are installed:
```bash
dotnet --version    # Expected: 10.0.x
pulumi version      # Expected: v3.120.0+
az version          # Expected: 2.50.0+
python3 --version   # Expected: 3.10+
```

> 📖 **Full Azure Portal Runbook**: For a detailed, step-by-step portal walkthrough with screenshots and RBAC requirements, refer to [Azure Portal Setup Guide](../../docs/azure-setup-guide.md).

### 2.2 Administrative Azure Storage for Pulumi State Backend

Create the dedicated storage container for the `azblob://` state backend:
```bash
# Set subscription and create administrative storage account
az account set --subscription "<SUBSCRIPTION_ID>"
az group create --name "rg-calmclass-admin" --location "westeurope"
az storage account create --name "stcalmclassadmin" --resource-group "rg-calmclass-admin" --sku "Standard_LRS"
az storage container create --name "pulumi-state" --account-name "stcalmclassadmin"
```

### 2.3 Azure OIDC Workload Identity Federation for GitHub Actions

Configure Federated Credentials on an Azure Entra ID App Registration:
1. Create App Registration: `app-calmclass-cicd`.
2. Add Federated Credentials:
   - Organization: `GorbunowArtem` (ID: `15319213`)
   - Repository: `calm-class` (ID: `1357346431`)
   - Issuer: `https://token.actions.githubusercontent.com`
   - Credential 1 (PR Preview): Entity `Pull request` -> Subject `repo:GorbunowArtem@15319213/calm-class@1357346431:pull_request`
   - Credential 2 (Dev Deploy): Entity `Environment: dev` -> Subject `repo:GorbunowArtem@15319213/calm-class@1357346431:environment:dev`
   - Credential 3 (Prod Deploy): Entity `Environment: prod` -> Subject `repo:GorbunowArtem@15319213/calm-class@1357346431:environment:prod`
3. Assign `Contributor` and `User Access Administrator` (or `Role Based Access Control Administrator`) on target subscription.

### 2.4 GitHub Repository Secrets & Environments

1. In GitHub Repository Settings -> **Environments**:
   - Create `dev`: No approval required. Add Environment Secrets: `TELEGRAM_BOT_TOKEN`, `TELEGRAM_SECRET_TOKEN`.
   - Create `prod`: Enable **Required Reviewers** (select authorized maintainers). Add Environment Secrets: `TELEGRAM_BOT_TOKEN`, `TELEGRAM_SECRET_TOKEN`.
2. In GitHub Repository Settings -> **Secrets and variables -> Actions**:
   - Variables: `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, `AZURE_STORAGE_ACCOUNT: stcalmclassadmin`.

---

## 3. Verification Scenarios

### Scenario 1: Local Pulumi Compilation and Dry-Run Preview

Verify that the C# Pulumi project compiles and evaluates the Azure Native resource graph cleanly:

```bash
# 1. Login to the Azure Blob Storage backend
export AZURE_STORAGE_ACCOUNT="stcalmclassadmin"
pulumi login "azblob://pulumi-state"

# 2. Navigate to Pulumi project
cd infra/CalmClass.IaC

# 3. Build and execute non-destructive dry-run preview
pulumi preview --stack dev
```

**Expected Outcome**:
- Pulumi connects to `azblob://pulumi-state`.
- Compiles `CalmClass.IaC.csproj` using .NET 10.
- Outputs the plan to create 7 Azure resources: Resource Group, Storage Account, Cosmos DB Account/Database/Container, Key Vault, Application Insights/Workspace, App Service Plan, and Function App.

---

### Scenario 2: Pull Request Validation and Pre-Merge Dev Deployment

Validate that code changes opened in a pull request execute the complete CI pipeline and deploy to `dev`:

1. Create a feature branch and push a commit:
   ```bash
   git checkout -b test/verify-cicd-pipeline
   git commit --allow-empty -m "ci: test pull request verification pipeline"
   git push origin test/verify-cicd-pipeline
   ```
2. Open a Pull Request targeting `main` on GitHub.
3. Observe GitHub Actions executing `.github/workflows/pr-ci-cd.yml`:
   - Step 1 (`validate-and-test`): Clean Code audit succeeds, .NET compiles in Release mode, all unit tests pass.
   - Step 2 (`pulumi-preview`): Authenticates via Azure OIDC, generates Pulumi preview summary for `dev`.
   - Step 3 (`deploy-dev`): Builds Function zip artifact, runs `pulumi up --stack dev --yes`, deploys zip to `func-calmclass-dev`, and invokes Telegram API `setWebhook`.

**Expected Outcome**:
- All jobs succeed with green checkmarks.
- Azure Function in `dev` is updated and active.
- Telegram Webhook is verified via `curl https://api.telegram.org/bot<DEV_TOKEN>/getWebhookInfo`.

---

### Scenario 3: Production Release with Manual Approval Gate

Validate that production deployment strictly halts until manual approval is provided:

1. Navigate to GitHub Actions -> **Production Release** workflow (`.github/workflows/prod-deploy.yml`).
2. Click **Run workflow** targeting `main` (or execute `gh workflow run prod-deploy.yml`).
3. Observe GitHub Actions executing:
   - Job 1 (`build-package`): Packages `functions-prod.zip` and uploads release artifact.
   - Job 2 (`deploy-prod`): Halts in `Waiting for review` status on GitHub Environment `prod`.
4. In GitHub Actions UI:
   - Confirm that the production deployment DOES NOT proceed without review.
   - Click **Review deployments**, select `prod`, and click **Approve and deploy**.
5. Observe the resumed workflow:
   - `azure/login@v2` authenticates with `prod` OIDC credentials.
   - `pulumi up --stack prod --yes` reconciles production infrastructure.
   - Deploys `functions-prod.zip` to `func-calmclass-prod`.
   - Registers production webhook with Telegram.

**Expected Outcome**:
- Production workflow runs exclusively when manually dispatched.
- Production deployment occurs exclusively after human approval.
- An auditable approval event is stamped in GitHub deployment history.
- Production environment is live and verified.

---

### Scenario 4: Dependabot Configuration Verification

Verify that Dependabot configuration is syntactically valid and active:

1. Check Dependabot status on GitHub under **Insights -> Dependency graph -> Dependabot**.
2. Trigger an immediate manual check or inspect logs for `nuget` and `github-actions`.

**Expected Outcome**:
- Both `nuget` and `github-actions` ecosystems are recognized with zero syntax errors.
- Dependabot monitors `Directory.Packages.props` for NuGet updates and `.github/workflows/` for action updates.
