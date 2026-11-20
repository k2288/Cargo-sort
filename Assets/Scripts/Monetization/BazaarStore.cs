#if CARGO_POOLAKEY
using System;
using Bazaar.Data;
using Bazaar.Poolakey;
using Bazaar.Poolakey.Data;
using CargoSort.Game;
using UnityEngine;

/// <summary>
/// In-app purchases through Cafe Bazaar (Poolakey). In the Bazaar developer panel, create in-app
/// products with the ids in Store.ProductIds (see Economy.cs). "remove_ads" is never consumed, so it
/// stays owned and is restored on start; every other product is consumed right after it is given,
/// so it can be bought again.
/// </summary>
public sealed class BazaarStore : IStoreBackend
{
    private Payment payment;
    private bool connected;

    public void Begin()
    {
        try
        {
            payment = new Payment(new PaymentConfiguration(SecurityCheck.Enable(AdsConfig.BazaarRsaPublicKey)));
        }
        catch (Exception e)
        {
            Debug.LogWarning("[IAP] Bazaar not available: " + e.Message);
            return;
        }
        Restore();
    }

    public async void Restore()
    {
        try
        {
            if (payment == null || !await Connect()) return;

            var owned = await payment.GetPurchases();
            if (owned.status != Status.Success || owned.data == null) return;
            foreach (var p in owned.data)
            {
                if (p.purchaseState != PurchaseInfo.State.Purchased) continue;
                if (p.productId == Store.RemoveAdsProductId)
                {
                    Store.Grant();
                    continue;
                }
                // A consumable that was paid for but not given (the app closed mid-purchase): give it now.
                var consumed = await payment.Consume(p.purchaseToken);
                if (consumed.status == Status.Success) Store.Deliver(p.productId);
            }
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

    public async void Buy(string productId, bool consumable, Action<bool> onDone)
    {
        try
        {
            if (payment == null || !await Connect())
            {
                onDone?.Invoke(false);
                return;
            }
            var result = await payment.Purchase(productId);
            if (result.status != Status.Success)
            {
                onDone?.Invoke(false);
                return;
            }
            if (productId == Store.RemoveAdsProductId) Store.Grant();
            if (consumable && result.data != null)
            {
                var consumed = await payment.Consume(result.data.purchaseToken);
                if (consumed.status != Status.Success) Debug.LogWarning("[IAP] Bazaar consume failed: " + consumed.message);
            }
            onDone?.Invoke(true);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[IAP] Bazaar purchase failed: " + e.Message);
            onDone?.Invoke(false);
        }
    }
}
#endif
