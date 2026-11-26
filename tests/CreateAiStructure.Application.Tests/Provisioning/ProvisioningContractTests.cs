using CreateAiStructure.Application.Provisioning;
using CreateAiStructure.Domain.Common;
using CreateAiStructure.Domain.Scaffolding;
using CreateAiStructure.Domain.Tooling;
using CreateAiStructure.Domain.Workflow;
using FluentAssertions;
using NSubstitute;

namespace CreateAiStructure.Application.Tests.Provisioning;

public class ProvisioningContractTests
{
    [Fact]
    public async Task Tool_validator_is_mockable()
    {
        var validator = Substitute.For<IToolValidator>();
        var requiredTools = RequiredTools.All;
        var expected = new ToolValidationResult(requiredTools, []);
        using var cancellation = new CancellationTokenSource();
        validator.ValidateRequiredToolsAsync(requiredTools, cancellation.Token).Returns(Task.FromResult(expected));
        validator.ValidateRequiredToolsAsync(requiredTools).Returns(Task.FromResult(expected));

        var withToken = await validator.ValidateRequiredToolsAsync(requiredTools, cancellation.Token);
        var withDefault = await validator.ValidateRequiredToolsAsync(requiredTools);

        withToken.Should().BeSameAs(expected);
        withDefault.Should().BeSameAs(expected);
        _ = validator.Received(1).ValidateRequiredToolsAsync(requiredTools, cancellation.Token);
        _ = validator.Received(1).ValidateRequiredToolsAsync(requiredTools);
    }

    [Fact]
    public async Task Repository_bootstrapper_is_mockable()
    {
        var bootstrapper = Substitute.For<IRepositoryBootstrapper>();
        var repositoryName = RepositoryName.Create("repo").Value;
        var labels = WorkflowLabelCatalog.All;
        var cloneResult = Result<RepositoryCloneResult>.Success(new RepositoryCloneResult(@"C:\work\repo", "owner/repo"));
        var finalizeResult = Result.Success();
        using var cancellation = new CancellationTokenSource();
        bootstrapper.CreateAndCloneRepositoryAsync(repositoryName, @"C:\work", cancellation.Token)
            .Returns(Task.FromResult(cloneResult));
        bootstrapper.CreateAndCloneRepositoryAsync(repositoryName, @"C:\other")
            .Returns(Task.FromResult(cloneResult));
        bootstrapper.FinalizeRepositoryAsync(@"C:\work\repo", labels, cancellation.Token)
            .Returns(Task.FromResult(finalizeResult));
        bootstrapper.FinalizeRepositoryAsync(@"C:\other\repo", labels)
            .Returns(Task.FromResult(finalizeResult));

        var clonedWithToken = await bootstrapper.CreateAndCloneRepositoryAsync(repositoryName, @"C:\work", cancellation.Token);
        var clonedWithDefault = await bootstrapper.CreateAndCloneRepositoryAsync(repositoryName, @"C:\other");
        var finalizedWithToken = await bootstrapper.FinalizeRepositoryAsync(@"C:\work\repo", labels, cancellation.Token);
        var finalizedWithDefault = await bootstrapper.FinalizeRepositoryAsync(@"C:\other\repo", labels);

        clonedWithToken.Should().BeSameAs(cloneResult);
        clonedWithDefault.Should().BeSameAs(cloneResult);
        finalizedWithToken.Should().BeSameAs(finalizeResult);
        finalizedWithDefault.Should().BeSameAs(finalizeResult);
        _ = bootstrapper.Received(1).CreateAndCloneRepositoryAsync(repositoryName, @"C:\work", cancellation.Token);
        _ = bootstrapper.Received(1).CreateAndCloneRepositoryAsync(repositoryName, @"C:\other");
        _ = bootstrapper.Received(1).FinalizeRepositoryAsync(@"C:\work\repo", labels, cancellation.Token);
        _ = bootstrapper.Received(1).FinalizeRepositoryAsync(@"C:\other\repo", labels);
    }
}
