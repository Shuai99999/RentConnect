using System.Text.Json;
using Microsoft.Extensions.Logging;
using RentConnect.Core.ValueObjects;

namespace RentConnect.Infrastructure.Properties;

public interface IPropertyGeocoder
{
    Task<GeoCoordinate?> FindAsync(Address address, CancellationToken cancellationToken);
}

public sealed class AzureMapsPropertyGeocoder(
    HttpClient client,
    string subscriptionKey,
    ILogger<AzureMapsPropertyGeocoder> logger) : IPropertyGeocoder
{
    public async Task<GeoCoordinate?> FindAsync(Address address, CancellationToken cancellationToken)
    {
        var fields = new Dictionary<string, string?>
        {
            ["addressLine"] = address.Line1,
            ["locality"] = address.City,
            ["adminDistrict"] = address.Region,
            ["postalCode"] = address.PostalCode,
            ["countryRegion"] = address.CountryCode
        };
        var query = string.Join("&", fields.Where(field => !string.IsNullOrWhiteSpace(field.Value))
            .Select(field => $"{field.Key}={Uri.EscapeDataString(field.Value!)}"));
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"https://atlas.microsoft.com/geocode?api-version=2025-01-01&top=2&{query}");
        request.Headers.Add("subscription-key", subscriptionKey);

        try
        {
            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Property geocoding returned HTTP {StatusCode}.", (int)response.StatusCode);
                return null;
            }

            using var payload = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync(cancellationToken));
            return ReadLocation(payload.RootElement);
        }
        catch (Exception error) when (error is HttpRequestException or JsonException ||
                                     error is OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Property geocoding could not complete ({ErrorType}).", error.GetType().Name);
            return null;
        }
    }

    private static GeoCoordinate? ReadLocation(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("features", out var features) ||
            features.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        GeoCoordinate? match = null;
        foreach (var feature in features.EnumerateArray())
        {
            if (feature.ValueKind != JsonValueKind.Object ||
                !feature.TryGetProperty("properties", out var properties) ||
                properties.ValueKind != JsonValueKind.Object ||
                !HasText(properties, "type", "Address") ||
                !HasText(properties, "confidence", "High") ||
                !properties.TryGetProperty("matchCodes", out var codes) ||
                codes.ValueKind != JsonValueKind.Array ||
                codes.GetArrayLength() != 1 ||
                codes[0].ValueKind != JsonValueKind.String ||
                !codes[0].ValueEquals("Good") ||
                !feature.TryGetProperty("geometry", out var geometry) ||
                geometry.ValueKind != JsonValueKind.Object ||
                !HasText(geometry, "type", "Point") ||
                !geometry.TryGetProperty("coordinates", out var coordinates) ||
                coordinates.ValueKind != JsonValueKind.Array ||
                coordinates.GetArrayLength() < 2 ||
                !ReadNumber(coordinates[0], out var longitude) ||
                !ReadNumber(coordinates[1], out var latitude) ||
                longitude is < -180 or > 180 || latitude is < -90 or > 90)
            {
                continue;
            }

            if (match is not null)
            {
                return null;
            }
            match = new GeoCoordinate(latitude, longitude);
        }

        return match;
    }

    private static bool HasText(JsonElement parent, string key, string value) =>
        parent.TryGetProperty(key, out var property) && property.ValueKind == JsonValueKind.String &&
        property.ValueEquals(value);

    private static bool ReadNumber(JsonElement value, out double number)
    {
        number = 0;
        return value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out number) &&
            double.IsFinite(number);
    }
}