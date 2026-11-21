using UnityEngine;

namespace CargoSort.Game
{
    /// <summary>Board color themes sold in the shop for coins.</summary>
    public sealed class Theme
    {
        public string NameKey;
        public int Price;
        public Color Background, Ground, Slot, TruckBed;
    }

    public static class Themes
    {
        public static readonly Theme[] All =
        {
            new Theme { NameKey = "theme_night", Price = 0,
                Background = new Color32(0x58, 0xB4, 0xF2, 0xFF), Ground = new Color32(0xB9, 0x81, 0x4C, 0xFF),
                Slot = new Color32(0x3B, 0x78, 0xAD, 0xFF), TruckBed = new Color32(0x55, 0x64, 0x8A, 0xFF) },
            new Theme { NameKey = "theme_sunny", Price = 300,
                Background = new Color32(0x3F, 0xA7, 0xD6, 0xFF), Ground = new Color32(0x2E, 0x86, 0xB0, 0xFF),
                Slot = new Color32(0x1F, 0x5F, 0x80, 0xFF), TruckBed = new Color32(0x55, 0x60, 0x70, 0xFF) },
            new Theme { NameKey = "theme_snow", Price = 600,
                Background = new Color32(0x8E, 0xA9, 0xC4, 0xFF), Ground = new Color32(0xC9, 0xD8, 0xE6, 0xFF),
                Slot = new Color32(0x6E, 0x88, 0xA3, 0xFF), TruckBed = new Color32(0x5A, 0x64, 0x72, 0xFF) },
            new Theme { NameKey = "theme_desert", Price = 900,
                Background = new Color32(0xC9, 0x8B, 0x4E, 0xFF), Ground = new Color32(0xA8, 0x6E, 0x38, 0xFF),
                Slot = new Color32(0x7A, 0x4E, 0x26, 0xFF), TruckBed = new Color32(0x5E, 0x55, 0x4C, 0xFF) },
            new Theme { NameKey = "theme_neon", Price = 1500,
                Background = new Color32(0x1A, 0x0F, 0x2E, 0xFF), Ground = new Color32(0x2E, 0x1A, 0x52, 0xFF),
                Slot = new Color32(0x12, 0x09, 0x22, 0xFF), TruckBed = new Color32(0x3D, 0x2B, 0x5E, 0xFF) },
        };

        public static Theme Current => All[Mathf.Clamp(Progress.Theme, 0, All.Length - 1)];
    }
}
