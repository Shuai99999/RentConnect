using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentConnect.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeLegacyUserProfileNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE profiles
                SET profiles.DisplayName =
                    CASE
                        WHEN CHARINDEX(N'@', users.Email) > 1
                            THEN LEFT(users.Email, CHARINDEX(N'@', users.Email) - 1)
                        WHEN NULLIF(LTRIM(RTRIM(users.UserName)), N'') IS NOT NULL
                            THEN users.UserName
                        ELSE N'User'
                    END
                FROM UserProfiles AS profiles
                INNER JOIN AspNetUsers AS users
                    ON users.UserProfileId = profiles.Id
                WHERE profiles.DisplayName = N'Existing User';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // This data correction is intentionally irreversible because a
            // user may update the generated display name after migration.
        }
    }
}
