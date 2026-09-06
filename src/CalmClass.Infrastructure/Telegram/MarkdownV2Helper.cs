namespace CalmClass.Infrastructure.Telegram;

/// <summary>
/// Infrastructure adapter forwarding to Application MarkdownV2Helper.
/// </summary>
public static class MarkdownV2Helper
{
    public static string Escape(string? text) =>
        CalmClass.Application.Common.Helpers.MarkdownV2Helper.Escape(text);

    public static string FormatMention(long userId, string displayName, string? username) =>
        CalmClass.Application.Common.Helpers.MarkdownV2Helper.FormatMention(userId, displayName, username);

    public static string Bold(string text) =>
        CalmClass.Application.Common.Helpers.MarkdownV2Helper.Bold(text);

    public static string Italic(string text) =>
        CalmClass.Application.Common.Helpers.MarkdownV2Helper.Italic(text);

    public static string TextLink(string text, string url) =>
        CalmClass.Application.Common.Helpers.MarkdownV2Helper.TextLink(text, url);
}
