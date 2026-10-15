namespace RentConnect.Core.Entities;

public sealed class TenancyMember
{
    private TenancyMember()
    {
    }

    internal TenancyMember(
        Guid id,
        Guid tenancyId,
        Guid userProfileId,
        DateOnly joinedOn)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "Tenancy member ID cannot be empty.",
                nameof(id));
        }

        if (tenancyId == Guid.Empty)
        {
            throw new ArgumentException(
                "Tenancy ID cannot be empty.",
                nameof(tenancyId));
        }

        if (userProfileId == Guid.Empty)
        {
            throw new ArgumentException(
                "User profile ID cannot be empty.",
                nameof(userProfileId));
        }

        Id = id;
        TenancyId = tenancyId;
        UserProfileId = userProfileId;
        JoinedOn = joinedOn;
    }

    public Guid Id { get; private set; }

    public Guid TenancyId { get; private set; }

    public Guid UserProfileId { get; private set; }

    public DateOnly JoinedOn { get; private set; }

    public DateOnly? LeftOn { get; private set; }

    public bool IsActiveOn(DateOnly date)
    {
        return date >= JoinedOn &&
               (!LeftOn.HasValue || date <= LeftOn.Value);
    }

    internal void MarkLeft(DateOnly leftOn)
    {
        if (leftOn < JoinedOn)
        {
            throw new ArgumentException(
                "Leave date cannot be before join date.",
                nameof(leftOn));
        }

        if (LeftOn.HasValue)
        {
            throw new InvalidOperationException(
                "The tenancy member has already left.");
        }

        LeftOn = leftOn;
    }
}