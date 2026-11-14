using System;
using UnityEngine;
#if UNITY_ANDROID
using Unity.Notifications.Android;
#endif

namespace CargoSort.Game
{
    /// <summary>
    /// Local reminder notifications (no server needed): "your daily gift is ready" the next evening,
    /// and "we miss you" after a few days away. They are rescheduled every time the game goes to the background.
    /// </summary>
    public static class Reminders
    {
        private const string Channel = "daily";
        private const int ReminderHour = 19;
        private const int MissYouDays = 3;

#if UNITY_ANDROID
        private static bool registered;
        private static PermissionRequest permission;
#endif

        /// <summary>Asks for the Android 13+ notification permission (once; Android remembers the answer).</summary>
        public static void AskPermission()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                Register();
                if (permission == null && AndroidNotificationCenter.UserPermissionToPost == PermissionStatus.NotRequested)
                    permission = new PermissionRequest();
            }
            catch (Exception e) { Debug.LogWarning("Notifications: " + e.Message); }
#endif
        }

        /// <summary>Called when the player leaves the game.</summary>
        public static void Schedule()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                Register();
                AndroidNotificationCenter.CancelAllScheduledNotifications();
                var now = DateTime.Now;

                var gift = now.Date.AddDays(1).AddHours(ReminderHour);
                Send(Loc.Raw("notif_gift_title"), Loc.Raw("notif_gift_text"), gift);

                var missYou = now.Date.AddDays(MissYouDays).AddHours(ReminderHour);
                Send(Loc.Raw("notif_miss_title"), Loc.Raw("notif_miss_text"), missYou);
            }
            catch (Exception e) { Debug.LogWarning("Notifications: " + e.Message); }
#endif
        }

        /// <summary>Called when the player comes back: clear reminders that are no longer needed.</summary>
        public static void Clear()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                Register();
                AndroidNotificationCenter.CancelAllNotifications();
            }
            catch (Exception e) { Debug.LogWarning("Notifications: " + e.Message); }
#endif
        }

#if UNITY_ANDROID
        private static void Register()
        {
            if (registered) return;
            registered = true;
            AndroidNotificationCenter.RegisterNotificationChannel(new AndroidNotificationChannel
            {
                Id = Channel,
                Name = Loc.Raw("notif_channel"),
                Description = Loc.Raw("notif_channel"),
                Importance = Importance.Default,
            });
        }

        private static void Send(string title, string text, DateTime when)
        {
            AndroidNotificationCenter.SendNotification(new AndroidNotification
            {
                Title = title,
                Text = text,
                FireTime = when,
            }, Channel);
        }
#endif
    }
}
