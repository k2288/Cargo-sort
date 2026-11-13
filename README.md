# Cargo Sort

A small hybrid-casual mobile puzzle game made in Unity. Tap a stack to send its top crate to the
truck of the same color. Crates with no matching truck wait on the dock. Fill every truck before
the dock runs out of space.

## Open and play

1. Open the folder in **Unity 6 (6000.0 LTS)** with Unity Hub. Add the Android and/or iOS build module.
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
| Buttons and panels | `Assets/Scripts/Game/Hud.cs` |
| Input, boosters, level flow | `Assets/Scripts/Game/GameController.cs` |
| Ads, remove-ads purchase, saves | `Assets/Scripts/Game/Monetization.cs` |
| Tests (Window > General > Test Runner) | `Assets/Tests/EditMode` |

Levels are generated from the level number, so level 12 is the same on every phone. Each level is
checked by a solver before it is used, so every level can be beaten. Difficulty grows with more
colors, more trucks and a smaller dock; every 5th level is a hard level with one less dock slot.
Tune it in `LevelGenerator.ForLevel`.

## Money

- **Rewarded ads:** Undo (after 1 free per level), +1 dock slot (max 2 per level), Hint, and
  "+1 Slot & Continue" when you lose.
- **Interstitial ads:** between levels, from level 4, every 3 levels.
- **Remove ads purchase:** stops interstitials. Rewarded ads stay because players choose them.

In the editor, ads are always 1 second test ads. The real SDK code is in `Assets/Scripts/Monetization`
and switches on by itself once an SDK is in the project (`Assets/Editor/MonetizationDefines.cs`).
Until then the game compiles and uses the test versions. All keys go in
`Assets/Scripts/Monetization/AdsConfig.cs` (none of them are secret).

### Pick the store

Menu **Cargo Sort > Store > Cafe Bazaar / Myket / Google Play**, then **Cargo Sort > Build Android APK**.
The APK is named after the store, for example `Builds/Android/CargoSort-bazaar.apk`.

| Store | Ads | No Ads purchase |
| --- | --- | --- |
| Cafe Bazaar | Tapsell Plus | Bazaar payments (Poolakey) |
| Myket | Tapsell Plus | Not wired yet, so the button is hidden |
| Google Play | Unity LevelPlay | Unity IAP |

### Cafe Bazaar and Myket

1. Sign up at [Tapsell](https://dashboard.tapsell.ir), add the app, and create one **Rewarded video** and one
   **Interstitial** zone. Put the app key and zone IDs in `AdsConfig.cs`.
2. Import the Tapsell Plus `.unitypackage` from
   [TapsellPlusSDK-UnityPlugin](https://github.com/tapsellorg/TapsellPlusSDK-UnityPlugin) (the `release` folder).
3. Sign up at the [Cafe Bazaar developer panel](https://pishkhan.cafebazaar.ir), add the app, create an in-app
   product with id `remove_ads`, and copy the app's RSA public key into `AdsConfig.cs`.
4. Import the Poolakey `.unitypackage` from [PoolakeyUnitySdk](https://github.com/cafebazaar/PoolakeyUnitySdk).
5. Bazaar and Myket don't accept apps that contain Google Play billing. If you installed Unity's
   **In-App Purchasing** package for the Google Play build, remove it before building for Bazaar or Myket.

### Google Play (only if you live or bank outside Iran)

1. Create a free account at [LevelPlay](https://platform.ironsrc.com), add an Android app, and create one
   **Rewarded** and one **Interstitial** ad unit. Put the app key and IDs in `AdsConfig.cs`.
2. In Package Manager install **Ads Mediation (LevelPlay)** 9+ and **In-App Purchasing** 5+.
3. In Google Play Console create a one-time in-app product with id `remove_ads`.

### Building on GitHub

The **Build Android APK** workflow builds all three store APKs. Run it from the Actions tab or push a tag
like `v1.0`. It needs the repo secrets `UNITY_EMAIL`, `UNITY_PASSWORD` and `UNITY_LICENSE` (the text of your
`Unity_lic.ulf`). SDKs you imported as `.unitypackage` must be committed for CI to include them.

Then add analytics (level start, win, lose, ad watched) so we can tune difficulty from real data.
