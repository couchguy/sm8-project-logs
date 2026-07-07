# sm8-project-logs

ServiceM8 add-on for tracking long-term projects with structured daily logs. Turns a normal ServiceM8 job into a project where techs record daily materials, which become billable line items on the job.

## Add-on Name

**Project Logs** (for the ServiceM8 Add-on Store)

## How It Works

1. Open a job card in ServiceM8 and tap **Enable Project Tracking** to opt the job in.
2. Techs add materials to the job as usual throughout the day.
3. At end of day, tap **Daily Log** on the job card and hit **Close out today's log**.
4. The add-on snapshots all new (unclaimed) materials into a dated log entry.
5. A summary Note is posted to the ServiceM8 diary as a breadcrumb.
6. Repeat daily. The full day-by-day breakdown lives in the add-on's own database.

## Tech Stack

- **Runtime:** .NET 10 / C# (targeting Linux)
- **API:** ASP.NET Core minimal API — single `POST /sm8` callback
- **Database:** Azure SQL + EF Core
- **Hosting:** Azure App Service
- **Auth:** ServiceM8 serverless OAuth (temp token per invocation, HS256 JWT)

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Azure SQL database (or SQL Server for local dev)
- ServiceM8 developer account with a registered add-on

## Build & Run

```bash
dotnet restore
dotnet build
dotnet run --project src/ProjectLogs.Api
```

The API starts on `https://localhost:5001` by default.

## Configuration

| Key | Description |
|-----|-------------|
| `ConnectionStrings:ProjectLogs` | SQL Server / Azure SQL connection string |
| `ServiceM8:AppSecret` | Add-on app secret from ServiceM8 Developer Portal |

Use `dotnet user-secrets` for local development:

```bash
cd src/ProjectLogs.Api
dotnet user-secrets init
dotnet user-secrets set "ServiceM8:AppSecret" "your-secret-here"
dotnet user-secrets set "ConnectionStrings:ProjectLogs" "Server=localhost;Database=ProjectLogs;Trusted_Connection=true;TrustServerCertificate=true"
```

## Database Migrations

```bash
dotnet ef migrations add InitialCreate --project src/ProjectLogs.Api
dotnet ef database update --project src/ProjectLogs.Api
```

## Project Structure

```
src/ProjectLogs.Api/          # ASP.NET Core minimal API
  Entities/                   # EF Core entities (ProjectFlag, DailyLog, DailyLogLine)
  Data/                       # DbContext + model configuration
  ServiceM8/                  # JWT validation, typed HTTP client, API models
  Handlers/                   # Event handlers per SM8 action/webhook
  Views/                      # HTML popup rendering (sdk.css/sdk.js)
manifest.json                 # Reference manifest for SM8 Developer Portal
```

## ServiceM8 Setup

1. Register a new add-on at the [ServiceM8 Developer Portal](https://developer.servicem8.com/).
2. Set the callback URL to your deployed endpoint (e.g. `https://your-app.azurewebsites.net/sm8`).
3. Upload `manifest.json` (fill in `iconURL` fields first).
4. Copy the app secret into your configuration.
5. Install the add-on on your ServiceM8 account.
