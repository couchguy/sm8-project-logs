using Microsoft.EntityFrameworkCore;
using ProjectLogs.Api.Entities;

namespace ProjectLogs.Api.Data;

public class ProjectLogsDbContext(DbContextOptions<ProjectLogsDbContext> options) : DbContext(options)
{
    public DbSet<ProjectFlag> ProjectFlags => Set<ProjectFlag>();
    public DbSet<DailyLog> DailyLogs => Set<DailyLog>();
    public DbSet<DailyLogLine> DailyLogLines => Set<DailyLogLine>();
    public DbSet<TenantRegistration> TenantRegistrations => Set<TenantRegistration>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProjectFlag>(e =>
        {
            e.HasIndex(f => new { f.AccountUuid, f.JobUuid }).IsUnique();
            e.Property(f => f.AccountUuid).HasMaxLength(36);
            e.Property(f => f.JobUuid).HasMaxLength(36);
            e.Property(f => f.EnabledByStaffUuid).HasMaxLength(36);
        });

        modelBuilder.Entity<DailyLog>(e =>
        {
            e.HasIndex(d => new { d.AccountUuid, d.JobUuid, d.LogDate });
            e.Property(d => d.AccountUuid).HasMaxLength(36);
            e.Property(d => d.JobUuid).HasMaxLength(36);
            e.Property(d => d.StaffUuid).HasMaxLength(36);
            e.Property(d => d.DiaryNoteUuid).HasMaxLength(36);
            e.Property(d => d.JobNumber).HasMaxLength(50);
            e.Property(d => d.Summary).HasMaxLength(500);
        });

        modelBuilder.Entity<TenantRegistration>(e =>
        {
            e.HasIndex(t => t.AccountUuid).IsUnique();
            e.Property(t => t.AccountUuid).HasMaxLength(36);
            e.Property(t => t.AccessToken).HasMaxLength(2000);
            e.Property(t => t.RefreshToken).HasMaxLength(2000);
        });

        modelBuilder.Entity<DailyLogLine>(e =>
        {
            e.HasIndex(l => l.SourceJobMaterialUuid).IsUnique();
            e.Property(l => l.SourceJobMaterialUuid).HasMaxLength(36);
            e.Property(l => l.MaterialUuid).HasMaxLength(36);
            e.Property(l => l.Name).HasMaxLength(500);
            e.Property(l => l.Quantity).HasPrecision(18, 4);
            e.Property(l => l.UnitPrice).HasPrecision(18, 4);
            e.Property(l => l.UnitCost).HasPrecision(18, 4);
            e.Property(l => l.LineTotal).HasPrecision(18, 4);
        });
    }
}
