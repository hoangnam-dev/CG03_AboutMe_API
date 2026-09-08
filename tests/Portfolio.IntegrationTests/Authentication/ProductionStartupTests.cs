using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Portfolio.IntegrationTests.Infrastructure;
using Xunit;

namespace Portfolio.IntegrationTests.Authentication;

public sealed class ProductionStartupTests
{
    [Theory]
    [InlineData("ConnectionStrings:PostgreSql", "", "PostgreSql")]
    [InlineData("Frontend:Origins:0", "http://portfolio.example", "HTTPS")]
    [InlineData("Jwt:ActiveKeyId", "", "ActiveKeyId")]
    [InlineData("SupabaseStorage:ServiceRoleKey", "", "ServiceRoleKey")]
    [InlineData("Upload:MaxFileSize", "0", "MaxFileSize")]
    [InlineData("RateLimit:Auth:LoginPermitLimit", "0", "Auth rate limits")]
    [InlineData("RateLimit:Contact:PermitLimit", "0", "Contact rate limit")]
    [InlineData("BootstrapAdmin:Enabled", "true", "disabled")]
    public void ProductionStartupRejectsUnsafeConfiguration(
        string key,
        string value,
        string expectedMessage)
    {
        using var factory = new ProductionApiFactory(key, value);

        var exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains(expectedMessage, exception.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private sealed class ProductionApiFactory(string overrideKey, string overrideValue)
        : WebApplicationFactory<Program>
    {
        private readonly TestJwtCertificate _certificate = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.UseSetting(
                "ConnectionStrings:PostgreSql",
                "Host=localhost;Database=portfolio_security_tests;Username=test;Password=test");
            builder.UseSetting("Frontend:Origins:0", "https://portfolio.example");
            builder.UseSetting("Jwt:Issuer", "portfolio-tests");
            builder.UseSetting("Jwt:Audience", "portfolio-client");
            builder.UseSetting("Jwt:ActiveKeyId", _certificate.KeyId);
            builder.UseSetting("Jwt:SigningCertificatePath", _certificate.Path);
            builder.UseSetting("Jwt:SigningCertificatePassword", _certificate.CertificatePassword);
            builder.UseSetting("RefreshToken:Pepper", "integration-test-pepper-with-at-least-32-bytes");
            builder.UseSetting("SupabaseStorage:Url", "https://storage.example");
            builder.UseSetting("SupabaseStorage:ServiceRoleKey", "test-service-role-key");
            builder.UseSetting("BootstrapAdmin:Enabled", "false");
            builder.UseSetting(overrideKey, overrideValue);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                _certificate.Dispose();
            }
        }
    }
}
