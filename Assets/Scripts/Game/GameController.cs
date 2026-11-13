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

        private Camera cam;
        private BoardView board;
        private Hud hud;
        private IAdsService ads;
        private GameSession session;
        private int level;
        private int freeUndos;
        private int bonusSlots;
        private bool waitingForAd;

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
            hud.Build();
            hud.Undo += OnUndo;
            hud.AddSlot += OnAddSlot;
            hud.Hint += OnHint;
            hud.Restart += () => { if (!board.Busy && !waitingForAd) StartLevel(level); };
            hud.Retry += () => StartLevel(level);
            hud.Revive += OnRevive;
            hud.Next += OnNext;
            hud.RemoveAds += OnRemoveAds;
            hud.SetRemoveAdsVisible(!Progress.AdsRemoved);

            StartLevel(Progress.Level);
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
            hud.ShowWin(false, n);
            hud.ShowLose(false);
            hud.SetLevel(n);
            board.Rebuild(session.State);
            RefreshHud();
        }

        private void RefreshHud()
        {
            hud.SetTrucks(session.State.TrucksShipped, session.State.TrucksTotal);
            hud.SetUndoLabel(freeUndos > 0 ? $"Undo ({freeUndos})" : "Undo (AD)");
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
                hud.ShowWin(true, level);
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
                hud.Toast("Dock is at max size");
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
                hud.Toast("No ad available, try again soon");
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

        private void OnNext()
        {
            int next = level + 1;
            bool showAd = !Progress.AdsRemoved && next >= FirstInterstitialLevel && (next - FirstInterstitialLevel) % InterstitialEveryLevels == 0;
            if (!showAd)
            {
                StartLevel(next);
                return;
            }
            hud.ShowWin(false, level);
            waitingForAd = true;
            ads.ShowInterstitial("level_end", () =>
            {
                waitingForAd = false;
                StartLevel(next);
            });
        }

        private void OnRemoveAds()
        {
            if (waitingForAd) return;
            Store.BuyRemoveAds(ok =>
            {
                if (ok) hud.Toast("Ads removed. Thank you!");
                else hud.Toast("Purchase didn't go through");
            });
        }

        private void OnAdsRemoved() => hud.SetRemoveAdsVisible(false);

        private void OnDestroy() => Store.AdsRemoved -= OnAdsRemoved;
    }
}
