using Microsoft.EntityFrameworkCore;
using ProjectLogs.Api.Data;
using ProjectLogs.Api.ServiceM8;

namespace ProjectLogs.Api.Handlers;

public class WebhookHandler(
    ProjectLogsDbContext db,
    IServiceM8Client sm8Client,
    ILogger<WebhookHandler> logger)
{
    public async Task<IResult> HandleAsync(ServiceM8Event sm8Event)
    {
        if (sm8Event.EventArgs.Entry is not { Count: > 0 })
            return Results.Ok();

        var accountUuid = sm8Event.Auth.AccountUUID;

        foreach (var entry in sm8Event.EventArgs.Entry)
        {
            if (!entry.ChangedFields.Contains("status"))
                continue;

            var isOurs = await db.ProjectFlags
                .AnyAsync(f => f.AccountUuid == accountUuid
                            && f.JobUuid == entry.Uuid
                            && f.Enabled);
            if (!isOurs)
                continue;

            var job = await sm8Client.GetJobAsync(sm8Event.Auth.AccessToken, entry.Uuid);
            if (job is null)
                continue;

            if (job.Status is "Completed" or "Unsuccessful")
            {
                logger.LogInformation(
                    "Tracked job {JobUuid} for account {AccountUuid} moved to {Status}",
                    entry.Uuid, accountUuid, job.Status);

                // Future: auto close-out remaining unclaimed materials
                // Future: lock the project from further logging
            }
        }

        return Results.Ok();
    }
}
