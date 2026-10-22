using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using RentConnect.Application.Tenancies;
using RentConnect.Core.Entities;
using RentConnect.Data;

namespace RentConnect.Infrastructure.Tenancies;

public sealed class TenancyService(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    TimeProvider timeProvider,
    ITenancyInvitationEmailSender invitationEmailSender) : ITenancyService
{
    public async Task<IReadOnlyList<TenancySummary>> GetVisibleAsync(
        Guid userProfileId,
        CancellationToken cancellationToken = default)
    {
        EnsureId(userProfileId, nameof(userProfileId));
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var ownedPropertyIds = context.PropertyOwners
            .Where(owner => owner.UserProfileId == userProfileId)
            .Select(owner => owner.PropertyId);

        return await (
                from tenancy in context.Tenancies.AsNoTracking()
                join unit in context.RentalUnits.AsNoTracking() on tenancy.RentalUnitId equals unit.Id
                join property in context.Properties.AsNoTracking() on unit.PropertyId equals property.Id
                where ownedPropertyIds.Contains(property.Id) ||
                      context.TenancyMembers.Any(member =>
                          member.TenancyId == tenancy.Id && member.UserProfileId == userProfileId)
                orderby tenancy.CreatedAtUtc descending, property.Name, unit.Name
                select new TenancySummary(
                    tenancy.Id,
                    property.Name,
                    unit.Name,
                    tenancy.StartDate,
                    tenancy.EndDate,
                    tenancy.IsCancelled,
                    context.TenancyMembers.Count(member => member.TenancyId == tenancy.Id),
                    ownedPropertyIds.Contains(property.Id)))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TenancyUnitOption>> GetAvailableUnitsAsync(
        Guid userProfileId,
        CancellationToken cancellationToken = default)
    {
        EnsureId(userProfileId, nameof(userProfileId));
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var today = Today;

        return await (
                from unit in context.RentalUnits.AsNoTracking()
                join property in context.Properties.AsNoTracking() on unit.PropertyId equals property.Id
                where unit.IsActive &&
                      context.PropertyOwners.Any(owner =>
                          owner.PropertyId == property.Id && owner.UserProfileId == userProfileId) &&
                      !context.Tenancies.Any(tenancy =>
                          tenancy.RentalUnitId == unit.Id && !tenancy.IsCancelled &&
                          (!tenancy.EndDate.HasValue || tenancy.EndDate.Value >= today))
                orderby property.Name, unit.Name
                select new TenancyUnitOption(unit.Id, property.Name, unit.Name))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<TenancyDetails?> GetDetailsAsync(
        Guid tenancyId,
        Guid userProfileId,
        CancellationToken cancellationToken = default)
    {
        EnsureId(tenancyId, nameof(tenancyId));
        EnsureId(userProfileId, nameof(userProfileId));
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var header = await (
                from tenancy in context.Tenancies.AsNoTracking()
                join unit in context.RentalUnits.AsNoTracking() on tenancy.RentalUnitId equals unit.Id
                join property in context.Properties.AsNoTracking() on unit.PropertyId equals property.Id
                where tenancy.Id == tenancyId
                select new
                {
                    Tenancy = tenancy,
                    PropertyId = property.Id,
                    PropertyName = property.Name,
                    UnitName = unit.Name,
                    CanManage = context.PropertyOwners.Any(owner =>
                        owner.PropertyId == property.Id && owner.UserProfileId == userProfileId),
                    IsMember = context.TenancyMembers.Any(member =>
                        member.TenancyId == tenancy.Id && member.UserProfileId == userProfileId)
                })
            .SingleOrDefaultAsync(cancellationToken);

        if (header is null || (!header.CanManage && !header.IsMember))
        {
            return null;
        }

        var members = await (
                from member in context.TenancyMembers.AsNoTracking()
                join profile in context.UserProfiles.AsNoTracking() on member.UserProfileId equals profile.Id
                join user in context.Users.AsNoTracking() on profile.Id equals user.UserProfileId
                where member.TenancyId == tenancyId
                orderby member.JoinedOn, profile.DisplayName
                select new TenancyMemberItem(
                    profile.Id,
                    profile.DisplayName,
                    user.Email ?? string.Empty,
                    member.JoinedOn,
                    member.LeftOn))
            .ToArrayAsync(cancellationToken);

        var invitations = header.CanManage
            ? await context.TenancyInvitations.AsNoTracking()
                .Where(invitation => invitation.TenancyId == tenancyId)
                .OrderByDescending(invitation => invitation.CreatedAtUtc)
                .Select(invitation => new TenancyInvitationItem(
                    invitation.Id,
                    invitation.Email,
                    invitation.CreatedAtUtc,
                    invitation.ExpiresAtUtc,
                    invitation.AcceptedAtUtc,
                    invitation.RevokedAtUtc))
                .ToArrayAsync(cancellationToken)
            : [];

        var tenancyRecord = header.Tenancy;
        return new TenancyDetails(
            tenancyRecord.Id,
            header.PropertyId,
            header.PropertyName,
            tenancyRecord.RentalUnitId,
            header.UnitName,
            tenancyRecord.StartDate,
            tenancyRecord.EndDate,
            tenancyRecord.IsCancelled,
            header.CanManage,
            members,
            invitations);
    }

    public async Task<Guid> CreateAsync(
        CreateTenancyCommand command,
        Guid userProfileId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        EnsureId(command.RentalUnitId, nameof(command.RentalUnitId));
        EnsureId(userProfileId, nameof(userProfileId));
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var unit = await GetManagedUnitAsync(context, command.RentalUnitId, userProfileId, cancellationToken);
        if (!unit.IsActive)
        {
            throw new InvalidOperationException("An inactive rental unit cannot receive a tenancy.");
        }

        var overlaps = await context.Tenancies.AnyAsync(
            tenancy => tenancy.RentalUnitId == unit.Id && !tenancy.IsCancelled &&
                       (!command.EndDate.HasValue || tenancy.StartDate <= command.EndDate.Value) &&
                       (!tenancy.EndDate.HasValue || tenancy.EndDate.Value >= command.StartDate),
            cancellationToken);
        if (overlaps)
        {
            throw new InvalidOperationException("The tenancy dates overlap an existing tenancy for this unit.");
        }

        var tenancy = new Tenancy(
            Guid.NewGuid(),
            unit.Id,
            command.StartDate,
            command.EndDate,
            timeProvider.GetUtcNow());
        context.Tenancies.Add(tenancy);
        await context.SaveChangesAsync(cancellationToken);
        return tenancy.Id;
    }

    public async Task UpdateEndDateAsync(
        UpdateTenancyEndDateCommand command,
        Guid userProfileId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var tenancy = await GetManagedTenancyAsync(context, command.TenancyId, userProfileId, cancellationToken);
        tenancy.ChangeEndDate(command.EndDate);

        var overlaps = await context.Tenancies.AnyAsync(
            other => other.Id != tenancy.Id && other.RentalUnitId == tenancy.RentalUnitId && !other.IsCancelled &&
                     (!command.EndDate.HasValue || other.StartDate <= command.EndDate.Value) &&
                     (!other.EndDate.HasValue || other.EndDate.Value >= tenancy.StartDate),
            cancellationToken);
        if (overlaps)
        {
            throw new InvalidOperationException("The updated dates overlap another tenancy for this unit.");
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task CancelAsync(Guid tenancyId, Guid userProfileId, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var tenancy = await GetManagedTenancyAsync(context, tenancyId, userProfileId, cancellationToken);
        tenancy.Cancel();
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<CreatedTenancyInvitation> InviteAsync(
        InviteTenancyMemberCommand command,
        Guid userProfileId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var normalizedEmail = ValidateAndNormalizeEmail(command.Email);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var tenancy = await GetManagedTenancyAsync(context, command.TenancyId, userProfileId, cancellationToken);

        if (command.JoinedOn < tenancy.StartDate || tenancy.EndDate.HasValue && command.JoinedOn > tenancy.EndDate.Value)
        {
            throw new ArgumentOutOfRangeException(nameof(command.JoinedOn), "Join date must fall within the tenancy period.");
        }

        var existingMember = await (
                from member in context.TenancyMembers
                join user in context.Users on member.UserProfileId equals user.UserProfileId
                where member.TenancyId == tenancy.Id && user.NormalizedEmail == normalizedEmail && !member.LeftOn.HasValue
                select member.Id)
            .AnyAsync(cancellationToken);
        if (existingMember)
        {
            throw new InvalidOperationException("This user is already a current tenancy member.");
        }

        var now = timeProvider.GetUtcNow();
        var pending = await context.TenancyInvitations
            .Where(invitation => invitation.TenancyId == tenancy.Id &&
                                 invitation.NormalizedEmail == normalizedEmail &&
                                 invitation.AcceptedAtUtc == null && invitation.RevokedAtUtc == null &&
                                 invitation.ExpiresAtUtc > now)
            .ToArrayAsync(cancellationToken);
        foreach (var invitation in pending)
        {
            invitation.Revoke(now);
        }

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var invitationRecord = new TenancyInvitation(
            Guid.NewGuid(),
            tenancy.Id,
            command.Email,
            HashToken(token),
            userProfileId,
            command.JoinedOn,
            now,
            now.AddDays(7));
        context.TenancyInvitations.Add(invitationRecord);

        await context.SaveChangesAsync(cancellationToken);

        var tenancyLabel = await (
                from unit in context.RentalUnits.AsNoTracking()
                join property in context.Properties.AsNoTracking() on unit.PropertyId equals property.Id
                where unit.Id == tenancy.RentalUnitId
                select new { PropertyName = property.Name, RentalUnitName = unit.Name })
            .SingleAsync(cancellationToken);
        var emailSent = await invitationEmailSender.SendAsync(
            new TenancyInvitationEmail(
                invitationRecord.Email,
                tenancyLabel.PropertyName,
                tenancyLabel.RentalUnitName,
                token,
                invitationRecord.ExpiresAtUtc),
            cancellationToken);

        return new CreatedTenancyInvitation(
            invitationRecord.Id,
            token,
            invitationRecord.ExpiresAtUtc,
            emailSent);
    }

    public async Task RevokeInvitationAsync(Guid invitationId, Guid userProfileId, CancellationToken cancellationToken = default)
    {
        EnsureId(invitationId, nameof(invitationId));
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var invitation = await context.TenancyInvitations.SingleOrDefaultAsync(item => item.Id == invitationId, cancellationToken);
        if (invitation is null)
        {
            throw new KeyNotFoundException("The invitation was not found.");
        }

        await GetManagedTenancyAsync(context, invitation.TenancyId, userProfileId, cancellationToken);
        invitation.Revoke(timeProvider.GetUtcNow());
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<TenancyInvitationPreview?> GetInvitationAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var tokenHash = HashToken(token);
        var now = timeProvider.GetUtcNow();
        return await (
                from invitation in context.TenancyInvitations.AsNoTracking()
                join tenancy in context.Tenancies.AsNoTracking() on invitation.TenancyId equals tenancy.Id
                join unit in context.RentalUnits.AsNoTracking() on tenancy.RentalUnitId equals unit.Id
                join property in context.Properties.AsNoTracking() on unit.PropertyId equals property.Id
                where invitation.TokenHash == tokenHash
                select new TenancyInvitationPreview(
                    property.Name,
                    unit.Name,
                    invitation.Email,
                    invitation.ExpiresAtUtc,
                    invitation.AcceptedAtUtc == null && invitation.RevokedAtUtc == null && invitation.ExpiresAtUtc > now && !tenancy.IsCancelled))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<Guid> AcceptInvitationAsync(
        string token,
        Guid userProfileId,
        string email,
        CancellationToken cancellationToken = default)
    {
        EnsureId(userProfileId, nameof(userProfileId));
        if (string.IsNullOrWhiteSpace(token)) throw new ArgumentException("Invitation token is required.", nameof(token));
        var normalizedEmail = ValidateAndNormalizeEmail(email);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var invitation = await context.TenancyInvitations.SingleOrDefaultAsync(
            item => item.TokenHash == HashToken(token), cancellationToken)
            ?? throw new KeyNotFoundException("The invitation was not found.");

        var now = timeProvider.GetUtcNow();
        if (!invitation.CanBeAcceptedAt(now))
        {
            throw new InvalidOperationException("The invitation is no longer available.");
        }

        if (!string.Equals(invitation.NormalizedEmail, normalizedEmail, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("Sign in with the email address that received this invitation.");
        }

        var tenancy = await context.Tenancies.Include(item => item.Members)
            .SingleAsync(item => item.Id == invitation.TenancyId, cancellationToken);
        if (tenancy.IsCancelled)
        {
            throw new InvalidOperationException("The tenancy has been cancelled.");
        }

        var joinedOn = invitation.JoinedOn;
        if (tenancy.EndDate.HasValue && joinedOn > tenancy.EndDate.Value)
        {
            throw new InvalidOperationException("The tenancy has already ended.");
        }

        var membership = tenancy.AddMember(Guid.NewGuid(), userProfileId, joinedOn);
        context.TenancyMembers.Add(membership);
        invitation.Accept(now);
        await context.SaveChangesAsync(cancellationToken);
        return tenancy.Id;
    }

    public async Task MarkMemberLeftAsync(
        MarkTenancyMemberLeftCommand command,
        Guid userProfileId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var tenancy = await context.Tenancies.Include(item => item.Members)
            .SingleOrDefaultAsync(item => item.Id == command.TenancyId, cancellationToken)
            ?? throw new KeyNotFoundException("The tenancy was not found.");
        await DemandOwnerAsync(context, tenancy.RentalUnitId, userProfileId, cancellationToken);
        tenancy.MarkMemberLeft(command.MemberUserProfileId, command.LeftOn);
        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task<Tenancy> GetManagedTenancyAsync(ApplicationDbContext context, Guid tenancyId, Guid userProfileId, CancellationToken cancellationToken)
    {
        EnsureId(tenancyId, nameof(tenancyId));
        EnsureId(userProfileId, nameof(userProfileId));
        var tenancy = await context.Tenancies.SingleOrDefaultAsync(item => item.Id == tenancyId, cancellationToken);
        if (tenancy is null) throw new KeyNotFoundException("The tenancy was not found.");
        await DemandOwnerAsync(context, tenancy.RentalUnitId, userProfileId, cancellationToken);
        return tenancy;
    }

    private static async Task<RentalUnit> GetManagedUnitAsync(ApplicationDbContext context, Guid rentalUnitId, Guid userProfileId, CancellationToken cancellationToken)
    {
        var unit = await context.RentalUnits.SingleOrDefaultAsync(item => item.Id == rentalUnitId, cancellationToken);
        if (unit is null) throw new KeyNotFoundException("The rental unit was not found.");
        await DemandOwnerAsync(context, unit.Id, userProfileId, cancellationToken);
        return unit;
    }

    private static async Task DemandOwnerAsync(ApplicationDbContext context, Guid rentalUnitId, Guid userProfileId, CancellationToken cancellationToken)
    {
        var ownsUnit = await (
                from unit in context.RentalUnits
                join owner in context.PropertyOwners on unit.PropertyId equals owner.PropertyId
                where unit.Id == rentalUnitId && owner.UserProfileId == userProfileId
                select unit.Id)
            .AnyAsync(cancellationToken);
        if (!ownsUnit) throw new KeyNotFoundException("The tenancy was not found.");
    }

    private DateOnly Today => DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
    private static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static string ValidateAndNormalizeEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) throw new ArgumentException("Email is required.", nameof(email));
        try
        {
            var address = new MailAddress(email.Trim());
            if (!string.Equals(address.Address, email.Trim(), StringComparison.OrdinalIgnoreCase))
                throw new FormatException();
        }
        catch (FormatException)
        {
            throw new ArgumentException("Enter a valid email address.", nameof(email));
        }
        return TenancyInvitation.NormalizeEmail(email);
    }

    private static void EnsureId(Guid id, string parameterName)
    {
        if (id == Guid.Empty) throw new ArgumentException("ID cannot be empty.", parameterName);
    }
}
