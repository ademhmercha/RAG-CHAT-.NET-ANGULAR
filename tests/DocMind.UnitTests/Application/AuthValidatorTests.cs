using DocMind.Application.Features.Auth;

namespace DocMind.UnitTests.Application;

public sealed class AuthValidatorTests
{
    private readonly RegisterRequestValidator _register = new();
    private readonly LoginRequestValidator _login = new();

    [Fact]
    public void Register_AcceptsAValidRequest()
    {
        var result = _register.Validate(new RegisterRequest("ada@docmind.test", "Passw0rdPassw0rd", "Ada"));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("", "Passw0rdPassw0rd", "Ada", "Email")]
    [InlineData("not-an-email", "Passw0rdPassw0rd", "Ada", "Email")]
    [InlineData("ada@docmind.test", "short1A", "Ada", "Password")]
    [InlineData("ada@docmind.test", "alllowercase123", "Ada", "Password")]
    [InlineData("ada@docmind.test", "ALLUPPERCASE123", "Ada", "Password")]
    [InlineData("ada@docmind.test", "NoDigitsInHereAtAll", "Ada", "Password")]
    [InlineData("ada@docmind.test", "Passw0rdPassw0rd", "", "DisplayName")]
    [InlineData("ada@docmind.test", "Passw0rdPassw0rd", "   ", "DisplayName")]
    public void Register_RejectsInvalidFields(string email, string password, string displayName, string failingField)
    {
        var result = _register.Validate(new RegisterRequest(email, password, displayName));

        result.Errors.Select(e => e.PropertyName).Should().Contain(failingField);
    }

    [Fact]
    public void Register_RejectsOversizedValues()
    {
        var result = _register.Validate(new RegisterRequest(
            $"{new string('a', 260)}@docmind.test",
            new string('A', PasswordPolicy.MaxLength) + "a1x",
            new string('n', 101)));

        result.Errors.Select(e => e.PropertyName).Should().Contain(["Email", "Password", "DisplayName"]);
    }

    [Fact]
    public void Login_RequiresBothFields()
    {
        var result = _login.Validate(new LoginRequest(string.Empty, string.Empty));

        result.Errors.Select(e => e.PropertyName).Should().Contain(["Email", "Password"]);
    }
}
