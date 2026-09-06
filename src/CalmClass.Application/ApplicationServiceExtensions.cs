namespace CalmClass.Application;

using CalmClass.Application.Features.Polls.Commands.CancelPoll;
using CalmClass.Application.Features.Polls.Commands.ClosePoll;
using CalmClass.Application.Features.Polls.Commands.CreatePoll;
using CalmClass.Application.Features.Polls.Commands.IngestVote;
using CalmClass.Application.Features.Polls.Services;
using Microsoft.Extensions.DependencyInjection;

public static class ApplicationServiceExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddSingleton<ICreatePollArgsParser, CreatePollArgsParser>();
        services.AddScoped<CreatePollCommandHandler>();
        services.AddScoped<ClosePollCommandHandler>();
        services.AddScoped<CancelPollCommandHandler>();
        services.AddScoped<IngestVoteCommandHandler>();
        services.AddScoped<PollMonitorService>();
        services.AddScoped<PollAuditService>();
        return services;
    }
}
