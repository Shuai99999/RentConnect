using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RentConnect.Core.Entities;
using RentConnect.Data;

const string connectionStringVariable =
    "E2E_CONNECTION_STRING";
const string emailVariable = "E2E_TEST_USER_EMAIL";
const string passwordVariable = "E2E_TEST_USER_PASSWORD";
const string applicantEmailVariable = "E2E_TEST_APPLICANT_EMAIL";
const string applicantPasswordVariable = "E2E_TEST_APPLICANT_PASSWORD";

var connectionString = GetRequiredVariable(
    connectionStringVariable);
var email = GetRequiredVariable(emailVariable);
var password = GetRequiredVariable(passwordVariable);
var applicantEmail = GetRequiredVariable(applicantEmailVariable);
var applicantPassword = GetRequiredVariable(applicantPasswordVariable);

var services = new ServiceCollection();

services.AddLogging();
services.AddDbContextFactory<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = true;
        options.Stores.SchemaVersion =
            IdentitySchemaVersions.Version3;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>();

await using var provider = services.BuildServiceProvider();

await using (var context = await provider
    .GetRequiredService<IDbContextFactory<ApplicationDbContext>>()
    .CreateDbContextAsync())
{
    await context.Database.MigrateAsync();
}

var userManager = provider
    .GetRequiredService<UserManager<ApplicationUser>>();

await EnsureUserAsync(userManager, email, password, "Playwright CI Owner");
await EnsureUserAsync(
    userManager,
    applicantEmail,
    applicantPassword,
    "Playwright CI Applicant");

Console.WriteLine("E2E database and test users are ready.");

static async Task EnsureUserAsync(
    UserManager<ApplicationUser> userManager,
    string email,
    string password,
    string displayName)
{
    var user = await userManager.FindByEmailAsync(email);

    if (user is null)
    {
        var profile = new UserProfile(Guid.NewGuid(), displayName);

        user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            UserProfileId = profile.Id,
            UserProfile = profile
        };

        var createResult = await userManager.CreateAsync(user, password);

        if (!createResult.Succeeded)
        {
            var errors = string.Join(
                "; ",
                createResult.Errors.Select(error => error.Description));

            throw new InvalidOperationException(
                $"Could not create E2E user '{email}': {errors}");
        }

        return;
    }

    if (user.EmailConfirmed)
    {
        return;
    }

    user.EmailConfirmed = true;
    var updateResult = await userManager.UpdateAsync(user);

    if (!updateResult.Succeeded)
    {
        throw new InvalidOperationException(
            $"Could not confirm E2E user '{email}'.");
    }
}

static string GetRequiredVariable(string name)
{
    var value = Environment.GetEnvironmentVariable(name);

    return string.IsNullOrWhiteSpace(value)
        ? throw new InvalidOperationException(
            $"Environment variable '{name}' is required.")
        : value;
}
