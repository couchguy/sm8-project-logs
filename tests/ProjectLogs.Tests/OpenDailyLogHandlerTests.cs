using ProjectLogs.Api.Entities;
using ProjectLogs.Api.Handlers;
using NSubstitute;

namespace ProjectLogs.Tests;

public class OpenDailyLogHandlerTests : IntegrationTestBase
{
    private readonly OpenDailyLogHandler _handler;

    public OpenDailyLogHandlerTests()
    {
        _handler = new OpenDailyLogHandler(Db, Sm8Client);
    }

    [Fact]
    public async Task Open_NotEnabled_ReturnsNotEnabledMessage()
    {
        var ev = CreateEvent("open_daily_log");

        var result = await _handler.HandleAsync(ev);

        var (status, body) = await ExecuteResultAsync(result);
        Assert.Equal(200, status);
        Assert.Contains("not enabled", body);
        Assert.Contains("Enable Project Tracking", body);
    }

    [Fact]
    public async Task Open_EnabledNoMaterials_RendersEmptyState()
    {
        var ev = CreateEvent("open_daily_log");
        Db.ProjectFlags.Add(new ProjectFlag
        {
            AccountUuid = ev.Auth.AccountUUID,
            JobUuid = ev.EventArgs.JobUUID!,
            Enabled = true,
            EnabledAtUtc = DateTime.UtcNow,
            EnabledByStaffUuid = ev.Auth.StaffUUID
        });
        await Db.SaveChangesAsync();

        Sm8Client.GetJobMaterialsAsync(Arg.Any<string>(), ev.EventArgs.JobUUID!)
            .Returns([]);

        var result = await _handler.HandleAsync(ev);

        var (status, body) = await ExecuteResultAsync(result);
        Assert.Equal(200, status);
        Assert.Contains("Daily Log", body);
        Assert.Contains("No new materials", body);
        Assert.Contains("No logs recorded", body);
    }

    [Fact]
    public async Task Open_WithUnclaimedMaterials_RendersMaterialsTable()
    {
        var ev = CreateEvent("open_daily_log");
        Db.ProjectFlags.Add(new ProjectFlag
        {
            AccountUuid = ev.Auth.AccountUUID,
            JobUuid = ev.EventArgs.JobUUID!,
            Enabled = true,
            EnabledAtUtc = DateTime.UtcNow,
            EnabledByStaffUuid = ev.Auth.StaffUUID
        });
        await Db.SaveChangesAsync();

        var materials = CreateMaterials(
            ("Copper Pipe 15mm", "3", "12.50"),
            ("Elbow Joint", "6", "4.25"));

        Sm8Client.GetJobMaterialsAsync(Arg.Any<string>(), ev.EventArgs.JobUUID!)
            .Returns(materials);

        var result = await _handler.HandleAsync(ev);

        var (status, body) = await ExecuteResultAsync(result);
        Assert.Equal(200, status);
        Assert.Contains("Copper Pipe 15mm", body);
        Assert.Contains("Elbow Joint", body);
        Assert.Contains("Close out today", body);
    }

    [Fact]
    public async Task Open_WithClaimedAndUnclaimed_ShowsOnlyUnclaimed()
    {
        var ev = CreateEvent("open_daily_log");
        Db.ProjectFlags.Add(new ProjectFlag
        {
            AccountUuid = ev.Auth.AccountUUID,
            JobUuid = ev.EventArgs.JobUUID!,
            Enabled = true,
            EnabledAtUtc = DateTime.UtcNow,
            EnabledByStaffUuid = ev.Auth.StaffUUID
        });

        // Pre-claim one material via a past log
        var claimedMaterialUuid = Guid.NewGuid().ToString();
        Db.DailyLogs.Add(new DailyLog
        {
            AccountUuid = ev.Auth.AccountUUID,
            JobUuid = ev.EventArgs.JobUUID!,
            LogDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            StaffUuid = ev.Auth.StaffUUID,
            ClosedAtUtc = DateTime.UtcNow.AddDays(-1),
            Summary = "1 item, $10.00",
            Lines =
            [
                new DailyLogLine
                {
                    SourceJobMaterialUuid = claimedMaterialUuid,
                    Name = "Already Claimed",
                    Quantity = 1, UnitPrice = 10, LineTotal = 10,
                    SnapshotAtUtc = DateTime.UtcNow.AddDays(-1)
                }
            ]
        });
        await Db.SaveChangesAsync();

        var materials = CreateMaterials(("New Unclaimed Part", "2", "5.00"));
        // Add the claimed material to the SM8 response too
        materials.Add(new Api.ServiceM8.Sm8JobMaterial
        {
            Uuid = claimedMaterialUuid,
            Name = "Already Claimed",
            Quantity = "1", Price = "10.00", Active = 1
        });

        Sm8Client.GetJobMaterialsAsync(Arg.Any<string>(), ev.EventArgs.JobUUID!)
            .Returns(materials);

        var result = await _handler.HandleAsync(ev);

        var (status, body) = await ExecuteResultAsync(result);
        Assert.Equal(200, status);
        Assert.Contains("New Unclaimed Part", body);
        // The claimed material shouldn't be in the unclaimed table,
        // but it shows up in log history
        Assert.Contains("Already Claimed", body); // in history section
    }
}
