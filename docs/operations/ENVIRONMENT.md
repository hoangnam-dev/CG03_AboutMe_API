# Production Environment Reference

Use `.env.example` as the canonical list of names. It contains no usable credentials and is not loaded automatically by ASP.NET Core. Configure these values in the deployment platform's environment/secret store; never copy a populated `.env` into the image.

## Required groups

| Group | Purpose | Secret |
| --- | --- | --- |
| `ConnectionStrings__PostgreSql` | Runtime PostgreSQL role connection | Yes |
| `Frontend__Origins__N` | Exact credentialed CORS origins, contiguous from index 0 | No |
| `Jwt__*` | Issuer/audience, active key ID, mounted PFX path/password, optional validation certificates | PFX/password yes |
| `RefreshToken__*` | Session lifetimes, cookie names, independent HMAC pepper | Pepper yes |
| `SupabaseStorage__*` | HTTPS project URL, server-only legacy service-role JWT, bucket names and client limits | Key yes |
| `BootstrapAdmin__*` | One-time administrator creation | Password yes; normally disabled |
| `Upload__*`, `SkillIcons__*` | Upload limits and external icon host allowlist | No |
| `RateLimit__Auth__*`, `RateLimit__Contact__*` | Fixed-window limits | No |

## Container contract

- Runtime environment: `ASPNETCORE_ENVIRONMENT=Production`.
- Listener: container port `8080`; the platform terminates TLS and forwards HTTP.
- JWT certificate: mount read-only, for example `/run/secrets/jwt-signing.pfx`.
- Persistent files: Supabase Storage only; container disk is disposable.
- Swagger: disabled in Production by application code.
- Migrations: absent from container startup and executed by the controlled runner in `MIGRATIONS.md`.

Before rollout, compare platform keys with `.env.example`, confirm no placeholder or blank required secret remains, and verify the runtime database role is not the migration owner. Never print environment values in CI or incident logs.
