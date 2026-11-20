using System;
using UnityEngine;

namespace CargoSort.Game
{
    public enum Booster { Undo, Slot, Crane, Magnet, Shuffle }

    /// <summary>What a reward or a pack contains.</summary>
    public enum ItemKind { Coins, NoAds, Undo, Slot, Crane, Magnet, Shuffle }

    public struct Item
    {
        public ItemKind Kind;
        public int Amount;
        public Item(ItemKind kind, int amount) { Kind = kind; Amount = amount; }
        public static Item Coins(int n) => new Item(ItemKind.Coins, n);
        public static Item Of(Booster b, int n) => new Item((ItemKind)((int)ItemKind.Undo + (int)b), n);
        public bool IsBooster => Kind >= ItemKind.Undo;
        public Booster AsBooster => (Booster)((int)Kind - (int)ItemKind.Undo);
    }

    /// <summary>A real-money offer in the shop.</summary>
    public sealed class Pack
    {
        public string ProductId;     // same id in Google Play Console / Cafe Bazaar panel
        public bool Consumable = true;
        public string PriceEn, PriceFa; // sample prices for the test build; set them to your store prices
        public string OldPriceEn, OldPriceFa;
        public string TagKey;        // ribbon like "+25% BONUS"
        public Item[] Items;

        /// <summary>Price ready to show (Persian is shaped for UI Text).</summary>
        public string Price => Loc.Persian ? CargoSort.Core.PersianShaper.Fix(PriceRaw) : PriceEn;
        public string OldPrice => Loc.Persian ? CargoSort.Core.PersianShaper.Fix(Loc.PersianDigits(OldPriceFa)) : OldPriceEn;

        /// <summary>Price not yet shaped, to pass into Loc.T as an argument.</summary>
        public string PriceRaw => Loc.Persian ? Loc.PersianDigits(PriceFa) : PriceEn;
    }

    /// <summary>A crate or truck look, bought with coins.</summary>
    public sealed class Skin
    {
        public string Key;
        public int Price;
        public Skin(string key, int price) { Key = key; Price = price; }
    }

    /// <summary>Prices, packs and skins. Tune these from analytics.</summary>
    public static class Economy
    {
        public static readonly Booster[] Boosters = { Booster.Undo, Booster.Slot, Booster.Crane, Booster.Magnet, Booster.Shuffle };

        /// <summary>Coin price of one use when the player has none left.</summary>
        public static int Price(Booster b)
        {
            switch (b)
            {
                case Booster.Undo: return 30;
                case Booster.Slot: return 60;
                case Booster.Crane: return 80;
                case Booster.Magnet: return 100;
                default: return 50;
            }
        }

        /// <summary>Your privacy policy page. Empty hides the Privacy button in Settings.</summary>
        public const string PrivacyUrl = "";

        public static string Key(Booster b) => b.ToString().ToLowerInvariant();

        // ----- Win streak and piggy bank -----
        public const int StreakCraneAt = 3;   // wins in a row for a free Crane each level
        public const int StreakMagnetAt = 5;  // ... and a free Magnet
        public const int PiggyPerWin = 20;    // x2 on hard levels
        public const int PiggyMax = 2000;
        public const int PiggyMin = 300;      // can be broken from here
        public const int StarterFromLevel = 3;
        public static readonly TimeSpan StarterDuration = TimeSpan.FromHours(24);

        // ----- Real-money packs (prices are samples: the stores show the real ones) -----
        public static readonly Pack Starter = new Pack
        {
            ProductId = "starter_pack", PriceEn = "$1.99", PriceFa = "59 هزار تومان", OldPriceEn = "$9.99", OldPriceFa = "299 هزار تومان", TagKey = "tag_off",
            Items = new[] { Item.Coins(1500), Item.Of(Booster.Crane, 3), Item.Of(Booster.Magnet, 3), Item.Of(Booster.Shuffle, 3) },
        };

        public static readonly Pack Piggy = new Pack { ProductId = "piggy_bank", PriceEn = "$2.99", PriceFa = "89 هزار تومان" };

        public static readonly Pack[] CoinPacks =
        {
            new Pack { ProductId = "coins_1000", PriceEn = "$0.99", PriceFa = "29 هزار تومان", Items = new[] { Item.Coins(1000) } },
            new Pack { ProductId = "coins_2500", PriceEn = "$1.99", PriceFa = "59 هزار تومان", TagKey = "tag_bonus", Items = new[] { Item.Coins(2500) } },
            new Pack { ProductId = "coins_6000", PriceEn = "$3.99", PriceFa = "119 هزار تومان", TagKey = "tag_popular", Items = new[] { Item.Coins(6000) } },
            new Pack { ProductId = "coins_15000", PriceEn = "$7.99", PriceFa = "239 هزار تومان", TagKey = "tag_best", Items = new[] { Item.Coins(15000) } },
        };

        public static readonly Pack BoosterBundle = new Pack
        {
            ProductId = "booster_bundle", PriceEn = "$2.99", PriceFa = "89 هزار تومان", TagKey = "tag_save",
            Items = new[] { Item.Coins(1000), Item.Of(Booster.Crane, 3), Item.Of(Booster.Magnet, 3), Item.Of(Booster.Shuffle, 3) },
        };

        /// <summary>Uses the existing "remove_ads" product id, so players who bought it keep it.</summary>
        public static readonly Pack NoAds = new Pack
        {
            ProductId = Store.RemoveAdsProductId, Consumable = false, PriceEn = "$4.99", PriceFa = "149 هزار تومان",
            Items = new[] { new Item(ItemKind.NoAds, 1), Item.Coins(1500), Item.Of(Booster.Crane, 2), Item.Of(Booster.Magnet, 2), Item.Of(Booster.Shuffle, 2) },
        };

        public static Pack[] AllPacks => new[] { Starter, Piggy, CoinPacks[0], CoinPacks[1], CoinPacks[2], CoinPacks[3], BoosterBundle, NoAds };

        // ----- Skins (coins) -----
        public static readonly Skin[] CrateSkins = { new Skin("wood", 0), new Skin("gift", 800), new Skin("metal", 1500), new Skin("candy", 2500) };
        public static readonly Skin[] TruckSkins = { new Skin("classic", 0), new Skin("racing", 1000), new Skin("icecream", 2000), new Skin("bigrig", 3500) };

        // ----- Offers -----

        /// <summary>Time left on the one-time starter pack (it starts at level 3 and lasts 24 hours).</summary>
        public static TimeSpan StarterLeft
        {
            get
            {
                if (Progress.StarterBought || Progress.Level < StarterFromLevel || !MonetizationServices.PurchasesEnabled) return TimeSpan.Zero;
                var start = Progress.StarterStart;
                if (start == DateTime.MinValue)
                {
                    start = DateTime.UtcNow;
                    Progress.StarterStart = start;
                }
                var left = start + StarterDuration - DateTime.UtcNow;
                return left > TimeSpan.Zero ? left : TimeSpan.Zero;
            }
        }

        public static bool StarterActive => StarterLeft > TimeSpan.Zero;
        public static bool ChestReady => Progress.ChestDay != Progress.Today;

        /// <summary>A random prize for the daily free chest.</summary>
        public static Item RollChest()
        {
            float r = UnityEngine.Random.value;
            if (r < 0.35f) return Item.Coins(50);
            if (r < 0.55f) return Item.Coins(120);
            if (r < 0.70f) return Item.Of(Booster.Crane, 1);
            if (r < 0.85f) return Item.Of(Booster.Magnet, 1);
            return Item.Of(Booster.Shuffle, 1);
        }

        public static string Clock(TimeSpan t)
        {
            string s = $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}";
            return Loc.Persian ? Loc.PersianDigits(s) : s;
        }

        public static string Number(int n) => Loc.Persian ? Loc.PersianDigits(n.ToString()) : n.ToString();
    }
}
