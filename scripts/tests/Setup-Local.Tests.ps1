[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = [System.IO.Path]::GetFullPath(
    (Join-Path $PSScriptRoot "..\.."))
$setupScript = Join-Path $repositoryRoot "scripts\Setup-Local.ps1"
$testRoot = Join-Path $repositoryRoot "TestResults\setup-local-tests"

function Assert-Equal {
    param(
        [Parameter(Mandatory = $true)] $Expected,
        [Parameter(Mandatory = $true)] $Actual,
        [Parameter(Mandatory = $true)] [string] $Message
    )

    if ($Expected -ne $Actual) {
        throw "Assertion failed: $Message. Expected '$Expected', actual '$Actual'."
    }
}

function Assert-NotContains {
    param(
        [Parameter(Mandatory = $true)] [string] $Text,
        [Parameter(Mandatory = $true)] [string] $Unexpected,
        [Parameter(Mandatory = $true)] [string] $Message
    )

    if ($Text.IndexOf($Unexpected, [System.StringComparison]::Ordinal) -ge 0) {
        throw "Assertion failed: $Message."
    }
}

function Invoke-SetupValidation {
    param(
        [Parameter(Mandatory = $true)] [string] $Name,
        [Parameter(Mandatory = $true)] [string] $Content
    )

    $environmentPath = Join-Path $testRoot "$Name.env"
    [System.IO.File]::WriteAllText(
        $environmentPath,
        $Content,
        [System.Text.UTF8Encoding]::new($false))

    $output = & powershell.exe `
        -NoProfile `
        -ExecutionPolicy Bypass `
        -File $setupScript `
        -EnvironmentFile $environmentPath `
        -ValidateOnly `
        -NonInteractive 2>&1 | Out-String

    return [pscustomobject]@{
        ExitCode = $LASTEXITCODE
        Output = $output
    }
}

$validEnvironment = @"
ConnectionStrings__PostgreSql=Host=db.example.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=database-secret;SSL Mode=Require
Frontend__Origins__0=https://localhost:44313
Frontend__Origins__1=http://localhost:3000
Jwt__Issuer=Portfolio.Api
Jwt__Audience=Portfolio.Frontend
Jwt__ActiveKeyId=test-key
Jwt__SigningCertificatePath=TestResults/test-only.pfx
Jwt__SigningCertificatePassword=test-only-password
Jwt__AccessTokenMinutes=10
Jwt__ClockSkewSeconds=30
RefreshToken__IdleLifetimeDays=7
RefreshToken__AbsoluteLifetimeDays=30
RefreshToken__Pepper=this-is-a-test-refresh-pepper-with-at-least-32-bytes
RefreshToken__CookieName=__Host-refresh
RefreshToken__CsrfCookieName=__Host-csrf
RefreshToken__CookieSameSite=Lax
RateLimit__Auth__LoginPermitLimit=5
RateLimit__Auth__RefreshPermitLimit=30
RateLimit__Auth__WindowSeconds=60
SupabaseStorage__Url=https://example.supabase.co/
SupabaseStorage__ServiceRoleKey=storage-secret
SupabaseStorage__RequestTimeoutSeconds=30
SupabaseStorage__MaxResponseBytes=65536
SupabaseStorage__Buckets__Avatars=avatars
SupabaseStorage__Buckets__ProjectImages=project-images
SupabaseStorage__Buckets__CertificateFiles=certificate-files
SupabaseStorage__Buckets__CvFiles=cv-files
Upload__MaxFileSize=10485760
BootstrapAdmin__Enabled=false
BootstrapAdmin__Email=
BootstrapAdmin__Password=
"@

try {
    if (Test-Path $testRoot) {
        $resolvedTestRoot = [System.IO.Path]::GetFullPath($testRoot)
        $resolvedResultsRoot = [System.IO.Path]::GetFullPath(
            (Join-Path $repositoryRoot "TestResults"))
        if (-not $resolvedTestRoot.StartsWith(
            $resolvedResultsRoot + [System.IO.Path]::DirectorySeparatorChar,
            [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to remove a test directory outside TestResults."
        }

        Remove-Item -LiteralPath $resolvedTestRoot -Recurse -Force
    }

    New-Item -ItemType Directory -Path $testRoot -Force | Out-Null

    $valid = Invoke-SetupValidation "valid" $validEnvironment
    Assert-Equal 0 $valid.ExitCode "valid configuration exits successfully"
    Assert-NotContains $valid.Output "database-secret" "database password is not logged"
    Assert-NotContains $valid.Output "storage-secret" "Storage key is not logged"

    $invalidSecondOrigin = Invoke-SetupValidation `
        "invalid-second-origin" `
        ($validEnvironment.Replace(
            "Frontend__Origins__1=http://localhost:3000",
            "Frontend__Origins__1=https://localhost:44313/swagger"))
    Assert-Equal 1 $invalidSecondOrigin.ExitCode "invalid second frontend origin fails"

    $missingJwt = Invoke-SetupValidation `
        "missing-jwt-certificate" `
        ($validEnvironment.Replace(
            "Jwt__SigningCertificatePath=TestResults/test-only.pfx",
            "Jwt__SigningCertificatePath="))
    Assert-Equal 1 $missingJwt.ExitCode "missing JWT signing certificate fails"
    Assert-NotContains $missingJwt.Output "database-secret" "failure output hides database password"
    Assert-NotContains $missingJwt.Output "storage-secret" "failure output hides Storage key"

    $placeholderStorage = Invoke-SetupValidation `
        "placeholder-storage" `
        ($validEnvironment.Replace(
            "SupabaseStorage__ServiceRoleKey=storage-secret",
            "SupabaseStorage__ServiceRoleKey=replace-with-server-only-service-role-key"))
    Assert-Equal 1 $placeholderStorage.ExitCode "placeholder Storage key fails"

    Write-Host "Setup-Local tests passed: 4."
}
finally {
    if (Test-Path $testRoot) {
        Remove-Item -LiteralPath $testRoot -Recurse -Force
    }
}
