using System.Collections.Generic;
using UnityEngine;
using Yunus.Game.Core;
using Yunus.Game.Domain.Ports;
using Yunus.Game.Gameplay;
namespace Yunus.Game.Generation
{
    /// <summary>
    /// Generates puzzle shapes procedurally using a multi-seed, BFS-like growth algorithm.
    ///
    /// ALGORITHM OVERVIEW:
    /// 1. Seeds are distributed across the grid (corners → edges → centre) by
    ///    <see cref="SeedCellSelector"/>.
    /// 2. Each seed becomes the root of a shape.
    /// 3. Shapes grow simultaneously, turn-based, via <see cref="NeighborSelector"/>'s 3-priority
    ///    rules.
    /// 4. <see cref="EnsureFullCoverage"/> then claims every triangle the growth phase missed, so
    ///    coverage is 100% by construction.
    ///
    /// Ownership lives on <see cref="Triangle.ownerShapeIndex"/>; a running <c>_unownedCount</c>
    /// replaces the old per-move rebuild of an "unowned pool", making each claim O(1).
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

        // The single randomness source for generation - seed placement, moves-per-turn, neighbour
        // tie-breaks, and the palette shuffle / colour fallback all draw from it. Injectable so a
        // whole run (topology *and* colours) is reproducible from one seed; defaults to a fresh
        // instance for "different every run".
        private readonly System.Random rng;

        // Live generation bookkeeping.
        private int unownedCount;
        private ShapeData[] shapeByIndex = System.Array.Empty<ShapeData>();

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
            int minTrianglesPerBox = 4,
            System.Random rng = null)
        {
            this.gridBuilder = gridBuilder;
            this.shapePool = shapePool;
            this.parentTransform = parent;
            this.colorPalette = colorPalette;
            this.minTrianglesPerBox = minTrianglesPerBox;
            this.rng = rng ?? new System.Random();

            Shapes = new List<ShapeData>();
            ShapeCount = 6;
            SeedMinCellDistance = 2;
            PreSeedCorners = true;
        }

        /// <summary>
        /// Main generation pipeline: picks seeds, creates shapes, grows them, then guarantees a
        /// fully covered grid.
        /// </summary>
        public void GenerateShapes()
        {
            colorPalette?.Shuffle(rng);
            Shapes.Clear();

            int K = ShapeCount;
            if (gridBuilder.AllTriangles.Count == 0 || K <= 0) return;

            unownedCount = gridBuilder.AllTriangles.Count;

            var seeds = PickDistributedSeeds(K);
            if (seeds.Count == 0)
            {
                Debug.LogWarning("[ShapeGenerator] No seed cells available; nothing generated.");
                return;
            }

            if (seeds.Count < K)
            {
                Debug.LogWarning(
                    $"[ShapeGenerator] Grid only fits {seeds.Count} of {K} requested shapes; " +
                    "generating with the smaller count.");
            }

            shapeByIndex = new ShapeData[seeds.Count];
            for (int i = 0; i < seeds.Count; i++)
            {
                var shape = CreateShape(i, seeds[i]);
                Shapes.Add(shape);
                shapeByIndex[i] = shape;
            }

            GrowShapes();
            EnsureFullCoverage();

            GameLog.Info(
                $"[ShapeGenerator] Generated {Shapes.Count} shapes covering " +
                $"{gridBuilder.AllTriangles.Count - unownedCount}/{gridBuilder.AllTriangles.Count} " +
                $"triangles (minTrianglesPerBox: {minTrianglesPerBox})");
        }

        /// <summary>
        /// Creates a new shape rooted on a single seed triangle.
        /// </summary>
        ShapeData CreateShape(int index, Triangle seedTri)
        {
            var root = shapePool.SpawnImmediate(Vector3.zero, Quaternion.identity, parentTransform);
            root.name = $"Shape_{index:D2}";
            root.SetActive(true);

            var shapeData = root.GetComponent<ShapeData>();
            shapeData.ShapeIndex = index;
            shapeData.ShapeColor = PickColor(index);
            shapeData.MovesPerTurn = rng.Next(1, 3);

            ClaimTriangle(seedTri, shapeData);
            shapeData.GrowthQueue.Enqueue(seedTri);

            return shapeData;
        }

        /// <summary>
        /// Grows all shapes simultaneously using turn-based expansion until no shape can expand
        /// (or the grid is full). Any triangles left over are handled by
        /// <see cref="EnsureFullCoverage"/>.
        /// </summary>
        void GrowShapes()
        {
            bool anyProgress = true;

            while (anyProgress && unownedCount > 0)
            {
                anyProgress = false;

                foreach (var shape in Shapes)
                {
                    if (shape.GrowthQueue.Count == 0) continue;

                    int movesToMake = shape.MovesPerTurn;

                    for (int moveIdx = 0; moveIdx < movesToMake; moveIdx++)
                    {
                        if (shape.GrowthQueue.Count == 0 || unownedCount == 0) break;

                        var currentTri = shape.GrowthQueue.Dequeue();

                        if (NeighborSelector.TryPickNext(
                                currentTri, shape, gridBuilder, minTrianglesPerBox, rng, out var picked))
                        {
                            ClaimTriangle(picked, shape);
                            shape.GrowthQueue.Enqueue(picked);
                            anyProgress = true;
                        }
                        else if (TryGrowFromAnyOwned(shape, currentTri, out var fallback))
                        {
                            ClaimTriangle(fallback, shape);
                            shape.GrowthQueue.Enqueue(fallback);
                            anyProgress = true;
                        }
                        // else: this frontier triangle can't expand - drop it (EnsureFullCoverage
                        // will mop up anything this leaves behind).
                    }
                }
            }
        }

        /// <summary>
        /// Retries neighbour selection from every triangle the shape already owns (except the one
        /// that just failed), in case a different frontier position can still expand.
        /// </summary>
        bool TryGrowFromAnyOwned(ShapeData shape, Triangle exclude, out Triangle picked)
        {
            picked = null;
            if (unownedCount == 0) return false;

            var owned = shape.OccupiedTriangles;
            for (int i = 0; i < owned.Count; i++)
            {
                var tri = owned[i];
                if (tri == null || tri == exclude) continue;

                if (NeighborSelector.TryPickNext(
                        tri, shape, gridBuilder, minTrianglesPerBox, rng, out picked))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Guarantees 100% coverage: BFS from the frontier of owned triangles, assigning every
        /// still-unowned triangle to an adjacent shape. The grid triangle-graph is connected and
        /// every shape has a seed, so this always drains <c>unownedCount</c> to zero.
        /// </summary>
        void EnsureFullCoverage()
        {
            if (unownedCount <= 0 || Shapes.Count == 0) return;

            var queue = new Queue<Triangle>();
            foreach (var tri in gridBuilder.AllTriangles)
                if (tri.ownerShapeIndex < 0 && TryGetOwnedAdjacentShape(tri, out _))
                    queue.Enqueue(tri);

            while (unownedCount > 0 && queue.Count > 0)
            {
                var tri = queue.Dequeue();
                if (tri.ownerShapeIndex >= 0) continue;
                if (!TryGetOwnedAdjacentShape(tri, out var shape)) continue;

                ClaimTriangle(tri, shape);

                foreach (var nb in NeighborTriangles(tri))
                    if (nb.ownerShapeIndex < 0) queue.Enqueue(nb);
            }

            if (unownedCount > 0)
            {
                // Unreachable on a connected grid with >=1 shape; kept as a hard guarantee.
                foreach (var tri in gridBuilder.AllTriangles)
                    if (tri.ownerShapeIndex < 0) ClaimTriangle(tri, Shapes[0]);

                Debug.LogWarning(
                    "[ShapeGenerator] Coverage sweep found an isolated pocket; force-assigned the remainder.");
            }
        }

        /// <summary>First shape that owns a triangle adjacent to <paramref name="tri"/>.</summary>
        bool TryGetOwnedAdjacentShape(Triangle tri, out ShapeData shape)
        {
            foreach (var nb in NeighborTriangles(tri))
            {
                if (nb.ownerShapeIndex >= 0 && nb.ownerShapeIndex < shapeByIndex.Length)
                {
                    shape = shapeByIndex[nb.ownerShapeIndex];
                    if (shape != null) return true;
                }
            }

            shape = null;
            return false;
        }

        /// <summary>
        /// The (up to) four triangles adjacent to <paramref name="tri"/>: its three same-cell
        /// siblings plus the one triangle it faces in the neighbouring cell.
        /// </summary>
        IEnumerable<Triangle> NeighborTriangles(Triangle tri)
        {
            for (int pos = 1; pos <= 4; pos++)
            {
                if (pos == tri.posIndex) continue;
                if (gridBuilder.TryGetTriangle(tri.x, tri.y, pos, out var sibling))
                    yield return sibling;
            }

            var off = FacingOffset(tri.posIndex);
            int nx = tri.x + off.x, ny = tri.y + off.y;
            if (gridBuilder.TryGetTriangle(nx, ny, Triangle.OppositePosIndex(tri.posIndex), out var facing))
                yield return facing;
        }

        static Vector2Int FacingOffset(int posIndex) => posIndex switch
        {
            1 => new Vector2Int(0, 1),   // Up
            2 => new Vector2Int(1, 0),   // Right
            3 => new Vector2Int(-1, 0),  // Left
            4 => new Vector2Int(0, -1),  // Down
            _ => Vector2Int.zero
        };

        /// <summary>
        /// Claims a triangle for a shape: records ownership, reparents, colours, registers.
        /// </summary>
        void ClaimTriangle(Triangle tri, ShapeData shape)
        {
            if (tri.ownerShapeIndex < 0) unownedCount--;
            tri.ownerShapeIndex = shape.ShapeIndex;

            tri.transform.SetParent(shape.transform, true);
            ColorTriangle(tri.gameObject, shape.ShapeColor);
            shape.RegisterTriangle(tri);
        }

        /// <summary>
        /// Applies shape color to a triangle using TriangleMeshRenderer, when present.
        /// </summary>
        void ColorTriangle(GameObject go, Color color)
        {
            var meshRenderer = go.GetComponent<TriangleMeshRenderer>();
            if (meshRenderer != null)
                meshRenderer.SetColor(color);
        }

        /// <summary>
        /// Picks color for shape by index from palette. Falls back to a random vivid HSV colour
        /// (drawn from <see cref="rng"/>) if no palette is set.
        /// </summary>
        Color PickColor(int index)
        {
            if (colorPalette != null)
            {
                var c = colorPalette.GetByIndex(index);
                return new Color(c.R, c.G, c.B, c.A);
            }

            float h = (float)rng.NextDouble();
            float s = 0.9f + 0.1f * (float)rng.NextDouble();
            float v = 0.95f + 0.05f * (float)rng.NextDouble();
            return Color.HSVToRGB(h, s, v);
        }

        /// <summary>
        /// Selects well-distributed seed triangles across the grid.
        ///
        /// Cell selection is delegated to <see cref="SeedCellSelector"/>, which always returns
        /// exactly <c>min(K, cellCount)</c> distinct, in-bounds cells - it honours
        /// <see cref="SeedMinCellDistance"/> where the grid allows and relaxes it otherwise. Each
        /// chosen cell is mapped to a concrete triangle via <see cref="FindTriangleAt"/>.
        /// </summary>
        List<Triangle> PickDistributedSeeds(int K)
        {
            var cells = SeedCellSelector.Select(
                gridBuilder.GridWidth,
                gridBuilder.GridHeight,
                K,
                SeedMinCellDistance,
                PreSeedCorners,
                rng);

            var seeds = new List<Triangle>(cells.Count);
            foreach (var cell in cells)
            {
                var tri = FindTriangleAt(cell.x, cell.y);
                if (tri != null) seeds.Add(tri);
            }

            return seeds;
        }

        /// <summary>Any triangle in cell (<paramref name="x"/>, <paramref name="y"/>).</summary>
        Triangle FindTriangleAt(int x, int y)
        {
            for (int pos = 1; pos <= 4; pos++)
                if (gridBuilder.TryGetTriangle(x, y, pos, out var tri))
                    return tri;

            return null;
        }

        /// <summary>
        /// Clears all generated shapes and returns them to the pool.
        /// </summary>
        public void Clear()
        {
            foreach (var shape in Shapes)
                if (shape != null) shapePool.Despawn(shape.gameObject);

            Shapes.Clear();
            shapeByIndex = System.Array.Empty<ShapeData>();
        }
    }
}
