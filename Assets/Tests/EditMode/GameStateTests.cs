using System.Collections.Generic;
using CargoSort.Core;
using NUnit.Framework;

namespace CargoSort.Tests
{
    public class GameStateTests
    {
        private static LevelData Simple()
        {
            // Two trucks: red (0) then blue (1), capacity 2, one bay, two buffer slots.
            return new LevelData
            {
                TruckCapacity = 2,
                BufferSize = 2,
                BayCount = 1,
                ColorCount = 2,
                TruckColors = new List<int> { 0, 1 },
                Columns = new List<List<int>> { new List<int> { 0, 1 }, new List<int> { 1, 0 } },
            };
        }

        [Test]
        public void MatchingCrateGoesToTruck()
        {
            var s = new GameState(Simple());
            var events = s.Tap(1); // top is red, truck is red
            Assert.AreEqual(Place.Bay, events[0].To);
            Assert.AreEqual(1, s.Bays[0].Loaded);
        }

        [Test]
        public void OtherCrateGoesToBufferThenIntoNextTruck()
        {
            var s = new GameState(Simple());
            s.Tap(0); // blue -> buffer
            Assert.AreEqual(1, s.Buffer[0]);
            s.Tap(0); // red -> truck (1/2)
            s.Tap(1); // red -> truck full, departs, blue truck arrives and pulls blue from buffer
            Assert.AreEqual(GameState.Empty, s.Buffer[0]);
            Assert.AreEqual(1, s.Bays[0].Color);
            Assert.AreEqual(1, s.Bays[0].Loaded);
            s.Tap(1);
            Assert.AreEqual(GameStatus.Won, s.Status);
        }

        [Test]
        public void FullBufferWithNoMatchLoses_AndExtraSlotRevives()
        {
            var data = Simple();
            data.BufferSize = 0;
            data.Columns = new List<List<int>> { new List<int> { 0, 0, 1 }, new List<int> { 1 } };
            var s = new GameState(data);
            Assert.AreEqual(GameStatus.Lost, s.Status);
            s.AddBufferSlot();
            Assert.AreEqual(GameStatus.Playing, s.Status);
        }

        [Test]
        public void UndoRestoresPreviousState()
        {
            var session = new GameSession(Simple());
            session.Tap(0);
            Assert.IsTrue(session.Undo());
            Assert.AreEqual(2, session.State.Columns[0].Count);
            Assert.AreEqual(GameState.Empty, session.State.Buffer[0]);
        }

        [Test]
        public void GeneratedLevelsAreSolvableAndDeterministic()
        {
            for (int level = 1; level <= 60; level++)
            {
                var data = LevelGenerator.Generate(level);
                Assert.IsTrue(Solver.CanSolve(data), "level " + level);
                int crates = 0;
                foreach (var c in data.Columns) crates += c.Count;
                Assert.AreEqual(data.TruckColors.Count * data.TruckCapacity, crates, "level " + level);
            }
            Assert.AreEqual(LevelGenerator.Generate(9).TruckColors, LevelGenerator.Generate(9).TruckColors);
        }
    }
}
