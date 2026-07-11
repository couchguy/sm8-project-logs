# sm8-project-logs

## What This Is

ServiceM8 add-on ("Project Logs") for long-term project tracking with daily logs and materials sync. Turns a normal ServiceM8 job into a project with structured, dated daily logs where materials become billable line items.

## Stack

- C# / .NET 10, targeting Linux
- ASP.NET Core minimal API (single `POST /sm8` callback)
- Azure SQL + EF Core (daily log store)
- Azure App Service for hosting
- Azure Key Vault for the add-on app secret

## Build & Run

```bash
dotnet restore
dotnet build
dotnet run --project src/ProjectLogs.Api
```

## Config

- `ConnectionStrings:ProjectLogs` — Azure SQL connection string
- `ServiceM8:AppId` — add-on App ID from Developer Portal
- `ServiceM8:AppSecret` — add-on app secret (validates JWTs + OAuth client secret)
- `ServiceM8:CallbackBaseUrl` — public HTTPS URL (e.g. `https://your-app.azurewebsites.net`)
- Use `dotnet user-secrets` for local dev, Key Vault for production

## EF Core Migrations

```bash
dotnet ef migrations add <Name> --project src/ProjectLogs.Api
dotnet ef database update --project src/ProjectLogs.Api
```

## Project Structure

```
src/ProjectLogs.Api/
  Program.cs                          # Service registration, endpoints, error handling
  Data/ProjectLogsDbContext.cs        # EF Core context + model config
  Entities/                           # ProjectFlag, DailyLog, DailyLogLine, TenantRegistration
  ServiceM8/                          # JWT validator, typed REST client, OAuth, API models
  Handlers/                           # Event handlers (enable, open, close-out, webhook)
  Views/PopupRenderer.cs              # HTML rendering for SM8 popup iframe
  Migrations/                         # EF Core migrations (InitialCreate)
tests/ProjectLogs.Tests/              # Integration tests (23 tests, LocalDB)
manifest.json                         # Reference manifest (register in SM8 Developer Portal)
servicem8-daily-logs-module.md        # Full build brief / spec
```

## ServiceM8 Integration

- **Per-request auth:** SM8 sends a temp token (900s TTL) in each JWT invocation
- **Stored auth:** Full OAuth 2.0 code grant for activation + stored refresh tokens
- **OAuth URLs:** authorize at `https://go.servicem8.com/oauth/authorize`, token at `https://go.servicem8.com/oauth/access_token`
- **JWT:** HS256 signed with app secret; POST body IS the raw JWT
- **API base:** `https://api.servicem8.com/api_1.0/`
- **Endpoints used:** Job, JobMaterial, Note
- **Scopes:** `read_jobs read_job_materials manage_job_materials publish_job_notes`
- **SDK:** `https://platform.servicem8.com/sdk/1.0/sdk.css` and `sdk.js`

## Endpoints

| Route | Purpose |
|---|---|
| `GET /health` | Health check |
| `GET /sm8/activate` | OAuth activation — redirects to SM8 authorize |
| `GET /sm8/oauth/callback` | OAuth callback — exchanges code, stores tokens |
| `POST /sm8` | SM8 callback — JWT actions, webhooks, invoke |

## Event Routing

| Event Name | Trigger | Handler |
|---|---|---|
| `enable_project` | "Enable Project Tracking" job card button | Upserts ProjectFlag |
| `open_daily_log` | "Daily Log" job card button | Renders day-grid popup |
| `close_out` | `client.invoke()` from popup JS | Diff + snapshot + diary Note |
| `webhook_subscription` | Job status webhook | Logs completion, future: auto close-out |

## Key Design Rules

- Multi-tenant: every table has `AccountUuid`, resolved per-request from JWT
- Opt-in per job via `ProjectFlag` — no flag = add-on does nothing
- `SourceJobMaterialUuid` is UNIQUE — makes the close-out diff reliable
- Our DB is the dated source of truth; SM8 holds the billable lines
- Soft-delete: disabling tracking keeps history
- HTTPS mandatory, no X-Frame-Options (must be iframe-embeddable)

## Sibling Add-ons (future, same backend)

- **Timesheet / project-labour** — extends close-out with `JobActivity` snapshots
- **NetSuite connector** — syncs customers, materials, invoices via SuiteTalk REST

## Gotchas

- SM8 has only 4 fixed job statuses — can't extend them
- `JobMaterial` has no user-settable date field — day attribution comes from our close-out diff
- Webhook eventName is always `webhook_subscription` (not configurable)
- Actions need separate manifest entries for `type: "online"` and `type: "app"`
- `callbackUrl` is set in the Developer Portal, NOT in the manifest
- Add-on name must be ≤30 chars, can't include "ServiceM8" or "M8"
