# ServiceM8 Daily Logs Add-on — Build Brief

A handoff spec for implementing the **Daily Logs** add-on. Read the whole thing before writing code. Where a fact must be confirmed against ServiceM8's live docs rather than assumed, it is marked **⚠️ VERIFY** — do not hardcode guesses for those.

---

## 1. Context & Goal

We're building a ServiceM8 add-on that turns a normal ServiceM8 **job** into a long-running **project** with structured, dated **daily logs**. ServiceM8 has no native multi-job "project" container and no native per-day log that produces billable line items — that gap is the product.

A tech working a long job (weeks/months) needs to record, per day, what materials and activity happened, and have those materials become real billable line items on the job. The native diary is a flat chronological feed and native Forms can't create billable line items or read the materials catalog — so we own a thin layer on top of ServiceM8's API, backed by our own database as the dated source of truth.

**This module = the Daily Logs add-on only.** It is one of three sibling add-ons sharing one backend (see §2). Build it so the shared patterns (multi-tenant, `accountUUID` partitioning) are correct from day one.

---

## 2. Scope

### In scope (this module)
- ServiceM8 web-service-hosted add-on: manifest + single .NET callback endpoint.
- Two job-card actions: **Enable Project Tracking** (opt-in) and **Daily Log** (the popup UI).
- One webhook: job status change (to finalise/close-out on job completion).
- Writing materials as `JobMaterial` line items (billing stays job-centric and native).
- Our own database holding the dated daily-log structure.
- Writing a per-log summary back to the ServiceM8 diary as a Note.
- Multi-tenant from day one, partitioned by `accountUUID`.

### Out of scope (siblings — design the backend to not preclude them)
- **Timesheet / project-labour add-on** (private): richer time capture/editing, writes `JobActivity`. Labour snapshots will later extend the Daily Logs close-out, so leave room for a `JobActivity` snapshot alongside materials.
- **NetSuite connector** (public): syncs customers, materials, invoices via SuiteTalk REST, keyed by NetSuite `externalId` = ServiceM8 UUID.

These are separate add-ons/listings. Don't build them now; just don't make the shared backend single-tenant or Daily-Logs-only in a way that blocks them.

---

## 3. Architecture Overview

```
ServiceM8 (web + iOS app)
   │  user taps job-card action  ──────────────►  signed JWT (+ temp OAuth token, job ctx)
   │  job status webhook         ──────────────►
   ▼
Your .NET callback endpoint (Azure)
   │  validate JWT  →  resolve tenant (accountUUID)  →  switch on eventName
   │  render popup HTML (sdk.css + sdk.js)  OR  process webhook
   │
   ├─►  ServiceM8 REST API (Bearer = temp OAuth token)   read/write JobMaterial, Note
   └─►  Azure SQL (EF Core)   DailyLog / DailyLogLine, partitioned by AccountUuid
```

Key properties:
- The popup is **web content served live from your endpoint**, rendered in a ServiceM8 **iframe**. It needs connectivity to open. Native material capture (the item picker) stays the offline-safe path; our close-out is a deliberate online tap.
- **Serverless OAuth**: ServiceM8 issues a short-lived OAuth token with each invocation, so we don't run a full OAuth token-storage flow for the UI path. Use that token as the Bearer for API calls made during that request. **⚠️ VERIFY** token lifetime and how it's delivered in the request against the Add-on SDK docs.

---

## 4. Tech Stack

- **.NET (C#)** — ASP.NET Core Web API (or minimal API). Single callback endpoint.
- **Azure App Service** for hosting to start (no cold-start friction; simplest with EF Core). Can move hot paths to **Azure Functions** later if cost/scale warrants.
- **Azure SQL** + **EF Core** for the log store.
- **Azure Key Vault** for the ServiceM8 add-on app secret (used to validate inbound JWTs).
- **HTTPS mandatory** on the endpoint. Do **not** send `X-Frame-Options: DENY` — content must be iframe-embeddable by ServiceM8.

---

## 5. ServiceM8 Add-on Model (how this works)

An add-on is external web code ServiceM8 triggers and that calls back through the REST API. Two trigger types matter here:

- **Action** — a button declared in the manifest that appears on the Job Card (online **and** in the iOS app). Tapping it invokes your callback URL with an `eventName`. For actions, your endpoint must **return HTML/JS that ServiceM8 renders in a popup**.
- **Webhook** — ServiceM8 notifies your endpoint when subscribed fields change on a record. The notification carries identifiers; you then fetch the record via the API. **⚠️ VERIFY** exact webhook payload shape (whether it includes changed values or just the resource URL/UUID).

Every invocation arrives as a **JWT signed with your add-on's app secret**, carrying the job/staff/account context plus the temporary OAuth token. **⚠️ VERIFY** the exact claim names and signing algorithm from the Add-on SDK docs before implementing validation.

Reference sample add-ons (Node + Python), including one that adds a Job Card button that opens a popup, and a webhook example: <https://github.com/servicem8/addon-sdk-samples>.

---

## 6. Key Design Decisions (already settled — do not re-litigate)

1. **Opt-in per job.** A job is only a "project" when a user taps **Enable Project Tracking**. No global default. The flag lives in **our** DB as a row keyed by `AccountUuid + JobUuid`. No row = ordinary job, add-on does nothing. The webhook handler must cheaply check "is this one of ours?" before acting.
2. **Our DB is the source of truth** for the dated log structure. ServiceM8 holds the billable lines; we hold the day-by-day truth.
3. **Materials become `JobMaterial` line items** so billing is native/job-centric. A form can't do this — that's why we exist.
4. **`JobMaterial` has no user-settable date field** (only system `edit_date` and `sort_order`). So day attribution comes from our own store via a **close-out diff**, not from ServiceM8. This is the crux of the whole design.
5. **Multi-tenant from day one.** Every table carries `AccountUuid`; every query is filtered by it; it's resolved per-request from the JWT.
6. **Soft-delete semantics.** Disabling project tracking stops new logging but keeps history. Claimed lines that are later edited/deleted in ServiceM8 stay attributed to their original day with their original snapshot values.

---

## 7. Data Model (EF Core)

Two tables. The linchpin is that each log line stores the **source `JobMaterial` UUID**, and that column is **unique** — which makes "new since last close-out" a trivial, reliable diff. Every table is partitioned by `AccountUuid`.

```csharp
public class ProjectFlag            // the opt-in record
{
    public int Id { get; set; }
    public string AccountUuid { get; set; }   // tenant
    public string JobUuid { get; set; }
    public bool Enabled { get; set; }
    public DateTime EnabledAtUtc { get; set; }
    public string EnabledByStaffUuid { get; set; }
}

public class DailyLog
{
    public int Id { get; set; }
    public string AccountUuid { get; set; }   // tenant

    public string JobUuid { get; set; }        // ServiceM8 job UUID
    public string? JobNumber { get; set; }     // denormalized for display
    public DateOnly LogDate { get; set; }      // the work date (set at source)
    public string StaffUuid { get; set; }      // who closed it out

    public string? DiaryNoteUuid { get; set; } // the Note we posted back
    public string? Summary { get; set; }       // e.g. "3 items, 2.5 hrs"

    public DateTime ClosedAtUtc { get; set; }  // audit: when close-out ran
    public ICollection<DailyLogLine> Lines { get; set; } = new List<DailyLogLine>();
}

public class DailyLogLine
{
    public int Id { get; set; }
    public int DailyLogId { get; set; }
    public DailyLog DailyLog { get; set; }

    public string SourceJobMaterialUuid { get; set; } // the claim key — UNIQUE per account
    public string? MaterialUuid { get; set; }         // catalog item ref
    public string Name { get; set; }                  // name as logged
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal? UnitCost { get; set; }
    public decimal LineTotal { get; set; }

    public DateTime SnapshotAtUtc { get; set; }
}
```

Constraints and indexes:

```csharp
// A JobMaterial belongs to one log, ever — scoped per tenant.
modelBuilder.Entity<DailyLogLine>()
    .HasIndex(l => new { l.SourceJobMaterialUuid }).IsUnique();

// Fast per-job, per-day lookups.
modelBuilder.Entity<DailyLog>()
    .HasIndex(d => new { d.AccountUuid, d.JobUuid, d.LogDate });

// Fast "is this job one of ours?" checks from the webhook.
modelBuilder.Entity<ProjectFlag>()
    .HasIndex(f => new { f.AccountUuid, f.JobUuid }).IsUnique();
```

> Growth hook: labour snapshots will add a `DailyLogLabour` table sourced from `JobActivity` (which already carries `start_date`/`end_date`/`staff_uuid`, so no date hack needed). Not in this module — leave the close-out extensible.

---

## 8. Manifest

Conceptual shape — **⚠️ VERIFY every key name, nesting, and allowed value against the ServiceM8 Manifest Reference** on the Developer Portal before running. Request only the scopes actually used now.

```json
{
  "name": "Project Daily Logs",
  "version": "1.0",
  "callbackUrl": "https://<your-app>.azurewebsites.net/sm8",
  "scopes": [
    "read_jobs",
    "read_job_materials",
    "manage_job_materials",
    "publish_job_notes"
  ],
  "actions": [
    { "label": "Enable Project Tracking", "type": "job", "eventName": "enable_project" },
    { "label": "Daily Log",               "type": "job", "eventName": "open_daily_log" }
  ],
  "webhooks": [
    { "object": "job", "fields": ["status"], "eventName": "job_status_changed" }
  ]
}
```

Notes:
- `name` must be ≤ 30 chars, unique, and must **not** contain "ServiceM8" or allusions ("M8", "Mate", "SM8") — relevant only if listed publicly later, but keep the rule in mind.
- Scopes appear on the user consent screen, so keep them minimal. Add `read_schedule`/`manage_schedule` only when labour snapshots land.

---

## 9. Popup Shell (native styling from first render)

Serve semantic HTML, link **sdk.css** (which bundles normalize.css — do **not** add your own reset), and include **sdk.js** for popup window control and server callbacks. **⚠️ VERIFY the exact sdk.css and sdk.js include URLs** from the Add-on Style and Client JS SDK docs — do not guess these.

```html
<!DOCTYPE html>
<html>
<head>
  <meta charset="utf-8">
  <!-- ⚠️ VERIFY exact URLs from ServiceM8 docs -->
  <link rel="stylesheet" href="{{SDK_CSS_URL}}">
  <script src="{{SDK_JS_URL}}"></script>
</head>
<body>
  <h1>Daily Log</h1>
  <table><!-- day grid; inherits sdk.css styling, no custom reset --></table>
  <button onclick="closeOut()">Close out today's log</button>

  <script>
    var client = SMClient.init();
    client.resizeWindow(720, 600);

    function closeOut() {
      // invoke() calls back to your endpoint with a new eventName,
      // no full reload — run the JobMaterial diff server-side, then re-render.
      client.invoke("close_out", { logDate: todayISO() })
        .then(function () { /* refresh the grid */ });
    }
  </script>
</body>
</html>
```

Rely on sdk.css base styles for buttons, inputs, tables, headers, fonts. Reserve custom CSS only for genuinely custom bits (the day grid). This is also how the add-on stays visually indistinguishable from the platform, which is the intended design bar. Note: sdk.css gives web-dashboard parity; inside the iOS app it renders your HTML consistently but won't perfectly impersonate every native iOS control.

---

## 10. .NET Endpoint Design

Single endpoint, e.g. `POST /sm8`. Flow for every request:

1. **Validate the JWT** using the add-on app secret (from Key Vault). Reject if invalid/expired. **⚠️ VERIFY** signing algorithm and claim names against the SDK docs.
2. **Resolve tenant**: extract `accountUUID` from the JWT → this is the partition key for all DB access in this request. Every EF query in the request must filter by it.
3. **Extract context**: `jobUuid`, `staffUuid`, `eventName`, and the temporary **OAuth access token**.
4. **Switch on `eventName`** and handle:

| eventName            | Trigger                       | Action |
|----------------------|-------------------------------|--------|
| `enable_project`     | "Enable Project Tracking" tap | Upsert `ProjectFlag` (Enabled = true). Return a small confirmation popup. |
| `open_daily_log`     | "Daily Log" tap               | Load current logs for the job from our DB + current `JobMaterial` from the API; render the day-grid popup. |
| `close_out`          | `invoke()` from the popup     | Run the close-out diff (§11), persist the new `DailyLog` + lines, write the diary Note (§12), return refreshed grid. |
| `job_status_changed` | job status webhook            | If job is one of ours and status = completed/closed, finalise (optional auto close-out or lock). Otherwise no-op. |

5. **Call the ServiceM8 REST API** with the temp OAuth token as `Authorization: Bearer <token>`. Base: `https://api.servicem8.com/api_1.0/{object}.json`.

Keep API access behind a small typed client (`IServiceM8Client`) so the sibling add-ons can reuse it.

---

## 11. Close-out Diff Logic

Fetch the job's current `JobMaterial` records via the API, drop any already claimed in our DB (for this tenant), snapshot the rest into a new `DailyLog` for the given `LogDate`.

```csharp
// claimed = source JobMaterial UUIDs already logged for this job + tenant
var claimed = db.DailyLogLines
    .Where(l => l.DailyLog.AccountUuid == accountUuid
             && l.DailyLog.JobUuid == jobUuid)
    .Select(l => l.SourceJobMaterialUuid)
    .ToHashSet();

// jobMaterials = current JobMaterial records pulled from the ServiceM8 API
var newLines = jobMaterials
    .Where(m => !claimed.Contains(m.Uuid))
    .ToList();

// build DailyLog + DailyLogLine snapshots from newLines, save.
```

Behaviours that fall out of snapshotting (expected, not bugs):
- Editing a claimed line in ServiceM8 doesn't change its UUID, so it stays on its original day with the original snapshot values.
- Deleting a line in ServiceM8 marks it inactive there; our log still holds it.
- Reconciliation of post-claim edits is deliberately **out of scope** for v1.

---

## 12. Diary Write-back

After a successful close-out, post a per-log summary to the ServiceM8 job diary as a **Note** (scope `publish_job_notes`), e.g. `"Daily Log — 2026-06-28: 3 items, 2.5 hrs"`. Store the returned note UUID on `DailyLog.DiaryNoteUuid`. The rich per-log view lives in our own popup, not the diary — the Note is just a lightweight dated breadcrumb. (Notes are user-editable/deletable, so they are not a tamper-proof record; a PDF attachment would be, but that's not needed for v1.)

---

## 13. Build Order

1. Scaffold ASP.NET Core Web API, Azure App Service, Azure SQL, Key Vault; wire config.
2. EF Core models + migrations (§7).
3. JWT validation + tenant resolution middleware (§10 steps 1–3). **⚠️ VERIFY claims first.**
4. `IServiceM8Client` typed REST client (auth via per-request temp token).
5. `enable_project` handler + `ProjectFlag` upsert + confirmation popup.
6. Popup shell with sdk.css/sdk.js (§9). **⚠️ VERIFY SDK URLs first.**
7. `open_daily_log` handler: render day grid from DB + live `JobMaterial`.
8. `close_out` handler: diff (§11), persist, diary Note (§12), re-render via `invoke()`.
9. `job_status_changed` webhook handler (ours-only check, finalise on completion).
10. End-to-end test against a private dev install before anything else.

---

## 14. Must VERIFY Against ServiceM8 Docs Before Coding

Do not trust these from memory — confirm each against the live Developer Portal:
- Exact **manifest** key names, nesting, and allowed `type`/`eventName` conventions (Manifest Reference).
- Exact **sdk.css** and **sdk.js** include URLs (Add-on Style; Client JS SDK).
- **JWT** signing algorithm and claim names (how `accountUUID`, `jobUuid`, `staffUuid`, temp OAuth token are delivered).
- **Serverless OAuth** token lifetime and delivery.
- **Webhook** payload shape (values vs resource URL/UUID) and the correct `object`/`fields` identifiers for job status.
- Exact **scope** strings.
- `JobMaterial` field names for read/write (quantity, price, cost, tax rate, job link).

---

## 15. ServiceM8 Documentation — Reference Links

- Developer Portal (root): <https://developer.servicem8.com/>
- Add-on Types: <https://developer.servicem8.com/docs/add-on-types>
- Add-on Capabilities: <https://developer.servicem8.com/docs/add-on-capabilities>
- Web Service Hosted Add-ons (iframe, HTTPS, callback URL, OAuth): <https://developer.servicem8.com/docs/web-service-hosted-add-ons>
- Add-on Style & Guidelines (sdk.css, normalize.css): <https://developer.servicem8.com/docs/add-on-style>
- Client JS SDK (sdk.js: `SMClient.init`, `resizeWindow`, `close`, `invoke`): <https://developer.servicem8.com/docs/client-api>
- Examples (Hello World, Showcase, Webhook samples): <https://developer.servicem8.com/docs/examples>
- Add-on SDK code samples (Node + Python): <https://github.com/servicem8/addon-sdk-samples>

> The Manifest Reference and OAuth/authentication pages live under the Developer Portal docs nav — locate and follow those for the **⚠️ VERIFY** items above.

---

## 16. ServiceM8 REST API — Reference

- Base URL pattern: `https://api.servicem8.com/api_1.0/{object}.json`
- Objects relevant now: **`JobMaterial`** (billable line items on a job), **`Note`** (diary entries).
- Objects relevant to siblings/growth: **`JobActivity`** (dated per-job time), **`formresponse`** (structured form submissions, scope `read_forms`), **`Material`** (catalog, scope `read_inventory`).
- Auth: `Authorization: Bearer <temporary OAuth token from the request>` for the UI/invocation path.
- Scopes used by this module: `read_jobs`, `read_job_materials`, `manage_job_materials`, `publish_job_notes`.

---

## 17. Non-negotiables (summary)

- Multi-tenant, `AccountUuid` on every table and query, resolved per-request from the JWT.
- Opt-in per job via `ProjectFlag`; do nothing to jobs without a flag.
- Our DB is the dated source of truth; `SourceJobMaterialUuid` unique = the close-out diff key.
- Materials → `JobMaterial` for native billing; never rebuild invoicing.
- HTTPS, iframe-embeddable, sdk.css/sdk.js for native styling.
- Verify all **⚠️ VERIFY** items against the docs before running.
