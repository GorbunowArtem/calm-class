using System.Net;
using CalmClass.Application.Common.Options;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CalmClass.Functions.Middleware;

public class TelegramSecretTokenMiddleware(
    IOptions<CalmClassOptions> options,
    ILogger<TelegramSecretTokenMiddleware> logger) : IFunctionsWorkerMiddleware
{
    public const string SecretHeaderName = "X-Telegram-Bot-Api-Secret-Token";

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var httpRequestData = await context.GetHttpRequestDataAsync();
        var telegramOptions = options.Value.Telegram;

        // Only validate secret token on HTTP triggers
        if (httpRequestData != null && !string.IsNullOrEmpty(telegramOptions.SecretToken))
        {
            if (!httpRequestData.Headers.TryGetValues(SecretHeaderName, out var values) ||
                !string.Equals(values.FirstOrDefault(), telegramOptions.SecretToken, StringComparison.Ordinal))
            {
                logger.LogWarning("Unauthorized webhook invocation: invalid or missing {Header}", SecretHeaderName);
                var response = httpRequestData.CreateResponse(HttpStatusCode.Unauthorized);
                await response.WriteStringAsync("Unauthorized");
                context.GetInvocationResult().Value = response;
                return;
            }
        }

        await next(context);
    }
}
