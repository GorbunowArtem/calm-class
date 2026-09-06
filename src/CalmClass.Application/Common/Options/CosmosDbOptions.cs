namespace CalmClass.Application.Common.Options;

public record CosmosDbOptions
{
    public const string SectionName = "CosmosDb";

    public string ConnectionString { get; init; } = string.Empty;
    public string DatabaseName { get; init; } = "CalmClassDb";
    public string ContainerName { get; init; } = "Polls";
    public bool UseInMemory { get; init; }
}
