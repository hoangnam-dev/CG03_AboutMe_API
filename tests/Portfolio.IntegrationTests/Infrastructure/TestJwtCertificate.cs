using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.IdentityModel.Tokens;

namespace Portfolio.IntegrationTests.Infrastructure;

internal sealed class TestJwtCertificate : IDisposable
{
    private const string Password = "test-only-password";
    private readonly X509Certificate2 _certificate;

    public TestJwtCertificate()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"portfolio-api-test-{Guid.NewGuid():N}.pfx");
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=portfolio-api-tests",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        using var generated = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(2));
        File.WriteAllBytes(Path, generated.Export(X509ContentType.Pfx, Password));
        _certificate = X509CertificateLoader.LoadPkcs12FromFile(
            Path,
            Password,
            X509KeyStorageFlags.EphemeralKeySet);
    }

    public string Path { get; }
    public string CertificatePassword { get; } = Password;
    public string KeyId { get; } = "api-test-key";

    public string Issue(
        Guid userId,
        Guid sessionId,
        int authVersion,
        string role = "User",
        string issuer = "portfolio-tests",
        string audience = "portfolio-client",
        DateTime? expires = null,
        string algorithm = SecurityAlgorithms.RsaSha256)
    {
        var key = new X509SecurityKey(_certificate) { KeyId = KeyId };
        var now = DateTime.UtcNow;
        var tokenExpires = expires ?? now.AddMinutes(5);
        var notBefore = tokenExpires <= now ? tokenExpires.AddMinutes(-5) : now.AddSeconds(-1);
        var token = new JwtSecurityToken(
            issuer,
            audience,
            [
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Iat, new DateTimeOffset(now).ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture), ClaimValueTypes.Integer64),
                new Claim("sid", sessionId.ToString()),
                new Claim("auth_version", authVersion.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new Claim(ClaimTypes.Role, role),
            ],
            notBefore,
            tokenExpires,
            new SigningCredentials(key, algorithm));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string IssueClaims(
        IEnumerable<Claim> claims,
        string issuer = "portfolio-tests",
        string audience = "portfolio-client",
        DateTime? expires = null)
    {
        var key = new X509SecurityKey(_certificate) { KeyId = KeyId };
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            issuer,
            audience,
            claims,
            now.AddSeconds(-1),
            expires ?? now.AddMinutes(5),
            new SigningCredentials(key, SecurityAlgorithms.RsaSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public void Dispose()
    {
        _certificate.Dispose();
        File.Delete(Path);
    }
}
