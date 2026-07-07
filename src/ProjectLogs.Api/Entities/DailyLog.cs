namespace ProjectLogs.Api.Entities;

public class DailyLog
{
    public int Id { get; set; }
    public required string AccountUuid { get; set; }
    public required string JobUuid { get; set; }
    public string? JobNumber { get; set; }
    public DateOnly LogDate { get; set; }
    public required string StaffUuid { get; set; }
    public string? DiaryNoteUuid { get; set; }
    public string? Summary { get; set; }
    public DateTime ClosedAtUtc { get; set; }
    public ICollection<DailyLogLine> Lines { get; set; } = [];
}
