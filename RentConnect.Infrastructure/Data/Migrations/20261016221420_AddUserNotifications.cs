using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentConnect.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserNotifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecipientUserProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorUserProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    TargetPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReadAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserNotifications", x => x.Id);
                    table.CheckConstraint("CK_UserNotifications_LocalTarget", "[TargetPath] LIKE '/%'\r\nAND\r\n[TargetPath] NOT LIKE '//%'");
                    table.CheckConstraint("CK_UserNotifications_ReadAfterCreation", "[ReadAtUtc] IS NULL\r\nOR\r\n[ReadAtUtc] >= [CreatedAtUtc]");
                    table.CheckConstraint("CK_UserNotifications_Type", "[Type] IN (1, 2, 3, 4)");
                    table.ForeignKey(
                        name: "FK_UserNotifications_UserProfiles_ActorUserProfileId",
                        column: x => x.ActorUserProfileId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_UserNotifications_UserProfiles_RecipientUserProfileId",
                        column: x => x.RecipientUserProfileId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserNotifications_ActorUserProfileId",
                table: "UserNotifications",
                column: "ActorUserProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_UserNotifications_RecipientUserProfileId_ReadAtUtc_CreatedAtUtc",
                table: "UserNotifications",
                columns: new[] { "RecipientUserProfileId", "ReadAtUtc", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserNotifications");
        }
    }
}
