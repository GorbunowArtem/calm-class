namespace CalmClass.InfrastructureTests.Unit.Telegram;

using CalmClass.Infrastructure.Telegram;

public class MarkdownV2HelperTests
{
    [Test]
    public async Task Escape_NullOrEmpty_ReturnsEmpty()
    {
        await Assert.That(MarkdownV2Helper.Escape(null)).IsEqualTo(string.Empty);
        await Assert.That(MarkdownV2Helper.Escape("")).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task Escape_TextWithSpecialChars_EscapesAllCorrectly()
    {
        var input = "Hello *world*! This is [test] (1+2=3). Cost: $5.00 #poll - ok? Yes_no.";
        var expected = @"Hello \*world\*\! This is \[test\] \(1\+2\=3\)\. Cost: $5\.00 \#poll \- ok? Yes\_no\.";

        var actual = MarkdownV2Helper.Escape(input);

        await Assert.That(actual).IsEqualTo(expected);
    }

    [Test]
    public async Task Escape_TextWithBackslash_EscapesBackslash()
    {
        var input = @"Path\to\file";
        var expected = @"Path\\to\\file";

        var actual = MarkdownV2Helper.Escape(input);

        await Assert.That(actual).IsEqualTo(expected);
    }

    [Test]
    public async Task FormatMention_WhenUsernamePresent_FormatsWithAtSymbolAndEscapesUnderscores()
    {
        var actual = MarkdownV2Helper.FormatMention(12345, "Taras", "@taras_sh");

        await Assert.That(actual).IsEqualTo(@"@taras\_sh");
    }

    [Test]
    public async Task FormatMention_WhenUsernameWithoutAt_PrependsAtSymbolAndEscapes()
    {
        var actual = MarkdownV2Helper.FormatMention(12345, "Taras", "taras_sh");

        await Assert.That(actual).IsEqualTo(@"@taras\_sh");
    }

    [Test]
    public async Task FormatMention_WhenUsernameNullOrEmpty_FormatsInlineLink()
    {
        var actual = MarkdownV2Helper.FormatMention(987654321, "Оксана В.", null);

        await Assert.That(actual).IsEqualTo(@"[Оксана В\.](tg://user?id=987654321)");
    }

    [Test]
    public async Task FormatMention_WhenDisplayNameHasSpecialChars_EscapesDisplayNameInLink()
    {
        var actual = MarkdownV2Helper.FormatMention(987654321, "Іван [Тато]", "");

        await Assert.That(actual).IsEqualTo(@"[Іван \[Тато\]](tg://user?id=987654321)");
    }

    [Test]
    public async Task BoldAndItalic_WrapsAndEscapesContent()
    {
        var bold = MarkdownV2Helper.Bold("Увага!");
        var italic = MarkdownV2Helper.Italic("тест_1");

        await Assert.That(bold).IsEqualTo(@"*Увага\!*");
        await Assert.That(italic).IsEqualTo(@"_тест\_1_");
    }
}
