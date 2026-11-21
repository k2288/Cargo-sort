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
        private const float BeltNextY = 4.2f;
        private const int NextShown = 2;

        // Conveyor belt: a racetrack under the trucks. The bottom run goes left, the top run goes right.
        private const float BeltHalfLength = 4.1f, BeltRadius = 0.72f, BeltCenterY = 2.6f;
        public float BeltSpeed = 2.6f;
        private float beltOffset;
        private readonly List<Transform> beltPlates = new List<Transform>();
        private readonly List<Transform> beltRidges = new List<Transform>();
        private readonly List<Transform> beltSpokes = new List<Transform>();
        private readonly List<SpriteRenderer> beltRim = new List<SpriteRenderer>();

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

        private Vector3 BufferPos(int slot) => IsBelt
            ? (Vector3)BeltPoint(SlotParam(slot))
            : new Vector3((slot - (state.Buffer.Length - 1) / 2f) * BufferSpacing, BufferY, 0f);

        private bool IsBelt => state != null && state.Belt;

        // ---------- Conveyor belt ----------

        private static float BeltLength => 4f * BeltHalfLength + 2f * Mathf.PI * BeltRadius;

        /// <summary>A point on the belt loop, s units along it from the bottom-right corner.</summary>
        private static Vector2 BeltPoint(float s)
        {
            float a = BeltHalfLength, r = BeltRadius, yc = BeltCenterY, half = Mathf.PI * r;
            s = Mathf.Repeat(s, BeltLength);
            if (s < 2f * a) return new Vector2(a - s, yc - r);
            s -= 2f * a;
            if (s < half) { float g = -Mathf.PI / 2f - s / r; return new Vector2(-a + r * Mathf.Cos(g), yc + r * Mathf.Sin(g)); }
            s -= half;
            if (s < 2f * a) return new Vector2(-a + s, yc + r);
            s -= 2f * a;
            float g2 = Mathf.PI / 2f - s / r;
            return new Vector2(a + r * Mathf.Cos(g2), yc + r * Mathf.Sin(g2));
        }

        /// <summary>Where belt spot i is now (spots are spread evenly and move together).</summary>
        private float SlotParam(int slot) => Mathf.Repeat(beltOffset + slot * BeltLength / Mathf.Max(1, state.Buffer.Length), BeltLength);

        /// <summary>The point on the top run right under a truck.</summary>
        private float BayParam(int bay) => 2f * BeltHalfLength + Mathf.PI * BeltRadius + BayPos(bay).x + BeltHalfLength;

        /// <summary>The free belt spot closest to a column's top crate (where a parked crate lands).</summary>
        public int NearestBeltSpot(int column)
        {
            if (!IsBelt || column < 0 || column >= state.Columns.Count) return -1;
            Vector2 from = ColumnPos(column, Mathf.Max(0, state.Columns[column].Count - 1));
            int best = -1;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < state.Buffer.Length; i++)
            {
                if (state.Buffer[i] != GameState.Empty) continue;
                float d = Vector2.Distance(BeltPoint(SlotParam(i)), from);
                if (d < bestDistance) { bestDistance = d; best = i; }
            }
            return best;
        }

        /// <summary>A riding crate that has just passed under a truck of its color, if any.</summary>
        public bool TryFindBeltLoad(out int slot, out int bay)
        {
            slot = bay = -1;
            if (!IsBelt || Busy || state.Status != GameStatus.Playing) return false;
            for (int i = 0; i < state.Buffer.Length; i++)
            {
                int color = state.Buffer[i];
                if (color == GameState.Empty || i >= bufferCrates.Length || bufferCrates[i] == null) continue;
                for (int b = 0; b < state.Bays.Length; b++)
                {
                    var truck = state.Bays[b];
                    if (truck == null || truck.Color != color || truck.Full) continue;
                    float d = SlotParam(i) - BayParam(b);
                    if (d < -0.1f || d >= 2.2f) continue; // just under the truck, or a little past it
                    slot = i;
                    bay = b;
                    return true;
                }
            }
            return false;
        }

        private void Update()
        {
            if (!IsBelt || root == null) return;
            beltOffset += BeltSpeed * Time.deltaTime;
            for (int i = 0; i < beltPlates.Count; i++)
                if (beltPlates[i] != null) beltPlates[i].localPosition = BufferPos(i);
            for (int i = 0; i < bufferCrates.Length && i < state.Buffer.Length; i++)
                if (bufferCrates[i] != null) bufferCrates[i].position = BufferPos(i);
            float step = BeltLength / Mathf.Max(1, beltRidges.Count);
            for (int i = 0; i < beltRidges.Count; i++)
                if (beltRidges[i] != null) beltRidges[i].localPosition = BeltPoint(Mathf.Repeat(beltOffset, step) + i * step);
            float spin = beltOffset / BeltRadius;
            for (int i = 0; i < beltSpokes.Count; i++)
            {
                if (beltSpokes[i] == null) continue;
                float x = i < 3 ? -BeltHalfLength : BeltHalfLength, an = spin + (i % 3) * 2.094f;
                beltSpokes[i].localPosition = new Vector2(x + Mathf.Cos(an) * 0.34f, BeltCenterY + Mathf.Sin(an) * 0.34f);
            }
            var rim = state.FreeBufferSlots == 0 ? new Color32(0xA8, 0x34, 0x2A, 0xFF) : new Color32(0x59, 0x63, 0x6F, 0xFF);
            foreach (var sr in beltRim) if (sr != null) sr.color = rim;
        }

        /// <summary>A rounded racetrack shape around the belt line (box in the middle, circles at the ends).</summary>
        private void Stadium(string name, float band, Color color, int order, bool rim)
        {
            float a = BeltHalfLength, r = BeltRadius, yc = BeltCenterY;
            var mid = Art.Box(root, name, new Vector2(0, yc), new Vector2(2f * a, 2f * r + band), color, order);
            var left = Art.Dot(root, name, new Vector2(-a, yc), 2f * r + band, color, order);
            var right = Art.Dot(root, name, new Vector2(a, yc), 2f * r + band, color, order);
            if (rim) { beltRim.Add(mid); beltRim.Add(left); beltRim.Add(right); }
        }

        /// <summary>The conveyor: rim, rubber band, moving ridges, end rollers, legs and one plate per spot.</summary>
        private void DrawBelt()
        {
            beltPlates.Clear();
            beltRidges.Clear();
            beltSpokes.Clear();
            beltRim.Clear();
            float a = BeltHalfLength, r = BeltRadius, yc = BeltCenterY;
            foreach (float lx in new[] { -a + 0.6f, 0f, a - 0.6f })
                Art.Box(root, "Leg", new Vector2(lx, yc - r - 0.55f), new Vector2(0.2f, 0.5f), Art.Darken(Art.Ground, 0.4f), -3);
            Stadium("BeltRim", CrateSize + 0.5f, new Color32(0x59, 0x63, 0x6F, 0xFF), -2, true);
            Stadium("Belt", CrateSize + 0.3f, new Color32(0x2A, 0x2F, 0x37, 0xFF), -2, false);
            Stadium("BeltTop", CrateSize + 0.06f, new Color32(0x36, 0x3D, 0x47, 0xFF), -2, false);
            Art.Box(root, "BeltGap", new Vector2(0, yc), new Vector2(2f * a, 0.12f), new Color32(0x22, 0x26, 0x2D, 0xFF), -1);

            int ridges = Mathf.RoundToInt(BeltLength / 0.55f);
            for (int i = 0; i < ridges; i++) beltRidges.Add(Art.Dot(root, "Ridge", Vector2.zero, 0.1f, new Color(1, 1, 1, 0.13f), -1).transform);
            foreach (float ex in new[] { -a, a })
            {
                Art.Dot(root, "Roller", new Vector2(ex, yc), 0.5f, new Color32(0x7B, 0x87, 0x94, 0xFF), -1);
                Art.Dot(root, "Hub", new Vector2(ex, yc), 0.22f, new Color32(0xC9, 0xD1, 0xDB, 0xFF), 0);
            }
            for (int i = 0; i < 6; i++) beltSpokes.Add(Art.Dot(root, "Spoke", Vector2.zero, 0.08f, new Color32(0x4B, 0x55, 0x63, 0xFF), 0).transform);
            for (int i = 0; i < state.Buffer.Length; i++)
                beltPlates.Add(Art.Box(root, "Spot", BufferPos(i), new Vector2(CrateSize + 0.06f, CrateSize + 0.06f), new Color(1, 1, 1, 0.1f), 1).transform);
            Update();
        }

        private Vector3 BayPos(int bay) =>
            new Vector3((bay - (state.Bays.Length - 1) / 2f) * BaySpacing, BayY, 0f);

        private static Vector3 TruckSlotLocal(int slot, int capacity) => Art.TruckSlotLocal(slot, capacity);

        /// <summary>Which column a world point is over, or -1.</summary>
        public int HitColumn(Vector2 world)
        {
            if (state == null) return -1;
            float top = IsBelt ? BeltCenterY - BeltRadius - 0.55f : BufferY - 0.7f;
            if (world.y < ColumnBaseY - 0.8f || world.y > top) return -1;
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

            // Dock buffer, or the conveyor belt
            DrawBackdrop();
            if (IsBelt) DrawBelt();
            else
            {
                beltPlates.Clear();
                beltRidges.Clear();
                beltSpokes.Clear();
                beltRim.Clear();
                DrawDock(state.Buffer.Length * BufferSpacing + 0.3f);
            }
            bufferCrates = new Transform[state.Buffer.Length];
            for (int i = 0; i < state.Buffer.Length; i++)
            {
                if (!IsBelt) Art.Box(root, "Slot", BufferPos(i), new Vector2(CrateSize + 0.08f, CrateSize + 0.08f), Art.Slot, 1);
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
                    if (r > 0 && state.Taped[c][r - 1]) AddTape(list[r - 1]);
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
                trucks[b] = CreateTruck(truck.Color, truck.Capacity, truck.Color2);
                trucks[b].Root.position = BayPos(b);
                for (int i = 0; i < truck.Loaded1; i++) AddCrateToTruck(trucks[b], Art.Crate(root, truck.Color, CrateSize), i);
                for (int i = 0; i < truck.Loaded2; i++) AddCrateToTruck(trucks[b], Art.Crate(root, truck.Color2, CrateSize), truck.Need1 + i);
            }
            DrawNextTrucks();
        }

        /// <summary>Tape from a crate up across the crate above it (the pair moves together).</summary>
        private static void AddTape(Transform lower)
        {
            float y = RowSpacing / 2f, length = RowSpacing + 0.42f;
            Art.Box(lower, "TapeShadow", new Vector2(0.03f, y - 0.04f), new Vector2(0.3f, length), new Color(0, 0, 0, 0.22f), 15);
            Art.Box(lower, "Tape", new Vector2(0, y), new Vector2(0.3f, length), new Color32(0xE8, 0xCF, 0x8F, 0xFF), 16);
            Art.Box(lower, "TapeShine", new Vector2(0, y), new Vector2(0.08f, length), new Color(1, 1, 1, 0.35f), 17);
            Art.Box(lower, "TapeBand", new Vector2(0, y), new Vector2(0.52f, 0.12f), new Color32(0xD6, 0xB8, 0x6A, 0xFF), 17);
        }

        /// <summary>The next trucks in line, so players can plan which crates to dig out.</summary>
        private void DrawNextTrucks()
        {
            if (nextRoot != null) Destroy(nextRoot.gameObject);
            nextRoot = new GameObject("NextTrucks").transform;
            nextRoot.SetParent(root, false);
            var next = state.UpcomingTruckInfo(NextShown);
            if (next.Count == 0) return;

            float ny = IsBelt ? BeltNextY : NextY;
            Art.Label(nextRoot, Loc.T("next_short"), new Vector2(-2.1f, ny), 0.42f, new Color(1, 1, 1, 0.7f), 5);
            for (int i = 0; i < next.Count; i++)
            {
                var info = next[i];
                var c = Art.CrateColor(info.Color);
                bool big = info.Capacity >= LevelGenerator.BigTruckCapacity;
                float x = -0.6f + i * 1.5f;
                float bedWidth = big ? 0.98f : 0.78f, bedX = big ? x - 0.1f : x;
                float alpha = i == 0 ? 1f : 0.6f;
                Color A(Color col) => new Color(col.r, col.g, col.b, col.a * alpha);
                Art.Box(nextRoot, "MiniShadow", new Vector2(x + 0.05f, ny - 0.3f), new Vector2(big ? 1.25f : 1.05f, 0.12f), A(new Color(0, 0, 0, 0.2f)), 4);
                if (info.TwoColor)
                {
                    Art.Box(nextRoot, "MiniBed", new Vector2(bedX - bedWidth / 4f, ny + 0.02f), new Vector2(bedWidth / 2f, 0.42f), A(c), 5);
                    Art.Box(nextRoot, "MiniBed2", new Vector2(bedX + bedWidth / 4f, ny + 0.02f), new Vector2(bedWidth / 2f, 0.42f), A(Art.CrateColor(info.Color2)), 5);
                }
                else Art.Box(nextRoot, "MiniBed", new Vector2(bedX, ny + 0.02f), new Vector2(bedWidth, 0.42f), A(c), 5);
                if (big) Art.Label(nextRoot, info.Capacity.ToString(), new Vector2(bedX, ny + 0.04f), 0.3f, A(Color.white), 7);
                Art.Box(nextRoot, "MiniStripe", new Vector2(bedX, ny - 0.15f), new Vector2(bedWidth, 0.07f), A(Art.Darken(c, 0.25f)), 6);
                Art.Box(nextRoot, "MiniCab", new Vector2(x + 0.55f, ny - 0.02f), new Vector2(0.3f, 0.34f), A(Art.Darken(c, 0.15f)), 5);
                Art.Box(nextRoot, "MiniWindow", new Vector2(x + 0.6f, ny + 0.06f), new Vector2(0.14f, 0.12f), A(Art.Window), 6);
                Art.DrawWheel(nextRoot, new Vector2(x - 0.2f, ny - 0.24f), 0.2f, 7);
                Art.DrawWheel(nextRoot, new Vector2(x + 0.5f, ny - 0.24f), 0.2f, 7);
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
        private TruckView CreateTruck(int color, int capacity, int color2 = -1)
        {
            var view = new TruckView { Capacity = capacity, Root = new GameObject("Truck").transform };
            view.Root.SetParent(root, false);
            view.Body = new GameObject("Body").transform;
            view.Body.SetParent(view.Root, false);
            Art.BuildSprites(view.Root, Art.TruckShapes(color, capacity, Progress.TruckSkin, color2), view.Body);
            return view;
        }

        private void AddCrateToTruck(TruckView truck, Transform crate, int spot)
        {
            crate.SetParent(truck.Body, true);
            crate.localRotation = Quaternion.identity;
            crate.localPosition = TruckSlotLocal(spot, truck.Capacity);
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
                    case EventKind.TruckArrive: yield return Arrive(e.Bay, e.Color, e.Capacity > 0 ? e.Capacity : s.TruckCapacity, e.Color2); break;
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
            int spot = 0;
            if (e.To == Place.Bay)
            {
                truck = trucks[e.ToIndex];
                if (truck == null) yield break;
                spot = e.ToSlot >= 0 ? e.ToSlot : truck.Crates.Count;
                target = truck.Body.TransformPoint(TruckSlotLocal(spot, truck.Capacity));
            }
            else
            {
                target = BufferPos(e.ToIndex);
            }
            bool live = truck == null && IsBelt; // belt spots keep moving during the flight

            Art.SetLift(crate, true);
            Vector3 start = crate.position;
            float arc = 0.6f + Vector3.Distance(start, target) * 0.06f;
            float spin = (target.x > start.x ? -1f : 1f) * 20f;
            Vector3 baseScale = crate.localScale;
            for (float t = 0; t < 1f; t += Time.deltaTime / MoveTime)
            {
                float k = EaseOut(t);
                if (live) target = BufferPos(e.ToIndex);
                crate.position = Vector3.Lerp(start, target, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * arc;
                crate.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(k * Mathf.PI) * spin);
                crate.localScale = baseScale * (1f + 0.06f * Mathf.Sin(k * Mathf.PI));
                yield return null;
            }
            if (live) target = BufferPos(e.ToIndex);
            crate.position = target;
            crate.localRotation = Quaternion.identity;
            crate.localScale = baseScale;
            Art.SetLift(crate, false);

            if (truck != null)
            {
                AddCrateToTruck(truck, crate, spot);
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

        private IEnumerator Arrive(int bay, int color, int capacity, int color2)
        {
            var truck = CreateTruck(color, capacity, color2);
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
