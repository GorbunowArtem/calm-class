namespace CalmClass.Functions.Functions;

using CalmClass.Application.Features.Polls.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

public class PollMonitorFunction(
    PollMonitorService pollMonitorService,
    ILogger<PollMonitorFunction> logger)
{
    [Function("PollMonitorFunction")]
    public async Task Run(
        [TimerTrigger("0 */5 * * * *")] TimerInfo timer,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("PollMonitorFunction timer cycle started");

        try
        {
            var reminded = await pollMonitorService.ProcessRemindersAsync(cancellationToken);
            var closed = await pollMonitorService.ProcessClosuresAsync(cancellationToken);

            logger.LogInformation("PollMonitorFunction cycle finished. Reminded: {Reminded}, Closed: {Closed}",
                reminded, closed);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in PollMonitorFunction execution cycle");
        }
    }
}
