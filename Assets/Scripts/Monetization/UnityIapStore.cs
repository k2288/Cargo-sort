#if CARGO_IAP
using System;
using System.Collections.Generic;
using System.Linq;
using CargoSort.Game;
using UnityEngine;
using UnityEngine.Purchasing;

/// <summary>
/// The "remove_ads" non-consumable through Unity IAP 5. Restores the purchase on every start,
/// so reinstalling or a new phone keeps ads removed.
/// Create the product "remove_ads" in Google Play Console / App Store Connect with the same id.
/// </summary>
public sealed class UnityIapStore : IStoreBackend
{
    private StoreController store;
    private Action<bool> pendingBuy;
    private bool connected;

    public async void Begin()
    {
        try
        {
            store = UnityIAPServices.StoreController();
            store.OnPurchasePending += OnPurchasePending;
            store.OnPurchaseConfirmed += OnPurchaseConfirmed;
            store.OnPurchaseFailed += OnPurchaseFailed;
            store.OnProductsFetched += _ => store.FetchPurchases();
            store.OnPurchasesFetched += OnPurchasesFetched;

            await store.Connect();
            connected = true;

            store.FetchProducts(new List<ProductDefinition>
            {
                new ProductDefinition(Store.RemoveAdsProductId, ProductType.NonConsumable),
            });
        }
        catch (Exception e)
        {
            Debug.LogWarning("[IAP] Store not available: " + e.Message);
        }
    }

    public void BuyRemoveAds(Action<bool> onDone)
    {
        if (!connected)
        {
            onDone?.Invoke(false);
            return;
        }
        pendingBuy = onDone;
        store.PurchaseProduct(Store.RemoveAdsProductId);
    }

    private void OnPurchasePending(PendingOrder order)
    {
        // Non-consumable with no server: grant on confirm.
        store.ConfirmPurchase(order);
    }

    private void OnPurchaseConfirmed(Order order)
    {
        bool ok = order is ConfirmedOrder && IsRemoveAds(order);
        if (ok) Store.Grant();
        Finish(ok);
    }

    private void OnPurchaseFailed(FailedOrder order)
    {
        Debug.LogWarning("[IAP] Purchase failed: " + order);
        Finish(false);
    }

    private void OnPurchasesFetched(Orders orders)
    {
        foreach (var order in orders.ConfirmedOrders)
            if (IsRemoveAds(order)) Store.Grant();
    }

    private static bool IsRemoveAds(Order order) =>
        order.Info.PurchasedProductInfo.Any(p => p.productId == Store.RemoveAdsProductId);

    private void Finish(bool ok)
    {
        var done = pendingBuy;
        pendingBuy = null;
        done?.Invoke(ok);
    }
}
#endif
