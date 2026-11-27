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
    public void Rejects_invalid_remote_urls_and_returns_empty_slug(string? remoteUrl)
    {
        var parsed = RemoteOriginSlugParser.TryParse(remoteUrl, out var slug);

        parsed.Should().BeFalse();
        slug.Should().BeEmpty();
    }
}
