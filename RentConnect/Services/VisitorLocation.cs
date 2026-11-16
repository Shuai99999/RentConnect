using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace RentConnect.Services;

internal static class VisitorLocation
{
    private static readonly System.Net.IPNetwork[] PrivateNetworks =
    [
        System.Net.IPNetwork.Parse("0.0.0.0/8"),
        System.Net.IPNetwork.Parse("10.0.0.0/8"),
        System.Net.IPNetwork.Parse("100.64.0.0/10"),
        System.Net.IPNetwork.Parse("169.254.0.0/16"),
        System.Net.IPNetwork.Parse("172.16.0.0/12"),
        System.Net.IPNetwork.Parse("192.168.0.0/16"),
        System.Net.IPNetwork.Parse("224.0.0.0/3"),
        System.Net.IPNetwork.Parse("fc00::/7")
    ];

    public static async Task<IResult> GetAsync(
        HttpContext context, IConfiguration configuration, IHttpClientFactory clients, IHostEnvironment environment)
    {
        context.Response.Headers.CacheControl = "no-store";
        var token = configuration["IpLocation:Token"];
        var ip = context.Connection.RemoteIpAddress;
        if (ip is { IsIPv4MappedToIPv6: true })
        {
            ip = ip.MapToIPv4();
        }

        if (string.IsNullOrWhiteSpace(token) || ip is null)
        {
            return Results.NoContent();
        }

        string target;
        if (IPAddress.IsLoopback(ip) && environment.IsDevelopment())
        {
            // For a local demo, the app and browser share the development machine's connection.
            target = "me";
        }
        else
        {
            if (IPAddress.IsLoopback(ip) || ip.Equals(IPAddress.IPv6Any) ||
                ip.IsIPv6LinkLocal || ip.IsIPv6Multicast ||
                PrivateNetworks.Any(network => network.Contains(ip)))
            {
                return Results.NoContent();
            }

            target = Uri.EscapeDataString(ip.ToString());
        }

        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"https://api.ipinfo.io/lite/{target}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Trim());
        try
        {
            using var client = clients.CreateClient("VisitorLocation");
            using var response = await client.SendAsync(request, context.RequestAborted);
            if (!response.IsSuccessStatusCode)
            {
                return Results.NoContent();
            }

            using var json = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync(context.RequestAborted));
            var label = ReadText(json.RootElement, "country");
            return label is null ? Results.NoContent() : Results.Ok(label);
        }
        catch (Exception error) when (error is HttpRequestException or JsonException or ArgumentException ||
            error is OperationCanceledException && !context.RequestAborted.IsCancellationRequested)
        {
            return Results.NoContent();
        }
    }

    private static string? ReadText(JsonElement root, string name) =>
        root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 and <= 100 } text
            ? text
            : null;
}