# sm8-project-logs

## What This Is

ServiceM8 add-on ("Project Logs") for long-term project tracking with daily logs and materials sync.

## Stack

- C# / .NET 10, targeting Linux
- ASP.NET Core minimal API
- OAuth 2.0 (ServiceM8 public add-on flow)
- Own database (TBD — stores daily log entries, materials per log)

## Build & Run

```bash
dotnet restore
dotnet build
dotnet run --project src/ProjectLogs.Api
```

## ServiceM8 Integration Points

- **Auth:** OAuth 2.0 — token exchange via ServiceM8 developer portal
- **API base:** `https://api.servicem8.com/api_1.0/`
- **Key endpoints:** Job, JobMaterial, Note, Attachment, Job Key-Value Storage
- **Webhooks:** Job status changes, FormResponse submissions
- **UI hooks:** Job Actions (button on job card), Menu Items

## Architecture Notes

- Daily logs stored in our own DB, not ServiceM8. ServiceM8 gets a summary Note per day.
- Parts added to daily logs sync to ServiceM8 JobMaterial on the parent job.
- OAuth tokens stored per-account; refresh flow needed.
- Add-on SDK manifest needed for Job Action UI integration.

## Key Decisions

- Public add-on ready from day one (OAuth, not API key).
- Minimal API style (no controllers).
- Solution file: `ProjectLogs.sln`

## Gotchas

- ServiceM8 has only 4 fixed job statuses (Quote, Work Order, Completed, Unsuccessful) — can't extend them.
- Job Key-Value Storage is per-add-on, good for metadata but not queryable.
- Custom fields require Public Application registration.
- Add-on name must be ≤30 chars, can't include "ServiceM8" or "M8".
