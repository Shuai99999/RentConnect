using Microsoft.AspNetCore.Authorization;
using RentConnect.Components.Pages.Listings;
using RentConnect.Components.Pages.Properties;
using RentConnect.Components.Pages.Tenancies;

namespace RentConnect.IntegrationTests;

public sealed class PageAuthorizationTests
{
    [Theory]
    [InlineData(typeof(ListingEdit))]
    [InlineData(typeof(ListingManage))]
    [InlineData(typeof(RentalApplicationList))]
    [InlineData(typeof(PropertyCreate))]
    [InlineData(typeof(TenancyCreate))]
    public void CapabilityPages_RequireAuthenticationWithoutRoleGate(
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
