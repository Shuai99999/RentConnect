using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentConnect.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMaintenanceAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AssignedAtUtc",
                table: "MaintenanceRequests",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AssignedToUserProfileId",
                table: "MaintenanceRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceRequests_AssignedToUserProfileId",
                table: "MaintenanceRequests",
                column: "AssignedToUserProfileId");

            migrationBuilder.AddForeignKey(
                name: "FK_MaintenanceRequests_UserProfiles_AssignedToUserProfileId",
                table: "MaintenanceRequests",
                column: "AssignedToUserProfileId",
                principalTable: "UserProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MaintenanceRequests_UserProfiles_AssignedToUserProfileId",
                table: "MaintenanceRequests");

            migrationBuilder.DropIndex(
                name: "IX_MaintenanceRequests_AssignedToUserProfileId",
                table: "MaintenanceRequests");

            migrationBuilder.DropColumn(
                name: "AssignedAtUtc",
                table: "MaintenanceRequests");

            migrationBuilder.DropColumn(
                name: "AssignedToUserProfileId",
                table: "MaintenanceRequests");
        }
    }
}
