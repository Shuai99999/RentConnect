using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentConnect.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBillAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BillAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SharedBillId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UploadedByUserProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    UploadedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillAttachments", x => x.Id);
                    table.CheckConstraint("CK_BillAttachments_FileSize", "[FileSizeBytes] > 0 AND [FileSizeBytes] <= 10485760");
                    table.ForeignKey(
                        name: "FK_BillAttachments_SharedBills_SharedBillId",
                        column: x => x.SharedBillId,
                        principalTable: "SharedBills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BillAttachments_UserProfiles_UploadedByUserProfileId",
                        column: x => x.UploadedByUserProfileId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BillAttachments_SharedBillId_UploadedAtUtc",
                table: "BillAttachments",
                columns: new[] { "SharedBillId", "UploadedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_BillAttachments_StorageKey",
                table: "BillAttachments",
                column: "StorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BillAttachments_UploadedByUserProfileId",
                table: "BillAttachments",
                column: "UploadedByUserProfileId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BillAttachments");
        }
    }
}
