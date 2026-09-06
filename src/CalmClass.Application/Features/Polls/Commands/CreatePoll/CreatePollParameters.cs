namespace CalmClass.Application.Features.Polls.Commands.CreatePoll;

using System.Collections.Generic;

public record CreatePollParameters(
    string Question,
    IReadOnlyList<string> Options,
    int DurationHours);
