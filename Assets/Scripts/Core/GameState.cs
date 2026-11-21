using System.Collections.Generic;

namespace CargoSort.Core
{
    public enum GameStatus { Playing, Won, Lost }

    public enum Place { Column, Buffer, Bay }

    public enum EventKind { MoveCrate, TruckDepart, TruckArrive }

    /// <summary>One visual step produced by a move. The view plays these in order.</summary>
    public sealed class GameEvent
    {
        public EventKind Kind;
        public Place From;
        public int FromIndex;
        public int FromRow = -1;   // column moves only: the row the crate left (-1 = top). The crane lifts from any row.
        public Place To;
        public int ToIndex;
        public int Color;
        public int Bay;
        public int ToSlot = -1;    // moves into a truck: which spot (two-color trucks fill each color's spots)
        public int Capacity;       // arrivals: the truck's size
        public int Color2 = -1;    // arrivals: second color of a two-color truck

        public static GameEvent Move(Place from, int fromIndex, Place to, int toIndex, int color) =>
            new GameEvent { Kind = EventKind.MoveCrate, From = from, FromIndex = fromIndex, To = to, ToIndex = toIndex, Color = color };

        public static GameEvent Depart(int bay) => new GameEvent { Kind = EventKind.TruckDepart, Bay = bay };

        public static GameEvent Arrive(int bay, int color) => new GameEvent { Kind = EventKind.TruckArrive, Bay = bay, Color = color };
    }

    /// <summary>
    /// A truck in a bay. Big trucks just have a larger capacity. Two-color trucks take half of each
    /// color: the first Need1 spots are for Color, the rest for Color2.
    /// </summary>
    public sealed class Truck
    {
        public int Color;
        public int Color2 = -1;
        public int Capacity;
        public int Loaded;
        public int Loaded1;   // crates of Color
        public int Loaded2;   // crates of Color2
        public bool Full => Loaded >= Capacity;
        public bool TwoColor => Color2 >= 0;
        public int Need1 => TwoColor ? Capacity / 2 : Capacity;

        public bool Accepts(int color) =>
            !Full && ((color == Color && Loaded1 < Need1) || (TwoColor && color == Color2 && Loaded2 < Capacity - Need1));

        /// <summary>Loads one crate (call Accepts first) and returns the spot it goes to.</summary>
        public int Load(int color)
        {
            Loaded++;
            if (color == Color && Loaded1 < Need1) return Loaded1++;
            return Need1 + Loaded2++;
        }

        public Truck Clone() => new Truck { Color = Color, Color2 = Color2, Capacity = Capacity, Loaded = Loaded, Loaded1 = Loaded1, Loaded2 = Loaded2 };
    }

    /// <summary>
    /// Pure puzzle rules. Crates sit in columns (bottom to top). Tapping a column sends its top crate
    /// to a matching truck in a bay, or to the dock buffer if no truck matches. Full trucks leave and
    /// the next truck in the queue arrives, pulling any matching crates from the buffer.
    /// Win: every truck shipped. Lose: buffer full and no top crate matches a waiting truck.
    /// Twists: hidden crates (shown as "?" until they reach the top of their column, view only) and
    /// frozen columns (can't be tapped; each tap on another column melts the ice by one).
    /// Belt levels: the buffer is a moving conveyor. Arriving trucks don't pull from it; a riding crate
    /// loads when the view reports it passing its truck (BeltLoad). The game is only lost when no tap
    /// is possible and no riding crate has a truck to reach.
    /// </summary>
    public sealed class GameState
    {
        public const int Empty = -1;

        public List<List<int>> Columns;
        public List<List<bool>> Hidden;   // same shape as Columns
        public List<List<bool>> Taped;    // same shape: crate r is taped to the crate above it (they move together)
        public int[] Frozen;              // taps left until each column thaws (0 = free)
        public int[] Buffer;
        public Truck[] Bays;
        public List<int> TruckQueue;
        public List<int> TruckCapacities;   // per queued truck (big trucks); null = all TruckCapacity
        public List<int> TruckSecondColors; // per queued truck (two-color trucks); null = none
        public int NextTruck;
        public int TruckCapacity;
        public int TrucksShipped;
        public GameStatus Status;
        public bool Belt;

        /// <summary>Belt levels: picks the free spot a crate from this column drops onto (the view knows where the spots are).</summary>
        public System.Func<int, int> BeltSpotPicker;

        private GameState() { }

        public GameState(LevelData data)
        {
            Columns = new List<List<int>>();
            foreach (var c in data.Columns) Columns.Add(new List<int>(c));
            Hidden = new List<List<bool>>();
            for (int c = 0; c < Columns.Count; c++)
            {
                var h = new List<bool>();
                for (int r = 0; r < Columns[c].Count; r++)
                    h.Add(data.Hidden != null && c < data.Hidden.Count && r < data.Hidden[c].Count && data.Hidden[c][r]);
                Hidden.Add(h);
            }
            Taped = new List<List<bool>>();
            for (int c = 0; c < Columns.Count; c++)
            {
                var t = new List<bool>();
                for (int r = 0; r < Columns[c].Count; r++)
                    t.Add(data.Taped != null && c < data.Taped.Count && r < data.Taped[c].Count && data.Taped[c][r]);
                Taped.Add(t);
            }
            Frozen = new int[Columns.Count];
            if (data.Frozen != null)
                for (int c = 0; c < Frozen.Length && c < data.Frozen.Count; c++) Frozen[c] = data.Frozen[c];
            Buffer = new int[data.BufferSize];
            for (int i = 0; i < Buffer.Length; i++) Buffer[i] = Empty;
            Bays = new Truck[data.BayCount];
            TruckQueue = new List<int>(data.TruckColors);
            TruckCapacity = data.TruckCapacity;
            TruckCapacities = data.TruckCapacities != null && data.TruckCapacities.Count == TruckQueue.Count ? new List<int>(data.TruckCapacities) : null;
            TruckSecondColors = data.TruckSecondColors != null && data.TruckSecondColors.Count == TruckQueue.Count ? new List<int>(data.TruckSecondColors) : null;
            Belt = data.Belt;
            var ignored = new List<GameEvent>();
            for (int b = 0; b < Bays.Length; b++) ArriveNext(b, ignored);
            Status = GameStatus.Playing;
            UpdateStatus();
        }

        public int TrucksTotal => TruckQueue.Count;

        public int FreeBufferSlots
        {
            get
            {
                int n = 0;
                foreach (var c in Buffer) if (c == Empty) n++;
                return n;
            }
        }

        public int Top(int column)
        {
            var c = Columns[column];
            return c.Count == 0 ? Empty : c[c.Count - 1];
        }

        public bool IsFrozen(int column) => Frozen[column] > 0;

        /// <summary>True if the crate should be drawn as "?" (hidden and not yet on top).</summary>
        public bool IsHidden(int column, int row) => Hidden[column][row] && row < Columns[column].Count - 1;

        /// <summary>Upcoming trucks after the ones in the bays (up to count).</summary>
        public List<int> UpcomingTrucks(int count)
        {
            var list = new List<int>();
            for (int i = NextTruck; i < TruckQueue.Count && list.Count < count; i++) list.Add(TruckQueue[i]);
            return list;
        }

        /// <summary>The queued truck at this index, as it will arrive (empty).</summary>
        public Truck QueuedTruck(int index) => new Truck
        {
            Color = TruckQueue[index],
            Capacity = TruckCapacities != null ? TruckCapacities[index] : TruckCapacity,
            Color2 = TruckSecondColors != null ? TruckSecondColors[index] : -1,
        };

        /// <summary>Upcoming trucks with their size and colors (for the "Next" row).</summary>
        public List<Truck> UpcomingTruckInfo(int count)
        {
            var list = new List<Truck>();
            for (int i = NextTruck; i < TruckQueue.Count && list.Count < count; i++) list.Add(QueuedTruck(i));
            return list;
        }

        public int FindBayFor(int color)
        {
            for (int b = 0; b < Bays.Length; b++)
                if (Bays[b] != null && Bays[b].Accepts(color)) return b;
            return -1;
        }

        public bool IsWanted(int color) => FindBayFor(color) >= 0;

        public bool CanTap(int column)
        {
            if (Status != GameStatus.Playing) return false;
            if (column < 0 || column >= Columns.Count) return false;
            if (Frozen[column] > 0) return false;
            int top = Top(column);
            if (top == Empty) return false;
            if (!IsWanted(top) && FreeBufferSlots == 0) return false;
            if (!IsTapedTop(column)) return true;

            // A taped pair also needs room for the second crate, after the first one has moved.
            var s = Clone();
            s.BeltSpotPicker = null;
            s.PopTop(column);
            s.Route(top, column, -1, new List<GameEvent>());
            int second = s.Top(column);
            return s.IsWanted(second) || s.FreeBufferSlots > 0;
        }

        /// <summary>The top crate is the upper half of a taped pair, so tapping moves both.</summary>
        public bool IsTapedTop(int column)
        {
            int n = Columns[column].Count;
            return n >= 2 && Taped[column][n - 2];
        }

        private int PopTop(int column)
        {
            var col = Columns[column];
            int color = col[col.Count - 1];
            col.RemoveAt(col.Count - 1);
            Hidden[column].RemoveAt(Hidden[column].Count - 1);
            var taped = Taped[column];
            taped.RemoveAt(taped.Count - 1);
            if (taped.Count > 0) taped[taped.Count - 1] = false; // the new top has nothing above to be taped to
            return color;
        }

        public List<GameEvent> Tap(int column)
        {
            var events = new List<GameEvent>();
            if (!CanTap(column)) return events;

            bool pair = IsTapedTop(column);
            for (int c = 0; c < Frozen.Length; c++)
                if (c != column && Frozen[c] > 0) Frozen[c]--;

            Route(PopTop(column), column, -1, events);
            if (pair) Route(PopTop(column), column, -1, events);
            UpdateStatus();
            return events;
        }

        /// <summary>Sends a crate that left a column to its truck, or to the dock; full trucks then leave.</summary>
        private void Route(int color, int column, int row, List<GameEvent> events)
        {
            int bay = FindBayFor(color);
            GameEvent move;
            if (bay >= 0)
            {
                int spot = Bays[bay].Load(color);
                move = GameEvent.Move(Place.Column, column, Place.Bay, bay, color);
                move.ToSlot = spot;
            }
            else
            {
                int slot = BeltSpotPicker != null ? BeltSpotPicker(column) : -1;
                if (slot < 0 || slot >= Buffer.Length || Buffer[slot] != Empty) slot = System.Array.IndexOf(Buffer, Empty);
                Buffer[slot] = color;
                move = GameEvent.Move(Place.Column, column, Place.Buffer, slot, color);
            }
            move.FromRow = row;
            events.Add(move);
            ResolveFullTrucks(events);
        }

        // ---------- Conveyor belt ----------

        /// <summary>Belt: the crate in this spot passed under its truck and jumps on.</summary>
        public List<GameEvent> BeltLoad(int slot, int bay)
        {
            var events = new List<GameEvent>();
            if (Status != GameStatus.Playing || slot < 0 || slot >= Buffer.Length || bay < 0 || bay >= Bays.Length) return events;
            var truck = Bays[bay];
            int color = Buffer[slot];
            if (truck == null || color == Empty || !truck.Accepts(color)) return events;
            Buffer[slot] = Empty;
            var move = GameEvent.Move(Place.Buffer, slot, Place.Bay, bay, color);
            move.ToSlot = truck.Load(color);
            events.Add(move);
            ResolveFullTrucks(events);
            UpdateStatus();
            return events;
        }

        /// <summary>Loads every riding crate that has a truck, as if the player waited a full loop.</summary>
        public void Settle()
        {
            bool moved = true;
            while (moved)
            {
                moved = false;
                for (int i = 0; i < Buffer.Length; i++)
                {
                    if (Buffer[i] == Empty) continue;
                    int bay = FindBayFor(Buffer[i]);
                    if (bay < 0) continue;
                    BeltLoad(i, bay);
                    moved = true;
                }
            }
        }

        /// <summary>A copy that follows dock rules (riding crates settled), for the solver.</summary>
        public GameState AsDock()
        {
            var s = Clone();
            s.BeltSpotPicker = null;
            if (!s.Belt) return s;
            if (s.Status == GameStatus.Lost) s.Status = GameStatus.Playing;
            s.Settle();
            s.Belt = false;
            s.UpdateStatus();
            return s;
        }

        // ---------- Boosters ----------

        /// <summary>Crane: can the crate at this row be lifted out (even from under others or out of ice)?</summary>
        public bool CanPluck(int column, int row)
        {
            if (Status != GameStatus.Playing || column < 0 || column >= Columns.Count) return false;
            var col = Columns[column];
            if (row < 0 || row >= col.Count) return false;
            return IsWanted(col[row]) || FreeBufferSlots > 0;
        }

        /// <summary>Crane: lifts any crate. Does not melt ice (it is not a tap).</summary>
        public List<GameEvent> Pluck(int column, int row)
        {
            var events = new List<GameEvent>();
            PluckInto(column, row, events);
            return events;
        }

        private void PluckInto(int column, int row, List<GameEvent> events)
        {
            if (!CanPluck(column, row)) return;
            int color = Columns[column][row];
            Columns[column].RemoveAt(row);
            Hidden[column].RemoveAt(row);
            Taped[column].RemoveAt(row);
            if (row > 0) Taped[column][row - 1] = false; // the crane cuts the tape
            Route(color, column, row == Columns[column].Count ? -1 : row, events);
            UpdateStatus();
        }

        /// <summary>Magnet target: the matching crate nearest to the top of any column. False if there is none.</summary>
        public bool MagnetTarget(out int column, out int row)
        {
            column = row = -1;
            int bestDepth = int.MaxValue;
            for (int c = 0; c < Columns.Count; c++)
            {
                var col = Columns[c];
                for (int r = col.Count - 1; r >= 0; r--)
                {
                    if (!IsWanted(col[r])) continue;
                    int depth = col.Count - 1 - r;
                    if (depth < bestDepth) { bestDepth = depth; column = c; row = r; }
                    break;
                }
            }
            return column >= 0;
        }

        public bool HasMagnetTarget => MagnetTarget(out _, out _);

        /// <summary>Magnet: fills the trucks waiting now with the nearest matching crates (as many as they still need).</summary>
        public List<GameEvent> Magnet()
        {
            var events = new List<GameEvent>();
            int need = 0;
            foreach (var t in Bays) if (t != null) need += t.Capacity - t.Loaded;
            while (need-- > 0 && Status == GameStatus.Playing)
            {
                if (!MagnetTarget(out int column, out int row)) break;
                PluckInto(column, row, events);
            }
            return events;
        }

        /// <summary>
        /// Shuffle: a copy with the crates mixed between the columns (same column heights). Tries a few
        /// mixes and keeps the first one the solver can still win. Also revives a lost game.
        /// </summary>
        public GameState Shuffled(int seed, int tries = 40)
        {
            var random = new System.Random(seed);
            GameState pick = null;
            for (int attempt = 0; attempt < tries; attempt++)
            {
                var s = Clone();
                var all = new List<int>();
                foreach (var col in s.Columns) all.AddRange(col);
                for (int i = all.Count - 1; i > 0; i--)
                {
                    int j = random.Next(i + 1);
                    int swap = all[i]; all[i] = all[j]; all[j] = swap;
                }
                int k = 0;
                foreach (var col in s.Columns)
                    for (int r = 0; r < col.Count; r++) col[r] = all[k++];
                foreach (var taped in s.Taped)
                    for (int r = 0; r < taped.Count; r++) taped[r] = false;
                s.Status = GameStatus.Playing;
                s.UpdateStatus();
                pick = s;
                if (Solver.Solve(s.AsDock()) != null) break;
            }
            return pick;
        }

        /// <summary>Adds one dock slot (rewarded-ad booster). Revives a lost game.</summary>
        public void AddBufferSlot()
        {
            var bigger = new int[Buffer.Length + 1];
            System.Array.Copy(Buffer, bigger, Buffer.Length);
            bigger[Buffer.Length] = Empty;
            Buffer = bigger;
            if (Status == GameStatus.Lost) Status = GameStatus.Playing;
            UpdateStatus();
        }

        /// <summary>Makes sure the buffer has at least this many slots (keeps bonus slots across undo).</summary>
        public void EnsureBufferSize(int size)
        {
            while (Buffer.Length < size) AddBufferSlot();
        }

        private void ResolveFullTrucks(List<GameEvent> events)
        {
            bool changed = true;
            while (changed)
            {
                changed = false;
                for (int b = 0; b < Bays.Length; b++)
                {
                    if (Bays[b] == null || !Bays[b].Full) continue;
                    events.Add(GameEvent.Depart(b));
                    Bays[b] = null;
                    TrucksShipped++;
                    ArriveNext(b, events);
                    changed = true;
                }
            }
        }

        private void ArriveNext(int bay, List<GameEvent> events)
        {
            if (NextTruck >= TruckQueue.Count) return;
            var truck = QueuedTruck(NextTruck++);
            Bays[bay] = truck;
            var arrive = GameEvent.Arrive(bay, truck.Color);
            arrive.Capacity = truck.Capacity;
            arrive.Color2 = truck.Color2;
            events.Add(arrive);
            if (Belt) return; // riding crates board when they pass the truck

            for (int i = 0; i < Buffer.Length && !truck.Full; i++)
            {
                int color = Buffer[i];
                if (color == Empty || !truck.Accepts(color)) continue;
                Buffer[i] = Empty;
                var move = GameEvent.Move(Place.Buffer, i, Place.Bay, bay, color);
                move.ToSlot = truck.Load(color);
                events.Add(move);
            }
        }

        private void UpdateStatus()
        {
            if (Status != GameStatus.Playing) return;

            bool baysEmpty = true;
            foreach (var t in Bays) if (t != null) baysEmpty = false;
            if (baysEmpty && NextTruck >= TruckQueue.Count)
            {
                Status = GameStatus.Won;
                return;
            }

            for (int c = 0; c < Columns.Count; c++)
                if (CanTap(c)) return;

            // Belt: a riding crate that has a truck will get there, so the game goes on.
            if (Belt)
                foreach (int color in Buffer)
                    if (color != Empty && IsWanted(color)) return;

            Status = GameStatus.Lost;
        }

        public GameState Clone()
        {
            var s = new GameState
            {
                Columns = new List<List<int>>(),
                Hidden = new List<List<bool>>(),
                Taped = new List<List<bool>>(),
                Frozen = (int[])Frozen.Clone(),
                Buffer = (int[])Buffer.Clone(),
                Bays = new Truck[Bays.Length],
                TruckQueue = TruckQueue,
                TruckCapacities = TruckCapacities,
                TruckSecondColors = TruckSecondColors,
                NextTruck = NextTruck,
                TruckCapacity = TruckCapacity,
                TrucksShipped = TrucksShipped,
                Status = Status,
                Belt = Belt,
                BeltSpotPicker = BeltSpotPicker,
            };
            foreach (var c in Columns) s.Columns.Add(new List<int>(c));
            foreach (var h in Hidden) s.Hidden.Add(new List<bool>(h));
            foreach (var t in Taped) s.Taped.Add(new List<bool>(t));
            for (int b = 0; b < Bays.Length; b++) s.Bays[b] = Bays[b]?.Clone();
            return s;
        }
    }
}
