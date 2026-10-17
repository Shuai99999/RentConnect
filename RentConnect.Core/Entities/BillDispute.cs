namespace RentConnect.Core.Entities;

public sealed class BillDispute
{
    public const int MaxReasonLength = 2000;
    public const int MaxResolutionLength = 2000;

    private BillDispute()
    {
    }

    internal BillDispute(
        Guid id,
        Guid sharedBillId,
        Guid raisedByUserProfileId,
        string reason,
        DateTimeOffset raisedAtUtc)
    {
        RequireId(id, nameof(id), "Bill dispute ID");
        RequireId(sharedBillId, nameof(sharedBillId), "Shared bill ID");
        RequireId(
            raisedByUserProfileId,
            nameof(raisedByUserProfileId),
            "Raising user profile ID");

        Id = id;
        SharedBillId = sharedBillId;
        RaisedByUserProfileId = raisedByUserProfileId;
        Reason = NormalizeText(
            reason,
            MaxReasonLength,
            nameof(reason),
            "Dispute reason");
        RaisedAtUtc = RequireUtc(raisedAtUtc, nameof(raisedAtUtc));
    }

    public Guid Id { get; private set; }

    public Guid SharedBillId { get; private set; }

    public Guid RaisedByUserProfileId { get; private set; }

    public string Reason { get; private set; } = string.Empty;

    public DateTimeOffset RaisedAtUtc { get; private set; }

    public Guid? ResolvedByUserProfileId { get; private set; }

    public string? Resolution { get; private set; }

    public DateTimeOffset? ResolvedAtUtc { get; private set; }

    public bool IsResolved => ResolvedAtUtc.HasValue;

    internal void Resolve(
        Guid resolvedByUserProfileId,
        string resolution,
        DateTimeOffset resolvedAtUtc)
    {
        if (IsResolved)
        {
            throw new InvalidOperationException(
                "The bill dispute has already been resolved.");
        }

        RequireId(
            resolvedByUserProfileId,
            nameof(resolvedByUserProfileId),
            "Resolving user profile ID");

        var normalizedResolvedAtUtc =
            RequireUtc(resolvedAtUtc, nameof(resolvedAtUtc));

        if (normalizedResolvedAtUtc < RaisedAtUtc)
        {
            throw new ArgumentException(
                "Dispute resolution cannot be before the dispute was raised.",
                nameof(resolvedAtUtc));
        }

        var normalizedResolution = NormalizeText(
            resolution,
            MaxResolutionLength,
            nameof(resolution),
            "Dispute resolution");

        ResolvedByUserProfileId = resolvedByUserProfileId;
        Resolution = normalizedResolution;
        ResolvedAtUtc = normalizedResolvedAtUtc;
    }

    private static string NormalizeText(
        string value,
        int maximumLength,
        string parameterName,
        string displayName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                $"{displayName} is required.",
                parameterName);
        }

        var normalizedValue = value.Trim();

        if (normalizedValue.Length > maximumLength)
        {
            throw new ArgumentException(
                $"{displayName} cannot exceed {maximumLength} characters.",
                parameterName);
        }

        return normalizedValue;
    }

    private static void RequireId(
        Guid value,
        string parameterName,
        string displayName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException(
                $"{displayName} cannot be empty.",
                parameterName);
        }
    }

    private static DateTimeOffset RequireUtc(
        DateTimeOffset value,
        string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "The timestamp must use UTC.",
                parameterName);
        }

        return value;
    }
}
