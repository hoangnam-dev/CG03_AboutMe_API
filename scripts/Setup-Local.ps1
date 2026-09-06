[CmdletBinding()]
param(
    [string] $EnvironmentFile = ".env",
    [switch] $NonInteractive,
    [switch] $ValidateOnly,
    [switch] $SyncUserSecrets,
    [switch] $ApplyMigrations
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$apiProject = Join-Path $repositoryRoot "src\Portfolio.Api\Portfolio.Api.csproj"
$infrastructureProject = Join-Path $repositoryRoot "src\Portfolio.Infrastructure\Portfolio.Infrastructure.csproj"
$solution = Join-Path $repositoryRoot "Portfolio.sln"

function Write-Step {
    param([Parameter(Mandatory = $true)] [string] $Message)

    Write-Host "[setup] $Message"
}

function Resolve-EnvironmentPath {
    param([Parameter(Mandatory = $true)] [string] $Path)

    $candidate = if ([System.IO.Path]::IsPathRooted($Path)) {
        $Path
    }
    else {
        Join-Path $repositoryRoot $Path
    }

    $resolved = [System.IO.Path]::GetFullPath($candidate)
    $rootPrefix = $repositoryRoot.TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar) +
        [System.IO.Path]::DirectorySeparatorChar
    if (-not $resolved.StartsWith(
        $rootPrefix,
        [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "EnvironmentFile must be located inside the repository."
    }

    return $resolved
}

function Read-PlainTextSecret {
    param([Parameter(Mandatory = $true)] [string] $Prompt)

    $secureValue = Read-Host $Prompt -AsSecureString
    $pointer = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureValue)
    try {
        $value = [System.Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)
        if ([string]::IsNullOrWhiteSpace($value)) {
            throw "$Prompt is required."
        }

        return $value
    }
    finally {
        [System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer)
    }
}

function Read-WithDefault {
    param(
        [Parameter(Mandatory = $true)] [string] $Prompt,
        [Parameter(Mandatory = $true)] [string] $DefaultValue
    )

    $value = Read-Host "$Prompt [$DefaultValue]"
    return if ([string]::IsNullOrWhiteSpace($value)) {
        $DefaultValue
    }
    else {
        $value.Trim()
    }
}

function New-JwtSigningKey {
    $bytes = New-Object byte[] 48
    $generator = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try {
        $generator.GetBytes($bytes)
        return [Convert]::ToBase64String($bytes)
    }
    finally {
        $generator.Dispose()
        [Array]::Clear($bytes, 0, $bytes.Length)
    }
}

function New-EnvironmentFile {
    param([Parameter(Mandatory = $true)] [string] $Path)

    if ($NonInteractive) {
        throw "Environment file does not exist. Run interactively to create it."
    }

    Write-Step "Creating a new ignored environment file. Values are not printed."
    $databaseHost = Read-Host "Supabase PostgreSQL host (direct or session pooler)"
    $databasePort = Read-WithDefault "Supabase PostgreSQL port" "5432"
    $databaseName = Read-WithDefault "PostgreSQL database" "postgres"
    $databaseUsername = Read-Host "Supabase PostgreSQL username"
    $databasePassword = Read-PlainTextSecret "Supabase database password"
    $frontendOrigin = Read-WithDefault "Local frontend origin" "http://localhost:3000"
    $supabaseUrl = Read-Host "Supabase project URL (https://<project-ref>.supabase.co/)"
    $storageServiceKey = Read-PlainTextSecret "Supabase legacy service_role key"
    $jwtSigningKey = New-JwtSigningKey

    foreach ($required in @(
        @{ Name = "database host"; Value = $databaseHost },
        @{ Name = "database username"; Value = $databaseUsername },
        @{ Name = "Supabase URL"; Value = $supabaseUrl }
    )) {
        if ([string]::IsNullOrWhiteSpace($required.Value)) {
            throw "$($required.Name) is required."
        }
    }

    $escapedPassword = $databasePassword.Replace('"', '""')
    $connectionString =
        "Host=$($databaseHost.Trim());" +
        "Port=$databasePort;" +
        "Database=$databaseName;" +
        "Username=$($databaseUsername.Trim());" +
        "Password=`"$escapedPassword`";" +
        "SSL Mode=Require"
    $normalizedSupabaseUrl = $supabaseUrl.Trim().TrimEnd('/') + "/"
    $content = @"
# Required outside Development
ConnectionStrings__PostgreSql=$connectionString
Frontend__Origin=$frontendOrigin
Jwt__Issuer=Portfolio.Api
Jwt__Audience=Portfolio.Frontend
Jwt__SigningKey=$jwtSigningKey

# Server-only Supabase Storage configuration
SupabaseStorage__Url=$normalizedSupabaseUrl
SupabaseStorage__ServiceRoleKey=$storageServiceKey
SupabaseStorage__RequestTimeoutSeconds=30
SupabaseStorage__MaxResponseBytes=65536
SupabaseStorage__Buckets__Avatars=avatars
SupabaseStorage__Buckets__ProjectImages=project-images
SupabaseStorage__Buckets__CertificateFiles=certificate-files
SupabaseStorage__Buckets__CvFiles=cv-files
Upload__MaxFileSize=10485760

# Optional one-time administrator bootstrap
BootstrapAdmin__Enabled=false
BootstrapAdmin__Email=
BootstrapAdmin__Password=
"@

    [System.IO.File]::WriteAllText(
        $Path,
        $content,
        [System.Text.UTF8Encoding]::new($false))
    $databasePassword = $null
    $storageServiceKey = $null
    $jwtSigningKey = $null
    Write-Step "Created .env. Keep it outside source control."
}

function Read-EnvironmentFile {
    param([Parameter(Mandatory = $true)] [string] $Path)

    $settings = [ordered]@{}
    foreach ($rawLine in [System.IO.File]::ReadAllLines($Path)) {
        $line = $rawLine.Trim()
        if ([string]::IsNullOrWhiteSpace($line) -or $line.StartsWith("#")) {
            continue
        }

        $separator = $line.IndexOf('=')
        if ($separator -lt 1) {
            throw "Invalid .env line. Expected KEY=VALUE without printing its content."
        }

        $name = $line.Substring(0, $separator).Trim().TrimStart([char]0xFEFF)
        $value = $line.Substring($separator + 1).Trim()
        if ($name -notmatch '^[A-Za-z_][A-Za-z0-9_]*$') {
            throw "Invalid environment variable name in .env."
        }

        if ($settings.Contains($name)) {
            throw "Duplicate environment variable '$name' in .env."
        }

        $settings[$name] = $value
    }

    return $settings
}

function Get-RequiredSetting {
    param(
        [Parameter(Mandatory = $true)] [System.Collections.IDictionary] $Settings,
        [Parameter(Mandatory = $true)] [string] $Name
    )

    if (-not $Settings.Contains($Name) -or
        [string]::IsNullOrWhiteSpace([string] $Settings[$Name])) {
        throw "$Name is required."
    }

    $value = [string] $Settings[$Name]
    if ($value.StartsWith("replace-with-", [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "$Name still contains a placeholder."
    }

    return $value
}

function Get-IntegerSetting {
    param(
        [Parameter(Mandatory = $true)] [System.Collections.IDictionary] $Settings,
        [Parameter(Mandatory = $true)] [string] $Name,
        [Parameter(Mandatory = $true)] [int] $Minimum,
        [Parameter(Mandatory = $true)] [int] $Maximum
    )

    $rawValue = Get-RequiredSetting $Settings $Name
    $parsedValue = 0
    if (-not [int]::TryParse($rawValue, [ref] $parsedValue) -or
        $parsedValue -lt $Minimum -or
        $parsedValue -gt $Maximum) {
        throw "$Name must be between $Minimum and $Maximum."
    }

    return $parsedValue
}

function Test-AbsoluteWebUri {
    param(
        [Parameter(Mandatory = $true)] [string] $Value,
        [Parameter(Mandatory = $true)] [string] $Name,
        [switch] $HttpsOnly,
        [switch] $OriginOnly
    )

    $uri = $null
    if (-not [Uri]::TryCreate($Value, [UriKind]::Absolute, [ref] $uri)) {
        throw "$Name must be an absolute URL."
    }

    $allowedScheme = if ($HttpsOnly) {
        $uri.Scheme -eq [Uri]::UriSchemeHttps
    }
    else {
        $uri.Scheme -eq [Uri]::UriSchemeHttps -or
        $uri.Scheme -eq [Uri]::UriSchemeHttp
    }
    if (-not $allowedScheme) {
        throw "$Name has an unsupported URL scheme."
    }

    if ($OriginOnly -and ($uri.PathAndQuery -ne "/" -or -not [string]::IsNullOrEmpty($uri.Fragment))) {
        throw "$Name must be an origin without a path, query, or fragment."
    }
}

function Test-Configuration {
    param([Parameter(Mandatory = $true)] [System.Collections.IDictionary] $Settings)

    $connectionString = Get-RequiredSetting $Settings "ConnectionStrings__PostgreSql"
    foreach ($part in @("Host", "Database", "Username", "Password")) {
        if ($connectionString -notmatch "(?i)(^|;)\s*$part\s*=") {
            throw "ConnectionStrings__PostgreSql must contain $part."
        }
    }
    if ($connectionString -match '(?i)(^|;)\s*Password\s*=\s*(;|$)' -or
        $connectionString -match '(?i)Password\s*=\s*replace-me') {
        throw "ConnectionStrings__PostgreSql must contain a real database password."
    }

    $frontendOrigin = Get-RequiredSetting $Settings "Frontend__Origin"
    Test-AbsoluteWebUri $frontendOrigin "Frontend__Origin" -OriginOnly

    [void] (Get-RequiredSetting $Settings "Jwt__Issuer")
    [void] (Get-RequiredSetting $Settings "Jwt__Audience")
    $jwtSigningKey = Get-RequiredSetting $Settings "Jwt__SigningKey"
    if ([System.Text.Encoding]::UTF8.GetByteCount($jwtSigningKey) -lt 32) {
        throw "Jwt__SigningKey must contain at least 32 UTF-8 bytes."
    }

    $storageUrl = Get-RequiredSetting $Settings "SupabaseStorage__Url"
    Test-AbsoluteWebUri $storageUrl "SupabaseStorage__Url" -HttpsOnly
    [void] (Get-RequiredSetting $Settings "SupabaseStorage__ServiceRoleKey")
    [void] (Get-IntegerSetting $Settings "SupabaseStorage__RequestTimeoutSeconds" 1 120)
    [void] (Get-IntegerSetting $Settings "SupabaseStorage__MaxResponseBytes" 1024 1048576)
    [void] (Get-IntegerSetting $Settings "Upload__MaxFileSize" 1 1073741824)

    $bucketKeys = @(
        "SupabaseStorage__Buckets__Avatars",
        "SupabaseStorage__Buckets__ProjectImages",
        "SupabaseStorage__Buckets__CertificateFiles",
        "SupabaseStorage__Buckets__CvFiles"
    )
    $bucketValues = foreach ($bucketKey in $bucketKeys) {
        $bucket = Get-RequiredSetting $Settings $bucketKey
        if ($bucket -notmatch '^[a-z0-9][a-z0-9-]{1,62}[a-z0-9]$') {
            throw "$bucketKey is not a valid bucket name."
        }
        $bucket
    }
    if (($bucketValues | Sort-Object -Unique).Count -ne $bucketValues.Count) {
        throw "Supabase Storage bucket names must be distinct."
    }

    $bootstrapEnabledRaw = Get-RequiredSetting $Settings "BootstrapAdmin__Enabled"
    $bootstrapEnabled = $false
    if (-not [bool]::TryParse($bootstrapEnabledRaw, [ref] $bootstrapEnabled)) {
        throw "BootstrapAdmin__Enabled must be true or false."
    }
    if ($bootstrapEnabled) {
        $bootstrapEmail = Get-RequiredSetting $Settings "BootstrapAdmin__Email"
        $bootstrapPassword = Get-RequiredSetting $Settings "BootstrapAdmin__Password"
        if ($bootstrapEmail -notmatch '^[^@\s]+@[^@\s]+\.[^@\s]+$') {
            throw "BootstrapAdmin__Email must be a valid email address."
        }
        if ($bootstrapPassword.Length -lt 12) {
            throw "BootstrapAdmin__Password must contain at least 12 characters."
        }
    }
}

function Export-ProcessEnvironment {
    param([Parameter(Mandatory = $true)] [System.Collections.IDictionary] $Settings)

    foreach ($entry in $Settings.GetEnumerator()) {
        [Environment]::SetEnvironmentVariable(
            [string] $entry.Key,
            [string] $entry.Value,
            [EnvironmentVariableTarget]::Process)
    }
}

function Sync-ConfigurationToUserSecrets {
    param([Parameter(Mandatory = $true)] [System.Collections.IDictionary] $Settings)

    $secrets = [ordered]@{}
    foreach ($entry in $Settings.GetEnumerator()) {
        $secretName = ([string] $entry.Key).Replace("__", ":")
        $secrets[$secretName] = [string] $entry.Value
    }

    $json = $secrets | ConvertTo-Json -Depth 5
    $json | & dotnet user-secrets set --project $apiProject | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to synchronize .env with .NET User Secrets."
    }

    Write-Step "Synchronized configuration to .NET User Secrets for IDE launches."
}

function Invoke-DotNet {
    param(
        [Parameter(Mandatory = $true)] [string] $Description,
        [Parameter(Mandatory = $true)] [string[]] $Arguments
    )

    Write-Step $Description
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Description failed with exit code $LASTEXITCODE."
    }
}

function Invoke-Setup {
    $environmentPath = Resolve-EnvironmentPath $EnvironmentFile
    if (-not (Test-Path -LiteralPath $environmentPath -PathType Leaf)) {
        New-EnvironmentFile $environmentPath
    }

    $settings = Read-EnvironmentFile $environmentPath
    Test-Configuration $settings
    Export-ProcessEnvironment $settings
    Write-Step "Environment configuration is valid and loaded for setup commands."

    if ($ValidateOnly) {
        Write-Step "Validation completed."
        return
    }

    $sdkVersionText = (& dotnet --version).Trim()
    if ($LASTEXITCODE -ne 0) {
        throw ".NET SDK is not available on PATH."
    }
    $sdkVersion = $null
    if (-not [version]::TryParse($sdkVersionText, [ref] $sdkVersion) -or
        $sdkVersion.Major -ne 10) {
        throw ".NET SDK 10 is required by global.json."
    }
    Write-Step "Using .NET SDK $sdkVersionText."

    if ($SyncUserSecrets) {
        Sync-ConfigurationToUserSecrets $settings
    }

    Invoke-DotNet "Restoring local .NET tools" @("tool", "restore")
    Invoke-DotNet "Restoring solution packages" @("restore", $solution)
    Invoke-DotNet "Building the solution" @(
        "build", $solution, "--configuration", "Release", "--no-restore")
    Invoke-DotNet "Listing EF Core migrations" @(
        "ef", "migrations", "list",
        "--project", $infrastructureProject,
        "--startup-project", $apiProject,
        "--no-build")

    if ($ApplyMigrations) {
        Invoke-DotNet "Applying EF Core migrations to the configured database" @(
            "ef", "database", "update",
            "--project", $infrastructureProject,
            "--startup-project", $apiProject,
            "--no-build")
    }
    else {
        Write-Step "Migrations were not applied. Re-run with -ApplyMigrations after review."
    }

    Write-Step "Local setup completed."
}

try {
    Invoke-Setup
}
catch {
    Write-Host "[setup] ERROR: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
