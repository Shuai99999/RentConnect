using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentConnect.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMaintenanceRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MaintenanceRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenancyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedByUserProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    Visibility = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ScheduledForUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ResolutionNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceRequests", x => x.Id);
                    table.CheckConstraint("CK_MaintenanceRequests_UpdateTime", "[UpdatedAtUtc] >= [CreatedAtUtc]");
                    table.ForeignKey(
                        name: "FK_MaintenanceRequests_Tenancies_TenancyId",
                        column: x => x.TenancyId,
                        principalTable: "Tenancies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenanceRequests_UserProfiles_CreatedByUserProfileId",
                        column: x => x.CreatedByUserProfileId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceRequests_CreatedByUserProfileId",
                table: "MaintenanceRequests",
                column: "CreatedByUserProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceRequests_TenancyId_CreatedAtUtc",
                table: "MaintenanceRequests",
                columns: new[] { "TenancyId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceRequests_TenancyId_Status",
                table: "MaintenanceRequests",
                columns: new[] { "TenancyId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MaintenanceRequests");
        }
    }
}
