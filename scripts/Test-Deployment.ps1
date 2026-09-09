#requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [uri] $BaseUri,

    [Parameter(Mandatory = $true)]
    [uri] $FrontendOrigin,

    [string] $PortfolioSlug = "portfolio",
    [string] $AdminEmail,
    [securestring] $AdminPassword,
    [switch] $VerifyContactRateLimit,
    [string] $StorageFixturePath,
    [switch] $AllowStorageReplacement
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$root = $BaseUri.AbsoluteUri.TrimEnd('/')
$origin = $FrontendOrigin.AbsoluteUri.TrimEnd('/')
$session = [Microsoft.PowerShell.Commands.WebRequestSession]::new()

function Invoke-Check {
    param([string] $Method, [string] $Path, [string] $Body)
    $parameters = @{
        Uri = "$root$Path"
        Method = $Method
        WebSession = $session
        SkipHttpErrorCheck = $true
        Headers = @{ Origin = $origin }
    }
    if ($PSBoundParameters.ContainsKey("Body")) {
        $parameters.ContentType = "application/json"
        $parameters.Body = $Body
    }
    Invoke-WebRequest @parameters
}

function Require-Status {
    param($Response, [int] $Status, [string] $Name)
    if ([int] $Response.StatusCode -ne $Status) {
        throw "$Name returned HTTP $([int] $Response.StatusCode), expected $Status."
    }
}

Require-Status (Invoke-Check GET "/health") 200 "Liveness"
Require-Status (Invoke-Check GET "/health/ready") 200 "Readiness"
Require-Status (Invoke-Check GET "/api/v1/portfolio/$PortfolioSlug/profile?locale=en") 200 "Public profile"

$problem = Invoke-Check POST "/api/v1/auth/login" "{}"
Require-Status $problem 400 "ProblemDetails"
if ($problem.Headers.'Content-Type' -notmatch '^application/problem\+json') {
    throw "Invalid login did not return application/problem+json."
}
if ($problem.Content -match "stackTrace|connection string|Password=") {
    throw "ProblemDetails exposed an unsafe implementation detail."
}

if ($VerifyContactRateLimit) {
    $response = $null
    foreach ($attempt in 1..6) {
        $response = Invoke-Check POST "/api/v1/contact" "{}"
    }
    Require-Status $response 429 "Contact rate limiting"
}

$hasEmail = -not [string]::IsNullOrWhiteSpace($AdminEmail)
$hasPassword = $null -ne $AdminPassword
if ($hasEmail -xor $hasPassword) {
    throw "AdminEmail and AdminPassword must be supplied together."
}

$accessToken = $null
if ($hasEmail) {
    $plainPassword = [System.Net.NetworkCredential]::new('', $AdminPassword).Password
    try {
        $loginBody = @{ email = $AdminEmail; password = $plainPassword } | ConvertTo-Json -Compress
        $login = Invoke-Check POST "/api/v1/auth/login" $loginBody
        Require-Status $login 200 "Admin login"
        $accessToken = ($login.Content | ConvertFrom-Json).data.accessToken
        if ([string]::IsNullOrWhiteSpace($accessToken)) { throw "Login response omitted accessToken." }
    }
    finally {
        $plainPassword = $null
    }
}

if (-not [string]::IsNullOrWhiteSpace($StorageFixturePath)) {
    if (-not $AllowStorageReplacement) {
        throw "Storage smoke replaces the current avatar; pass -AllowStorageReplacement explicitly."
    }
    if ([string]::IsNullOrWhiteSpace($accessToken)) {
        throw "Storage smoke requires admin credentials."
    }
    $fixture = Get-Item -LiteralPath $StorageFixturePath
    $upload = Invoke-WebRequest `
        -Uri "$root/api/v1/admin/profile/avatar" `
        -Method Post `
        -Headers @{ Authorization = "Bearer $accessToken"; Origin = $origin } `
        -Form @{ file = $fixture } `
        -SkipHttpErrorCheck
    Require-Status $upload 200 "Supabase Storage avatar replacement"
}

Write-Host "Deployment smoke checks passed for $root."
