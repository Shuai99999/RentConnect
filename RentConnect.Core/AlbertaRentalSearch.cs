using RentConnect.Core.ValueObjects;

namespace RentConnect.Core;

public static class AlbertaRentalSearch
{
    private static readonly Dictionary<string, GeoCoordinate> Centres = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Airdrie"] = new(51.29, -114.01),
        ["Calgary"] = new(51.05, -114.07),
        ["Chestermere"] = new(51.04, -113.82),
        ["Cochrane"] = new(51.19, -114.47),
        ["Edmonton"] = new(53.55, -113.49),
        ["Fort Saskatchewan"] = new(53.71, -113.21),
        ["Leduc"] = new(53.26, -113.55),
        ["Lethbridge"] = new(49.69, -112.83),
        ["Okotoks"] = new(50.73, -113.98),
        ["Red Deer"] = new(52.27, -113.81),
        ["Sherwood Park"] = new(53.54, -113.30),
        ["Spruce Grove"] = new(53.55, -113.90),
        ["St. Albert"] = new(53.63, -113.63),
        ["Stony Plain"] = new(53.53, -114.00)
    };

    public static IReadOnlyList<string> Cities { get; } =
        Array.AsReadOnly(Centres.Keys.OrderBy(city => city, StringComparer.OrdinalIgnoreCase).ToArray());

    public static GeoCoordinate? FindCentre(string? city)
    {
        if (string.IsNullOrWhiteSpace(city))
        {
            return null;
        }

        return Centres.GetValueOrDefault(city.Trim());
    }

    public static double DistanceKm(GeoCoordinate origin, GeoCoordinate destination)
    {
        const double toRadians = Math.PI / 180;
        const double earthRadiusKm = 6371;
        var latitudeDelta = (destination.Latitude - origin.Latitude) * toRadians;
        var longitudeDelta = (destination.Longitude - origin.Longitude) * toRadians;
        var halfChord = Math.Pow(Math.Sin(latitudeDelta / 2), 2) +
            Math.Cos(origin.Latitude * toRadians) * Math.Cos(destination.Latitude * toRadians) *
            Math.Pow(Math.Sin(longitudeDelta / 2), 2);

        return 2 * earthRadiusKm * Math.Asin(Math.Sqrt(Math.Clamp(halfChord, 0, 1)));
    }
}