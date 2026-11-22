using System.Text;
using DocMind.Application.Features.Auth;
using DocMind.Infrastructure.Auth;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace DocMind.UnitTests.Infrastructure;

public sealed class AccessTokenServiceTests
{
    private const string Key = "unit-test-signing-key-0123456789-abcdefgh";

    private static readonly UserDto User = new(Guid.NewGuid(), "ada@docmind.test", "Ada");

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 3, 1, 8, 0, 0, TimeSpan.Zero));

    private AccessTokenService CreateService(int minutes = 15) =>
        new(Options.Create(new JwtOptions { SigningKey = Key, AccessTokenMinutes = minutes }), _time);

    [Fact]
    public async Task Create_ProducesASignedTokenCarryingTheUserClaims()
    {
        var token = CreateService().Create(User);

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token.Value, Parameters(Key));

        result.IsValid.Should().BeTrue();
        result.Claims["sub"].Should().Be(User.Id.ToString());
        result.Claims["email"].Should().Be(User.Email);
        result.Claims["name"].Should().Be(User.DisplayName);
        result.Claims.Should().ContainKey("jti");
    }

    [Fact]
    public void Create_ReportsTheConfiguredExpiry()
    {
        var token = CreateService(minutes: 30).Create(User);

        token.ExpiresAt.Should().Be(_time.GetUtcNow().AddMinutes(30));
    }

    [Fact]
    public async Task Create_ProducesTokensThatCannotBeValidatedWithAnotherKey()
    {
        var token = CreateService().Create(User);

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token.Value, Parameters("another-signing-key-0123456789-abcdefghijk"));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Create_ProducesUniqueTokenIdentifiers()
    {
        var service = CreateService();
        var handler = new JsonWebTokenHandler();

        var first = handler.ReadJsonWebToken(service.Create(User).Value).Id;
        var second = handler.ReadJsonWebToken(service.Create(User).Value).Id;

        first.Should().NotBe(second);
    }

    private static TokenValidationParameters Parameters(string key) => new()
    {
        ValidIssuer = "docmind",
        ValidAudience = "docmind-web",
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
        ValidateLifetime = false,
    };
}
