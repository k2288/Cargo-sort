using System.Collections.Generic;
using CargoSort.Core;
using UnityEngine;

namespace CargoSort.Game
{
    /// <summary>English and Persian texts. Persian is shaped for Unity's UI Text by <see cref="PersianShaper"/>.</summary>
    public static class Loc
    {
        private static readonly Dictionary<string, (string en, string fa)> Strings = new Dictionary<string, (string, string)>
        {
            { "level", ("Level {0}", "مرحله {0}") },
            { "trucks", ("Trucks {0}/{1}", "کامیون {0}/{1}") },
            { "coins", ("{0} coins", "{0} سکه") },
            { "shop", ("Shop", "فروشگاه") },
            { "win_title", ("Level {0} complete!", "مرحله {0} تمام شد!") },
            { "reward", ("+{0} coins", "+{0} سکه") },
            { "claim", ("Claim", "دریافت") },
            { "lose_title", ("Dock is full!", "اسکله پر شد!") },
            { "retry", ("Retry", "دوباره") },
            { "daily_title", ("Daily gift - Day {0}", "هدیه روزانه - روز {0}") },
            { "daily_hint", ("Come back tomorrow for a bigger gift!", "فردا برگرد، هدیه بزرگ‌تری داری!") },
            { "shop_title", ("Shop", "فروشگاه") },
            { "remove_ads", ("Remove ads", "حذف تبلیغات") },
            { "use", ("Use", "انتخاب") },
            { "selected", ("Selected", "انتخاب شده") },
            { "close", ("Close", "بستن") },
            { "other_language", ("فارسی", "English") },
            { "no_ad", ("No ad available, try again soon", "الان تبلیغی نیست، کمی بعد امتحان کن") },
            { "dock_max", ("Dock is at max size", "اسکله به بیشترین اندازه رسیده") },
            { "ads_removed", ("Ads removed. Thank you!", "تبلیغات حذف شد. ممنون!") },
            { "purchase_failed", ("Purchase didn't go through", "خرید انجام نشد") },
            { "not_enough", ("Not enough coins", "سکه کافی نداری") },
            { "theme_night", ("Night Dock", "اسکله شب") },
            { "theme_sunny", ("Sunny Port", "بندر آفتابی") },
            { "theme_snow", ("Snowy Yard", "انبار برفی") },
            { "theme_desert", ("Desert Depot", "انبار کویری") },
            { "theme_neon", ("Neon City", "شهر نئون") },
            { "challenge", ("Challenge", "چالش") },
            { "hard_level", ("Level {0} - HARD", "مرحله {0} - سخت") },
            { "hard_start", ("Hard level! Double coins", "مرحله سخت! سکه دو برابر") },
            { "twist_hidden", ("New: ? crates show their color on top", "جدید: رنگ جعبه ؟ بالای ستون معلوم می‌شود") },
            { "twist_frozen", ("New: ice melts as you tap other columns", "جدید: یخ با ضربه به ستون‌های دیگر آب می‌شود") },
            { "frozen", ("Frozen! Tap other columns to melt it", "یخ زده! به ستون‌های دیگر ضربه بزن") },
            { "next_short", ("Next", "بعدی") },
            { "tut_truck", ("Tap this column: its top crate\njumps onto the matching truck", "روی این ستون بزن: جعبه بالایی\nروی کامیون هم‌رنگ می‌پرد") },
            { "tut_dock", ("No truck for this color? It waits on the dock.\nDon't let the dock fill up!", "کامیون هم‌رنگ نیست؟ جعبه روی اسکله می‌ماند.\nنگذار اسکله پر شود!") },
            { "challenge_level", ("Daily challenge", "چالش روزانه") },
            { "challenge_win", ("Challenge complete!", "چالش امروز تمام شد!") },
            { "challenge_done", ("Done for today! New challenge tomorrow", "چالش امروز انجام شد! فردا چالش جدید") },
            { "challenge_start", ("Hard level, big prize: {0} coins", "مرحله سخت، جایزه بزرگ: {0} سکه") },
            { "claim_x3", ("Claim x3", "دریافت ۳ برابر") },
            { "claim_x2", ("Claim x2", "دریافت ۲ برابر") },
            { "watch_ad", ("WATCH AD", "با تبلیغ") },
            { "revive", ("+1 Slot & Continue", "یک جای اضافه و ادامه") },
            { "lose_text", ("No crate can move.", "هیچ جعبه‌ای حرکت نمی‌کند.") },
            { "lose_undo", ("Undo", "برگشت") },
            { "undo", ("Undo", "برگشت") },
            { "slot", ("+1 Slot", "+۱ جا") },
            { "crane", ("Crane", "جرثقیل") },
            { "magnet", ("Magnet", "آهنربا") },
            { "shuffle", ("Shuffle", "جابه‌جایی") },
            { "crane_tip", ("Crane: tap any crate to lift it", "جرثقیل: روی هر جعبه‌ای بزن تا بلندش کند") },
            { "magnet_none", ("No crate matches the trucks", "هیچ جعبه‌ای به کامیون‌ها نمی‌خورد") },
            { "streak_bonus", ("Win streak {0}! Free boosters this level", "{0} برد پشت هم! بوستر رایگان در این مرحله") },
            { "streak_warn", ("You will lose your win streak of {0}!", "رکورد {0} برد پشت همت از دست می‌رود!") },
            { "win_streak", ("Streak {0}", "{0} برد پشت هم") },
            { "piggy_add", ("+{0} in piggy", "+{0} در قلک") },
            { "home", ("Home", "خانه") },
            { "belt_intro", ("New: conveyor belt! Crates ride around and jump onto their truck", "جدید: نوار نقاله! جعبه‌ها می‌چرخند و روی کامیون همرنگ می‌پرند") },
            { "tape_intro", ("New: taped crates move together as a pair", "جدید: جعبه‌های چسب‌خورده با هم جابه‌جا می‌شوند") },
            { "tarp_intro", ("New: a covered stack opens when the stack its arrow points to is empty", "جدید: ستون پوشیده وقتی ستونی که فلش نشان می‌دهد خالی شود باز می‌شود") },
            { "covered", ("Covered! Empty the stack the arrow points to", "پوشیده! ستونی که فلش نشان می‌دهد را خالی کن") },
            { "bomb_intro", ("New: move the bomb crate out of its stack before its counter hits 0", "جدید: جعبه بمب را قبل از صفر شدن شمارنده از ستونش بردار") },
            { "bomb_title", ("Boom!", "بوم!") },
            { "bomb_text", ("A bomb crate ran out of moves.", "حرکت‌های جعبه بمب تمام شد.") },
            { "bomb_revive", ("+5 Moves & Continue", "۵ حرکت بیشتر و ادامه") },
            { "lock_intro", ("New: locked crates open when their key crate leaves its stack", "جدید: جعبه قفل با برداشتن جعبه کلیددار باز می‌شود") },
            { "big_intro", ("New: big trucks hold 4 crates", "جدید: کامیون بزرگ ۴ جعبه جا دارد") },
            { "two_intro", ("New: two-color trucks take 2 of each color", "جدید: کامیون دورنگ از هر رنگ ۲ جعبه می‌برد") },
            { "dev_title", ("Developer", "توسعه‌دهنده") },
            { "dev_go", ("Go to level {0}", "برو به مرحله {0}") },
            { "dev_note", ("Only in the editor and development builds", "فقط در ادیتور و نسخه‌های توسعه") },
            { "settings", ("Settings", "تنظیمات") },
            { "sound", ("Sound", "صدا") },
            { "vibration", ("Vibration", "لرزش") },
            { "language", ("Language", "زبان") },
            { "restart", ("Restart", "از نو") },
            { "restore", ("Restore", "بازیابی خرید") },
            { "restore_done", ("Purchases restored", "خریدها بازیابی شد") },
            { "privacy", ("Privacy", "حریم خصوصی") },
            { "play_hard", ("HARD LEVEL", "مرحله سخت") },
            { "chest", ("Free gift", "هدیه رایگان") },
            { "chest_sub", ("Open one gift every day", "هر روز یک هدیه باز کن") },
            { "chest_tomorrow", ("Tomorrow", "فردا") },
            { "open", ("Open", "باز کن") },
            { "starter_pack", ("Starter Pack", "بسته شروع") },
            { "ends_in", ("Ends in {0}", "تا {0} دیگر") },
            { "piggy_bank", ("Piggy Bank", "قلک") },
            { "piggy_sub", ("Every win drops coins in. Break it to take them all!", "هر برد سکه به قلک می‌اندازد. بشکن و همه را بردار!") },
            { "piggy_break", ("Break {0}", "بشکن {0}") },
            { "piggy_min", ("Fill to {0}", "تا {0} پر کن") },
            { "coins_sect", ("Coins", "سکه") },
            { "bundles", ("Bundles", "بسته‌ها") },
            { "crates", ("Crate skins", "پوسته جعبه") },
            { "trucks_sect", ("Truck skins", "پوسته کامیون") },
            { "themes", ("Themes", "تم‌ها") },
            { "tag_bonus", ("+25% BONUS", "۲۵٪ بیشتر") },
            { "tag_popular", ("POPULAR", "محبوب") },
            { "tag_best", ("BEST VALUE", "به‌صرفه‌ترین") },
            { "tag_save", ("SAVE 70%", "۷۰٪ تخفیف") },
            { "tag_off", ("-80%", "۸۰٪ تخفیف") },
            { "booster_bundle", ("Booster Bundle", "بسته بوستر") },
            { "booster_bundle_sub", ("3 of each new booster + coins", "از هر بوستر جدید ۳ تا + سکه") },
            { "noads_pack", ("No Ads Pack", "بسته بدون تبلیغ") },
            { "noads_pack_sub", ("No ads between levels + boosters + coins", "بدون تبلیغ بین مرحله‌ها + بوستر + سکه") },
            { "purchased", ("Thank you!", "ممنون از خریدت!") },
            { "noads_item", ("No ads", "بدون تبلیغ") },
            { "skin_wood", ("Wooden", "چوبی") },
            { "skin_gift", ("Gift box", "جعبه کادو") },
            { "skin_metal", ("Steel", "فلزی") },
            { "skin_candy", ("Candy", "آبنباتی") },
            { "truck_classic", ("Classic", "کلاسیک") },
            { "truck_racing", ("Racer", "مسابقه‌ای") },
            { "truck_icecream", ("Ice cream", "بستنی‌فروش") },
            { "truck_bigrig", ("Big rig", "تریلی") },
            { "skin_sub", ("Shows on every move", "در هر حرکت دیده می‌شود") },
            { "owned", ("Owned", "داری") },
            { "theme_sub", ("New look for your harbor", "ظاهر تازه برای بندرت") },
            { "notif_channel", ("Daily reminders", "یادآوری روزانه") },
            { "notif_gift_title", ("Your daily gift is ready 🎁", "هدیه روزانه‌ات آماده است 🎁") },
            { "notif_gift_text", ("Claim your coins and beat today's challenge!", "سکه‌هایت را بگیر و چالش امروز را ببر!") },
            { "notif_miss_title", ("The trucks are waiting 🚚", "کامیون‌ها منتظرت هستند 🚚") },
            { "notif_miss_text", ("Your dock is piling up. Come sort some crates!", "اسکله شلوغ شده. بیا جعبه‌ها را مرتب کن!") },
        };

        public static bool Persian => Progress.Language == "fa";

        /// <summary>Text for a key in the current language, formatted and ready to show.</summary>
        public static string T(string key, params object[] args)
        {
            if (!Strings.TryGetValue(key, out var pair)) return key;
            if (!Persian) return args.Length == 0 ? pair.en : string.Format(pair.en, args);

            var faArgs = new object[args.Length];
            for (int i = 0; i < args.Length; i++) faArgs[i] = PersianDigits(args[i]?.ToString() ?? "");
            string text = args.Length == 0 ? pair.fa : string.Format(pair.fa, faArgs);
            return PersianShaper.Fix(text);
        }

        /// <summary>Text without RTL shaping, for places that draw Persian themselves (Android notifications).</summary>
        public static string Raw(string key)
        {
            if (!Strings.TryGetValue(key, out var pair)) return key;
            return Persian ? pair.fa : pair.en;
        }

        /// <summary>The language the game starts in: Persian for Bazaar and Myket builds, English otherwise.</summary>
        public static string DefaultLanguage
        {
            get
            {
#if CARGO_STORE_BAZAAR || CARGO_STORE_MYKET
                return "fa";
#else
                return "en";
#endif
            }
        }

        public static string PersianDigits(string s)
        {
            var chars = s.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
                if (chars[i] >= '0' && chars[i] <= '9') chars[i] = (char)('۰' + (chars[i] - '0'));
            return new string(chars);
        }
    }
}
