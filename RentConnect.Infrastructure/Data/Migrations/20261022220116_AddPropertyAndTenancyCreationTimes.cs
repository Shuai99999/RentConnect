using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentConnect.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPropertyAndTenancyCreationTimes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAtUtc",
                table: "Tenancies",
                type: "datetimeoffset",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAtUtc",
                table: "Properties",
                type: "datetimeoffset",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.CreateIndex(
                name: "IX_Tenancies_CreatedAtUtc",
                table: "Tenancies",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Properties_CreatedAtUtc",
                table: "Properties",
                column: "CreatedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tenancies_CreatedAtUtc",
                table: "Tenancies");

            migrationBuilder.DropIndex(
                name: "IX_Properties_CreatedAtUtc",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "Tenancies");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "Properties");
        }
    }
}
