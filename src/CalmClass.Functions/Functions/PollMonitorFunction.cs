using CalmClass.Application.Features.Polls.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace CalmClass.Functions.Functions;

public class PollMonitorFunction
{
    private readonly PollMonitorService _pollMonitorService;
    private readonly ILogger<PollMonitorFunction> _logger;

    public PollMonitorFunction(
        PollMonitorService pollMonitorService,
        ILogger<PollMonitorFunction> logger)
    {
        _pollMonitorService = pollMonitorService;
        _logger = logger;
    }

    [Function("PollMonitorFunction")]
    public async Task Run(
        [TimerTrigger("0 */5 * * * *")] TimerInfo timer,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("PollMonitorFunction timer cycle started");

        try
        {
            var reminded = await _pollMonitorService.ProcessRemindersAsync(cancellationToken);
            var closed = await _pollMonitorService.ProcessClosuresAsync(cancellationToken);

            _logger.LogInformation("PollMonitorFunction cycle finished. Reminded: {Reminded}, Closed: {Closed}",
                reminded, closed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in PollMonitorFunction execution cycle");
        }
    }
}
