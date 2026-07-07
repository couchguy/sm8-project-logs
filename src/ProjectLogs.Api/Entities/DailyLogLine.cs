namespace ProjectLogs.Api.Entities;

public class DailyLogLine
{
    public int Id { get; set; }
    public int DailyLogId { get; set; }
    public DailyLog DailyLog { get; set; } = null!;
    public required string SourceJobMaterialUuid { get; set; }
    public string? MaterialUuid { get; set; }
    public required string Name { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal? UnitCost { get; set; }
    public decimal LineTotal { get; set; }
    public DateTime SnapshotAtUtc { get; set; }
}
