# Azure Maps setup

RentConnect uses one Azure Maps account for:

- rendering the interactive rental map in the browser;
- converting a property address into latitude and longitude when the property is created or updated.

## Local development

1. In the Azure portal, create an **Azure Maps Account** with the **Gen2** pricing tier.
2. Open the account's **Authentication** page and copy the primary subscription key.
3. Store it in ASP.NET Core user secrets from the web project directory:

   ```powershell
   cd C:\vscodeC#\RentConnect\RentConnect
   dotnet user-secrets set "AzureMaps:SubscriptionKey" "YOUR_AZURE_MAPS_KEY"
   ```

4. Restart RentConnect and open **Find a home**.

Do not put the real key in `appsettings.json` or commit it to Git. The empty setting in that file only documents the expected configuration name.

Properties created after Azure Maps is configured are geocoded automatically. For an existing property that has no map location, open it as its landlord, edit it, and save the address again.

## Deployment

Set `AzureMaps__SubscriptionKey` in the host's secret configuration. The shared subscription key is acceptable for local testing. Before a public production launch, replace browser shared-key authentication with Microsoft Entra ID or a short-lived Azure Maps SAS token, restrict allowed origins, and configure an Azure budget alert.
