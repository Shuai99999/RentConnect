namespace RentConnect.Core.Entities;

public sealed class Tenancy
{
    private readonly List<TenancyMember> _members = [];

    private Tenancy()
    {
    }

    public Tenancy(
        Guid id,
        Guid rentalUnitId,
        DateOnly startDate,
        DateOnly? endDate)
        : this(
            id,
            rentalUnitId,
            startDate,
            endDate,
            DateTimeOffset.UtcNow)
    {
    }

    public Tenancy(
        Guid id,
        Guid rentalUnitId,
        DateOnly startDate,
        DateOnly? endDate,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "Tenancy ID cannot be empty.",
                nameof(id));
        }

        if (rentalUnitId == Guid.Empty)
        {
            throw new ArgumentException(
                "Rental unit ID cannot be empty.",
                nameof(rentalUnitId));
        }

        ValidateDateRange(startDate, endDate);

        Id = id;
        RentalUnitId = rentalUnitId;
        StartDate = startDate;
        EndDate = endDate;
        CreatedAtUtc = RequireUtc(createdAtUtc);
    }

    public Guid Id { get; private set; }

    public Guid RentalUnitId { get; private set; }

    public DateOnly StartDate { get; private set; }

    public DateOnly? EndDate { get; private set; }

    public bool IsCancelled { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public IReadOnlyCollection<TenancyMember> Members => _members;

    public bool IsActiveOn(DateOnly date)
    {
        if (IsCancelled)
        {
            return false;
        }

        return date >= StartDate &&
               (!EndDate.HasValue || date <= EndDate.Value);
    }

    public bool IsMemberActiveOn(
        Guid userProfileId,
        DateOnly date)
    {
        if (!IsActiveOn(date))
        {
            return false;
        }

        return _members.Any(
            member =>
                member.UserProfileId == userProfileId &&
                member.IsActiveOn(date));
    }

    public TenancyMember AddMember(
        Guid membershipId,
        Guid userProfileId,
        DateOnly joinedOn)
    {
        ValidateMemberDate(joinedOn, nameof(joinedOn));

        var hasOverlappingMembership = _members.Any(
            member =>
                member.UserProfileId == userProfileId &&
                (!member.LeftOn.HasValue ||
                 member.LeftOn.Value >= joinedOn));

        if (hasOverlappingMembership)
        {
            throw new InvalidOperationException(
                "The user already has an overlapping membership.");
        }

        var member = new TenancyMember(
            membershipId,
            Id,
            userProfileId,
            joinedOn);

        _members.Add(member);

        return member;
    }

    public void MarkMemberLeft(
        Guid userProfileId,
        DateOnly leftOn)
    {
        if (userProfileId == Guid.Empty)
        {
            throw new ArgumentException(
                "User profile ID cannot be empty.",
                nameof(userProfileId));
        }

        ValidateMemberDate(leftOn, nameof(leftOn));

        var member = _members
            .Where(member =>
                member.UserProfileId == userProfileId &&
                !member.LeftOn.HasValue &&
                member.JoinedOn <= leftOn)
            .OrderByDescending(member => member.JoinedOn)
            .FirstOrDefault();

        if (member is null)
        {
            throw new InvalidOperationException(
                "No active membership was found for the user.");
        }

        member.MarkLeft(leftOn);
    }

    public void ChangeEndDate(DateOnly? endDate)
    {
        ValidateDateRange(StartDate, endDate);

        if (endDate.HasValue &&
            _members.Any(member =>
                member.JoinedOn > endDate.Value ||
                member.LeftOn.HasValue &&
                member.LeftOn.Value > endDate.Value))
        {
            throw new InvalidOperationException(
                "The tenancy cannot end before an existing membership date.");
        }

        EndDate = endDate;
    }

    public void Cancel()
    {
        IsCancelled = true;
    }

    private void ValidateMemberDate(
        DateOnly date,
        string parameterName)
    {
        if (date < StartDate ||
            EndDate.HasValue && date > EndDate.Value)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "Membership date must fall within the tenancy period.");
        }
    }

    private static void ValidateDateRange(
        DateOnly startDate,
        DateOnly? endDate)
    {
        if (endDate.HasValue && endDate.Value < startDate)
        {
            throw new ArgumentException(
                "Tenancy end date cannot be before its start date.",
                nameof(endDate));
        }
    }

    private static DateTimeOffset RequireUtc(DateTimeOffset value)
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
