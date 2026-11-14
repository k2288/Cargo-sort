using System;
using UnityEngine;

namespace CargoSort.Game
{
    /// <summary>
    /// Sound effects made in code (no audio files to import) and short phone vibrations.
    /// Both can be turned off in the shop.
    /// </summary>
    public static class Feedback
    {
        private const int Rate = 44100;
        private static AudioSource source;
        private static AudioClip[] pops;
        private static AudioClip thud, horn, buzz, win, lose, click, coin;

        public static void Init(GameObject host)
        {
            if (source != null) return;
            source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;

            // Each crate loaded into a truck plays a higher note: C E G C E G.
            float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f, 1318.5f, 1568f };
            pops = new AudioClip[notes.Length];
            for (int i = 0; i < notes.Length; i++)
            {
                var b = new float[(int)(Rate * 0.11f)];
                Tone(b, 0, 0.11f, notes[i] * 1.25f, notes[i], Wave.Sine, 0.55f, 0.004f);
                Tone(b, 0, 0.08f, notes[i] * 2f, notes[i] * 2f, Wave.Sine, 0.12f, 0.004f);
                pops[i] = Clip("pop" + i, b);
            }

            var t = new float[(int)(Rate * 0.14f)];
            Tone(t, 0, 0.14f, 170, 70, Wave.Sine, 0.7f, 0.002f);
            Noise(t, 0, 0.05f, 0.18f, 3);
            thud = Clip("thud", t);

            var h = new float[(int)(Rate * 0.42f)];
            Tone(h, 0, 0.18f, 330, 330, Wave.Horn, 0.28f, 0.02f);
            Tone(h, 0, 0.18f, 415, 415, Wave.Horn, 0.22f, 0.02f);
            Tone(h, 0.2f, 0.22f, 330, 326, Wave.Horn, 0.28f, 0.02f);
            Tone(h, 0.2f, 0.22f, 415, 410, Wave.Horn, 0.22f, 0.02f);
            horn = Clip("horn", h);

            var z = new float[(int)(Rate * 0.16f)];
            Tone(z, 0, 0.16f, 120, 100, Wave.Square, 0.22f, 0.004f);
            buzz = Clip("buzz", z);

            float[] fanfare = { 523.25f, 659.25f, 783.99f, 1046.5f };
            var w = new float[(int)(Rate * 0.8f)];
            for (int i = 0; i < fanfare.Length; i++)
            {
                float len = i == fanfare.Length - 1 ? 0.45f : 0.12f;
                Tone(w, i * 0.11f, len, fanfare[i], fanfare[i], Wave.Triangle, 0.45f, 0.006f);
                Tone(w, i * 0.11f, len, fanfare[i] * 2, fanfare[i] * 2, Wave.Sine, 0.1f, 0.006f);
            }
            win = Clip("win", w);

            float[] sad = { 392f, 329.63f, 261.63f };
            var l = new float[(int)(Rate * 0.95f)];
            for (int i = 0; i < sad.Length; i++)
            {
                float len = i == sad.Length - 1 ? 0.5f : 0.2f;
                Tone(l, i * 0.22f, len, sad[i], sad[i] * (i == sad.Length - 1 ? 0.94f : 1f), Wave.Triangle, 0.45f, 0.01f);
            }
            lose = Clip("lose", l);

            var c = new float[(int)(Rate * 0.04f)];
            Tone(c, 0, 0.04f, 1400, 900, Wave.Sine, 0.3f, 0.001f);
            click = Clip("click", c);

            var k = new float[(int)(Rate * 0.25f)];
            Tone(k, 0, 0.08f, 1318.5f, 1318.5f, Wave.Sine, 0.35f, 0.002f);
            Tone(k, 0.07f, 0.18f, 1975.5f, 1975.5f, Wave.Sine, 0.35f, 0.002f);
            coin = Clip("coin", k);
        }

        // ---------- Events ----------

        public static void CrateLoaded(int crateIndex) => Play(pops?[Mathf.Clamp(crateIndex, 0, pops.Length - 1)]);
        public static void CrateParked() { Play(thud); Vibrate(12); }
        public static void TruckLeft() { Play(horn); Vibrate(25); }
        public static void Invalid() { Play(buzz); Vibrate(40); }
        public static void Won() { Play(win); Vibrate(60); }
        public static void Lost() { Play(lose); Vibrate(120); }
        public static void Click() => Play(click, 0.8f);
        public static void Revealed() => Play(pops?[5], 0.5f);
        public static void IceMelted() { Play(thud); Play(pops?[3], 0.6f); Vibrate(20); }
        public static void Coins() => Play(coin);

        private static void Play(AudioClip clip, float volume = 1f)
        {
            if (clip == null || source == null || !Progress.Sound) return;
            source.PlayOneShot(clip, volume);
        }

        // ---------- Vibration ----------

#if UNITY_ANDROID && !UNITY_EDITOR
        private static AndroidJavaObject vibrator;
        private static bool vibratorLooked;
#endif

        public static void Vibrate(long ms)
        {
            if (!Progress.Vibration) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                if (!vibratorLooked)
                {
                    vibratorLooked = true;
                    using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                        vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                }
                vibrator?.Call("vibrate", ms);
            }
            catch (Exception)
            {
                vibrator = null;
            }
#elif UNITY_IOS && !UNITY_EDITOR
            if (ms >= 60) Handheld.Vibrate();
#endif
            // Never runs; it makes Unity add the VIBRATE permission to the Android manifest.
            if (ms < 0) Handheld.Vibrate();
        }

        // ---------- Synth ----------

        private enum Wave { Sine, Square, Triangle, Horn }

        private static AudioClip Clip(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static void Tone(float[] buf, float start, float duration, float f0, float f1, Wave wave, float volume, float attack)
        {
            int s0 = (int)(start * Rate);
            int n = (int)(duration * Rate);
            double phase = 0;
            for (int i = 0; i < n && s0 + i < buf.Length; i++)
            {
                float t = i / (float)Rate;
                float k = i / (float)n;
                float freq = Mathf.Lerp(f0, f1, k);
                phase += 2 * Math.PI * freq / Rate;
                float p = (float)(phase % (2 * Math.PI));
                float v;
                switch (wave)
                {
                    case Wave.Square: v = p < Math.PI ? 0.6f : -0.6f; break;
                    case Wave.Triangle: v = 1f - 4f * Mathf.Abs(p / (2 * Mathf.PI) - 0.5f); break;
                    case Wave.Horn: v = Mathf.Sin(p) * 0.6f + Mathf.Sin(2 * p) * 0.3f + Mathf.Sin(3 * p) * 0.15f; break;
                    default: v = Mathf.Sin(p); break;
                }
                float env = Mathf.Min(1f, t / Mathf.Max(attack, 0.0001f)) * Mathf.Pow(1f - k, 1.6f);
                buf[s0 + i] += v * env * volume;
            }
        }

        private static void Noise(float[] buf, float start, float duration, float volume, int seed)
        {
            var rng = new System.Random(seed);
            int s0 = (int)(start * Rate);
            int n = (int)(duration * Rate);
            for (int i = 0; i < n && s0 + i < buf.Length; i++)
                buf[s0 + i] += (float)(rng.NextDouble() * 2 - 1) * volume * (1f - i / (float)n);
        }
    }
}
