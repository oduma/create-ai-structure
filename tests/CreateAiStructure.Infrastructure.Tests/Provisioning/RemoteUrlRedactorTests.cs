using CreateAiStructure.Infrastructure.Provisioning;
using FluentAssertions;

namespace CreateAiStructure.Infrastructure.Tests.Provisioning;

public class RemoteUrlRedactorTests
{
    [Fact]
    public void Throws_when_value_is_null()
    {
        var act = () => RemoteUrlRedactor.Redact(null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("value");
    }

    [Theory]
    [InlineData("https://x-access-token:SECRET@ghe.corp/org/sub/repo.git", "https://***@ghe.corp/org/sub/repo.git")]
    [InlineData("https://user@github.com/o/r", "https://***@github.com/o/r")]
    [InlineData("https://@github.com/o/r", "https://***@github.com/o/r")]
    [InlineData("ssh://git:pw@h/o/r ssh://a:b@h2/o/r", "ssh://***@h/o/r ssh://***@h2/o/r")]
    public void Replaces_userinfo_after_double_slash(string value, string expected)
    {
        RemoteUrlRedactor.Redact(value).Should().Be(expected);
    }

    [Theory]
    [InlineData("https://github.com/o/r@v1")]
    [InlineData("not-a-remote-url")]
    [InlineData("git@github.com:o/r.git")]
    [InlineData("https://a b@github.com/o/r")]
    [InlineData("")]
    public void Leaves_values_without_double_slash_userinfo_unchanged(string value)
    {
        RemoteUrlRedactor.Redact(value).Should().Be(value);
    }

    [Fact]
    public void Strips_control_characters_including_ansi_escape_and_newlines()
    {
        var redacted = RemoteUrlRedactor.Redact("\u001b[31mhttps://github.com/o\u0007/r\r\n\u0000");

        redacted.Should().Be("[31mhttps://github.com/o/r");
    }
}
