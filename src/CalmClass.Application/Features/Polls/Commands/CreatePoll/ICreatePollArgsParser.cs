namespace CalmClass.Application.Features.Polls.Commands.CreatePoll;

using CalmClass.Application.Common.Options;

public interface ICreatePollArgsParser
{
    CreatePollArgsResolutionResult ResolveAndValidate(CreatePollCommand command, PollOptions pollOptions);

    (string Question, List<string> Options, int DurationHours)? ParseRawTokens(string raw, int defaultDuration);
}
