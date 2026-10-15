using RentConnect.Core.Enums;

namespace RentConnect.Core.Entities;

public sealed class RentalUnit
{
    public const int MaxNameLength = 100;

    private RentalUnit()
    {
    }

    public RentalUnit(
        Guid id,
        Guid propertyId,
        string name,
        RentalUnitType type)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "Rental unit ID cannot be empty.",
                nameof(id));
        }

        if (propertyId == Guid.Empty)
        {
            throw new ArgumentException(
                "Property ID cannot be empty.",
                nameof(propertyId));
        }

        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(
                nameof(type),
                "Rental unit type is invalid.");
        }

        Id = id;
        PropertyId = propertyId;
        Type = type;
        IsActive = true;

        SetName(name);
    }

    public Guid Id { get; private set; }

    public Guid PropertyId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public RentalUnitType Type { get; private set; }

    public bool IsActive { get; private set; }

    public void Rename(string name)
    {
        SetName(name);
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public void Activate()
    {
        IsActive = true;
    }

    private void SetName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "Rental unit name is required.",
                nameof(name));
        }

        var normalizedName = name.Trim();

        if (normalizedName.Length > MaxNameLength)
        {
            throw new ArgumentException(
                $"Rental unit name cannot exceed {MaxNameLength} characters.",
                nameof(name));
        }

        Name = normalizedName;
    }
}
