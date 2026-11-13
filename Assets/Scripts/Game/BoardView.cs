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
        private const float SlotSpacing = 0.95f;
        private const float OffscreenX = 9f;

        public float MoveTime = 0.16f;
        public float TruckTime = 0.24f;

        private GameState state;
        private Transform root;
        private readonly List<List<Transform>> columnCrates = new List<List<Transform>>();
        private Transform[] bufferCrates = new Transform[0];
        private TruckView[] trucks = new TruckView[0];
        private Transform hintMarker;

        private sealed class TruckView
        {
            public Transform Root;
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

        private static Vector3 TruckSlotLocal(int slot, int capacity) =>
            new Vector3((slot - (capacity - 1) / 2f) * SlotSpacing - 0.45f, 0.12f, 0f);

        /// <summary>Which column a world point is over, or -1.</summary>
        public int HitColumn(Vector2 world)
        {
            if (state == null) return -1;
            if (world.y < ColumnBaseY - 0.8f || world.y > BufferY - 0.7f) return -1;
            for (int c = 0; c < state.Columns.Count; c++)
                if (Mathf.Abs(world.x - ColumnPos(c, 0).x) <= ColumnSpacing / 2f) return c;
            return -1;
        }

        // ---------- Build ----------

        public void Rebuild(GameState s)
        {
            StopAllCoroutines();
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
            Art.Box(root, "Dock", new Vector2(0, BufferY), new Vector2(state.Buffer.Length * BufferSpacing + 0.3f, 1.25f), Art.Ground, 0);
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
            int tallest = 1;
            foreach (var col in state.Columns) tallest = Mathf.Max(tallest, col.Count);
            for (int c = 0; c < state.Columns.Count; c++)
            {
                var pallet = ColumnPos(c, 0);
                float h = tallest * RowSpacing + 0.25f;
                Art.Box(root, "Lane", new Vector2(pallet.x, ColumnBaseY - 0.55f + h / 2f), new Vector2(ColumnSpacing - 0.15f, h), Art.Ground, 0);

                var list = new List<Transform>();
                for (int r = 0; r < state.Columns[c].Count; r++)
                {
                    var crate = Art.Crate(root, state.Columns[c][r], CrateSize);
                    crate.position = ColumnPos(c, r);
                    list.Add(crate);
                }
                columnCrates.Add(list);
            }

            // Trucks
            trucks = new TruckView[state.Bays.Length];
            for (int b = 0; b < state.Bays.Length; b++)
            {
                Art.Box(root, "BayMark", BayPos(b) + new Vector3(0, -0.95f), new Vector2(BaySpacing - 0.4f, 0.18f), Art.Slot, 0);
                var truck = state.Bays[b];
                if (truck == null) continue;
                trucks[b] = CreateTruck(truck.Color, truck.Capacity);
                trucks[b].Root.position = BayPos(b);
                for (int i = 0; i < truck.Loaded; i++) AddCrateToTruck(trucks[b], Art.Crate(root, truck.Color, CrateSize));
            }
        }

        private TruckView CreateTruck(int color, int capacity)
        {
            var view = new TruckView { Capacity = capacity, Root = new GameObject("Truck").transform };
            view.Root.SetParent(root, false);
            var c = Art.CrateColor(color);
            float bedWidth = capacity * SlotSpacing + 0.2f;
            float bedLeft = TruckSlotLocal(0, capacity).x - SlotSpacing / 2f - 0.1f;
            float bedCenter = bedLeft + bedWidth / 2f;

            Art.Box(view.Root, "Bed", new Vector2(bedCenter, 0.05f), new Vector2(bedWidth, 1.15f), Art.TruckBed, 2);
            Art.Box(view.Root, "BedRim", new Vector2(bedCenter, -0.48f), new Vector2(bedWidth, 0.2f), c, 3);
            for (int i = 0; i < capacity; i++)
                Art.Box(view.Root, "SlotMark", TruckSlotLocal(i, capacity), new Vector2(CrateSize, CrateSize), Art.Darken(Art.TruckBed, 0.25f), 3);

            float cabX = bedLeft + bedWidth + 0.5f;
            Art.Box(view.Root, "Cab", new Vector2(cabX, -0.05f), new Vector2(0.9f, 1.0f), c, 2);
            Art.Box(view.Root, "Window", new Vector2(cabX + 0.12f, 0.15f), new Vector2(0.5f, 0.5f), Art.Window, 3);
            Art.Dot(view.Root, "WheelBack", new Vector2(bedLeft + 0.55f, -0.62f), 0.5f, Art.Wheel, 4);
            Art.Dot(view.Root, "WheelMid", new Vector2(bedLeft + bedWidth - 0.4f, -0.62f), 0.5f, Art.Wheel, 4);
            Art.Dot(view.Root, "WheelFront", new Vector2(cabX + 0.05f, -0.62f), 0.5f, Art.Wheel, 4);
            return view;
        }

        private void AddCrateToTruck(TruckView truck, Transform crate)
        {
            crate.SetParent(truck.Root, true);
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
            Redraw(s);
            done?.Invoke();
        }

        private IEnumerator MoveCrate(GameEvent e)
        {
            Transform crate;
            if (e.From == Place.Column)
            {
                var list = columnCrates[e.FromIndex];
                crate = list[list.Count - 1];
                list.RemoveAt(list.Count - 1);
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
                target = truck.Root.TransformPoint(TruckSlotLocal(truck.Crates.Count, truck.Capacity));
            }
            else
            {
                target = BufferPos(e.ToIndex);
            }

            Art.SetLift(crate, true);
            Vector3 start = crate.position;
            float arc = 0.6f + Vector3.Distance(start, target) * 0.06f;
            for (float t = 0; t < 1f; t += Time.deltaTime / MoveTime)
            {
                float k = EaseOut(t);
                crate.position = Vector3.Lerp(start, target, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * arc;
                yield return null;
            }
            crate.position = target;
            Art.SetLift(crate, false);

            if (truck != null)
            {
                AddCrateToTruck(truck, crate);
                yield return Punch(truck.Root, 0.08f);
            }
            else
            {
                bufferCrates[e.ToIndex] = crate;
            }
        }

        private IEnumerator Depart(int bay)
        {
            var truck = trucks[bay];
            trucks[bay] = null;
            if (truck == null) yield break;
            yield return Slide(truck.Root, truck.Root.position + Vector3.right * OffscreenX, TruckTime);
            Destroy(truck.Root.gameObject);
        }

        private IEnumerator Arrive(int bay, int color, int capacity)
        {
            var truck = CreateTruck(color, capacity);
            trucks[bay] = truck;
            truck.Root.position = BayPos(bay) + Vector3.left * OffscreenX;
            yield return Slide(truck.Root, BayPos(bay), TruckTime);
        }

        private static IEnumerator Slide(Transform t, Vector3 target, float time)
        {
            Vector3 start = t.position;
            for (float k = 0; k < 1f; k += Time.deltaTime / time)
            {
                t.position = Vector3.Lerp(start, target, EaseOut(k));
                yield return null;
            }
            t.position = target;
        }

        private static IEnumerator Punch(Transform t, float amount)
        {
            Vector3 baseScale = t.localScale;
            for (float k = 0; k < 1f; k += Time.deltaTime / 0.12f)
            {
                t.localScale = baseScale * (1f + Mathf.Sin(k * Mathf.PI) * amount);
                yield return null;
            }
            t.localScale = baseScale;
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
