namespace ProjectLogs.Api.Entities;

public class ProjectFlag
{
    public int Id { get; set; }
    public required string AccountUuid { get; set; }
    public required string JobUuid { get; set; }
    public bool Enabled { get; set; }
    public DateTime EnabledAtUtc { get; set; }
    public required string EnabledByStaffUuid { get; set; }
}
