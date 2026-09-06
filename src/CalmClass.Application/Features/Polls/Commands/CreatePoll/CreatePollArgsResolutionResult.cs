namespace CalmClass.Application.Features.Polls.Commands.CreatePoll;

public record CreatePollArgsResolutionResult(
    bool IsSuccess,
    CreatePollParameters? Parameters = null,
    string? ErrorMessage = null)
{
    public static CreatePollArgsResolutionResult Succeeded(CreatePollParameters parameters) =>
        new(true, Parameters: parameters);

    public static CreatePollArgsResolutionResult Failed(string errorMessage) =>
        new(false, ErrorMessage: errorMessage);
}
