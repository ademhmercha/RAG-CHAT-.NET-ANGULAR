using DocMind.Domain.Common;

namespace DocMind.UnitTests.Domain;

public sealed class ResultTests
{
    private static readonly Error SampleError = Error.NotFound("sample.not_found", "Sample was not found.");

    [Fact]
    public void Success_HasNoError()
    {
        var result = Result.Success();

        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Error.Should().Be(Error.None);
    }

    [Fact]
    public void Failure_CarriesTheError()
    {
        var result = Result.Failure(SampleError);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SampleError);
    }

    [Fact]
    public void Failure_WithoutError_IsRejected()
    {
        var act = () => Result.Failure(Error.None);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GenericSuccess_ExposesValue()
    {
        var result = Result.Success(42);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void GenericFailure_ThrowsWhenReadingValue()
    {
        var result = Result.Failure<int>(SampleError);

        var act = () => result.Value;

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ImplicitConversions_BuildResultsFromValuesAndErrors()
    {
        Result<string> success = "value";
        Result<string> failure = SampleError;

        success.Value.Should().Be("value");
        failure.Error.Should().Be(SampleError);
    }

    [Fact]
    public void Match_InvokesTheMatchingBranch()
    {
        Result<int> success = 2;
        Result<int> failure = SampleError;

        success.Match(v => v * 2, _ => -1).Should().Be(4);
        failure.Match(v => v * 2, e => e.Code.Length).Should().Be(SampleError.Code.Length);
    }

    [Theory]
    [InlineData(ErrorType.Validation)]
    [InlineData(ErrorType.NotFound)]
    [InlineData(ErrorType.Conflict)]
    [InlineData(ErrorType.Unauthorized)]
    [InlineData(ErrorType.Forbidden)]
    [InlineData(ErrorType.PayloadTooLarge)]
    [InlineData(ErrorType.UnsupportedMediaType)]
    [InlineData(ErrorType.Failure)]
    public void ErrorFactories_SetTheExpectedType(ErrorType type)
    {
        var error = type switch
        {
            ErrorType.Validation => Error.Validation("c", "d"),
            ErrorType.NotFound => Error.NotFound("c", "d"),
            ErrorType.Conflict => Error.Conflict("c", "d"),
            ErrorType.Unauthorized => Error.Unauthorized("c", "d"),
            ErrorType.Forbidden => Error.Forbidden("c", "d"),
            ErrorType.PayloadTooLarge => Error.PayloadTooLarge("c", "d"),
            ErrorType.UnsupportedMediaType => Error.UnsupportedMediaType("c", "d"),
            _ => Error.Failure("c", "d"),
        };

        error.Type.Should().Be(type);
        error.Code.Should().Be("c");
    }
}
