using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Yunus.Game.Generation;

namespace Yunus.Game.Tests
{
    /// <summary>
    /// Regression + behaviour tests for <see cref="SeedCellSelector"/>.
    ///
    /// The bug this guards against: the previous inline seed picker in <c>ShapeGenerator</c> could
    /// return fewer cells than <c>shapeCount</c> on tight grids, and <c>GenerateShapes</c> then
    /// indexed past the end of the list and threw, aborting level generation (levels 6 and 7).
    /// </summary>
    [TestFixture]
    public class SeedCellSelectorTests
    {
        /// <summary>
        /// The full levels.json matrix using the ORIGINAL (pre-fix) parameters, so level-6
        /// (5x5, K=9, d=2) and level-7 (6x6, K=10, d=2) are exercised at the values that used to
        /// crash.
        /// </summary>
        static readonly object[] LevelMatrix =
        {
            new object[] { "level-1", 4, 4, 5, 1 },
            new object[] { "level-2", 4, 4, 6, 1 },
            new object[] { "level-3", 4, 4, 7, 1 },
            new object[] { "level-4", 5, 5, 7, 2 },
            new object[] { "level-5", 5, 5, 8, 2 },
            new object[] { "level-6", 5, 5, 9, 2 },
            new object[] { "level-7", 6, 6, 10, 2 },
            new object[] { "level-8", 6, 6, 11, 1 },
            new object[] { "level-9", 6, 6, 12, 1 },
        };

        [TestCaseSource(nameof(LevelMatrix))]
        public void Select_ForEveryLevelConfig_ReturnsRequestedCount(
            string levelId, int width, int height, int shapeCount, int minDist)
        {
            int expected = Math.Min(shapeCount, width * height);

            // Many seeds: the old greedy loop failed intermittently (~50% for level-6).
            for (int seed = 0; seed < 250; seed++)
            {
                var cells = SeedCellSelector.Select(
                    width, height, shapeCount, minDist, true, new System.Random(seed));

                Assert.AreEqual(expected, cells.Count,
                    $"{levelId} (seed {seed}): expected {expected} seed cells, got {cells.Count}");
                CollectionAssert.AllItemsAreUnique(cells,
                    $"{levelId} (seed {seed}): duplicate seed cell");
                Assert.IsTrue(
                    cells.All(c => c.x >= 0 && c.x < width && c.y >= 0 && c.y < height),
                    $"{levelId} (seed {seed}): seed cell outside the grid");
            }
        }

        [Test]
        public void Select_WhenDistanceFits_AllPairsRespectMinDistance()
        {
            // 4x4, 4 seeds, min Chebyshev distance 2 -> exactly the four corners.
            var cells = SeedCellSelector.Select(4, 4, 4, 2, true, new System.Random(1));

            Assert.AreEqual(4, cells.Count);
            AssertChebyshevAtLeast(cells, 2);
        }

        [Test]
        public void Select_WhenGridCannotFitDistance_StillReturnsFullCount()
        {
            // 5x5, 9 seeds all >= 2 apart is impossible; must relax the distance, not fail.
            var cells = SeedCellSelector.Select(5, 5, 9, 2, true, new System.Random(7));

            Assert.AreEqual(9, cells.Count);
            CollectionAssert.AllItemsAreUnique(cells);
        }

        [Test]
        public void Select_WhenShapeCountExceedsCellCount_ClampsToCellCount()
        {
            var cells = SeedCellSelector.Select(4, 4, 100, 1, true, new System.Random(3));

            Assert.AreEqual(16, cells.Count);
            CollectionAssert.AllItemsAreUnique(cells);
        }

        [Test]
        public void Select_IsDeterministicForSameSeed()
        {
            var a = SeedCellSelector.Select(6, 6, 10, 2, true, new System.Random(42));
            var b = SeedCellSelector.Select(6, 6, 10, 2, true, new System.Random(42));

            CollectionAssert.AreEqual(a, b);
        }

        [Test]
        public void Select_ProducesVariedLayoutsAcrossSeeds()
        {
            var layouts = Enumerable.Range(0, 8)
                .Select(s => SeedCellSelector.Select(6, 6, 10, 1, true, new System.Random(s)))
                .ToList();

            bool anyDifferent = layouts.Skip(1).Any(l => !l.SequenceEqual(layouts[0]));
            Assert.IsTrue(anyDifferent, "every seed produced an identical layout");
        }

        [Test]
        public void Select_WithPreSeedCorners_IncludesAllFourCorners()
        {
            var cells = SeedCellSelector.Select(6, 6, 8, 2, true, new System.Random(5));

            Assert.Contains(new Vector2Int(0, 0), cells);
            Assert.Contains(new Vector2Int(5, 0), cells);
            Assert.Contains(new Vector2Int(0, 5), cells);
            Assert.Contains(new Vector2Int(5, 5), cells);
        }

        [Test]
        public void Select_WithoutPreSeedCorners_StillReturnsFullCount()
        {
            var cells = SeedCellSelector.Select(6, 6, 12, 2, false, new System.Random(9));

            Assert.AreEqual(12, cells.Count);
            CollectionAssert.AllItemsAreUnique(cells);
        }

        [TestCase(0, 4)]
        [TestCase(4, 0)]
        [TestCase(-1, 5)]
        public void Select_WithNonPositiveDimensions_ReturnsEmpty(int width, int height)
        {
            var cells = SeedCellSelector.Select(width, height, 5, 1, true, new System.Random(0));

            Assert.IsEmpty(cells);
        }

        [Test]
        public void Select_WithZeroShapeCount_ReturnsEmpty()
        {
            var cells = SeedCellSelector.Select(5, 5, 0, 2, true, new System.Random(0));

            Assert.IsEmpty(cells);
        }

        [Test]
        public void Select_WithNullRng_DoesNotThrow()
        {
            Assert.DoesNotThrow(
                () => SeedCellSelector.Select(5, 5, 8, 2, true, null));
        }

        static void AssertChebyshevAtLeast(IReadOnlyList<Vector2Int> cells, int minDist)
        {
            for (int i = 0; i < cells.Count; i++)
            {
                for (int j = i + 1; j < cells.Count; j++)
                {
                    int d = Math.Max(
                        Math.Abs(cells[i].x - cells[j].x),
                        Math.Abs(cells[i].y - cells[j].y));
                    Assert.GreaterOrEqual(d, minDist,
                        $"cells {cells[i]} and {cells[j]} are closer than {minDist}");
                }
            }
        }
    }
}
