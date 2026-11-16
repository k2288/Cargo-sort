using UnityEngine;

namespace CargoSort.Game
{
    /// <summary>
    /// The port-building meta: coins from levels build the six pieces of a port. A finished port pays a bonus
    /// and the next, more expensive port opens. Progress is saved in PlayerPrefs.
    /// </summary>
    public static class Port
    {
        public const int Items = 6;

        // Build order on screen: pier, warehouse, crane, truck, lighthouse, ship.
        public static readonly string[] ItemKeys = { "pi_pier", "pi_warehouse", "pi_crane", "pi_truck", "pi_lighthouse", "pi_ship" };
        private static readonly int[] BaseCost = { 60, 90, 120, 150, 190, 240 };

        public static readonly string[] PortKeys = { "port_1", "port_2", "port_3", "port_4", "port_5", "port_6" };

        // Sky and water colors for each port, so every new port looks different.
        private static readonly Color32[] Skies =
        {
            new Color32(0x8F, 0xD3, 0xF4, 0xFF), new Color32(0xFF, 0xC8, 0x8A, 0xFF), new Color32(0xA8, 0xE6, 0xCF, 0xFF),
            new Color32(0xC7, 0xB8, 0xF2, 0xFF), new Color32(0xF7, 0xA8, 0xB8, 0xFF), new Color32(0x2C, 0x3E, 0x6B, 0xFF),
        };
        private static readonly Color32[] Waters =
        {
            new Color32(0x2E, 0x86, 0xC8, 0xFF), new Color32(0x3B, 0x8E, 0x9E, 0xFF), new Color32(0x1F, 0x9E, 0x9A, 0xFF),
            new Color32(0x4A, 0x6F, 0xC9, 0xFF), new Color32(0x3A, 0x7B, 0xB8, 0xFF), new Color32(0x16, 0x4A, 0x7A, 0xFF),
        };

        public static int Index
        {
            get => PlayerPrefs.GetInt("port_index", 0);
            private set { PlayerPrefs.SetInt("port_index", value); PlayerPrefs.Save(); }
        }

        private static int BuiltMask
        {
            get => PlayerPrefs.GetInt("port_built", 0);
            set { PlayerPrefs.SetInt("port_built", value); PlayerPrefs.Save(); }
        }

        public static bool IsBuilt(int item) => (BuiltMask & (1 << item)) != 0;

        public static int BuiltCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Items; i++) if (IsBuilt(i)) n++;
                return n;
            }
        }

        public static bool Complete => BuiltCount == Items;

        /// <summary>Price of an item in the current port. Each port costs 50% more than the one before.</summary>
        public static int Cost(int item) => Mathf.RoundToInt(BaseCost[item] * (1f + 0.5f * Index) / 10f) * 10;

        /// <summary>Bonus coins for finishing the current port.</summary>
        public static int CompletionReward => 100 + 50 * Index;

        /// <summary>Cheapest item still to build, or -1 when the port is complete.</summary>
        public static int CheapestLeft
        {
            get
            {
                int best = -1;
                for (int i = 0; i < Items; i++)
                    if (!IsBuilt(i) && (best < 0 || Cost(i) < Cost(best))) best = i;
                return best;
            }
        }

        public static bool CanBuildSomething(int coins)
        {
            int i = CheapestLeft;
            return i >= 0 && coins >= Cost(i);
        }

        public static void Build(int item) => BuiltMask |= 1 << item;

        /// <summary>Moves on to the next port (call after the completion reward is paid).</summary>
        public static void NextPort()
        {
            BuiltMask = 0;
            Index = Index + 1;
        }

        /// <summary>Port name; after the last named port they are numbered ("Mega Port 2", ...).</summary>
        public static string Name
        {
            get
            {
                int i = Index;
                if (i < PortKeys.Length) return Loc.T(PortKeys[i]);
                return Loc.T("port_numbered", Loc.Raw(PortKeys[PortKeys.Length - 1]), i - PortKeys.Length + 2);
            }
        }

        /// <summary>Unshaped name, for use as an argument inside another Loc.T text.</summary>
        public static string RawName
        {
            get
            {
                int i = Index;
                if (i < PortKeys.Length) return Loc.Raw(PortKeys[i]);
                return Loc.Raw(PortKeys[PortKeys.Length - 1]) + " " + (i - PortKeys.Length + 2);
            }
        }

        public static Color Sky => Skies[Index % Skies.Length];
        public static Color Water => Waters[Index % Waters.Length];
    }
}
