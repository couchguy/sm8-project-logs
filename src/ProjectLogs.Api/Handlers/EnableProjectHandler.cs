using Microsoft.EntityFrameworkCore;
using ProjectLogs.Api.Data;
using ProjectLogs.Api.Entities;
using ProjectLogs.Api.ServiceM8;
using ProjectLogs.Api.Views;

namespace ProjectLogs.Api.Handlers;

public class EnableProjectHandler(ProjectLogsDbContext db, IServiceM8Client sm8Client)
{
    public async Task<IResult> HandleAsync(ServiceM8Event sm8Event)
    {
        var accountUuid = sm8Event.Auth.AccountUUID;
        var jobUuid = sm8Event.EventArgs.JobUUID;
        if (string.IsNullOrEmpty(jobUuid))
            return Results.BadRequest("Missing jobUUID");

        var existing = await db.ProjectFlags
            .FirstOrDefaultAsync(f => f.AccountUuid == accountUuid && f.JobUuid == jobUuid);

        if (existing is not null)
        {
            if (existing.Enabled)
            {
                return Results.Content(
                    PopupRenderer.RenderConfirmation("Project tracking is already enabled for this job."),
                    "text/html");
            }

            existing.Enabled = true;
            existing.EnabledAtUtc = DateTime.UtcNow;
            existing.EnabledByStaffUuid = sm8Event.Auth.StaffUUID;
        }
        else
        {
            db.ProjectFlags.Add(new ProjectFlag
            {
                AccountUuid = accountUuid,
                JobUuid = jobUuid,
                Enabled = true,
                EnabledAtUtc = DateTime.UtcNow,
                EnabledByStaffUuid = sm8Event.Auth.StaffUUID
            });
        }

        await db.SaveChangesAsync();

        var job = await sm8Client.GetJobAsync(sm8Event.Auth.AccessToken, jobUuid);
        var jobLabel = job?.GeneratedJobId ?? jobUuid[..8];

        return Results.Content(
            PopupRenderer.RenderConfirmation(
                $"Project tracking enabled for Job #{System.Net.WebUtility.HtmlEncode(jobLabel)}."),
            "text/html");
    }
}
