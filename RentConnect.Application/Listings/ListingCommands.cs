namespace RentConnect.Application.Listings;

public sealed record PublishListingCommand(
    Guid RentalUnitId,
    string Title,
    string Description,
    decimal MonthlyRent,
    DateOnly AvailableFrom);

public sealed record ApplyForListingCommand(Guid ListingId, string Message);
