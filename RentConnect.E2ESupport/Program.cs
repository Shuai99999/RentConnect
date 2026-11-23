using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RentConnect.Core.Entities;
using RentConnect.Data;

const string connectionStringVariable =
    "E2E_CONNECTION_STRING";
const string emailVariable = "E2E_TEST_USER_EMAIL";
const string passwordVariable = "E2E_TEST_USER_PASSWORD";

var connectionString = GetRequiredVariable(
    connectionStringVariable);
var email = GetRequiredVariable(emailVariable);
var password = GetRequiredVariable(passwordVariable);

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

var user = await userManager.FindByEmailAsync(email);

if (user is null)
{
    var profile = new UserProfile(
        Guid.NewGuid(),
        "Playwright CI User");

    user = new ApplicationUser
    {
        UserName = email,
        Email = email,
        EmailConfirmed = true,
        UserProfileId = profile.Id,
        UserProfile = profile
    };

    var result = await userManager.CreateAsync(user, password);

    if (!result.Succeeded)
    {
        var errors = string.Join(
            "; ",
            result.Errors.Select(error => error.Description));

        throw new InvalidOperationException(
            $"Could not create the E2E test user: {errors}");
    }
}
else if (!user.EmailConfirmed)
{
    user.EmailConfirmed = true;
    var result = await userManager.UpdateAsync(user);

    if (!result.Succeeded)
    {
        throw new InvalidOperationException(
            "Could not confirm the E2E test user's email.");
    }
}

Console.WriteLine("E2E database and test user are ready.");

static string GetRequiredVariable(string name)
{
    var value = Environment.GetEnvironmentVariable(name);

    return string.IsNullOrWhiteSpace(value)
        ? throw new InvalidOperationException(
            $"Environment variable '{name}' is required.")
        : value;
}
