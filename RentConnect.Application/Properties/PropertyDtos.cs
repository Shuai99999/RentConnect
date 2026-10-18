using RentConnect.Core.Enums;

namespace RentConnect.Application.Properties;

public sealed record PropertySummary(
    Guid Id,
    string Name,
    string AddressLine1,
    string City,
    string? Region,
    string CountryCode,
    int VisibleUnitCount,
    int ActiveTenancyCount,
    bool CanManage);

public sealed record RentalUnitItem(
    Guid Id,
    string Name,
    RentalUnitType Type,
    bool IsActive,
    bool HasActiveTenancy,
    int CurrentTenantCount,
    DateOnly? CurrentTenancyStartDate,
    DateOnly? CurrentTenancyEndDate);

public sealed record PropertyDetails(
    Guid Id,
    string Name,
    string AddressLine1,
    string? AddressLine2,
    string City,
    string? Region,
    string? PostalCode,
    string CountryCode,
    bool CanManage,
    IReadOnlyList<string> OwnerNames,
    IReadOnlyList<RentalUnitItem> Units);
