namespace CalmClass.Infrastructure.Telegram;

using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

public static class TelegramResiliencePipeline
{
    public static ResiliencePipeline<HttpResponseMessage> CreatePipeline(ILogger? logger = null) => new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = TimeSpan.FromSeconds(1),
                ShouldHandle = args =>
                {
                    var response = args.Outcome.Result;
                    if (args.Outcome.Exception != null)
                    {
                        return ValueTask.FromResult(true);
                    }

                    if (response == null)
                    {
                        return ValueTask.FromResult(false);
                    }

                    var statusCode = (int)response.StatusCode;
                    // Retry on 408 Request Timeout, 429 Too Many Requests, or 5xx Server Errors
                    var shouldRetry = statusCode == (int)HttpStatusCode.RequestTimeout
                                      || statusCode == 429
                                      || statusCode >= 500;

                    return ValueTask.FromResult(shouldRetry);
                },
                DelayGenerator = async args =>
                {
                    var response = args.Outcome.Result;
                    if (response?.StatusCode == (HttpStatusCode)429)
                    {
                        try
                        {
                            var content = await response.Content.ReadAsStringAsync();
                            using var doc = JsonDocument.Parse(content);
                            if (doc.RootElement.TryGetProperty("parameters", out var parameters) &&
                                parameters.TryGetProperty("retry_after", out var retryAfterProp) &&
                                retryAfterProp.TryGetInt32(out var seconds))
                            {
                                logger?.LogWarning("Telegram 429 Too Many Requests. Retrying after {Seconds} seconds.", seconds);
                                return TimeSpan.FromSeconds(Math.Max(seconds, 1));
                            }
                        }
                        catch
                        {
                            // Fallback to default backoff on parse error
                        }
                    }

                    return null; // Uses standard exponential delay
                },
                OnRetry = args =>
                {
                    logger?.LogWarning(
                        args.Outcome.Exception,
                        "Telegram API call retry attempt {AttemptNumber}. Waiting {Delay}.",
                        args.AttemptNumber,
                        args.RetryDelay);
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
}
