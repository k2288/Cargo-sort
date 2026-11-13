#if CARGO_POOLAKEY
using System;
using Bazaar.Data;
using Bazaar.Poolakey;
using Bazaar.Poolakey.Data;
using CargoSort.Game;
using UnityEngine;

/// <summary>
/// The "remove_ads" purchase through Cafe Bazaar (Poolakey). Restores the purchase on start.
/// Create an in-app product with id "remove_ads" in the Bazaar developer panel (not consumable:
/// we never consume it, so it stays owned).
/// </summary>
public sealed class BazaarStore : IStoreBackend
{
    private Payment payment;
    private bool connected;

    public async void Begin()
    {
        try
        {
            payment = new Payment(new PaymentConfiguration(SecurityCheck.Enable(AdsConfig.BazaarRsaPublicKey)));
            if (!await Connect()) return;

            var owned = await payment.GetPurchases();
            if (owned.status != Status.Success || owned.data == null) return;
            foreach (var p in owned.data)
                if (p.productId == Store.RemoveAdsProductId && p.purchaseState == PurchaseInfo.State.Purchased)
                    Store.Grant();
        }
        catch (Exception e)
        {
            Debug.LogWarning("[IAP] Bazaar not available: " + e.Message);
        }
    }

    private async System.Threading.Tasks.Task<bool> Connect()
    {
        if (connected) return true;
        var result = await payment.Connect();
        connected = result.status == Status.Success;
        if (!connected) Debug.LogWarning("[IAP] Bazaar connect: " + result.status + " " + result.message);
        return connected;
    }

    public async void BuyRemoveAds(Action<bool> onDone)
    {
        try
        {
            if (payment == null || !await Connect())
            {
                onDone?.Invoke(false);
                return;
            }
            var result = await payment.Purchase(Store.RemoveAdsProductId);
            onDone?.Invoke(result.status == Status.Success);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[IAP] Bazaar purchase failed: " + e.Message);
            onDone?.Invoke(false);
        }
    }
}
#endif
