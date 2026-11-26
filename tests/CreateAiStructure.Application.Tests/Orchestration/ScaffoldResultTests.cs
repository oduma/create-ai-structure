using CreateAiStructure.Application.Orchestration;
using FluentAssertions;

namespace CreateAiStructure.Application.Tests.Orchestration;

public class ScaffoldResultTests
{
    [Fact]
    public void Constructor_exposes_values()
    {
        IReadOnlyList<string> files = [@"C:\work\repo\CLAUDE.md"];

        var result = new ScaffoldResult(@"C:\work\repo", files, true, "Import the repository in T3 Code.");

        result.RepositoryDirectory.Should().Be(@"C:\work\repo");
        result.MaterializedFiles.Should().BeSameAs(files);
        result.StartedT3DesktopApp.Should().BeTrue();
        result.T3ImportReminder.Should().Be("Import the repository in T3 Code.");
    }

    [Fact]
    public void Results_with_the_same_values_are_equal()
    {
        IReadOnlyList<string> files = [@"C:\work\repo\CLAUDE.md"];

        var first = new ScaffoldResult(@"C:\work\repo", files, false, "reminder");
        var second = new ScaffoldResult(@"C:\work\repo", files, false, "reminder");

        first.Should().Be(second);
        (first == second).Should().BeTrue();
        first.GetHashCode().Should().Be(second.GetHashCode());
    }
}
