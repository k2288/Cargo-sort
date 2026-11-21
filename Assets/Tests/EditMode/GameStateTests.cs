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
                int spots = 0;
                foreach (int cap in data.TruckCapacities) spots += cap; // big and two-color trucks hold more
                Assert.AreEqual(spots, crates, "level " + level);
            }
            Assert.AreEqual(LevelGenerator.Generate(9).TruckColors, LevelGenerator.Generate(9).TruckColors);
        }

        // Belt test level: trucks red, blue, red (1 crate each), one bay, one belt spot.
        private static LevelData BeltLevel() => new LevelData
        {
            TruckCapacity = 1,
            BufferSize = 1,
            BayCount = 1,
            ColorCount = 2,
            Belt = true,
            TruckColors = new List<int> { 0, 1, 0 },
            Columns = new List<List<int>> { new List<int> { 0, 1 }, new List<int> { 0 } },
        };

        [Test]
        public void BeltCratesWaitForTheirTruckInsteadOfBeingPulled()
        {
            var s = new GameState(BeltLevel());
            s.Tap(0); // blue rides the belt
            s.Tap(1); // red fills the red truck; the blue truck arrives
            Assert.AreEqual(1, s.Bays[0].Color);
            Assert.AreEqual(0, s.Bays[0].Loaded);   // not pulled from the belt
            Assert.AreEqual(1, s.Buffer[0]);
            // No tap is possible (belt full, red on top), but the riding blue crate has a truck: keep playing.
            Assert.IsFalse(s.CanTap(0));
            Assert.AreEqual(GameStatus.Playing, s.Status);

            var events = s.BeltLoad(0, 0);
            Assert.AreEqual(Place.Buffer, events[0].From);
            Assert.AreEqual(GameState.Empty, s.Buffer[0]);
            s.Tap(0);
            Assert.AreEqual(GameStatus.Won, s.Status);
        }

        [Test]
        public void DockStillPullsCratesWhenATruckArrives()
        {
            var data = BeltLevel();
            data.Belt = false;
            var s = new GameState(data);
            s.Tap(0);
            s.Tap(1);
            Assert.AreEqual(GameState.Empty, s.Buffer[0]); // pulled into the blue truck, which then left
        }

        [Test]
        public void BeltSpotPickerChoosesTheSpot()
        {
            var data = Simple();
            data.Belt = true;
            var s = new GameState(data) { BeltSpotPicker = column => 1 };
            s.Tap(0); // blue goes to the belt, spot 1 as picked
            Assert.AreEqual(1, s.Buffer[1]);
            Assert.AreEqual(GameState.Empty, s.Buffer[0]);
        }

        [Test]
        public void BeltStateCanBeSolvedAsDock()
        {
            var s = new GameState(BeltLevel());
            s.Tap(0);
            s.Tap(1);
            Assert.IsNotNull(Solver.Solve(s.AsDock()));
            Assert.IsTrue(s.Belt); // the original is unchanged
        }

        [Test]
        public void BeltLevelsStartAtFifteenEveryThirdLevel()
        {
            Assert.IsFalse(LevelGenerator.IsBelt(12));
            Assert.IsTrue(LevelGenerator.IsBelt(15));
            Assert.IsFalse(LevelGenerator.IsBelt(16));
            Assert.IsTrue(LevelGenerator.IsBelt(18));
            var belt = LevelGenerator.Generate(18);
            Assert.IsTrue(belt.Belt);
            Assert.IsFalse(LevelGenerator.Generate(16).Belt);
            Assert.IsTrue(Solver.CanSolve(new LevelData
            {
                TruckCapacity = belt.TruckCapacity, BufferSize = belt.BufferSize - LevelGenerator.BeltExtraSpots, BayCount = belt.BayCount,
                ColorCount = belt.ColorCount, TruckColors = belt.TruckColors, Columns = belt.Columns, Hidden = belt.Hidden, Frozen = belt.Frozen,
            }));
        }

        // Taped test level: red truck then blue truck (2 crates each), column 0 = blue, red, red (top two taped).
        private static LevelData TapedLevel(int buffer) => new LevelData
        {
            TruckCapacity = 2,
            BufferSize = buffer,
            BayCount = 1,
            ColorCount = 2,
            TruckColors = new List<int> { 0, 1 },
            Columns = new List<List<int>> { new List<int> { 1, 0, 0 }, new List<int> { 1 } },
            Taped = new List<List<bool>> { new List<bool> { false, true, false }, new List<bool> { false } },
        };

        [Test]
        public void TapedPairMovesTogether()
        {
            var s = new GameState(TapedLevel(1));
            Assert.IsTrue(s.IsTapedTop(0));
            var events = s.Tap(0);
            Assert.AreEqual(1, s.Columns[0].Count);  // both red crates left
            Assert.AreEqual(1, s.TrucksShipped);    // the red truck filled up and left
            Assert.AreEqual(Place.Column, events[0].From);
            Assert.IsFalse(s.IsTapedTop(0));
        }

        [Test]
        public void TapedPairNeedsRoomForBothCrates()
        {
            var data = TapedLevel(0);
            data.Columns[0] = new List<int> { 1, 1, 0 };          // top two: blue (taped under) and red
            data.Taped[0] = new List<bool> { false, true, false };
            var s = new GameState(data);
            // Red goes to the red truck, but the blue crate under it has no truck and the dock has no room.
            Assert.IsFalse(s.CanTap(0));
        }

        [Test]
        public void CraneCutsTheTape()
        {
            var s = new GameState(TapedLevel(1));
            s.Pluck(0, 2); // lift only the upper crate of the pair
            Assert.AreEqual(2, s.Columns[0].Count);
            Assert.IsFalse(s.Taped[0][1]);
            Assert.IsFalse(s.IsTapedTop(0));
        }

        [Test]
        public void TapedPairsStartAtLevel28()
        {
            int Count(LevelData d) { int n = 0; foreach (var col in d.Taped) foreach (bool t in col) if (t) n++; return n; }
            Assert.AreEqual(0, Count(LevelGenerator.Generate(27)));
            Assert.AreEqual(1, Count(LevelGenerator.Generate(28)));
            Assert.AreEqual(3, Count(LevelGenerator.Generate(44)));
            for (int level = 28; level <= 60; level += 4)
                Assert.IsTrue(Solver.CanSolve(LevelGenerator.Generate(level)), "level " + level);
        }

        [Test]
        public void BigTruckHoldsFourCrates()
        {
            var s = new GameState(new LevelData
            {
                TruckCapacity = 3, BufferSize = 2, BayCount = 1, ColorCount = 1,
                TruckColors = new List<int> { 0 }, TruckCapacities = new List<int> { 4 }, TruckSecondColors = new List<int> { -1 },
                Columns = new List<List<int>> { new List<int> { 0, 0 }, new List<int> { 0, 0 } },
            });
            Assert.AreEqual(4, s.Bays[0].Capacity);
            s.Tap(0); s.Tap(0); s.Tap(1);
            Assert.AreEqual(3, s.Bays[0].Loaded);       // still waiting for the 4th
            s.Tap(1);
            Assert.AreEqual(GameStatus.Won, s.Status);
        }

        [Test]
        public void TwoColorTruckTakesTwoOfEach()
        {
            var s = new GameState(new LevelData
            {
                TruckCapacity = 3, BufferSize = 3, BayCount = 1, ColorCount = 2,
                TruckColors = new List<int> { 0 }, TruckCapacities = new List<int> { 4 }, TruckSecondColors = new List<int> { 1 },
                Columns = new List<List<int>> { new List<int> { 1, 1, 0 }, new List<int> { 0 } },
            });
            var truck = s.Bays[0];
            var first = s.Tap(0);                       // red -> spot 0
            Assert.AreEqual(0, first[0].ToSlot);
            s.Tap(1);                                   // red -> spot 1 (red half now full)
            Assert.IsFalse(truck.Accepts(0));
            Assert.IsTrue(truck.Accepts(1));
            var blue = s.Tap(0);                        // blue -> spot 2
            Assert.AreEqual(2, blue[0].ToSlot);
            s.Tap(0);
            Assert.AreEqual(GameStatus.Won, s.Status);
        }

        [Test]
        public void SpecialTrucksStartAt36And40()
        {
            int Big(LevelData d) { int n = 0; for (int i = 0; i < d.TruckCapacities.Count; i++) if (d.TruckCapacities[i] == 4 && d.TruckSecondColors[i] < 0) n++; return n; }
            int Two(LevelData d) { int n = 0; foreach (int c in d.TruckSecondColors) if (c >= 0) n++; return n; }
            Assert.AreEqual(0, Big(LevelGenerator.Generate(35)));
            Assert.AreEqual(1, Big(LevelGenerator.Generate(36)));
            Assert.AreEqual(0, Two(LevelGenerator.Generate(39)));
            Assert.AreEqual(1, Two(LevelGenerator.Generate(40)));
            var level = LevelGenerator.Generate(50);
            int crates = 0, spots = 0;
            foreach (var col in level.Columns) crates += col.Count;
            foreach (int cap in level.TruckCapacities) spots += cap;
            Assert.AreEqual(spots, crates);             // every crate has a spot on some truck
            for (int n = 36; n <= 70; n += 3)
                Assert.IsTrue(Solver.CanSolve(LevelGenerator.Generate(n)), "level " + n);
        }

        // Lock test level: red truck (2 crates), column 0 = red (top, locked by id 1), column 1 = red with the key.
        private static LevelData LockLevel() => new LevelData
        {
            TruckCapacity = 2, BufferSize = 1, BayCount = 1, ColorCount = 1,
            TruckColors = new List<int> { 0 },
            Columns = new List<List<int>> { new List<int> { 0 }, new List<int> { 0 } },
            Locks = new List<List<int>> { new List<int> { 1 }, new List<int> { 0 } },
            Keys = new List<List<int>> { new List<int> { 0 }, new List<int> { 1 } },
        };

        [Test]
        public void LockedCrateWaitsForItsKey()
        {
            var s = new GameState(LockLevel());
            Assert.IsFalse(s.CanTap(0));       // locked
            Assert.IsFalse(s.CanPluck(0, 0));  // the crane can't lift it either
            s.Tap(1);                          // the key crate leaves its stack
            Assert.IsFalse(s.IsLocked(0, 0));
            Assert.IsTrue(s.CanTap(0));
            s.Tap(0);
            Assert.AreEqual(GameStatus.Won, s.Status);
        }

        [Test]
        public void MagnetSkipsLockedCrates()
        {
            var s = new GameState(LockLevel());
            Assert.IsTrue(s.MagnetTarget(out int column, out _));
            Assert.AreEqual(1, column);        // not the locked crate in column 0
        }

        [Test]
        public void LocksStartAtLevel48()
        {
            int Count(LevelData d) { int n = 0; foreach (var col in d.Locks) foreach (int id in col) if (id > 0) n++; return n; }
            Assert.AreEqual(0, Count(LevelGenerator.Generate(47)));
            Assert.AreEqual(1, Count(LevelGenerator.Generate(48)));
            Assert.AreEqual(3, Count(LevelGenerator.Generate(68)));
            for (int n = 48; n <= 80; n += 4)
                Assert.IsTrue(Solver.CanSolve(LevelGenerator.Generate(n)), "level " + n);
        }

        // Bomb test level: red truck (3 crates). Column 0 = red, blue(bomb), red; column 1 = red, blue, blue.
        private static LevelData BombLevel(int bombAt) => new LevelData
        {
            TruckCapacity = 3, BufferSize = 3, BayCount = 1, ColorCount = 2,
            TruckColors = new List<int> { 0, 1 },
            Columns = new List<List<int>> { new List<int> { 0, 1, 0 }, new List<int> { 0, 1, 1 } },
            Bombs = new List<List<int>> { new List<int> { 0, bombAt, 0 }, new List<int> { 0, 0, 0 } },
        };

        [Test]
        public void BombExplodesWhenItsTapsRunOut()
        {
            var s = new GameState(BombLevel(2));
            s.Tap(1);                          // tap 1: blue to the dock
            Assert.AreEqual(1, s.BombTapsLeft(0, 1));
            s.Tap(1);                          // tap 2: the bomb is still in column 0
            Assert.AreEqual(GameStatus.Lost, s.Status);
            Assert.IsTrue(s.Exploded);
            s.ExtendBombs(5);                  // revive
            Assert.AreEqual(GameStatus.Playing, s.Status);
            Assert.AreEqual(5, s.BombTapsLeft(0, 1));
        }

        [Test]
        public void MovingTheBombCrateDefusesIt()
        {
            var s = new GameState(BombLevel(2));
            s.Tap(0);                          // red to the truck
            s.Tap(0);                          // the bomb crate (blue) leaves its stack: safe
            s.Tap(1);
            s.Tap(1);
            Assert.AreNotEqual(GameStatus.Lost, s.Status);
            Assert.IsFalse(s.Exploded);
        }

        [Test]
        public void BombsStartAtLevel56AndStayWinnable()
        {
            int Count(LevelData d) { int n = 0; foreach (var col in d.Bombs) foreach (int b in col) if (b > 0) n++; return n; }
            Assert.AreEqual(0, Count(LevelGenerator.Generate(55)));
            Assert.AreEqual(1, Count(LevelGenerator.Generate(56)));
            Assert.AreEqual(2, Count(LevelGenerator.Generate(70)));
            for (int n = 56; n <= 90; n += 3)
                Assert.IsTrue(Solver.CanSolve(LevelGenerator.Generate(n)), "level " + n);
        }

        // Tarp test level: red truck then blue. Column 0 = blue, blue, blue (covered until column 1 is empty); column 1 = red x3.
        private static LevelData TarpLevel() => new LevelData
        {
            TruckCapacity = 3, BufferSize = 3, BayCount = 1, ColorCount = 2,
            TruckColors = new List<int> { 0, 1 },
            Columns = new List<List<int>> { new List<int> { 1, 1, 1 }, new List<int> { 0, 0, 0 } },
            Tarps = new List<int> { 1, -1 },
        };

        [Test]
        public void TarpOpensWhenItsNeighborIsEmpty()
        {
            var s = new GameState(TarpLevel());
            Assert.IsTrue(s.IsCovered(0));
            Assert.IsFalse(s.CanTap(0));
            Assert.IsFalse(s.CanPluck(0, 0));
            s.Tap(1); s.Tap(1);
            Assert.IsTrue(s.IsCovered(0));
            s.Tap(1);                          // column 1 is empty now
            Assert.IsFalse(s.IsCovered(0));
            s.Tap(0); s.Tap(0); s.Tap(0);
            Assert.AreEqual(GameStatus.Won, s.Status);
        }

        [Test]
        public void TarpsStartAtLevel64AndStayWinnable()
        {
            int Count(LevelData d) { int n = 0; foreach (int t in d.Tarps) if (t >= 0) n++; return n; }
            Assert.AreEqual(0, Count(LevelGenerator.Generate(63)));
            Assert.AreEqual(1, Count(LevelGenerator.Generate(64)));
            for (int n = 64; n <= 100; n += 4)
                Assert.IsTrue(Solver.CanSolve(LevelGenerator.Generate(n)), "level " + n);
        }

        [Test]
        public void WorldTourBuildsLandmarksWithStars()
        {
            int city = 0, mask = 0, stars = 2;
            Assert.IsFalse(WorldTour.CanBuildAny(city, mask, stars));
            Assert.AreEqual(WorldTour.BuildResult.Failed, WorldTour.Build(ref city, ref mask, ref stars, 0, out _));
            stars = 3;
            Assert.AreEqual(WorldTour.BuildResult.Built, WorldTour.Build(ref city, ref mask, ref stars, 0, out int coins));
            Assert.AreEqual(0, stars);
            Assert.AreEqual(1, mask);
            Assert.AreEqual(WorldTour.MarkCoins(0, 0), coins);
            Assert.AreEqual(WorldTour.BuildResult.Failed, WorldTour.Build(ref city, ref mask, ref stars, 0, out _)); // already built
        }

        [Test]
        public void FinishingACityMovesTheTourOn()
        {
            int city = 0, mask = 0, stars = 100;
            for (int i = 0; i < 3; i++) WorldTour.Build(ref city, ref mask, ref stars, i, out _);
            Assert.AreEqual(WorldTour.BuildResult.CityDone, WorldTour.Build(ref city, ref mask, ref stars, 3, out _));
            Assert.AreEqual(1, city);
            Assert.AreEqual(0, mask);
            Assert.AreEqual(100 - (3 + 4 + 5 + 6), stars);
        }

        [Test]
        public void WorldTourDataIsComplete()
        {
            Assert.AreEqual(8, WorldTour.Cities.Length);
            foreach (var c in WorldTour.Cities)
            {
                Assert.AreEqual(WorldTour.MarksPerCity, c.Marks.Length);
                foreach (var m in c.Marks) Assert.IsTrue(WorldTour.Shapes.ContainsKey(m.Shape), m.Shape);
            }
            Assert.AreEqual(1, WorldTour.StarsForWin(4));
            Assert.AreEqual(2, WorldTour.StarsForWin(5));   // hard level
            Assert.AreEqual(0, WorldTour.StarsForWin(0));   // daily challenge
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
