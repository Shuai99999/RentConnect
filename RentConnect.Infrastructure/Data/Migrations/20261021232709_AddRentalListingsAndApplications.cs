using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentConnect.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRentalListingsAndApplications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_UserNotifications_Type",
                table: "UserNotifications");

            migrationBuilder.CreateTable(
                name: "RentalListings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RentalUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    MonthlyRent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AvailableFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RentalListings", x => x.Id);
                    table.CheckConstraint("CK_RentalListings_MonthlyRent", "[MonthlyRent] > 0");
                    table.ForeignKey(
                        name: "FK_RentalListings_RentalUnits_RentalUnitId",
                        column: x => x.RentalUnitId,
                        principalTable: "RentalUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RentalApplications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RentalListingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApplicantUserProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ResolvedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RentalApplications", x => x.Id);
                    table.CheckConstraint("CK_RentalApplications_Resolution", "([Status] = 1 AND [ResolvedAtUtc] IS NULL) OR ([Status] IN (2, 3) AND [ResolvedAtUtc] IS NOT NULL)");
                    table.CheckConstraint("CK_RentalApplications_Status", "[Status] IN (1, 2, 3)");
                    table.ForeignKey(
                        name: "FK_RentalApplications_RentalListings_RentalListingId",
                        column: x => x.RentalListingId,
                        principalTable: "RentalListings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RentalApplications_UserProfiles_ApplicantUserProfileId",
                        column: x => x.ApplicantUserProfileId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_UserNotifications_Type",
                table: "UserNotifications",
                sql: "[Type] IN (1, 2, 3, 4, 5, 6, 7)");

            migrationBuilder.CreateIndex(
                name: "IX_RentalApplications_ApplicantUserProfileId_CreatedAtUtc",
                table: "RentalApplications",
                columns: new[] { "ApplicantUserProfileId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RentalApplications_RentalListingId_ApplicantUserProfileId",
                table: "RentalApplications",
                columns: new[] { "RentalListingId", "ApplicantUserProfileId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RentalListings_IsPublished_CreatedAtUtc",
                table: "RentalListings",
                columns: new[] { "IsPublished", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RentalListings_RentalUnitId",
                table: "RentalListings",
                column: "RentalUnitId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RentalApplications");

            migrationBuilder.DropTable(
                name: "RentalListings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_UserNotifications_Type",
                table: "UserNotifications");

            migrationBuilder.AddCheckConstraint(
                name: "CK_UserNotifications_Type",
                table: "UserNotifications",
                sql: "[Type] IN (1, 2, 3, 4)");
        }
    }
}
