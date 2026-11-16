using CargoSort.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CargoSort.Game
{
    /// <summary>Wires input, the board view, the HUD and ads together.</summary>
    public sealed class GameController : MonoBehaviour
    {
        // Monetization tuning. Change these from feedback and analytics.
        private const int FreeUndosPerLevel = 1;
        private const int MaxBonusSlots = 2;
        private const int UndoPrice = 30;        // coin prices; with too few coins the booster costs an ad
        private const int SlotPrice = 60;
        private const int HintPrice = 20;
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
        private int freeUndos;
        private int bonusSlots;
        private bool waitingForAd;
        private int pendingWinReward;
        private int pendingDaily;

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

            hud = gameObject.AddComponent<Hud>();
            hud.Undo += OnUndo;
            hud.AddSlot += OnAddSlot;
            hud.Hint += OnHint;
            hud.Restart += () => { if (!board.Busy && !waitingForAd) StartLevel(level); };
            hud.Retry += () => StartLevel(level);
            hud.Challenge += OnChallenge;
            hud.ToggleSound += () => { Progress.Sound = !Progress.Sound; hud.RefreshShop(); };
            hud.ToggleVibration += () =>
            {
                Progress.Vibration = !Progress.Vibration;
                Feedback.Vibrate(40);
                hud.RefreshShop();
            };
            hud.Revive += OnRevive;
            hud.ClaimWin += OnClaimWin;
            hud.ClaimDaily += OnClaimDaily;
            hud.OpenShop += OnOpenShop;
            hud.OpenPort += OnOpenPort;
            hud.BuildPortItem += OnBuildPortItem;
            hud.ThemeTapped += OnThemeTapped;
            hud.FreeCoins += OnFreeCoins;
            hud.RemoveAds += OnRemoveAds;
            hud.ToggleLanguage += OnToggleLanguage;
            BuildHud();

            Analytics.SessionStart();
            StartLevel(Progress.Level);
            CheckDailyGift();
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
            if (session != null && pendingDaily <= 0 && !hud.PanelOpen) CheckDailyGift();
        }

        private void OnApplicationQuit() => Reminders.Schedule();

        /// <summary>(Re)creates all UI texts, e.g. after switching language.</summary>
        private void BuildHud()
        {
            hud.Build();
            hud.SetRemoveAdsVisible(!Progress.AdsRemoved && MonetizationServices.PurchasesEnabled);
            hud.SetCoins(Progress.Coins);
            if (session != null)
            {
                hud.SetLevel(level, IsHard);
                RefreshHud();
            }
        }

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

        /// <summary>Starts level n, or today's daily challenge when n is 0.</summary>
        private void StartLevel(int n)
        {
            if (session != null && session.State.Status == GameStatus.Playing)
                Analytics.LevelQuit(level, session.State.TrucksShipped, session.State.TrucksTotal);
            level = n;
            session = new GameSession(n > 0 ? LevelGenerator.Generate(n) : LevelGenerator.GenerateChallenge(Progress.Today));
            freeUndos = FreeUndosPerLevel;
            bonusSlots = 0;
            hud.ShowWin(false);
            hud.ShowLose(false);
            hud.SetLevel(n, IsHard);
            hud.SetChallengeButton(n == 0);
            board.Rebuild(session.State);
            board.PlayIntro();
            Analytics.LevelStart(n);
            RefreshHud();
            ShowTutorialStep();

            if (n == LevelGenerator.FrozenFromLevel) hud.Toast(Loc.T("twist_frozen"), 3f);
            else if (n == LevelGenerator.HiddenFromLevel) hud.Toast(Loc.T("twist_hidden"), 3f);
            else if (IsHard) hud.Toast(Loc.T("hard_start"), 2f);
            else if (portTipPending) hud.Toast(Loc.T("port_tip"), 2.4f);
            portTipPending = false;
        }

        private bool IsHard => level > 0 && LevelGenerator.IsHard(level);

        private void RefreshHud()
        {
            hud.SetTrucks(session.State.TrucksShipped, session.State.TrucksTotal);
            hud.SetBoosters(freeUndos, Progress.Coins, UndoPrice, SlotPrice, HintPrice);
            hud.SetCoins(Progress.Coins);
        }

        private void Update()
        {
            FitCamera();
            if (board.Busy || waitingForAd || hud.PanelOpen) return;
            if (session.State.Status != GameStatus.Playing) return;
            if (!TryGetTap(out var screen)) return;

            int column = board.HitColumn(cam.ScreenToWorldPoint(screen));
            if (column < 0) return;

            if (!session.State.CanTap(column))
            {
                Feedback.Invalid();
                if (session.State.IsFrozen(column)) hud.Toast(Loc.T("frozen"));
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
            if (!TutorialActive || session.State.Status != GameStatus.Playing)
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
                }
                StartCoroutine(ShowWinSoon(level, pendingWinReward));
            }
            else if (session.State.Status == GameStatus.Lost)
            {
                Feedback.Lost();
                hud.ShowLose(true);
                Analytics.LevelFail(level, session.State.TrucksShipped, session.State.TrucksTotal, session.State.Buffer.Length);
            }
        }

        /// <summary>Lets the confetti play for a moment before the win panel covers the board.</summary>
        private System.Collections.IEnumerator ShowWinSoon(int wonLevel, int reward)
        {
            waitingForAd = true; // blocks other buttons for the short pause
            yield return new WaitForSeconds(0.8f);
            waitingForAd = false;
            if (level == wonLevel && pendingWinReward == reward) hud.ShowWin(true, wonLevel, reward);
        }

        // ---------- Boosters (rewarded ads) ----------

        private void OnUndo()
        {
            if (board.Busy || waitingForAd || !session.CanUndo) return;
            if (freeUndos > 0)
            {
                freeUndos--;
                Analytics.Booster("undo", "free");
                DoUndo();
                return;
            }
            if (TrySpend(UndoPrice)) { Analytics.Booster("undo", "coins"); DoUndo(); return; }
            WatchRewarded("undo", () => { Analytics.Booster("undo", "ad"); DoUndo(); });
        }

        private void DoUndo()
        {
            if (!session.Undo()) return;
            hud.ShowLose(false);
            board.Rebuild(session.State);
            RefreshHud();
            ShowTutorialStep();
        }

        private void OnAddSlot()
        {
            if (board.Busy || waitingForAd) return;
            if (bonusSlots >= MaxBonusSlots)
            {
                hud.Toast(Loc.T("dock_max"));
                return;
            }
            if (TrySpend(SlotPrice)) { Analytics.Booster("slot", "coins"); AddSlotNow(); return; }
            WatchRewarded("extra_slot", () => { Analytics.Booster("slot", "ad"); AddSlotNow(); });
        }

        private void OnRevive()
        {
            if (waitingForAd) return;
            WatchRewarded("revive", () =>
            {
                Analytics.Booster("revive", "ad");
                AddSlotNow();
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

        private void OnHint()
        {
            if (board.Busy || waitingForAd) return;
            if (TrySpend(HintPrice)) { Analytics.Booster("hint", "coins"); board.ShowHint(session.HintColumn()); return; }
            WatchRewarded("hint", () => { Analytics.Booster("hint", "ad"); board.ShowHint(session.HintColumn()); });
        }

        /// <summary>Pays for a booster with coins if the player has enough.</summary>
        private bool TrySpend(int price)
        {
            if (Progress.Coins < price) return false;
            Progress.Coins -= price;
            Feedback.Coins();
            RefreshHud();
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

        // ---------- Level flow and interstitials ----------

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
            if (Port.CanBuildSomething(Progress.Coins) && PlayerPrefs.GetInt("port_tip_shown", 0) == 0)
            {
                PlayerPrefs.SetInt("port_tip_shown", 1);
                PlayerPrefs.Save();
                portTipPending = true;
            }
            if (level >= AskNotificationsAfterLevel) Reminders.AskPermission();
            if (level == 0)
            {
                // After the challenge, go back to the normal levels (no interstitial here).
                StartLevel(Progress.Level);
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
            if (board.Busy || waitingForAd) return;
            if (level == 0)
            {
                StartLevel(Progress.Level);
                return;
            }
            if (Progress.ChallengeDay == Progress.Today)
            {
                hud.Toast(Loc.T("challenge_done"), 2.2f);
                return;
            }
            StartLevel(0);
            hud.Toast(Loc.T("challenge_start", ChallengeReward), 2.2f);
        }

        private void AddCoins(int amount)
        {
            Feedback.Coins();
            hud.FlyCoins(amount);
            Progress.Coins += amount;
            hud.SetCoins(Progress.Coins);
            hud.RefreshShop();
            hud.RefreshPort();
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
        }

        // ---------- Shop ----------

        private void OnOpenShop()
        {
            if (board.Busy || waitingForAd) return;
            hud.RefreshShop();
            hud.ShowShop(true);
        }

        // ---------- Port building ----------

        private void OnOpenPort()
        {
            if (board.Busy || waitingForAd) return;
            Analytics.PortOpened(Port.Index, Port.BuiltCount);
            hud.ShowPort(true);
        }

        private void OnBuildPortItem(int item)
        {
            if (portCompleting || Port.IsBuilt(item)) return;
            int cost = Port.Cost(item);
            if (!TrySpend(cost))
            {
                hud.Toast(Loc.T("not_enough"));
                return;
            }
            Port.Build(item);
            Analytics.PortBuild(Port.Index, Port.ItemKeys[item], cost);
            Feedback.TruckLeft();
            hud.RefreshPort();
            hud.AnimatePortItem(item);
            if (Port.Complete) StartCoroutine(CompletePort());
        }

        private bool portCompleting, portTipPending;

        /// <summary>Plays the finished port for a moment, pays the bonus, then opens the next port.</summary>
        private System.Collections.IEnumerator CompletePort()
        {
            portCompleting = true;
            yield return new WaitForSecondsRealtime(0.7f);
            int reward = Port.CompletionReward;
            Analytics.PortComplete(Port.Index);
            Feedback.Won();
            hud.Toast(Loc.T("port_done", reward), 2.2f);
            yield return new WaitForSecondsRealtime(2.2f);
            Port.NextPort();
            AddCoins(reward);
            hud.Toast(Loc.T("port_next", Port.RawName), 2f);
            portCompleting = false;
        }

        private void OnThemeTapped(int index)
        {
            if (index == Progress.Theme) return;
            if (!Progress.OwnsTheme(index))
            {
                int price = Themes.All[index].Price;
                if (Progress.Coins < price)
                {
                    hud.Toast(Loc.T("not_enough"));
                    return;
                }
                Progress.Coins -= price;
                Progress.UnlockTheme(index);
            }
            Progress.Theme = index;
            cam.backgroundColor = Art.Background;
            board.Rebuild(session.State);
            RefreshHud();
            hud.RefreshShop();
        }

        private void OnFreeCoins()
        {
            if (waitingForAd) return;
            WatchRewarded("shop_coins", () => AddCoins(Hud.FreeCoinsAmount));
        }

        private void OnToggleLanguage()
        {
            Progress.Language = Loc.Persian ? "en" : "fa";
            bool dailyOpen = pendingDaily > 0;
            bool winOpen = pendingWinReward > 0;
            bool lost = session.State.Status == GameStatus.Lost;
            BuildHud();
            hud.SetChallengeButton(level == 0);
            ShowTutorialStep();
            hud.RefreshShop();
            hud.ShowShop(true);
            if (dailyOpen) hud.ShowDaily(true, Progress.DailyStreak, pendingDaily);
            if (winOpen) hud.ShowWin(true, level, pendingWinReward);
            if (lost) hud.ShowLose(true);
        }

        private void OnRemoveAds()
        {
            if (waitingForAd) return;
            Store.BuyRemoveAds(ok =>
            {
                Analytics.Purchase(Store.RemoveAdsProductId, ok);
                if (ok) hud.Toast(Loc.T("ads_removed"));
                else hud.Toast(Loc.T("purchase_failed"));
            });
        }

        private void OnAdsRemoved() => hud.SetRemoveAdsVisible(false);

        private void OnDestroy() => Store.AdsRemoved -= OnAdsRemoved;
    }
}
