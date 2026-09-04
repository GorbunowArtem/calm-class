using System.Text;

namespace CalmClass.Application.Common.Helpers;

/// <summary>
/// Utility for escaping and formatting text strictly per Telegram Bot API MarkdownV2 specifications.
/// </summary>
public static class MarkdownV2Helper
{
    // Characters that MUST be escaped in Telegram MarkdownV2 text:
    // '_', '*', '[', ']', '(', ')', '~', '`', '>', '#', '+', '-', '=', '|', '{', '}', '.', '!'
    private static readonly HashSet<char> SpecialChars = new()
    {
        '_', '*', '[', ']', '(', ')', '~', '`', '>', '#', '+', '-', '=', '|', '{', '}', '.', '!'
    };

    /// <summary>
    /// Escapes all special characters reserved by Telegram MarkdownV2 in regular text blocks.
    /// </summary>
    public static string Escape(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var sb = new StringBuilder(text.Length * 2);
        foreach (var ch in text)
        {
            if (SpecialChars.Contains(ch) || ch == '\\')
            {
                sb.Append('\\');
            }
            sb.Append(ch);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Formats an inline mention for a group member.
    /// Uses @username if available; otherwise falls back to [DisplayName](tg://user?id=userId).
    /// </summary>
    public static string FormatMention(long userId, string displayName, string? username)
    {
        if (!string.IsNullOrWhiteSpace(username))
        {
            var cleanUsername = username.TrimStart('@');
            return "@" + Escape(cleanUsername);
        }

        var escapedName = Escape(string.IsNullOrWhiteSpace(displayName) ? "Користувач" : displayName);
        return $"[{escapedName}](tg://user?id={userId})";
    }

    /// <summary>
    /// Formats bold text in MarkdownV2.
    /// </summary>
    public static string Bold(string text) => $"*{Escape(text)}*";

    /// <summary>
    /// Formats italic text in MarkdownV2.
    /// </summary>
    public static string Italic(string text) => $"_{Escape(text)}_";

    /// <summary>
    /// Formats an inline text link in MarkdownV2.
    /// </summary>
    public static string TextLink(string text, string url)
    {
        var escapedText = Escape(text);
        var escapedUrl = url.Replace("\\", "\\\\").Replace(")", "\\)");
        return $"[{escapedText}]({escapedUrl})";
    }
}
