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

Ads and purchases are **test stubs** right now (`DebugAds` shows a 1 second fake ad). Before release:

1. Install **Unity LevelPlay** (or Google AdMob), make a class that implements `IAdsService`, and
   use it in `GameController.Awake` instead of `DebugAds`.
2. Install **Unity IAP**, create the non-consumable product `remove_ads`, and call it from `Store.BuyRemoveAds`.
3. Add analytics (level start, win, lose, ad watched) so we can tune difficulty from real data.
