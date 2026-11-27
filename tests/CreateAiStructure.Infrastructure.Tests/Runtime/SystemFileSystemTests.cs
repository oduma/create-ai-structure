using System.Text;
using CreateAiStructure.Infrastructure.Runtime;
using FluentAssertions;

namespace CreateAiStructure.Infrastructure.Tests.Runtime;

/// <summary>
/// Specification of <see cref="SystemFileSystem"/> against the real file system (Sub-Issue #35, AC-1).
/// Every test works inside its own <see cref="RuntimeTempDirectory"/>.
/// </summary>
public sealed class SystemFileSystemTests : IDisposable
{
    private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];

    private readonly RuntimeTempDirectory _temp = new();
    private readonly SystemFileSystem _sut = new();

    public void Dispose() => _temp.Dispose();

    public static TheoryData<string?> NullOrWhiteSpace => new() { null, string.Empty, " " };

    [Fact]
    public void DirectoryExists_ReturnsTrue_ForExistingDirectory()
    {
        _sut.DirectoryExists(_temp.Path).Should().BeTrue();
    }

    [Fact]
    public void DirectoryExists_ReturnsFalse_ForMissingDirectory()
    {
        _sut.DirectoryExists(_temp.Combine("missing")).Should().BeFalse();
    }

    [Fact]
    public void DirectoryExists_ReturnsFalse_ForFilePath()
    {
        var file = _temp.Combine("a.txt");
        File.WriteAllText(file, "x");

        _sut.DirectoryExists(file).Should().BeFalse();
    }

    [Fact]
    public void FileExists_ReturnsTrue_ForExistingFile()
    {
        var file = _temp.Combine("a.txt");
        File.WriteAllText(file, "x");

        _sut.FileExists(file).Should().BeTrue();
    }

    [Fact]
    public void FileExists_ReturnsFalse_ForMissingFile()
    {
        _sut.FileExists(_temp.Combine("missing.txt")).Should().BeFalse();
    }

    [Fact]
    public void FileExists_ReturnsFalse_ForDirectoryPath()
    {
        _sut.FileExists(_temp.Path).Should().BeFalse();
    }

    [Theory]
    [MemberData(nameof(NullOrWhiteSpace))]
    public void ExistenceChecks_ReturnFalse_ForNullOrBlankPath(string? path)
    {
        _sut.DirectoryExists(path!).Should().BeFalse();
        _sut.FileExists(path!).Should().BeFalse();
    }

    [Fact]
    public void GetFiles_ReturnsOnlyMatchingFullPaths()
    {
        var exe1 = _temp.Combine("one.exe");
        var exe2 = _temp.Combine("two.exe");
        var txt = _temp.Combine("notes.txt");
        File.WriteAllText(exe1, "1");
        File.WriteAllText(exe2, "2");
        File.WriteAllText(txt, "t");
        Directory.CreateDirectory(_temp.Combine("sub"));
        File.WriteAllText(_temp.Combine("sub", "nested.exe"), "n");

        var exes = _sut.GetFiles(_temp.Path, "*.exe");
        var txts = _sut.GetFiles(_temp.Path, "*.txt");

        exes.Should().BeEquivalentTo([exe1, exe2]);
        exes.Should().OnlyContain(p => Path.IsPathFullyQualified(p));
        txts.Should().ContainSingle().Which.Should().Be(txt);
    }

    [Fact]
    public void GetFiles_ReturnsEmpty_WhenNothingMatches()
    {
        File.WriteAllText(_temp.Combine("notes.txt"), "t");

        _sut.GetFiles(_temp.Path, "*.exe").Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(NullOrWhiteSpace))]
    public void GetFiles_Throws_ForNullOrBlankDirectoryPath(string? directoryPath)
    {
        Action act = () => _sut.GetFiles(directoryPath!, "*.exe");

        AssertGuard(act, directoryPath, "directoryPath");
    }

    [Theory]
    [MemberData(nameof(NullOrWhiteSpace))]
    public void GetFiles_Throws_ForNullOrBlankSearchPattern(string? searchPattern)
    {
        Action act = () => _sut.GetFiles(_temp.Path, searchPattern!);

        AssertGuard(act, searchPattern, "searchPattern");
    }

    [Fact]
    public void CreateDirectory_CreatesNestedDirectories()
    {
        var nested = _temp.Combine("a", "b", "c");

        _sut.CreateDirectory(nested);

        Directory.Exists(nested).Should().BeTrue();
    }

    [Fact]
    public void CreateDirectory_IsIdempotent_ForExistingDirectory()
    {
        var act = () => _sut.CreateDirectory(_temp.Path);

        act.Should().NotThrow();
        Directory.Exists(_temp.Path).Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(NullOrWhiteSpace))]
    public void CreateDirectory_Throws_ForNullOrBlankPath(string? path)
    {
        var act = () => _sut.CreateDirectory(path!);

        AssertGuard(act, path, "path");
    }

    [Fact]
    public async Task WriteAllTextUtf8NoBomAsync_RoundTripsNonAsciiContent_WithoutBom()
    {
        var file = _temp.Combine("out.md");
        const string content = "café üß 中文 ☃\nline2";

        await _sut.WriteAllTextUtf8NoBomAsync(file, content);

        var bytes = await File.ReadAllBytesAsync(file);
        bytes.Should().Equal(new UTF8Encoding(false).GetBytes(content));
        bytes.Take(Utf8Bom.Length).Should().NotEqual(Utf8Bom);
        (await File.ReadAllTextAsync(file, Encoding.UTF8)).Should().Be(content);
    }

    [Fact]
    public async Task WriteAllTextUtf8NoBomAsync_OverwritesExistingContent()
    {
        var file = _temp.Combine("out.txt");
        await File.WriteAllTextAsync(file, "a much longer original content that must disappear entirely");

        await _sut.WriteAllTextUtf8NoBomAsync(file, "short");

        (await File.ReadAllTextAsync(file)).Should().Be("short");
    }

    [Fact]
    public async Task WriteAllTextUtf8NoBomAsync_WritesEmptyFile_ForEmptyContent()
    {
        var file = _temp.Combine("empty.txt");

        await _sut.WriteAllTextUtf8NoBomAsync(file, string.Empty);

        (await File.ReadAllBytesAsync(file)).Should().BeEmpty();
    }

    [Fact]
    public async Task WriteAllTextUtf8NoBomAsync_Throws_ForPreCancelledToken()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = () => _sut.WriteAllTextUtf8NoBomAsync(_temp.Combine("cancelled.txt"), "content", cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Theory]
    [MemberData(nameof(NullOrWhiteSpace))]
    public async Task WriteAllTextUtf8NoBomAsync_Throws_ForNullOrBlankPath(string? path)
    {
        var act = () => _sut.WriteAllTextUtf8NoBomAsync(path!, "content");

        if (path is null)
        {
            (await act.Should().ThrowExactlyAsync<ArgumentNullException>()).Which.ParamName.Should().Be("path");
        }
        else
        {
            (await act.Should().ThrowExactlyAsync<ArgumentException>()).Which.ParamName.Should().Be("path");
        }
    }

    [Fact]
    public async Task WriteAllTextUtf8NoBomAsync_Throws_ForNullContent()
    {
        var file = _temp.Combine("null.txt");

        var act = () => _sut.WriteAllTextUtf8NoBomAsync(file, null!);

        (await act.Should().ThrowExactlyAsync<ArgumentNullException>()).Which.ParamName.Should().Be("content");
        File.Exists(file).Should().BeFalse();
    }

    private static void AssertGuard(Action act, string? value, string paramName)
    {
        if (value is null)
        {
            act.Should().ThrowExactly<ArgumentNullException>().Which.ParamName.Should().Be(paramName);
        }
        else
        {
            act.Should().ThrowExactly<ArgumentException>().Which.ParamName.Should().Be(paramName);
        }
    }
}
