using System.Net;
using Azure;
using Azure.Communication.Email;
using Microsoft.Extensions.Logging;
using RentConnect.Application.Tenancies;

namespace RentConnect.Infrastructure.Tenancies;

public sealed class AzureCommunicationTenancyInvitationEmailSender(
    EmailClient emailClient,
    string senderAddress,
    Uri applicationBaseUri,
    ILogger<AzureCommunicationTenancyInvitationEmailSender> logger)
    : ITenancyInvitationEmailSender
{
    public async Task<bool> SendAsync(
        TenancyInvitationEmail message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        var invitationUri = new Uri(applicationBaseUri, $"invitations/{Uri.EscapeDataString(message.Token)}");
        var propertyName = WebUtility.HtmlEncode(message.PropertyName);
        var rentalUnitName = WebUtility.HtmlEncode(message.RentalUnitName);
        var invitationLink = WebUtility.HtmlEncode(invitationUri.ToString());
        var expiry = message.ExpiresAtUtc.ToString("MMMM d, yyyy 'at' h:mm tt 'UTC'");

        var plainText = $"You have been invited to join {message.PropertyName} — {message.RentalUnitName} on RentConnect. " +
                        $"Open {invitationUri} to accept. This link expires {expiry}.";
        var html = $$"""
            <!doctype html>
            <html lang="en"><body style="margin:0;background:#f7f6f2;font-family:Arial,sans-serif;color:#1f2d2b">
              <div style="max-width:600px;margin:0 auto;padding:32px 20px">
                <div style="background:#fff;border:1px solid #dde3df;border-radius:16px;padding:32px">
                  <p style="margin:0 0 8px;color:#287068;font-weight:700;text-transform:uppercase;letter-spacing:.08em">RentConnect invitation</p>
                  <h1 style="margin:0 0 12px;font-size:26px">Join {{propertyName}}</h1>
                  <p style="margin:0 0 24px;color:#667873">You have been invited to the {{rentalUnitName}} tenancy.</p>
                  <a href="{{invitationLink}}" style="display:inline-block;padding:12px 20px;border-radius:9px;background:#287068;color:#fff;text-decoration:none;font-weight:700">Accept invitation</a>
                  <p style="margin:24px 0 0;color:#667873;font-size:13px">This private link expires {{expiry}}. If you were not expecting it, you can ignore this email.</p>
                </div>
              </div>
            </body></html>
            """;

        try
        {
            await emailClient.SendAsync(
                WaitUntil.Started,
                senderAddress,
                message.RecipientEmail,
                "You are invited to a RentConnect tenancy",
                html,
                plainText,
                cancellationToken);
            return true;
        }
        catch (RequestFailedException exception)
        {
            logger.LogError(exception, "Azure Communication Services could not queue a tenancy invitation email for {RecipientEmail}.", message.RecipientEmail);
            return false;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unexpected failure while sending a tenancy invitation email for {RecipientEmail}.", message.RecipientEmail);
            return false;
        }
    }
}
