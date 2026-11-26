using CreateAiStructure.Domain.Common;
using FluentAssertions;

namespace CreateAiStructure.Domain.Tests.Common;

public class ResultOfTTests
{
    private static readonly DomainError SampleError = new("sample.failure", "Something went wrong.");

    [Theory]
    [InlineData(0)]
    [InlineData(42)]
    [InlineData(-7)]
    public void Success_exposes_the_given_value(int value)
    {
        var result = Result<int>.Success(value);

        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Value.Should().Be(value);
    }

    [Fact]
    public void Success_throws_when_reading_error()
    {
        var result = Result<int>.Success(42);

        var act = () => result.Error;

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Success_allows_a_null_value_for_a_nullable_type()
    {
        var result = Result<string?>.Success(null);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
    }

    [Fact]
    public void Failure_is_a_failure_and_not_successful()
    {
        var result = Result<int>.Failure(SampleError);

        result.IsFailure.Should().BeTrue();
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void Failure_exposes_the_given_error()
    {
        var result = Result<int>.Failure(SampleError);

        result.Error.Should().Be(SampleError);
    }

    [Fact]
    public void Failure_throws_when_reading_value()
    {
        var result = Result<int>.Failure(SampleError);

        var act = () => result.Value;

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Failure_rejects_a_null_error()
    {
        var act = () => Result<int>.Failure(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
