using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentConnect.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class BackfillMissingUserProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
        DECLARE @MissingProfiles TABLE
        (
            UserId nvarchar(450) NOT NULL,
            ProfileId uniqueidentifier NOT NULL
        );

        INSERT INTO @MissingProfiles (UserId, ProfileId)
        SELECT Id, NEWID()
        FROM AspNetUsers
        WHERE UserProfileId IS NULL;

        INSERT INTO UserProfiles (Id, DisplayName)
        SELECT ProfileId, N'Existing User'
        FROM @MissingProfiles;

        UPDATE users
        SET users.UserProfileId = missing.ProfileId
        FROM AspNetUsers AS users
        INNER JOIN @MissingProfiles AS missing
            ON missing.UserId = users.Id;
        """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // This data migration is intentionally irreversible.
            // Removing generated profiles could delete user-owned data.
        }
    }
}
