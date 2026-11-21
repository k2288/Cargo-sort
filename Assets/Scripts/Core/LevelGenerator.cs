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
        public List<List<bool>> Hidden = new List<List<bool>>(); // same shape as Columns
        public List<int> Frozen = new List<int>();               // ice taps per column
        public List<List<bool>> Taped = new List<List<bool>>();  // [c][r]: crate r is taped to the crate above
        public List<List<int>> Locks = new List<List<int>>();    // [c][r]: lock id on this crate (0 = none)
        public List<List<int>> Keys = new List<List<int>>();     // [c][r]: id of the lock this crate's key opens (0 = none)
        public List<int> TruckCapacities = new List<int>();      // per truck in TruckColors (big trucks hold more)
        public List<int> TruckSecondColors = new List<int>();    // per truck: second color of a two-color truck, or -1
        public bool Hard;
        public bool Belt;   // conveyor belt instead of the dock (see GameState.Belt)
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
        public int HiddenPercent;   // chance a non-top crate is hidden
        public int FrozenColumns;
        public int FrozenTaps;
        public int TapedPairs;
        public int BigTrucks;
        public int TwoColorTrucks;
        public int Locks;
        public bool Hard;
    }

    /// <summary>
    /// Builds levels from the level number (same number = same level on every device), and only
    /// returns levels the built-in solver can finish, so every level is beatable.
    /// </summary>
    public static class LevelGenerator
    {
        public const int MaxColors = 8;
        public const int HiddenFromLevel = 11;
        public const int FrozenFromLevel = 21;

        /// <summary>Every 5th level is a hard level: one less dock slot, double coins.</summary>
        public static bool IsHard(int level) => level >= 5 && level % 5 == 0;

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
            d.Hard = IsHard(level);

            // A new twist every 10 levels.
            if (level >= HiddenFromLevel) d.HiddenPercent = Math.Min(20 + (level - HiddenFromLevel) / 2, 30);
            if (level >= FrozenFromLevel)
            {
                d.FrozenColumns = level >= 41 ? 2 : 1;
                d.FrozenTaps = 3 + (level % 3);
            }
            if (level >= TapedFromLevel) d.TapedPairs = Math.Min(1 + (level - TapedFromLevel) / 8, 3);
            if (level >= BigTrucksFromLevel) d.BigTrucks = Math.Min(1 + (level - BigTrucksFromLevel) / 10, 3);
            if (level >= TwoColorFromLevel) d.TwoColorTrucks = Math.Min(1 + (level - TwoColorFromLevel) / 10, 3);
            if (level >= LocksFromLevel) d.Locks = Math.Min(1 + (level - LocksFromLevel) / 10, 3);
            return d;
        }

        /// <summary>Locked crates (opened by moving their key crate) start here.</summary>
        public const int LocksFromLevel = 48;

        /// <summary>Big trucks (4 crates) start here; two-color trucks (2 + 2) a bit later.</summary>
        public const int BigTrucksFromLevel = 36;
        public const int TwoColorFromLevel = 40;
        public const int BigTruckCapacity = 4;

        /// <summary>Taped crates (pairs that move together) start here.</summary>
        public const int TapedFromLevel = 28;

        /// <summary>Conveyor belt levels: level 15, then every 3rd level.</summary>
        public const int BeltFromLevel = 15;

        /// <summary>The belt has this many more spots than the dock would have (crates take time to ride around).</summary>
        public const int BeltExtraSpots = 2;

        public static bool IsBelt(int level) => level >= BeltFromLevel && level % 3 == 0;

        /// <summary>
        /// Builds level n. Belt levels are generated and solver-checked with dock rules: on the belt a
        /// player can always wait for a crate to reach its truck, so a dock-solvable level is belt-solvable.
        /// </summary>
        public static LevelData Generate(int level)
        {
            var data = Generate(level, ForLevel(level), level * 7919);
            if (IsBelt(level))
            {
                data.Belt = true;
                data.BufferSize += BeltExtraSpots;
            }
            return data;
        }

        /// <summary>
        /// The daily challenge for a day number: the same level for every player that day, harder than
        /// normal levels (many colors and trucks, a small dock), but still checked to be solvable.
        /// </summary>
        public static LevelData GenerateChallenge(int day)
        {
            var d = ForLevel(12 + (day % 9));
            d.Trucks = Math.Min(d.Trucks + 2, 18);
            d.Buffer = 4;
            return Generate(0, d, 500009 + day * 7349);
        }

        /// <summary>How many solvable layouts are compared before picking the one closest to the target difficulty.</summary>
        public const int Candidates = 12;
        private const double TooHardMargin = 0.2;
        private const int MaxEasing = 1;

        private static LevelData Generate(int level, Difficulty d, int seed)
        {
            LevelData last = null;
            LevelData best = null;
            double bestGap = double.MaxValue, bestRate = 0;
            double target = TargetWinRate(level);

            // Build several solvable layouts and keep the one whose casual-player win rate is closest to
            // the target for this level. If every layout is far too hard (or none is solvable), give the
            // dock one more slot and try again.
            for (int extraBuffer = 0; extraBuffer <= 3; extraBuffer++)
            {
                int found = 0;
                for (int attempt = 0; attempt < 200 && found < Candidates; attempt++)
                {
                    var rng = new Random(seed + attempt * 104729 + extraBuffer * 31);
                    var dd = d;
                    dd.Buffer += extraBuffer;
                    last = Build(level, dd, rng);
                    if (!Solver.CanSolve(last)) continue;
                    found++;
                    double rate = LevelRater.CasualWinRate(last, seed);
                    double gap = Math.Abs(rate - target);
                    if (gap < bestGap) { bestGap = gap; best = last; bestRate = rate; }
                }
                if (best != null && (bestRate >= target - TooHardMargin || extraBuffer >= MaxEasing)) break;
            }
            return best ?? last;
        }

        /// <summary>
        /// The share of casual players (see <see cref="LevelRater"/>) who should beat the level on the first try.
        /// Falls slowly as levels go up, in a sawtooth: every 5 levels end on a HARD level, followed by a relief level.
        /// </summary>
        public static double TargetWinRate(int level)
        {
            if (level <= 0) return 0.3;                      // daily challenge
            if (level <= 3) return 0.95;                     // first levels: everyone wins
            double baseRate = level <= 30
                ? 0.9 - (level - 3) * (0.5 / 27.0)          // 0.90 at level 3 -> 0.40 at level 30
                : Math.Max(0.25, 0.4 - (level - 30) * 0.005);
            double[] wave = { -0.15, 0.12, 0.04, 0.0, -0.05 }; // index = level % 5 (0 = HARD level)
            return Math.Min(0.97, Math.Max(0.12, baseRate + wave[level % 5]));
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
                Hard = d.Hard,
            };

            for (int i = 0; i < d.Trucks; i++) data.TruckColors.Add(i % d.Colors);
            Shuffle(data.TruckColors, rng);

            // Special trucks (random numbers only drawn when a level has them, so earlier levels stay the same).
            foreach (var _ in data.TruckColors)
            {
                data.TruckCapacities.Add(d.Capacity);
                data.TruckSecondColors.Add(-1);
            }
            for (int k = 0; k < d.BigTrucks; k++)
            {
                int i = PickPlainTruck(data, d.Capacity, rng);
                if (i >= 0) data.TruckCapacities[i] = BigTruckCapacity;
            }
            for (int k = 0; k < d.TwoColorTrucks && d.Colors >= 2; k++)
            {
                int i = PickPlainTruck(data, d.Capacity, rng);
                if (i < 0) continue;
                data.TruckCapacities[i] = BigTruckCapacity;
                data.TruckSecondColors[i] = (data.TruckColors[i] + 1 + rng.Next(d.Colors - 1)) % d.Colors;
            }

            var crates = new List<int>();
            for (int t = 0; t < data.TruckColors.Count; t++)
            {
                int cap = data.TruckCapacities[t], second = data.TruckSecondColors[t];
                for (int k = 0; k < cap; k++) crates.Add(second >= 0 && k >= cap / 2 ? second : data.TruckColors[t]);
            }
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

            // Twists use the random numbers after the layout, so earlier levels stay the same.
            foreach (var col in data.Columns)
            {
                var hidden = new List<bool>();
                for (int r = 0; r < col.Count; r++)
                    hidden.Add(d.HiddenPercent > 0 && r < col.Count - 1 && rng.Next(100) < d.HiddenPercent);
                data.Hidden.Add(hidden);
                data.Frozen.Add(0);
            }
            for (int i = 0; i < d.FrozenColumns; i++)
            {
                int c = rng.Next(columns);
                if (data.Frozen[c] == 0) data.Frozen[c] = d.FrozenTaps + i * 2;
            }

            // Taped pairs (drawn after everything else, so earlier levels stay the same). Never hidden or chained.
            foreach (var col in data.Columns) data.Taped.Add(new List<bool>(new bool[col.Count]));
            for (int k = 0, tries = 0; k < d.TapedPairs && tries < 40; tries++)
            {
                int c = rng.Next(columns);
                var col = data.Columns[c];
                if (col.Count < 3) continue;
                int row = rng.Next(col.Count - 1);
                var taped = data.Taped[c];
                if (taped[row] || (row > 0 && taped[row - 1]) || taped[row + 1]) continue;
                taped[row] = true;
                data.Hidden[c][row] = false;
                data.Hidden[c][row + 1] = false;
                k++;
            }

            // Locks and keys: a lock and its key are in different stacks. Never on taped or hidden crates.
            foreach (var col in data.Columns)
            {
                data.Locks.Add(new List<int>(new int[col.Count]));
                data.Keys.Add(new List<int>(new int[col.Count]));
            }
            for (int id = 1, tries = 0; id <= d.Locks && tries < 60; tries++)
            {
                int c1 = rng.Next(columns), c2 = rng.Next(columns);
                if (c1 == c2) continue;
                int n1 = data.Columns[c1].Count, n2 = data.Columns[c2].Count;
                if (n1 < 2 || n2 == 0) continue;
                int r1 = rng.Next(n1), r2 = rng.Next(n2);
                if (!FreeForLock(data, c1, r1) || !FreeForLock(data, c2, r2)) continue;
                data.Locks[c1][r1] = id;
                data.Keys[c2][r2] = id;
                data.Hidden[c1][r1] = false;
                data.Hidden[c2][r2] = false;
                id++;
            }
            return data;
        }

        private static bool FreeForLock(LevelData data, int c, int row) =>
            !data.Taped[c][row] && !(row > 0 && data.Taped[c][row - 1]) && data.Locks[c][row] == 0 && data.Keys[c][row] == 0;

        private static int PickPlainTruck(LevelData data, int capacity, Random rng)
        {
            for (int tries = 0; tries < 30; tries++)
            {
                int i = rng.Next(data.TruckColors.Count);
                if (data.TruckCapacities[i] == capacity) return i;
            }
            return -1;
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
        /// <summary>Belt levels are checked with dock rules (a player can always wait for a riding crate).</summary>
        public static bool CanSolve(LevelData data) => Solve(new GameState(data).AsDock()) != null;

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
                if (!s.IsFrozen(c) && s.Top(c) != GameState.Empty && s.IsWanted(s.Top(c)) && s.CanTap(c)) return c;

            int free = s.FreeBufferSlots;
            int best = -1, bestDepth = int.MaxValue;
            for (int c = 0; c < s.Columns.Count; c++)
            {
                if (s.IsFrozen(c)) continue;
                var col = s.Columns[c];
                for (int i = col.Count - 1; i >= 0; i--)
                {
                    if (!s.IsWanted(col[i])) continue;
                    int depth = col.Count - 1 - i;
                    if (depth <= free && depth < bestDepth && s.CanTap(c)) { bestDepth = depth; best = c; }
                    break;
                }
            }
            if (best >= 0) return best;

            // Nothing wanted is reachable: park the crate whose truck comes soonest.
            int bestWait = int.MaxValue;
            for (int c = 0; c < s.Columns.Count; c++)
            {
                int top = s.Top(c);
                if (top == GameState.Empty || free == 0 || s.IsFrozen(c)) continue;
                int wait = s.TruckQueue.IndexOf(top, Math.Min(s.NextTruck, s.TruckQueue.Count));
                if (wait < 0) wait = int.MaxValue - 1;
                if (wait < bestWait && s.CanTap(c)) { bestWait = wait; best = c; }
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

        /// <summary>Belt: a riding crate reached a truck of its color (not an undo step).</summary>
        public List<GameEvent> BeltLoad(int slot, int bay) => State.BeltLoad(slot, bay);

        /// <summary>Crane booster: lift any crate. Undo can take it back.</summary>
        public List<GameEvent> Pluck(int column, int row)
        {
            if (!State.CanPluck(column, row)) return new List<GameEvent>();
            history.Push(State.Clone());
            return State.Pluck(column, row);
        }

        /// <summary>Magnet booster. Undo can take it back.</summary>
        public List<GameEvent> Magnet()
        {
            if (!State.HasMagnetTarget) return new List<GameEvent>();
            history.Push(State.Clone());
            return State.Magnet();
        }

        /// <summary>Shuffle booster. Undo can take it back.</summary>
        public void Shuffle(int seed)
        {
            history.Push(State.Clone());
            int bufferSize = State.Buffer.Length;
            State = State.Shuffled(seed);
            State.EnsureBufferSize(bufferSize);
        }

        public int HintColumn() => Solver.PickMove(State.Clone());
    }
}
