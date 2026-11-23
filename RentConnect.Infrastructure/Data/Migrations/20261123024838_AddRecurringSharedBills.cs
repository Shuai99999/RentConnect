using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentConnect.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRecurringSharedBills : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "NextOccurrenceDate",
                table: "SharedBills",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "RecurrenceEndsOn",
                table: "SharedBills",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RecurrenceIntervalMonths",
                table: "SharedBills",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RecurringSeriesId",
                table: "SharedBills",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SharedBills_NextOccurrenceDate",
                table: "SharedBills",
                column: "NextOccurrenceDate",
                filter: "[NextOccurrenceDate] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SharedBills_RecurringSeriesId_PeriodStart",
                table: "SharedBills",
                columns: new[] { "RecurringSeriesId", "PeriodStart" },
                unique: true,
                filter: "[RecurringSeriesId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SharedBills_NextOccurrenceDate",
                table: "SharedBills");

            migrationBuilder.DropIndex(
                name: "IX_SharedBills_RecurringSeriesId_PeriodStart",
                table: "SharedBills");

            migrationBuilder.DropColumn(
                name: "NextOccurrenceDate",
                table: "SharedBills");

            migrationBuilder.DropColumn(
                name: "RecurrenceEndsOn",
                table: "SharedBills");

            migrationBuilder.DropColumn(
                name: "RecurrenceIntervalMonths",
                table: "SharedBills");

            migrationBuilder.DropColumn(
                name: "RecurringSeriesId",
                table: "SharedBills");
        }
    }
}
