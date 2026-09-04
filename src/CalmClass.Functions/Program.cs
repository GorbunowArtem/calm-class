using CalmClass.Application;
using CalmClass.Functions.Middleware;
using CalmClass.Infrastructure;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Worker", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("Starting CalmClass Functions Host");

    var host = new HostBuilder()
        .ConfigureFunctionsWorkerDefaults(worker =>
        {
            worker.UseMiddleware<TelegramSecretTokenMiddleware>();
        })
        .UseSerilog()
        .ConfigureServices((context, services) =>
        {
            services.AddApplicationServices();
            services.AddInfrastructureServices(context.Configuration);
        })
        .Build();

    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "CalmClass Functions Host terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}
