using CreateAiStructure.Application.Runtime;
using CreateAiStructure.Infrastructure.T3;
using FluentAssertions;
using NSubstitute;

namespace CreateAiStructure.Infrastructure.Tests.T3;

public class T3DesktopExecutableLocatorTests
{
    private const string OverrideVariable = "T3CODE_DESKTOP_EXE";
    private const string ProgramFilesVariable = "ProgramFiles";
    private const string ProgramFilesX86Variable = "ProgramFiles(x86)";
    private const string ExeSearchPattern = "*.exe";
    private const string NotFoundCode = "T3Launch.DesktopExecutableNotFound";

    private const string LocalAppData = @"C:\Users\u\AppData\Local";
    private const string ProgramFiles = @"C:\Program Files";
    private const string ProgramFilesX86 = @"C:\Program Files (x86)";

    private static readonly string[] ExactNames =
    [
        "T3 Code.exe",
        "T3 Code (Beta).exe",
        "t3code.exe",
        "T3Code.exe",
    ];

    private static readonly string[] StandardDirectories =
    [
        Path.Combine(LocalAppData, "Programs", "t3code"),
        Path.Combine(LocalAppData, "Programs", "T3 Code"),
        Path.Combine(LocalAppData, "t3code"),
        Path.Combine(ProgramFiles, "t3code"),
        Path.Combine(ProgramFiles, "T3 Code"),
        Path.Combine(ProgramFilesX86, "t3code"),
        Path.Combine(ProgramFilesX86, "T3 Code"),
    ];

    private readonly IEnvironmentProvider _environment = Substitute.For<IEnvironmentProvider>();
    private readonly IFileSystem _fileSystem = Substitute.For<IFileSystem>();

    public T3DesktopExecutableLocatorTests()
    {
        _environment.GetEnvironmentVariable(Arg.Any<string>()).Returns((string?)null);
        _environment.GetLocalApplicationDataPath().Returns(string.Empty);
        _fileSystem.FileExists(Arg.Any<string>()).Returns(false);
        _fileSystem.DirectoryExists(Arg.Any<string>()).Returns(false);
        _fileSystem.GetFiles(Arg.Any<string>(), Arg.Any<string>()).Returns(Array.Empty<string>());
    }

    public static TheoryData<int> DirectoryIndexes
    {
        get
        {
            var data = new TheoryData<int>();
            for (var index = 0; index < StandardDirectories.Length; index++)
            {
                data.Add(index);
            }

            return data;
        }
    }

    public static TheoryData<int> AdjacentDirectoryIndexes
    {
        get
        {
            var data = new TheoryData<int>();
            for (var index = 0; index < StandardDirectories.Length - 1; index++)
            {
                data.Add(index);
            }

            return data;
        }
    }

    public static TheoryData<int> ExactNameIndexes
    {
        get
        {
            var data = new TheoryData<int>();
            for (var index = 0; index < ExactNames.Length; index++)
            {
                data.Add(index);
            }

            return data;
        }
    }

    public static TheoryData<string> InvalidOverrides => new()
    {
        string.Empty,
        "   ",
        "t3.exe",
        @".\t3.exe",
        @"C:\x\..\t3.exe",
        "C:/x/../t3.exe",
        "\"",
        "\"\"",
    };

    public static TheoryData<string?> InvalidBasePaths => new()
    {
        null,
        string.Empty,
        "   ",
        @"relative\path",
    };

    public static TheoryData<string> ExcludedFallbackNames => new()
    {
        "Uninstall T3 Code (Beta).exe",
        "Update.exe",
        "elevate.exe",
        "T3 Updater.exe",
        "notepad.exe",
        "T3-ELEVATE.exe",
    };

    // ---------- Constructor ----------

    [Fact]
    public void Constructor_NullEnvironmentProvider_ThrowsArgumentNullException()
    {
        var act = () => new T3DesktopExecutableLocator(null!, _fileSystem);

        act.Should().Throw<ArgumentNullException>().WithParameterName("environmentProvider");
    }

    [Fact]
    public void Constructor_NullFileSystem_ThrowsArgumentNullException()
    {
        var act = () => new T3DesktopExecutableLocator(_environment, null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("fileSystem");
    }

    [Fact]
    public void Locator_ImplementsInterface()
    {
        IT3DesktopExecutableLocator locator = CreateLocator();

        locator.Should().BeOfType<T3DesktopExecutableLocator>();
    }

    // ---------- Override ----------

    [Theory]
    [InlineData(@"C:\Tools\T3 Code\T3 Code.exe", @"C:\Tools\T3 Code\T3 Code.exe")]
    [InlineData("  \"C:\\Tools\\T3 Code\\T3 Code.exe\"  ", @"C:\Tools\T3 Code\T3 Code.exe")]
    [InlineData("\"  C:\\Tools\\T3 Code\\T3 Code.exe  \"", @"C:\Tools\T3 Code\T3 Code.exe")]
    [InlineData(@"\\server\share\T3 Code.exe", @"\\server\share\T3 Code.exe")]
    public void Locate_ValidExistingOverride_ReturnsOverrideWithoutProbingDirectories(string rawOverride, string expected)
    {
        UseStandardBases();
        _environment.GetEnvironmentVariable(OverrideVariable).Returns(rawOverride);
        _fileSystem.FileExists(expected).Returns(true);

        var result = CreateLocator().Locate();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expected);
        _fileSystem.DidNotReceive().DirectoryExists(Arg.Any<string>());
        _fileSystem.DidNotReceive().GetFiles(Arg.Any<string>(), Arg.Any<string>());
    }

    [Theory]
    [MemberData(nameof(InvalidOverrides))]
    public void Locate_InvalidOverride_IsIgnoredAndNeverPassedToFileExists(string rawOverride)
    {
        UseStandardBases();
        _environment.GetEnvironmentVariable(OverrideVariable).Returns(rawOverride);
        var expected = Path.Combine(StandardDirectories[0], "T3 Code.exe");
        _fileSystem.DirectoryExists(StandardDirectories[0]).Returns(true);
        _fileSystem.FileExists(expected).Returns(true);

        var result = CreateLocator().Locate();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expected);
        FileExistsArguments().Should().OnlyContain(path => IsUnderStandardDirectory(path));
    }

    [Fact]
    public void Locate_ValidOverrideThatDoesNotExist_FallsBackToDirectories()
    {
        const string missingOverride = @"C:\Missing\T3 Code.exe";
        UseStandardBases();
        _environment.GetEnvironmentVariable(OverrideVariable).Returns(missingOverride);
        var expected = Path.Combine(StandardDirectories[0], "T3 Code.exe");
        _fileSystem.DirectoryExists(StandardDirectories[0]).Returns(true);
        _fileSystem.FileExists(expected).Returns(true);

        var result = CreateLocator().Locate();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expected);
        _fileSystem.Received(1).FileExists(missingOverride);
        _fileSystem.Received().DirectoryExists(StandardDirectories[0]);
    }

    // ---------- Directory precedence ----------

    [Theory]
    [MemberData(nameof(DirectoryIndexes))]
    public void Locate_ExecutableOnlyInDirectoryN_ReturnsPathInDirectoryN(int index)
    {
        UseStandardBases();
        MarkAllStandardDirectoriesExisting();
        var expected = Path.Combine(StandardDirectories[index], "T3 Code.exe");
        _fileSystem.FileExists(expected).Returns(true);

        var result = CreateLocator().Locate();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expected);
    }

    [Theory]
    [MemberData(nameof(AdjacentDirectoryIndexes))]
    public void Locate_ExecutableInDirectoriesNAndNextOne_DirectoryNWins(int index)
    {
        UseStandardBases();
        MarkAllStandardDirectoriesExisting();
        var expected = Path.Combine(StandardDirectories[index], "T3 Code.exe");
        _fileSystem.FileExists(expected).Returns(true);
        _fileSystem.FileExists(Path.Combine(StandardDirectories[index + 1], "T3 Code.exe")).Returns(true);

        var result = CreateLocator().Locate();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expected);
    }

    [Fact]
    public void Locate_ExistingDirectoryWithoutMatchingFiles_ContinuesToNextDirectory()
    {
        UseStandardBases();
        _fileSystem.DirectoryExists(StandardDirectories[0]).Returns(true);
        _fileSystem.DirectoryExists(StandardDirectories[1]).Returns(true);
        var expected = Path.Combine(StandardDirectories[1], "t3code.exe");
        _fileSystem.FileExists(expected).Returns(true);

        var result = CreateLocator().Locate();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expected);
        _fileSystem.Received(1).GetFiles(StandardDirectories[0], ExeSearchPattern);
    }

    [Fact]
    public void Locate_MissingDirectory_IsNotProbedForFiles()
    {
        UseStandardBases();
        var expected = Path.Combine(StandardDirectories[1], "T3 Code.exe");
        _fileSystem.DirectoryExists(StandardDirectories[1]).Returns(true);
        _fileSystem.FileExists(expected).Returns(true);

        var result = CreateLocator().Locate();

        result.Value.Should().Be(expected);
        _fileSystem.DidNotReceive().GetFiles(StandardDirectories[0], Arg.Any<string>());
        FileExistsArguments().Should().NotContain(path => path.StartsWith(StandardDirectories[0] + Path.DirectorySeparatorChar, StringComparison.Ordinal));
    }

    // ---------- Deduplication ----------

    [Fact]
    public void Locate_ProgramFilesVariablesDifferOnlyInCase_EachDistinctDirectoryProbedOnce()
    {
        const string upperProgramFiles = @"C:\PROGRAM FILES";
        _environment.GetEnvironmentVariable(ProgramFilesVariable).Returns(ProgramFiles);
        _environment.GetEnvironmentVariable(ProgramFilesX86Variable).Returns(upperProgramFiles);
        _fileSystem.DirectoryExists(Arg.Any<string>()).Returns(true);

        var result = CreateLocator().Locate();

        result.IsFailure.Should().BeTrue();
        var t3codeDirectory = Path.Combine(ProgramFiles, "t3code");
        var t3CodeDirectory = Path.Combine(ProgramFiles, "T3 Code");
        _fileSystem.Received(1).DirectoryExists(Arg.Is<string>(path => string.Equals(path, t3codeDirectory, StringComparison.OrdinalIgnoreCase)));
        _fileSystem.Received(1).DirectoryExists(Arg.Is<string>(path => string.Equals(path, t3CodeDirectory, StringComparison.OrdinalIgnoreCase)));
        _fileSystem.Received(2).DirectoryExists(Arg.Any<string>());
    }

    // ---------- Base path validation ----------

    [Theory]
    [MemberData(nameof(InvalidBasePaths))]
    public void Locate_InvalidLocalAppDataPath_IsSkipped(string? invalidBase)
    {
        _environment.GetLocalApplicationDataPath().Returns(invalidBase!);
        _environment.GetEnvironmentVariable(ProgramFilesVariable).Returns(ProgramFiles);

        var result = CreateLocator().Locate();

        result.IsFailure.Should().BeTrue();
        DirectoryExistsArguments().Should().BeEquivalentTo(
            [StandardDirectories[3], StandardDirectories[4]],
            options => options.WithStrictOrdering());
    }

    [Theory]
    [MemberData(nameof(InvalidBasePaths))]
    public void Locate_InvalidProgramFilesPaths_AreSkipped(string? invalidBase)
    {
        _environment.GetLocalApplicationDataPath().Returns(LocalAppData);
        _environment.GetEnvironmentVariable(ProgramFilesVariable).Returns(invalidBase);
        _environment.GetEnvironmentVariable(ProgramFilesX86Variable).Returns(invalidBase);

        var result = CreateLocator().Locate();

        result.IsFailure.Should().BeTrue();
        DirectoryExistsArguments().Should().BeEquivalentTo(
            [StandardDirectories[0], StandardDirectories[1], StandardDirectories[2]],
            options => options.WithStrictOrdering());
    }

    [Fact]
    public void Locate_OnlyProgramFilesX86Valid_ProbesOnlyItsDirectories()
    {
        _environment.GetEnvironmentVariable(ProgramFilesX86Variable).Returns(ProgramFilesX86);

        var result = CreateLocator().Locate();

        result.IsFailure.Should().BeTrue();
        DirectoryExistsArguments().Should().BeEquivalentTo(
            [StandardDirectories[5], StandardDirectories[6]],
            options => options.WithStrictOrdering());
    }

    [Fact]
    public void Locate_AllBasesValidAndNothingExists_ProbesAllSevenDirectoriesInOrder()
    {
        UseStandardBases();

        CreateLocator().Locate();

        DirectoryExistsArguments().Should().BeEquivalentTo(StandardDirectories, options => options.WithStrictOrdering());
    }

    // ---------- Exact-name priority ----------

    [Theory]
    [MemberData(nameof(ExactNameIndexes))]
    public void Locate_SeveralExactNamesExist_ReturnsHighestPriorityName(int index)
    {
        UseStandardBases();
        var directory = StandardDirectories[0];
        _fileSystem.DirectoryExists(directory).Returns(true);
        for (var lower = index; lower < ExactNames.Length; lower++)
        {
            _fileSystem.FileExists(Path.Combine(directory, ExactNames[lower])).Returns(true);
        }

        var result = CreateLocator().Locate();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(Path.Combine(directory, ExactNames[index]));
        _fileSystem.DidNotReceive().GetFiles(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public void Locate_ExactNames_AreProbedInPriorityOrder()
    {
        UseStandardBases();
        var directory = StandardDirectories[0];
        _fileSystem.DirectoryExists(directory).Returns(true);

        CreateLocator().Locate();

        var probedInFirstDirectory = FileExistsArguments()
            .Where(path => string.Equals(Path.GetDirectoryName(path), directory, StringComparison.Ordinal))
            .ToList();
        probedInFirstDirectory.Should().BeEquivalentTo(
            ExactNames.Select(name => Path.Combine(directory, name)),
            options => options.WithStrictOrdering());
    }

    // ---------- Fallback scan ----------

    [Fact]
    public void Locate_NoExactName_FallbackScanReturnsT3Executable()
    {
        UseStandardBases();
        var directory = StandardDirectories[0];
        _fileSystem.DirectoryExists(directory).Returns(true);
        var canary = Path.Combine(directory, "T3 Code (Canary).exe");
        _fileSystem.GetFiles(directory, ExeSearchPattern).Returns(
        [
            Path.Combine(directory, "Uninstall T3 Code (Beta).exe"),
            Path.Combine(directory, "Update.exe"),
            Path.Combine(directory, "elevate.exe"),
            Path.Combine(directory, "T3 Updater.exe"),
            Path.Combine(directory, "notepad.exe"),
            Path.Combine(directory, "T3-ELEVATE.exe"),
            canary,
        ]);

        var result = CreateLocator().Locate();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(canary);
        _fileSystem.Received(1).GetFiles(directory, ExeSearchPattern);
    }

    [Theory]
    [MemberData(nameof(ExcludedFallbackNames))]
    public void Locate_FallbackScanOnlyFindsExcludedName_ContinuesToNextDirectory(string excludedName)
    {
        UseStandardBases();
        _fileSystem.DirectoryExists(StandardDirectories[0]).Returns(true);
        _fileSystem.GetFiles(StandardDirectories[0], ExeSearchPattern).Returns([Path.Combine(StandardDirectories[0], excludedName)]);
        _fileSystem.DirectoryExists(StandardDirectories[1]).Returns(true);
        var expected = Path.Combine(StandardDirectories[1], "T3 Code.exe");
        _fileSystem.FileExists(expected).Returns(true);

        var result = CreateLocator().Locate();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expected);
    }

    [Fact]
    public void Locate_FallbackScanAllFilesExcluded_ContinuesToNextDirectoryFallback()
    {
        UseStandardBases();
        _fileSystem.DirectoryExists(StandardDirectories[0]).Returns(true);
        _fileSystem.GetFiles(StandardDirectories[0], ExeSearchPattern).Returns(
        [
            Path.Combine(StandardDirectories[0], "Uninstall T3 Code (Beta).exe"),
            Path.Combine(StandardDirectories[0], "Update.exe"),
            Path.Combine(StandardDirectories[0], "notepad.exe"),
        ]);
        _fileSystem.DirectoryExists(StandardDirectories[1]).Returns(true);
        var expected = Path.Combine(StandardDirectories[1], "T3 Code (Canary).exe");
        _fileSystem.GetFiles(StandardDirectories[1], ExeSearchPattern).Returns([expected]);

        var result = CreateLocator().Locate();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expected);
    }

    [Fact]
    public void Locate_FallbackScanWithCaseVariantNames_OrdersDeterministically()
    {
        UseFallbackFiles([@"C:\d\t3 b.exe", @"C:\d\T3 a.exe", @"C:\d\T3 A.exe"]);

        var result = CreateLocator().Locate();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(@"C:\d\T3 A.exe");
    }

    [Theory]
    [InlineData(@"C:\d\T3 a.exe", @"C:\d\T3 A.exe", @"C:\d\t3 b.exe")]
    [InlineData(@"C:\d\T3 A.exe", @"C:\d\t3 b.exe", @"C:\d\T3 a.exe")]
    [InlineData(@"C:\d\t3 b.exe", @"C:\d\T3 A.exe", @"C:\d\T3 a.exe")]
    public void Locate_FallbackScanWithCaseVariantNamesInAnyInputOrder_ReturnsSameResult(string first, string second, string third)
    {
        UseFallbackFiles([first, second, third]);

        var result = CreateLocator().Locate();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(@"C:\d\T3 A.exe");
    }

    // ---------- Not found ----------

    [Fact]
    public void Locate_NothingFound_ReturnsNotFoundErrorListingEachProbedDirectory()
    {
        UseStandardBases();
        MarkAllStandardDirectoriesExisting();

        var result = CreateLocator().Locate();

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(NotFoundCode);
        foreach (var directory in StandardDirectories)
        {
            result.Error.Message.Should().Contain(directory);
        }

        result.Error.Message.Should().Contain(OverrideVariable);
    }

    [Fact]
    public void Locate_NoValidBases_ReturnsNotFoundErrorSayingNoInstallationDirectories()
    {
        _environment.GetLocalApplicationDataPath().Returns(@"relative\path");
        _environment.GetEnvironmentVariable(ProgramFilesVariable).Returns("   ");

        var result = CreateLocator().Locate();

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(NotFoundCode);
        result.Error.Message.Should().ContainEquivalentOf("no installation directories");
        result.Error.Message.Should().Contain(OverrideVariable);
        _fileSystem.DidNotReceive().DirectoryExists(Arg.Any<string>());
    }

    // ---------- Helpers ----------

    private T3DesktopExecutableLocator CreateLocator() => new(_environment, _fileSystem);

    private void UseStandardBases()
    {
        _environment.GetLocalApplicationDataPath().Returns(LocalAppData);
        _environment.GetEnvironmentVariable(ProgramFilesVariable).Returns(ProgramFiles);
        _environment.GetEnvironmentVariable(ProgramFilesX86Variable).Returns(ProgramFilesX86);
    }

    private void MarkAllStandardDirectoriesExisting()
    {
        foreach (var directory in StandardDirectories)
        {
            _fileSystem.DirectoryExists(directory).Returns(true);
        }
    }

    /// <summary>
    /// Makes the first standard directory exist with no exact-name match and routes its <c>*.exe</c> scan to
    /// <paramref name="files"/>, so the locator's fallback ordering alone decides the result.
    /// </summary>
    private void UseFallbackFiles(string[] files)
    {
        UseStandardBases();
        var firstDirectory = StandardDirectories[0];
        _fileSystem.DirectoryExists(firstDirectory).Returns(true);
        _fileSystem.GetFiles(firstDirectory, ExeSearchPattern).Returns(files);
    }

    private IReadOnlyList<string> FileExistsArguments() => ArgumentsOf(nameof(IFileSystem.FileExists));

    private IReadOnlyList<string> DirectoryExistsArguments() => ArgumentsOf(nameof(IFileSystem.DirectoryExists));

    private IReadOnlyList<string> ArgumentsOf(string methodName) =>
        _fileSystem.ReceivedCalls()
            .Where(call => call.GetMethodInfo().Name == methodName)
            .Select(call => (string)call.GetArguments()[0]!)
            .ToList();

    private static bool IsUnderStandardDirectory(string path) =>
        StandardDirectories.Any(directory =>
            string.Equals(Path.GetDirectoryName(path), directory, StringComparison.OrdinalIgnoreCase));
}
