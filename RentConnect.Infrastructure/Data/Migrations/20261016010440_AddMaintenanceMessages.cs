using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentConnect.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMaintenanceMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MaintenanceMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MaintenanceRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthorUserProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EditedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeletedByUserProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceMessages", x => x.Id);
                    table.CheckConstraint("CK_MaintenanceMessages_Deletion", "([DeletedAtUtc] IS NULL AND [DeletedByUserProfileId] IS NULL)\nOR\n([DeletedAtUtc] IS NOT NULL AND [DeletedByUserProfileId] IS NOT NULL)");
                    table.CheckConstraint("CK_MaintenanceMessages_EditedAfterCreation", "[EditedAtUtc] IS NULL\nOR [EditedAtUtc] > [CreatedAtUtc]");
                    table.ForeignKey(
                        name: "FK_MaintenanceMessages_MaintenanceRequests_MaintenanceRequestId",
                        column: x => x.MaintenanceRequestId,
                        principalTable: "MaintenanceRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MaintenanceMessages_UserProfiles_AuthorUserProfileId",
                        column: x => x.AuthorUserProfileId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenanceMessages_UserProfiles_DeletedByUserProfileId",
                        column: x => x.DeletedByUserProfileId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceMessages_AuthorUserProfileId",
                table: "MaintenanceMessages",
                column: "AuthorUserProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceMessages_DeletedByUserProfileId",
                table: "MaintenanceMessages",
                column: "DeletedByUserProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceMessages_MaintenanceRequestId_CreatedAtUtc",
                table: "MaintenanceMessages",
                columns: new[] { "MaintenanceRequestId", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MaintenanceMessages");
        }
    }
}
