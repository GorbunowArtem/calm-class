namespace CalmClass.IaC;

using Pulumi;

public record StackOutputs(
    Output<string> ResourceGroupName,
    Output<string> StorageAccountName,
    Output<string> CosmosDbAccountEndpoint,
    Output<string> KeyVaultUri,
    Output<string> ApplicationInsightsInstrumentationKey,
    Output<string> FunctionAppName,
    Output<string> FunctionAppHostName,
    Output<string> WebhookEndpointUrl);
