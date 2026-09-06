# Feature Specification: Azure CI/CD Pipeline & Pulumi Infrastructure as Code

**Feature Branch**: `002-azure-cicd-pulumi`

**Created**: 2026-09-06

**Status**: Draft

**Input**: User description: "add a CI/CD pipeline to build, run tests and deploy the application to Azure. Use Pulumi for infrastructure as code. The application must have two environments: `dev` and `prod`. dependatabot must be present in the solution to keep NuGet and pulumi dependencies up to date. You can infer other required Azure resources from the service itself. If the pull request passed, the final artefact must be published to Azure. All deployments to `prod` must be happening only after the manual approval"

## Clarifications

### Session 2026-09-06
- Q: Where should Pulumi store its infrastructure state and concurrency locks during CI/CD pipeline runs? → A: Azure Blob Storage backend (`azblob://` container in an Azure Storage Account) with zero external SaaS dependencies.
- Q: Which programming language should be used for the Pulumi Infrastructure as Code project? → A: C# (.NET 10) using the Pulumi .NET SDK and Azure Native provider, keeping the solution unified under NuGet package management.
- Q: Should the CI/CD pipeline automatically register the Telegram webhook URL with Telegram upon successful deployment to an environment? → A: Automatically register/update the webhook with Telegram as a post-deployment pipeline step using the environment's configured bot token and secret token.
- Q: At what point should the final artifact be published and deployed to Azure: upon pull request merge to main, or directly from the pull request branch as soon as checks pass? → A: Deploy to Azure dev directly from the PR branch as soon as all checks pass (pre-merge cloud verification).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Automated Pull Request Validation & Quality Gate (Priority: P1)

As a software engineer submitting a code contribution, I want the automated continuous integration system to compile the solution, run clean-code audits, execute all unit tests, and perform a non-destructive infrastructure preview against proposed changes, so that defective code or invalid infrastructure definitions are prevented from entering the default branch.

**Why this priority**: Fast and reliable automated verification is the cornerstone of code quality and team velocity. Without a reliable PR validation gate, defective changes or breaking schema modifications could break shared environments.

**Independent Test**: Can be tested independently by opening a pull request with new code or intentionally introduced test/build failures. The CI pipeline executes automatically, verifies the code build and test suites, runs a dry-run infrastructure preview, and reports pass/fail status directly on the pull request.

**Acceptance Scenarios**:

1. **Given** a pull request targeting the `main` branch with valid code and passing tests, **When** the pull request validation workflow executes, **Then** all build, audit, and test jobs complete successfully, an infrastructure preview is generated, and the pull request status check is marked as passed.
2. **Given** a pull request containing failing unit tests or compile errors, **When** the workflow executes, **Then** the workflow fails at the test or build step, detailed error logs are attached, and merge is blocked.
3. **Given** a pull request with infrastructure modifications, **When** the workflow executes, **Then** a preview of planned infrastructure additions, modifications, or deletions is rendered in the workflow output without modifying actual cloud resources.

---

### User Story 2 - Pre-Merge Continuous Deployment to Development Environment (Priority: P1)

As a developer and stakeholder, I want a pull request that successfully passes all builds and tests to automatically compile the final release package, reconcile the cloud infrastructure, and deploy the application to the `dev` environment, so that new functionality can be verified live in the cloud before merging the pull request.

**Why this priority**: Pre-merge deployment to the development environment provides immediate live validation in Azure, ensuring that pull requests are battle-tested in the real cloud environment prior to merging into `main`.

**Independent Test**: Can be tested independently by pushing a commit to a pull request. Once validation checks pass, the pipeline automatically compiles the release artifact, applies infrastructure changes to the `dev` stack via Pulumi, deploys the artifact to the `dev` Azure Function App, registers the `dev` Telegram webhook, and confirms the deployment is healthy.

**Acceptance Scenarios**:

1. **Given** a pull request with passing validation checks, **When** the continuous delivery stage triggers, **Then** the final application artifact is compiled, packaged, and published as a versioned build artifact.
2. **Given** the packaged artifact, **When** the `dev` deployment stage runs, **Then** Pulumi applies any pending infrastructure changes to the `dev` stack and deploys the artifact to the `dev` Azure Function App.
3. **Given** a successful deployment of infrastructure and code to `dev`, **When** the post-deployment step runs, **Then** the pipeline automatically registers the `dev` Function App webhook endpoint (`/api/telegram/webhook`) with the Telegram Bot API using the `dev` bot token and secret token.
4. **Given** completed deployment and webhook registration, **When** the `dev` stage finishes, **Then** deployment status and release metadata are recorded, confirming the development environment is live and operational for PR review.

---

### User Story 3 - Gated Production Deployment with Manual Approval (Priority: P1)

As a release manager or system owner, I want deployments to the `prod` environment to occur from approved and merged code on `main`, halting at an explicit authorization checkpoint that requires manual approval by an authorized reviewer before applying infrastructure changes and deploying the artifact, so that production stability and user trust are safeguarded against unintended releases.

**Why this priority**: Production workloads serve active classroom chats and handle sensitive member data. Manual approval ensures deliberate human oversight and operational readiness before any change impacts live users.

**Independent Test**: Can be tested independently by merging code to `main` and triggering the production deployment workflow. The pipeline halts execution before the `prod` stage, notifying designated approvers. The `prod` deployment proceeds only after approval is granted, or terminates safely if rejected.

**Acceptance Scenarios**:

1. **Given** approved changes merged into `main`, **When** the production deployment workflow executes, **Then** the pipeline halts and waits for explicit manual authorization via the production environment gate.
2. **Given** an authorized reviewer grants approval on the production gate, **When** the pipeline resumes, **Then** Pulumi executes infrastructure reconciliation against the `prod` stack, and the verified artifact is deployed to the production Azure Function App.
3. **Given** a successful deployment of infrastructure and code to `prod`, **When** the post-deployment step runs, **Then** the pipeline automatically registers the `prod` Function App webhook endpoint (`/api/telegram/webhook`) with the Telegram Bot API using the `prod` bot token and secret token.
4. **Given** a reviewer rejects the promotion or the approval window expires, **When** the gate decision is recorded, **Then** the production stage is aborted and the existing production environment remains untouched.

---

### User Story 4 - Reproducible Cloud Infrastructure via Pulumi (Priority: P2)

As a platform engineer, I want all required cloud infrastructure resources (compute, database, storage, secret vault, and telemetry) to be declared as code using Pulumi with separate configuration stacks for `dev` and `prod`, so that environments can be audited, reproduced, and torn down deterministically without manual portal interventions.

**Why this priority**: Infrastructure as Code eliminates configuration drift, guarantees environment parity between `dev` and `prod`, and enables reliable disaster recovery and local simulation.

**Independent Test**: Can be tested independently by running Pulumi deployment commands with stack parameters for `dev` and `prod`. The complete topology of Azure resources is stood up matching application specifications.

**Acceptance Scenarios**:

1. **Given** a fresh Azure subscription and credentials, **When** the Pulumi program runs with the `dev` stack configuration, **Then** all supporting Azure resources (Resource Group, Storage Account, Cosmos DB Account/Database/Container, Key Vault, Application Insights/Log Analytics, and Function App) are provisioned with `dev` naming conventions and settings.
2. **Given** the Pulumi program runs with the `prod` stack configuration, **When** execution completes, **Then** an identical, isolated topology is provisioned with `prod` naming conventions, separate secrets, and independent data persistence.
3. **Given** existing infrastructure, **When** Pulumi detects no configuration changes, **Then** it performs zero unnecessary resource modifications, ensuring idempotent updates.

---

### User Story 5 - Automated Dependency Health & Updates with Dependabot (Priority: P2)

As a repository maintainer, I want Dependabot configured within the solution to regularly check for outdated or vulnerable NuGet and Pulumi dependencies and automatically submit pull requests, so that security patches and library upgrades are continuously integrated with minimal manual toil.

**Why this priority**: Dependency rot and security vulnerabilities introduce severe operational and compliance risks. Automated dependency scanning keeps the platform secure and aligned with the latest framework capabilities.

**Independent Test**: Can be tested independently by inspecting `.github/dependabot.yml` and triggering Dependabot update checks. Dependabot scans project files and packages props, opening targeted pull requests when newer versions are available.

**Acceptance Scenarios**:

1. **Given** the repository configuration, **When** Dependabot executes its scheduled scan, **Then** it inspects all NuGet packages (covering both application libraries and Pulumi packages) and GitHub Actions dependencies.
2. **Given** a newer version of a package is discovered, **When** Dependabot processes the update, **Then** it creates a dedicated pull request with release notes and version diffs, triggering the standard PR validation pipeline.

---

### Edge Cases

- **Deployment Failure in Development Stage**: If artifact packaging or `dev` infrastructure deployment encounters an error, the deployment pipeline immediately fails and aborts, preventing any promotion prompt or manual approval request for `prod`.
- **Rejected or Expired Production Approval**: If a reviewer rejects the production gate or the approval timeout is exceeded, the production deployment job cancels gracefully. The running `prod` environment continues serving traffic on the previous stable release.
- **Missing or Misconfigured Secret in Key Vault**: If an environment is missing a mandatory configuration secret (e.g. Telegram Bot Token), pre-deployment verification or the Function App startup health check identifies the missing configuration, and the deployment logs report the specific missing key without exposing other secret values.
- **Concurrent Pull Request Deployments to Dev**: Because passing PR branches deploy directly to `dev` for pre-merge verification, GitHub Actions concurrency groups (`concurrency: dev_environment, cancel-in-progress: false`) ensure PR deployments to `dev` run sequentially to avoid state collisions in Pulumi and overwriting active deployments mid-test.
- **Rollback to Known Good Version**: If an issue arises in production after deployment, operators can trigger a targeted redeployment of a previous successful release artifact and its associated Pulumi stack state without rebuilding from scratch.
- **Transient Cloud API Rate Limits or Network Glitches**: Pipeline steps interacting with Azure or Pulumi state backends utilize retry policies with exponential backoff to handle transient network interruptions.
- **Transient Telegram API Outage during Webhook Registration**: If the Telegram Bot API is temporarily unreachable during the post-deployment webhook registration step, the step retries with exponential backoff and provides clear error diagnostics if registration fails without corrupting the deployed application state.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST implement an automated continuous integration pipeline triggered on all pull requests targeting the default branch (`main`).
- **FR-002**: Pull request validation MUST execute solution compilation, verify coding standards/audits, and run all unit tests across the solution.
- **FR-003**: Pull request validation MUST execute a dry-run infrastructure preview (`pulumi preview`) against the target stack, publishing the anticipated resource changes in the job summary.
- **FR-004**: The system MUST enforce that pull requests cannot be merged unless all validation checks (build, test, audit, preview) succeed.
- **FR-005**: When all validation checks (build, test, audit, preview) on a pull request branch succeed, the continuous integration pipeline MUST compile, optimize, and package the application into a deployable release artifact.
- **FR-006**: The system MUST automatically publish and deploy the release artifact and reconcile infrastructure state for the development (`dev`) environment directly from the passing pull request branch, enabling live cloud verification prior to merge.
- **FR-007**: The system MUST provide two strictly isolated environments: `dev` and `prod`, maintaining independent resource groups, storage accounts, databases, key vaults, and compute instances.
- **FR-008**: Deployments targeting the production (`prod`) environment MUST occur only from approved code on the default branch (`main`) and require explicit manual approval from designated authorized reviewers via environment protection gates before any infrastructure changes or artifact deployments occur.
- **FR-009**: All cloud infrastructure resources MUST be declared and managed as code using Pulumi in C# (.NET 10) with the Azure Native provider, storing deployment state and concurrency locks in an Azure Blob Storage backend (`azblob://`) without third-party SaaS dependencies.
- **FR-010**: The Pulumi infrastructure code MUST provision all required Azure resources inferred from the application:
  - Resource Groups for environment segregation (`dev` and `prod`).
  - Azure Storage Account providing blob, table, and queue endpoints required for Azure Functions runtime host execution and timer trigger lease management.
  - Azure Cosmos DB Account running in Serverless capacity mode, containing database `CalmClassDb` and container `Polls` with partition key `/chatId`.
  - Azure Key Vault storing sensitive secrets (Telegram Bot Token, Telegram Secret Token, Cosmos DB Connection String, Application Insights Connection String).
  - Azure Application Insights and Log Analytics Workspace configured for structured telemetry and operational diagnostics.
  - Linux Consumption App Service Plan and Azure Function App configured for .NET 10 Isolated Worker runtime with system-assigned Managed Identity and Key Vault access permissions.
- **FR-011**: The system MUST inject sensitive credentials and API tokens exclusively through secure environment secrets and Azure Key Vault references (`@Microsoft.KeyVault(...)`), prohibiting plaintext credentials in source control or build logs.
- **FR-012**: The deployment pipeline MUST authenticate to Microsoft Azure using secure OpenID Connect (OIDC / Federated Credentials), eliminating the need for long-lived stored passwords.
- **FR-013**: The repository MUST include a Dependabot configuration (`.github/dependabot.yml`) configured with the `nuget` package ecosystem to track and update both application and Pulumi C# package dependencies, alongside `github-actions` for workflow actions.
- **FR-014**: The pipeline MUST record and retain build artifacts, deployment logs, and commit metadata for auditing and rollback capabilities.
- **FR-015**: The deployment pipeline MUST automatically register or update the Telegram webhook endpoint (`/api/telegram/webhook`) with the Telegram Bot API upon successful deployment to an environment, configuring the environment's specific bot token, secret token, and allowed update types (`message`, `poll_answer`).

### Key Entities

- **Deployment Pipeline**: The automated orchestration workflow containing jobs for continuous integration (build, test, audit, preview), release artifact generation, and multi-stage environment promotion.
- **Deployment Environment**: An isolated cloud deployment target (`dev` or `prod`) characterized by a dedicated resource group, independent database container, secret vault, and distinct access permissions.
- **Release Artifact**: The packaged, immutable zip bundle produced by compiling and publishing the Azure Functions project, tagged with the triggering commit SHA or release identifier.
- **Infrastructure Stack**: The Pulumi stack definition (`dev` and `prod`) containing the state and configuration parameters for all provisioned Azure cloud resources.
- **Approval Gate**: The manual security checkpoint configured within the deployment environment protection rules that mandates explicit review and authorization before deploying to production.
- **Dependency Update Configuration**: The declared Dependabot schedule and ecosystem targets responsible for keeping third-party packages and workflow actions current and secure.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of pull requests undergo automated build, test, and audit validation, preventing 100% of unverified code changes from merging into the default branch.
- **SC-002**: Automated deployment from pull request merge to development environment verification completes in under 15 minutes.
- **SC-003**: 0% of production deployments occur without documented, logged manual authorization from an authorized reviewer.
- **SC-004**: 100% of required Azure cloud resources are provisioned and managed reproducibly via code, with zero manual configuration required in the cloud portal.
- **SC-005**: All application and infrastructure package dependencies are scanned regularly, with automated update proposals opened within 7 days of upstream release.
- **SC-006**: In the event of a deployment failure at any stage, the target environment remains in a known, stable operational state without affecting other environments.

## Assumptions

- **Source Hosting & CI Platform**: The project is hosted on GitHub, utilizing GitHub Actions as the CI/CD execution engine and GitHub Environments for deployment tracking and approval gates.
- **Cloud Provider**: Microsoft Azure is the target cloud hosting platform for both `dev` and `prod` environments.
- **Pulumi Project Structure & Language**: The Pulumi infrastructure project is authored in C# (.NET 10) using the Pulumi Azure Native provider, allowing all Pulumi package dependencies to be managed as standard NuGet packages alongside the rest of the C# solution.
- **Authentication Standard**: Authentication between GitHub Actions and Microsoft Azure uses OpenID Connect (OIDC) with Workload Identity Federation, adhering to least-privilege principles without storing permanent service principal client secrets.
- **Secrets Management**: Initial bootstrap secrets (such as the Telegram Bot Token for `dev` and `prod` bots) are supplied via GitHub Environment Secrets or directly into the respective Azure Key Vaults during environment setup.
- **Pulumi State Backend**: Pulumi state and concurrency locks are stored securely using an Azure Blob Storage backend (`azblob://`) within an administrative Azure Storage Account, keeping infrastructure state synchronized across pipeline runs with zero external SaaS dependencies.
- **Runtime Environment**: The backend application executes on Azure Functions (.NET 10 Isolated Worker on Linux) with Cosmos DB serverless persistence, matching the architecture and frugality principles established in the system constitution.
