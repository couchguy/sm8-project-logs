using Microsoft.EntityFrameworkCore;
using ProjectLogs.Api.Handlers;

namespace ProjectLogs.Tests;

public class EnableProjectHandlerTests : IntegrationTestBase
{
    private readonly EnableProjectHandler _handler;

    public EnableProjectHandlerTests()
    {
        _handler = new EnableProjectHandler(Db, Sm8Client);
    }

    [Fact]
    public async Task Enable_NewJob_CreatesProjectFlag()
    {
        var ev = CreateEvent("enable_project");

        var result = await _handler.HandleAsync(ev);

        var (status, body) = await ExecuteResultAsync(result);
        Assert.Equal(200, status);
        Assert.Contains("Project tracking enabled", body);

        var flag = await Db.ProjectFlags.FirstOrDefaultAsync(
            f => f.AccountUuid == ev.Auth.AccountUUID && f.JobUuid == ev.EventArgs.JobUUID);
        Assert.NotNull(flag);
        Assert.True(flag.Enabled);
        Assert.Equal(ev.Auth.StaffUUID, flag.EnabledByStaffUuid);
    }

    [Fact]
    public async Task Enable_AlreadyEnabled_ReturnsAlreadyEnabledMessage()
    {
        var ev = CreateEvent("enable_project");

        // Enable once
        await _handler.HandleAsync(ev);

        // Enable again
        var result = await _handler.HandleAsync(ev);

        var (status, body) = await ExecuteResultAsync(result);
        Assert.Equal(200, status);
        Assert.Contains("already enabled", body);

        // Only one flag row
        var count = await Db.ProjectFlags.CountAsync(
            f => f.AccountUuid == ev.Auth.AccountUUID && f.JobUuid == ev.EventArgs.JobUUID);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Enable_PreviouslyDisabled_ReEnables()
    {
        var ev = CreateEvent("enable_project");

        // Enable then manually disable
        await _handler.HandleAsync(ev);
        var flag = await Db.ProjectFlags.FirstAsync(
            f => f.AccountUuid == ev.Auth.AccountUUID && f.JobUuid == ev.EventArgs.JobUUID);
        flag.Enabled = false;
        await Db.SaveChangesAsync();

        // Re-enable
        var result = await _handler.HandleAsync(ev);

        var (status, body) = await ExecuteResultAsync(result);
        Assert.Equal(200, status);
        Assert.Contains("Project tracking enabled", body);

        await Db.Entry(flag).ReloadAsync();
        Assert.True(flag.Enabled);
    }

    [Fact]
    public async Task Enable_MissingJobUuid_ReturnsBadRequest()
    {
        var ev = CreateEvent("enable_project");
        ev.EventArgs.JobUUID = null;

        var result = await _handler.HandleAsync(ev);

        var (status, _) = await ExecuteResultAsync(result);
        Assert.Equal(400, status);
    }
}
