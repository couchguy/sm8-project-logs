using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectLogs.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DailyLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountUuid = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    JobUuid = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    JobNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    LogDate = table.Column<DateOnly>(type: "date", nullable: false),
                    StaffUuid = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    DiaryNoteUuid = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: true),
                    Summary = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ClosedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProjectFlags",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountUuid = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    JobUuid = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    EnabledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EnabledByStaffUuid = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectFlags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TenantRegistrations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountUuid = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    AccessToken = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RefreshToken = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    TokenExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    InstalledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UninstalledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantRegistrations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DailyLogLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DailyLogId = table.Column<int>(type: "int", nullable: false),
                    SourceJobMaterialUuid = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    MaterialUuid = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: true),
                    Name = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    LineTotal = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    SnapshotAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyLogLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DailyLogLines_DailyLogs_DailyLogId",
                        column: x => x.DailyLogId,
                        principalTable: "DailyLogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DailyLogLines_DailyLogId",
                table: "DailyLogLines",
                column: "DailyLogId");

            migrationBuilder.CreateIndex(
                name: "IX_DailyLogLines_SourceJobMaterialUuid",
                table: "DailyLogLines",
                column: "SourceJobMaterialUuid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DailyLogs_AccountUuid_JobUuid_LogDate",
                table: "DailyLogs",
                columns: new[] { "AccountUuid", "JobUuid", "LogDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectFlags_AccountUuid_JobUuid",
                table: "ProjectFlags",
                columns: new[] { "AccountUuid", "JobUuid" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantRegistrations_AccountUuid",
                table: "TenantRegistrations",
                column: "AccountUuid",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyLogLines");

            migrationBuilder.DropTable(
                name: "ProjectFlags");

            migrationBuilder.DropTable(
                name: "TenantRegistrations");

            migrationBuilder.DropTable(
                name: "DailyLogs");
        }
    }
}
