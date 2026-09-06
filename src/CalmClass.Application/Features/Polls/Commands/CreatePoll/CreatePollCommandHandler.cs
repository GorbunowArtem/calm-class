namespace CalmClass.Application.Features.Polls.Commands.CreatePoll;

using CalmClass.Application.Common.Interfaces;
using CalmClass.Application.Common.Options;
using CalmClass.Application.Domain.Entities;
using CalmClass.Application.Domain.Enums;
using CalmClass.Application.Features.Polls.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public class CreatePollCommandHandler(
    IPollRepository pollRepository,
    ITelegramBotClient telegramBotClient,
    IDateTimeProvider dateTimeProvider,
    IOptions<CalmClassOptions> options,
    ICreatePollArgsParser argsParser,
    ILogger<CreatePollCommandHandler> logger)
{
    private const string CommandName = "/create_poll";

    public async Task<CreatePollResult> HandleAsync(CreatePollCommand command, CancellationToken cancellationToken = default)
    {
        if (!await IsAuthorizedAdminAsync(command.ChatId, command.UserId, cancellationToken))
        {
            logger.LogWarning("Unauthorized {Command} attempt by user {UserId} in chat {ChatId}", CommandName, command.UserId, command.ChatId);
            return await FailAsync(command.ChatId, UkrainianPollMessages.UnauthorizedAdminOnly, cancellationToken);
        }

        if (await HasActivePollAsync(command.ChatId, cancellationToken))
        {
            logger.LogWarning("Rejecting {Command}: chat {ChatId} already has active poll", CommandName, command.ChatId);
            return await FailAsync(command.ChatId, UkrainianPollMessages.ActivePollAlreadyExists, cancellationToken);
        }

        var resolution = argsParser.ResolveAndValidate(command, options.Value.Poll);
        if (!resolution.IsSuccess || resolution.Parameters == null)
        {
            var errorMessage = resolution.ErrorMessage ?? UkrainianPollMessages.CreatePollUsage;
            return await FailAsync(command.ChatId, errorMessage, cancellationToken);
        }

        return await PublishAndTrackPollAsync(command.ChatId, resolution.Parameters, cancellationToken);
    }

    private async Task<bool> IsAuthorizedAdminAsync(string chatId, long userId, CancellationToken cancellationToken)
    {
        var member = await pollRepository.GetMemberAsync(chatId, userId, cancellationToken);
        return member is { IsActive: true, Role: MemberRole.Admin };
    }

    private async Task<bool> HasActivePollAsync(string chatId, CancellationToken cancellationToken)
    {
        var activePoll = await pollRepository.GetActivePollAsync(chatId, cancellationToken);
        return activePoll != null;
    }

    private async Task<CreatePollResult> FailAsync(string chatId, string errorMessage, CancellationToken cancellationToken)
    {
        await telegramBotClient.SendMessageAsync(
            chatId,
            errorMessage,
            cancellationToken: cancellationToken);

        return CreatePollResult.Failed(errorMessage);
    }

    private async Task<CreatePollResult> PublishAndTrackPollAsync(
        string chatId,
        CreatePollParameters parameters,
        CancellationToken cancellationToken)
    {
        var telegramResult = await telegramBotClient.SendPollAsync(
            chatId,
            parameters.Question,
            parameters.Options,
            isAnonymous: false,
            allowsMultipleAnswers: false,
            cancellationToken: cancellationToken);

        var now = dateTimeProvider.UtcNow;
        var trackedPoll = new TrackedPoll
        {
            ChatId = chatId,
            PollId = telegramResult.PollId,
            MessageId = telegramResult.MessageId,
            Question = parameters.Question,
            Options = parameters.Options,
            AllowsMultipleAnswers = false,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddHours(parameters.DurationHours),
            Status = PollStatus.Open
        };

        await pollRepository.CreatePollAsync(trackedPoll, cancellationToken);
        logger.LogInformation("Successfully executed {Command}: created poll {PollId} in chat {ChatId}", CommandName, trackedPoll.PollId, chatId);

        return CreatePollResult.Succeeded(trackedPoll);
    }
}
