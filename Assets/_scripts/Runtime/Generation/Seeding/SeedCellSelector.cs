using System;
using System.Collections.Generic;
using UnityEngine;

namespace Yunus.Game.Generation
{
    /// <summary>
    /// Chooses well-distributed seed cells for shape generation.
    ///
    /// This is a pure, engine-light helper (only <see cref="Vector2Int"/> is used from Unity) so it
    /// can be unit-tested without entering play mode.
    ///
    /// GUARANTEE:
    /// <see cref="Select"/> always returns exactly <c>min(shapeCount, gridWidth * gridHeight)</c>
    /// distinct, in-bounds cells. It honours <paramref name="minCellDistance"/> (Chebyshev) where the
    /// grid can fit that many spaced-out cells, then relaxes the distance step-by-step down to zero,
    /// and finally fills from any remaining cell. It never returns a short list and never loops
    /// forever - the previous inline implementation in <c>ShapeGenerator</c> could do both, which
    /// crashed <c>GenerateShapes</c> with an out-of-range access on tight level configs.
    /// </summary>
    public static class SeedCellSelector
    {
        /// <summary>
        /// Selects seed cells for a <paramref name="gridWidth"/> x <paramref name="gridHeight"/> grid.
        /// </summary>
        /// <param name="gridWidth">Grid width in cells. Non-positive returns an empty list.</param>
        /// <param name="gridHeight">Grid height in cells. Non-positive returns an empty list.</param>
        /// <param name="shapeCount">Desired seed count. Clamped to <c>[0, gridWidth * gridHeight]</c>.</param>
        /// <param name="minCellDistance">Preferred minimum Chebyshev distance between seeds.</param>
        /// <param name="preSeedCorners">When true, up to four corner cells are placed first.</param>
        /// <param name="rng">Randomness source; deterministic for a given seeded instance. May be null.</param>
        /// <returns>Exactly <c>min(shapeCount, gridWidth * gridHeight)</c> distinct in-bounds cells.</returns>
        public static List<Vector2Int> Select(
            int gridWidth,
            int gridHeight,
            int shapeCount,
            int minCellDistance,
            bool preSeedCorners,
            System.Random rng)
        {
            var chosen = new List<Vector2Int>();
            if (gridWidth <= 0 || gridHeight <= 0) return chosen;

            rng ??= new System.Random();

            int cellCount = gridWidth * gridHeight;
            int target = Math.Min(Math.Max(shapeCount, 0), cellCount);
            if (target == 0) return chosen;

            var chosenSet = new HashSet<long>();

            // Categorise every cell once: corners -> edges -> centers (matches the original intent).
            var corners = new List<Vector2Int>();
            var edges = new List<Vector2Int>();
            var centers = new List<Vector2Int>();

            for (int y = 0; y < gridHeight; y++)
            {
                for (int x = 0; x < gridWidth; x++)
                {
                    bool isCorner = (x == 0 || x == gridWidth - 1) && (y == 0 || y == gridHeight - 1);
                    bool isEdge = x == 0 || y == 0 || x == gridWidth - 1 || y == gridHeight - 1;

                    if (isCorner) corners.Add(new Vector2Int(x, y));
                    else if (isEdge) edges.Add(new Vector2Int(x, y));
                    else centers.Add(new Vector2Int(x, y));
                }
            }

            // Step 1: pre-seed corners (natural starting points).
            if (preSeedCorners)
            {
                Shuffle(corners, rng);
                foreach (var corner in corners)
                {
                    if (chosen.Count >= Math.Min(target, 4)) break;
                    if (chosenSet.Add(Key(corner))) chosen.Add(corner);
                }
            }

            Shuffle(edges, rng);
            Shuffle(centers, rng);

            int phase = rng.Next(0, 2);

            // Step 2: greedy far-first fill. Try the requested distance, then progressively relax it
            // so a full-size set is always produced even on grids that cannot fit `target` cells at
            // `minCellDistance`.
            for (int d = Math.Max(minCellDistance, 0); d >= 0 && chosen.Count < target; d--)
            {
                FillGreedy(chosen, chosenSet, edges, centers, phase, d, target);
            }

            // Step 3: last resort - append any remaining unused cell (covers corners when
            // preSeedCorners is false and edges + centers alone cannot reach `target`).
            if (chosen.Count < target)
            {
                var all = new List<Vector2Int>(cellCount);
                for (int y = 0; y < gridHeight; y++)
                    for (int x = 0; x < gridWidth; x++)
                        all.Add(new Vector2Int(x, y));

                Shuffle(all, rng);

                foreach (var cell in all)
                {
                    if (chosen.Count >= target) break;
                    if (chosenSet.Add(Key(cell))) chosen.Add(cell);
                }
            }

            return chosen;
        }

        /// <summary>
        /// Repeatedly claims the best remaining candidate (farthest from everything chosen so far)
        /// until <paramref name="target"/> is reached or no candidate satisfies <paramref name="minDist"/>.
        /// </summary>
        static void FillGreedy(
            List<Vector2Int> chosen,
            HashSet<long> chosenSet,
            List<Vector2Int> edges,
            List<Vector2Int> centers,
            int phase,
            int minDist,
            int target)
        {
            while (chosen.Count < target)
            {
                bool pickEdge = ((chosen.Count + phase) % 2) == 0;
                var primary = pickEdge ? edges : centers;
                var secondary = pickEdge ? centers : edges;

                var src = primary;
                int idx = PickBestIndex(primary, chosen, chosenSet, minDist);
                if (idx < 0)
                {
                    src = secondary;
                    idx = PickBestIndex(secondary, chosen, chosenSet, minDist);
                }

                if (idx < 0) return;

                var cell = src[idx];
                chosenSet.Add(Key(cell));
                chosen.Add(cell);
            }
        }

        /// <summary>
        /// Index of the unused candidate that is at least <paramref name="minDist"/> (Chebyshev) from
        /// every chosen cell and maximises the squared Euclidean distance to the nearest chosen cell.
        /// Returns -1 when nothing qualifies.
        /// </summary>
        static int PickBestIndex(
            List<Vector2Int> candidates,
            List<Vector2Int> chosen,
            HashSet<long> chosenSet,
            int minDist)
        {
            int best = -1;
            float bestScore = -1f;

            for (int i = 0; i < candidates.Count; i++)
            {
                var p = candidates[i];
                if (chosenSet.Contains(Key(p))) continue;
                if (!IsFarEnough(p, chosen, minDist)) continue;

                float score = MinDistanceSquared(p, chosen);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = i;
                }
            }

            return best;
        }

        static bool IsFarEnough(Vector2Int p, List<Vector2Int> chosen, int minDist)
        {
            if (minDist <= 0) return true;

            for (int i = 0; i < chosen.Count; i++)
            {
                var c = chosen[i];
                int d = Math.Max(Math.Abs(p.x - c.x), Math.Abs(p.y - c.y));
                if (d < minDist) return false;
            }

            return true;
        }

        static float MinDistanceSquared(Vector2Int p, List<Vector2Int> chosen)
        {
            if (chosen.Count == 0) return float.PositiveInfinity;

            float best = float.PositiveInfinity;
            for (int i = 0; i < chosen.Count; i++)
            {
                int dx = p.x - chosen[i].x;
                int dy = p.y - chosen[i].y;
                float d2 = dx * dx + dy * dy;
                if (d2 < best) best = d2;
            }

            return best;
        }

        static void Shuffle<T>(List<T> list, System.Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        static long Key(Vector2Int c) => ((long)c.x << 32) | (uint)c.y;
    }
}
