using RentConnect.Core.Entities;
using RentConnect.Core.Enums;

namespace RentConnect.UnitTests.Entities;

public sealed class RentalApplicationTests
{
    [Fact]
    public void Application_CanBeApprovedOnce()
    {
        var created = new DateTimeOffset(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);
        var application = new RentalApplication(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), " Interested ", created);

        application.Approve(created.AddMinutes(5));

        Assert.Equal(RentalApplicationStatus.Approved, application.Status);
        Assert.Equal("Interested", application.Message);
        Assert.Throws<InvalidOperationException>(() => application.Reject(created.AddMinutes(10)));
    }

    [Fact]
    public void Application_RejectsOverlongMessage()
    {
        Assert.Throws<ArgumentException>(() => new RentalApplication(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new string('x', RentalApplication.MaxMessageLength + 1), DateTimeOffset.UtcNow));
    }
}
