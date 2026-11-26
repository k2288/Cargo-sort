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
        Debug.Log($"[IAP] Bazaar start: package {Application.identifier}, version {Application.version}, RSA key length {AdsConfig.BazaarRsaPublicKey.Length}");
        try
        {
            payment = new Payment(new PaymentConfiguration(SecurityCheck.Enable(AdsConfig.BazaarRsaPublicKey)));
        }
        catch (Exception e)
        {
            Debug.LogWarning("[IAP] Bazaar not available: " + e);
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
            Debug.Log($"[IAP] Bazaar owned purchases: {owned.status} {owned.message} ({owned.data?.Count ?? 0})");
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
        Debug.Log("[IAP] Bazaar connecting...");
        var result = await payment.Connect();
        connected = result.status == Status.Success;
        if (!connected) Debug.LogWarning("[IAP] Bazaar connect failed: " + result.status + " " + result.message + " " + result.stackTrace);
        else Debug.Log("[IAP] Bazaar connected");
        return connected;
    }

    public async void Buy(string productId, bool consumable, Action<bool> onDone)
    {
        try
        {
            Debug.Log($"[IAP] Bazaar buy {productId} (consumable: {consumable})");
            if (payment == null || !await Connect())
            {
                Debug.LogWarning("[IAP] Bazaar buy stopped: " + (payment == null ? "payment not created (see start error)" : "not connected"));
                onDone?.Invoke(false);
                return;
            }
            var sku = await payment.GetSkuDetails(productId);
            Debug.Log($"[IAP] Bazaar product {productId}: {sku.status} {sku.message} (found {sku.data?.Count ?? 0})");
            var result = await payment.Purchase(productId);
            Debug.Log($"[IAP] Bazaar purchase result: {result.status} {result.message}");
            if (result.status != Status.Success)
            {
                Debug.LogWarning($"[IAP] Bazaar purchase failed: {result.status} {result.message} {result.stackTrace}");
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
            Debug.LogWarning("[IAP] Bazaar purchase failed: " + e);
            onDone?.Invoke(false);
        }
    }
}
#endif
