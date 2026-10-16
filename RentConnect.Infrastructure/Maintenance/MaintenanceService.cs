using Microsoft.EntityFrameworkCore;
using RentConnect.Application.Maintenance;
using RentConnect.Core.Entities;
using RentConnect.Core.Enums;
using RentConnect.Core.Policies;
using RentConnect.Data;

namespace RentConnect.Infrastructure.Maintenance;

public sealed class MaintenanceService(
    ApplicationDbContext context,
    TimeProvider timeProvider,
    IMaintenanceAttachmentStorage attachmentStorage)
    : IMaintenanceService
{
    public async Task<IReadOnlyList<MaintenanceTenancyOption>>
        GetAvailableTenanciesAsync(
            Guid userProfileId,
            CancellationToken cancellationToken = default)
    {
        EnsureUserProfileId(userProfileId);

        var today = GetToday();

        return await (
            from tenancy in context.Tenancies.AsNoTracking()
            join unit in context.RentalUnits.AsNoTracking()
                on tenancy.RentalUnitId equals unit.Id
            join property in context.Properties.AsNoTracking()
                on unit.PropertyId equals property.Id
            where !tenancy.IsCancelled &&
                  tenancy.StartDate <= today &&
                  (!tenancy.EndDate.HasValue ||
                   tenancy.EndDate.Value >= today) &&
                  (
                      context.TenancyMembers.Any(member =>
                          member.TenancyId == tenancy.Id &&
                          member.UserProfileId == userProfileId &&
                          member.JoinedOn <= today &&
                          (!member.LeftOn.HasValue ||
                           member.LeftOn.Value >= today))
                      ||
                      context.PropertyOwners.Any(owner =>
                          owner.PropertyId == property.Id &&
                          owner.UserProfileId == userProfileId)
                  )
            orderby property.Name, unit.Name
            select new MaintenanceTenancyOption(
                tenancy.Id,
                property.Name,
                unit.Name))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MaintenanceRequestSummary>>
        GetVisibleRequestsAsync(
            Guid userProfileId,
            CancellationToken cancellationToken = default)
    {
        EnsureUserProfileId(userProfileId);

        var today = GetToday();

        var activeTenancyIds =
            from tenancy in context.Tenancies
            join member in context.TenancyMembers
                on tenancy.Id equals member.TenancyId
            where member.UserProfileId == userProfileId &&
                  !tenancy.IsCancelled &&
                  tenancy.StartDate <= today &&
                  (!tenancy.EndDate.HasValue ||
                   tenancy.EndDate.Value >= today) &&
                  member.JoinedOn <= today &&
                  (!member.LeftOn.HasValue ||
                   member.LeftOn.Value >= today)
            select tenancy.Id;

        var ownedTenancyIds =
            from tenancy in context.Tenancies
            join unit in context.RentalUnits
                on tenancy.RentalUnitId equals unit.Id
            join owner in context.PropertyOwners
                on unit.PropertyId equals owner.PropertyId
            where owner.UserProfileId == userProfileId
            select tenancy.Id;

        return await context.MaintenanceRequests
            .AsNoTracking()
            .Where(request =>
                request.CreatedByUserProfileId == userProfileId ||
                ownedTenancyIds.Contains(request.TenancyId) ||
                request.Visibility ==
                    MaintenanceVisibility.SharedWithTenancy &&
                activeTenancyIds.Contains(request.TenancyId))
            .OrderByDescending(request => request.UpdatedAtUtc)
            .Select(request => new MaintenanceRequestSummary(
                request.Id,
                request.TenancyId,
                request.Title,
                request.Category,
                request.Priority,
                request.Visibility,
                request.Status,
                request.UpdatedAtUtc))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<MaintenanceRequestDetails?> GetDetailsAsync(
        Guid maintenanceRequestId,
        Guid userProfileId,
        CancellationToken cancellationToken = default)
    {
        EnsureId(
            maintenanceRequestId,
            nameof(maintenanceRequestId));
        EnsureUserProfileId(userProfileId);

        var request = await context.MaintenanceRequests
            .AsNoTracking()
            .Include(item => item.StatusHistory)
            .SingleOrDefaultAsync(
                item => item.Id == maintenanceRequestId,
                cancellationToken);

        if (request is null)
        {
            return null;
        }

        var access = await CreateAccessContextAsync(
            request,
            userProfileId,
            cancellationToken);

        // Returning null for both missing and forbidden records prevents callers
        // from discovering the existence of private maintenance requests.
        if (!MaintenanceAccessPolicy.CanView(request, access))
        {
            return null;
        }

        var messages = await context.MaintenanceMessages
            .AsNoTracking()
            .Where(message =>
                message.MaintenanceRequestId == request.Id)
            .OrderBy(message => message.CreatedAtUtc)
            .ToArrayAsync(cancellationToken);

        var attachments =
            await context.MaintenanceAttachments
                .AsNoTracking()
                .Where(attachment =>
                    attachment.MaintenanceRequestId ==
                        request.Id)
                .OrderBy(attachment =>
                    attachment.UploadedAtUtc)
                .ToArrayAsync(cancellationToken);

        var statusHistory = request.StatusHistory
            .OrderBy(change => change.ChangedAtUtc)
            .Select(change => new MaintenanceStatusChangeItem(
                change.Id,
                change.FromStatus,
                change.ToStatus,
                change.ChangedByUserProfileId,
                change.ChangedAtUtc,
                change.Note))
            .ToArray();

        var messageItems = messages
            .Select(message => new MaintenanceMessageItem(
                message.Id,
                message.AuthorUserProfileId,
                message.IsDeleted ? null : message.Body,
                message.CreatedAtUtc,
                message.EditedAtUtc,
                message.IsDeleted,
                message.DeletedByUserProfileId,
                message.DeletedAtUtc,
                MaintenanceAccessPolicy.CanEditMessage(
                    request,
                    message,
                    access),
                MaintenanceAccessPolicy.CanDeleteMessage(
                    request,
                    message,
                    access)))
            .ToArray();

        var attachmentItems = attachments
            .Select(attachment =>
                new MaintenanceAttachmentItem(
                    attachment.Id,
                    attachment.UploadedByUserProfileId,
                    attachment.OriginalFileName,
                    attachment.ContentType,
                    attachment.FileSizeBytes,
                    attachment.UploadedAtUtc,
                    MaintenanceAccessPolicy
                        .CanDeleteAttachment(
                            request,
                            attachment,
                            access)))
            .ToArray();

        return new MaintenanceRequestDetails(
            request.Id,
            request.TenancyId,
            request.CreatedByUserProfileId,
            request.Title,
            request.Description,
            request.Category,
            request.Priority,
            request.Visibility,
            request.Status,
            request.CreatedAtUtc,
            request.UpdatedAtUtc,
            request.ScheduledForUtc,
            request.ResolutionNotes,
            statusHistory,
            messageItems,
            attachmentItems,
            CreatePermissions(request, access));
    }

    public async Task<Guid> CreateAsync(
        CreateMaintenanceRequestCommand command,
        Guid userProfileId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        EnsureUserProfileId(userProfileId);

        var tenancy = await context.Tenancies
            .Include(item => item.Members)
            .SingleOrDefaultAsync(
                item => item.Id == command.TenancyId,
                cancellationToken)
            ?? throw new KeyNotFoundException(
                "The tenancy was not found.");

        var today = GetToday();
        var isActiveMember =
            tenancy.IsMemberActiveOn(userProfileId, today);
        var isOwner = await IsPropertyOwnerAsync(
            tenancy.Id,
            userProfileId,
            cancellationToken);

        if (!tenancy.IsActiveOn(today) ||
            !isActiveMember && !isOwner)
        {
            throw new KeyNotFoundException(
                "The tenancy was not found.");
        }

        var request = new MaintenanceRequest(
            Guid.NewGuid(),
            tenancy.Id,
            userProfileId,
            command.Title,
            command.Description,
            command.Category,
            command.Priority,
            command.Visibility,
            timeProvider.GetUtcNow());

        context.MaintenanceRequests.Add(request);
        await context.SaveChangesAsync(cancellationToken);

        return request.Id;
    }

    public async Task UpdateDetailsAsync(
        UpdateMaintenanceRequestCommand command,
        Guid userProfileId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        EnsureUserProfileId(userProfileId);

        var request = await GetRequestAsync(
            command.MaintenanceRequestId,
            includeStatusHistory: false,
            cancellationToken);
        var access = await CreateAccessContextAsync(
            request,
            userProfileId,
            cancellationToken);

        Demand(
            MaintenanceAccessPolicy.CanUpdateDetails(request, access));

        request.UpdateDetails(
            command.Title,
            command.Description,
            command.Category,
            command.Priority,
            command.Visibility,
            timeProvider.GetUtcNow());

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<Guid> AddMessageAsync(
        AddMaintenanceMessageCommand command,
        Guid userProfileId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        EnsureUserProfileId(userProfileId);

        var request = await GetRequestAsync(
            command.MaintenanceRequestId,
            includeStatusHistory: false,
            cancellationToken);
        var access = await CreateAccessContextAsync(
            request,
            userProfileId,
            cancellationToken);

        Demand(
            MaintenanceAccessPolicy.CanPostMessage(request, access));

        var message = new MaintenanceMessage(
            Guid.NewGuid(),
            request.Id,
            userProfileId,
            command.Body,
            timeProvider.GetUtcNow());

        context.MaintenanceMessages.Add(message);
        await context.SaveChangesAsync(cancellationToken);

        return message.Id;
    }

    public async Task EditMessageAsync(
        EditMaintenanceMessageCommand command,
        Guid userProfileId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        EnsureUserProfileId(userProfileId);

        var message = await GetMessageAsync(
            command.MessageId,
            cancellationToken);
        var request = await GetRequestAsync(
            message.MaintenanceRequestId,
            includeStatusHistory: false,
            cancellationToken);
        var access = await CreateAccessContextAsync(
            request,
            userProfileId,
            cancellationToken);

        Demand(
            MaintenanceAccessPolicy.CanEditMessage(
                request,
                message,
                access));

        message.Edit(command.Body, timeProvider.GetUtcNow());
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteMessageAsync(
        DeleteMaintenanceMessageCommand command,
        Guid userProfileId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        EnsureUserProfileId(userProfileId);

        var message = await GetMessageAsync(
            command.MessageId,
            cancellationToken);
        var request = await GetRequestAsync(
            message.MaintenanceRequestId,
            includeStatusHistory: false,
            cancellationToken);
        var access = await CreateAccessContextAsync(
            request,
            userProfileId,
            cancellationToken);

        Demand(
            MaintenanceAccessPolicy.CanDeleteMessage(
                request,
                message,
                access));

        message.Delete(userProfileId, timeProvider.GetUtcNow());
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<Guid> AddAttachmentAsync(
        AddMaintenanceAttachmentCommand command,
        Guid userProfileId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        EnsureUserProfileId(userProfileId);

        var request = await GetRequestAsync(
            command.MaintenanceRequestId,
            includeStatusHistory: false,
            cancellationToken);
        var access = await CreateAccessContextAsync(
            request,
            userProfileId,
            cancellationToken);

        Demand(
            MaintenanceAccessPolicy.CanAddAttachment(
                request,
                access));

        var normalizedContentType =
            NormalizeImageContentType(command.ContentType);

        await using var bufferedContent =
            await BufferAndValidateImageAsync(
                command.Content,
                normalizedContentType,
                cancellationToken);

        var attachmentId = Guid.NewGuid();
        var storageKey = CreateStorageKey(
            request.Id,
            attachmentId,
            normalizedContentType);

        var attachment = new MaintenanceAttachment(
            attachmentId,
            request.Id,
            userProfileId,
            command.OriginalFileName,
            normalizedContentType,
            bufferedContent.Length,
            storageKey,
            timeProvider.GetUtcNow());

        var fileWasStored = false;

        try
        {
            await attachmentStorage.SaveAsync(
                storageKey,
                bufferedContent,
                cancellationToken);

            fileWasStored = true;
            context.MaintenanceAttachments.Add(attachment);

            await context.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            var entry = context.Entry(attachment);

            if (entry.State != EntityState.Detached)
            {
                entry.State = EntityState.Detached;
            }

            if (fileWasStored)
            {
                try
                {
                    await attachmentStorage.DeleteAsync(
                        storageKey,
                        CancellationToken.None);
                }
                catch
                {
                    // Preserve the original failure. Orphaned files can be
                    // removed by a maintenance cleanup process.
                }
            }

            throw;
        }

        return attachment.Id;
    }

    public async Task<MaintenanceAttachmentDownload?>
        OpenAttachmentAsync(
            Guid attachmentId,
            Guid userProfileId,
            CancellationToken cancellationToken = default)
    {
        EnsureId(attachmentId, nameof(attachmentId));
        EnsureUserProfileId(userProfileId);

        var attachment = await context.MaintenanceAttachments
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Id == attachmentId,
                cancellationToken);

        if (attachment is null)
        {
            return null;
        }

        var request = await context.MaintenanceRequests
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item =>
                    item.Id == attachment.MaintenanceRequestId,
                cancellationToken);

        if (request is null)
        {
            return null;
        }

        var access = await CreateAccessContextAsync(
            request,
            userProfileId,
            cancellationToken);

        if (!MaintenanceAccessPolicy.CanView(request, access))
        {
            return null;
        }

        var content = await attachmentStorage.OpenReadAsync(
            attachment.StorageKey,
            cancellationToken);

        if (content is null)
        {
            return null;
        }

        return new MaintenanceAttachmentDownload(
            attachment.OriginalFileName,
            attachment.ContentType,
            content);
    }

    public async Task DeleteAttachmentAsync(
        DeleteMaintenanceAttachmentCommand command,
        Guid userProfileId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        EnsureUserProfileId(userProfileId);

        var attachment = await GetAttachmentAsync(
            command.AttachmentId,
            cancellationToken);
        var request = await GetRequestAsync(
            attachment.MaintenanceRequestId,
            includeStatusHistory: false,
            cancellationToken);
        var access = await CreateAccessContextAsync(
            request,
            userProfileId,
            cancellationToken);

        Demand(
            MaintenanceAccessPolicy.CanDeleteAttachment(
                request,
                attachment,
                access));

        context.MaintenanceAttachments.Remove(attachment);
        await context.SaveChangesAsync(cancellationToken);

        await attachmentStorage.DeleteAsync(
            attachment.StorageKey,
            cancellationToken);
    }

    public async Task ChangeStatusAsync(
        ChangeMaintenanceStatusCommand command,
        Guid userProfileId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        EnsureUserProfileId(userProfileId);

        var request = await GetRequestAsync(
            command.MaintenanceRequestId,
            includeStatusHistory: true,
            cancellationToken);
        var access = await CreateAccessContextAsync(
            request,
            userProfileId,
            cancellationToken);
        var now = timeProvider.GetUtcNow();
        var statusChangeId = Guid.NewGuid();

        switch (command.Action)
        {
            case MaintenanceAction.Acknowledge:
                Demand(
                    MaintenanceAccessPolicy.CanAcknowledge(
                        request,
                        access));
                request.Acknowledge(
                    statusChangeId,
                    userProfileId,
                    now,
                    command.Note);
                break;

            case MaintenanceAction.Schedule:
                Demand(
                    MaintenanceAccessPolicy.CanSchedule(
                        request,
                        access));
                request.Schedule(
                    statusChangeId,
                    userProfileId,
                    command.ScheduledForUtc ??
                        throw new ArgumentException(
                            "A scheduled time is required.",
                            nameof(command)),
                    now,
                    command.Note);
                break;

            case MaintenanceAction.StartWork:
                Demand(
                    MaintenanceAccessPolicy.CanStartWork(
                        request,
                        access));
                request.StartWork(
                    statusChangeId,
                    userProfileId,
                    now,
                    command.Note);
                break;

            case MaintenanceAction.Resolve:
                Demand(
                    MaintenanceAccessPolicy.CanResolve(
                        request,
                        access));
                request.Resolve(
                    statusChangeId,
                    userProfileId,
                    command.Note ?? string.Empty,
                    now);
                break;

            case MaintenanceAction.Close:
                Demand(
                    MaintenanceAccessPolicy.CanClose(
                        request,
                        access));
                request.Close(
                    statusChangeId,
                    userProfileId,
                    now,
                    command.Note);
                break;

            case MaintenanceAction.Reopen:
                Demand(
                    MaintenanceAccessPolicy.CanReopen(
                        request,
                        access));
                request.Reopen(
                    statusChangeId,
                    userProfileId,
                    command.Note ?? string.Empty,
                    now);
                break;

            case MaintenanceAction.Cancel:
                Demand(
                    MaintenanceAccessPolicy.CanCancel(
                        request,
                        access));
                request.Cancel(
                    statusChangeId,
                    userProfileId,
                    now,
                    command.Note);
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(command),
                    command.Action,
                    "The maintenance action is invalid.");
        }

        var newStatusChange = request.StatusHistory.Single(
            change => change.Id == statusChangeId);

        // Status history uses application-assigned Guid keys. Explicitly marking
        // the new child as Added prevents EF from treating it as an update when
        // the aggregate was already tracked by an earlier operation in the scope.
        context.MaintenanceStatusChanges.Add(newStatusChange);

        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task<MaintenanceRequest> GetRequestAsync(
        Guid maintenanceRequestId,
        bool includeStatusHistory,
        CancellationToken cancellationToken)
    {
        EnsureId(
            maintenanceRequestId,
            nameof(maintenanceRequestId));

        IQueryable<MaintenanceRequest> query =
            context.MaintenanceRequests;

        if (includeStatusHistory)
        {
            query = query.Include(item => item.StatusHistory);
        }

        return await query.SingleOrDefaultAsync(
                   item => item.Id == maintenanceRequestId,
                   cancellationToken)
               ?? throw new KeyNotFoundException(
                   "The maintenance resource was not found.");
    }

    private async Task<MaintenanceMessage> GetMessageAsync(
        Guid messageId,
        CancellationToken cancellationToken)
    {
        EnsureId(messageId, nameof(messageId));

        return await context.MaintenanceMessages
                   .SingleOrDefaultAsync(
                       message => message.Id == messageId,
                       cancellationToken)
               ?? throw new KeyNotFoundException(
                   "The maintenance resource was not found.");
    }

    private async Task<MaintenanceAttachment> GetAttachmentAsync(
        Guid attachmentId,
        CancellationToken cancellationToken)
    {
        EnsureId(attachmentId, nameof(attachmentId));

        return await context.MaintenanceAttachments
                   .SingleOrDefaultAsync(
                       attachment =>
                           attachment.Id == attachmentId,
                       cancellationToken)
               ?? throw new KeyNotFoundException(
                   "The maintenance resource was not found.");
    }

    private async Task<MaintenanceAccessContext>
        CreateAccessContextAsync(
            MaintenanceRequest request,
            Guid userProfileId,
            CancellationToken cancellationToken)
    {
        var today = GetToday();

        var isActiveMember = await context.Tenancies
            .Where(tenancy =>
                tenancy.Id == request.TenancyId &&
                !tenancy.IsCancelled &&
                tenancy.StartDate <= today &&
                (!tenancy.EndDate.HasValue ||
                 tenancy.EndDate.Value >= today))
            .AnyAsync(
                tenancy => context.TenancyMembers.Any(member =>
                    member.TenancyId == tenancy.Id &&
                    member.UserProfileId == userProfileId &&
                    member.JoinedOn <= today &&
                    (!member.LeftOn.HasValue ||
                     member.LeftOn.Value >= today)),
                cancellationToken);

        var isOwner = await IsPropertyOwnerAsync(
            request.TenancyId,
            userProfileId,
            cancellationToken);

        return new MaintenanceAccessContext(
            userProfileId,
            isOwner,
            isActiveMember);
    }

    private Task<bool> IsPropertyOwnerAsync(
        Guid tenancyId,
        Guid userProfileId,
        CancellationToken cancellationToken)
    {
        return (
            from tenancy in context.Tenancies
            join unit in context.RentalUnits
                on tenancy.RentalUnitId equals unit.Id
            join owner in context.PropertyOwners
                on unit.PropertyId equals owner.PropertyId
            where tenancy.Id == tenancyId &&
                  owner.UserProfileId == userProfileId
            select owner)
            .AnyAsync(cancellationToken);
    }

    private static MaintenancePermissions CreatePermissions(
        MaintenanceRequest request,
        MaintenanceAccessContext access)
    {
        return new MaintenancePermissions(
            MaintenanceAccessPolicy.CanPostMessage(request, access),
            MaintenanceAccessPolicy.CanAddAttachment(request, access),
            MaintenanceAccessPolicy.CanUpdateDetails(request, access),
            MaintenanceAccessPolicy.CanCancel(request, access),
            MaintenanceAccessPolicy.CanAcknowledge(request, access),
            MaintenanceAccessPolicy.CanSchedule(request, access),
            MaintenanceAccessPolicy.CanStartWork(request, access),
            MaintenanceAccessPolicy.CanResolve(request, access),
            MaintenanceAccessPolicy.CanClose(request, access),
            MaintenanceAccessPolicy.CanReopen(request, access));
    }

    private static async Task<MemoryStream>
        BufferAndValidateImageAsync(
            Stream content,
            string contentType,
            CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (!content.CanRead)
        {
            throw new ArgumentException(
                "The attachment stream must be readable.",
                nameof(content));
        }

        var bufferedContent = new MemoryStream();
        var buffer = new byte[81920];

        try
        {
            while (true)
            {
                var bytesRead = await content.ReadAsync(
                    buffer.AsMemory(),
                    cancellationToken);

                if (bytesRead == 0)
                {
                    break;
                }

                if (bufferedContent.Length + bytesRead >
                    MaintenanceAttachment.MaxFileSizeBytes)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(content),
                        $"Attachment size cannot exceed {MaintenanceAttachment.MaxFileSizeBytes} bytes.");
                }

                await bufferedContent.WriteAsync(
                    buffer.AsMemory(0, bytesRead),
                    cancellationToken);
            }

            if (bufferedContent.Length == 0)
            {
                throw new ArgumentException(
                    "The attachment cannot be empty.",
                    nameof(content));
            }

            if (!bufferedContent.TryGetBuffer(
                    out var contentBuffer))
            {
                throw new InvalidOperationException(
                    "The attachment buffer could not be inspected.");
            }

            var headerLength = (int)Math.Min(
                12,
                bufferedContent.Length);

            if (!HasValidImageSignature(
                    contentType,
                    contentBuffer.AsSpan(0, headerLength)))
            {
                throw new ArgumentException(
                    "The file content does not match its declared image type.",
                    nameof(content));
            }

            bufferedContent.Position = 0;
            return bufferedContent;
        }
        catch
        {
            await bufferedContent.DisposeAsync();
            throw;
        }
    }

    private static string NormalizeImageContentType(
        string contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            throw new ArgumentException(
                "Content type is required.",
                nameof(contentType));
        }

        var normalizedContentType =
            contentType.Trim().ToLowerInvariant();

        if (normalizedContentType is not
            ("image/jpeg" or
             "image/png" or
             "image/webp"))
        {
            throw new ArgumentException(
                "Only JPEG, PNG, and WebP images are supported.",
                nameof(contentType));
        }

        return normalizedContentType;
    }

    private static bool HasValidImageSignature(
        string contentType,
        ReadOnlySpan<byte> header)
    {
        return contentType switch
        {
            "image/jpeg" =>
                header.Length >= 3 &&
                header[0] == 0xFF &&
                header[1] == 0xD8 &&
                header[2] == 0xFF,

            "image/png" =>
                header.Length >= 8 &&
                header[0] == 0x89 &&
                header[1] == 0x50 &&
                header[2] == 0x4E &&
                header[3] == 0x47 &&
                header[4] == 0x0D &&
                header[5] == 0x0A &&
                header[6] == 0x1A &&
                header[7] == 0x0A,

            "image/webp" =>
                header.Length >= 12 &&
                header[0] == 0x52 &&
                header[1] == 0x49 &&
                header[2] == 0x46 &&
                header[3] == 0x46 &&
                header[8] == 0x57 &&
                header[9] == 0x45 &&
                header[10] == 0x42 &&
                header[11] == 0x50,

            _ => false
        };
    }

    private static string CreateStorageKey(
        Guid maintenanceRequestId,
        Guid attachmentId,
        string contentType)
    {
        var extension = contentType switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            _ => throw new ArgumentOutOfRangeException(
                nameof(contentType),
                contentType,
                "The image content type is unsupported.")
        };

        return
            $"maintenance/{maintenanceRequestId:N}/{attachmentId:N}{extension}";
    }

    private DateOnly GetToday()
    {
        return DateOnly.FromDateTime(
            timeProvider.GetUtcNow().UtcDateTime);
    }

    private static void Demand(bool isAllowed)
    {
        if (!isAllowed)
        {
            // Missing and forbidden resources deliberately have the same public
            // failure shape so private requests cannot be discovered by ID.
            throw new KeyNotFoundException(
                "The maintenance resource was not found.");
        }
    }

    private static void EnsureUserProfileId(Guid userProfileId)
    {
        EnsureId(userProfileId, nameof(userProfileId));
    }

    private static void EnsureId(
        Guid id,
        string parameterName)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "The ID cannot be empty.",
                parameterName);
        }
    }
}
