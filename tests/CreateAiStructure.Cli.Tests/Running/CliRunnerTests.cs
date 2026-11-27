using CreateAiStructure.Application.Orchestration;
using CreateAiStructure.Application.Runtime;
using CreateAiStructure.Cli.ConsoleIo;
using CreateAiStructure.Cli.Parsing;
using CreateAiStructure.Domain.Common;
using CreateAiStructure.Domain.Scaffolding;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace CreateAiStructure.Cli.Tests.Running;

public class CliRunnerTests
{
    private const string WorkingDirectory = @"C:\work";
    private const string Reminder = "Import the repository into T3 Code.";

    private static readonly string NewLine = Environment.NewLine;
    private static readonly string[] ScaffoldArgs = ["--type", "api", "--repo", "my-app"];

    private readonly IScaffoldOrchestrator _orchestrator = Substitute.For<IScaffoldOrchestrator>();
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();
    private readonly CliRunner _runner;

    public CliRunnerTests()
    {
        var environment = Substitute.For<IEnvironmentProvider>();
        environment.GetCurrentDirectory().Returns(WorkingDirectory);
        _runner = new CliRunner(new CliArgumentParser(environment), _orchestrator, new ConsoleStreams(_out, _error));
    }

    private static ScaffoldResult SuccessfulResult(bool startedT3) =>
        new(@"C:\work\my-app", ["AGENTS.md", "CLAUDE.md", "t3.json"], startedT3, Reminder);

    private void OrchestratorReturns(Result<ScaffoldResult> result) =>
        _orchestrator.ExecuteAsync(Arg.Any<ScaffoldRequest>(), Arg.Any<CancellationToken>()).Returns(result);

    private static string Lines(params string[] lines) => string.Concat(lines.Select(line => line + NewLine));

    // ---- Constructor & guards ----

    [Fact]
    public void Constructor_throws_when_parser_is_null()
    {
        var act = () => new CliRunner(null!, _orchestrator, new ConsoleStreams(_out, _error));

        act.Should().Throw<ArgumentNullException>().WithParameterName("parser");
    }

    [Fact]
    public void Constructor_throws_when_orchestrator_is_null()
    {
        var parser = new CliArgumentParser(Substitute.For<IEnvironmentProvider>());

        var act = () => new CliRunner(parser, null!, new ConsoleStreams(_out, _error));

        act.Should().Throw<ArgumentNullException>().WithParameterName("orchestrator");
    }

    [Fact]
    public void Constructor_throws_when_streams_is_null()
    {
        var parser = new CliArgumentParser(Substitute.For<IEnvironmentProvider>());

        var act = () => new CliRunner(parser, _orchestrator, null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("streams");
    }

    [Fact]
    public async Task RunAsync_throws_when_args_is_null()
    {
        var act = () => _runner.RunAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("args");
        _orchestrator.ReceivedCalls().Should().BeEmpty();
    }

    // ---- Help ----

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("--type", "api", "--help")]
    public async Task Help_writes_usage_to_stdout_and_returns_success(params string[] args)
    {
        var exitCode = await _runner.RunAsync(args);

        exitCode.Should().Be(CliExitCodes.Success);
        _out.ToString().Should().Be(Lines(CliUsage.Text));
        _error.ToString().Should().BeEmpty();
        _orchestrator.ReceivedCalls().Should().BeEmpty();
    }

    // ---- Version ----

    [Theory]
    [InlineData("--version")]
    [InlineData("-v")]
    public async Task Version_writes_the_tool_version_to_stdout_and_returns_success(string flag)
    {
        var exitCode = await _runner.RunAsync([flag]);

        exitCode.Should().Be(CliExitCodes.Success);
        _out.ToString().Should().Be(Lines("create-ai-structure 1.0.0"));
        _error.ToString().Should().BeEmpty();
        _orchestrator.ReceivedCalls().Should().BeEmpty();
    }

    // ---- Parse failure ----

    public static TheoryData<string[], string, string> ParseFailures => new()
    {
        { [], CliErrorCodes.MissingRequiredOptions, "The --type and --repo options are required." },
        { ["--bogus"], CliErrorCodes.UnknownArgument, "Unknown argument '--bogus'." },
        { ["--repo", "my-app"], CliErrorCodes.MissingRequiredOption, "The --type option is required." },
    };

    [Theory]
    [MemberData(nameof(ParseFailures))]
    public async Task Parse_failure_writes_error_blank_line_and_usage_to_stderr_and_returns_usage_error(
        string[] args, string code, string message)
    {
        var exitCode = await _runner.RunAsync(args);

        exitCode.Should().Be(CliExitCodes.UsageError);
        _error.ToString().Should().Be(Lines($"Error [{code}]: {message}", string.Empty, CliUsage.Text));
        _out.ToString().Should().BeEmpty();
        _orchestrator.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task Domain_validation_failure_is_reported_with_its_domain_code()
    {
        var exitCode = await _runner.RunAsync(["--type", "cobol", "--repo", "my-app"]);

        var expected = ProjectType.Parse("cobol").Error;
        exitCode.Should().Be(CliExitCodes.UsageError);
        _error.ToString().Should().StartWith($"Error [{expected.Code}]: {expected.Message}{NewLine}{NewLine}");
        _out.ToString().Should().BeEmpty();
        _orchestrator.ReceivedCalls().Should().BeEmpty();
    }

    // ---- Orchestrator failure ----

    [Fact]
    public async Task Orchestrator_failure_writes_the_error_to_stderr_and_returns_execution_failure()
    {
        OrchestratorReturns(Result<ScaffoldResult>.Failure(new DomainError("Tools.GitMissing", "git was not found.")));

        var exitCode = await _runner.RunAsync(ScaffoldArgs);

        exitCode.Should().Be(CliExitCodes.ExecutionFailure);
        _error.ToString().Should().Be(Lines("Error [Tools.GitMissing]: git was not found."));
        _out.ToString().Should().BeEmpty();
    }

    // ---- Success ----

    [Theory]
    [InlineData(true, "Started the T3 Code desktop app.")]
    [InlineData(false, "T3 Code desktop app was already running.")]
    public async Task Success_writes_the_summary_to_stdout_and_returns_success(bool startedT3, string t3Status)
    {
        OrchestratorReturns(Result<ScaffoldResult>.Success(SuccessfulResult(startedT3)));

        var exitCode = await _runner.RunAsync(ScaffoldArgs);

        exitCode.Should().Be(CliExitCodes.Success);
        _out.ToString().Should().Be(Lines(
            @"Repository created at C:\work\my-app",
            "Materialized 3 files.",
            t3Status,
            Reminder));
        _error.ToString().Should().BeEmpty();
    }

    [Fact]
    public async Task Scaffold_passes_the_parsed_request_and_cancellation_token_to_the_orchestrator()
    {
        OrchestratorReturns(Result<ScaffoldResult>.Success(SuccessfulResult(startedT3: true)));
        using var cts = new CancellationTokenSource();

        await _runner.RunAsync(ScaffoldArgs, cts.Token);

        await _orchestrator.Received(1).ExecuteAsync(
            Arg.Is<ScaffoldRequest>(request =>
                request.ProjectType == ProjectType.Api &&
                request.RepositoryName.Value == "my-app" &&
                request.WorkingDirectory == WorkingDirectory),
            cts.Token);
    }

    // ---- Cancellation & other exceptions ----

    [Fact]
    public async Task Cancellation_writes_cancelled_to_stderr_and_returns_130()
    {
        _orchestrator.ExecuteAsync(Arg.Any<ScaffoldRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        var exitCode = await _runner.RunAsync(ScaffoldArgs);

        exitCode.Should().Be(CliExitCodes.Cancelled);
        _error.ToString().Should().Be(Lines("Operation cancelled."));
        _out.ToString().Should().BeEmpty();
    }

    [Fact]
    public async Task Task_cancellation_is_also_reported_as_cancelled()
    {
        _orchestrator.ExecuteAsync(Arg.Any<ScaffoldRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException());

        var exitCode = await _runner.RunAsync(ScaffoldArgs);

        exitCode.Should().Be(CliExitCodes.Cancelled);
        _error.ToString().Should().Be(Lines("Operation cancelled."));
    }

    [Fact]
    public async Task Other_exceptions_propagate_without_output()
    {
        _orchestrator.ExecuteAsync(Arg.Any<ScaffoldRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("boom"));

        var act = () => _runner.RunAsync(ScaffoldArgs);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("boom");
        _out.ToString().Should().BeEmpty();
        _error.ToString().Should().BeEmpty();
    }
}
