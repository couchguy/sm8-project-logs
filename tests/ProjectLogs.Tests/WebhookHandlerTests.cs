using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ProjectLogs.Api.Entities;
using ProjectLogs.Api.Handlers;
using ProjectLogs.Api.ServiceM8;

namespace ProjectLogs.Tests;

public class WebhookHandlerTests : IntegrationTestBase
{
    private readonly WebhookHandler _handler;

    public WebhookHandlerTests()
    {
        _handler = new WebhookHandler(Db, Sm8Client,
            NullLoggerFactory.Instance.CreateLogger<WebhookHandler>());
    }

    [Fact]
    public async Task Webhook_TrackedJobCompleted_ReturnsOk()
    {
        var accountUuid = Guid.NewGuid().ToString();
        var jobUuid = Guid.NewGuid().ToString();

        Db.ProjectFlags.Add(new ProjectFlag
        {
            AccountUuid = accountUuid,
            JobUuid = jobUuid,
            Enabled = true,
            EnabledAtUtc = DateTime.UtcNow,
            EnabledByStaffUuid = Guid.NewGuid().ToString()
        });
        await Db.SaveChangesAsync();

        Sm8Client.GetJobAsync(Arg.Any<string>(), jobUuid)
            .Returns(new Sm8Job { Uuid = jobUuid, Status = "Completed" });

        var ev = new ServiceM8Event
        {
            EventName = "webhook_subscription",
            Auth = new ServiceM8Auth
            {
                AccountUUID = accountUuid,
                AccessToken = "test-token",
                StaffUUID = Guid.NewGuid().ToString()
            },
            EventArgs = new ServiceM8EventArgs
            {
                Object = "job",
                Entry =
                [
                    new WebhookEntry
                    {
                        Uuid = jobUuid,
                        ChangedFields = ["status"],
                        Time = "2026-07-07 12:00:00"
                    }
                ]
            }
        };

        var result = await _handler.HandleAsync(ev);

        var (status, _) = await ExecuteResultAsync(result);
        Assert.Equal(200, status);

        // Verified it fetched the job to check status
        await Sm8Client.Received(1).GetJobAsync("test-token", jobUuid);
    }

    [Fact]
    public async Task Webhook_UntrackedJob_DoesNotFetchJob()
    {
        var ev = new ServiceM8Event
        {
            EventName = "webhook_subscription",
            Auth = new ServiceM8Auth
            {
                AccountUUID = Guid.NewGuid().ToString(),
                AccessToken = "test-token",
                StaffUUID = Guid.NewGuid().ToString()
            },
            EventArgs = new ServiceM8EventArgs
            {
                Object = "job",
                Entry =
                [
                    new WebhookEntry
                    {
                        Uuid = Guid.NewGuid().ToString(),
                        ChangedFields = ["status"],
                        Time = "2026-07-07 12:00:00"
                    }
                ]
            }
        };

        var result = await _handler.HandleAsync(ev);

        var (status, _) = await ExecuteResultAsync(result);
        Assert.Equal(200, status);

        // Should NOT have called GetJobAsync since the job isn't tracked
        await Sm8Client.DidNotReceive().GetJobAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Webhook_NonStatusChange_SkipsProcessing()
    {
        var accountUuid = Guid.NewGuid().ToString();
        var jobUuid = Guid.NewGuid().ToString();

        Db.ProjectFlags.Add(new ProjectFlag
        {
            AccountUuid = accountUuid,
            JobUuid = jobUuid,
            Enabled = true,
            EnabledAtUtc = DateTime.UtcNow,
            EnabledByStaffUuid = Guid.NewGuid().ToString()
        });
        await Db.SaveChangesAsync();

        var ev = new ServiceM8Event
        {
            EventName = "webhook_subscription",
            Auth = new ServiceM8Auth
            {
                AccountUUID = accountUuid,
                AccessToken = "test-token",
                StaffUUID = Guid.NewGuid().ToString()
            },
            EventArgs = new ServiceM8EventArgs
            {
                Object = "job",
                Entry =
                [
                    new WebhookEntry
                    {
                        Uuid = jobUuid,
                        ChangedFields = ["badges", "job_description"], // not status
                        Time = "2026-07-07 12:00:00"
                    }
                ]
            }
        };

        var result = await _handler.HandleAsync(ev);

        var (status, _) = await ExecuteResultAsync(result);
        Assert.Equal(200, status);

        // Should NOT have called GetJobAsync since status didn't change
        await Sm8Client.DidNotReceive().GetJobAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Webhook_EmptyEntry_ReturnsOk()
    {
        var ev = new ServiceM8Event
        {
            EventName = "webhook_subscription",
            Auth = new ServiceM8Auth
            {
                AccountUUID = Guid.NewGuid().ToString(),
                AccessToken = "test-token",
                StaffUUID = Guid.NewGuid().ToString()
            },
            EventArgs = new ServiceM8EventArgs
            {
                Object = "job",
                Entry = []
            }
        };

        var result = await _handler.HandleAsync(ev);

        var (status, _) = await ExecuteResultAsync(result);
        Assert.Equal(200, status);
    }
}
