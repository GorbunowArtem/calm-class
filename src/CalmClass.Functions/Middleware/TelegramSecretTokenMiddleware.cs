using System.Net;
using CalmClass.Application.Common.Options;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CalmClass.Functions.Middleware;

public class TelegramSecretTokenMiddleware : IFunctionsWorkerMiddleware
{
    public const string SecretHeaderName = "X-Telegram-Bot-Api-Secret-Token";
    private readonly TelegramOptions _telegramOptions;
    private readonly ILogger<TelegramSecretTokenMiddleware> _logger;

    public TelegramSecretTokenMiddleware(
        IOptions<CalmClassOptions> options,
        ILogger<TelegramSecretTokenMiddleware> logger)
    {
        _telegramOptions = options.Value.Telegram;
        _logger = logger;
    }

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var httpRequestData = await context.GetHttpRequestDataAsync();

        // Only validate secret token on HTTP triggers
        if (httpRequestData != null && !string.IsNullOrEmpty(_telegramOptions.SecretToken))
        {
            if (!httpRequestData.Headers.TryGetValues(SecretHeaderName, out var values) ||
                !string.Equals(values.FirstOrDefault(), _telegramOptions.SecretToken, StringComparison.Ordinal))
            {
                _logger.LogWarning("Unauthorized webhook invocation: invalid or missing {Header}", SecretHeaderName);
                var response = httpRequestData.CreateResponse(HttpStatusCode.Unauthorized);
                await response.WriteStringAsync("Unauthorized");
                context.GetInvocationResult().Value = response;
                return;
            }
        }

        await next(context);
    }
}
