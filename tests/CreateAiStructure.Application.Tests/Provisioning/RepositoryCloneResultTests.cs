using CreateAiStructure.Application.Provisioning;
using FluentAssertions;

namespace CreateAiStructure.Application.Tests.Provisioning;

public class RepositoryCloneResultTests
{
    public static TheoryData<string?> BlankValues => new()
    {
        null,
        "",
        " ",
        "\t",
        "\r\n",
    };

    [Theory]
    [MemberData(nameof(BlankValues))]
    public void Constructor_rejects_blank_repository_directory(string? repositoryDirectory)
    {
        var act = () => new RepositoryCloneResult(repositoryDirectory!, "owner/repo");

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [MemberData(nameof(BlankValues))]
    public void Constructor_rejects_blank_repository_slug(string? repositorySlug)
    {
        var act = () => new RepositoryCloneResult(@"C:\work\repo", repositorySlug!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_exposes_values()
    {
        var result = new RepositoryCloneResult(@"C:\work\repo", "owner/repo");

        result.RepositoryDirectory.Should().Be(@"C:\work\repo");
        result.RepositorySlug.Should().Be("owner/repo");
    }

    [Fact]
    public void Results_with_the_same_values_are_equal()
    {
        var first = new RepositoryCloneResult(@"C:\work\repo", "owner/repo");
        var second = new RepositoryCloneResult(@"C:\work\repo", "owner/repo");

        first.Should().Be(second);
        (first == second).Should().BeTrue();
        first.GetHashCode().Should().Be(second.GetHashCode());
    }
}
