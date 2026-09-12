# Trusted Nginx Forwarded Headers Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the API honor scheme and client IP forwarded by the deployment's Nginx proxy while continuing to reject forwarded headers from every untrusted peer.

**Architecture:** Add a small `ReverseProxy` options boundary in `Portfolio.Api`, validate that Production names at least one explicit proxy IP, and configure ASP.NET Core's built-in Forwarded Headers Middleware with a one-hop limit. The Docker Compose network will use `172.30.0.0/24`; Nginx reaches the loopback-published container port and the API trusts only the Docker bridge gateway `172.30.0.1`.

**Tech Stack:** .NET 10, ASP.NET Core 10, `ForwardedHeadersMiddleware`, xUnit v3, `WebApplicationFactory<Program>`/`TestServer`, Docker Compose, Nginx.

**Spec:** `docs/operations/DOCKER_SETUP_DEPLOYMENT_GUIDE.md` section 8.2 and `docs/architecture/RATE_LIMITING_GUIDE.md` section 7.

## Global Constraints

- Process forwarded headers before HSTS, HTTPS redirection, Serilog request logging, authentication, and rate limiting.
- Trust only literal IP addresses configured under `ReverseProxy:KnownProxies`; do not add the middleware when the list is empty.
- Accept only `X-Forwarded-For` and `X-Forwarded-Proto`, with `ForwardLimit = 1` for the single Nginx hop.
- Production must fail startup when no trusted proxy is configured or an entry is not a literal IP address.
- Keep `tests/Portfolio.IntegrationTests/Api/ContactsApiTests.cs::SpoofedForwardedHeadersDoNotBypassConnectionRateLimit` passing.
- Do not add a package: the required middleware and TestServer APIs are already available through the ASP.NET Core shared framework and existing test dependencies.

---

### Task 1: Trusted proxy configuration and request-pipeline behavior

**Files:**
- Create: `src/Portfolio.Api/Configuration/ReverseProxyOptions.cs`
- Create: `src/Portfolio.Api/Configuration/ReverseProxyOptionsValidator.cs`
- Create: `src/Portfolio.Api/Configuration/ConfigureForwardedHeadersOptions.cs`
- Create: `tests/Portfolio.IntegrationTests/Api/ForwardedHeadersApiTests.cs`
- Modify: `src/Portfolio.Api/Extensions/ServiceCollectionExtensions.cs`
- Modify: `src/Portfolio.Api/Program.cs`
- Modify: `src/Portfolio.Api/appsettings.json`
- Modify: `.env.example`
- Modify: `tests/Portfolio.IntegrationTests/Authentication/ProductionStartupTests.cs`
- Modify: `docs/operations/DOCKER_SETUP_DEPLOYMENT_GUIDE.md`
- Modify: `docs/architecture/RATE_LIMITING_GUIDE.md`

**Interfaces:**
- Consumes: configuration keys `ReverseProxy:KnownProxies:{index}` containing literal IPv4 or IPv6 addresses.
- Produces: `ReverseProxyOptions`, `ReverseProxyOptionsValidator`, and `ConfigureForwardedHeadersOptions`; Production startup validation; trusted one-hop processing of `X-Forwarded-For` and `X-Forwarded-Proto`.

- [x] **Step 1: Write failing request-pipeline tests**

Create `tests/Portfolio.IntegrationTests/Api/ForwardedHeadersApiTests.cs` with a factory configured with `ReverseProxy:KnownProxies:0=172.30.0.1`. Use `factory.Server.SendAsync` to set `Connection.RemoteIpAddress`, request path `/health`, and forwarded headers before the real application pipeline runs.

The first test sets the connection peer to `172.30.0.1`, `X-Forwarded-For` to `198.51.100.25`, and `X-Forwarded-Proto` to `https`; assert the completed context has client IP `198.51.100.25`, scheme `https`, and status `200`.

The second test sets the connection peer to untrusted `192.0.2.10` with the same forwarded headers; assert the completed context retains IP `192.0.2.10` and scheme `http`.

- [x] **Step 2: Add a failing Production startup case**

In `ProductionStartupTests`, configure the factory's valid baseline with:

```csharp
builder.UseSetting("ReverseProxy:KnownProxies:0", "172.30.0.1");
```

Add this theory row:

```csharp
[InlineData("ReverseProxy:KnownProxies:0", "not-an-ip", "literal IP")]
```

Add a separate test that supplies an empty `ReverseProxy:KnownProxies:0` and asserts Production startup fails with a message containing `at least one trusted proxy`.

- [x] **Step 3: Run the focused tests and verify RED**

Run:

```powershell
dotnet test tests/Portfolio.IntegrationTests/Portfolio.IntegrationTests.csproj --configuration Release --filter "FullyQualifiedName~ForwardedHeadersApiTests|FullyQualifiedName~ProductionStartupTests"
```

Expected: the new tests fail because no `ReverseProxy` options, validator, forwarded-header configuration, or middleware registration exists.

- [x] **Step 4: Add the options and validator**

Create `ReverseProxyOptions` with `SectionName = "ReverseProxy"` and `string[] KnownProxies`. Create `ReverseProxyOptionsValidator` with a constructor flag matching the existing production-validator pattern. Outside Development, require at least one non-empty entry and reject any entry for which `IPAddress.TryParse` returns false.

- [x] **Step 5: Configure the built-in middleware securely**

Implement `IConfigureOptions<ForwardedHeadersOptions>` in `ConfigureForwardedHeadersOptions`. Set:

```csharp
options.ForwardedHeaders =
    ForwardedHeaders.XForwardedFor |
    ForwardedHeaders.XForwardedProto;
options.ForwardLimit = 1;
options.KnownIPNetworks.Clear();
options.KnownProxies.Clear();
```

Parse every validated configured address with `IPAddress.Parse` and add it to `KnownProxies`.

Register the bound options, validator, and `IConfigureOptions<ForwardedHeadersOptions>` in `AddApiServices`. Keep Development valid with an empty list, which means no proxy is trusted.

- [x] **Step 6: Put middleware first in the request pipeline**

In `Program.cs`, resolve `ReverseProxyOptions` and add the middleware only when at least one proxy is configured:

```csharp
var reverseProxy = app.Services.GetRequiredService<IOptions<ReverseProxyOptions>>().Value;
if (reverseProxy.KnownProxies.Length > 0)
{
    app.UseForwardedHeaders();
}
```

immediately after `builder.Build()` and before the non-Development HSTS branch. This ensures HTTPS redirection, request logging, authentication, and IP rate limiting see the normalized scheme and remote address.

- [x] **Step 7: Add configuration contracts**

Add this non-trusting default to `appsettings.json`:

```json
"ReverseProxy": {
  "KnownProxies": []
}
```

Add `ReverseProxy__KnownProxies__0=127.0.0.1` to `.env.example` as a local-only example. Document the DigitalOcean Compose value `ReverseProxy__KnownProxies__0=172.30.0.1`, the fixed `172.30.0.0/24` deployment network, the one-hop assumption, and a requirement to re-check the observed peer IP if topology changes.

- [x] **Step 8: Run focused tests and verify GREEN**

Run the focused command from Step 3. Expected: all selected tests pass, including the existing spoofed-header rate-limit regression.

- [x] **Step 9: Run repository verification**

Run:

```powershell
dotnet format Portfolio.sln --verify-no-changes --no-restore
dotnet build Portfolio.sln --configuration Release --no-restore
dotnet test Portfolio.sln --configuration Release --no-build
docker build --tag portfolio-api:forwarded-headers .
./scripts/Test-Container.ps1 -Image portfolio-api:forwarded-headers
```

Expected: formatting, Release build, full test suite, image build, non-root assertion, and container smoke test all pass.

- [ ] **Step 10: Verify the deployed trust boundary before public cutover**

After Compose is introduced with subnet `172.30.0.0/24`, add `ReverseProxy__KnownProxies__0=172.30.0.1` to the protected production `app.env`. Confirm requests through Nginx observe HTTPS and distinct client IP rate-limit partitions; confirm direct requests to loopback port `8080` carrying spoofed forwarded headers remain untrusted. Keep UFW closed for port `8080`.
