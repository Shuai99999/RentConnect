using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentConnect.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ScopeImagesToRentalUnits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RentalUnitImages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RentalUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Content = table.Column<byte[]>(type: "varbinary(max)", maxLength: 5242880, nullable: false),
                    UploadedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RentalUnitImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RentalUnitImages_RentalUnits_RentalUnitId",
                        column: x => x.RentalUnitId,
                        principalTable: "RentalUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RentalUnitImages_RentalUnitId_UploadedAtUtc",
                table: "RentalUnitImages",
                columns: new[] { "RentalUnitId", "UploadedAtUtc" });

            // A property-level image was ambiguous when a property had more than
            // one rental space. Preserve it as an initial image for each existing
            // space; from this migration onward every copy is managed independently.
            migrationBuilder.Sql(
                """
                INSERT INTO [RentalUnitImages]
                    ([Id], [RentalUnitId], [ContentType], [Content], [UploadedAtUtc])
                SELECT NEWID(), [unit].[Id], [image].[ContentType],
                       [image].[Content], [image].[UploadedAtUtc]
                FROM [PropertyImages] AS [image]
                INNER JOIN [RentalUnits] AS [unit]
                    ON [unit].[PropertyId] = [image].[PropertyId];
                """);

            migrationBuilder.DropTable(
                name: "PropertyImages");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PropertyImages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Content = table.Column<byte[]>(type: "varbinary(max)", maxLength: 5242880, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PropertyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UploadedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropertyImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PropertyImages_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PropertyImages_PropertyId_UploadedAtUtc",
                table: "PropertyImages",
                columns: new[] { "PropertyId", "UploadedAtUtc" });

            migrationBuilder.Sql(
                """
                INSERT INTO [PropertyImages]
                    ([Id], [PropertyId], [ContentType], [Content], [UploadedAtUtc])
                SELECT NEWID(), [unit].[PropertyId], [image].[ContentType],
                       [image].[Content], [image].[UploadedAtUtc]
                FROM [RentalUnitImages] AS [image]
                INNER JOIN [RentalUnits] AS [unit]
                    ON [unit].[Id] = [image].[RentalUnitId];
                """);

            migrationBuilder.DropTable(
                name: "RentalUnitImages");
        }
    }
}
