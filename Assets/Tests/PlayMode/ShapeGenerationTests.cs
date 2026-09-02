using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Yunus.Game.Core;
using Yunus.Game.Gameplay;
using Yunus.Game.Generation;

namespace Yunus.Game.Tests
{
    /// <summary>
    /// PlayMode tests for the shape-growth stage. These lock in two guarantees:
    ///  * every generated level fills 100% of the grid (<see cref="ShapeGenerator.EnsureFullCoverage"/>);
    ///  * shapes form a disjoint partition of the triangles.
    /// They also exercise the full <see cref="NeighborSelector"/> path after its rewrite to
    /// registry lookups.
    /// </summary>
    [TestFixture]
    public class ShapeGenerationTests
    {
        GameObject _root;
        FakePool _trianglePool;
        FakePool _shapePool;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("TestRoot");
            _trianglePool = new FakePool(() =>
            {
                var go = new GameObject("Tri");
                go.AddComponent<Triangle>();
                return go;
            });
            _shapePool = new FakePool(() =>
            {
                var go = new GameObject("Shape");
                go.AddComponent<ShapeData>();
                return go;
            });
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) UnityEngine.Object.DestroyImmediate(_root);
            _trianglePool?.DestroyRemaining();
            _shapePool?.DestroyRemaining();
        }

        /// <summary>
        /// Every levels.json config, plus the two that used to crash before the seed fix
        /// (5x5 K=9 d=2, 6x6 K=10 d=2), at the parameters that were shipped there.
        /// Columns: name, width, height, shapeCount, minTrianglesPerBox, seedMinCellDistance.
        /// </summary>
        static readonly object[] Configs =
        {
            new object[] { "level-1", 4, 4, 5, 4, 1 },
            new object[] { "level-2", 4, 4, 6, 4, 1 },
            new object[] { "level-3", 4, 4, 7, 4, 1 },
            new object[] { "level-4", 5, 5, 7, 4, 2 },
            new object[] { "level-5", 5, 5, 8, 4, 2 },
            new object[] { "level-6", 5, 5, 8, 4, 2 },
            new object[] { "level-7", 6, 6, 10, 3, 1 },
            new object[] { "level-8", 6, 6, 11, 2, 1 },
            new object[] { "level-9", 6, 6, 12, 2, 1 },
            new object[] { "orig-6",  5, 5, 9, 4, 2 },
            new object[] { "orig-7",  6, 6, 10, 3, 2 },
        };

        [Test]
        public void Generate_FillsWholeGrid_AndPartitionsTriangles(
            [ValueSource(nameof(Configs))] object[] cfg)
        {
            var (name, w, h, shapeCount, minPerBox, seedDist) = Unpack(cfg);

            for (int seed = 0; seed < 20; seed++)
            {
                using (var run = Generate(w, h, shapeCount, minPerBox, seedDist, seed))
                {
                    var triangles = run.Grid.AllTriangles;
                    int unowned = triangles.Count(t => t.ownerShapeIndex < 0);
                    Assert.AreEqual(0, unowned,
                        $"{name} (seed {seed}): {unowned}/{triangles.Count} triangles left unclaimed");

                    Assert.AreEqual(shapeCount, run.Gen.Shapes.Count,
                        $"{name} (seed {seed}): wrong shape count");

                    var owned = run.Gen.Shapes.SelectMany(s => s.OccupiedTriangles).ToList();
                    Assert.AreEqual(triangles.Count, owned.Count,
                        $"{name} (seed {seed}): occupied-triangle total != grid size");
                    Assert.AreEqual(owned.Count, owned.Distinct().Count(),
                        $"{name} (seed {seed}): a triangle is owned by more than one shape");

                    foreach (var shape in run.Gen.Shapes)
                        foreach (var tri in shape.OccupiedTriangles)
                            Assert.AreEqual(shape.ShapeIndex, tri.ownerShapeIndex,
                                $"{name} (seed {seed}): triangle ownerShapeIndex disagrees with its shape");
                }
            }
        }

        [Test]
        public void Generate_EveryCellBelongsToOneShape()
        {
            using var run = Generate(6, 6, 10, 3, 1, seed: 123);

            foreach (var tri in run.Grid.AllTriangles)
                Assert.GreaterOrEqual(tri.ownerShapeIndex, 0);

            // Each of the 6x6 cells has all four of its triangles owned.
            var byCell = run.Grid.AllTriangles.GroupBy(t => t.boxIndex);
            foreach (var cell in byCell)
                Assert.AreEqual(4, cell.Count(t => t.ownerShapeIndex >= 0),
                    $"cell {cell.Key} is not fully claimed");
        }

        [Test]
        public void Generate_IsReproducible_FromTheInjectedSeedAlone()
        {
            // Topology draws only from the injected System.Random now (no UnityEngine.Random),
            // so the exact per-triangle ownership must match between two same-seed runs.
            List<int> Owners(int seed)
            {
                using var run = Generate(6, 6, 10, 3, 1, seed);
                return run.Grid.AllTriangles.Select(t => t.ownerShapeIndex).ToList();
            }

            CollectionAssert.AreEqual(Owners(7), Owners(7));
            CollectionAssert.AreEqual(Owners(99), Owners(99));
            Assert.IsFalse(Owners(7).SequenceEqual(Owners(99)),
                "different seeds produced identical ownership");
        }

        // ---- helpers ----

        static (string name, int w, int h, int shapeCount, int minPerBox, int seedDist) Unpack(object[] c)
            => ((string)c[0], (int)c[1], (int)c[2], (int)c[3], (int)c[4], (int)c[5]);

        Run Generate(int w, int h, int shapeCount, int minPerBox, int seedDist, int seed)
        {
            var grid = new GridBuilder(_trianglePool, _root.transform, w, h);
            grid.BuildGrid();

            var gen = new ShapeGenerator(grid, _shapePool, _root.transform, null, minPerBox,
                new System.Random(seed))
            {
                ShapeCount = shapeCount,
                SeedMinCellDistance = seedDist,
                PreSeedCorners = true,
            };
            gen.GenerateShapes();

            return new Run(grid, gen);
        }

        sealed class Run : IDisposable
        {
            public readonly GridBuilder Grid;
            public readonly ShapeGenerator Gen;
            public Run(GridBuilder grid, ShapeGenerator gen) { Grid = grid; Gen = gen; }

            public void Dispose()
            {
                Gen.Clear();
                Grid.DeSpawn();
            }
        }

        /// <summary>Minimal <see cref="IPrefabPool"/>: instantiates via a factory, tracks lifetimes.</summary>
        sealed class FakePool : IPrefabPool
        {
            readonly Func<GameObject> _factory;
            readonly List<GameObject> _live = new();

            public FakePool(Func<GameObject> factory) => _factory = factory;

            public GameObject Spawn(Vector3 pos, Quaternion rot, Transform parent = null)
                => SpawnImmediate(pos, rot, parent);

            public GameObject SpawnImmediate(Vector3 pos, Quaternion rot, Transform parent = null)
            {
                var go = _factory();
                go.transform.SetParent(parent, false);
                go.transform.SetPositionAndRotation(pos, rot);
                go.SetActive(true);
                _live.Add(go);
                return go;
            }

            public void Despawn(GameObject instance)
            {
                _live.Remove(instance);
                if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
            }

            public void DespawnAll()
            {
                foreach (var go in _live.ToArray()) Despawn(go);
            }

            public void DestroyRemaining() => DespawnAll();

            public (int available, int inUse, int total) Stats => (0, _live.Count, _live.Count);
        }
    }
}
