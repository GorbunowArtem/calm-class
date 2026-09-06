# Pulumi Stack Configuration & IaC Contracts

**Feature**: `specs/002-azure-cicd-pulumi`  
**Date**: 2026-09-06  
**Status**: Completed  

---

## 1. Pulumi Project Structure: `infra/CalmClass.IaC/`

The Pulumi project is declared as a C# (.NET 10) executable project integrated into `CalmClass.slnx` and managed under central package versions.

```text
infra/CalmClass.IaC/
├── CalmClass.IaC.csproj      # C# project referencing Pulumi and Pulumi.AzureNative
├── Program.cs                # Deployment entry point: Deployment.RunAsync<CalmClassStack>()
├── CalmClassStack.cs         # Primary stack definition coordinating all Azure components
├── Pulumi.yaml               # Project metadata & runtime specification
├── Pulumi.dev.yaml           # Dev stack configuration & parameter values
└── Pulumi.prod.yaml          # Prod stack configuration & parameter values
```

---

## 2. Project Metadata Contract: `Pulumi.yaml`

```yaml
name: CalmClass.IaC
runtime: dotnet
description: Cloud Infrastructure as Code for CalmClass School Chat Platform (Azure Native)
backend:
  url: azblob://pulumi-state
```

---

## 3. Stack Configuration Contracts

### 3.1 Development Stack Contract: `Pulumi.dev.yaml`

```yaml
config:
  azure-native:location: polandcentral
  CalmClass.IaC:environment: dev
  CalmClass.IaC:resourcePrefix: calmclass-dev
  CalmClass.IaC:cosmosDatabaseName: CalmClassDb
  CalmClass.IaC:cosmosContainerName: Polls
  CalmClass.IaC:quietHoursStartHour: 20
  CalmClass.IaC:quietHoursEndHour: 8
  CalmClass.IaC:quietHoursTimeZoneId: Europe/Kyiv
  CalmClass.IaC:telegramBotToken:
    secure: [CIPHERTEXT]
  CalmClass.IaC:telegramSecretToken:
    secure: [CIPHERTEXT]
```

### 3.2 Production Stack Contract: `Pulumi.prod.yaml`

```yaml
config:
  azure-native:location: polandcentral
  CalmClass.IaC:environment: prod
  CalmClass.IaC:resourcePrefix: calmclass-prod
  CalmClass.IaC:cosmosDatabaseName: CalmClassDb
  CalmClass.IaC:cosmosContainerName: Polls
  CalmClass.IaC:quietHoursStartHour: 20
  CalmClass.IaC:quietHoursEndHour: 8
  CalmClass.IaC:quietHoursTimeZoneId: Europe/Kyiv
  CalmClass.IaC:telegramBotToken:
    secure: [CIPHERTEXT]
  CalmClass.IaC:telegramSecretToken:
    secure: [CIPHERTEXT]
```

---

## 4. Stack Inputs & Outputs Contract

### 4.1 Input Schema (`Config` Keys)

| Key | Type | Secret | Default / Required | Description |
| :--- | :--- | :---: | :--- | :--- |
| `azure-native:location` | `string` | No | `polandcentral` | Target Azure primary region. |
| `environment` | `string` | No | Required (`dev` or `prod`) | Environment identifier used in naming. |
| `resourcePrefix` | `string` | No | Required | Naming prefix (e.g. `calmclass-dev`). |
| `cosmosDatabaseName` | `string` | No | `CalmClassDb` | Cosmos SQL database name. |
| `cosmosContainerName`| `string` | No | `Polls` | Cosmos SQL container name. |
| `telegramBotToken` | `string` | Yes | Required | Telegram bot authentication token stored in Key Vault. |
| `telegramSecretToken`| `string` | Yes | Required | Webhook verification token stored in Key Vault. |

### 4.2 Output Schema (`Stack` Outputs)

| Output Name | Type | Description |
| :--- | :--- | :--- |
| `ResourceGroupName` | `Output<string>` | Name of the provisioned Azure Resource Group. |
| `StorageAccountName` | `Output<string>` | Name of the primary Azure Storage Account. |
| `CosmosDbAccountEndpoint` | `Output<string>` | URI endpoint of the Cosmos DB account. |
| `KeyVaultUri` | `Output<string>` | Vault URI endpoint (`https://<vault-name>.vault.azure.net/`). |
| `ApplicationInsightsInstrumentationKey` | `Output<string>` | Telemetry instrumentation key (masked as secret). |
| `FunctionAppName` | `Output<string>` | Name of the deployed Azure Function App. |
| `FunctionAppHostName` | `Output<string>` | Public hostname: `<func-app-name>.azurewebsites.net`. |
| `WebhookEndpointUrl` | `Output<string>` | Public webhook URL: `https://<func-app-name>.azurewebsites.net/api/telegram/webhook`. |

---

## 5. Clean Code & Architectural Invariants for Pulumi C#

In accordance with project constitution and clean architecture rules:
- All classes, records, and stack definitions MUST use **C# primary constructors**.
- Every class or record resides in its own dedicated file (e.g. `CalmClassStack.cs`, `StackOutputs.cs`).
- All `using` directives reside inside file-scoped namespaces sorted with `System` first.
- Resource names are strongly-typed without inline magic strings.
- Cosmos DB connection strings and Telegram tokens are assigned to Key Vault and referenced in Function App settings via Key Vault references (`@Microsoft.KeyVault(...)`).
