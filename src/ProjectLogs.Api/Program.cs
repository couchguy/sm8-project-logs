using Microsoft.EntityFrameworkCore;
using ProjectLogs.Api.Data;
using ProjectLogs.Api.Handlers;
using ProjectLogs.Api.ServiceM8;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ProjectLogsDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("ProjectLogs")));

builder.Services.AddHttpClient<IServiceM8Client, ServiceM8Client>();

builder.Services.AddScoped<EnableProjectHandler>();
builder.Services.AddScoped<OpenDailyLogHandler>();
builder.Services.AddScoped<CloseOutHandler>();
builder.Services.AddScoped<WebhookHandler>();

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
    .WithName("HealthCheck");

app.MapPost("/sm8", async (
    HttpContext ctx,
    IConfiguration config,
    EnableProjectHandler enableHandler,
    OpenDailyLogHandler openLogHandler,
    CloseOutHandler closeOutHandler,
    WebhookHandler webhookHandler) =>
{
    // The JWT is the entire POST body
    using var reader = new StreamReader(ctx.Request.Body);
    var body = await reader.ReadToEndAsync();
    body = body.Trim();

    if (string.IsNullOrEmpty(body))
        return Results.BadRequest("Empty request body");

    var appSecret = config["ServiceM8:AppSecret"];
    if (string.IsNullOrEmpty(appSecret))
        return Results.StatusCode(500);

    var sm8Event = await JwtValidator.ValidateAndDecodeAsync(body, appSecret);
    if (sm8Event is null)
        return Results.Unauthorized();

    return sm8Event.EventName switch
    {
        "enable_project" => await enableHandler.HandleAsync(sm8Event),
        "open_daily_log" => await openLogHandler.HandleAsync(sm8Event),
        "close_out" => await closeOutHandler.HandleAsync(sm8Event),
        "webhook_subscription" => await webhookHandler.HandleAsync(sm8Event),
        _ => Results.BadRequest(new { error = $"Unknown event: {sm8Event.EventName}" })
    };
})
.WithName("ServiceM8Callback");

app.Run();
