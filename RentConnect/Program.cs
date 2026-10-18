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
    builder.Services.AddSingleton<IMaintenanceAttachmentStorage>(
        _ => new LocalMaintenanceAttachmentStorage(
            Path.Combine(
                builder.Environment.ContentRootPath,
                "App_Data",
                "MaintenanceAttachments")));
}
else
{
    var maintenanceContainerName =
        builder.Configuration[
            "AzureStorage:MaintenanceContainerName"]
        ?? throw new InvalidOperationException(
            "Azure Storage maintenance container name is not configured.");

    builder.Services.AddSingleton<IMaintenanceAttachmentStorage>(
        _ => new AzureBlobMaintenanceAttachmentStorage(
            new Uri(blobServiceUri),
            maintenanceContainerName));
}

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = true;
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

var app = builder.Build();

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

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

app.Run();
