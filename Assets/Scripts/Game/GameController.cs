using System.Collections.Generic;
using CargoSort.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CargoSort.Game
{
    /// <summary>Wires input, the board view, the HUD, boosters, offers, ads and purchases together.</summary>
    public sealed class GameController : MonoBehaviour
    {
        // Monetization tuning. Booster prices, packs and skins are in Economy.cs.
        private const int MaxBonusSlots = 2;
        private const int FirstInterstitialLevel = 4;
        private const int InterstitialEveryLevels = 3;

        // Economy tuning.
        private const int DailyBase = 20;      // day 1 gift; +20 per streak day, up to day 7
        private const int MaxStreak = 7;
        private const int ChallengeReward = 100;  // x3 with an ad, like normal levels
        private const int AskNotificationsAfterLevel = 2;

        private Camera cam;
        private BoardView board;
        private Hud hud;
        private IAdsService ads;
        private GameSession session;
        private int level;          // 0 while playing the daily challenge
        private int bonusSlots;
        private bool waitingForAd, buying, craneMode;
        private int pendingWinReward;
        private int pendingDaily;
        private readonly Dictionary<Booster, int> freeUses = new Dictionary<Booster, int>();
        private IList<Item> pendingItems;

        /// <summary>Creates the game when Play starts, so no scene setup is needed.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (FindFirstObjectByType<GameController>() != null) return;
            new GameObject("CargoSort").AddComponent<GameController>();
        }

        private void Awake()
        {
            Application.targetFrameRate = 60;
#if ENABLE_LEGACY_INPUT_MANAGER && !ENABLE_INPUT_SYSTEM
            Input.multiTouchEnabled = false;
#endif
            SetupCamera();

            board = new GameObject("BoardView").AddComponent<BoardView>();
            board.transform.SetParent(transform, false);

            Feedback.Init(gameObject);
            ads = MonetizationServices.CreateAds?.Invoke(gameObject) ?? gameObject.AddComponent<DebugAds>();
            Store.AdsRemoved += OnAdsRemoved;
            Store.Delivered += OnStoreDelivered;

            hud = gameObject.AddComponent<Hud>();
            hud.BoosterTapped += OnBooster;
            hud.Restart += OnRestart;
            hud.Retry += () => { BreakStreak(); StartLevel(level); };
            hud.LoseUndo += () => Pay(Booster.Undo, "undo", DoUndo);
            hud.Revive += OnRevive;
            hud.ClaimWin += OnClaimWin;
            hud.ClaimDaily += OnClaimDaily;
            hud.GoHome += OnGoHome;
            hud.Play += OnPlay;
            hud.Challenge += OnChallenge;
            hud.OpenShop += OnOpenShop;
            hud.OpenTour += OnOpenTour;
            hud.BuildLandmark += OnBuildLandmark;
            hud.OpenChest += OnOpenChest;
            hud.OpenStarter += () => { Analytics.Offer("starter", "shown"); hud.ShowStarter(true); };
            hud.BuyPack += OnBuyPack;
            hud.SkinTapped += OnSkinTapped;
            hud.ThemeTapped += OnThemeTapped;
            hud.RewardClaimed += OnRewardClaimed;
            hud.ToggleSound += () => { Progress.Sound = !Progress.Sound; hud.RefreshSettings(); };
            hud.ToggleVibration += () =>
            {
                Progress.Vibration = !Progress.Vibration;
                Feedback.Vibrate(40);
                hud.RefreshSettings();
            };
            hud.ToggleLanguage += OnToggleLanguage;
            hud.RestorePurchases += OnRestorePurchases;
            hud.Privacy += () => Application.OpenURL(Economy.PrivacyUrl);
            hud.DevGoToLevel += OnDevGoToLevel;
            hud.DevAddCoins += () => AddCoins(1000);
            hud.Build();

            Analytics.SessionStart();
            ShowHome();
            StartLevel(Progress.Level); // loads behind the home screen, ready for Play
            CheckDailyGift();
            MaybeShowStarter();
            Reminders.Clear();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                Reminders.Schedule();
                return;
            }
            Reminders.Clear();
            // Back in the app on a new day: offer the daily gift if nothing else is open.
            if (session != null && pendingDaily <= 0 && !hud.DailyVisible && (hud.HomeVisible || !hud.PanelOpen)) CheckDailyGift();
        }

        private void OnApplicationQuit() => Reminders.Schedule();

        private void SetupCamera()
        {
            cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
            }
            cam.orthographic = true;
            cam.transform.position = new Vector3(0, 0, -10);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Art.Background;
            FitCamera();
        }

        private void FitCamera()
        {
            // The board is designed for a 10 x 18 world box; keep all of it visible on any aspect.
            cam.orthographicSize = Mathf.Max(9f, 5.2f / Mathf.Max(0.1f, cam.aspect));
        }

        // ---------- Home and level flow ----------

        private void ShowHome()
        {
            SetCrane(false);
            hud.ShowHome(true);
            int next = Progress.Level;
            hud.SetHome(next, LevelGenerator.IsHard(next), Progress.WinStreak, Progress.ChallengeDay != Progress.Today, Economy.ChestReady);
            hud.SetCoins(Progress.Coins);
            if (Progress.Stars > 0 && !Progress.TourSeen)
            {
                Progress.TourSeen = true;
                hud.Toast(Loc.T("tour_intro"), 3.8f);
            }
        }

        private void ShowGame()
        {
            hud.ShowHome(false);
            RefreshHud();
            ShowTutorialStep();
        }

        private void OnPlay()
        {
            if (waitingForAd) return;
            int next = Progress.Level;
            ShowGame();
            // Keep a level that is already being played; otherwise start it fresh.
            bool inProgress = level == next && session != null && session.CanUndo && session.State.Status == GameStatus.Playing;
            if (!inProgress) StartLevel(next);
        }

        private void OnGoHome()
        {
            if (board.Busy || waitingForAd) return;
            if (session.State.Status == GameStatus.Lost) BreakStreak();
            hud.ShowLose(false);
            if (session.State.Status != GameStatus.Playing || level == 0) StartLevel(Progress.Level);
            ShowHome();
        }

        private void OnRestart()
        {
            if (board.Busy || waitingForAd) return;
            if (session.CanUndo) BreakStreak(); // giving up a started level ends the streak
            StartLevel(level);
        }

        /// <summary>Starts level n, or today's daily challenge when n is 0.</summary>
        private void StartLevel(int n)
        {
            if (session != null && session.State.Status == GameStatus.Playing)
                Analytics.LevelQuit(level, session.State.TrucksShipped, session.State.TrucksTotal);
            level = n;
            session = new GameSession(n > 0 ? LevelGenerator.Generate(n) : LevelGenerator.GenerateChallenge(Progress.Today));
            bonusSlots = 0;
            SetCrane(false);
            int streak = n > 0 ? Progress.WinStreak : 0;
            freeUses[Booster.Undo] = 1;
            freeUses[Booster.Slot] = 0;
            freeUses[Booster.Crane] = streak >= Economy.StreakCraneAt ? 1 : 0;
            freeUses[Booster.Magnet] = streak >= Economy.StreakMagnetAt ? 1 : 0;
            freeUses[Booster.Shuffle] = 0;

            hud.ShowWin(false);
            hud.ShowLose(false);
            hud.SetLevel(n, IsHard);
            session.State.BeltSpotPicker = board.NearestBeltSpot; // copied into undo states and shuffles
            board.Rebuild(session.State);
            board.PlayIntro();
            Analytics.LevelStart(n);
            RefreshHud();
            ShowTutorialStep();

            if (hud.HomeVisible) return; // toasts only while playing
            if (n == LevelGenerator.FrozenFromLevel) hud.Toast(Loc.T("twist_frozen"), 3f);
            else if (n == LevelGenerator.HiddenFromLevel) hud.Toast(Loc.T("twist_hidden"), 3f);
            else if (n == LevelGenerator.BeltFromLevel) hud.Toast(Loc.T("belt_intro"), 4f);
            else if (n == LevelGenerator.TapedFromLevel) hud.Toast(Loc.T("tape_intro"), 4f);
            else if (n == LevelGenerator.BigTrucksFromLevel) hud.Toast(Loc.T("big_intro"), 4f);
            else if (n == LevelGenerator.TwoColorFromLevel) hud.Toast(Loc.T("two_intro"), 4f);
            else if (n == LevelGenerator.LocksFromLevel) hud.Toast(Loc.T("lock_intro"), 4f);
            else if (n == LevelGenerator.BombsFromLevel) hud.Toast(Loc.T("bomb_intro"), 4.5f);
            else if (n == LevelGenerator.TarpsFromLevel) hud.Toast(Loc.T("tarp_intro"), 4.5f);
            else if (freeUses[Booster.Crane] > 0) hud.Toast(Loc.T("streak_bonus", streak), 2.4f);
            else if (IsHard) hud.Toast(Loc.T("hard_start"), 2f);
        }

        private bool IsHard => level > 0 && LevelGenerator.IsHard(level);

        private void RefreshHud()
        {
            hud.SetLevel(level, IsHard);
            hud.SetTrucks(session.State.TrucksShipped, session.State.TrucksTotal);
            hud.SetBoosters(b => freeUses[b] + Progress.Owned(b), Progress.Coins);
            hud.SetCoins(Progress.Coins);
        }

        private void Update()
        {
            FitCamera();
            if (board.Busy || waitingForAd || buying || hud.PanelOpen) return;

            // Belt: a riding crate passing under its truck jumps on.
            if (session.State.Belt && board.TryFindBeltLoad(out int beltSlot, out int beltBay))
            {
                var loaded = session.BeltLoad(beltSlot, beltBay);
                if (loaded.Count > 0)
                {
                    board.Play(loaded, session.State, AfterMove);
                    return;
                }
            }
            if (session.State.Status != GameStatus.Playing) return;
            if (!TryGetTap(out var screen)) return;

            Vector2 world = cam.ScreenToWorldPoint(screen);
            int column = board.HitColumn(world);
            if (column < 0) return;

            if (craneMode)
            {
                int row = board.HitRow(world, column);
                if (row < 0) return;
                if (!session.State.CanPluck(column, row))
                {
                    Feedback.Invalid();
                    board.Shake(column);
                    return;
                }
                Pay(Booster.Crane, "crane", () =>
                {
                    SetCrane(false);
                    var lifted = session.Pluck(column, row);
                    Feedback.TruckLeft();
                    board.Play(lifted, session.State, AfterMove);
                });
                return;
            }

            if (!session.State.CanTap(column))
            {
                Feedback.Invalid();
                if (session.State.IsFrozen(column)) hud.Toast(Loc.T("frozen"));
                else if (session.State.IsCovered(column)) hud.Toast(Loc.T("covered"));
                board.Shake(column);
                return;
            }
            var events = session.Tap(column);
            Analytics.Move();
            board.Play(events, session.State, AfterMove);
        }

        private static bool TryGetTap(out Vector2 position)
        {
            position = default;
#if ENABLE_INPUT_SYSTEM
            var pointer = UnityEngine.InputSystem.Pointer.current;
            if (pointer == null || !pointer.press.wasPressedThisFrame) return false;
            position = pointer.position.ReadValue();
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return false;
            return true;
#else
            if (Input.touchCount > 0)
            {
                var touch = Input.GetTouch(0);
                if (touch.phase != TouchPhase.Began) return false;
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(touch.fingerId)) return false;
                position = touch.position;
                return true;
            }
            if (!Input.GetMouseButtonDown(0)) return false;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return false;
            position = Input.mousePosition;
            return true;
#endif
        }

        // ---------- First-level tutorial ----------

        private bool TutorialActive => level == 1 && !Progress.TutorialDone;

        /// <summary>Points the hand at the best column and explains where its crate will go.</summary>
        private void ShowTutorialStep()
        {
            if (!TutorialActive || session.State.Status != GameStatus.Playing || hud.HomeVisible)
            {
                hud.SetTip(null);
                return;
            }
            int column = session.HintColumn();
            board.ShowTutorialHand(column);
            int top = column >= 0 ? session.State.Top(column) : GameState.Empty;
            hud.SetTip(Loc.T(top != GameState.Empty && session.State.IsWanted(top) ? "tut_truck" : "tut_dock"));
        }

        private void AfterMove()
        {
            RefreshHud();
            ShowTutorialStep();
            if (session.State.Status == GameStatus.Won)
            {
                Feedback.Won();
                board.Confetti();
                if (level == 1 && !Progress.TutorialDone) Analytics.Tutorial("done");
                if (level == 1) Progress.TutorialDone = true;
                Analytics.LevelWin(level, session.State.Buffer.Length);
                hud.SetTip(null);
                int streak = 0, piggyAdded = 0, starsAdded = 0;
                if (level == 0)
                {
                    Progress.ChallengeDay = Progress.Today;
                    Progress.ChallengesDone++;
                    pendingWinReward = ChallengeReward;
                }
                else
                {
                    Progress.Level = level + 1;
                    pendingWinReward = (10 + level / 2) * (IsHard ? 2 : 1);
                    streak = ++Progress.WinStreak;
                    piggyAdded = Mathf.Min(Economy.PiggyMax - Progress.Piggy, Economy.PiggyPerWin * (IsHard ? 2 : 1));
                    Progress.Piggy += piggyAdded;
                    starsAdded = WorldTour.StarsForWin(level);
                    Progress.Stars += starsAdded;
                }
                StartCoroutine(ShowWinSoon(level, pendingWinReward, streak, piggyAdded, starsAdded));
            }
            else if (session.State.Status == GameStatus.Lost)
            {
                SetCrane(false);
                Feedback.Lost();
                hud.ShowLose(true, level > 0 ? Progress.WinStreak : 0, session.State.Exploded);
                Analytics.LevelFail(level, session.State.TrucksShipped, session.State.TrucksTotal, session.State.Buffer.Length);
            }
        }

        /// <summary>Lets the confetti play for a moment before the win panel covers the board.</summary>
        private System.Collections.IEnumerator ShowWinSoon(int wonLevel, int reward, int streak, int piggyAdded, int starsAdded)
        {
            waitingForAd = true; // blocks other buttons for the short pause
            yield return new WaitForSeconds(0.8f);
            waitingForAd = false;
            if (level == wonLevel && pendingWinReward == reward) hud.ShowWin(true, wonLevel, reward, streak, piggyAdded, starsAdded);
        }

        private void BreakStreak()
        {
            if (level > 0 && Progress.WinStreak > 0) Progress.WinStreak = 0;
        }

        // ---------- Boosters ----------

        private void OnBooster(Booster b)
        {
            if (board.Busy || waitingForAd || buying || hud.HomeVisible) return;
            if (b != Booster.Crane) SetCrane(false);
            var status = session.State.Status;
            switch (b)
            {
                case Booster.Undo:
                    if (!session.CanUndo) return;
                    Pay(b, "undo", DoUndo);
                    break;
                case Booster.Slot:
                    if (bonusSlots >= MaxBonusSlots)
                    {
                        hud.Toast(Loc.T("dock_max"));
                        return;
                    }
                    Pay(b, "extra_slot", AddSlotNow);
                    break;
                case Booster.Crane:
                    if (status == GameStatus.Playing) SetCrane(!craneMode);
                    break;
                case Booster.Magnet:
                    if (status != GameStatus.Playing) return;
                    if (!session.State.HasMagnetTarget)
                    {
                        hud.Toast(Loc.T("magnet_none"));
                        return;
                    }
                    Pay(b, "magnet", () =>
                    {
                        var events = session.Magnet();
                        Feedback.TruckLeft();
                        board.Play(events, session.State, AfterMove);
                    });
                    break;
                case Booster.Shuffle:
                    if (status == GameStatus.Won) return;
                    Pay(b, "shuffle", DoShuffle);
                    break;
            }
        }

        /// <summary>Pays for one use: free uses first, then owned ones, then coins, then a rewarded ad.</summary>
        private void Pay(Booster b, string placement, System.Action use)
        {
            string key = Economy.Key(b);
            if (freeUses[b] > 0)
            {
                freeUses[b]--;
                Analytics.Booster(key, "free");
            }
            else if (Progress.Owned(b) > 0)
            {
                Progress.AddOwned(b, -1);
                Analytics.Booster(key, "owned");
            }
            else if (TrySpend(Economy.Price(b)))
            {
                Analytics.Booster(key, "coins");
            }
            else
            {
                WatchRewarded(placement, () =>
                {
                    Analytics.Booster(key, "ad");
                    use();
                    RefreshHud();
                });
                return;
            }
            RefreshHud();
            use();
        }

        private void SetCrane(bool on)
        {
            craneMode = on;
            hud.SetCraneMode(on);
        }

        private void DoUndo()
        {
            if (!session.Undo()) return;
            hud.ShowLose(false);
            board.Rebuild(session.State);
            RefreshHud();
            ShowTutorialStep();
        }

        private void DoShuffle()
        {
            session.Shuffle(Random.Range(1, int.MaxValue));
            hud.ShowLose(false);
            board.Rebuild(session.State);
            board.PlayIntro();
            Feedback.CrateParked();
            AfterMove();
        }

        private void OnRevive()
        {
            if (waitingForAd) return;
            bool exploded = session.State.Exploded;
            WatchRewarded(exploded ? "revive_bomb" : "revive", () =>
            {
                Analytics.Booster(exploded ? "revive_bomb" : "revive", "ad");
                if (exploded)
                {
                    session.State.ExtendBombs(5); // +5 moves for every bomb
                    board.Rebuild(session.State);
                    RefreshHud();
                }
                else AddSlotNow();
                hud.ShowLose(false);
            });
        }

        private void AddSlotNow()
        {
            bonusSlots++;
            session.AddBufferSlot();
            board.Rebuild(session.State);
            RefreshHud();
            ShowTutorialStep();
        }

        /// <summary>Pays with coins if the player has enough.</summary>
        private bool TrySpend(int price)
        {
            if (Progress.Coins < price) return false;
            Progress.Coins -= price;
            Feedback.Coins();
            return true;
        }

        private void WatchRewarded(string placement, System.Action reward)
        {
            if (!ads.RewardedReady)
            {
                hud.Toast(Loc.T("no_ad"));
                return;
            }
            waitingForAd = true;
            ads.ShowRewarded(placement, ok =>
            {
                waitingForAd = false;
                Analytics.Ad("rewarded", placement, ok);
                if (ok) reward();
            });
        }

        // ---------- Win rewards and interstitials ----------

        private void OnClaimWin(bool triple)
        {
            if (waitingForAd || pendingWinReward <= 0) return;
            if (triple)
            {
                // A rewarded ad replaces the interstitial for this level.
                WatchRewarded("level_reward_x3", () => FinishWin(pendingWinReward * 3, false));
                return;
            }
            FinishWin(pendingWinReward, true);
        }

        private void FinishWin(int coins, bool allowInterstitial)
        {
            AddCoins(coins);
            pendingWinReward = 0;
            hud.ShowWin(false);
            if (level >= AskNotificationsAfterLevel) Reminders.AskPermission();
            if (level == 0)
            {
                // After the challenge, go back home (no interstitial here).
                StartLevel(Progress.Level);
                ShowHome();
                return;
            }
            int next = level + 1;
            bool showAd = allowInterstitial && !Progress.AdsRemoved &&
                          next >= FirstInterstitialLevel && (next - FirstInterstitialLevel) % InterstitialEveryLevels == 0;
            if (!showAd)
            {
                StartLevel(next);
                return;
            }
            waitingForAd = true;
            ads.ShowInterstitial("level_end", () =>
            {
                waitingForAd = false;
                Analytics.Ad("interstitial", "level_end", true);
                StartLevel(next);
            });
        }

        // ---------- Daily challenge ----------

        private void OnChallenge()
        {
            if (waitingForAd) return;
            if (Progress.ChallengeDay == Progress.Today)
            {
                hud.Toast(Loc.T("challenge_done"), 2.2f);
                return;
            }
            ShowGame();
            StartLevel(0);
            hud.Toast(Loc.T("challenge_start", ChallengeReward), 2.2f);
        }

        private void AddCoins(int amount)
        {
            Feedback.Coins();
            hud.FlyCoins(amount);
            Progress.Coins += amount;
            hud.SetCoins(Progress.Coins);
            if (session != null) RefreshHud();
        }

        // ---------- Daily gift ----------

        private void CheckDailyGift()
        {
            int today = Progress.Today;
            if (Progress.LastDailyDay == today) return;
            int streak = Progress.LastDailyDay == today - 1 ? Mathf.Min(Progress.DailyStreak + 1, MaxStreak) : 1;
            pendingDaily = DailyBase * streak;
            Progress.DailyStreak = streak;
            hud.ShowDaily(true, streak, pendingDaily);
        }

        private void OnClaimDaily(bool doubled)
        {
            if (waitingForAd || pendingDaily <= 0) return;
            if (doubled)
            {
                WatchRewarded("daily_x2", () => FinishDaily(pendingDaily * 2));
                return;
            }
            FinishDaily(pendingDaily);
        }

        private void FinishDaily(int coins)
        {
            Progress.LastDailyDay = Progress.Today;
            Analytics.DailyGift(Progress.DailyStreak, coins > pendingDaily);
            pendingDaily = 0;
            hud.ShowDaily(false);
            AddCoins(coins);
            MaybeShowStarter();
        }

        // ---------- Offers: starter pack, free gift, piggy bank ----------

        /// <summary>The starter pack pops up once, on the home screen, after the daily gift is closed.</summary>
        private void MaybeShowStarter()
        {
            if (!hud.HomeVisible || hud.DailyVisible || Progress.StarterSeen || !Economy.StarterActive) return;
            Progress.StarterSeen = true;
            Analytics.Offer("starter", "shown");
            hud.ShowStarter(true);
        }

        private void OnOpenChest()
        {
            if (waitingForAd || buying) return;
            if (!Economy.ChestReady)
            {
                hud.Toast(Loc.T("chest_tomorrow"));
                return;
            }
            WatchRewarded("free_gift", () =>
            {
                Progress.ChestDay = Progress.Today;
                Analytics.Offer("chest", "opened");
                GiveItems(new[] { Economy.RollChest() }, "chest");
            });
        }

        // ---------- World Tour ----------

        private void OnOpenTour()
        {
            if (board.Busy || waitingForAd) return;
            SetCrane(false);
            hud.ShowTour(true);
        }

        private void OnBuildLandmark(int mark)
        {
            int city = Progress.TourCity, mask = Progress.TourBuilt, stars = Progress.Stars, was = city;
            var result = WorldTour.Build(ref city, ref mask, ref stars, mark, out int coins);
            if (result == WorldTour.BuildResult.Failed)
            {
                hud.Toast(Loc.T("need_stars"));
                return;
            }
            Progress.Stars = stars;
            Progress.TourCity = city;
            Progress.TourBuilt = mask;
            Feedback.Won();
            Analytics.Landmark(was, mark, result == WorldTour.BuildResult.CityDone);
            AddCoins(coins);
            hud.RefreshTour();
            if (result == WorldTour.BuildResult.CityDone)
            {
                GiveItems(new[] { Item.Coins(WorldTour.CityCoins(was)), Item.Of(Booster.Crane, 1), Item.Of(Booster.Magnet, 1), Item.Of(Booster.Shuffle, 1) }, "city_done");
                if (!WorldTour.Finished(city)) hud.Toast(Loc.T("next_city", Loc.T("c_" + WorldTour.Cities[city].Key)), 2.6f);
            }
            if (hud.HomeVisible) ShowHome();
        }

        // ---------- Shop ----------

        private void OnOpenShop()
        {
            if (board.Busy || waitingForAd) return;
            SetCrane(false);
            hud.ShowShop(true);
        }

        private void OnBuyPack(Pack pack)
        {
            if (waitingForAd || buying) return;
            buying = true;
            Store.Buy(pack, ok =>
            {
                buying = false;
                Analytics.Purchase(pack.ProductId, ok);
                if (!ok)
                {
                    hud.Toast(Loc.T("purchase_failed"));
                    return;
                }
                Deliver(pack);
            });
        }

        /// <summary>A purchase the store finished outside of Buy (interrupted earlier): give it now.</summary>
        private void OnStoreDelivered(string productId)
        {
            foreach (var pack in Economy.AllPacks)
                if (pack.ProductId == productId) Deliver(pack);
        }

        private void Deliver(Pack pack)
        {
            Analytics.Offer(pack.ProductId, "bought");
            if (pack == Economy.Starter)
            {
                Progress.StarterBought = true;
                hud.ShowStarter(false);
            }
            if (pack == Economy.Piggy)
            {
                int coins = Progress.Piggy;
                Progress.Piggy = 0;
                GiveItems(new[] { Item.Coins(coins) }, "purchased");
                return;
            }
            GiveItems(pack.Items, "purchased");
        }

        /// <summary>Shows the reward popup; the items are added when the player taps Claim.</summary>
        private void GiveItems(IList<Item> items, string titleKey)
        {
            if (pendingItems != null) Grant(pendingItems); // never lose an earlier reward
            pendingItems = items;
            hud.ShowReward(titleKey, items);
        }

        private void OnRewardClaimed()
        {
            if (pendingItems == null) return;
            var items = pendingItems;
            pendingItems = null;
            Grant(items);
        }

        private void Grant(IList<Item> items)
        {
            int coins = 0;
            foreach (var item in items)
            {
                if (item.Kind == ItemKind.Coins) coins += item.Amount;
                else if (item.Kind == ItemKind.NoAds) Store.Grant();
                else Progress.AddOwned(item.AsBooster, item.Amount);
            }
            if (coins > 0) AddCoins(coins);
            RefreshAll();
        }

        private void RefreshAll()
        {
            if (session != null) RefreshHud();
            hud.SetCoins(Progress.Coins);
            hud.RefreshShop();
            hud.RefreshTour();
            if (hud.HomeVisible) ShowHome();
        }

        private void OnSkinTapped(string kind, Skin skin)
        {
            bool truck = kind == "truck";
            if ((truck ? Progress.TruckSkin : Progress.CrateSkin) == skin.Key) return;
            if (!Progress.OwnsSkin(kind, skin))
            {
                if (!TrySpend(skin.Price))
                {
                    hud.Toast(Loc.T("not_enough"));
                    return;
                }
                Progress.UnlockSkin(kind, skin);
                Analytics.SkinBought(kind, skin.Key, skin.Price);
            }
            if (truck) Progress.TruckSkin = skin.Key;
            else Progress.CrateSkin = skin.Key;
            board.Rebuild(session.State);
            RefreshAll();
        }

        private void OnThemeTapped(int index)
        {
            if (index == Progress.Theme) return;
            if (!Progress.OwnsTheme(index))
            {
                if (!TrySpend(Themes.All[index].Price))
                {
                    hud.Toast(Loc.T("not_enough"));
                    return;
                }
                Progress.UnlockTheme(index);
            }
            Progress.Theme = index;
            cam.backgroundColor = Art.Background;
            board.Rebuild(session.State);
            RefreshAll();
        }

        private void OnRestorePurchases()
        {
            var backend = MonetizationServices.StoreBackend;
            if (backend != null) backend.Restore();
            hud.Toast(Loc.T("restore_done"));
        }

        // ---------- Developer tools (editor and development builds only; see Hud.BuildDevPanel) ----------

        private void OnDevGoToLevel(int n)
        {
            if (board.Busy || waitingForAd) return;
            n = Mathf.Clamp(n, 1, 999);
            pendingWinReward = 0;
            hud.ShowWin(false);
            hud.ShowLose(false);
            Progress.Level = n;
            ShowGame();
            StartLevel(n);
        }

        // ---------- Language ----------

        private void OnToggleLanguage()
        {
            Progress.Language = Loc.Persian ? "en" : "fa";
            bool home = hud.HomeVisible;
            bool winOpen = pendingWinReward > 0;
            bool lost = session.State.Status == GameStatus.Lost;
            hud.Build();
            board.Rebuild(session.State); // the "Next" label is drawn in the world
            if (home) ShowHome();
            else ShowGame();
            hud.ShowSettings();
            if (pendingDaily > 0) hud.ShowDaily(true, Progress.DailyStreak, pendingDaily);
            if (winOpen) hud.ShowWin(true, level, pendingWinReward, level > 0 ? Progress.WinStreak : 0);
            if (lost) hud.ShowLose(true, level > 0 ? Progress.WinStreak : 0, session.State.Exploded);
            if (pendingItems != null) hud.ShowReward("purchased", pendingItems);
        }

        private void OnAdsRemoved() => hud.RefreshShop();

        private void OnDestroy()
        {
            Store.AdsRemoved -= OnAdsRemoved;
            Store.Delivered -= OnStoreDelivered;
        }
    }
}
