using System;
using System.Collections;
using System.Collections.Generic;
using CargoSort.Core;
using UnityEngine;

namespace CargoSort.Game
{
    /// <summary>Draws the GameState with sprites and animates the events a move produces.</summary>
    public sealed class BoardView : MonoBehaviour
    {
        public const float CrateSize = 0.86f;
        private const float ColumnSpacing = 1.25f;
        private const float RowSpacing = 0.95f;
        private const float ColumnBaseY = -6.2f;
        private const float BufferY = 2.6f;
        private const float BayY = 5.5f;
        private const float BaySpacing = 4.7f;
        private const float OffscreenX = 9f;
        private const float NextY = 3.9f;
        private const int NextShown = 2;

        public float MoveTime = 0.16f;
        public float TruckTime = 0.24f;

        private GameState state;
        private Transform root;
        private readonly List<List<Transform>> columnCrates = new List<List<Transform>>();
        private Transform[] bufferCrates = new Transform[0];
        private TruckView[] trucks = new TruckView[0];
        private Transform hintMarker;
        private Transform nextRoot;
        private Transform confetti;
        private readonly List<List<bool>> shownHidden = new List<List<bool>>();
        private readonly Dictionary<int, (Transform root, TextMesh count)> ice = new Dictionary<int, (Transform, TextMesh)>();

        private sealed class TruckView
        {
            public Transform Root;
            public Transform Body; // everything above the wheels; it bounces when crates land
            public int Capacity;
            public readonly List<Transform> Crates = new List<Transform>();
        }

        public bool Busy { get; private set; }

        // ---------- Layout ----------

        private Vector3 ColumnPos(int column, int row)
        {
            float x = (column - (state.Columns.Count - 1) / 2f) * ColumnSpacing;
            return new Vector3(x, ColumnBaseY + row * RowSpacing, 0f);
        }

        private float BufferSpacing => Mathf.Min(1.15f, 9.4f / Mathf.Max(1, state.Buffer.Length));

        private Vector3 BufferPos(int slot) =>
            new Vector3((slot - (state.Buffer.Length - 1) / 2f) * BufferSpacing, BufferY, 0f);

        private Vector3 BayPos(int bay) =>
            new Vector3((bay - (state.Bays.Length - 1) / 2f) * BaySpacing, BayY, 0f);

        private static Vector3 TruckSlotLocal(int slot, int capacity) => Art.TruckSlotLocal(slot, capacity);

        /// <summary>Which column a world point is over, or -1.</summary>
        public int HitColumn(Vector2 world)
        {
            if (state == null) return -1;
            if (world.y < ColumnBaseY - 0.8f || world.y > BufferY - 0.7f) return -1;
            for (int c = 0; c < state.Columns.Count; c++)
                if (Mathf.Abs(world.x - ColumnPos(c, 0).x) <= ColumnSpacing / 2f) return c;
            return -1;
        }

        /// <summary>Which crate row of a column a world point is over (for the crane), clamped to the stack.</summary>
        public int HitRow(Vector2 world, int column)
        {
            if (state == null || column < 0 || column >= state.Columns.Count) return -1;
            int count = state.Columns[column].Count;
            if (count == 0) return -1;
            return Mathf.Clamp(Mathf.RoundToInt((world.y - ColumnBaseY) / RowSpacing), 0, count - 1);
        }

        // ---------- Build ----------

        public void Rebuild(GameState s)
        {
            StopAllCoroutines();
            if (confetti != null) Destroy(confetti.gameObject);
            confetti = null;
            Redraw(s);
        }

        private void Redraw(GameState s)
        {
            Busy = false;
            state = s;
            if (root != null) Destroy(root.gameObject);
            root = new GameObject("Board").transform;
            root.SetParent(transform, false);
            hintMarker = null;

            // Dock buffer
            DrawBackdrop();
            DrawDock(state.Buffer.Length * BufferSpacing + 0.3f);
            bufferCrates = new Transform[state.Buffer.Length];
            for (int i = 0; i < state.Buffer.Length; i++)
            {
                Art.Box(root, "Slot", BufferPos(i), new Vector2(CrateSize + 0.08f, CrateSize + 0.08f), Art.Slot, 1);
                if (state.Buffer[i] != GameState.Empty)
                {
                    bufferCrates[i] = Art.Crate(root, state.Buffer[i], CrateSize);
                    bufferCrates[i].position = BufferPos(i);
                }
            }

            // Columns
            columnCrates.Clear();
            shownHidden.Clear();
            ice.Clear();
            int tallest = 1;
            foreach (var col in state.Columns) tallest = Mathf.Max(tallest, col.Count);
            for (int c = 0; c < state.Columns.Count; c++)
            {
                var pallet = ColumnPos(c, 0);
                float h = tallest * RowSpacing + 0.25f;
                Art.Box(root, "Lane", new Vector2(pallet.x, ColumnBaseY - 0.55f + h / 2f), new Vector2(ColumnSpacing - 0.15f, h), Art.Ground, 0);
                Art.Box(root, "LaneInner", new Vector2(pallet.x, ColumnBaseY - 0.55f + h / 2f), new Vector2(ColumnSpacing - 0.43f, h - 0.28f), Art.Darken(Art.Ground, 0.12f), 0);

                var list = new List<Transform>();
                var hidden = new List<bool>();
                for (int r = 0; r < state.Columns[c].Count; r++)
                {
                    bool isHidden = state.IsHidden(c, r);
                    var crate = isHidden ? Art.HiddenCrate(root, CrateSize) : Art.Crate(root, state.Columns[c][r], CrateSize);
                    crate.position = ColumnPos(c, r);
                    list.Add(crate);
                    hidden.Add(isHidden);
                }
                columnCrates.Add(list);
                shownHidden.Add(hidden);
            }
            RefreshTwists(false);

            // Trucks
            trucks = new TruckView[state.Bays.Length];
            for (int b = 0; b < state.Bays.Length; b++)
            {
                Art.Box(root, "BayMark", BayPos(b) + new Vector3(0, -0.95f), new Vector2(BaySpacing - 0.4f, 0.06f), new Color(1, 1, 1, 0.18f), -1);
                var truck = state.Bays[b];
                if (truck == null) continue;
                trucks[b] = CreateTruck(truck.Color, truck.Capacity);
                trucks[b].Root.position = BayPos(b);
                for (int i = 0; i < truck.Loaded; i++) AddCrateToTruck(trucks[b], Art.Crate(root, truck.Color, CrateSize));
            }
            DrawNextTrucks();
        }

        /// <summary>The next trucks in line, so players can plan which crates to dig out.</summary>
        private void DrawNextTrucks()
        {
            if (nextRoot != null) Destroy(nextRoot.gameObject);
            nextRoot = new GameObject("NextTrucks").transform;
            nextRoot.SetParent(root, false);
            var next = state.UpcomingTrucks(NextShown);
            if (next.Count == 0) return;

            Art.Label(nextRoot, Loc.T("next_short"), new Vector2(-2.1f, NextY), 0.42f, new Color(1, 1, 1, 0.7f), 5);
            for (int i = 0; i < next.Count; i++)
            {
                var c = Art.CrateColor(next[i]);
                float x = -0.6f + i * 1.5f;
                float alpha = i == 0 ? 1f : 0.6f;
                Color A(Color col) => new Color(col.r, col.g, col.b, col.a * alpha);
                Art.Box(nextRoot, "MiniShadow", new Vector2(x + 0.05f, NextY - 0.3f), new Vector2(1.05f, 0.12f), A(new Color(0, 0, 0, 0.2f)), 4);
                Art.Box(nextRoot, "MiniBed", new Vector2(x, NextY + 0.02f), new Vector2(0.78f, 0.42f), A(c), 5);
                Art.Box(nextRoot, "MiniStripe", new Vector2(x, NextY - 0.15f), new Vector2(0.78f, 0.07f), A(Art.Darken(c, 0.25f)), 6);
                Art.Box(nextRoot, "MiniCab", new Vector2(x + 0.55f, NextY - 0.02f), new Vector2(0.3f, 0.34f), A(Art.Darken(c, 0.15f)), 5);
                Art.Box(nextRoot, "MiniWindow", new Vector2(x + 0.6f, NextY + 0.06f), new Vector2(0.14f, 0.12f), A(Art.Window), 6);
                Art.DrawWheel(nextRoot, new Vector2(x - 0.2f, NextY - 0.24f), 0.2f, 7);
                Art.DrawWheel(nextRoot, new Vector2(x + 0.5f, NextY - 0.24f), 0.2f, 7);
            }
        }

        /// <summary>Reveals "?" crates that reached the top and updates the ice on frozen columns.</summary>
        private void RefreshTwists(bool animate)
        {
            for (int c = 0; c < columnCrates.Count && c < state.Columns.Count; c++)
            {
                var list = columnCrates[c];
                var hidden = shownHidden[c];
                while (hidden.Count > list.Count) hidden.RemoveAt(hidden.Count - 1);
                for (int r = 0; r < list.Count; r++)
                {
                    if (!hidden[r] || state.IsHidden(c, r)) continue;
                    var pos = list[r].position;
                    Destroy(list[r].gameObject);
                    list[r] = Art.Crate(root, state.Columns[c][r], CrateSize);
                    list[r].position = pos;
                    hidden[r] = false;
                    if (animate) Feedback.Revealed();
                }

                int taps = state.Frozen[c];
                if (taps > 0)
                {
                    if (!ice.TryGetValue(c, out var block))
                    {
                        var top = ColumnPos(c, Mathf.Max(0, state.Columns[c].Count - 1));
                        var iceRoot = new GameObject("Ice").transform;
                        iceRoot.SetParent(root, false);
                        iceRoot.position = top;
                        var sr = Art.Box(iceRoot, "IceBlock", Vector2.zero, new Vector2(CrateSize + 0.18f, CrateSize + 0.18f), new Color(0.72f, 0.9f, 1f, 0.82f), 20);
                        sr.sortingOrder = 20;
                        var count = Art.Label(iceRoot, "", new Vector2(0, 0.02f), 0.6f, new Color32(0x1F, 0x4E, 0x79, 0xFF), 21);
                        block = (iceRoot, count);
                        ice[c] = block;
                    }
                    block.count.text = taps.ToString();
                }
                else if (ice.TryGetValue(c, out var melted))
                {
                    Destroy(melted.root.gameObject);
                    ice.Remove(c);
                    if (animate) Feedback.IceMelted();
                }
            }
        }

        /// <summary>Flatbed truck: side rails, cab with window, headlight and roof light, and hubcap wheels.</summary>
        private TruckView CreateTruck(int color, int capacity)
        {
            var view = new TruckView { Capacity = capacity, Root = new GameObject("Truck").transform };
            view.Root.SetParent(root, false);
            view.Body = new GameObject("Body").transform;
            view.Body.SetParent(view.Root, false);
            Art.BuildSprites(view.Root, Art.TruckShapes(color, capacity, Progress.TruckSkin), view.Body);
            return view;
        }

        private void AddCrateToTruck(TruckView truck, Transform crate)
        {
            crate.SetParent(truck.Body, true);
            crate.localRotation = Quaternion.identity;
            crate.localPosition = TruckSlotLocal(truck.Crates.Count, truck.Capacity);
            crate.localScale = Vector3.one * 0.95f;
            truck.Crates.Add(crate);
        }

        // ---------- Animation ----------

        /// <summary>Animates the events, then redraws from the state to stay exactly in sync.</summary>
        public void Play(List<GameEvent> events, GameState s, Action done)
        {
            ClearHint();
            StartCoroutine(PlayRoutine(events, s, done));
        }

        private IEnumerator PlayRoutine(List<GameEvent> events, GameState s, Action done)
        {
            Busy = true;
            foreach (var e in events)
            {
                switch (e.Kind)
                {
                    case EventKind.MoveCrate: yield return MoveCrate(e); break;
                    case EventKind.TruckDepart: yield return Depart(e.Bay); break;
                    case EventKind.TruckArrive: yield return Arrive(e.Bay, e.Color, s.TruckCapacity); break;
                }
            }
            state = s;
            RefreshTwists(true);
            Redraw(s);
            done?.Invoke();
        }

        private IEnumerator MoveCrate(GameEvent e)
        {
            Transform crate;
            if (e.From == Place.Column)
            {
                var list = columnCrates[e.FromIndex];
                int row = e.FromRow >= 0 && e.FromRow < list.Count ? e.FromRow : list.Count - 1;
                crate = list[row];
                list.RemoveAt(row);
                var hidden = shownHidden[e.FromIndex];
                if (row < hidden.Count) hidden.RemoveAt(row);
                // Crane lifted a crate from inside the stack: the crates above it drop down.
                for (int r = row; r < list.Count; r++)
                    if (list[r] != null) StartCoroutine(Slide(list[r], ColumnPos(e.FromIndex, r), MoveTime));
            }
            else
            {
                crate = bufferCrates[e.FromIndex];
                bufferCrates[e.FromIndex] = null;
            }
            if (crate == null) yield break;

            Vector3 target;
            TruckView truck = null;
            if (e.To == Place.Bay)
            {
                truck = trucks[e.ToIndex];
                if (truck == null) yield break;
                target = truck.Body.TransformPoint(TruckSlotLocal(truck.Crates.Count, truck.Capacity));
            }
            else
            {
                target = BufferPos(e.ToIndex);
            }

            Art.SetLift(crate, true);
            Vector3 start = crate.position;
            float arc = 0.6f + Vector3.Distance(start, target) * 0.06f;
            float spin = (target.x > start.x ? -1f : 1f) * 20f;
            Vector3 baseScale = crate.localScale;
            for (float t = 0; t < 1f; t += Time.deltaTime / MoveTime)
            {
                float k = EaseOut(t);
                crate.position = Vector3.Lerp(start, target, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * arc;
                crate.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(k * Mathf.PI) * spin);
                crate.localScale = baseScale * (1f + 0.06f * Mathf.Sin(k * Mathf.PI));
                yield return null;
            }
            crate.position = target;
            crate.localRotation = Quaternion.identity;
            crate.localScale = baseScale;
            Art.SetLift(crate, false);

            if (truck != null)
            {
                AddCrateToTruck(truck, crate);
                Feedback.CrateLoaded(truck.Crates.Count - 1);
                Puff(target + Vector3.down * 0.4f, 4, 0.5f);
                StartCoroutine(Squash(crate));
                yield return Bob(truck.Body, -0.09f, 0.22f);
            }
            else
            {
                bufferCrates[e.ToIndex] = crate;
                Feedback.CrateParked();
                Puff(target + Vector3.down * 0.45f, 5, 0.7f);
                StartCoroutine(Squash(crate));
            }
        }

        private IEnumerator Depart(int bay)
        {
            var truck = trucks[bay];
            trucks[bay] = null;
            if (truck == null) yield break;
            Feedback.TruckLeft();
            Puff(truck.Root.position + new Vector3(-1.6f, -0.6f), 6, 0.3f, new Color(0.8f, 0.8f, 0.8f, 0.6f));
            // Pull away slowly, then speed up, rocking on the springs.
            Vector3 from = truck.Root.position;
            for (float k = 0; k < 1f; k += Time.deltaTime / (TruckTime * 1.35f))
            {
                truck.Root.position = from + Vector3.right * Mathf.Pow(k, 1.6f) * (OffscreenX + 1f);
                truck.Body.localPosition = Vector3.up * 0.05f * Mathf.Sin(k * 9f) * (1f - k);
                yield return null;
            }
            Destroy(truck.Root.gameObject);
        }

        private IEnumerator Arrive(int bay, int color, int capacity)
        {
            var truck = CreateTruck(color, capacity);
            trucks[bay] = truck;
            truck.Root.position = BayPos(bay) + Vector3.left * OffscreenX;
            yield return Slide(truck.Root, BayPos(bay), TruckTime * 1.25f);
            StartCoroutine(Bob(truck.Body, -0.08f, 0.25f));
        }

        private static IEnumerator Slide(Transform t, Vector3 target, float time)
        {
            if (t == null) yield break;
            Vector3 start = t.position;
            for (float k = 0; k < 1f; k += Time.deltaTime / time)
            {
                if (t == null) yield break;
                t.position = Vector3.Lerp(start, target, EaseOut(k));
                yield return null;
            }
            if (t != null) t.position = target;
        }

        /// <summary>Moves a part down and back up once (truck springs).</summary>
        private static IEnumerator Bob(Transform t, float depth, float time)
        {
            for (float k = 0; k < 1f; k += Time.deltaTime / time)
            {
                if (t == null) yield break;
                t.localPosition = Vector3.up * depth * Mathf.Sin(k * Mathf.PI);
                yield return null;
            }
            if (t != null) t.localPosition = Vector3.zero;
        }

        /// <summary>A landed crate flattens a little and springs back, pivoting on its bottom edge.</summary>
        private static IEnumerator Squash(Transform t)
        {
            Vector3 baseScale = t.localScale;
            Vector3 basePos = t.localPosition;
            float half = CrateSize * 0.5f;
            var parent = t.parent;
            for (float k = 0; k < 1f; k += Time.deltaTime / 0.22f)
            {
                if (t == null || t.parent != parent) yield break;
                float a = 0.18f * (1f - k);
                t.localScale = new Vector3(baseScale.x * (1f + a), baseScale.y * (1f - a), 1f);
                t.localPosition = basePos + Vector3.down * half * a * baseScale.y;
                yield return null;
            }
            if (t == null || t.parent != parent) yield break;
            t.localScale = baseScale;
            t.localPosition = basePos;
        }

        /// <summary>Little dust puffs that float up and fade.</summary>
        private void Puff(Vector3 at, int count, float spread, Color? color = null)
        {
            var c = color ?? new Color(1, 1, 1, 0.5f);
            for (int i = 0; i < count; i++)
            {
                var dot = Art.Dot(root, "Puff", at + Vector3.right * UnityEngine.Random.Range(-spread, spread) * 0.5f,
                    UnityEngine.Random.Range(0.28f, 0.56f), c, 45);
                StartCoroutine(PuffRoutine(dot, new Vector3(UnityEngine.Random.Range(-1.1f, 1.1f), UnityEngine.Random.Range(0.4f, 1.6f))));
            }
        }

        private static IEnumerator PuffRoutine(SpriteRenderer dot, Vector3 velocity)
        {
            var t = dot.transform;
            Vector3 size = t.localScale;
            Color c = dot.color;
            for (float k = 0; k < 1f; k += Time.deltaTime / 0.5f)
            {
                if (dot == null) yield break;
                t.position += velocity * Time.deltaTime;
                t.localScale = size * (1f + 0.6f * k);
                dot.color = new Color(c.r, c.g, c.b, c.a * (1f - k));
                yield return null;
            }
            if (dot != null) Destroy(dot.gameObject);
        }

        // ---------- Scenery ----------

        /// <summary>Lighter sky behind the trucks, far-off cranes and containers, a water line and the road.</summary>
        private void DrawBackdrop()
        {
            var bg = Art.Background;
            float roadY = BayY - 1.05f;
            // Sky: bands that get lighter towards the top (a cheap gradient).
            for (int i = 0; i < 6; i++)
                Art.Box(root, "Sky", new Vector2(0, roadY + 0.4f + i * 1.4f + 0.7f), new Vector2(40f, 1.42f), Art.Lighten(bg, 0.03f * (i + 1)), -20);
            var far = Art.Darken(bg, 0.18f);
            far.a = 0.85f;
            float[,] cranes = { { -4.6f, 2.2f }, { 3.9f, 2.6f } };
            for (int i = 0; i < 2; i++)
            {
                float x = cranes[i, 0], h = cranes[i, 1];
                Art.Box(root, "FarCrane", new Vector2(x, roadY + 0.55f + h / 2f), new Vector2(0.16f, h), far, -15);
                Art.Box(root, "FarArm", new Vector2(x + 0.55f, roadY + 0.55f + h), new Vector2(1.6f, 0.14f), far, -15);
                Art.Box(root, "FarWeight", new Vector2(x - 0.3f, roadY + 0.45f + h), new Vector2(0.45f, 0.25f), far, -15);
            }
            float[,] boxes = { { -2.6f, 0 }, { -1.3f, 0 }, { -2.0f, 1 }, { 1.4f, 0 }, { 2.7f, 0 }, { 2.0f, 1 } };
            for (int i = 0; i < boxes.GetLength(0); i++)
                Art.Box(root, "FarContainer", new Vector2(boxes[i, 0], roadY + 0.75f + boxes[i, 1] * 0.42f), new Vector2(1.22f, 0.38f), far, -15);
            Art.Box(root, "Water", new Vector2(0, roadY + 0.35f), new Vector2(40f, 0.22f), Art.Darken(bg, 0.28f), -14);
            Art.Box(root, "Road", new Vector2(0, roadY + 0.02f), new Vector2(40f, 0.48f), Art.Darken(bg, 0.38f), -14);
            for (int i = -10; i <= 10; i++)
                Art.Box(root, "Dash", new Vector2(i, roadY + 0.02f), new Vector2(0.45f, 0.06f), new Color(1, 1, 1, 0.35f), -13);
            // The yard gets a little darker towards the bottom.
            for (int i = 0; i < 5; i++)
                Art.Box(root, "Shade", new Vector2(0, roadY - 0.22f - 3.5f * i - 1.75f), new Vector2(40f, 3.5f), new Color(0, 0, 0, 0.045f * (i + 1)), -19);
        }

        /// <summary>Wooden dock with posts.</summary>
        private void DrawDock(float width)
        {
            var wood = Art.Ground;
            Art.Box(root, "DockShadow", new Vector2(0, BufferY - 0.72f), new Vector2(width, 0.18f), new Color(0, 0, 0, 0.25f), -2);
            Art.Box(root, "Post", new Vector2(-width / 2f + 0.35f, BufferY - 0.86f), new Vector2(0.22f, 0.4f), Art.Darken(wood, 0.35f), -2);
            Art.Box(root, "Post", new Vector2(width / 2f - 0.35f, BufferY - 0.86f), new Vector2(0.22f, 0.4f), Art.Darken(wood, 0.35f), -2);
            Art.Box(root, "Dock", new Vector2(0, BufferY), new Vector2(width, 1.25f), wood, 0);
            Art.Box(root, "DockEdge", new Vector2(0, BufferY + 0.56f), new Vector2(width - 0.2f, 0.06f), Art.Lighten(wood, 0.18f), 0);
            Art.Box(root, "DockEdge", new Vector2(0, BufferY - 0.56f), new Vector2(width - 0.2f, 0.06f), Art.Darken(wood, 0.2f), 0);
        }

        private static float EaseOut(float t) => 1f - (1f - Mathf.Clamp01(t)) * (1f - Mathf.Clamp01(t));

        /// <summary>Wiggles a column when the tap is not allowed.</summary>
        public void Shake(int column)
        {
            if (column < 0 || column >= columnCrates.Count) return;
            var list = columnCrates[column];
            if (list.Count == 0) return;
            StartCoroutine(ShakeRoutine(list[list.Count - 1]));
        }

        private static IEnumerator ShakeRoutine(Transform t)
        {
            Vector3 start = t.position;
            for (float k = 0; k < 1f; k += Time.deltaTime / 0.25f)
            {
                if (t == null) yield break;
                t.position = start + Vector3.right * Mathf.Sin(k * Mathf.PI * 6f) * 0.12f * (1f - k);
                yield return null;
            }
            if (t != null) t.position = start;
        }

        /// <summary>Shows a bouncing arrow above a column (hint booster).</summary>
        public void ShowHint(int column)
        {
            ClearHint();
            if (column < 0 || column >= columnCrates.Count) return;
            int top = Mathf.Max(0, columnCrates[column].Count);
            hintMarker = Art.Dot(root, "Hint", ColumnPos(column, top) + Vector3.up * 0.1f, 0.45f, Color.white, 50).transform;
            StartCoroutine(Bounce(hintMarker));
        }

        /// <summary>Tutorial: a hand pointing at the column to tap, with a pulsing ring on its top crate.</summary>
        public void ShowTutorialHand(int column)
        {
            ClearHint();
            if (column < 0 || column >= columnCrates.Count) return;
            int top = Mathf.Max(0, columnCrates[column].Count - 1);
            var target = ColumnPos(column, top);
            hintMarker = new GameObject("TutorialHand").transform;
            hintMarker.SetParent(root, false);
            hintMarker.position = target;

            var ring = Art.Dot(hintMarker, "Ring", Vector2.zero, 1.25f, new Color(1, 1, 1, 0.35f), 9);
            var hand = new GameObject("Hand").transform;
            hand.SetParent(hintMarker, false);
            var outline = new Color32(0x1B, 0x1B, 0x1B, 0xFF);
            Art.Box(hand, "PalmOutline", new Vector2(0, 1.25f), new Vector2(0.72f, 0.66f), outline, 50);
            Art.Box(hand, "Palm", new Vector2(0, 1.25f), new Vector2(0.6f, 0.54f), Color.white, 51);
            Art.Box(hand, "FingerOutline", new Vector2(-0.12f, 0.72f), new Vector2(0.3f, 0.66f), outline, 50);
            Art.Box(hand, "Finger", new Vector2(-0.12f, 0.74f), new Vector2(0.2f, 0.58f), Color.white, 52);
            StartCoroutine(TutorialPulse(ring.transform, hand));
        }

        private static IEnumerator TutorialPulse(Transform ring, Transform hand)
        {
            while (ring != null && hand != null)
            {
                float k = Mathf.Repeat(Time.time * 1.4f, 1f);
                ring.localScale = Vector3.one * (0.8f + k * 0.7f);
                var sr = ring.GetComponent<SpriteRenderer>();
                sr.color = new Color(1, 1, 1, 0.45f * (1f - k));
                hand.localPosition = Vector3.up * Mathf.Abs(Mathf.Sin(Time.time * 4f)) * 0.3f;
                yield return null;
            }
        }

        /// <summary>Level start: trucks drive in and the crate stacks pop up.</summary>
        public void PlayIntro()
        {
            StartCoroutine(IntroRoutine());
        }

        private IEnumerator IntroRoutine()
        {
            Busy = true;
            var crates = new List<Transform>();
            foreach (var list in columnCrates) crates.AddRange(list);
            foreach (var c in crates) c.localScale = Vector3.zero;
            for (int b = 0; b < trucks.Length; b++)
                if (trucks[b] != null) trucks[b].Root.position = BayPos(b) + Vector3.left * OffscreenX;

            for (float t = 0; t < 1f; t += Time.deltaTime / 0.45f)
            {
                for (int i = 0; i < crates.Count; i++)
                {
                    if (crates[i] == null) continue;
                    float k = Mathf.Clamp01(t * 1.6f - (i % 8) * 0.06f);
                    crates[i].localScale = Vector3.one * BackOut(k);
                }
                for (int b = 0; b < trucks.Length; b++)
                    if (trucks[b] != null) trucks[b].Root.position = Vector3.Lerp(BayPos(b) + Vector3.left * OffscreenX, BayPos(b), EaseOut(t));
                yield return null;
            }
            foreach (var c in crates) if (c != null) c.localScale = Vector3.one;
            for (int b = 0; b < trucks.Length; b++)
                if (trucks[b] != null) trucks[b].Root.position = BayPos(b);
            Busy = false;
        }

        private static float BackOut(float k)
        {
            k = Mathf.Clamp01(k);
            const float s = 1.7f;
            k -= 1f;
            return k * k * ((s + 1f) * k + s) + 1f;
        }

        /// <summary>Win: a burst of confetti in the crate colors.</summary>
        public void Confetti()
        {
            StartCoroutine(ConfettiRoutine());
        }

        private IEnumerator ConfettiRoutine()
        {
            if (confetti != null) Destroy(confetti.gameObject);
            var host = new GameObject("Confetti").transform;
            host.SetParent(transform, false);
            confetti = host;
            const int count = 60;
            var bits = new Transform[count];
            var vel = new Vector3[count];
            var spin = new float[count];
            for (int i = 0; i < count; i++)
            {
                var sr = Art.Box(host, "Bit", new Vector2(UnityEngine.Random.Range(-1.5f, 1.5f), 0f),
                    new Vector2(0.22f, 0.12f), Art.CrateColor(i), 60);
                bits[i] = sr.transform;
                float angle = UnityEngine.Random.Range(50f, 130f) * Mathf.Deg2Rad;
                float speed = UnityEngine.Random.Range(9f, 16f);
                vel[i] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
                spin[i] = UnityEngine.Random.Range(-720f, 720f);
            }
            for (float t = 0; t < 2.2f; t += Time.deltaTime)
            {
                for (int i = 0; i < count; i++)
                {
                    vel[i] += Vector3.down * 18f * Time.deltaTime;
                    vel[i] *= 1f - 0.8f * Time.deltaTime;
                    bits[i].position += vel[i] * Time.deltaTime;
                    bits[i].Rotate(0, 0, spin[i] * Time.deltaTime);
                }
                yield return null;
            }
            Destroy(host.gameObject);
            confetti = null;
        }

        private void ClearHint()
        {
            if (hintMarker != null) Destroy(hintMarker.gameObject);
            hintMarker = null;
        }

        private static IEnumerator Bounce(Transform t)
        {
            Vector3 start = t.position;
            while (t != null)
            {
                t.position = start + Vector3.up * Mathf.Abs(Mathf.Sin(Time.time * 6f)) * 0.3f;
                yield return null;
            }
        }
    }
}
