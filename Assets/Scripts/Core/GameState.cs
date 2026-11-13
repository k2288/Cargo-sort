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
        public Place To;
        public int ToIndex;
        public int Color;
        public int Bay;

        public static GameEvent Move(Place from, int fromIndex, Place to, int toIndex, int color) =>
            new GameEvent { Kind = EventKind.MoveCrate, From = from, FromIndex = fromIndex, To = to, ToIndex = toIndex, Color = color };

        public static GameEvent Depart(int bay) => new GameEvent { Kind = EventKind.TruckDepart, Bay = bay };

        public static GameEvent Arrive(int bay, int color) => new GameEvent { Kind = EventKind.TruckArrive, Bay = bay, Color = color };
    }

    public sealed class Truck
    {
        public int Color;
        public int Capacity;
        public int Loaded;
        public bool Full => Loaded >= Capacity;

        public Truck Clone() => new Truck { Color = Color, Capacity = Capacity, Loaded = Loaded };
    }

    /// <summary>
    /// Pure puzzle rules. Crates sit in columns (bottom to top). Tapping a column sends its top crate
    /// to a matching truck in a bay, or to the dock buffer if no truck matches. Full trucks leave and
    /// the next truck in the queue arrives, pulling any matching crates from the buffer.
    /// Win: every truck shipped. Lose: buffer full and no top crate matches a waiting truck.
    /// </summary>
    public sealed class GameState
    {
        public const int Empty = -1;

        public List<List<int>> Columns;
        public int[] Buffer;
        public Truck[] Bays;
        public List<int> TruckQueue;
        public int NextTruck;
        public int TruckCapacity;
        public int TrucksShipped;
        public GameStatus Status;

        private GameState() { }

        public GameState(LevelData data)
        {
            Columns = new List<List<int>>();
            foreach (var c in data.Columns) Columns.Add(new List<int>(c));
            Buffer = new int[data.BufferSize];
            for (int i = 0; i < Buffer.Length; i++) Buffer[i] = Empty;
            Bays = new Truck[data.BayCount];
            TruckQueue = new List<int>(data.TruckColors);
            TruckCapacity = data.TruckCapacity;
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

        public int FindBayFor(int color)
        {
            for (int b = 0; b < Bays.Length; b++)
                if (Bays[b] != null && !Bays[b].Full && Bays[b].Color == color) return b;
            return -1;
        }

        public bool IsWanted(int color) => FindBayFor(color) >= 0;

        public bool CanTap(int column)
        {
            if (Status != GameStatus.Playing) return false;
            if (column < 0 || column >= Columns.Count) return false;
            int top = Top(column);
            if (top == Empty) return false;
            return IsWanted(top) || FreeBufferSlots > 0;
        }

        public List<GameEvent> Tap(int column)
        {
            var events = new List<GameEvent>();
            if (!CanTap(column)) return events;

            var col = Columns[column];
            int color = col[col.Count - 1];
            col.RemoveAt(col.Count - 1);

            int bay = FindBayFor(color);
            if (bay >= 0)
            {
                Bays[bay].Loaded++;
                events.Add(GameEvent.Move(Place.Column, column, Place.Bay, bay, color));
            }
            else
            {
                int slot = System.Array.IndexOf(Buffer, Empty);
                Buffer[slot] = color;
                events.Add(GameEvent.Move(Place.Column, column, Place.Buffer, slot, color));
            }

            ResolveFullTrucks(events);
            UpdateStatus();
            return events;
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
            int color = TruckQueue[NextTruck++];
            var truck = new Truck { Color = color, Capacity = TruckCapacity };
            Bays[bay] = truck;
            events.Add(GameEvent.Arrive(bay, color));

            for (int i = 0; i < Buffer.Length && !truck.Full; i++)
            {
                if (Buffer[i] != color) continue;
                Buffer[i] = Empty;
                truck.Loaded++;
                events.Add(GameEvent.Move(Place.Buffer, i, Place.Bay, bay, color));
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

            Status = GameStatus.Lost;
        }

        public GameState Clone()
        {
            var s = new GameState
            {
                Columns = new List<List<int>>(),
                Buffer = (int[])Buffer.Clone(),
                Bays = new Truck[Bays.Length],
                TruckQueue = TruckQueue,
                NextTruck = NextTruck,
                TruckCapacity = TruckCapacity,
                TrucksShipped = TrucksShipped,
                Status = Status,
            };
            foreach (var c in Columns) s.Columns.Add(new List<int>(c));
            for (int b = 0; b < Bays.Length; b++) s.Bays[b] = Bays[b]?.Clone();
            return s;
        }
    }
}
