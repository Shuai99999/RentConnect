# Azure email setup

RentConnect can send tenancy invitations through Azure Communication Services Email. When the settings below are absent, invitation creation still works and the owner can copy the private link from the tenancy page.

## Azure resources

1. Create an **Email Communication Services** resource.
2. Provision the free Azure-managed domain (or verify a custom domain).
3. Create an **Azure Communication Services** resource and connect the verified email domain to it.
4. On the Communication Services resource, open **Access control (IAM)** and assign the App Service's managed identity the **Contributor** role. A narrower custom role may instead grant `Microsoft.Communication/CommunicationServices/Read` and `Microsoft.Communication/CommunicationServices/Write`.

Official references:

- https://learn.microsoft.com/azure/communication-services/quickstarts/email/send-email
- https://learn.microsoft.com/azure/communication-services/quickstarts/email/connect-email-communication-resource
- https://learn.microsoft.com/azure/communication-services/concepts/email/email-domain-and-sender-authentication
- https://learn.microsoft.com/samples/azure-samples/communication-services-dotnet-quickstarts/email-sample-send-email-with-managed-identity/

## App Service settings

Add these environment variables under **Settings → Environment variables**:

| Name | Example |
| --- | --- |
| `Email__AzureCommunicationServicesEndpoint` | `https://my-communication-service.communication.azure.com/` |
| `Email__SenderAddress` | `donotreply@xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx.azurecomm.net` |
| `Email__ApplicationBaseUrl` | `https://rentconnect-example.azurewebsites.net/` |

The sender address must be the **MailFrom** address displayed for the connected domain. Keep the trailing slash on the application base URL. Saving the settings restarts the App Service.

No access key or connection string is stored in configuration. `DefaultAzureCredential` uses the App Service managed identity in Azure and the developer's Azure credential chain locally.

## Verification

Open an owned tenancy, choose **Invite member**, and enter an email address. The result panel reports **Invitation emailed** when Azure accepted the message for delivery. If it reports **Invitation ready**, the invitation remains valid but email delivery was unavailable; inspect the App Service log stream for the Azure request error and use the copy-link fallback.
