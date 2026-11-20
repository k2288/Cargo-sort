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

        [Test]
        public void CraneLiftsACrateFromUnderOthers()
        {
            var s = new GameState(Simple());
            // Column 0 is red (bottom), blue (top). Lift the red one from under the blue crate.
            Assert.IsTrue(s.CanPluck(0, 0));
            var events = s.Pluck(0, 0);
            Assert.AreEqual(Place.Bay, events[0].To);
            Assert.AreEqual(0, events[0].FromRow);
            Assert.AreEqual(1, s.Columns[0].Count);
            Assert.AreEqual(1, s.Columns[0][0]);
            Assert.AreEqual(1, s.Bays[0].Loaded);
        }

        [Test]
        public void CraneWorksOnFrozenColumns()
        {
            var data = Simple();
            data.Frozen = new List<int> { 3, 0 };
            var s = new GameState(data);
            Assert.IsFalse(s.CanTap(0));
            Assert.IsTrue(s.CanPluck(0, 1));
            s.Pluck(0, 1);
            Assert.AreEqual(3, s.Frozen[0]); // lifting is not a tap, so the ice does not melt
        }

        [Test]
        public void MagnetFillsTheWaitingTruck()
        {
            var s = new GameState(Simple());
            Assert.IsTrue(s.HasMagnetTarget);
            s.Magnet(); // the red truck needs 2: both reds are pulled, the truck leaves, blue arrives
            Assert.AreEqual(1, s.TrucksShipped);
            Assert.AreEqual(1, s.Bays[0].Color);
            Assert.AreEqual(0, s.Bays[0].Loaded);
        }

        [Test]
        public void ShuffleKeepsTheCratesAndColumnHeights()
        {
            var level = LevelGenerator.Generate(12);
            var s = new GameState(level);
            var mixed = s.Shuffled(7);
            Assert.AreEqual(s.Columns.Count, mixed.Columns.Count);
            var before = new List<int>();
            var after = new List<int>();
            for (int c = 0; c < s.Columns.Count; c++)
            {
                Assert.AreEqual(s.Columns[c].Count, mixed.Columns[c].Count);
                before.AddRange(s.Columns[c]);
                after.AddRange(mixed.Columns[c]);
            }
            before.Sort();
            after.Sort();
            Assert.AreEqual(before, after);
            Assert.AreEqual(GameStatus.Playing, mixed.Status);
        }

        [Test]
        public void BoostersCanBeUndone()
        {
            var session = new GameSession(Simple());
            session.Pluck(0, 0);
            Assert.IsTrue(session.Undo());
            Assert.AreEqual(2, session.State.Columns[0].Count);
            session.Magnet();
            Assert.IsTrue(session.Undo());
            Assert.AreEqual(0, session.State.TrucksShipped);
            session.Shuffle(3);
            Assert.IsTrue(session.Undo());
            Assert.AreEqual(0, session.State.Columns[0][0]);
        }
    }
}
