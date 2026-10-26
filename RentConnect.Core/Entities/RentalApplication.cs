using RentConnect.Core.Enums;

namespace RentConnect.Core.Entities;

public sealed class RentalApplication
{
    public const int MaxMessageLength = 500;

    private RentalApplication() { }

    public RentalApplication(Guid id, Guid rentalListingId, Guid applicantUserProfileId,
        string message, DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty) throw new ArgumentException("Application ID cannot be empty.", nameof(id));
        if (rentalListingId == Guid.Empty) throw new ArgumentException("Listing ID cannot be empty.", nameof(rentalListingId));
        if (applicantUserProfileId == Guid.Empty) throw new ArgumentException("Applicant ID cannot be empty.", nameof(applicantUserProfileId));
        if (createdAtUtc.Offset != TimeSpan.Zero) throw new ArgumentException("Creation time must use UTC.", nameof(createdAtUtc));

        Id = id;
        RentalListingId = rentalListingId;
        ApplicantUserProfileId = applicantUserProfileId;
        Message = NormalizeMessage(message);
        Status = RentalApplicationStatus.Pending;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid RentalListingId { get; private set; }
    public Guid ApplicantUserProfileId { get; private set; }
    public string Message { get; private set; } = string.Empty;
    public RentalApplicationStatus Status { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? ResolvedAtUtc { get; private set; }

    public void Approve(DateTimeOffset resolvedAtUtc) => Resolve(RentalApplicationStatus.Approved, resolvedAtUtc);
    public void Reject(DateTimeOffset resolvedAtUtc) => Resolve(RentalApplicationStatus.Rejected, resolvedAtUtc);

    private void Resolve(RentalApplicationStatus status, DateTimeOffset resolvedAtUtc)
    {
        if (Status != RentalApplicationStatus.Pending) throw new InvalidOperationException("The application has already been resolved.");
        if (resolvedAtUtc.Offset != TimeSpan.Zero) throw new ArgumentException("Resolution time must use UTC.", nameof(resolvedAtUtc));
        if (resolvedAtUtc < CreatedAtUtc) throw new ArgumentException("Resolution time cannot be before creation time.", nameof(resolvedAtUtc));
        Status = status;
        ResolvedAtUtc = resolvedAtUtc;
    }

    private static string NormalizeMessage(string message)
    {
        var normalized = message?.Trim() ?? string.Empty;
        if (normalized.Length > MaxMessageLength) throw new ArgumentException($"Message cannot exceed {MaxMessageLength} characters.", nameof(message));
        return normalized;
    }
}
