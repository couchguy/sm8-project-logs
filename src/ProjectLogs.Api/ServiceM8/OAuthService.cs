using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ProjectLogs.Api.Data;
using ProjectLogs.Api.Entities;

namespace ProjectLogs.Api.ServiceM8;

public class OAuthService(
    IHttpClientFactory httpFactory,
    IConfiguration config,
    ILogger<OAuthService> logger)
{
    private const string AuthorizeUrl = "https://go.servicem8.com/oauth/authorize";
    private const string TokenUrl = "https://go.servicem8.com/oauth/access_token";
    private const string Scopes = "read_jobs read_job_materials manage_job_materials publish_job_notes";

    private string AppId => config["ServiceM8:AppId"]
        ?? throw new InvalidOperationException("ServiceM8:AppId not configured");
    private string AppSecret => config["ServiceM8:AppSecret"]
        ?? throw new InvalidOperationException("ServiceM8:AppSecret not configured");
    private string RedirectUri => $"{config["ServiceM8:CallbackBaseUrl"]}/sm8/oauth/callback";

    public string BuildAuthorizeUrl()
    {
        return $"{AuthorizeUrl}" +
               $"?response_type=code" +
               $"&client_id={Uri.EscapeDataString(AppId)}" +
               $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}" +
               $"&scope={Uri.EscapeDataString(Scopes)}";
    }

    public async Task<OAuthTokenResponse?> ExchangeCodeAsync(string code)
    {
        using var http = httpFactory.CreateClient();
        var response = await http.PostAsync(TokenUrl, new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["client_id"] = AppId,
                ["client_secret"] = AppSecret,
                ["code"] = code,
                ["redirect_uri"] = RedirectUri
            }));

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            logger.LogError("OAuth token exchange failed: {Status} {Body}",
                response.StatusCode, body);
            return null;
        }

        return await response.Content.ReadFromJsonAsync<OAuthTokenResponse>();
    }

    public async Task<OAuthTokenResponse?> RefreshTokenAsync(string refreshToken)
    {
        using var http = httpFactory.CreateClient();
        var response = await http.PostAsync(TokenUrl, new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["client_id"] = AppId,
                ["client_secret"] = AppSecret,
                ["refresh_token"] = refreshToken
            }));

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("OAuth token refresh failed: {Status}", response.StatusCode);
            return null;
        }

        // SM8 returns a new refresh token each time — caller must store it
        return await response.Content.ReadFromJsonAsync<OAuthTokenResponse>();
    }

    /// <summary>
    /// Tries to resolve the SM8 account UUID from the access token.
    /// Attempts JWT decode first, then falls back to an API call.
    /// </summary>
    public async Task<string?> ResolveAccountUuidAsync(string accessToken)
    {
        // Attempt 1: decode if the access token is a JWT
        var parts = accessToken.Split('.');
        if (parts.Length == 3)
        {
            try
            {
                var payload = Base64UrlEncoder.Decode(parts[1]);
                using var doc = JsonDocument.Parse(payload);
                if (doc.RootElement.TryGetProperty("accountUUID", out var prop))
                    return prop.GetString();
                if (doc.RootElement.TryGetProperty("account_uuid", out prop))
                    return prop.GetString();
            }
            catch
            {
                // Not a valid JWT — try API fallback
            }
        }

        // Attempt 2: call the SM8 API — any successful call confirms the token works.
        // The account UUID may be in the response if SM8 includes it.
        try
        {
            using var http = httpFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Get,
                "https://api.servicem8.com/api_1.0/job.json?%24top=1");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await http.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation("OAuth token validated via API call");
                // API calls are scoped to the account but don't return the UUID.
                // The account UUID will be resolved on the first JWT invocation.
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to validate token via API call");
        }

        return null;
    }

    /// <summary>
    /// Stores or updates a tenant registration after successful OAuth.
    /// </summary>
    public async Task UpsertRegistrationAsync(
        ProjectLogsDbContext db,
        string accountUuid,
        OAuthTokenResponse tokens)
    {
        var existing = await db.TenantRegistrations
            .FirstOrDefaultAsync(t => t.AccountUuid == accountUuid);

        if (existing is not null)
        {
            existing.AccessToken = tokens.AccessToken;
            existing.RefreshToken = tokens.RefreshToken;
            existing.TokenExpiresAtUtc = DateTime.UtcNow.AddSeconds(tokens.ExpiresIn);
            existing.Active = true;
            existing.UninstalledAtUtc = null;
        }
        else
        {
            db.TenantRegistrations.Add(new TenantRegistration
            {
                AccountUuid = accountUuid,
                AccessToken = tokens.AccessToken,
                RefreshToken = tokens.RefreshToken,
                TokenExpiresAtUtc = DateTime.UtcNow.AddSeconds(tokens.ExpiresIn),
                Active = true,
                InstalledAtUtc = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync();
    }
}

public class OAuthTokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = "";

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = "";

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("scope")]
    public string? Scope { get; set; }
}
