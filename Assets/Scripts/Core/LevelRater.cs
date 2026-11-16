using System;
using System.Collections.Generic;

namespace CargoSort.Core
{
    /// <summary>
    /// Estimates how hard a level feels by playing it many times like a casual player:
    /// load a waiting truck when an obvious move exists; otherwise usually make a sensible move
    /// (dig toward a crate a truck wants), sometimes a random one.
    /// </summary>
    public static class LevelRater
    {
        public const int Runs = 40;

        /// <summary>Share of casual runs (0..1) that win. Same result every time for the same level and seed.</summary>
        public static double CasualWinRate(LevelData data, int seed, int runs = Runs)
        {
            var rng = new Random(seed ^ 0x5bd1e995);
            int wins = 0;
            var start = new GameState(data);
            var choices = new List<int>();
            for (int run = 0; run < runs; run++)
            {
                var s = start.Clone();
                int guard = 2000;
                while (s.Status == GameStatus.Playing && guard-- > 0)
                {
                    int move = CasualMove(s, rng, choices);
                    if (move < 0) break;
                    s.Tap(move);
                }
                if (s.Status == GameStatus.Won) wins++;
            }
            return wins / (double)runs;
        }

        /// <summary>How often a casual player makes the sensible move when no truck can be loaded right away.</summary>
        public const double Skill = 0.65;

        private static int CasualMove(GameState s, Random rng, List<int> choices)
        {
            choices.Clear();
            bool obvious = false;
            for (int c = 0; c < s.Columns.Count && !obvious; c++)
                obvious = s.CanTap(c) && s.IsWanted(s.Top(c));
            if (!obvious && rng.NextDouble() < Skill)
            {
                int smart = Solver.PickMove(s);
                if (smart >= 0 && s.CanTap(smart)) return smart;
            }
            for (int c = 0; c < s.Columns.Count; c++)
                if (s.CanTap(c) && s.IsWanted(s.Top(c))) choices.Add(c);
            if (choices.Count == 0)
                for (int c = 0; c < s.Columns.Count; c++)
                    if (s.CanTap(c)) choices.Add(c);
            return choices.Count == 0 ? -1 : choices[rng.Next(choices.Count)];
        }
    }
}
