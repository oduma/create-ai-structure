using CreateAiStructure.Application.Orchestration;
using CreateAiStructure.Domain.Common;
using CreateAiStructure.Domain.Scaffolding;
using FluentAssertions;
using NSubstitute;

namespace CreateAiStructure.Application.Tests.Orchestration;

public class OrchestrationContractTests
{
    [Fact]
    public async Task Scaffold_orchestrator_is_mockable()
    {
        var orchestrator = Substitute.For<IScaffoldOrchestrator>();
        var request = ScaffoldRequest.Create(ProjectType.Console, RepositoryName.Create("repo").Value, @"C:\work").Value;
        var expected = Result<ScaffoldResult>.Success(
            new ScaffoldResult(@"C:\work\repo", [@"C:\work\repo\CLAUDE.md"], false, "reminder"));
        using var cancellation = new CancellationTokenSource();
        orchestrator.ExecuteAsync(request, cancellation.Token).Returns(Task.FromResult(expected));
        orchestrator.ExecuteAsync(request).Returns(Task.FromResult(expected));

        var withToken = await orchestrator.ExecuteAsync(request, cancellation.Token);
        var withDefault = await orchestrator.ExecuteAsync(request);

        withToken.Should().BeSameAs(expected);
        withDefault.Should().BeSameAs(expected);
        _ = orchestrator.Received(1).ExecuteAsync(request, cancellation.Token);
        _ = orchestrator.Received(1).ExecuteAsync(request);
    }
}
