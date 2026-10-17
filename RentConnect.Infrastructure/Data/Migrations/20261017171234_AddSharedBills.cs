using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentConnect.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSharedBills : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SharedBills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenancyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedByUserProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaidByUserProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Currency = table.Column<string>(type: "char(3)", unicode: false, fixedLength: true, maxLength: 3, nullable: false),
                    PeriodStart = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodEnd = table.Column<DateOnly>(type: "date", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SplitMethod = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SharedBills", x => x.Id);
                    table.CheckConstraint("CK_SharedBills_BillingPeriod", "[PeriodEnd] >= [PeriodStart]");
                    table.CheckConstraint("CK_SharedBills_DueDate", "[DueDate] >= [PeriodEnd]");
                    table.CheckConstraint("CK_SharedBills_TotalAmount", "[TotalAmount] > 0");
                    table.CheckConstraint("CK_SharedBills_UpdateTime", "[UpdatedAtUtc] >= [CreatedAtUtc]");
                    table.ForeignKey(
                        name: "FK_SharedBills_Tenancies_TenancyId",
                        column: x => x.TenancyId,
                        principalTable: "Tenancies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SharedBills_UserProfiles_CreatedByUserProfileId",
                        column: x => x.CreatedByUserProfileId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SharedBills_UserProfiles_PaidByUserProfileId",
                        column: x => x.PaidByUserProfileId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BillDisputes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SharedBillId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RaisedByUserProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    RaisedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ResolvedByUserProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Resolution = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ResolvedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillDisputes", x => x.Id);
                    table.CheckConstraint("CK_BillDisputes_ResolutionCompleteness", "([ResolvedAtUtc] IS NULL AND [ResolvedByUserProfileId] IS NULL AND [Resolution] IS NULL) OR ([ResolvedAtUtc] IS NOT NULL AND [ResolvedByUserProfileId] IS NOT NULL AND [Resolution] IS NOT NULL)");
                    table.CheckConstraint("CK_BillDisputes_ResolutionTime", "[ResolvedAtUtc] IS NULL OR [ResolvedAtUtc] >= [RaisedAtUtc]");
                    table.ForeignKey(
                        name: "FK_BillDisputes_SharedBills_SharedBillId",
                        column: x => x.SharedBillId,
                        principalTable: "SharedBills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BillDisputes_UserProfiles_RaisedByUserProfileId",
                        column: x => x.RaisedByUserProfileId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BillDisputes_UserProfiles_ResolvedByUserProfileId",
                        column: x => x.ResolvedByUserProfileId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BillShares",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SharedBillId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Currency = table.Column<string>(type: "char(3)", unicode: false, fixedLength: true, maxLength: 3, nullable: false),
                    PaymentDeclaredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PaymentConfirmedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillShares", x => x.Id);
                    table.CheckConstraint("CK_BillShares_Amount", "[Amount] >= 0");
                    table.CheckConstraint("CK_BillShares_PaymentSequence", "[PaymentConfirmedAtUtc] IS NULL OR ([PaymentDeclaredAtUtc] IS NOT NULL AND [PaymentConfirmedAtUtc] >= [PaymentDeclaredAtUtc])");
                    table.ForeignKey(
                        name: "FK_BillShares_SharedBills_SharedBillId",
                        column: x => x.SharedBillId,
                        principalTable: "SharedBills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BillShares_UserProfiles_UserProfileId",
                        column: x => x.UserProfileId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BillDisputes_RaisedByUserProfileId",
                table: "BillDisputes",
                column: "RaisedByUserProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_BillDisputes_ResolvedByUserProfileId",
                table: "BillDisputes",
                column: "ResolvedByUserProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_BillDisputes_SharedBillId_RaisedAtUtc",
                table: "BillDisputes",
                columns: new[] { "SharedBillId", "RaisedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_BillShares_SharedBillId_UserProfileId",
                table: "BillShares",
                columns: new[] { "SharedBillId", "UserProfileId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BillShares_UserProfileId",
                table: "BillShares",
                column: "UserProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_SharedBills_CreatedByUserProfileId",
                table: "SharedBills",
                column: "CreatedByUserProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_SharedBills_PaidByUserProfileId",
                table: "SharedBills",
                column: "PaidByUserProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_SharedBills_TenancyId_DueDate",
                table: "SharedBills",
                columns: new[] { "TenancyId", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SharedBills_TenancyId_Status_CreatedAtUtc",
                table: "SharedBills",
                columns: new[] { "TenancyId", "Status", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BillDisputes");

            migrationBuilder.DropTable(
                name: "BillShares");

            migrationBuilder.DropTable(
                name: "SharedBills");
        }
    }
}
