#if CARGO_IAP
using System;
using System.Collections.Generic;
using System.Linq;
using CargoSort.Game;
using UnityEngine;
using UnityEngine.Purchasing;

/// <summary>
/// In-app purchases through Unity IAP 5. "remove_ads" is a non-consumable and is restored on every
/// start; every other shop product (coins, bundles, starter pack, piggy bank) is a consumable.
/// Create the products in Google Play Console / App Store Connect with the ids in Store.ProductIds.
/// </summary>
public sealed class UnityIapStore : IStoreBackend
{
    private StoreController store;
    private Action<bool> pendingBuy;
    private string pendingProduct;
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

            var products = new List<ProductDefinition>();
            foreach (var id in Store.ProductIds)
                products.Add(new ProductDefinition(id, Store.IsConsumable(id) ? ProductType.Consumable : ProductType.NonConsumable));
            store.FetchProducts(products);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[IAP] Store not available: " + e.Message);
        }
    }

    public void Restore()
    {
        if (connected) store.FetchPurchases();
    }

    public void Buy(string productId, bool consumable, Action<bool> onDone)
    {
        if (!connected || pendingBuy != null)
        {
            onDone?.Invoke(false);
            return;
        }
        pendingBuy = onDone;
        pendingProduct = productId;
        store.PurchaseProduct(productId);
    }

    private void OnPurchasePending(PendingOrder order)
    {
        // No server: confirm right away (this also consumes consumables), then give the items on confirm.
        store.ConfirmPurchase(order);
    }

    private void OnPurchaseConfirmed(Order order)
    {
        bool ok = order is ConfirmedOrder;
        foreach (var id in ProductIdsOf(order))
        {
            if (!ok) continue;
            if (id == Store.RemoveAdsProductId) Store.Grant();
            // A purchase we did not start in this session (interrupted earlier): give it now.
            if (pendingBuy == null || id != pendingProduct) Store.Deliver(id);
        }
        if (pendingBuy != null && ProductIdsOf(order).Contains(pendingProduct)) Finish(ok);
    }

    private void OnPurchaseFailed(FailedOrder order)
    {
        Debug.LogWarning("[IAP] Purchase failed: " + order);
        Finish(false);
    }

    private void OnPurchasesFetched(Orders orders)
    {
        foreach (var order in orders.ConfirmedOrders)
            if (ProductIdsOf(order).Contains(Store.RemoveAdsProductId)) Store.Grant();
    }

    private static IEnumerable<string> ProductIdsOf(Order order) => order.Info.PurchasedProductInfo.Select(p => p.productId);

    private void Finish(bool ok)
    {
        var done = pendingBuy;
        pendingBuy = null;
        pendingProduct = null;
        done?.Invoke(ok);
    }
}
#endif
