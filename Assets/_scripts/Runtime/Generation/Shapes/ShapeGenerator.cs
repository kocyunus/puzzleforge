using System.Collections.Generic;
using UnityEngine;
using Yunus.Game.Core;
using Yunus.Game.Domain.Ports;
using Yunus.Game.Gameplay;
namespace Yunus.Game.Generation
{
    /// <summary>
    /// Generates puzzle shapes procedurally using multi-seed BFS-like growth algorithm.
    /// 
    /// ALGORITHM OVERVIEW:
    /// 1. Seeds are distributed across grid (corners → edges → center)
    /// 2. Each seed becomes root of a shape
    /// 3. Shapes grow simultaneously in turn-based fashion
    /// 4. Growth uses 3-priority neighbor selection to fill grid evenly
    /// 5. Guaranteed 100% grid coverage with balanced shape sizes
    /// 
    /// WHY MULTI-SEED?
    /// - Single seed cannot distribute evenly across entire grid
    /// - Multi-seed provides natural, balanced partition
    /// - Reduces dead-zones and ensures every triangle is claimed
    /// - Different seed positions = different puzzle each run
    /// </summary>
    public class ShapeGenerator
    {
        private readonly IPrefabPool shapePool;

        // Input dependencies
        private readonly GridBuilder gridBuilder;
        private readonly Transform parentTransform;
        private readonly IColorPalette colorPalette;

        // Configuration
        public int ShapeCount { get; set; }
        public int SeedMinCellDistance { get; set; }
        public bool PreSeedCorners { get; set; }
        private int minTrianglesPerBox;

        // Output
        public List<ShapeData> Shapes { get; private set; }

        /// <summary>
        /// Initializes the shape generator with grid and pool references.
        /// </summary>
        public ShapeGenerator(
            GridBuilder gridBuilder,
            IPrefabPool shapePool,
            Transform parent,
            IColorPalette colorPalette = null,
            int minTrianglesPerBox = 4)
        {
            this.gridBuilder = gridBuilder;
            this.shapePool = shapePool;
            this.parentTransform = parent;
            this.colorPalette = colorPalette;
            this.minTrianglesPerBox = minTrianglesPerBox;

            Shapes = new List<ShapeData>();
            ShapeCount = 6;
            SeedMinCellDistance = 2;
            PreSeedCorners = true;
        }

        /// <summary>
        /// Main generation pipeline: picks seeds, creates shapes, grows them to fill grid.
        /// 
        /// STEPS:
        /// 1. Shuffle color palette (different colors each run)
        /// 2. Pick well-distributed seeds (far from each other)
        /// 3. Instantiate shape GameObjects at seed positions
        /// 4. Run multi-round growth until grid is filled
        /// 5. Output: List of complete, non-overlapping shapes
        /// </summary>
        public void GenerateShapes()
        {
            colorPalette?.Shuffle();
            Shapes.Clear();

            int K = ShapeCount;
            if (gridBuilder.TriangleGameObjects.Count == 0 || K <= 0) return;

            var seeds = PickDistributedSeeds(K);

            for (int i = 0; i < K; i++)
            {
                
                var shape = CreateShape(i, seeds[i]);
                Shapes.Add(shape);
            }

            GrowShapes();

            Debug.Log($"[ShapeGenerator] Generated {K} shapes (minTrianglesPerBox: {minTrianglesPerBox})");
        }

        /// <summary>
        /// Creates a new shape with a single seed triangle.
        /// 
        /// STEPS:
        /// 1. Spawn shape root GameObject from pool
        /// 2. Assign unique index and color
        /// 3. Move seed triangle as child of shape
        /// 4. Color seed triangle with shape color
        /// 5. Register seed in growth queue
        /// </summary>
        ShapeData CreateShape(int index, int seedTriangleIndex)
        {
            // Spawn shape root from pool
            var root = shapePool.SpawnImmediate(Vector3.zero, Quaternion.identity, parentTransform);
            root.name = $"Shape_{index:D2}";
            root.SetActive(true);

            var shapeData = root.GetComponent<ShapeData>();
            shapeData.ShapeIndex = index;
            shapeData.ShapeColor = PickColor(index);
            shapeData.MovesPerTurn = Random.Range(1, 3);

            var seedGO = gridBuilder.TriangleGameObjects[seedTriangleIndex];
            seedGO.transform.SetParent(root.transform, true);
            ColorTriangle(seedGO, shapeData.ShapeColor);

            var seedTri = seedGO.GetComponent<Triangle>();
            shapeData.RegisterTriangle(seedTri);
            shapeData.GrowthQueue.Enqueue(seedTriangleIndex);

            return shapeData;
        }

        /// <summary>
        /// Grows all shapes simultaneously using turn-based expansion.
        /// 
        /// ALGORITHM:
        /// 1. Each shape gets a turn to expand by MovesPerTurn triangles
        /// 2. For each move: pop triangle from growth queue
        /// 3. Find valid neighbor using 3-priority selector
        /// 4. Claim neighbor (parent, color, register)
        /// 5. Add neighbor to growth queue
        /// 6. Repeat until all shapes can't expand (grid filled)
        /// 
        /// GUARANTEES:
        /// - 100% grid coverage (Priority 3 = final fallback)
        /// - Balanced shape sizes (turn-based prevents hogging)
        /// - Varied results (MovesPerTurn randomized)
        /// </summary>
        void GrowShapes()
        {
            bool anyProgress = true;

            while (anyProgress)
            {
                anyProgress = false;

                foreach (var shape in Shapes)
                {
                    if (shape.GrowthQueue.Count == 0) continue;

                    int movesToMake = shape.MovesPerTurn;

                    for (int moveIdx = 0; moveIdx < movesToMake; moveIdx++)
                    {
                        if (shape.GrowthQueue.Count == 0) break;

                        int currentIdx = shape.GrowthQueue.Dequeue();
                        var currentTri = gridBuilder.TriangleGameObjects[currentIdx].GetComponent<Triangle>();

                        var pool = GetUnownedTriangles();
                        if (pool.Count == 0) continue;

                        if (NeighborSelector.TryPickNext(
                            currentTri,
                            pool,
                            Shapes,
                            gridBuilder.AllTriangles,
                            shape.ShapeIndex,
                            minTrianglesPerBox,
                            out int pickedIdx,
                            out _))
                        {
                            var pickedTri = pool[pickedIdx];
                            ClaimTriangle(pickedTri, shape);

                            int globalIdx = gridBuilder.TriangleGameObjects.IndexOf(pickedTri.gameObject);
                            shape.GrowthQueue.Enqueue(globalIdx);
                            anyProgress = true;
                        }
                        else
                        {
                            if (TryGrowFromAnyOwned(shape, currentTri, out var picked))
                            {
                                ClaimTriangle(picked, shape);

                                int globalIdx = gridBuilder.TriangleGameObjects.IndexOf(picked.gameObject);
                                shape.GrowthQueue.Enqueue(globalIdx);
                                anyProgress = true;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Attempts to claim a neighbor triangle for a shape.
        /// Uses 3-priority neighbor selection to ensure coverage.
        /// </summary>
        bool TryGrowFromAnyOwned(ShapeData shape, Triangle exclude, out Triangle picked)
        {
            picked = null;
            var pool = GetUnownedTriangles();
            if (pool.Count == 0) return false;

            foreach (var tri in shape.OccupiedTriangles)
            {
                if (tri == exclude) continue;

                if (NeighborSelector.TryPickNext(
                    tri,
                    pool,
                    Shapes,
                    gridBuilder.AllTriangles,
                    shape.ShapeIndex,
                    minTrianglesPerBox,
                    out int idx,
                    out _))
                {
                    picked = pool[idx];
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Claims a triangle for a shape (parent, color, register).
        /// </summary>
        void ClaimTriangle(Triangle tri, ShapeData shape)
        {
            tri.gameObject.transform.SetParent(shape.transform, true);
            ColorTriangle(tri.gameObject, shape.ShapeColor);
            shape.RegisterTriangle(tri);
        }

        /// <summary>
        /// Gets all triangles not yet owned by any shape.
        /// </summary>
        List<Triangle> GetUnownedTriangles()
        {
            var owned = new HashSet<Triangle>();
            foreach (var shape in Shapes)
            {
                if (shape == null) continue;
                foreach (var tri in shape.OccupiedTriangles)
                    if (tri != null) owned.Add(tri);
            }

            var pool = new List<Triangle>();
            foreach (var go in gridBuilder.TriangleGameObjects)
            {
                var tri = go.GetComponent<Triangle>();
                if (tri != null && !owned.Contains(tri))
                    pool.Add(tri);
            }

            return pool;
        }

        /// <summary>
        /// Applies shape color to a triangle using TriangleMeshRenderer.
        /// Custom renderer handles vertex colors and MaterialPropertyBlock updates.
        /// </summary>
        void ColorTriangle(GameObject go, Color color)
        {
            // Use TriangleMeshRenderer instead of Shapes.Triangle
            var meshRenderer = go.GetComponent<TriangleMeshRenderer>();
            if (meshRenderer != null)
            {
                meshRenderer.SetColor(color);
            }
        }

        /// <summary>
        /// Picks color for shape by index from palette.
        /// Falls back to random HSV if no palette available.
        /// </summary>
        Color PickColor(int index)
        {
            if (colorPalette != null)
            {
                var c = colorPalette.GetByIndex(index);
                return new Color(c.R, c.G, c.B, c.A);
            }

            return Random.ColorHSV(0f, 1f, 0.9f, 1f, 0.95f, 1f);
        }

        /// <summary>
        /// Selects well-distributed seed positions across the grid.
        /// 
        /// STRATEGY:
        /// 1. Pre-seed corners (4 corner cells)
        /// 2. Distribute remaining seeds using far-first selection
        /// 3. Maintain minimum distance between seeds
        /// 4. Alternate between edge and center cells for variety
        /// </summary>
        List<int> PickDistributedSeeds(int K)
        {
            var seeds = new List<int>();
            var usedCells = new HashSet<long>();
            var chosenCells = new List<Vector2Int>();

            var corners = new List<Vector2Int>
            {
                new(0, 0),
                new(gridBuilder.GridWidth - 1, 0),
                new(0, gridBuilder.GridHeight - 1),
                new(gridBuilder.GridWidth - 1, gridBuilder.GridHeight - 1),
            };

            var edges = new List<Vector2Int>();
            var centers = new List<Vector2Int>();

            for (int y = 0; y < gridBuilder.GridHeight; y++)
            {
                for (int x = 0; x < gridBuilder.GridWidth; x++)
                {
                    bool isCorner = (x == 0 || x == gridBuilder.GridWidth - 1) &&
                                    (y == 0 || y == gridBuilder.GridHeight - 1);
                    bool isEdge = x == 0 || y == 0 || x == gridBuilder.GridWidth - 1 || y == gridBuilder.GridHeight - 1;

                    if (isCorner) continue;
                    if (isEdge) edges.Add(new(x, y));
                    else centers.Add(new(x, y));
                }
            }

            if (PreSeedCorners && K > 0)
            {
                Shuffle(corners);
                foreach (var corner in corners)
                {
                    if (seeds.Count >= Mathf.Min(K, 4)) break;
                    if (TryAddSeed(corner, usedCells, chosenCells, out int idx))
                        seeds.Add(idx);
                }
            }

            Shuffle(edges);
            Shuffle(centers);

            int phase = Random.Range(0, 2);

            while (seeds.Count < K)
            {
                bool pickEdge = ((seeds.Count + phase) % 2) == 0;
                var primary = pickEdge ? edges : centers;
                var secondary = pickEdge ? centers : edges;

                var chosen = PickBest(primary, usedCells, chosenCells);
                if (chosen.x < 0) chosen = PickBest(secondary, usedCells, chosenCells);
                if (chosen.x < 0) break;

                if (TryAddSeed(chosen, usedCells, chosenCells, out int idx))
                    seeds.Add(idx);
            }

            return seeds;
        }

        bool TryAddSeed(Vector2Int cell, HashSet<long> used, List<Vector2Int> chosen, out int triIdx)
        {
            triIdx = -1;
            long key = PackCell(cell.x, cell.y);

            if (used.Contains(key)) return false;

            triIdx = FindBestTriangleAt(cell.x, cell.y);
            if (triIdx < 0) return false;

            used.Add(key);
            chosen.Add(cell);
            return true;
        }

        Vector2Int PickBest(List<Vector2Int> candidates, HashSet<long> used, List<Vector2Int> chosen)
        {
            if (candidates == null || candidates.Count == 0) return new(-1, -1);

            Vector2Int best = new(-1, -1);
            float bestScore = -1;

            foreach (var p in candidates)
            {
                if (used.Contains(PackCell(p.x, p.y))) continue;
                if (!IsFarEnough(p, chosen)) continue;

                float score = MinDist2(p, chosen);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = p;
                }
            }

            return best;
        }

        bool IsFarEnough(Vector2Int p, List<Vector2Int> chosen)
        {
            if (chosen.Count == 0) return true;

            foreach (var c in chosen)
            {
                int d = Mathf.Max(Mathf.Abs(p.x - c.x), Mathf.Abs(p.y - c.y));
                if (d < SeedMinCellDistance) return false;
            }

            return true;
        }

        float MinDist2(Vector2Int p, List<Vector2Int> chosen)
        {
            if (chosen.Count == 0) return float.PositiveInfinity;

            float best = float.PositiveInfinity;
            foreach (var c in chosen)
            {
                int dx = p.x - c.x, dy = p.y - c.y;
                float d2 = dx * dx + dy * dy;
                if (d2 < best) best = d2;
            }

            return best;
        }

        int FindBestTriangleAt(int x, int y)
        {
            Vector2Int target = new(x, y);

            for (int i = 0; i < gridBuilder.TriangleGameObjects.Count; i++)
            {
                var tri = gridBuilder.TriangleGameObjects[i].GetComponent<Triangle>();
                if (tri.gridPos == target) return i;
            }

            return -1;
        }

        void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        long PackCell(int x, int y) => ((long)x << 32) | (uint)y;

        /// <summary>
        /// Clears all generated shapes and returns them to the pool.
        /// </summary>
        public void Clear()
        {
            foreach (var shape in Shapes)
                if (shape != null) shapePool.Despawn(shape.gameObject);

            Shapes.Clear();
        }
    }
}
