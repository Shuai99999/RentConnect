namespace RentConnect.Core.Entities;

public sealed class PropertyOwner
{
    private PropertyOwner()
    {
    }

    internal PropertyOwner(
        Guid propertyId,
        Guid userProfileId)
    {
        if (propertyId == Guid.Empty)
        {
            throw new ArgumentException(
                "Property ID cannot be empty.",
                nameof(propertyId));
        }

        if (userProfileId == Guid.Empty)
        {
            throw new ArgumentException(
                "User profile ID cannot be empty.",
                nameof(userProfileId));
        }

        PropertyId = propertyId;
        UserProfileId = userProfileId;
    }

    public Guid PropertyId { get; private set; }

    public Guid UserProfileId { get; private set; }
}