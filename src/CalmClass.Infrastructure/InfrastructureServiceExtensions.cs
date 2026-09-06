namespace CalmClass.Infrastructure;

using System;
using System.Text.Json;
using System.Text.Json.Serialization;
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

        var cosmosDbSection = configuration.GetSection(CosmosDbOptions.SectionName);
        var useInMemory = cosmosDbSection.GetValue<bool>(nameof(CosmosDbOptions.UseInMemory));
        var connectionString = cosmosDbSection.GetValue<string>(nameof(CosmosDbOptions.ConnectionString));

        var shouldUseInMemory = useInMemory
            || string.IsNullOrWhiteSpace(connectionString)
            || string.Equals(connectionString, "InMemory", StringComparison.OrdinalIgnoreCase);

        if (shouldUseInMemory)
        {
            services.AddSingleton<IPollRepository, InMemoryPollRepository>();
        }
        else
        {
            services.AddSingleton(sp =>
            {
                var options = sp.GetRequiredService<IOptions<CalmClassOptions>>().Value.CosmosDb;
                return new CosmosClient(options.ConnectionString, new CosmosClientOptions
                {
                    UseSystemTextJsonSerializerWithOptions = new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                    }
                });
            });

            services.AddScoped<IPollRepository, CosmosPollRepository>();
        }

        services.AddHttpClient<ITelegramBotClient, TelegramBotClient>();

        return services;
    }
}


