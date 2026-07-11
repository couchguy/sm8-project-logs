using Microsoft.EntityFrameworkCore;
using ProjectLogs.Api.Data;
using ProjectLogs.Api.Handlers;
using ProjectLogs.Api.ServiceM8;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ProjectLogsDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("ProjectLogs")));

builder.Services.AddHttpClient<IServiceM8Client, ServiceM8Client>();
builder.Services.AddHttpClient();

builder.Services.AddScoped<OAuthService>();
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

// --- Health ---

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
    .WithName("HealthCheck");

// --- OAuth activation flow ---

app.MapGet("/sm8/activate", (OAuthService oauth) =>
{
    return Results.Redirect(oauth.BuildAuthorizeUrl());
})
.WithName("ServiceM8Activate")
.ExcludeFromDescription();

app.MapGet("/sm8/oauth/callback", async (
    string code,
    OAuthService oauth,
    ProjectLogsDbContext db,
    ILogger<Program> logger) =>
{
    var tokens = await oauth.ExchangeCodeAsync(code);
    if (tokens is null)
        return Results.Content(
            "<html><body><h1>Activation failed</h1><p>Could not complete authorization. Please try again.</p></body></html>",
            "text/html");

    // Try to resolve the SM8 account UUID from the token
    var accountUuid = await oauth.ResolveAccountUuidAsync(tokens.AccessToken);
    if (string.IsNullOrEmpty(accountUuid))
    {
        // Store with a temporary ID — will be reconciled on first JWT invocation.
        // SM8 UUIDs for different accounts never collide, so this is safe.
        accountUuid = $"pending-{Guid.NewGuid():N}";
        logger.LogWarning(
            "Could not resolve account UUID during activation. Stored as {TempId}. " +
            "Will reconcile on first JWT invocation.", accountUuid);
    }

    await oauth.UpsertRegistrationAsync(db, accountUuid, tokens);

    logger.LogInformation("Add-on activated for account {AccountUuid}", accountUuid);

    return Results.Content("""
        <!DOCTYPE html>
        <html>
        <head><meta charset="utf-8"><title>Project Logs</title></head>
        <body>
            <h1>Project Logs activated!</h1>
            <p>You can close this window and return to ServiceM8.</p>
            <p>Open any job card and tap <strong>Enable Project Tracking</strong> to get started.</p>
        </body>
        </html>
        """, "text/html");
})
.WithName("ServiceM8OAuthCallback")
.ExcludeFromDescription();

// --- SM8 callback (actions + webhooks) ---

app.MapPost("/sm8", async (
    HttpContext ctx,
    IConfiguration config,
    EnableProjectHandler enableHandler,
    OpenDailyLogHandler openLogHandler,
    CloseOutHandler closeOutHandler,
    WebhookHandler webhookHandler,
    ILogger<Program> logger) =>
{
    using var reader = new StreamReader(ctx.Request.Body);
    var body = await reader.ReadToEndAsync();
    body = body.Trim();

    if (string.IsNullOrEmpty(body))
        return Results.BadRequest("Empty request body");

    // Handle webhook verification challenge (plain POST, not JWT)
    if (!body.Contains('.'))
    {
        if (body.Contains("challenge"))
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("challenge", out var challenge))
                    return Results.Text(challenge.GetString() ?? "");
            }
            catch
            {
                // Not JSON — try form-encoded
                var pairs = body.Split('&')
                    .Select(p => p.Split('=', 2))
                    .Where(p => p.Length == 2)
                    .ToDictionary(p => p[0], p => Uri.UnescapeDataString(p[1]));
                if (pairs.TryGetValue("challenge", out var ch))
                    return Results.Text(ch);
            }
        }
        return Results.BadRequest("Invalid request");
    }

    var appSecret = config["ServiceM8:AppSecret"];
    if (string.IsNullOrEmpty(appSecret))
    {
        logger.LogError("ServiceM8:AppSecret is not configured");
        return Results.StatusCode(500);
    }

    var sm8Event = await JwtValidator.ValidateAndDecodeAsync(body, appSecret);
    if (sm8Event is null)
        return Results.Unauthorized();

    try
    {
        return sm8Event.EventName switch
        {
            "enable_project" => await enableHandler.HandleAsync(sm8Event),
            "open_daily_log" => await openLogHandler.HandleAsync(sm8Event),
            "close_out" => await closeOutHandler.HandleAsync(sm8Event),
            "webhook_subscription" => await webhookHandler.HandleAsync(sm8Event),
            _ => Results.BadRequest(new { error = $"Unknown event: {sm8Event.EventName}" })
        };
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error handling SM8 event {EventName} for account {AccountUuid}",
            sm8Event.EventName, sm8Event.Auth.AccountUUID);
        return Results.Content(
            "<p>An error occurred. Please try again or contact support.</p>",
            "text/html");
    }
})
.WithName("ServiceM8Callback");

app.Run();
