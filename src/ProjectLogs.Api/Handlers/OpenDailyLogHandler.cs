using Microsoft.EntityFrameworkCore;
using ProjectLogs.Api.Data;
using ProjectLogs.Api.ServiceM8;
using ProjectLogs.Api.Views;

namespace ProjectLogs.Api.Handlers;

public class OpenDailyLogHandler(ProjectLogsDbContext db, IServiceM8Client sm8Client)
{
    public async Task<IResult> HandleAsync(ServiceM8Event sm8Event)
    {
        var accountUuid = sm8Event.Auth.AccountUUID;
        var jobUuid = sm8Event.EventArgs.JobUUID;
        if (string.IsNullOrEmpty(jobUuid))
            return Results.BadRequest("Missing jobUUID");

        var flag = await db.ProjectFlags
            .FirstOrDefaultAsync(f => f.AccountUuid == accountUuid && f.JobUuid == jobUuid && f.Enabled);

        if (flag is null)
        {
            return Results.Content(
                PopupRenderer.RenderFullPage("Daily Log",
                    "<p>Project tracking is not enabled for this job.</p>" +
                    "<p>Use the <strong>Enable Project Tracking</strong> action first.</p>"),
                "text/html");
        }

        var job = await sm8Client.GetJobAsync(sm8Event.Auth.AccessToken, jobUuid);
        var jobLabel = job?.GeneratedJobId ?? jobUuid[..8];

        var jobMaterials = await sm8Client.GetJobMaterialsAsync(sm8Event.Auth.AccessToken, jobUuid);

        var claimedIds = await db.DailyLogLines
            .Where(l => l.DailyLog.AccountUuid == accountUuid && l.DailyLog.JobUuid == jobUuid)
            .Select(l => l.SourceJobMaterialUuid)
            .ToListAsync();
        var claimed = claimedIds.ToHashSet();

        var unclaimed = jobMaterials
            .Where(m => !claimed.Contains(m.Uuid) && m.Active == 1)
            .ToList();

        var pastLogs = await db.DailyLogs
            .Include(d => d.Lines)
            .Where(d => d.AccountUuid == accountUuid && d.JobUuid == jobUuid)
            .OrderByDescending(d => d.LogDate)
            .ToListAsync();

        return Results.Content(
            PopupRenderer.RenderDailyLogPage(jobLabel, unclaimed, pastLogs),
            "text/html");
    }
}
