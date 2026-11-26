using CreateAiStructure.Domain.Common;
using FluentAssertions;

namespace CreateAiStructure.Domain.Tests.Common;

public class ResultTests
{
    private static readonly DomainError SampleError = new("sample.failure", "Something went wrong.");

    [Fact]
    public void Success_is_successful_and_not_a_failure()
    {
        var result = Result.Success();

        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
    }

    [Fact]
    public void Success_throws_when_reading_error()
    {
        var result = Result.Success();

        var act = () => result.Error;

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Failure_is_a_failure_and_not_successful()
    {
        var result = Result.Failure(SampleError);

        result.IsFailure.Should().BeTrue();
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void Failure_exposes_the_given_error()
    {
        var result = Result.Failure(SampleError);

        result.Error.Should().Be(SampleError);
    }

    [Fact]
    public void Failure_rejects_a_null_error()
    {
        var act = () => Result.Failure(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
