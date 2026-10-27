using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RentConnect.Application.Maintenance;
using RentConnect.Components;
using RentConnect.Components.Account;
using RentConnect.Data;
using RentConnect.Infrastructure.Maintenance;
using RentConnect.Services;
using RentConnect.Application.Notifications;
using RentConnect.Infrastructure.Notifications;
using RentConnect.Application.Billing;
using RentConnect.Infrastructure.Billing;
using RentConnect.Application.Properties;
using RentConnect.Infrastructure.Properties;
using RentConnect.Application.Tenancies;
using RentConnect.Infrastructure.Tenancies;
using RentConnect.Application.Listings;
using RentConnect.Infrastructure.Listings;
using Azure.Communication.Email;
using Azure.Identity;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<CurrentUserProfileService>();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<ISharedBillService, SharedBillService>();
builder.Services.AddScoped<IPropertyService, PropertyService>();
builder.Services.AddScoped<IPropertyImageService, PropertyImageService>();
builder.Services.AddScoped<ITenancyService, TenancyService>();
builder.Services.AddScoped<IRentalListingService, RentalListingService>();

var isAzureAppService = !string.IsNullOrWhiteSpace(
    Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID"));

if (isAzureAppService)
{
    builder.Services.AddDataProtection()
        .SetApplicationName("RentConnect")
        .PersistKeysToFileSystem(
            new DirectoryInfo("/home/data-protection-keys"));
}

var emailEndpoint = builder.Configuration["Email:AzureCommunicationServicesEndpoint"];
var emailSenderAddress = builder.Configuration["Email:SenderAddress"];
var applicationBaseUrl = builder.Configuration["Email:ApplicationBaseUrl"];

if (!string.IsNullOrWhiteSpace(emailEndpoint) &&
    !string.IsNullOrWhiteSpace(emailSenderAddress) &&
    !string.IsNullOrWhiteSpace(applicationBaseUrl))
{
    builder.Services.AddSingleton(
        new EmailClient(new Uri(emailEndpoint), new DefaultAzureCredential()));
    builder.Services.AddSingleton<ITenancyInvitationEmailSender>(services =>
        new AzureCommunicationTenancyInvitationEmailSender(
            services.GetRequiredService<EmailClient>(),
            emailSenderAddress,
            new Uri(applicationBaseUrl),
            services.GetRequiredService<ILogger<AzureCommunicationTenancyInvitationEmailSender>>()));
}
else
{
    builder.Services.AddSingleton<ITenancyInvitationEmailSender>(
        NullTenancyInvitationEmailSender.Instance);
}

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
    options.UseSqlServer(
        connectionString,
        sqlServerOptions => sqlServerOptions.EnableRetryOnFailure()));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IMaintenanceService, MaintenanceService>();

var blobServiceUri =
    builder.Configuration["AzureStorage:BlobServiceUri"];

if (string.IsNullOrWhiteSpace(blobServiceUri))
{
    builder.Services.AddSingleton(
        _ => new LocalMaintenanceAttachmentStorage(
            Path.Combine(
                builder.Environment.ContentRootPath,
                "App_Data",
                "MaintenanceAttachments")));
    builder.Services.AddSingleton<IMaintenanceAttachmentStorage>(
        services => services.GetRequiredService<LocalMaintenanceAttachmentStorage>());
    builder.Services.AddSingleton<IBillAttachmentStorage>(
        services => services.GetRequiredService<LocalMaintenanceAttachmentStorage>());
}
else
{
    var maintenanceContainerName =
        builder.Configuration[
            "AzureStorage:MaintenanceContainerName"]
        ?? throw new InvalidOperationException(
            "Azure Storage maintenance container name is not configured.");

    builder.Services.AddSingleton(
        _ => new AzureBlobMaintenanceAttachmentStorage(
            new Uri(blobServiceUri),
            maintenanceContainerName));
    builder.Services.AddSingleton<IMaintenanceAttachmentStorage>(
        services => services.GetRequiredService<AzureBlobMaintenanceAttachmentStorage>());
    builder.Services.AddSingleton<IBillAttachmentStorage>(
        services => services.GetRequiredService<AzureBlobMaintenanceAttachmentStorage>());
}

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = true;
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddRoles<IdentityRole>()
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddSignInManager()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

var app = builder.Build();

if (builder.Configuration.GetValue(
        "Database:ApplyMigrationsOnStartup",
        defaultValue: true))
{
    await using var migrationScope = app.Services.CreateAsyncScope();
    var dbContextFactory = migrationScope.ServiceProvider
        .GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
    await using var migrationContext =
        await dbContextFactory.CreateDbContextAsync();
    await migrationContext.Database.MigrateAsync();
}

using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider
        .GetRequiredService<RoleManager<IdentityRole>>();

    foreach (var role in new[] { "Landlord", "Renter" })
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            var result = await roleManager.CreateAsync(
                new IdentityRole(role));

            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Failed to create role: {role}");
            }
        }
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.Use(async (httpContext, next) =>
{
    httpContext.Response.OnStarting(() =>
    {
        var headers = httpContext.Response.Headers;
        headers.TryAdd("X-Content-Type-Options", "nosniff");
        headers.TryAdd("X-Frame-Options", "DENY");
        headers.TryAdd("Referrer-Policy", "strict-origin-when-cross-origin");
        headers.TryAdd(
            "Permissions-Policy",
            "camera=(), microphone=(), geolocation=(), payment=()");
        return Task.CompletedTask;
    });

    await next();
});

app.UseAntiforgery();

app.MapGet(
        "/maintenance/attachments/{attachmentId:guid}/content",
        async (
            Guid attachmentId,
            HttpContext httpContext,
            UserManager<ApplicationUser> userManager,
            IMaintenanceService maintenanceService,
            CancellationToken cancellationToken) =>
        {
            var user = await userManager.GetUserAsync(
                httpContext.User);

            if (user?.UserProfileId is not Guid userProfileId)
            {
                return Results.NotFound();
            }

            var download =
                await maintenanceService.OpenAttachmentAsync(
                    attachmentId,
                    userProfileId,
                    cancellationToken);

            if (download is null)
            {
                return Results.NotFound();
            }

            httpContext.Response.Headers[
                "X-Content-Type-Options"] = "nosniff";
            httpContext.Response.Headers[
                "Cache-Control"] = "private, no-store";
            httpContext.Response.Headers[
                "Pragma"] = "no-cache";

            return Results.File(
                download.Content,
                download.ContentType,
                enableRangeProcessing: false);
        })
    .RequireAuthorization();

app.MapGet(
        "/bills/attachments/{attachmentId:guid}/content",
        async (
            Guid attachmentId,
            HttpContext httpContext,
            UserManager<ApplicationUser> userManager,
            ISharedBillService sharedBillService,
            CancellationToken cancellationToken) =>
        {
            var user = await userManager.GetUserAsync(httpContext.User);

            if (user?.UserProfileId is not Guid userProfileId)
            {
                return Results.NotFound();
            }

            var download = await sharedBillService.OpenAttachmentAsync(
                attachmentId,
                userProfileId,
                cancellationToken);

            if (download is null)
            {
                return Results.NotFound();
            }

            httpContext.Response.Headers["X-Content-Type-Options"] = "nosniff";
            httpContext.Response.Headers["Cache-Control"] = "private, no-store";
            httpContext.Response.Headers["Pragma"] = "no-cache";

            return Results.File(
                download.Content,
                download.ContentType,
                download.OriginalFileName,
                enableRangeProcessing: false);
        })
    .RequireAuthorization();

app.MapGet(
    "/health",
    async (
        IDbContextFactory<ApplicationDbContext> dbContextFactory,
        CancellationToken cancellationToken) =>
    {
        await using var context = await dbContextFactory
            .CreateDbContextAsync(cancellationToken);
        return await context.Database.CanConnectAsync(cancellationToken)
            ? Results.Ok(new { status = "healthy" })
            : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    });

app.MapGet("/properties/images/{imageId:guid}", async (
    Guid imageId, HttpContext http, UserManager<ApplicationUser> users,
    IPropertyImageService images, CancellationToken cancellationToken) =>
{
    var user = await users.GetUserAsync(http.User);
    if (user?.UserProfileId is not Guid userId) return Results.NotFound();
    var image = await images.OpenAsync(imageId,userId,cancellationToken);
    http.Response.Headers["Cache-Control"] = "private, no-store";
    http.Response.Headers["X-Content-Type-Options"] = "nosniff";
    return image is null ? Results.NotFound() : Results.File(image.Content,image.ContentType);
}).RequireAuthorization();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

app.Run();
