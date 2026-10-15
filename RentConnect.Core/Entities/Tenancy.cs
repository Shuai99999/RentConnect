namespace RentConnect.Core.Entities;

public sealed class Tenancy
{
    private Tenancy()
    {
    }

    public Tenancy(
        Guid id,
        Guid rentalUnitId,
        DateOnly startDate,
        DateOnly? endDate)
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
    }

    public Guid Id { get; private set; }

    public Guid RentalUnitId { get; private set; }

    public DateOnly StartDate { get; private set; }

    public DateOnly? EndDate { get; private set; }

    public bool IsCancelled { get; private set; }

    public bool IsActiveOn(DateOnly date)
    {
        if (IsCancelled)
        {
            return false;
        }

        return date >= StartDate &&
               (!EndDate.HasValue || date <= EndDate.Value);
    }

    public void ChangeEndDate(DateOnly? endDate)
    {
        ValidateDateRange(StartDate, endDate);

        EndDate = endDate;
    }

    public void Cancel()
    {
        IsCancelled = true;
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
}