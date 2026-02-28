using FluentAssertions;
using PaymentService.Infrastructure.Auth;

namespace PaymentService.Application.Tests.Auth;

public class RefreshTokenGeneratorTests
{
    [Fact]
    public void Generate_ReturnsNonEmptyToken()
    {
        var generator = new RefreshTokenGenerator();

        var token = generator.Generate();

        token.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Generate_ReturnsDifferentTokensEachTime()
    {
        var generator = new RefreshTokenGenerator();

        var token1 = generator.Generate();
        var token2 = generator.Generate();

        token1.Should().NotBe(token2);
    }

    [Fact]
    public void Generate_ReturnsBase64UrlSafeString()
    {
        var generator = new RefreshTokenGenerator();

        var token = generator.Generate();

        // Base64Url should not contain +, /, or = characters
        token.Should().NotContain("+");
        token.Should().NotContain("/");
        token.Should().NotContain("=");
    }

    [Fact]
    public void Generate_HasSufficientLength()
    {
        var generator = new RefreshTokenGenerator();

        var token = generator.Generate();

        // 64 bytes base64url encodes to ~85 chars
        token.Length.Should().BeGreaterThan(60);
    }
}
