using RentConnect.Core.ValueObjects;

namespace RentConnect.UnitTests.ValueObjects;

public class GeoCoordinateTests
{
    [Theory]
    [InlineData(-90.0, -180.0)]
    [InlineData(0.0, 0.0)]
    [InlineData(90.0, 180.0)]
    public void Constructor_WithValidCoordinates_CreatesCoordinate(
        double latitude,
        double longitude)
    {
        var coordinate = new GeoCoordinate(latitude, longitude);

        Assert.Equal(latitude, coordinate.Latitude);
        Assert.Equal(longitude, coordinate.Longitude);
    }

    [Fact]
    public void TwoCoordinates_WithSameValues_AreEqual()
    {
        var first = new GeoCoordinate(43.6532, -79.3832);
        var second = new GeoCoordinate(43.6532, -79.3832);

        Assert.NotSame(first, second);
        Assert.Equal(first, second);
    }

    [Theory]
    [InlineData(-90.000001)]
    [InlineData(90.000001)]
    [InlineData(double.NaN)]
    public void Constructor_WithInvalidLatitude_ThrowsException(
        double latitude)
    {
        var action = () =>
            new GeoCoordinate(latitude, longitude: 0);

        Assert.Throws<ArgumentOutOfRangeException>(action);
    }

    [Theory]
    [InlineData(-180.000001)]
    [InlineData(180.000001)]
    [InlineData(double.NaN)]
    public void Constructor_WithInvalidLongitude_ThrowsException(
        double longitude)
    {
        var action = () =>
            new GeoCoordinate(latitude: 0, longitude);

        Assert.Throws<ArgumentOutOfRangeException>(action);
    }
}