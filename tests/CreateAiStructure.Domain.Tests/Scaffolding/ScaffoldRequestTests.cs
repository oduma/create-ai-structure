using CreateAiStructure.Domain.Scaffolding;
using FluentAssertions;

namespace CreateAiStructure.Domain.Tests.Scaffolding;

public class ScaffoldRequestTests
{
    private const string ValidWorkingDirectory = @"C:\work";

    public static TheoryData<string?> BlankDirectories => new()
    {
        null,
        "",
        " ",
        "   ",
        "\t",
        "\r\n",
    };

    private static RepositoryName ValidRepositoryName() => RepositoryName.Create("my-repo").Value;

    // ---- Create: success ----

    [Fact]
    public void Create_succeeds_and_exposes_values()
    {
        var repositoryName = ValidRepositoryName();

        var result = ScaffoldRequest.Create(ProjectType.Api, repositoryName, ValidWorkingDirectory);

        result.IsSuccess.Should().BeTrue();
        result.Value.ProjectType.Should().BeSameAs(ProjectType.Api);
        result.Value.RepositoryName.Should().BeSameAs(repositoryName);
        result.Value.WorkingDirectory.Should().Be(ValidWorkingDirectory);
    }

    [Fact]
    public void Create_succeeds_for_every_project_type()
    {
        foreach (var projectType in ProjectType.All)
        {
            var result = ScaffoldRequest.Create(projectType, ValidRepositoryName(), ValidWorkingDirectory);

            result.IsSuccess.Should().BeTrue();
            result.Value.ProjectType.Should().Be(projectType);
        }
    }

    [Fact]
    public void Create_stores_working_directory_as_given()
    {
        var result = ScaffoldRequest.Create(ProjectType.Console, ValidRepositoryName(), " relative/dir ");

        result.IsSuccess.Should().BeTrue();
        result.Value.WorkingDirectory.Should().Be(" relative/dir ");
    }

    [Fact]
    public void Create_does_not_check_the_filesystem()
    {
        var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "does-not-exist");

        var result = ScaffoldRequest.Create(ProjectType.Maui, ValidRepositoryName(), missing);

        result.IsSuccess.Should().BeTrue();
        result.Value.WorkingDirectory.Should().Be(missing);
    }

    // ---- Create: failure ----

    [Fact]
    public void Create_fails_when_project_type_is_null()
    {
        var result = ScaffoldRequest.Create(null, ValidRepositoryName(), ValidWorkingDirectory);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ScaffoldRequest.ProjectTypeRequired");
        result.Error.Message.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Create_fails_when_repository_name_is_null()
    {
        var result = ScaffoldRequest.Create(ProjectType.WebApp, null, ValidWorkingDirectory);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ScaffoldRequest.RepositoryNameRequired");
        result.Error.Message.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [MemberData(nameof(BlankDirectories))]
    public void Create_fails_when_working_directory_is_blank(string? workingDirectory)
    {
        var result = ScaffoldRequest.Create(ProjectType.WebApp, ValidRepositoryName(), workingDirectory);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ScaffoldRequest.WorkingDirectoryRequired");
        result.Error.Message.Should().NotBeNullOrWhiteSpace();
    }
}
