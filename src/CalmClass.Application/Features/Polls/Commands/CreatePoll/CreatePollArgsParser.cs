namespace CalmClass.Application.Features.Polls.Commands.CreatePoll;

using System.Text.RegularExpressions;
using CalmClass.Application.Common.Options;
using CalmClass.Application.Features.Polls.Localization;

public partial class CreatePollArgsParser : ICreatePollArgsParser
{
    [GeneratedRegex(@"[""'](?<quoted>[^""']+)[""']|(?<unquoted>\S+)")]
    private static partial Regex ArgumentTokenRegex();

    public CreatePollArgsResolutionResult ResolveAndValidate(CreatePollCommand command, PollOptions pollOptions)
    {
        var question = command.Question;
        var options = command.Options?.ToList();
        var duration = command.DurationHours ?? pollOptions.DefaultDurationHours;

        if (!string.IsNullOrWhiteSpace(command.RawArgs) && (string.IsNullOrEmpty(question) || options == null))
        {
            var parsed = ParseRawTokens(command.RawArgs, pollOptions.DefaultDurationHours);
            if (parsed == null)
            {
                return CreatePollArgsResolutionResult.Failed(UkrainianPollMessages.CreatePollUsage);
            }

            question = parsed.Value.Question;
            options = parsed.Value.Options;
            duration = command.DurationHours ?? parsed.Value.DurationHours;
        }

        if (string.IsNullOrWhiteSpace(question) || options == null ||
            options.Count < pollOptions.MinOptionCount || options.Count > pollOptions.MaxOptionCount)
        {
            return CreatePollArgsResolutionResult.Failed(UkrainianPollMessages.InvalidOptionsCount);
        }

        if (duration < pollOptions.MinDurationHours || duration > pollOptions.MaxDurationHours)
        {
            return CreatePollArgsResolutionResult.Failed(UkrainianPollMessages.InvalidDuration);
        }

        var parameters = new CreatePollParameters(question, options, duration);
        return CreatePollArgsResolutionResult.Succeeded(parameters);
    }

    public (string Question, List<string> Options, int DurationHours)? ParseRawTokens(string raw, int defaultDuration)
    {
        var tokens = ArgumentTokenRegex()
            .Matches(raw)
            .Select(m => m.Groups["quoted"].Success ? m.Groups["quoted"].Value.Trim() : m.Groups["unquoted"].Value.Trim())
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToList();

        if (tokens.Count < 3)
        {
            return null;
        }

        var duration = defaultDuration;
        if (tokens.Count >= 4 && int.TryParse(tokens[^1], out var parsedDuration))
        {
            duration = parsedDuration;
            tokens.RemoveAt(tokens.Count - 1);
        }

        var question = tokens[0];
        var options = tokens.Skip(1).ToList();

        return (question, options, duration);
    }
}
