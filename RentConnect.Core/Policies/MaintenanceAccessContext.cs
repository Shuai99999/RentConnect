namespace RentConnect.Core.Policies;

public sealed class MaintenanceAccessContext
{
    public MaintenanceAccessContext(
        Guid userProfileId,
        bool isPropertyOwner,
        bool isActiveTenancyMember)
    {
        if (userProfileId == Guid.Empty)
        {
            throw new ArgumentException(
                "User profile ID cannot be empty.",
                nameof(userProfileId));
        }

        UserProfileId = userProfileId;
        IsPropertyOwner = isPropertyOwner;
        IsActiveTenancyMember = isActiveTenancyMember;
    }

    public Guid UserProfileId { get; }

    public bool IsPropertyOwner { get; }

    public bool IsActiveTenancyMember { get; }
}
