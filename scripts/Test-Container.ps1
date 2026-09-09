[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $Image,

    [ValidateRange(1024, 65535)]
    [int] $HostPort = 18080,

    [ValidateRange(5, 60)]
    [int] $StartupTimeoutSeconds = 45
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Net.Http
$containerName = "portfolio-api-smoke-$([Guid]::NewGuid().ToString('N'))"
$baseUri = "http://127.0.0.1:$HostPort"
$handler = [System.Net.Http.HttpClientHandler]::new()
$handler.AllowAutoRedirect = $false
$client = [System.Net.Http.HttpClient]::new($handler)
$client.Timeout = [TimeSpan]::FromSeconds(10)
$client.DefaultRequestHeaders.TryAddWithoutValidation("Origin", "http://smoke.local") | Out-Null

function Invoke-SmokeRequest {
    param(
        [Parameter(Mandatory = $true)] [System.Net.Http.HttpMethod] $Method,
        [Parameter(Mandatory = $true)] [string] $Path,
        [string] $Json
    )

    $request = [System.Net.Http.HttpRequestMessage]::new($Method, "$baseUri$Path")
    if ($PSBoundParameters.ContainsKey("Json")) {
        $request.Content = [System.Net.Http.StringContent]::new(
            $Json,
            [System.Text.Encoding]::UTF8,
            "application/json")
    }

    try {
        return $client.SendAsync($request).GetAwaiter().GetResult()
    }
    finally {
        $request.Dispose()
    }
}

function Assert-Status {
    param(
        [Parameter(Mandatory = $true)] $Response,
        [Parameter(Mandatory = $true)] [int[]] $Expected,
        [Parameter(Mandatory = $true)] [string] $Check
    )

    $actual = [int] $Response.StatusCode
    if ($actual -notin $Expected) {
        $body = $Response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        throw "$Check returned HTTP $actual; expected $($Expected -join ', '). Body: $body"
    }
}

try {
    & docker image inspect $Image *> $null
    if ($LASTEXITCODE -ne 0) {
        throw "Docker image '$Image' does not exist."
    }

    $containerId = & docker run --detach --name $containerName `
        --publish "127.0.0.1:${HostPort}:8080" `
        --env ASPNETCORE_ENVIRONMENT=Development `
        --env Frontend__Origins__0=http://smoke.local `
        --env RateLimit__Contact__PermitLimit=5 `
        $Image
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($containerId)) {
        throw "Docker could not start the smoke container."
    }

    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($StartupTimeoutSeconds)
    do {
        try {
            $health = Invoke-SmokeRequest ([System.Net.Http.HttpMethod]::Get) "/health"
            if ([int] $health.StatusCode -eq 200) {
                $health.Dispose()
                break
            }
            $health.Dispose()
        }
        catch {
            if ([DateTimeOffset]::UtcNow -ge $deadline) { throw }
        }
        Start-Sleep -Milliseconds 500
    } while ([DateTimeOffset]::UtcNow -lt $deadline)

    if ([DateTimeOffset]::UtcNow -ge $deadline) {
        throw "Container did not become live within $StartupTimeoutSeconds seconds."
    }

    $ready = Invoke-SmokeRequest ([System.Net.Http.HttpMethod]::Get) "/health/ready"
    Assert-Status $ready @(503) "Dependency-free readiness"
    $ready.Dispose()

    $invalidLogin = Invoke-SmokeRequest ([System.Net.Http.HttpMethod]::Post) "/api/v1/auth/login" "{}"
    Assert-Status $invalidLogin @(400) "ProblemDetails validation"
    if ($invalidLogin.Content.Headers.ContentType.MediaType -ne "application/problem+json") {
        throw "Validation response is not application/problem+json."
    }
    $problemBody = $invalidLogin.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    if ($problemBody -match "stackTrace|connection string|Password=") {
        throw "ProblemDetails response contains an unsafe implementation detail."
    }
    $invalidLogin.Dispose()

    $public = Invoke-SmokeRequest ([System.Net.Http.HttpMethod]::Get) "/api/v1/portfolio/smoke/profile?locale=en"
    Assert-Status $public @(200, 404, 503) "Public profile endpoint"
    $public.Dispose()

    $contactJson = '{"senderName":"Smoke","senderEmail":"smoke@example.test","subject":"Container smoke","message":"Container smoke request","website":"bot-field"}'
    $lastContact = $null
    foreach ($attempt in 1..6) {
        if ($null -ne $lastContact) { $lastContact.Dispose() }
        $lastContact = Invoke-SmokeRequest ([System.Net.Http.HttpMethod]::Post) "/api/v1/contact" $contactJson
    }
    Assert-Status $lastContact @(429) "Contact rate limiting"
    $lastContact.Dispose()

    Write-Host "Container smoke checks passed for $Image."
}
catch {
    & docker logs $containerName 2>&1 | Write-Host
    throw
}
finally {
    $client.Dispose()
    $handler.Dispose()
    $cleanupErrorPreference = $ErrorActionPreference
    $ErrorActionPreference = "SilentlyContinue"
    & docker rm --force $containerName *> $null
    $ErrorActionPreference = $cleanupErrorPreference
}
