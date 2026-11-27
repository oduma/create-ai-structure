using CreateAiStructure.Infrastructure.Provisioning;
using FluentAssertions;

namespace CreateAiStructure.Infrastructure.Tests.Provisioning;

public class RemoteOriginSlugParserTests
{
    [Theory]
    [InlineData("https://github.com/o/r.git")]
    [InlineData("https://github.com/o/r")]
    [InlineData("https://github.com/o/r/")]
    [InlineData("https://github.com/o/r.git/")]
    [InlineData("https://user@github.com/o/r.git")]
    [InlineData("HTTPS://github.com/o/r.GIT")]
    [InlineData("ssh://git@github.com/o/r.git")]
    [InlineData("git@github.com:o/r.git")]
    [InlineData("git@github.com:o/r")]
    [InlineData("https://github.example.com/o/r.git")]
    [InlineData("  https://github.com/o/r.git\n")]
    [InlineData("git@github.com:o/r.git\r\n")]
    public void Parses_valid_remote_urls_to_owner_slash_repo(string remoteUrl)
    {
        var parsed = RemoteOriginSlugParser.TryParse(remoteUrl, out var slug);

        parsed.Should().BeTrue();
        slug.Should().Be("o/r");
    }

    [Fact]
    public void Preserves_owner_and_repo_casing_and_punctuation()
    {
        var parsed = RemoteOriginSlugParser.TryParse("https://github.com/Octo-Org/My_Repo.v2.git", out var slug);

        parsed.Should().BeTrue();
        slug.Should().Be("Octo-Org/My_Repo.v2");
    }

    [Theory]
    [InlineData("git@github.com:o-/r-.git", "o-/r-")]
    [InlineData("git@github.com:.o/..r", ".o/..r")]
    [InlineData("git@github.com:O_1/R.2", "O_1/R.2")]
    public void Accepts_segments_made_of_letters_digits_dot_underscore_and_hyphen(string remoteUrl, string expected)
    {
        var parsed = RemoteOriginSlugParser.TryParse(remoteUrl, out var slug);

        parsed.Should().BeTrue();
        slug.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("https://github.com/o")]
    [InlineData("https://github.com/o/r/extra")]
    [InlineData("https://github.com//r")]
    [InlineData("git@github.com:")]
    [InlineData(":o/r")]
    [InlineData("no-colon-or-scheme")]
    [InlineData("http://[bad")]
    [InlineData("git@github.com:o/r x")]
    [InlineData("git@github.com:o x/r")]
    [InlineData("git@github.com:o//r")]
    [InlineData("git@github.com:o/r/extra.git")]
    [InlineData("https://github.com/")]
    [InlineData("https://github.com/o%2Fx/r.git")]
    [InlineData("https://github.com/o/r%2Fx")]
    [InlineData("git@github.com:o@x/r.git")]
    [InlineData("git@github.com:o/r@x")]
    [InlineData("git@github.com:-o/r.git")]
    [InlineData("git@github.com:o/-r.git")]
    [InlineData("git@github.com:./r.git")]
    [InlineData("git@github.com:../r.git")]
    [InlineData("git@github.com:o/..")]
    [InlineData("git@github.com:o/.")]
    [InlineData("u:p@h:o/r")]
    [InlineData("git@github.com:o/r;x")]
    [InlineData("git@github.com:o/r\tx")]
    [InlineData("git@github.com:o/ré")]
    public void Rejects_invalid_remote_urls_and_returns_empty_slug(string? remoteUrl)
    {
        var parsed = RemoteOriginSlugParser.TryParse(remoteUrl, out var slug);

        parsed.Should().BeFalse();
        slug.Should().BeEmpty();
    }
}
