namespace CalmClass.InfrastructureTests.Unit.DependencyInjection;

using System.Collections.Generic;
using System.Linq;
using CalmClass.Application.Common.Interfaces;
using CalmClass.Infrastructure;
using CalmClass.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public class InfrastructureServiceExtensionsTests
{
    [Test]
    public async Task AddInfrastructureServices_WhenUseInMemoryIsTrue_RegistersInMemoryPollRepositoryAsSingleton()
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            ["CosmosDb:UseInMemory"] = "true",
            ["CosmosDb:ConnectionString"] = "AccountEndpoint=https://localhost:8081/;AccountKey=dummy;"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructureServices(configuration);

        using var provider = services.BuildServiceProvider();
        var repo1 = provider.GetRequiredService<IPollRepository>();
        var repo2 = provider.GetRequiredService<IPollRepository>();

        await Assert.That(repo1).IsTypeOf<InMemoryPollRepository>();
        await Assert.That(repo1).IsSameReferenceAs(repo2);
    }

    [Test]
    public async Task AddInfrastructureServices_WhenConnectionStringIsEmpty_RegistersInMemoryPollRepository()
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            ["CosmosDb:ConnectionString"] = ""
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructureServices(configuration);

        using var provider = services.BuildServiceProvider();
        var repo = provider.GetRequiredService<IPollRepository>();

        await Assert.That(repo).IsTypeOf<InMemoryPollRepository>();
    }

    [Test]
    public async Task AddInfrastructureServices_WhenConnectionStringIsConfiguredAndNotInMemory_RegistersCosmosPollRepository()
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            ["CosmosDb:UseInMemory"] = "false",
            ["CosmosDb:ConnectionString"] = "AccountEndpoint=https://localhost:8081/;AccountKey=dummykey=="
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructureServices(configuration);

        var descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(IPollRepository));
        await Assert.That(descriptor).IsNotNull();
        await Assert.That(descriptor!.ImplementationType).IsEqualTo(typeof(CosmosPollRepository));
        await Assert.That(descriptor.Lifetime).IsEqualTo(ServiceLifetime.Scoped);
    }
}
