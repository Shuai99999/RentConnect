using RentConnect.Core.ValueObjects;

namespace RentConnect.Core.Entities;

public sealed class Property
{
    private readonly List<PropertyOwner> _owners = [];

    private Property()
    {
    }

    public Property(
        Guid id,
        string name,
        Address address,
        Guid initialOwnerProfileId)
        : this(
            id,
            name,
            address,
            initialOwnerProfileId,
            DateTimeOffset.UtcNow)
    {
    }

    public Property(
        Guid id,
        string name,
        Address address,
        Guid initialOwnerProfileId,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "Property ID cannot be empty.",
                nameof(id));
        }

        ArgumentNullException.ThrowIfNull(address);

        Id = id;
        SetName(name);
        Address = address;
        CreatedAtUtc = RequireUtc(createdAtUtc);

        _owners.Add(
            new PropertyOwner(
                id,
                initialOwnerProfileId));
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public Address Address { get; private set; } = null!;

    public GeoCoordinate? Location { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public IReadOnlyCollection<PropertyOwner> Owners => _owners;

    public void Rename(string name)
    {
        SetName(name);
    }

    public void ChangeAddress(Address address)
    {
        ArgumentNullException.ThrowIfNull(address);

        if (Address != address)
        {
            Location = null;
        }

        Address = address;
    }

    public void SetLocation(GeoCoordinate location)
    {
        ArgumentNullException.ThrowIfNull(location);

        Location = location;
    }

    public void AddOwner(Guid userProfileId)
    {
        if (userProfileId == Guid.Empty)
        {
            throw new ArgumentException(
                "User profile ID cannot be empty.",
                nameof(userProfileId));
        }

        if (_owners.Any(
                owner =>
                    owner.UserProfileId == userProfileId))
        {
            throw new InvalidOperationException(
                "This user is already an owner of the property.");
        }

        _owners.Add(
            new PropertyOwner(
                Id,
                userProfileId));
    }

    private void SetName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "Property name is required.",
                nameof(name));
        }

        Name = name.Trim();
    }

    private static DateTimeOffset RequireUtc(
        DateTimeOffset value)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "Creation time must use the UTC offset.",
                nameof(value));
        }

        return value;
    }
}