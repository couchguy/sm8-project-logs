using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ProjectLogs.Api.Data;
using ProjectLogs.Api.ServiceM8;

namespace ProjectLogs.Tests;

public abstract class IntegrationTestBase : IDisposable
{
    private readonly string _dbName;

    protected ProjectLogsDbContext Db { get; }
    protected IServiceM8Client Sm8Client { get; }

    protected IntegrationTestBase()
    {
        // Each test class gets its own database for full isolation
        _dbName = $"ProjectLogsTest_{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<ProjectLogsDbContext>()
            .UseSqlServer($"Server=(localdb)\\MSSQLLocalDB;Database={_dbName};Trusted_Connection=true;TrustServerCertificate=true")
            .Options;

        Db = new ProjectLogsDbContext(options);
        Db.Database.EnsureCreated();

        Sm8Client = Substitute.For<IServiceM8Client>();

        // Default: GetJobAsync returns a job with a generated ID
        Sm8Client.GetJobAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(ci => new Sm8Job
            {
                Uuid = ci.ArgAt<string>(1),
                Status = "Work Order",
                GeneratedJobId = "TEST-001"
            });

        // Default: CreateNoteAsync returns a fake UUID
        Sm8Client.CreateNoteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(Guid.NewGuid().ToString());
    }

    protected static ServiceM8Event CreateEvent(
        string eventName,
        string? accountUuid = null,
        string? jobUuid = null,
        string? staffUuid = null,
        string? logDate = null)
    {
        return new ServiceM8Event
        {
            EventVersion = "1.0",
            EventName = eventName,
            Auth = new ServiceM8Auth
            {
                AccountUUID = accountUuid ?? Guid.NewGuid().ToString(),
                StaffUUID = staffUuid ?? Guid.NewGuid().ToString(),
                AccessToken = "test-token",
                AccessTokenExpiry = 900
            },
            EventArgs = new ServiceM8EventArgs
            {
                JobUUID = jobUuid ?? Guid.NewGuid().ToString(),
                LogDate = logDate
            }
        };
    }

    protected static List<Sm8JobMaterial> CreateMaterials(params (string name, string qty, string price)[] items)
    {
        return items.Select(i => new Sm8JobMaterial
        {
            Uuid = Guid.NewGuid().ToString(),
            Name = i.name,
            Quantity = i.qty,
            Price = i.price,
            Active = 1
        }).ToList();
    }

    protected ILogger<T> CreateLogger<T>() => NullLoggerFactory.Instance.CreateLogger<T>();

    /// <summary>
    /// Executes an IResult against a DefaultHttpContext and returns the status code and body.
    /// </summary>
    protected static async Task<(int StatusCode, string Body)> ExecuteResultAsync(IResult result)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var sp = services.BuildServiceProvider();

        var ctx = new DefaultHttpContext { RequestServices = sp };
        ctx.Response.Body = new MemoryStream();
        await result.ExecuteAsync(ctx);
        ctx.Response.Body.Position = 0;
        var body = await new StreamReader(ctx.Response.Body).ReadToEndAsync();
        return (ctx.Response.StatusCode, body);
    }

    public void Dispose()
    {
        Db.Database.EnsureDeleted();
        Db.Dispose();
        GC.SuppressFinalize(this);
    }
}
