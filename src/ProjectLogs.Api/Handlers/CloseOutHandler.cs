using Microsoft.EntityFrameworkCore;
using ProjectLogs.Api.Data;
using ProjectLogs.Api.Entities;
using ProjectLogs.Api.ServiceM8;
using ProjectLogs.Api.Views;

namespace ProjectLogs.Api.Handlers;

public class CloseOutHandler(ProjectLogsDbContext db, IServiceM8Client sm8Client)
{
    public async Task<IResult> HandleAsync(ServiceM8Event sm8Event)
    {
        var accountUuid = sm8Event.Auth.AccountUUID;
        var jobUuid = sm8Event.EventArgs.JobUUID;
        if (string.IsNullOrEmpty(jobUuid))
            return Results.BadRequest("Missing jobUUID");

        if (!DateOnly.TryParse(sm8Event.EventArgs.LogDate, out var logDate))
            logDate = DateOnly.FromDateTime(DateTime.UtcNow);

        var flag = await db.ProjectFlags
            .FirstOrDefaultAsync(f => f.AccountUuid == accountUuid && f.JobUuid == jobUuid && f.Enabled);
        if (flag is null)
            return Results.Content("<p>Project tracking is not enabled for this job.</p>", "text/html");

        var jobMaterials = await sm8Client.GetJobMaterialsAsync(sm8Event.Auth.AccessToken, jobUuid);

        var claimedIds = await db.DailyLogLines
            .Where(l => l.DailyLog.AccountUuid == accountUuid && l.DailyLog.JobUuid == jobUuid)
            .Select(l => l.SourceJobMaterialUuid)
            .ToListAsync();
        var claimed = claimedIds.ToHashSet();

        var newMaterials = jobMaterials
            .Where(m => !claimed.Contains(m.Uuid) && m.Active == 1)
            .ToList();

        var job = await sm8Client.GetJobAsync(sm8Event.Auth.AccessToken, jobUuid);
        var jobLabel = job?.GeneratedJobId ?? jobUuid[..8];

        if (newMaterials.Count == 0)
        {
            var existingLogs = await GetPastLogsAsync(accountUuid, jobUuid);
            return Results.Content(
                PopupRenderer.RenderDailyLogContent(jobLabel, [], existingLogs,
                    "No new materials to close out."),
                "text/html");
        }

        var now = DateTime.UtcNow;
        var log = new DailyLog
        {
            AccountUuid = accountUuid,
            JobUuid = jobUuid,
            JobNumber = job?.GeneratedJobId,
            LogDate = logDate,
            StaffUuid = sm8Event.Auth.StaffUUID,
            ClosedAtUtc = now,
            Lines = newMaterials.Select(m => new DailyLogLine
            {
                SourceJobMaterialUuid = m.Uuid,
                MaterialUuid = m.MaterialUuid,
                Name = m.Name ?? "(unnamed)",
                Quantity = ParseDecimal(m.Quantity),
                UnitPrice = ParseDecimal(m.Price),
                UnitCost = string.IsNullOrEmpty(m.Cost) ? null : ParseDecimal(m.Cost),
                LineTotal = ParseDecimal(m.Quantity) * ParseDecimal(m.Price),
                SnapshotAtUtc = now
            }).ToList()
        };

        var totalItems = log.Lines.Count;
        var totalValue = log.Lines.Sum(l => l.LineTotal);
        log.Summary = $"{totalItems} item{(totalItems != 1 ? "s" : "")}, ${totalValue:F2}";

        var noteText = $"Daily Log \u2014 {logDate:yyyy-MM-dd}: {log.Summary}";
        var noteUuid = await sm8Client.CreateNoteAsync(sm8Event.Auth.AccessToken, jobUuid, noteText);
        log.DiaryNoteUuid = noteUuid;

        db.DailyLogs.Add(log);
        await db.SaveChangesAsync();

        var pastLogs = await GetPastLogsAsync(accountUuid, jobUuid);

        var allClaimed = claimed.Union(newMaterials.Select(m => m.Uuid)).ToHashSet();
        var remainingUnclaimed = jobMaterials
            .Where(m => !allClaimed.Contains(m.Uuid) && m.Active == 1)
            .ToList();

        return Results.Content(
            PopupRenderer.RenderDailyLogContent(jobLabel, remainingUnclaimed, pastLogs,
                $"Closed out {logDate:yyyy-MM-dd}: {log.Summary}"),
            "text/html");
    }

    private async Task<List<DailyLog>> GetPastLogsAsync(string accountUuid, string jobUuid)
    {
        return await db.DailyLogs
            .Include(d => d.Lines)
            .Where(d => d.AccountUuid == accountUuid && d.JobUuid == jobUuid)
            .OrderByDescending(d => d.LogDate)
            .ToListAsync();
    }

    private static decimal ParseDecimal(string? value)
        => decimal.TryParse(value, out var result) ? result : 0;
}
