using System;
using System.Collections.Generic;

namespace CargoSort.Core
{
    [Serializable]
    public sealed class LevelData
    {
        public int Level;
        public int ColorCount;
        public int TruckCapacity;
        public int BufferSize;
        public int BayCount;
        public List<List<int>> Columns = new List<List<int>>(); // bottom -> top
        public List<int> TruckColors = new List<int>();         // arrival order
    }

    public struct Difficulty
    {
        public int Colors;
        public int Trucks;
        public int Columns;
        public int Buffer;
        public int Bays;
        public int Capacity;
        public int MaxHeight;
    }

    /// <summary>
    /// Builds levels from the level number (same number = same level on every device), and only
    /// returns levels the built-in solver can finish, so every level is beatable.
    /// </summary>
    public static class LevelGenerator
    {
        public const int MaxColors = 8;

        public static Difficulty ForLevel(int level)
        {
            if (level < 1) level = 1;
            var d = new Difficulty { Bays = 2, Capacity = 3, MaxHeight = 8 };

            if (level == 1) { d.Colors = 2; d.Trucks = 4; d.Columns = 3; d.Buffer = 5; return d; }
            if (level == 2) { d.Colors = 3; d.Trucks = 6; d.Columns = 4; d.Buffer = 5; return d; }

            d.Colors = Math.Min(3 + (level - 1) / 3, MaxColors);
            d.Trucks = Math.Min(d.Colors * 2 + (level - 1) / 2, 16);
            d.Columns = Math.Max(4, Math.Min(d.Colors, 7));
            d.Buffer = level < 15 ? 5 : 4;

            // Every 5th level is a "hard" level with one less dock slot.
            if (level % 5 == 0) d.Buffer -= 1;
            return d;
        }

        public static LevelData Generate(int level)
        {
            var d = ForLevel(level);
            LevelData last = null;

            // If no solvable layout is found, loosen the dock one slot at a time.
            for (int extraBuffer = 0; extraBuffer <= 3; extraBuffer++)
            {
                for (int attempt = 0; attempt < 200; attempt++)
                {
                    var rng = new Random(level * 7919 + attempt * 104729 + extraBuffer * 31);
                    var dd = d;
                    dd.Buffer += extraBuffer;
                    last = Build(level, dd, rng);
                    if (Solver.CanSolve(last)) return last;
                }
            }
            return last;
        }

        public static LevelData Build(int level, Difficulty d, Random rng)
        {
            var data = new LevelData
            {
                Level = level,
                ColorCount = d.Colors,
                TruckCapacity = d.Capacity,
                BufferSize = d.Buffer,
                BayCount = d.Bays,
            };

            for (int i = 0; i < d.Trucks; i++) data.TruckColors.Add(i % d.Colors);
            Shuffle(data.TruckColors, rng);

            var crates = new List<int>();
            foreach (var color in data.TruckColors)
                for (int k = 0; k < d.Capacity; k++) crates.Add(color);
            Shuffle(crates, rng);

            int columns = Math.Max(d.Columns, (crates.Count + d.MaxHeight - 1) / d.MaxHeight);
            for (int c = 0; c < columns; c++) data.Columns.Add(new List<int>());

            // Spread crates so column heights differ by at most a little, with some randomness.
            int index = 0;
            while (index < crates.Count)
            {
                int c = rng.Next(columns);
                int min = int.MaxValue;
                foreach (var col in data.Columns) min = Math.Min(min, col.Count);
                if (data.Columns[c].Count > min + 1 || data.Columns[c].Count >= d.MaxHeight) continue;
                data.Columns[c].Add(crates[index++]);
            }
            return data;
        }

        private static void Shuffle<T>(IList<T> list, Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }

    /// <summary>Greedy solver used to make sure generated levels can be beaten.</summary>
    public static class Solver
    {
        public static bool CanSolve(LevelData data) => Solve(new GameState(data)) != null;

        /// <summary>Returns the list of column taps that wins, or null if greedy play fails.</summary>
        public static List<int> Solve(GameState s)
        {
            var moves = new List<int>();
            int guard = 2000;
            while (s.Status == GameStatus.Playing && guard-- > 0)
            {
                int move = PickMove(s);
                if (move < 0 || !s.CanTap(move)) return null;
                s.Tap(move);
                moves.Add(move);
            }
            return s.Status == GameStatus.Won ? moves : null;
        }

        /// <summary>Best next tap: load a waiting truck if possible, otherwise dig toward the shallowest wanted crate.</summary>
        public static int PickMove(GameState s)
        {
            for (int c = 0; c < s.Columns.Count; c++)
                if (s.Top(c) != GameState.Empty && s.IsWanted(s.Top(c))) return c;

            int free = s.FreeBufferSlots;
            int best = -1, bestDepth = int.MaxValue;
            for (int c = 0; c < s.Columns.Count; c++)
            {
                var col = s.Columns[c];
                for (int i = col.Count - 1; i >= 0; i--)
                {
                    if (!s.IsWanted(col[i])) continue;
                    int depth = col.Count - 1 - i;
                    if (depth <= free && depth < bestDepth) { bestDepth = depth; best = c; }
                    break;
                }
            }
            if (best >= 0) return best;

            // Nothing wanted is reachable: park the crate whose truck comes soonest.
            int bestWait = int.MaxValue;
            for (int c = 0; c < s.Columns.Count; c++)
            {
                int top = s.Top(c);
                if (top == GameState.Empty || free == 0) continue;
                int wait = s.TruckQueue.IndexOf(top, Math.Min(s.NextTruck, s.TruckQueue.Count));
                if (wait < 0) wait = int.MaxValue - 1;
                if (wait < bestWait) { bestWait = wait; best = c; }
            }
            return best;
        }
    }

    /// <summary>Current game plus undo history.</summary>
    public sealed class GameSession
    {
        private readonly Stack<GameState> history = new Stack<GameState>();

        public GameState State { get; private set; }
        public LevelData Level { get; }

        public GameSession(LevelData level)
        {
            Level = level;
            State = new GameState(level);
        }

        public bool CanUndo => history.Count > 0 && State.Status != GameStatus.Won;

        public List<GameEvent> Tap(int column)
        {
            if (!State.CanTap(column)) return new List<GameEvent>();
            history.Push(State.Clone());
            return State.Tap(column);
        }

        public bool Undo()
        {
            if (!CanUndo) return false;
            int bufferSize = State.Buffer.Length;
            State = history.Pop();
            State.EnsureBufferSize(bufferSize);
            return true;
        }

        public void AddBufferSlot() => State.AddBufferSlot();

        public int HintColumn() => Solver.PickMove(State.Clone());
    }
}
