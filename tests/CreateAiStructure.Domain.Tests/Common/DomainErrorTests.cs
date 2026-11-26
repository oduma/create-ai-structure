using CreateAiStructure.Domain.Common;
using FluentAssertions;

namespace CreateAiStructure.Domain.Tests.Common;

public class DomainErrorTests
{
    private const string ValidCode = "config.invalid";
    private const string ValidMessage = "The configuration is invalid.";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_rejects_missing_code(string? code)
    {
        var act = () => new DomainError(code!, ValidMessage);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_rejects_missing_message(string? message)
    {
        var act = () => new DomainError(ValidCode, message!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_exposes_code_and_message_for_valid_input()
    {
        var error = new DomainError(ValidCode, ValidMessage);

        error.Code.Should().Be(ValidCode);
        error.Message.Should().Be(ValidMessage);
    }

    [Fact]
    public void Errors_with_the_same_values_are_equal()
    {
        var first = new DomainError(ValidCode, ValidMessage);
        var second = new DomainError(ValidCode, ValidMessage);

        first.Should().Be(second);
        (first == second).Should().BeTrue();
        first.GetHashCode().Should().Be(second.GetHashCode());
    }

    [Theory]
    [InlineData("other.code", ValidMessage)]
    [InlineData(ValidCode, "Another message.")]
    public void Errors_with_different_values_are_not_equal(string code, string message)
    {
        var first = new DomainError(ValidCode, ValidMessage);
        var second = new DomainError(code, message);

        first.Should().NotBe(second);
        (first != second).Should().BeTrue();
    }
}
