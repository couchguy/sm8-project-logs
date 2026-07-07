# sm8-project-logs

ServiceM8 add-on for tracking long-term projects with structured daily logs. Designed for field service businesses that need to manage multi-day/multi-week jobs with daily progress entries, materials tracking, and sync back to ServiceM8.

## Add-on Name

**Project Logs** (for the ServiceM8 Add-on Store)

## Features (Planned)

- Daily log entries per job (hours, tasks, conditions, notes, photos)
- Materials/parts tracking per daily log, synced to ServiceM8 job materials
- Project-level progress view across multiple days
- OAuth 2.0 authentication (public add-on ready)
- Job Action integration in ServiceM8 UI

## Tech Stack

- **Runtime:** .NET 10 / C# (targeting Linux)
- **API:** ASP.NET Core minimal API
- **Database:** TBD (own storage for log data)
- **Auth:** OAuth 2.0 (ServiceM8 public add-on flow)

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

## Build & Run

```bash
dotnet restore
dotnet build
dotnet run --project src/ProjectLogs.Api
```

The API starts on `https://localhost:5001` by default.

## Project Structure

```
src/
  ProjectLogs.Api/       # ASP.NET Core minimal API
```
