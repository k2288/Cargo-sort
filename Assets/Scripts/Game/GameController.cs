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
        private const int FirstInterstitialLevel = 4;
        private const int InterstitialEveryLevels = 3;

        // Economy tuning.
        private const int DailyBase = 20;      // day 1 gift; +20 per streak day, up to day 7
        private const int MaxStreak = 7;

        private Camera cam;
        private BoardView board;
        private Hud hud;
        private IAdsService ads;
        private GameSession session;
        private int level;
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

            ads = MonetizationServices.CreateAds?.Invoke(gameObject) ?? gameObject.AddComponent<DebugAds>();
            Store.AdsRemoved += OnAdsRemoved;

            hud = gameObject.AddComponent<Hud>();
            hud.Undo += OnUndo;
            hud.AddSlot += OnAddSlot;
            hud.Hint += OnHint;
            hud.Restart += () => { if (!board.Busy && !waitingForAd) StartLevel(level); };
            hud.Retry += () => StartLevel(level);
            hud.Revive += OnRevive;
            hud.ClaimWin += OnClaimWin;
            hud.ClaimDaily += OnClaimDaily;
            hud.OpenShop += OnOpenShop;
            hud.ThemeTapped += OnThemeTapped;
            hud.FreeCoins += OnFreeCoins;
            hud.RemoveAds += OnRemoveAds;
            hud.ToggleLanguage += OnToggleLanguage;
            BuildHud();

            StartLevel(Progress.Level);
            CheckDailyGift();
        }

        /// <summary>(Re)creates all UI texts, e.g. after switching language.</summary>
        private void BuildHud()
        {
            hud.Build();
            hud.SetRemoveAdsVisible(!Progress.AdsRemoved && MonetizationServices.PurchasesEnabled);
            hud.SetCoins(Progress.Coins);
            if (session != null)
            {
                hud.SetLevel(level);
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

        private void StartLevel(int n)
        {
            level = n;
            session = new GameSession(LevelGenerator.Generate(n));
            freeUndos = FreeUndosPerLevel;
            bonusSlots = 0;
            hud.ShowWin(false);
            hud.ShowLose(false);
            hud.SetLevel(n);
            board.Rebuild(session.State);
            RefreshHud();
        }

        private void RefreshHud()
        {
            hud.SetTrucks(session.State.TrucksShipped, session.State.TrucksTotal);
            hud.SetUndoLabel(freeUndos);
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
                board.Shake(column);
                return;
            }
            var events = session.Tap(column);
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

        private void AfterMove()
        {
            RefreshHud();
            if (session.State.Status == GameStatus.Won)
            {
                Progress.Level = level + 1;
                pendingWinReward = 10 + level / 2;
                hud.ShowWin(true, level, pendingWinReward);
            }
            else if (session.State.Status == GameStatus.Lost)
            {
                hud.ShowLose(true);
            }
        }

        // ---------- Boosters (rewarded ads) ----------

        private void OnUndo()
        {
            if (board.Busy || waitingForAd || !session.CanUndo) return;
            if (freeUndos > 0)
            {
                freeUndos--;
                DoUndo();
                return;
            }
            WatchRewarded("undo", DoUndo);
        }

        private void DoUndo()
        {
            if (!session.Undo()) return;
            hud.ShowLose(false);
            board.Rebuild(session.State);
            RefreshHud();
        }

        private void OnAddSlot()
        {
            if (board.Busy || waitingForAd) return;
            if (bonusSlots >= MaxBonusSlots)
            {
                hud.Toast(Loc.T("dock_max"));
                return;
            }
            WatchRewarded("extra_slot", AddSlotNow);
        }

        private void OnRevive()
        {
            if (waitingForAd) return;
            WatchRewarded("revive", () =>
            {
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
        }

        private void OnHint()
        {
            if (board.Busy || waitingForAd) return;
            WatchRewarded("hint", () => board.ShowHint(session.HintColumn()));
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
                StartLevel(next);
            });
        }

        private void AddCoins(int amount)
        {
            Progress.Coins += amount;
            hud.SetCoins(Progress.Coins);
            hud.RefreshShop();
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
            hud.SetCoins(Progress.Coins);
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
                if (ok) hud.Toast(Loc.T("ads_removed"));
                else hud.Toast(Loc.T("purchase_failed"));
            });
        }

        private void OnAdsRemoved() => hud.SetRemoveAdsVisible(false);

        private void OnDestroy() => Store.AdsRemoved -= OnAdsRemoved;
    }
}
