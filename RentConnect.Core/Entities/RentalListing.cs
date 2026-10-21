namespace RentConnect.Core.Entities;

public sealed class RentalListing
{
    public const int MaxTitleLength = 150;
    public const int MaxDescriptionLength = 2000;

    private RentalListing() { }

    public RentalListing(Guid id, Guid rentalUnitId, string title, string description,
        decimal monthlyRent, DateOnly availableFrom, DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty) throw new ArgumentException("Listing ID cannot be empty.", nameof(id));
        if (rentalUnitId == Guid.Empty) throw new ArgumentException("Rental unit ID cannot be empty.", nameof(rentalUnitId));
        if (monthlyRent <= 0) throw new ArgumentOutOfRangeException(nameof(monthlyRent), "Monthly rent must be greater than zero.");
        if (createdAtUtc.Offset != TimeSpan.Zero) throw new ArgumentException("Creation time must use UTC.", nameof(createdAtUtc));

        Id = id;
        RentalUnitId = rentalUnitId;
        MonthlyRent = decimal.Round(monthlyRent, 2, MidpointRounding.AwayFromZero);
        AvailableFrom = availableFrom;
        CreatedAtUtc = createdAtUtc;
        IsPublished = true;
        SetContent(title, description);
    }

    public Guid Id { get; private set; }
    public Guid RentalUnitId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public decimal MonthlyRent { get; private set; }
    public DateOnly AvailableFrom { get; private set; }
    public bool IsPublished { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public void Update(string title, string description, decimal monthlyRent, DateOnly availableFrom)
    {
        if (monthlyRent <= 0) throw new ArgumentOutOfRangeException(nameof(monthlyRent), "Monthly rent must be greater than zero.");
        MonthlyRent = decimal.Round(monthlyRent, 2, MidpointRounding.AwayFromZero);
        AvailableFrom = availableFrom;
        SetContent(title, description);
    }

    public void Publish() => IsPublished = true;
    public void Unpublish() => IsPublished = false;

    private void SetContent(string title, string description)
    {
        Title = Normalize(title, MaxTitleLength, nameof(title));
        Description = Normalize(description, MaxDescriptionLength, nameof(description));
    }

    private static string Normalize(string value, int maximumLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A value is required.", parameterName);
        var normalized = value.Trim();
        if (normalized.Length > maximumLength) throw new ArgumentException($"The value cannot exceed {maximumLength} characters.", parameterName);
        return normalized;
    }
}
