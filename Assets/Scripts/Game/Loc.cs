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
            { "challenge", ("Challenge", "چالش") },
            { "undo_coins", ("Undo\n{0} coins", "برگشت\n{0} سکه") },
            { "slot_coins", ("+Slot\n{0} coins", "+جا\n{0} سکه") },
            { "hint_coins", ("Hint\n{0} coins", "راهنما\n{0} سکه") },
            { "hard_level", ("Level {0} - HARD", "مرحله {0} - سخت") },
            { "hard_start", ("Hard level! Double coins", "مرحله سخت! سکه دو برابر") },
            { "twist_hidden", ("New: ? crates show their color on top", "جدید: رنگ جعبه ؟ بالای ستون معلوم می‌شود") },
            { "twist_frozen", ("New: ice melts as you tap other columns", "جدید: یخ با ضربه به ستون‌های دیگر آب می‌شود") },
            { "frozen", ("Frozen! Tap other columns to melt it", "یخ زده! به ستون‌های دیگر ضربه بزن") },
            { "next_short", ("Next", "بعدی") },
            { "levels", ("Levels", "مراحل") },
            { "challenge_level", ("Daily challenge", "چالش روزانه") },
            { "challenge_win", ("Challenge complete!", "چالش امروز تمام شد!") },
            { "challenge_done", ("Done for today! New challenge tomorrow", "چالش امروز انجام شد! فردا چالش جدید") },
            { "challenge_start", ("Hard level, big prize: {0} coins", "مرحله سخت، جایزه بزرگ: {0} سکه") },
            { "sound_on", ("Sound: On", "صدا: روشن") },
            { "sound_off", ("Sound: Off", "صدا: خاموش") },
            { "vibration_on", ("Vibration: On", "لرزش: روشن") },
            { "vibration_off", ("Vibration: Off", "لرزش: خاموش") },
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
