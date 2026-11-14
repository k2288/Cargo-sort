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
            { "reset", ("Reset", "از نو") },
            { "shop", ("Shop", "فروشگاه") },
            { "undo", ("Undo ({0})", "برگشت ({0})") },
            { "undo_ad", ("Undo (AD)", "برگشت (تبلیغ)") },
            { "slot", ("+Slot (AD)", "+جا (تبلیغ)") },
            { "hint", ("Hint (AD)", "راهنما (تبلیغ)") },
            { "win_title", ("Level {0} complete!", "مرحله {0} تمام شد!") },
            { "reward", ("+{0} coins", "+{0} سکه") },
            { "claim", ("Claim", "دریافت") },
            { "claim_x3", ("x3 Claim (AD)", "دریافت ۳ برابر (تبلیغ)") },
            { "claim_x2", ("x2 Claim (AD)", "دریافت ۲ برابر (تبلیغ)") },
            { "lose_title", ("Dock is full!", "اسکله پر شد!") },
            { "revive", ("+1 Slot & Continue (AD)", "یک جای اضافه و ادامه (تبلیغ)") },
            { "retry", ("Retry", "دوباره") },
            { "daily_title", ("Daily gift - Day {0}", "هدیه روزانه - روز {0}") },
            { "daily_hint", ("Come back tomorrow for a bigger gift!", "فردا برگرد، هدیه بزرگ‌تری داری!") },
            { "shop_title", ("Shop", "فروشگاه") },
            { "free_coins", ("+{0} coins (AD)", "+{0} سکه (تبلیغ)") },
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
