using Xunit;

namespace Portfolio.IntegrationTests.Deployment;

public sealed class DeploymentAssetTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public void DockerfileUsesDotNet10NonRootRuntimeOnPort8080()
    {
        var dockerfile = Read("Dockerfile");

        Assert.Contains("mcr.microsoft.com/dotnet/sdk:10.0", dockerfile, StringComparison.Ordinal);
        Assert.Contains("mcr.microsoft.com/dotnet/aspnet:10.0", dockerfile, StringComparison.Ordinal);
        Assert.Contains("EXPOSE 8080", dockerfile, StringComparison.Ordinal);
        Assert.Contains("USER $APP_UID", dockerfile, StringComparison.Ordinal);
        Assert.Contains("COPY .editorconfig", dockerfile, StringComparison.Ordinal);
        Assert.DoesNotContain("database update", dockerfile, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ContinuousIntegrationUsesLeastPrivilegeAndAllReleaseGates()
    {
        var workflow = Read(".github/workflows/backend-ci.yml");

        Assert.Contains("contents: read", workflow, StringComparison.Ordinal);
        Assert.Contains("dotnet restore", workflow, StringComparison.Ordinal);
        Assert.Contains("--configuration Release --no-restore", workflow, StringComparison.Ordinal);
        Assert.Contains("--configuration Release --no-build", workflow, StringComparison.Ordinal);
        Assert.Contains("dotnet format", workflow, StringComparison.Ordinal);
        Assert.Contains("portfolio-api:${{ github.sha }}", workflow, StringComparison.Ordinal);
        Assert.Contains("Test-Container.ps1", workflow, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(".dockerignore")]
    [InlineData(".env.example")]
    [InlineData("scripts/Test-Container.ps1")]
    [InlineData("scripts/Test-Deployment.ps1")]
    [InlineData("docs/operations/ENVIRONMENT.md")]
    [InlineData("docs/operations/MIGRATIONS.md")]
    [InlineData("docs/operations/DEPLOYMENT.md")]
    [InlineData("docs/operations/ROLLBACK.md")]
    [InlineData("docs/operations/RELEASE_CHECKLIST.md")]
    public void RequiredOperationalAssetExists(string relativePath) =>
        Assert.True(File.Exists(Path.Combine(RepositoryRoot, relativePath)), relativePath);

    [Fact]
    public void ContainerSmokeLoadsHttpClientForWindowsPowerShell51()
    {
        var script = Read("scripts/Test-Container.ps1");

        Assert.Contains("Add-Type -AssemblyName System.Net.Http", script, StringComparison.Ordinal);
    }

    [Fact]
    public void ContainerSmokeCleanupCannotMaskThePrimaryFailure()
    {
        var script = Read("scripts/Test-Container.ps1");

        Assert.Contains("$cleanupErrorPreference = $ErrorActionPreference", script, StringComparison.Ordinal);
        Assert.Contains("$ErrorActionPreference = \"SilentlyContinue\"", script, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptsSendAnExplicitAllowedFrontendOrigin()
    {
        var containerScript = Read("scripts/Test-Container.ps1");
        var deploymentScript = Read("scripts/Test-Deployment.ps1");

        Assert.Contains("Frontend__Origins__0=http://smoke.local", containerScript, StringComparison.Ordinal);
        Assert.Contains("RateLimit__Contact__PermitLimit=5", containerScript, StringComparison.Ordinal);
        Assert.Contains("Origin", containerScript, StringComparison.Ordinal);
        Assert.Contains("FrontendOrigin", deploymentScript, StringComparison.Ordinal);
        Assert.Contains("Origin", deploymentScript, StringComparison.Ordinal);
    }

    private static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(RepositoryRoot, relativePath));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Portfolio.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Could not find the repository root.");
    }
}
