using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using RentConnect.Data;

namespace RentConnect.Services;

public sealed class CurrentUserProfileService(
    AuthenticationStateProvider authenticationStateProvider,
    IDbContextFactory<ApplicationDbContext> contextFactory)
{
    public async Task<Guid> GetRequiredUserProfileIdAsync()
    {
        var profile = await GetRequiredUserProfileAsync();

        return profile.Id;
    }

    public async Task<CurrentUserProfile> GetRequiredUserProfileAsync()
    {
        var authenticationState =
            await authenticationStateProvider
                .GetAuthenticationStateAsync();

        var principal = authenticationState.User;

        if (principal.Identity?.IsAuthenticated != true)
        {
            throw new UnauthorizedAccessException(
                "The current user is not authenticated.");
        }

        var userId = principal.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new InvalidOperationException(
                "The current user identifier is unavailable.");
        }

        await using var context =
            await contextFactory.CreateDbContextAsync();

        var profile = await context.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new CurrentUserProfile(
                user.UserProfileId ?? Guid.Empty,
                user.UserProfile!.DisplayName,
                user.Email ?? user.UserName ?? string.Empty))
            .SingleOrDefaultAsync();

        if (profile is null || profile.Id == Guid.Empty)
        {
            throw new InvalidOperationException(
                "The current user does not have a user profile.");
        }

        return profile;
    }
}

public sealed record CurrentUserProfile(
    Guid Id,
    string DisplayName,
    string Email);
