namespace CalmClass.ApplicationTests.Unit.Features;

using System.Collections.Generic;
using CalmClass.Application.Common.Options;
using CalmClass.Application.Features.Polls.Commands.CreatePoll;
using CalmClass.Application.Features.Polls.Localization;

public class CreatePollArgsParserTests
{
    private readonly CreatePollArgsParser _parser = new();
    private readonly PollOptions _options = new();

    [Test]
    public async Task ParseRawTokens_WithDoubleQuotes_ExtractsQuestionAndOptions()
    {
        var raw = "\"Тема опитування\" \"Варіант 1\" \"Варіант 2\" 48";
        var result = _parser.ParseRawTokens(raw, 24);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Question).IsEqualTo("Тема опитування");
        await Assert.That(result.Value.Options.Count).IsEqualTo(2);
        await Assert.That(result.Value.Options[0]).IsEqualTo("Варіант 1");
        await Assert.That(result.Value.Options[1]).IsEqualTo("Варіант 2");
        await Assert.That(result.Value.DurationHours).IsEqualTo(48);
    }

    [Test]
    public async Task ParseRawTokens_WithSingleQuotes_ExtractsQuestionAndOptions()
    {
        var raw = "'Вибір екскурсії' 'Музей' 'Театр'";
        var result = _parser.ParseRawTokens(raw, 24);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Question).IsEqualTo("Вибір екскурсії");
        await Assert.That(result.Value.Options.Count).IsEqualTo(2);
        await Assert.That(result.Value.DurationHours).IsEqualTo(24);
    }

    [Test]
    public async Task ParseRawTokens_FewerThanThreeTokens_ReturnsNull()
    {
        var raw = "\"Тільки питання\" \"Один варіант\"";
        var result = _parser.ParseRawTokens(raw, 24);

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task ResolveAndValidate_WhenRawArgsValid_ReturnsSuccess()
    {
        var command = new CreatePollCommand
        {
            ChatId = "-1001",
            UserId = 100,
            RawArgs = "\"Питання\" \"Опція 1\" \"Опція 2\" 72"
        };

        var resolution = _parser.ResolveAndValidate(command, _options);

        await Assert.That(resolution.IsSuccess).IsTrue();
        await Assert.That(resolution.Parameters).IsNotNull();
        await Assert.That(resolution.Parameters!.Question).IsEqualTo("Питання");
        await Assert.That(resolution.Parameters.Options.Count).IsEqualTo(2);
        await Assert.That(resolution.Parameters.DurationHours).IsEqualTo(72);
    }

    [Test]
    public async Task ResolveAndValidate_WhenRawArgsInvalid_ReturnsUsageError()
    {
        var command = new CreatePollCommand
        {
            ChatId = "-1001",
            UserId = 100,
            RawArgs = "\"Недостатньо аргументів\""
        };

        var resolution = _parser.ResolveAndValidate(command, _options);

        await Assert.That(resolution.IsSuccess).IsFalse();
        await Assert.That(resolution.ErrorMessage).IsEqualTo(UkrainianPollMessages.CreatePollUsage);
    }

    [Test]
    public async Task ResolveAndValidate_WhenOptionsCountLessThanMinimum_ReturnsOptionsCountError()
    {
        var command = new CreatePollCommand
        {
            ChatId = "-1001",
            UserId = 100,
            Question = "Питання",
            Options = new List<string> { "Один" }
        };

        var resolution = _parser.ResolveAndValidate(command, _options);

        await Assert.That(resolution.IsSuccess).IsFalse();
        await Assert.That(resolution.ErrorMessage).IsEqualTo(UkrainianPollMessages.InvalidOptionsCount);
    }

    [Test]
    public async Task ResolveAndValidate_WhenOptionsCountExceedsMaximum_ReturnsOptionsCountError()
    {
        var command = new CreatePollCommand
        {
            ChatId = "-1001",
            UserId = 100,
            Question = "Питання",
            Options = new List<string> { "1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11" }
        };

        var resolution = _parser.ResolveAndValidate(command, _options);

        await Assert.That(resolution.IsSuccess).IsFalse();
        await Assert.That(resolution.ErrorMessage).IsEqualTo(UkrainianPollMessages.InvalidOptionsCount);
    }

    [Test]
    public async Task ResolveAndValidate_WhenDurationExceedsMaximum_ReturnsDurationError()
    {
        var command = new CreatePollCommand
        {
            ChatId = "-1001",
            UserId = 100,
            Question = "Питання",
            Options = new List<string> { "Так", "Ні" },
            DurationHours = 200
        };

        var resolution = _parser.ResolveAndValidate(command, _options);

        await Assert.That(resolution.IsSuccess).IsFalse();
        await Assert.That(resolution.ErrorMessage).IsEqualTo(UkrainianPollMessages.InvalidDuration);
    }
}
