using System.Linq;
using UnityEditor;
using UnityEditor.Build;

namespace CargoSort.EditorTools
{
    /// <summary>Which Android store the next build is for. Menu: Cargo Sort > Store.</summary>
    public static class StoreTarget
    {
        public const string Bazaar = "CARGO_STORE_BAZAAR";
        public const string Myket = "CARGO_STORE_MYKET";

        private const string GoogleMenu = "Cargo Sort/Store/Google Play";
        private const string BazaarMenu = "Cargo Sort/Store/Cafe Bazaar";
        private const string MyketMenu = "Cargo Sort/Store/Myket";

        public static string Current
        {
            get
            {
                var d = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android);
                if (d.Contains(Bazaar)) return "bazaar";
                if (d.Contains(Myket)) return "myket";
                return "googleplay";
            }
        }

        [MenuItem(GoogleMenu)] private static void SetGoogle() => Set(null);
        [MenuItem(BazaarMenu)] private static void SetBazaar() => Set(Bazaar);
        [MenuItem(MyketMenu)] private static void SetMyket() => Set(Myket);

        [MenuItem(GoogleMenu, true)] private static bool CheckGoogle() { Menu.SetChecked(GoogleMenu, Current == "googleplay"); return true; }
        [MenuItem(BazaarMenu, true)] private static bool CheckBazaar() { Menu.SetChecked(BazaarMenu, Current == "bazaar"); return true; }
        [MenuItem(MyketMenu, true)] private static bool CheckMyket() { Menu.SetChecked(MyketMenu, Current == "myket"); return true; }

        public static void Set(string storeDefine)
        {
            var defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android).Split(';')
                .Where(d => !string.IsNullOrWhiteSpace(d) && d != Bazaar && d != Myket).ToList();
            if (storeDefine != null) defines.Add(storeDefine);
            PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Android, string.Join(";", defines));
            UnityEngine.Debug.Log("[Cargo Sort] Building for " + Current + " now.");
        }
    }
}
