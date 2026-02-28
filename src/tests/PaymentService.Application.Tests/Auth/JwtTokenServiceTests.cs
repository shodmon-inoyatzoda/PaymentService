using System.IdentityModel.Tokens.Jwt;
using FluentAssertions;
using Microsoft.Extensions.Options;
using PaymentService.Domain.Entities.Users;
using PaymentService.Infrastructure.Auth;

namespace PaymentService.Application.Tests.Auth;

public class JwtTokenServiceTests
{
    private static JwtTokenService CreateService(JwtSettings? settings = null)
    {
        var jwtSettings = settings ?? new JwtSettings
        {
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            SigningKey = "test-super-secret-key-at-least-32-chars!!",
            AccessTokenExpirationMinutes = 15
        };
        return new JwtTokenService(Options.Create(jwtSettings));
    }

    private static User CreateTestUser()
    {
        var result = User.Register("+998901234567", "test@example.com", "Test User", "Password1");
        result.IsSuccess.Should().BeTrue();
        return result.Value;
    }

    [Fact]
    public void CreateAccessToken_ReturnsNonEmptyToken()
    {
        var service = CreateService();
        var user = CreateTestUser();

        var token = service.CreateAccessToken(user);

        token.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void CreateAccessToken_ReturnsParsableJwt()
    {
        var service = CreateService();
        var user = CreateTestUser();

        var token = service.CreateAccessToken(user);

        var handler = new JwtSecurityTokenHandler();
        handler.CanReadToken(token).Should().BeTrue();

        var jwtToken = handler.ReadJwtToken(token);
        jwtToken.Subject.Should().Be(user.Id.ToString());
        jwtToken.Issuer.Should().Be("TestIssuer");
    }

    [Fact]
    public void CreateAccessToken_ContainsUserClaims()
    {
        var service = CreateService();
        var user = CreateTestUser();

        var token = service.CreateAccessToken(user);

        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        jwtToken.Subject.Should().Be(user.Id.ToString());
        jwtToken.Claims.Should().Contain(c => c.Value == user.FullName);
    }

    [Fact]
    public void CreateAccessToken_ExpiresAtConfiguredTime()
    {
        var settings = new JwtSettings
        {
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            SigningKey = "test-super-secret-key-at-least-32-chars!!",
            AccessTokenExpirationMinutes = 30
        };
        var service = CreateService(settings);
        var user = CreateTestUser();

        var before = DateTime.UtcNow;
        var token = service.CreateAccessToken(user);
        var after = DateTime.UtcNow;

        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        jwtToken.ValidTo.Should().BeCloseTo(before.AddMinutes(30), TimeSpan.FromSeconds(5));
    }
}
