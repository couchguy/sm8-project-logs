namespace ProjectLogs.Api.Entities;

public class TenantRegistration
{
    public int Id { get; set; }
    public required string AccountUuid { get; set; }
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? TokenExpiresAtUtc { get; set; }
    public bool Active { get; set; }
    public DateTime InstalledAtUtc { get; set; }
    public DateTime? UninstalledAtUtc { get; set; }
}
