using CreateAiStructure.Application.Runtime;
using CreateAiStructure.Application.Templates;
using CreateAiStructure.Domain.Common;
using CreateAiStructure.Domain.Scaffolding;
using CreateAiStructure.Domain.Templates;
using CreateAiStructure.Infrastructure.Templates;
using FluentAssertions;
using NSubstitute;
using NSubstitute.Core;

namespace CreateAiStructure.Infrastructure.Tests.Templates;

/// <summary>
/// Specifies <see cref="TemplateMaterializer"/> (Epic #2 FR-4, AC-6, AC-7; Sub-Issue #24): constructor and argument
/// null guards, target-directory validation, path containment, a zero-write pre-flight phase (existing files,
/// provider failures), <c>WriteFailed</c> mapping of IO failures, cancellation propagation, and exact per-type
/// materialization with the real embedded templates and renderer over a mocked <see cref="IFileSystem"/>.
/// </summary>
public class TemplateMaterializerTests
{
    private const string RepoNameValue = "materialized-repo";
    private const string ClaudeTemplatePath = "CLAUDE.md";
    private const string PoAgentTemplatePath = ".agents/agents/po/agent.md";
    private const string ConstitutionPrefix = "docs/constitution/";
    private const string AgentPrefix = ".claude/agents/";

    private static readonly string[] AngularOnlyFiles =
    [
        ".claude/agents/angular-frontend-developer.md",
        ".claude/agents/angular-material-ux.md",
        "docs/constitution/angular-net-api-architecture.md",
    ];

    private static readonly string[] MauiOnlyFiles =
    [
        ".claude/agents/MAUI_UX_ENGINEER.md",
        "docs/constitution/maui-architecture.md",
    ];

    private static readonly string[] AllTokens =
    [
        TemplateTokens.RepoNameLegacy,
        TemplateTokens.RepoName,
        TemplateTokens.RepoSlug,
        TemplateTokens.ConstitutionFilesList,
        TemplateTokens.AgentsList,
    ];

    private readonly string _root = Path.Combine(Path.GetTempPath(), "cas-materializer-tests", Guid.NewGuid().ToString("N"));
    private readonly ITemplateResourceProvider _provider = Substitute.For<ITemplateResourceProvider>();
    private readonly ITemplateRenderer _renderer = Substitute.For<ITemplateRenderer>();
    private readonly IFileSystem _fileSystem = Substitute.For<IFileSystem>();

    public TemplateMaterializerTests()
    {
        // Default port behaviour: every template reads as "raw:<path>", rendering is identity, nothing exists.
        _provider.ReadTemplateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(Result<string>.Success($"raw:{call.ArgAt<string>(0)}")));
        _renderer.Render(Arg.Any<string>(), Arg.Any<TemplateRenderContext>())
            .Returns(call => call.ArgAt<string>(0));
        _fileSystem.FileExists(Arg.Any<string>()).Returns(false);
    }

    public static TheoryData<string, int> ProjectTypesWithFileCounts() => new()
    {
        { "webApp", 24 },
        { "api", 21 },
        { "console", 21 },
        { "MAUI", 23 },
    };

    public static TheoryData<string?> BlankTargetDirectories() => new()
    {
        null,
        "",
        "  ",
    };

    // ---------------------------------------------------------------- contract & constructor guards

    [Fact]
    public void TemplateMaterializer_implements_ITemplateMaterializer()
    {
        CreateSut().Should().BeAssignableTo<ITemplateMaterializer>();
    }

    [Fact]
    public void Constructor_throws_ArgumentNullException_when_resourceProvider_is_null()
    {
        var act = () => new TemplateMaterializer(null!, _renderer, _fileSystem);

        act.Should().Throw<ArgumentNullException>().WithParameterName("resourceProvider");
    }

    [Fact]
    public void Constructor_throws_ArgumentNullException_when_renderer_is_null()
    {
        var act = () => new TemplateMaterializer(_provider, null!, _fileSystem);

        act.Should().Throw<ArgumentNullException>().WithParameterName("renderer");
    }

    [Fact]
    public void Constructor_throws_ArgumentNullException_when_fileSystem_is_null()
    {
        var act = () => new TemplateMaterializer(_provider, _renderer, null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("fileSystem");
    }

    // ---------------------------------------------------------------- argument null guards

    [Fact]
    public async Task MaterializeAsync_throws_ArgumentNullException_when_manifest_is_null()
    {
        var act = () => CreateSut().MaterializeAsync(null!, ContextFor(ProjectType.Api), _root);

        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("manifest");
        AssertNoFileSystemCalls();
    }

    [Fact]
    public async Task MaterializeAsync_throws_ArgumentNullException_when_context_is_null()
    {
        var act = () => CreateSut().MaterializeAsync(TemplateManifest.For(ProjectType.Api), null!, _root);

        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("context");
        AssertNoFileSystemCalls();
    }

    // ---------------------------------------------------------------- target directory validation

    [Theory]
    [MemberData(nameof(BlankTargetDirectories))]
    public async Task MaterializeAsync_fails_with_TargetDirectoryRequired_for_a_blank_target_directory(string? targetDirectory)
    {
        var result = await CreateSut().MaterializeAsync(
            TemplateManifest.For(ProjectType.Api), ContextFor(ProjectType.Api), targetDirectory!);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("TemplateMaterializer.TargetDirectoryRequired");
        AssertNoWritesOrDirectories();
        await _provider.DidNotReceiveWithAnyArgs().ReadTemplateAsync(default!, default);
    }

    [Fact]
    public async Task MaterializeAsync_fails_with_InvalidTargetDirectory_when_the_target_directory_cannot_be_normalized()
    {
        var result = await CreateSut().MaterializeAsync(
            TemplateManifest.For(ProjectType.Api), ContextFor(ProjectType.Api), "a\0b");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("TemplateMaterializer.InvalidTargetDirectory");
        AssertNoWritesOrDirectories();
        await _provider.DidNotReceiveWithAnyArgs().ReadTemplateAsync(default!, default);
    }

    // ---------------------------------------------------------------- path containment (internal seam)

    [Fact]
    public async Task MaterializeAsync_fails_with_UnsafeTemplatePath_for_a_traversal_path_and_writes_nothing()
    {
        var result = await CreateSut().MaterializeAsync(
            new[] { ClaudeTemplatePath, "../escape" },
            ContextFor(ProjectType.Api),
            _root,
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("TemplateMaterializer.UnsafeTemplatePath");
        result.Error.Message.Should().Contain("../escape");
        AssertNoWritesOrDirectories();
        await _provider.DidNotReceive().ReadTemplateAsync("../escape", Arg.Any<CancellationToken>());
    }

    // ---------------------------------------------------------------- pre-flight: duplicate destinations (internal seam)

    public static TheoryData<string, string> DuplicateDestinationPairs() => new()
    {
        { ClaudeTemplatePath, ClaudeTemplatePath },
        { ClaudeTemplatePath, "claude.md" },
    };

    [Theory]
    [MemberData(nameof(DuplicateDestinationPairs))]
    public async Task MaterializeAsync_fails_with_DuplicateDestination_when_two_paths_resolve_to_the_same_destination_ignoring_case_and_writes_nothing(
        string firstPath, string secondPath)
    {
        // Default mocks: FileExists is false and the provider returns content for every name, so only the
        // duplicate check can fail this call. The comparison is case-insensitive on every OS.
        var result = await CreateSut().MaterializeAsync(
            new[] { firstPath, secondPath },
            ContextFor(ProjectType.Api),
            _root,
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("TemplateMaterializer.DuplicateDestination");
        AssertNoWritesOrDirectories();
    }

    [Fact]
    public async Task MaterializeAsync_names_the_duplicate_path_in_the_DuplicateDestination_failure_message()
    {
        var result = await CreateSut().MaterializeAsync(
            new[] { ClaudeTemplatePath, "claude.md" },
            ContextFor(ProjectType.Api),
            _root,
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("TemplateMaterializer.DuplicateDestination");
        result.Error.Message.Should().Match(message =>
            message.Contains("claude.md", StringComparison.Ordinal)
            || message.Contains(DestinationOf("claude.md"), StringComparison.Ordinal));
    }

    // ---------------------------------------------------------------- pre-flight: existing target file

    [Fact]
    public async Task MaterializeAsync_fails_with_TargetFileAlreadyExists_when_the_last_manifest_file_exists_and_writes_nothing()
    {
        var manifest = TemplateManifest.For(ProjectType.Api);
        var lastFile = manifest.Files[^1];
        var lastDestination = DestinationOf(lastFile);
        _fileSystem.FileExists(lastDestination).Returns(true);

        var result = await CreateSut().MaterializeAsync(manifest, ContextFor(ProjectType.Api), _root);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("TemplateMaterializer.TargetFileAlreadyExists");
        result.Error.Message.Should().Match(message =>
            message.Contains(lastFile, StringComparison.Ordinal)
            || message.Contains(lastDestination, StringComparison.Ordinal));
        AssertNoWritesOrDirectories();

        // Every earlier file passed the existence check during the pre-flight phase.
        foreach (var file in manifest.Files)
        {
            _fileSystem.Received(1).FileExists(DestinationOf(file));
        }
    }

    // ---------------------------------------------------------------- pre-flight: provider failure

    [Fact]
    public async Task MaterializeAsync_returns_the_provider_error_unchanged_and_writes_nothing_when_a_template_fails_to_load()
    {
        var manifest = TemplateManifest.For(ProjectType.Api);
        var error = new DomainError("TemplateResource.NotFound", "Template 'AGENTS.md' was not found.");
        _provider.ReadTemplateAsync("AGENTS.md", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<string>.Failure(error)));

        var result = await CreateSut().MaterializeAsync(manifest, ContextFor(ProjectType.Api), _root);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeSameAs(error);
        AssertNoWritesOrDirectories();
    }

    // ---------------------------------------------------------------- rendering of every file

    [Fact]
    public async Task MaterializeAsync_renders_every_manifest_file_with_the_supplied_context_and_writes_the_rendered_content()
    {
        var manifest = TemplateManifest.For(ProjectType.Console);
        var context = ContextFor(ProjectType.Console);
        _renderer.Render(Arg.Any<string>(), Arg.Any<TemplateRenderContext>())
            .Returns(call => $"rendered:{call.ArgAt<string>(0)}");

        var result = await CreateSut().MaterializeAsync(manifest, context, _root);

        result.IsSuccess.Should().BeTrue();
        foreach (var file in manifest.Files)
        {
            _renderer.Received(1).Render($"raw:{file}", context);
        }

        Writes().Should().Equal(manifest.Files.Select(file => (DestinationOf(file), $"rendered:raw:{file}")));
    }

    [Fact]
    public async Task MaterializeAsync_completes_the_pre_flight_phase_for_every_file_before_creating_any_directory()
    {
        var manifest = TemplateManifest.For(ProjectType.Api);

        var result = await CreateSut().MaterializeAsync(manifest, ContextFor(ProjectType.Api), _root);

        result.IsSuccess.Should().BeTrue();
        var callNames = _fileSystem.ReceivedCalls().Select(call => call.GetMethodInfo().Name).ToList();
        var lastExistenceCheck = callNames.LastIndexOf(nameof(IFileSystem.FileExists));
        var firstDirectoryCreation = callNames.IndexOf(nameof(IFileSystem.CreateDirectory));
        lastExistenceCheck.Should().BeLessThan(firstDirectoryCreation);
    }

    // ---------------------------------------------------------------- write phase failures

    [Fact]
    public async Task MaterializeAsync_fails_with_WriteFailed_when_CreateDirectory_throws_IOException()
    {
        _fileSystem.When(fs => fs.CreateDirectory(Arg.Any<string>())).Do(_ => throw new IOException("disk is full"));

        var result = await MaterializeApiAsync();

        AssertWriteFailed(result, DestinationOf(TemplateManifest.For(ProjectType.Api).Files[0]), "disk is full");
        Writes().Should().BeEmpty();
    }

    [Fact]
    public async Task MaterializeAsync_fails_with_WriteFailed_when_CreateDirectory_throws_UnauthorizedAccessException()
    {
        _fileSystem.When(fs => fs.CreateDirectory(Arg.Any<string>()))
            .Do(_ => throw new UnauthorizedAccessException("access denied"));

        var result = await MaterializeApiAsync();

        AssertWriteFailed(result, DestinationOf(TemplateManifest.For(ProjectType.Api).Files[0]), "access denied");
        Writes().Should().BeEmpty();
    }

    [Fact]
    public async Task MaterializeAsync_fails_with_WriteFailed_naming_the_destination_when_a_write_throws_IOException()
    {
        var failingDestination = DestinationOf(TemplateManifest.For(ProjectType.Api).Files[3]);
        _fileSystem.WriteAllTextUtf8NoBomAsync(failingDestination, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new IOException("sharing violation"));

        var result = await MaterializeApiAsync();

        AssertWriteFailed(result, failingDestination, "sharing violation");
        Writes().Should().HaveCount(4, "the first three files were written before the fourth failed");
    }

    [Fact]
    public async Task MaterializeAsync_fails_with_WriteFailed_naming_the_destination_when_a_write_throws_UnauthorizedAccessException()
    {
        var failingDestination = DestinationOf(TemplateManifest.For(ProjectType.Api).Files[3]);
        _fileSystem.WriteAllTextUtf8NoBomAsync(failingDestination, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new UnauthorizedAccessException("read-only file"));

        var result = await MaterializeApiAsync();

        AssertWriteFailed(result, failingDestination, "read-only file");
    }

    [Fact]
    public async Task MaterializeAsync_fails_with_WriteFailed_when_a_write_returns_a_task_faulted_with_IOException()
    {
        var failingDestination = DestinationOf(TemplateManifest.For(ProjectType.Api).Files[0]);
        _fileSystem.WriteAllTextUtf8NoBomAsync(failingDestination, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new IOException("async disk failure")));

        var result = await MaterializeApiAsync();

        AssertWriteFailed(result, failingDestination, "async disk failure");
    }

    [Fact]
    public async Task MaterializeAsync_fails_with_WriteFailed_when_a_write_returns_a_task_faulted_with_UnauthorizedAccessException()
    {
        var failingDestination = DestinationOf(TemplateManifest.For(ProjectType.Api).Files[0]);
        _fileSystem.WriteAllTextUtf8NoBomAsync(failingDestination, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new UnauthorizedAccessException("async access denied")));

        var result = await MaterializeApiAsync();

        AssertWriteFailed(result, failingDestination, "async access denied");
    }

    // ---------------------------------------------------------------- cancellation

    [Fact]
    public async Task MaterializeAsync_throws_OperationCanceledException_for_a_pre_cancelled_token_without_touching_any_port()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = () => CreateSut().MaterializeAsync(
            TemplateManifest.For(ProjectType.Api), ContextFor(ProjectType.Api), _root, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        AssertNoFileSystemCalls();
        await _provider.DidNotReceiveWithAnyArgs().ReadTemplateAsync(default!, default);
    }

    [Fact]
    public async Task MaterializeAsync_checks_cancellation_before_validating_the_target_directory()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = () => CreateSut().MaterializeAsync(
            TemplateManifest.For(ProjectType.Api), ContextFor(ProjectType.Api), "  ", cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task MaterializeAsync_throws_OperationCanceledException_when_cancelled_during_the_pre_flight_phase()
    {
        using var cts = new CancellationTokenSource();
        _provider.ReadTemplateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                cts.Cancel();
                return Task.FromResult(Result<string>.Success($"raw:{call.ArgAt<string>(0)}"));
            });

        var act = () => CreateSut().MaterializeAsync(
            TemplateManifest.For(ProjectType.Api), ContextFor(ProjectType.Api), _root, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        await _provider.ReceivedWithAnyArgs(1).ReadTemplateAsync(default!, default);
        AssertNoWritesOrDirectories();
    }

    [Fact]
    public async Task MaterializeAsync_throws_OperationCanceledException_when_cancelled_during_the_write_phase_before_the_next_file()
    {
        using var cts = new CancellationTokenSource();
        _fileSystem.WriteAllTextUtf8NoBomAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                cts.Cancel();
                return Task.CompletedTask;
            });

        var act = () => CreateSut().MaterializeAsync(
            TemplateManifest.For(ProjectType.Api), ContextFor(ProjectType.Api), _root, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        Writes().Should().HaveCount(1);
        _fileSystem.ReceivedWithAnyArgs(1).CreateDirectory(default!);
    }

    [Fact]
    public async Task MaterializeAsync_propagates_a_cancelled_write_instead_of_mapping_it_to_WriteFailed()
    {
        _fileSystem.WriteAllTextUtf8NoBomAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromCanceled(new CancellationToken(canceled: true)));

        var act = () => MaterializeApiAsync();

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task MaterializeAsync_passes_the_cancellation_token_to_the_provider_and_the_file_system()
    {
        using var cts = new CancellationTokenSource();
        var manifest = TemplateManifest.For(ProjectType.Api);

        var result = await CreateSut().MaterializeAsync(manifest, ContextFor(ProjectType.Api), _root, cts.Token);

        result.IsSuccess.Should().BeTrue();
        await _provider.Received(manifest.Files.Count).ReadTemplateAsync(Arg.Any<string>(), cts.Token);
        await _fileSystem.Received(manifest.Files.Count)
            .WriteAllTextUtf8NoBomAsync(Arg.Any<string>(), Arg.Any<string>(), cts.Token);
    }

    // ---------------------------------------------------------------- success with real templates

    [Theory]
    [MemberData(nameof(ProjectTypesWithFileCounts))]
    public async Task MaterializeAsync_writes_the_exact_per_type_file_set_in_manifest_order(string projectTypeName, int expectedCount)
    {
        var projectType = Parse(projectTypeName);
        var manifest = TemplateManifest.For(projectType);

        var result = await CreateRealSut().MaterializeAsync(manifest, ContextFor(projectType), _root);

        result.IsSuccess.Should().BeTrue();
        var expectedPaths = manifest.Files.Select(file => Path.GetFullPath(Path.Combine(_root, file))).ToList();
        result.Value.Should().HaveCount(expectedCount);
        result.Value.Should().Equal(expectedPaths);
        Writes().Select(write => write.Path).Should().Equal(expectedPaths);
        AssertProjectSpecificPaths(manifest.Files, projectType);
    }

    [Theory]
    [MemberData(nameof(ProjectTypesWithFileCounts))]
    public async Task MaterializeAsync_returns_a_read_only_list(string projectTypeName, int expectedCount)
    {
        var projectType = Parse(projectTypeName);

        var result = await CreateRealSut().MaterializeAsync(TemplateManifest.For(projectType), ContextFor(projectType), _root);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(expectedCount);
        var asList = (IList<string>)result.Value;
        asList.IsReadOnly.Should().BeTrue();
        ((Action)(() => asList.Add("extra"))).Should().Throw<NotSupportedException>();
        ((Action)(() => asList[0] = "replaced")).Should().Throw<NotSupportedException>();
        ((Action)(() => asList.RemoveAt(0))).Should().Throw<NotSupportedException>();
        ((Action)(() => asList.Clear())).Should().Throw<NotSupportedException>();
    }

    [Theory]
    [MemberData(nameof(ProjectTypesWithFileCounts))]
    public async Task MaterializeAsync_renders_CLAUDE_md_with_the_repository_name_and_the_exact_per_type_lists(
        string projectTypeName, int expectedCount)
    {
        var projectType = Parse(projectTypeName);
        var manifest = TemplateManifest.For(projectType);

        var result = await CreateRealSut().MaterializeAsync(manifest, ContextFor(projectType), _root);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(expectedCount);
        var claude = WrittenContentOf(ClaudeTemplatePath);
        var newline = claude.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";

        claude.Should().NotContain("{{");
        claude.Should().Contain(RepoNameValue);
        claude.Should().Contain(string.Join(newline, manifest.ConstitutionFiles));
        claude.Should().Contain(string.Join(newline, manifest.AgentFiles));

        var lines = claude.Split(newline);
        lines.Where(line => line.StartsWith(ConstitutionPrefix, StringComparison.Ordinal))
            .Should().Equal(manifest.ConstitutionFiles);
        lines.Where(line => line.StartsWith(AgentPrefix, StringComparison.Ordinal))
            .Should().Equal(manifest.AgentFiles);

        foreach (var angularFile in AngularOnlyFiles)
        {
            claude.Contains(angularFile, StringComparison.Ordinal).Should().Be(projectType == ProjectType.WebApp, angularFile);
        }

        foreach (var mauiFile in MauiOnlyFiles)
        {
            claude.Contains(mauiFile, StringComparison.Ordinal).Should().Be(projectType == ProjectType.Maui, mauiFile);
        }
    }

    [Theory]
    [MemberData(nameof(ProjectTypesWithFileCounts))]
    public async Task MaterializeAsync_renders_the_po_agent_with_the_repository_name(string projectTypeName, int expectedCount)
    {
        var projectType = Parse(projectTypeName);

        var result = await CreateRealSut().MaterializeAsync(TemplateManifest.For(projectType), ContextFor(projectType), _root);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(expectedCount);
        var poAgent = WrittenContentOf(PoAgentTemplatePath);
        poAgent.Should().Contain(RepoNameValue);
        poAgent.Should().NotContain(TemplateTokens.RepoNameLegacy);
        poAgent.Should().NotContain("{{");
    }

    [Theory]
    [MemberData(nameof(ProjectTypesWithFileCounts))]
    public async Task MaterializeAsync_writes_non_tokenized_files_verbatim_and_tokenized_files_rendered(
        string projectTypeName, int expectedCount)
    {
        var projectType = Parse(projectTypeName);
        var manifest = TemplateManifest.For(projectType);
        var context = ContextFor(projectType);
        var realProvider = new EmbeddedTemplateResourceProvider();
        var realRenderer = new TemplateRenderer();

        var result = await CreateRealSut().MaterializeAsync(manifest, context, _root);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(expectedCount);
        var nonTokenizedCount = 0;
        foreach (var file in manifest.Files)
        {
            var raw = (await realProvider.ReadTemplateAsync(file)).Value;
            var written = WrittenContentOf(file);
            if (AllTokens.Any(token => raw.Contains(token, StringComparison.Ordinal)))
            {
                written.Should().Be(realRenderer.Render(raw, context), file);
            }
            else
            {
                nonTokenizedCount++;
                written.Should().Be(raw, $"'{file}' has no tokens and must be written verbatim");
            }
        }

        nonTokenizedCount.Should().BePositive();
    }

    [Theory]
    [MemberData(nameof(ProjectTypesWithFileCounts))]
    public async Task MaterializeAsync_writes_every_file_through_WriteAllTextUtf8NoBomAsync_after_creating_its_parent_directory(
        string projectTypeName, int expectedCount)
    {
        var projectType = Parse(projectTypeName);
        var manifest = TemplateManifest.For(projectType);

        var result = await CreateRealSut().MaterializeAsync(manifest, ContextFor(projectType), _root);

        result.IsSuccess.Should().BeTrue();
        Writes().Should().HaveCount(expectedCount);
        var calls = _fileSystem.ReceivedCalls().ToList();
        foreach (var file in manifest.Files)
        {
            var destination = DestinationOf(file);
            var parent = Path.GetDirectoryName(destination)!;
            _fileSystem.Received(1).FileExists(destination);
            _fileSystem.Received().CreateDirectory(parent);
            await _fileSystem.Received(1)
                .WriteAllTextUtf8NoBomAsync(destination, Arg.Any<string>(), Arg.Any<CancellationToken>());

            var writeIndex = calls.FindIndex(call => IsCall(call, nameof(IFileSystem.WriteAllTextUtf8NoBomAsync), destination));
            var directoryIndex = calls.FindIndex(call => IsCall(call, nameof(IFileSystem.CreateDirectory), parent));
            directoryIndex.Should().BeInRange(0, writeIndex - 1, $"the parent of '{file}' must be created before it is written");
        }

        _fileSystem.Received().CreateDirectory(Path.GetFullPath(_root));
        _fileSystem.DidNotReceiveWithAnyArgs().GetFiles(default!, default!);
    }

    // ---------------------------------------------------------------- helpers

    private TemplateMaterializer CreateSut() => new(_provider, _renderer, _fileSystem);

    private TemplateMaterializer CreateRealSut() =>
        new(new EmbeddedTemplateResourceProvider(), new TemplateRenderer(), _fileSystem);

    private Task<Result<IReadOnlyList<string>>> MaterializeApiAsync() =>
        CreateSut().MaterializeAsync(TemplateManifest.For(ProjectType.Api), ContextFor(ProjectType.Api), _root);

    private string DestinationOf(string relativePath) => Path.GetFullPath(Path.Combine(_root, relativePath));

    private List<(string Path, string Content)> Writes() =>
        _fileSystem.ReceivedCalls()
            .Where(call => call.GetMethodInfo().Name == nameof(IFileSystem.WriteAllTextUtf8NoBomAsync))
            .Select(call => ((string)call.GetArguments()[0]!, (string)call.GetArguments()[1]!))
            .ToList();

    private string WrittenContentOf(string relativePath)
    {
        var destination = DestinationOf(relativePath);
        var matches = Writes().Where(write => write.Path == destination).ToList();
        matches.Should().ContainSingle($"'{relativePath}' must be written exactly once");
        return matches[0].Content;
    }

    private static bool IsCall(ICall call, string methodName, string firstArgument) =>
        call.GetMethodInfo().Name == methodName && (string?)call.GetArguments()[0] == firstArgument;

    private void AssertNoWritesOrDirectories()
    {
        _fileSystem.DidNotReceiveWithAnyArgs().CreateDirectory(default!);
        _ = _fileSystem.DidNotReceiveWithAnyArgs().WriteAllTextUtf8NoBomAsync(default!, default!, default);
    }

    private void AssertNoFileSystemCalls() => _fileSystem.ReceivedCalls().Should().BeEmpty();

    private static void AssertWriteFailed(Result<IReadOnlyList<string>> result, string destination, string exceptionMessage)
    {
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("TemplateMaterializer.WriteFailed");
        result.Error.Message.Should().Contain(destination);
        result.Error.Message.Should().Contain(exceptionMessage);
    }

    // Angular files appear only for webApp and MAUI files only for MAUI; neither appears for api/console.
    private static void AssertProjectSpecificPaths(IReadOnlyList<string> files, ProjectType projectType)
    {
        foreach (var angularFile in AngularOnlyFiles)
        {
            files.Contains(angularFile).Should().Be(projectType == ProjectType.WebApp, angularFile);
        }

        foreach (var mauiFile in MauiOnlyFiles)
        {
            files.Contains(mauiFile).Should().Be(projectType == ProjectType.Maui, mauiFile);
        }
    }

    private static ProjectType Parse(string projectTypeName) => ProjectType.Parse(projectTypeName).Value;

    private static TemplateRenderContext ContextFor(ProjectType projectType) =>
        new(RepositoryName.Create(RepoNameValue).Value, TemplateManifest.For(projectType));
}
