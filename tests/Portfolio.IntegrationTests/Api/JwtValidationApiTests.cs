using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.Tokens;
using Portfolio.IntegrationTests.Infrastructure;
using Xunit;

namespace Portfolio.IntegrationTests.Api;

public sealed class JwtValidationApiTests : IClassFixture<DatabaseOptionalApiFactory>
{
    private readonly DatabaseOptionalApiFactory _factory;
    private readonly HttpClient _client;

    public JwtValidationApiTests(DatabaseOptionalApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
    }

    [Fact]
    public async Task ValidTokenReachesAuthorizationAndApplicationBoundary()
    {
        var response = await SendAsync(_factory.CreateToken(role: "Admin"));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task ExpiredTokenIsRejected()
    {
        var response = await SendAsync(_factory.CreateToken(
            role: "Admin",
            expires: DateTime.UtcNow.AddMinutes(-2)));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("wrong-issuer", "portfolio-client")]
    [InlineData("portfolio-tests", "wrong-audience")]
    public async Task WrongIssuerOrAudienceIsRejected(string issuer, string audience)
    {
        var response = await SendAsync(_factory.CreateToken("Admin", issuer, audience));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task WrongSignatureIsRejected()
    {
        using var otherCertificate = new TestJwtCertificate();
        var response = await SendAsync(otherCertificate.Issue(
            Guid.NewGuid(),
            Guid.NewGuid(),
            0,
            "Admin"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SymmetricAlgorithmIsRejected()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
            "test-only-symmetric-key-with-more-than-thirty-two-bytes"))
        {
            KeyId = "api-test-key",
        };
        var token = new JwtSecurityToken(
            "portfolio-tests",
            "portfolio-client",
            [new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString())],
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        var response = await SendAsync(new JwtSecurityTokenHandler().WriteToken(token));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MissingSessionClaimsAreRejected()
    {
        var token = _factory.CreateTokenWithClaims(
        [
            new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture), ClaimValueTypes.Integer64),
            new Claim(ClaimTypes.Role, "Admin"),
        ]);

        var response = await SendAsync(token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private Task<HttpResponseMessage> SendAsync(string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/profile");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
