using RentConnect.Core.Enums;

namespace RentConnect.Application.Listings;

public sealed record PublicListingSummary(
    Guid Id,
    string Title,
    string PropertyName,
    string RentalUnitName,
    RentalUnitType UnitType,
    string City,
    string? Region,
    decimal MonthlyRent,
    DateOnly AvailableFrom,
    int TenantCount);

public sealed record PublicListingDetails(
    Guid Id,
    Guid RentalUnitId,
    string Title,
    string Description,
    string PropertyName,
    string RentalUnitName,
    RentalUnitType UnitType,
    string City,
    string? Region,
    string CountryCode,
    decimal MonthlyRent,
    DateOnly AvailableFrom,
    int TenantCount,
    bool CanManage,
    RentalApplicationStatus? CurrentUserApplicationStatus);

public sealed record OwnedListingUnit(
    Guid RentalUnitId,
    string PropertyName,
    string RentalUnitName,
    RentalUnitType UnitType,
    bool IsActive,
    bool HasTenants,
    Guid? ListingId,
    string? ListingTitle,
    string? Description,
    decimal? MonthlyRent,
    DateOnly? AvailableFrom,
    bool IsPublished);

public sealed record ManagedRentalApplication(
    Guid Id,
    Guid ListingId,
    string ListingTitle,
    string PropertyName,
    string RentalUnitName,
    Guid ApplicantUserProfileId,
    string ApplicantName,
    string ApplicantEmail,
    string Message,
    RentalApplicationStatus Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ResolvedAtUtc);
