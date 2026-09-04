namespace CalmClass.Infrastructure;

using CalmClass.Application.Common.Interfaces;
using CalmClass.Application.Common.Options;
using CalmClass.Infrastructure.Persistence;
using CalmClass.Infrastructure.Services;
using CalmClass.Infrastructure.Telegram;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<CalmClassOptions>(options =>
        {
            configuration.GetSection(CalmClassOptions.SectionName).Bind(options);
            configuration.GetSection(TelegramOptions.SectionName).Bind(options.Telegram);
            configuration.GetSection(CosmosDbOptions.SectionName).Bind(options.CosmosDb);
            configuration.GetSection(QuietHoursOptions.SectionName).Bind(options.QuietHours);
            configuration.GetSection(PollOptions.SectionName).Bind(options.Poll);
        });

        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();

        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<CalmClassOptions>>().Value.CosmosDb;
            var connectionString = options.ConnectionString;
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                // Fallback dummy for tests/local initialization without connection string
                connectionString = "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";
            }

            return new CosmosClient(connectionString, new CosmosClientOptions
            {
                SerializerOptions = new CosmosSerializationOptions
                {
                    PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
                }
            });
        });

        services.AddScoped<IPollRepository, CosmosPollRepository>();

        services.AddHttpClient<ITelegramBotClient, TelegramBotClient>();

        return services;
    }
}
