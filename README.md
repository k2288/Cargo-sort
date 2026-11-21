# Cargo Sort

A small hybrid-casual mobile puzzle game made in Unity. Tap a stack to send its top crate to the
truck of the same color. Crates with no matching truck wait on the dock. Fill every truck before
the dock runs out of space.

## Open and play

1. Open the folder in **Unity 6.3 LTS (6000.3)** with Unity Hub. Add the Android and/or iOS build module.
2. If Unity asks to enable the new Input System backend, say **Yes** (both input systems are supported).
3. On first open the project creates `Assets/Scenes/Main.unity`, adds it to Build Settings and sets
   portrait mode. You can rerun this from the menu **Cargo Sort > Setup Project**.
4. Set the Game view to a phone size (for example 1080x1920) and press **Play**.

Everything (board, trucks, UI) is built from code at runtime, so there are no prefabs or art to set up.

## How the game works

| Thing | Where |
| --- | --- |
| Puzzle rules (pure C#, no Unity) | `Assets/Scripts/Core/GameState.cs` |
| Level generator, solver, undo | `Assets/Scripts/Core/LevelGenerator.cs` |
| Drawing and animations | `Assets/Scripts/Game/BoardView.cs` |
| Top bar, boosters and popups | `Assets/Scripts/Game/Hud.cs` |
| Home screen | `Assets/Scripts/Game/HudHome.cs` |
| Shop | `Assets/Scripts/Game/HudShop.cs` |
| Booster prices, packs, skins, offers | `Assets/Scripts/Game/Economy.cs` |
| Crate and truck drawings (and skins) | `Assets/Scripts/Game/Art.cs` |
| Input, boosters, level flow | `Assets/Scripts/Game/GameController.cs` |
| Ads, purchases, saves | `Assets/Scripts/Game/Monetization.cs` |
| Tests (Window > General > Test Runner) | `Assets/Tests/EditMode` |

Levels are generated from the level number, so level 12 is the same on every phone. Each level is
checked by a solver before it is used, so every level can be beaten. Difficulty grows with more
colors, more trucks and a smaller dock; every 5th level is a hard level with one less dock slot.
Tune it in `LevelGenerator.ForLevel`.

Twists come in over time: "?" crates from level 11, the **conveyor belt** from level 15 (then every
3rd level), ice from level 21, **taped crates** from level 28 (two crates taped together move as
a pair and need room for both; Crane and Magnet cut the tape), **big trucks** (4 crates) from level 36,
**two-color trucks** (2 crates of each of two colors) from level 40, and **locked crates** from
level 48 (a padlock opens when the crate with the matching key leaves its stack; the Crane can't lift
a locked crate and Magnet skips it), and **bomb crates** from level 56 (move the crate out of its
stack before its tap counter runs out; each counter is the tap on which the solver moves that crate
plus 8, so levels stay winnable; after an explosion the revive gives +5 moves). On belt levels the dock becomes a moving belt with 2 more spots;
crates ride around and jump onto a truck of their color as they pass under it. Belt levels are
checked with dock rules, because on the belt a player can always wait for a crate to come around.

**Developer panel:** in the editor and in development builds, Settings has a **DEV** button to jump
to any level and add coins. It is not in release builds.

## Money

- **Boosters:** Undo, +1 Slot (max 2 per level), Crane (lift any crate), Magnet (fill the waiting
  trucks) and Shuffle. Each use takes a free use first (1 Undo per level, win-streak rewards), then
  one the player owns, then coins, then a rewarded ad.
- **Win streak:** 3 wins in a row give a free Crane each level, 5 add a free Magnet. Losing ends it.
- **Rewarded ads:** boosters, "+1 Slot & Continue", x3 level reward, x2 daily gift, the daily free gift.
- **Interstitial ads:** between levels, from level 4, every 3 levels.
- **Purchases** (ids in `Economy.cs`): `starter_pack` (once, 24 hours from level 3), `piggy_bank`
  (every win adds coins; break it from 300), `coins_1000`, `coins_2500`, `coins_6000`, `coins_15000`,
  `booster_bundle`, and `remove_ads` (the No Ads Pack: no interstitials plus boosters and coins).
  `remove_ads` is non-consumable; all others are consumable. The prices shown in the shop are in
  `Economy.cs` and match the Cafe Bazaar panel: update both together.
- **Coins:** crate skins, truck skins and themes.

In the editor, ads are always 1 second test ads. The real SDK code is in `Assets/Scripts/Monetization`
and switches on by itself once an SDK is in the project (`Assets/Editor/MonetizationDefines.cs`).
Until then the game compiles and uses the test versions. All keys go in
`Assets/Scripts/Monetization/AdsConfig.cs` (none of them are secret).

### Pick the store

Menu **Cargo Sort > Store > Cafe Bazaar / Myket / Google Play**, then **Cargo Sort > Build Android APK**.
The APK is named after the store, for example `Builds/Android/CargoSort-bazaar.apk`.

| Store | Ads | Purchases |
| --- | --- | --- |
| Cafe Bazaar | Tapsell Plus | Bazaar payments (Poolakey) |
| Myket | Tapsell Plus | Not wired yet, so paid offers are hidden |
| Google Play | Unity LevelPlay | Unity IAP |

### Cafe Bazaar and Myket

1. Sign up at [Tapsell](https://dashboard.tapsell.ir), add the app, and create one **Rewarded video** and one
   **Interstitial** zone. Put the app key and zone IDs in `AdsConfig.cs`.
2. Import the Tapsell Plus `.unitypackage` from
   [TapsellPlusSDK-UnityPlugin](https://github.com/tapsellorg/TapsellPlusSDK-UnityPlugin) (the `release` folder).
3. Sign up at the [Cafe Bazaar developer panel](https://pishkhan.cafebazaar.ir), add the app, create the in-app
   products listed under Money (same ids), and copy the app's RSA public key into `AdsConfig.cs`.
4. Import the Poolakey `.unitypackage` from [PoolakeyUnitySdk](https://github.com/cafebazaar/PoolakeyUnitySdk).
5. Bazaar and Myket don't accept apps that contain Google Play billing. If you installed Unity's
   **In-App Purchasing** package for the Google Play build, remove it before building for Bazaar or Myket.

### Google Play (only if you live or bank outside Iran)

1. Create a free account at [LevelPlay](https://platform.ironsrc.com), add an Android app, and create one
   **Rewarded** and one **Interstitial** ad unit. Put the app key and IDs in `AdsConfig.cs`.
2. In Package Manager install **Ads Mediation (LevelPlay)** 9+ and **In-App Purchasing** 5+.
3. In Google Play Console create the in-app products listed under Money (same ids).

### Building on GitHub

The **Build Android APK** workflow builds all three store APKs. Run it from the Actions tab or push a tag
like `v1.0`. It needs the repo secrets `UNITY_EMAIL`, `UNITY_PASSWORD` and `UNITY_LICENSE` (the text of your
`Unity_lic.ulf`). SDKs you imported as `.unitypackage` must be committed for CI to include them.

Then add analytics (level start, win, lose, ad watched) so we can tune difficulty from real data.
