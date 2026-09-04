using System.Text;

namespace CalmClass.Application.Features.Polls.Localization;

public static class UkrainianPollMessages
{
    public const string UnauthorizedAdminOnly = "⚠️ Команда доступна лише адміністраторам батьківського комітету.";
    public const string ActivePollAlreadyExists = "⚠️ У групі вже є активне опитування. Завершіть або скасуйте його перед створенням нового (/close_poll або /cancel_poll).";
    public const string InvalidOptionsCount = "⚠️ Опитування повинно містити від 2 до 10 варіантів відповіді.";
    public const string InvalidDuration = "⚠️ Тривалість опитування має становити від 1 до 168 годин (за замовчуванням 24 години).";
    public const string CreatePollUsage = "ℹ️ Формат команди: `/create_poll \"Питання\" \"Варіант 1\" \"Варіант 2\" ... [години]`";
    public const string NoActivePollFound = "⚠️ У цьому чаті немає активного опитування.";
    public const string PollCancelled = "🚫 Опитування скасовано адміністратором. Результати анульовано.";

    public static string FormatReminder(string escapedQuestion, IEnumerable<string> formattedMentions)
    {
        var sb = new StringBuilder();
        sb.AppendLine("⏰ *Нагадування про голосування\\!*");
        sb.AppendLine();
        sb.AppendLine($"Залишилося менше 6 годин до завершення опитування: *{escapedQuestion}*");
        sb.AppendLine();
        sb.AppendLine("Будь ласка, проголосуйте:");
        sb.AppendLine(string.Join(", ", formattedMentions));
        return sb.ToString().TrimEnd();
    }

    public static string FormatSummaryReport(
        string escapedQuestion,
        int totalEligibleMembers,
        int totalVoters,
        IReadOnlyList<(string OptionName, int VoteCount, double Percentage)> optionResults,
        IReadOnlyList<string> winnerOptions)
    {
        var turnoutPercent = totalEligibleMembers > 0
            ? Math.Round((double)totalVoters / totalEligibleMembers * 100.0, 1)
            : 0.0;

        var sb = new StringBuilder();
        sb.AppendLine($"📊 *Підсумки голосування:* *{escapedQuestion}*");
        sb.AppendLine();
        sb.AppendLine($"Всього учасників: {totalEligibleMembers}");
        sb.AppendLine($"Проголосувало: {totalVoters} \\({turnoutPercent:0.#}%\\)");
        sb.AppendLine();
        sb.AppendLine("*Результати:*");

        foreach (var (optionName, count, pct) in optionResults)
        {
            sb.AppendLine($"• {optionName}: {count} \\({pct:0.#}%\\)");
        }

        sb.AppendLine();
        if (totalVoters == 0)
        {
            sb.AppendLine("Результатів немає \\(ніхто не проголосував\\)\\.");
        }
        else if (winnerOptions.Count == 1)
        {
            sb.AppendLine($"🏆 Переможець: *{winnerOptions[0]}*");
        }
        else if (winnerOptions.Count > 1)
        {
            sb.AppendLine($"🤝 Однаковий результат: *{string.Join(", ", winnerOptions)}*");
        }

        return sb.ToString().TrimEnd();
    }
}
