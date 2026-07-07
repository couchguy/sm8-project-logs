using Microsoft.EntityFrameworkCore;
using NSubstitute;
using ProjectLogs.Api.Entities;
using ProjectLogs.Api.Handlers;
using ProjectLogs.Api.ServiceM8;

namespace ProjectLogs.Tests;

public class CloseOutHandlerTests : IntegrationTestBase
{
    private readonly CloseOutHandler _handler;

    public CloseOutHandlerTests()
    {
        _handler = new CloseOutHandler(Db, Sm8Client);
    }

    private async Task EnableProject(string accountUuid, string jobUuid, string staffUuid)
    {
        Db.ProjectFlags.Add(new ProjectFlag
        {
            AccountUuid = accountUuid,
            JobUuid = jobUuid,
            Enabled = true,
            EnabledAtUtc = DateTime.UtcNow,
            EnabledByStaffUuid = staffUuid
        });
        await Db.SaveChangesAsync();
    }

    [Fact]
    public async Task CloseOut_ClaimsAllNewMaterials()
    {
        var ev = CreateEvent("close_out", logDate: "2026-07-07");
        await EnableProject(ev.Auth.AccountUUID, ev.EventArgs.JobUUID!, ev.Auth.StaffUUID);

        var materials = CreateMaterials(
            ("Pipe 15mm", "3", "12.50"),
            ("Elbow", "6", "4.25"),
            ("Solder", "1", "8.00"));

        Sm8Client.GetJobMaterialsAsync(Arg.Any<string>(), ev.EventArgs.JobUUID!)
            .Returns(materials);

        var result = await _handler.HandleAsync(ev);

        var (status, body) = await ExecuteResultAsync(result);
        Assert.Equal(200, status);
        Assert.Contains("Closed out 2026-07-07", body);

        // Verify all 3 lines were saved
        var log = await Db.DailyLogs
            .Include(d => d.Lines)
            .FirstAsync(d => d.AccountUuid == ev.Auth.AccountUUID && d.JobUuid == ev.EventArgs.JobUUID);

        Assert.Equal(new DateOnly(2026, 7, 7), log.LogDate);
        Assert.Equal(3, log.Lines.Count);
        Assert.Contains(log.Lines, l => l.Name == "Pipe 15mm" && l.Quantity == 3 && l.UnitPrice == 12.50m);
        Assert.Contains(log.Lines, l => l.Name == "Elbow" && l.Quantity == 6 && l.UnitPrice == 4.25m);
        Assert.Contains(log.Lines, l => l.Name == "Solder" && l.Quantity == 1 && l.UnitPrice == 8.00m);
    }

    [Fact]
    public async Task CloseOut_SecondCloseOutOnlyClaimsNewMaterials()
    {
        var ev = CreateEvent("close_out", logDate: "2026-07-07");
        await EnableProject(ev.Auth.AccountUUID, ev.EventArgs.JobUUID!, ev.Auth.StaffUUID);

        // First batch: 2 materials
        var batch1 = CreateMaterials(("Pipe", "1", "10.00"), ("Elbow", "2", "5.00"));
        Sm8Client.GetJobMaterialsAsync(Arg.Any<string>(), ev.EventArgs.JobUUID!)
            .Returns(batch1);

        await _handler.HandleAsync(ev);

        // Second batch: original 2 + 1 new
        var batch2 = new List<Sm8JobMaterial>(batch1);
        var newMaterial = CreateMaterials(("Valve", "1", "25.00"));
        batch2.AddRange(newMaterial);

        Sm8Client.GetJobMaterialsAsync(Arg.Any<string>(), ev.EventArgs.JobUUID!)
            .Returns(batch2);

        var ev2 = CreateEvent("close_out",
            accountUuid: ev.Auth.AccountUUID,
            jobUuid: ev.EventArgs.JobUUID,
            logDate: "2026-07-08");

        var result = await _handler.HandleAsync(ev2);

        var (status, body) = await ExecuteResultAsync(result);
        Assert.Equal(200, status);
        Assert.Contains("Closed out 2026-07-08", body);

        // Should have 2 logs total
        var logs = await Db.DailyLogs
            .Include(d => d.Lines)
            .Where(d => d.AccountUuid == ev.Auth.AccountUUID && d.JobUuid == ev.EventArgs.JobUUID)
            .OrderBy(d => d.LogDate)
            .ToListAsync();

        Assert.Equal(2, logs.Count);
        Assert.Equal(2, logs[0].Lines.Count); // first close-out: 2 items
        Assert.Single(logs[1].Lines);          // second close-out: 1 new item
        Assert.Equal("Valve", logs[1].Lines.First().Name);
    }

    [Fact]
    public async Task CloseOut_NoUnclaimedMaterials_ReturnsNoNewMessage()
    {
        var ev = CreateEvent("close_out", logDate: "2026-07-07");
        await EnableProject(ev.Auth.AccountUUID, ev.EventArgs.JobUUID!, ev.Auth.StaffUUID);

        var materials = CreateMaterials(("Pipe", "1", "10.00"));
        Sm8Client.GetJobMaterialsAsync(Arg.Any<string>(), ev.EventArgs.JobUUID!)
            .Returns(materials);

        // First close-out claims everything
        await _handler.HandleAsync(ev);

        // Second close-out with same materials
        var ev2 = CreateEvent("close_out",
            accountUuid: ev.Auth.AccountUUID,
            jobUuid: ev.EventArgs.JobUUID,
            logDate: "2026-07-08");

        var result = await _handler.HandleAsync(ev2);

        var (status, body) = await ExecuteResultAsync(result);
        Assert.Equal(200, status);
        Assert.Contains("No new materials", body);

        // Still only 1 log
        var logCount = await Db.DailyLogs
            .CountAsync(d => d.AccountUuid == ev.Auth.AccountUUID && d.JobUuid == ev.EventArgs.JobUUID);
        Assert.Equal(1, logCount);
    }

    [Fact]
    public async Task CloseOut_CalculatesLineTotalCorrectly()
    {
        var ev = CreateEvent("close_out", logDate: "2026-07-07");
        await EnableProject(ev.Auth.AccountUUID, ev.EventArgs.JobUUID!, ev.Auth.StaffUUID);

        var materials = CreateMaterials(("Part A", "3", "15.50"));
        Sm8Client.GetJobMaterialsAsync(Arg.Any<string>(), ev.EventArgs.JobUUID!)
            .Returns(materials);

        await _handler.HandleAsync(ev);

        var line = await Db.DailyLogLines.FirstAsync();
        Assert.Equal(3m, line.Quantity);
        Assert.Equal(15.50m, line.UnitPrice);
        Assert.Equal(46.50m, line.LineTotal); // 3 * 15.50
    }

    [Fact]
    public async Task CloseOut_SummaryFormatIsCorrect()
    {
        var ev = CreateEvent("close_out", logDate: "2026-07-07");
        await EnableProject(ev.Auth.AccountUUID, ev.EventArgs.JobUUID!, ev.Auth.StaffUUID);

        var materials = CreateMaterials(
            ("A", "2", "10.00"),
            ("B", "1", "5.00"));
        Sm8Client.GetJobMaterialsAsync(Arg.Any<string>(), ev.EventArgs.JobUUID!)
            .Returns(materials);

        await _handler.HandleAsync(ev);

        var log = await Db.DailyLogs.FirstAsync(
            d => d.AccountUuid == ev.Auth.AccountUUID && d.JobUuid == ev.EventArgs.JobUUID);

        // 2 items, total = (2*10) + (1*5) = $25.00
        Assert.Equal("2 items, $25.00", log.Summary);
    }

    [Fact]
    public async Task CloseOut_SingleItem_SummarySaysItem()
    {
        var ev = CreateEvent("close_out", logDate: "2026-07-07");
        await EnableProject(ev.Auth.AccountUUID, ev.EventArgs.JobUUID!, ev.Auth.StaffUUID);

        var materials = CreateMaterials(("Solo Part", "1", "99.99"));
        Sm8Client.GetJobMaterialsAsync(Arg.Any<string>(), ev.EventArgs.JobUUID!)
            .Returns(materials);

        await _handler.HandleAsync(ev);

        var log = await Db.DailyLogs.FirstAsync();
        Assert.Equal("1 item, $99.99", log.Summary);
    }

    [Fact]
    public async Task CloseOut_PostsDiaryNote()
    {
        var ev = CreateEvent("close_out", logDate: "2026-07-07");
        await EnableProject(ev.Auth.AccountUUID, ev.EventArgs.JobUUID!, ev.Auth.StaffUUID);

        var materials = CreateMaterials(("Pipe", "1", "10.00"));
        Sm8Client.GetJobMaterialsAsync(Arg.Any<string>(), ev.EventArgs.JobUUID!)
            .Returns(materials);

        var noteUuid = Guid.NewGuid().ToString();
        Sm8Client.CreateNoteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(noteUuid);

        await _handler.HandleAsync(ev);

        // Verify note was created with correct content
        await Sm8Client.Received(1).CreateNoteAsync(
            "test-token",
            ev.EventArgs.JobUUID!,
            Arg.Is<string>(s => s.Contains("2026-07-07") && s.Contains("1 item")));

        // Verify UUID was stored
        var log = await Db.DailyLogs.FirstAsync();
        Assert.Equal(noteUuid, log.DiaryNoteUuid);
    }

    [Fact]
    public async Task CloseOut_IgnoresInactiveMaterials()
    {
        var ev = CreateEvent("close_out", logDate: "2026-07-07");
        await EnableProject(ev.Auth.AccountUUID, ev.EventArgs.JobUUID!, ev.Auth.StaffUUID);

        var materials = CreateMaterials(("Active Part", "1", "10.00"));
        materials.Add(new Sm8JobMaterial
        {
            Uuid = Guid.NewGuid().ToString(),
            Name = "Deleted Part",
            Quantity = "1", Price = "5.00",
            Active = 0 // inactive/deleted in SM8
        });

        Sm8Client.GetJobMaterialsAsync(Arg.Any<string>(), ev.EventArgs.JobUUID!)
            .Returns(materials);

        await _handler.HandleAsync(ev);

        var log = await Db.DailyLogs.Include(d => d.Lines).FirstAsync();
        Assert.Single(log.Lines);
        Assert.Equal("Active Part", log.Lines.First().Name);
    }

    [Fact]
    public async Task CloseOut_MultiTenant_AccountsAreIsolated()
    {
        // SM8 generates globally unique UUIDs, so different tenants
        // will always have different material UUIDs. This test verifies
        // that tenant A's claimed materials don't block tenant B's close-out.
        var jobUuid = Guid.NewGuid().ToString();
        var accountA = Guid.NewGuid().ToString();
        var accountB = Guid.NewGuid().ToString();

        await EnableProject(accountA, jobUuid, Guid.NewGuid().ToString());
        await EnableProject(accountB, jobUuid, Guid.NewGuid().ToString());

        // Account A has its own materials
        var materialsA = CreateMaterials(("Part from A", "1", "10.00"));
        Sm8Client.GetJobMaterialsAsync("test-token", jobUuid)
            .Returns(materialsA);

        var evA = CreateEvent("close_out", accountUuid: accountA, jobUuid: jobUuid, logDate: "2026-07-07");
        await _handler.HandleAsync(evA);

        // Account B has its own different materials (different UUIDs, as SM8 would generate)
        var materialsB = CreateMaterials(("Part from B", "2", "20.00"));
        Sm8Client.GetJobMaterialsAsync("test-token", jobUuid)
            .Returns(materialsB);

        var evB = CreateEvent("close_out", accountUuid: accountB, jobUuid: jobUuid, logDate: "2026-07-07");
        var result = await _handler.HandleAsync(evB);

        var (status, body) = await ExecuteResultAsync(result);
        Assert.Equal(200, status);
        Assert.Contains("Closed out", body);

        // Both accounts should have their own independent logs
        var logsA = await Db.DailyLogs.CountAsync(d => d.AccountUuid == accountA);
        var logsB = await Db.DailyLogs.CountAsync(d => d.AccountUuid == accountB);
        Assert.Equal(1, logsA);
        Assert.Equal(1, logsB);

        // Verify the correct materials landed in the correct tenant
        var lineA = await Db.DailyLogLines
            .FirstAsync(l => l.DailyLog.AccountUuid == accountA);
        Assert.Equal("Part from A", lineA.Name);

        var lineB = await Db.DailyLogLines
            .FirstAsync(l => l.DailyLog.AccountUuid == accountB);
        Assert.Equal("Part from B", lineB.Name);
    }

    [Fact]
    public async Task CloseOut_NotEnabled_ReturnsError()
    {
        var ev = CreateEvent("close_out", logDate: "2026-07-07");
        // Don't enable project tracking

        var result = await _handler.HandleAsync(ev);

        var (status, body) = await ExecuteResultAsync(result);
        Assert.Equal(200, status);
        Assert.Contains("not enabled", body);
    }

    [Fact]
    public async Task CloseOut_SnapshotsCostWhenAvailable()
    {
        var ev = CreateEvent("close_out", logDate: "2026-07-07");
        await EnableProject(ev.Auth.AccountUUID, ev.EventArgs.JobUUID!, ev.Auth.StaffUUID);

        var materials = new List<Sm8JobMaterial>
        {
            new()
            {
                Uuid = Guid.NewGuid().ToString(),
                Name = "With Cost",
                Quantity = "2", Price = "15.00", Cost = "8.00", Active = 1
            },
            new()
            {
                Uuid = Guid.NewGuid().ToString(),
                Name = "No Cost",
                Quantity = "1", Price = "10.00", Cost = null, Active = 1
            }
        };
        Sm8Client.GetJobMaterialsAsync(Arg.Any<string>(), ev.EventArgs.JobUUID!)
            .Returns(materials);

        await _handler.HandleAsync(ev);

        var lines = await Db.DailyLogLines.OrderBy(l => l.Name).ToListAsync();
        Assert.Equal("No Cost", lines[0].Name);
        Assert.Null(lines[0].UnitCost);
        Assert.Equal("With Cost", lines[1].Name);
        Assert.Equal(8.00m, lines[1].UnitCost);
    }
}
