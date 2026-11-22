using DocMind.Application.Abstractions;
using DocMind.Application.Features.Auth;
using DocMind.Domain.Common;

namespace DocMind.UnitTests.Application;

public sealed class AuthHandlerTests
{
    private static readonly UserDto User = new(Guid.NewGuid(), "ada@docmind.test", "Ada");
    private static readonly AccessToken Access = new("access", DateTimeOffset.UtcNow.AddMinutes(15));
    private static readonly IssuedRefreshToken Refresh = new("refresh", DateTimeOffset.UtcNow.AddDays(7));

    private readonly IIdentityService _identity = Substitute.For<IIdentityService>();
    private readonly IAccessTokenService _accessTokens = Substitute.For<IAccessTokenService>();
    private readonly IRefreshTokenService _refreshTokens = Substitute.For<IRefreshTokenService>();

    public AuthHandlerTests()
    {
        _accessTokens.Create(Arg.Any<UserDto>()).Returns(Access);
    }

    [Fact]
    public async Task Login_IssuesBothTokensForValidCredentials()
    {
        _identity.VerifyCredentialsAsync("ada@docmind.test", "pw", Arg.Any<CancellationToken>()).Returns(Result.Success(User));
        _refreshTokens.IssueAsync(User.Id, Arg.Any<CancellationToken>()).Returns(Refresh);
        var handler = new LoginHandler(_identity, _accessTokens, _refreshTokens);

        var result = await handler.HandleAsync(new LoginRequest("  ada@docmind.test ", "pw"), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(new AuthResult(Access, User, Refresh));
    }

    [Fact]
    public async Task Login_DoesNotIssueTokensWhenCredentialsAreInvalid()
    {
        _identity.VerifyCredentialsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<UserDto>(AuthErrors.InvalidCredentials));
        var handler = new LoginHandler(_identity, _accessTokens, _refreshTokens);

        var result = await handler.HandleAsync(new LoginRequest("ada@docmind.test", "bad"), TestContext.Current.CancellationToken);

        result.Error.Should().Be(AuthErrors.InvalidCredentials);
        await _refreshTokens.DidNotReceive().IssueAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        _accessTokens.DidNotReceive().Create(Arg.Any<UserDto>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Refresh_RejectsAMissingToken(string? token)
    {
        var handler = new RefreshHandler(_identity, _accessTokens, _refreshTokens);

        var result = await handler.HandleAsync(token, TestContext.Current.CancellationToken);

        result.Error.Should().Be(AuthErrors.InvalidRefreshToken);
        await _refreshTokens.DidNotReceive().RotateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Refresh_ReturnsFreshTokensForTheRotatedSession()
    {
        _refreshTokens.RotateAsync("old", Arg.Any<CancellationToken>())
            .Returns(Result.Success(new RotatedRefreshToken(User.Id, Refresh)));
        _identity.FindByIdAsync(User.Id, Arg.Any<CancellationToken>()).Returns(User);
        var handler = new RefreshHandler(_identity, _accessTokens, _refreshTokens);

        var result = await handler.HandleAsync("old", TestContext.Current.CancellationToken);

        result.Value.Should().Be(new AuthResult(Access, User, Refresh));
    }

    [Fact]
    public async Task Refresh_RevokesTheNewTokenWhenTheUserNoLongerExists()
    {
        _refreshTokens.RotateAsync("old", Arg.Any<CancellationToken>())
            .Returns(Result.Success(new RotatedRefreshToken(User.Id, Refresh)));
        _identity.FindByIdAsync(User.Id, Arg.Any<CancellationToken>()).Returns((UserDto?)null);
        var handler = new RefreshHandler(_identity, _accessTokens, _refreshTokens);

        var result = await handler.HandleAsync("old", TestContext.Current.CancellationToken);

        result.Error.Should().Be(AuthErrors.InvalidRefreshToken);
        await _refreshTokens.Received(1).RevokeAsync(Refresh.Value, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Refresh_PropagatesRotationFailures()
    {
        _refreshTokens.RotateAsync("stale", Arg.Any<CancellationToken>())
            .Returns(Result.Failure<RotatedRefreshToken>(AuthErrors.InvalidRefreshToken));
        var handler = new RefreshHandler(_identity, _accessTokens, _refreshTokens);

        var result = await handler.HandleAsync("stale", TestContext.Current.CancellationToken);

        result.Error.Should().Be(AuthErrors.InvalidRefreshToken);
    }

    [Fact]
    public async Task Logout_RevokesThePresentedToken()
    {
        var handler = new LogoutHandler(_refreshTokens);

        await handler.HandleAsync("token", TestContext.Current.CancellationToken);

        await _refreshTokens.Received(1).RevokeAsync("token", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Logout_IgnoresAMissingToken()
    {
        var handler = new LogoutHandler(_refreshTokens);

        await handler.HandleAsync(null, TestContext.Current.CancellationToken);

        await _refreshTokens.DidNotReceive().RevokeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Register_TrimsInputBeforeCreatingTheAccount()
    {
        _identity.RegisterAsync("ada@docmind.test", "Passw0rdPassw0rd", "Ada", Arg.Any<CancellationToken>())
            .Returns(Result.Success(User));
        var handler = new RegisterHandler(_identity);

        var result = await handler.HandleAsync(new RegisterRequest(" ada@docmind.test ", "Passw0rdPassw0rd", " Ada "), TestContext.Current.CancellationToken);

        result.Value.Should().Be(User);
    }

    [Fact]
    public async Task CurrentUser_ReportsAMissingAccount()
    {
        _identity.FindByIdAsync(User.Id, Arg.Any<CancellationToken>()).Returns((UserDto?)null);
        var handler = new GetCurrentUserHandler(_identity);

        var result = await handler.HandleAsync(User.Id, TestContext.Current.CancellationToken);

        result.Error.Should().Be(AuthErrors.UserNotFound);
    }
}
