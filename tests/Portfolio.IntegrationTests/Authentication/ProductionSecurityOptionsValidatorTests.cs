using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Portfolio.Api.Configuration;
using Portfolio.Infrastructure.Authentication;
using Xunit;

namespace Portfolio.IntegrationTests.Authentication;

public sealed class ProductionSecurityOptionsValidatorTests
{
    [Theory]
    [InlineData("http://portfolio.example")]
    [InlineData("http://localhost:3000")]
    public void ProductionFrontendValidationRejectsNonHttpsOrigin(string origin)
    {
        var validator = new ProductionFrontendOptionsValidator(
            new TestHostEnvironment(Environments.Production));

        var result = validator.Validate(null, new FrontendOptions { Origins = [origin] });

        Assert.True(result.Failed);
        Assert.Contains("HTTPS", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionFrontendValidationRejectsMultipleOrigins()
    {
        var validator = new ProductionFrontendOptionsValidator(
            new TestHostEnvironment(Environments.Production));

        var result = validator.Validate(null, new FrontendOptions
        {
            Origins = ["https://portfolio.example", "https://preview.example"],
        });

        Assert.True(result.Failed);
        Assert.Contains("exactly one", result.FailureMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProductionFrontendValidationAcceptsOneHttpsOrigin()
    {
        var validator = new ProductionFrontendOptionsValidator(
            new TestHostEnvironment(Environments.Production));

        var result = validator.Validate(null, new FrontendOptions
        {
            Origins = ["https://portfolio.example"],
        });

        Assert.True(result.Succeeded, result.FailureMessage);
    }

    [Fact]
    public void DevelopmentFrontendValidationDoesNotAddProductionRestrictions()
    {
        var validator = new ProductionFrontendOptionsValidator(
            new TestHostEnvironment(Environments.Development));

        var result = validator.Validate(null, new FrontendOptions
        {
            Origins = ["http://localhost:3000", "https://localhost:7097"],
        });

        Assert.True(result.Succeeded, result.FailureMessage);
    }

    [Fact]
    public void ProductionBootstrapValidationRejectsEnabledBootstrap()
    {
        var validator = new ProductionBootstrapAdminOptionsValidator(true);

        var result = validator.Validate(null, new BootstrapAdminOptions
        {
            Enabled = true,
            Email = "admin@example.com",
            Password = "Example-password-123!",
        });

        Assert.True(result.Failed);
        Assert.Contains("disabled", result.FailureMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DevelopmentBootstrapValidationDoesNotBlockProvisioning()
    {
        var validator = new ProductionBootstrapAdminOptionsValidator(false);

        var result = validator.Validate(null, new BootstrapAdminOptions { Enabled = true });

        Assert.True(result.Succeeded, result.FailureMessage);
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Portfolio.IntegrationTests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
