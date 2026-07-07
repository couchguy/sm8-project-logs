using System.Text.Json.Serialization;

namespace ProjectLogs.Api.ServiceM8;

public class ServiceM8Event
{
    [JsonPropertyName("eventVersion")]
    public string EventVersion { get; set; } = "";

    [JsonPropertyName("eventName")]
    public string EventName { get; set; } = "";

    [JsonPropertyName("auth")]
    public ServiceM8Auth Auth { get; set; } = new();

    [JsonPropertyName("eventArgs")]
    public ServiceM8EventArgs EventArgs { get; set; } = new();
}

public class ServiceM8Auth
{
    [JsonPropertyName("accountUUID")]
    public string AccountUUID { get; set; } = "";

    [JsonPropertyName("staffUUID")]
    public string StaffUUID { get; set; } = "";

    [JsonPropertyName("accessToken")]
    public string AccessToken { get; set; } = "";

    [JsonPropertyName("accessTokenExpiry")]
    public int AccessTokenExpiry { get; set; }
}

public class ServiceM8EventArgs
{
    [JsonPropertyName("jobUUID")]
    public string? JobUUID { get; set; }

    [JsonPropertyName("companyUUID")]
    public string? CompanyUUID { get; set; }

    // Webhook fields (snake_case from SM8)
    [JsonPropertyName("object")]
    public string? Object { get; set; }

    [JsonPropertyName("entry")]
    public List<WebhookEntry>? Entry { get; set; }

    [JsonPropertyName("resource_url")]
    public string? ResourceUrl { get; set; }

    // Custom invoke args from client.invoke()
    [JsonPropertyName("logDate")]
    public string? LogDate { get; set; }
}

public class WebhookEntry
{
    [JsonPropertyName("uuid")]
    public string Uuid { get; set; } = "";

    [JsonPropertyName("changed_fields")]
    public List<string> ChangedFields { get; set; } = [];

    [JsonPropertyName("time")]
    public string Time { get; set; } = "";
}
