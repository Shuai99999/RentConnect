using RentConnect.Core.Enums;

namespace RentConnect.Application.Properties;

public sealed record CreatePropertyCommand(
    string Name,
    string AddressLine1,
    string? AddressLine2,
    string City,
    string? Region,
    string? PostalCode,
    string CountryCode,
    double? Latitude = null,
    double? Longitude = null);

public sealed record UpdatePropertyCommand(
    Guid PropertyId,
    string Name,
    string AddressLine1,
    string? AddressLine2,
    string City,
    string? Region,
    string? PostalCode,
    string CountryCode,
    double? Latitude = null,
    double? Longitude = null);

public sealed record AddRentalUnitCommand(
    Guid PropertyId,
    string Name,
    RentalUnitType Type);

public sealed record UpdateRentalUnitCommand(
    Guid RentalUnitId,
    string Name,
    RentalUnitType Type);

public sealed record SetRentalUnitActiveCommand(
    Guid RentalUnitId,
    bool IsActive);