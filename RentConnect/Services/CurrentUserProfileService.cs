using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using RentConnect.Data;

namespace RentConnect.Services;

public sealed class CurrentUserProfileService(
    AuthenticationStateProvider authenticationStateProvider,
    UserManager<ApplicationUser> userManager)
{
    public async Task<Guid> GetRequiredUserProfileIdAsync()
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

        var user = await userManager.GetUserAsync(principal);

        if (user?.UserProfileId is not Guid userProfileId)
        {
            throw new InvalidOperationException(
                "The current user does not have a user profile.");
        }

        return userProfileId;
    }
}