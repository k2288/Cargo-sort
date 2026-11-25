using UnityEngine;

namespace CargoSort.Game
{
    /// <summary>Opens this game's comment / rating page in the store it was built for (Bazaar, Myket or Google Play).</summary>
    public static class StoreReview
    {
        public static void Open()
        {
            string id = Application.identifier;
#if CARGO_STORE_BAZAAR
            if (!StartIntent("android.intent.action.EDIT", "bazaar://details?id=" + id, "com.farsitel.bazaar"))
                Application.OpenURL("https://cafebazaar.ir/app/" + id);
#elif CARGO_STORE_MYKET
            if (!StartIntent("android.intent.action.VIEW", "myket://comment?id=" + id, "ir.mservices.market"))
                Application.OpenURL("https://myket.ir/app/" + id);
#else
            if (!StartIntent("android.intent.action.VIEW", "market://details?id=" + id, "com.android.vending"))
                Application.OpenURL("https://play.google.com/store/apps/details?id=" + id);
#endif
        }

        /// <summary>Starts an Android intent for the store app. False if it is not installed (or not on Android).</summary>
        private static bool StartIntent(string action, string uri, string package)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var uriClass = new AndroidJavaClass("android.net.Uri"))
                using (var parsed = uriClass.CallStatic<AndroidJavaObject>("parse", uri))
                using (var intent = new AndroidJavaObject("android.content.Intent", action, parsed))
                {
                    intent.Call<AndroidJavaObject>("setPackage", package);
                    intent.Call<AndroidJavaObject>("addFlags", 0x10000000); // FLAG_ACTIVITY_NEW_TASK
                    activity.Call("startActivity", intent);
                    return true;
                }
            }
            catch (System.Exception e)
            {
                Debug.Log("Store app not available: " + e.Message);
                return false;
            }
#else
            return false;
#endif
        }
    }
}
