using System.ComponentModel;
using CreateAiStructure.Infrastructure.Process;
using FluentAssertions;
using SysProcess = System.Diagnostics.Process;

namespace CreateAiStructure.Infrastructure.Tests.Process;

/// <summary>
/// Unit tests for <see cref="SystemProcessHandle.KillEntireProcessTree"/> best-effort semantics, driven through the
/// internal kill-delegate seam so every swallowed exception type is proven deterministically.
/// </summary>
public class SystemProcessHandleTests
{
    public static TheoryData<Exception> SwallowedKillExceptions() =>
    [
        new InvalidOperationException("already exited"),
        new Win32Exception(5, "access denied"),
        new AggregateException(new Win32Exception(5, "descendant access denied")),
    ];

    [Theory]
    [MemberData(nameof(SwallowedKillExceptions))]
    public void KillEntireProcessTree_swallows_best_effort_kill_failures(Exception failure)
    {
        using var handle = new SystemProcessHandle(new SysProcess(), _ => throw failure);

        var act = handle.KillEntireProcessTree;

        act.Should().NotThrow();
    }

    [Fact]
    public void KillEntireProcessTree_propagates_unrelated_exceptions()
    {
        using var handle = new SystemProcessHandle(new SysProcess(), _ => throw new NotSupportedException("remote"));

        var act = handle.KillEntireProcessTree;

        act.Should().Throw<NotSupportedException>().WithMessage("remote");
    }

    [Fact]
    public void KillEntireProcessTree_requests_the_entire_tree()
    {
        bool? requested = null;
        using var handle = new SystemProcessHandle(new SysProcess(), entireTree => requested = entireTree);

        handle.KillEntireProcessTree();

        requested.Should().BeTrue();
    }
}
