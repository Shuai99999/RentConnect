using RentConnect.Core.Entities;

namespace RentConnect.UnitTests.Entities;

public sealed class RentalListingTests
{
    [Fact]
    public void Listing_NormalizesContentAndRent()
    {
        var listing = new RentalListing(
            Guid.NewGuid(), Guid.NewGuid(), "  Bright room  ", "  Near campus  ",
            899.995m, new DateOnly(2026, 11, 1), DateTimeOffset.UtcNow);

        Assert.Equal("Bright room", listing.Title);
        Assert.Equal("Near campus", listing.Description);
        Assert.Equal(900.00m, listing.MonthlyRent);
        Assert.True(listing.IsPublished);
    }

    [Fact]
    public void Listing_CanBeUnpublishedAndPublishedAgain()
    {
        var listing = new RentalListing(
            Guid.NewGuid(), Guid.NewGuid(), "Room", "Description", 800m,
            new DateOnly(2026, 11, 1), DateTimeOffset.UtcNow);

        listing.Unpublish();
        Assert.False(listing.IsPublished);

        listing.Publish();
        Assert.True(listing.IsPublished);
    }
}
