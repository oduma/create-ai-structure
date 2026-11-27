using CreateAiStructure.Tests.Shared;
using FluentAssertions;

namespace CreateAiStructure.Cli.Tests.Publishing;

/// <summary>
/// Guards the single-file publish settings of the CLI project: bundled, compressed and with embedded symbols, while the
/// runtime identifier and self-contained mode stay on the publish command line and reflection-hostile trimming/AOT stay off.
/// </summary>
public class SingleFilePublishSettingsTests
{
    private static readonly ProjectFile CliProject = ProjectFile.Load("src", "CreateAiStructure.Cli");

    [Theory]
    [InlineData("PublishSingleFile", "true")]
    [InlineData("IncludeNativeLibrariesForSelfExtract", "true")]
    [InlineData("EnableCompressionInSingleFile", "true")]
    [InlineData("DebugType", "embedded")]
    public void Cli_project_declares_the_single_file_publish_property(string name, string expected) =>
        CliProject.Property(name).Should().Be(expected);

    [Theory]
    [InlineData("RuntimeIdentifier")]
    [InlineData("SelfContained")]
    [InlineData("PublishTrimmed")]
    [InlineData("PublishAot")]
    public void Cli_project_leaves_the_property_unset(string name) =>
        CliProject.Property(name).Should().BeNull();
}
