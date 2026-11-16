using Microsoft.AspNetCore.Authorization;
using RentConnect.Components.Pages.Listings;

namespace RentConnect.IntegrationTests;

public sealed class ListingPageAuthorizationTests
{
    [Theory]
    [InlineData(typeof(ListingEdit))]
    [InlineData(typeof(ListingManage))]
    public void ListingManagementRoutes_RequireAuthenticationWithoutRoleGate(
        Type pageType)
    {
        var attributes = pageType
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .ToArray();

        var attribute = Assert.Single(attributes);
        Assert.Null(attribute.Roles);
        Assert.Null(attribute.Policy);
    }
}
