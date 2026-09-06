namespace CalmClass.IaC;

using System;
using System.Collections.Generic;
using DocumentDB = Pulumi.AzureNative.DocumentDB;
using DocumentDBInputs = Pulumi.AzureNative.DocumentDB.Inputs;
using Insights = Pulumi.AzureNative.Insights;
using KeyVault = Pulumi.AzureNative.KeyVault;
using KeyVaultInputs = Pulumi.AzureNative.KeyVault.Inputs;
using OperationalInsights = Pulumi.AzureNative.OperationalInsights;
using OperationalInsightsInputs = Pulumi.AzureNative.OperationalInsights.Inputs;
using Pulumi;
using Resources = Pulumi.AzureNative.Resources;
using Storage = Pulumi.AzureNative.Storage;
using StorageInputs = Pulumi.AzureNative.Storage.Inputs;
using Web = Pulumi.AzureNative.Web;
using WebInputs = Pulumi.AzureNative.Web.Inputs;

public class CalmClassStack : Stack
{
    [Output] public Output<string> ResourceGroupName { get; set; }
    [Output] public Output<string> StorageAccountName { get; set; }
    [Output] public Output<string> CosmosDbAccountEndpoint { get; set; }
    [Output] public Output<string> KeyVaultUri { get; set; }
    [Output] public Output<string> ApplicationInsightsInstrumentationKey { get; set; }
    [Output] public Output<string> FunctionAppName { get; set; }
    [Output] public Output<string> FunctionAppHostName { get; set; }
    [Output] public Output<string> WebhookEndpointUrl { get; set; }

    public CalmClassStack()
    {
        var config = new Config();
        var environment = config.Get("environment") ?? "dev";
        var prefix = config.Get("resourcePrefix") ?? $"calmclass{environment}";
        var cosmosDbName = config.Get("cosmosDatabaseName") ?? "CalmClassDb";
        var cosmosContainer = config.Get("cosmosContainerName") ?? "Polls";
        var quietHoursStart = config.Get("quietHoursStartHour") ?? "20";
        var quietHoursEnd = config.Get("quietHoursEndHour") ?? "8";
        var quietHoursZone = config.Get("quietHoursTimeZoneId") ?? "Europe/Kyiv";
        var telegramBotToken = config.GetSecret("telegramBotToken") ?? Output.CreateSecret("placeholder-token");
        var telegramSecretToken = config.GetSecret("telegramSecretToken") ?? Output.CreateSecret("placeholder-secret");

        // 1. Resource Group
        var resourceGroup = new Resources.ResourceGroup($"rg-{prefix}", new Resources.ResourceGroupArgs
        {
            ResourceGroupName = $"rg-{prefix}",
            Tags = new Dictionary<string, string>
            {
                { "Environment", environment },
                { "ManagedBy", "Pulumi" },
                { "Project", "CalmClass" }
            }
        });

        // 2. Storage Account (for Functions Host Runtime and State)
        var sanitizedPrefix = prefix.Replace("-", string.Empty).ToLowerInvariant();
        var storageAccountName = $"st{sanitizedPrefix}";
        if (storageAccountName.Length > 24)
        {
            storageAccountName = storageAccountName[..24];
        }

        var storageAccount = new Storage.StorageAccount(storageAccountName, new Storage.StorageAccountArgs
        {
            AccountName = storageAccountName,
            ResourceGroupName = resourceGroup.Name,
            Sku = new StorageInputs.SkuArgs { Name = Storage.SkuName.Standard_LRS },
            Kind = Storage.Kind.StorageV2,
            EnableHttpsTrafficOnly = true,
            MinimumTlsVersion = Storage.MinimumTlsVersion.TLS1_2,
            AllowBlobPublicAccess = false
        });

        var storageKeys = Storage.ListStorageAccountKeys.Invoke(new Storage.ListStorageAccountKeysInvokeArgs
        {
            ResourceGroupName = resourceGroup.Name,
            AccountName = storageAccount.Name
        });

        var storageConnectionString = Output.Tuple(storageAccount.Name, storageKeys).Apply(t =>
        {
            var accountName = t.Item1;
            var key = t.Item2.Keys[0].Value;
            return $"DefaultEndpointsProtocol=https;AccountName={accountName};AccountKey={key};EndpointSuffix=core.windows.net";
        });

        // 3. Azure Cosmos DB (Serverless)
        var cosmosAccountName = $"cosmos-{prefix}";
        var cosmosAccount = new DocumentDB.DatabaseAccount(cosmosAccountName, new DocumentDB.DatabaseAccountArgs
        {
            AccountName = cosmosAccountName,
            ResourceGroupName = resourceGroup.Name,
            DatabaseAccountOfferType = DocumentDB.DatabaseAccountOfferType.Standard,
            Capabilities = new[]
            {
                new DocumentDBInputs.CapabilityArgs { Name = "EnableServerless" }
            },
            Locations = new[]
            {
                new DocumentDBInputs.LocationArgs
                {
                    LocationName = resourceGroup.Location,
                    FailoverPriority = 0
                }
            }
        });

        var cosmosDatabase = new DocumentDB.SqlResourceSqlDatabase(cosmosDbName, new DocumentDB.SqlResourceSqlDatabaseArgs
        {
            ResourceGroupName = resourceGroup.Name,
            AccountName = cosmosAccount.Name,
            DatabaseName = cosmosDbName,
            Resource = new DocumentDBInputs.SqlDatabaseResourceArgs { Id = cosmosDbName }
        });

        var cosmosContainerResource = new DocumentDB.SqlResourceSqlContainer(cosmosContainer, new DocumentDB.SqlResourceSqlContainerArgs
        {
            ResourceGroupName = resourceGroup.Name,
            AccountName = cosmosAccount.Name,
            DatabaseName = cosmosDatabase.Name,
            ContainerName = cosmosContainer,
            Resource = new DocumentDBInputs.SqlContainerResourceArgs
            {
                Id = cosmosContainer,
                PartitionKey = new DocumentDBInputs.ContainerPartitionKeyArgs
                {
                    Paths = new[] { "/chatId" },
                    Kind = "Hash"
                }
            }
        });

        var cosmosConnectionStrings = DocumentDB.ListDatabaseAccountConnectionStrings.Invoke(new DocumentDB.ListDatabaseAccountConnectionStringsInvokeArgs
        {
            ResourceGroupName = resourceGroup.Name,
            AccountName = cosmosAccount.Name
        });

        var cosmosPrimaryConnectionString = cosmosConnectionStrings.Apply(c => c.ConnectionStrings[0].ConnectionString);

        // 4. Log Analytics & Application Insights
        var logAnalytics = new OperationalInsights.Workspace($"log-{prefix}", new OperationalInsights.WorkspaceArgs
        {
            WorkspaceName = $"log-{prefix}",
            ResourceGroupName = resourceGroup.Name,
            Sku = new OperationalInsightsInputs.WorkspaceSkuArgs { Name = OperationalInsights.WorkspaceSkuNameEnum.PerGB2018 },
            RetentionInDays = 30
        });

        var appInsights = new Insights.Component($"appi-{prefix}", new Insights.ComponentArgs
        {
            ResourceName = $"appi-{prefix}",
            ResourceGroupName = resourceGroup.Name,
            ApplicationType = Insights.ApplicationType.Web,
            Kind = "web",
            WorkspaceResourceId = logAnalytics.Id
        });

        // 5. Azure Key Vault
        var keyVaultName = $"kv-{prefix}";
        if (keyVaultName.Length > 24)
        {
            keyVaultName = keyVaultName[..24];
        }

        var clientConfig = Pulumi.AzureNative.Authorization.GetClientConfig.InvokeAsync();

        var keyVault = new KeyVault.Vault(keyVaultName, new KeyVault.VaultArgs
        {
            VaultName = keyVaultName,
            ResourceGroupName = resourceGroup.Name,
            Properties = new KeyVaultInputs.VaultPropertiesArgs
            {
                TenantId = clientConfig.Result.TenantId,
                Sku = new KeyVaultInputs.SkuArgs
                {
                    Family = KeyVault.SkuFamily.A,
                    Name = KeyVault.SkuName.Standard
                },
                EnableRbacAuthorization = true,
                SoftDeleteRetentionInDays = 7
            }
        });

        // Store Secrets in Key Vault
        var secretTelegramBotToken = new KeyVault.Secret("telegram-bot-token", new KeyVault.SecretArgs
        {
            SecretName = "telegram-bot-token",
            VaultName = keyVault.Name,
            ResourceGroupName = resourceGroup.Name,
            Properties = new KeyVaultInputs.SecretPropertiesArgs { Value = telegramBotToken }
        });

        var secretTelegramSecretToken = new KeyVault.Secret("telegram-secret-token", new KeyVault.SecretArgs
        {
            SecretName = "telegram-secret-token",
            VaultName = keyVault.Name,
            ResourceGroupName = resourceGroup.Name,
            Properties = new KeyVaultInputs.SecretPropertiesArgs { Value = telegramSecretToken }
        });

        var secretCosmosConnStr = new KeyVault.Secret("cosmos-connection-string", new KeyVault.SecretArgs
        {
            SecretName = "cosmos-connection-string",
            VaultName = keyVault.Name,
            ResourceGroupName = resourceGroup.Name,
            Properties = new KeyVaultInputs.SecretPropertiesArgs { Value = cosmosPrimaryConnectionString }
        });

        var secretAppInsightsKey = new KeyVault.Secret("appinsights-key", new KeyVault.SecretArgs
        {
            SecretName = "appinsights-key",
            VaultName = keyVault.Name,
            ResourceGroupName = resourceGroup.Name,
            Properties = new KeyVaultInputs.SecretPropertiesArgs { Value = appInsights.InstrumentationKey }
        });

        // 6. App Service Plan (Consumption Linux Y1)
        var appServicePlan = new Web.AppServicePlan($"asp-{prefix}", new Web.AppServicePlanArgs
        {
            Name = $"asp-{prefix}",
            ResourceGroupName = resourceGroup.Name,
            Kind = "functionapp,linux",
            Reserved = true,
            Sku = new WebInputs.SkuDescriptionArgs
            {
                Name = "Y1",
                Tier = "Dynamic"
            }
        });

        // 7. Azure Function App (.NET 10 Isolated, Linux)
        var functionAppName = $"func-{prefix}";
        var functionApp = new Web.WebApp(functionAppName, new Web.WebAppArgs
        {
            Name = functionAppName,
            ResourceGroupName = resourceGroup.Name,
            ServerFarmId = appServicePlan.Id,
            Kind = "functionapp,linux",
            Identity = new WebInputs.ManagedServiceIdentityArgs
            {
                Type = Web.ManagedServiceIdentityType.SystemAssigned
            },
            SiteConfig = new WebInputs.SiteConfigArgs
            {
                LinuxFxVersion = "DOTNET-ISOLATED|10.0",
                NetFrameworkVersion = "v10.0",
                Use32BitWorkerProcess = false,
                Http20Enabled = true,
                MinTlsVersion = "1.2",
                AppSettings = new[]
                {
                    new WebInputs.NameValuePairArgs { Name = "FUNCTIONS_EXTENSION_VERSION", Value = "~4" },
                    new WebInputs.NameValuePairArgs { Name = "FUNCTIONS_WORKER_RUNTIME", Value = "dotnet-isolated" },
                    new WebInputs.NameValuePairArgs { Name = "AzureWebJobsStorage", Value = storageConnectionString },
                    new WebInputs.NameValuePairArgs { Name = "APPLICATIONINSIGHTS_CONNECTION_STRING", Value = appInsights.ConnectionString },
                    new WebInputs.NameValuePairArgs { Name = "APPINSIGHTS_INSTRUMENTATIONKEY", Value = appInsights.InstrumentationKey },
                    new WebInputs.NameValuePairArgs { Name = "CosmosDb__ConnectionString", Value = cosmosPrimaryConnectionString },
                    new WebInputs.NameValuePairArgs { Name = "CosmosDb__DatabaseName", Value = cosmosDatabase.Name },
                    new WebInputs.NameValuePairArgs { Name = "CosmosDb__ContainerName", Value = cosmosContainerResource.Name },
                    new WebInputs.NameValuePairArgs { Name = "CosmosDb__UseInMemory", Value = "false" },
                    new WebInputs.NameValuePairArgs { Name = "Telegram__BotToken", Value = telegramBotToken },
                    new WebInputs.NameValuePairArgs { Name = "Telegram__SecretToken", Value = telegramSecretToken },
                    new WebInputs.NameValuePairArgs { Name = "Telegram__BaseUrl", Value = "https://api.telegram.org" },
                    new WebInputs.NameValuePairArgs { Name = "QuietHours__StartHour", Value = quietHoursStart },
                    new WebInputs.NameValuePairArgs { Name = "QuietHours__EndHour", Value = quietHoursEnd },
                    new WebInputs.NameValuePairArgs { Name = "QuietHours__TimeZoneId", Value = quietHoursZone },
                    new WebInputs.NameValuePairArgs { Name = "Poll__DefaultDurationHours", Value = "24" },
                    new WebInputs.NameValuePairArgs { Name = "Poll__ReminderHoursBeforeExpiry", Value = "6" }
                }
            }
        });

        // Set Stack Outputs
        ResourceGroupName = resourceGroup.Name;
        StorageAccountName = storageAccount.Name;
        CosmosDbAccountEndpoint = cosmosAccount.DocumentEndpoint;
        KeyVaultUri = keyVault.Properties.Apply(p => p.VaultUri);
        ApplicationInsightsInstrumentationKey = appInsights.InstrumentationKey;
        FunctionAppName = functionApp.Name;
        FunctionAppHostName = functionApp.DefaultHostName;
        WebhookEndpointUrl = Output.Format($"https://{functionApp.DefaultHostName}/api/telegram/webhook");
    }
}
