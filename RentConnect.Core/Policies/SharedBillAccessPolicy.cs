using RentConnect.Core.Entities;
using RentConnect.Core.Enums;

namespace RentConnect.Core.Policies;

public static class SharedBillAccessPolicy
{
    public static bool CanView(SharedBillAccessContext access)
    {
        return access.IsPropertyOwner ||
               access.IsActiveTenancyMember;
    }

    public static bool CanCreate(SharedBillAccessContext access)
    {
        return CanView(access);
    }

    public static bool CanPublish(
        SharedBill bill,
        SharedBillAccessContext access)
    {
        return bill.Status == SharedBillStatus.Draft &&
               (access.IsPropertyOwner ||
                bill.CreatedByUserProfileId ==
                    access.CurrentUserProfileId);
    }

    public static bool CanVoid(
        SharedBill bill,
        SharedBillAccessContext access)
    {
        return (bill.Status is SharedBillStatus.Draft or
                SharedBillStatus.Published) &&
               (access.IsPropertyOwner ||
                bill.CreatedByUserProfileId ==
                    access.CurrentUserProfileId);
    }

    public static bool CanDeclarePayment(
        SharedBill bill,
        SharedBillAccessContext access)
    {
        return access.IsActiveTenancyMember &&
               access.HasBillShare &&
               (bill.Status is SharedBillStatus.Published or
                   SharedBillStatus.PartiallySettled or
                   SharedBillStatus.Disputed);
    }

    public static bool CanConfirmPayment(
        SharedBill bill,
        SharedBillAccessContext access)
    {
        return bill.PaidByUserProfileId ==
                   access.CurrentUserProfileId &&
               CanView(access) &&
               (bill.Status is SharedBillStatus.Published or
                   SharedBillStatus.PartiallySettled or
                   SharedBillStatus.Disputed);
    }

    public static bool CanRaiseDispute(
        SharedBill bill,
        SharedBillAccessContext access)
    {
        return access.IsActiveTenancyMember &&
               access.HasBillShare &&
               (bill.Status is SharedBillStatus.Published or
                   SharedBillStatus.PartiallySettled);
    }

    public static bool CanResolveDispute(
        SharedBill bill,
        SharedBillAccessContext access)
    {
        return bill.Status == SharedBillStatus.Disputed &&
               (access.IsPropertyOwner ||
                bill.PaidByUserProfileId ==
                    access.CurrentUserProfileId ||
                bill.CreatedByUserProfileId ==
                    access.CurrentUserProfileId);
    }
}
