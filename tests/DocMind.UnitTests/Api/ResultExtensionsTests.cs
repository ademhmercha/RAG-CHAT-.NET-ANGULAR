using DocMind.Api.Common;
using DocMind.Domain.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace DocMind.UnitTests.Api;

public sealed class ResultExtensionsTests
{
    [Theory]
    [InlineData(ErrorType.Validation, StatusCodes.Status400BadRequest)]
    [InlineData(ErrorType.Unauthorized, StatusCodes.Status401Unauthorized)]
    [InlineData(ErrorType.Forbidden, StatusCodes.Status403Forbidden)]
    [InlineData(ErrorType.NotFound, StatusCodes.Status404NotFound)]
    [InlineData(ErrorType.Conflict, StatusCodes.Status409Conflict)]
    [InlineData(ErrorType.PayloadTooLarge, StatusCodes.Status413PayloadTooLarge)]
    [InlineData(ErrorType.UnsupportedMediaType, StatusCodes.Status415UnsupportedMediaType)]
    [InlineData(ErrorType.Failure, StatusCodes.Status500InternalServerError)]
    public void ToProblem_MapsErrorTypesToStatusCodes(ErrorType type, int expectedStatus)
    {
        var error = new Error("sample.code", "Something happened.", type);

        var problem = error.ToProblem().Should().BeOfType<ProblemHttpResult>().Subject;

        problem.StatusCode.Should().Be(expectedStatus);
        problem.ProblemDetails.Status.Should().Be(expectedStatus);
        problem.ProblemDetails.Detail.Should().Be("Something happened.");
        problem.ProblemDetails.Type.Should().StartWith("https://www.rfc-editor.org/rfc/");
        problem.ProblemDetails.Extensions[ResultExtensions.ErrorCodeKey].Should().Be("sample.code");
    }

    [Fact]
    public void ToProblem_RejectsSuccessfulResults()
    {
        var act = () => Result.Success().ToProblem();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ToHttpResult_ReturnsTheSuccessResultOrAProblem()
    {
        Result<int> success = 7;
        Result<int> failure = Error.NotFound("x.not_found", "Missing.");

        success.ToHttpResult(v => Results.Ok(v)).Should().BeOfType<Ok<int>>().Which.Value.Should().Be(7);
        failure.ToHttpResult(v => Results.Ok(v)).Should().BeOfType<ProblemHttpResult>().Which.StatusCode.Should().Be(404);
    }

    [Theory]
    [InlineData(400)]
    [InlineData(404)]
    [InlineData(409)]
    [InlineData(413)]
    [InlineData(429)]
    [InlineData(500)]
    [InlineData(503)]
    public void ProblemTypes_ReferenceAnRfcForKnownStatuses(int status) =>
        ProblemTypes.ForStatus(status).Should().StartWith("https://www.rfc-editor.org/rfc/");
}
