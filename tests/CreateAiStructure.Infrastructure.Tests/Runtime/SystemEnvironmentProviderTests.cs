using CreateAiStructure.Infrastructure.Runtime;
using FluentAssertions;

namespace CreateAiStructure.Infrastructure.Tests.Runtime;

/// <summary>
/// Specification of <see cref="SystemEnvironmentProvider"/> against the real process environment (Sub-Issue #35, AC-1).
/// Environment variable names are unique per test so the tests are safe to run in parallel.
/// </summary>
public sealed class SystemEnvironmentProviderTests
{
    private readonly SystemEnvironmentProvider _sut = new();

    public static TheoryData<string?> NullOrWhiteSpace => new() { null, string.Empty, " " };

    [Fact]
    public void GetEnvironmentVariable_ReturnsValue_SetByTest()
    {
        var name = UniqueName();
        const string value = "runtime-adapter-value";
        Environment.SetEnvironmentVariable(name, value);
        try
        {
            _sut.GetEnvironmentVariable(name).Should().Be(value);
        }
        finally
        {
            Environment.SetEnvironmentVariable(name, null);
        }
    }

    [Fact]
    public void GetEnvironmentVariable_ReturnsNull_ForUnsetVariable()
    {
        _sut.GetEnvironmentVariable(UniqueName()).Should().BeNull();
    }

    [Theory]
    [MemberData(nameof(NullOrWhiteSpace))]
    public void GetEnvironmentVariable_Throws_ForNullOrBlankName(string? name)
    {
        var act = () => _sut.GetEnvironmentVariable(name!);

        if (name is null)
        {
            act.Should().ThrowExactly<ArgumentNullException>().Which.ParamName.Should().Be("name");
        }
        else
        {
            act.Should().ThrowExactly<ArgumentException>().Which.ParamName.Should().Be("name");
        }
    }

    [Fact]
    public void GetLocalApplicationDataPath_MatchesEnvironmentFolderPath()
    {
        _sut.GetLocalApplicationDataPath()
            .Should().Be(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
    }

    [Fact]
    public void GetCurrentDirectory_MatchesEnvironmentCurrentDirectory()
    {
        _sut.GetCurrentDirectory().Should().Be(Environment.CurrentDirectory);
    }

    private static string UniqueName() => $"CAS_RUNTIME_TEST_{Guid.NewGuid():N}";
}
