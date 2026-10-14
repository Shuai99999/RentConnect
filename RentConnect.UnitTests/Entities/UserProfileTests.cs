using RentConnect.Core.Entities;

namespace RentConnect.UnitTests.Entities;

public class UserProfileTests
{
    [Fact]
    public void Constructor_WithValidValues_CreatesProfile()
    {
        var id = Guid.NewGuid();

        var profile = new UserProfile(id, "  Alice  ");

        Assert.Equal(id, profile.Id);
        Assert.Equal("Alice", profile.DisplayName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithoutDisplayName_ThrowsException(
        string displayName)
    {
        var action = () =>
            new UserProfile(Guid.NewGuid(), displayName);

        Assert.Throws<ArgumentException>(action);
    }

    [Fact]
    public void Rename_WithValidName_ChangesDisplayName()
    {
        var profile = new UserProfile(Guid.NewGuid(), "Alice");

        profile.Rename("  Alice Smith  ");

        Assert.Equal("Alice Smith", profile.DisplayName);
    }
}