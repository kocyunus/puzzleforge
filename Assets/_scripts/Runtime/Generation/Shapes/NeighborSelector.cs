using System.Collections.Generic;
using UnityEngine;
using Yunus.Game.Gameplay;

namespace Yunus.Game.Generation
{
    /// <summary>
    /// Triangle growth neighbour selection (3-Priority System).
    ///
    /// Decides which triangle a shape claims next while expanding. Ownership is read straight from
    /// <see cref="Triangle.ownerShapeIndex"/> and cells are resolved through
    /// <see cref="GridBuilder.TryGetTriangle"/> (O(1) dictionary), so every query below is constant
    /// time - no per-move scans of an "unowned pool".
    ///
    /// PRIORITY 1 - SAME CELL (respects minTrianglesPerBox): fill the current cell up to the
    ///   minimum before spreading. Prefers positions adjacent to the current one; falls back to the
    ///   opposite position only when both perpendicular positions are already owned.
    /// PRIORITY 2 - ADJACENT CELL (entry/exit door logic): step into a neighbouring cell through a
    ///   matching door - the exit position in the current cell must be owned, the entry position in
    ///   the neighbour must be empty. Prefers completely empty neighbour cells.
    /// PRIORITY 3 - ANY OWNED CELL (rule ignored): fill any empty position in a cell the shape
    ///   already owns. Last resort before the caller's coverage sweep.
    /// </summary>
    public static class NeighborSelector
    {
        /// <summary>
        /// Tries to pick the next triangle for <paramref name="shape"/> to claim, expanding from
        /// <paramref name="current"/>. Returns false when none of the three priorities apply.
        /// </summary>
        public static bool TryPickNext(
            Triangle current,
            ShapeData shape,
            GridBuilder grid,
            int minTrianglesPerBox,
            System.Random rng,
            out Triangle picked)
        {
            picked = null;
            if (current == null || shape == null || grid == null) return false;

            int group = shape.ShapeIndex;

            if (TryPickFromSameCell(current, grid, group, minTrianglesPerBox, out picked)) return true;
            if (TryPickFromAdjacentCell(current, grid, group, rng, out picked)) return true;
            if (TryPickFromAnyOwnedCell(shape, grid, out picked)) return true;

            return false;
        }

        // ============ PRIORITY 1: SAME CELL ============

        static bool TryPickFromSameCell(
            Triangle current,
            GridBuilder grid,
            int group,
            int minTrianglesPerBox,
            out Triangle picked)
        {
            picked = null;

            int x = current.x, y = current.y;

            if (CountOwnedInCell(grid, x, y, group) >= minTrianglesPerBox)
                return false;

            // Prefer positions perpendicular to the current one (a smooth L/▔ fill).
            foreach (int pos in GetAdjacentPositions(current.posIndex))
            {
                var t = TriAt(grid, x, y, pos);
                if (t != null && t.ownerShapeIndex < 0)
                {
                    picked = t;
                    return true;
                }
            }

            // Opposite position: only take it if both perpendicular positions are already ours,
            // otherwise we would leave an awkward gap in the cell.
            int opposite = Triangle.OppositePosIndex(current.posIndex);
            var opp = TriAt(grid, x, y, opposite);
            if (opp != null && opp.ownerShapeIndex < 0 &&
                BothPerpendicularOwned(grid, x, y, current.posIndex, group))
            {
                picked = opp;
                return true;
            }

            return false;
        }

        // ============ PRIORITY 2: ADJACENT CELL ============

        static readonly (Vector2Int offset, int entryPos)[] Directions =
        {
            (Vector2Int.up,    4), // going UP    -> enter neighbour via DOWN
            (Vector2Int.right, 3), // going RIGHT -> enter neighbour via LEFT
            (Vector2Int.left,  2), // going LEFT  -> enter neighbour via RIGHT
            (Vector2Int.down,  1), // going DOWN  -> enter neighbour via UP
        };

        static bool TryPickFromAdjacentCell(
            Triangle current,
            GridBuilder grid,
            int group,
            System.Random rng,
            out Triangle picked)
        {
            picked = null;

            var valid = new List<(int nx, int ny, int entryPos, int emptyCount)>(4);

            foreach (var (offset, entryPos) in Directions)
            {
                int nx = current.x + offset.x;
                int ny = current.y + offset.y;
                if (!CellExists(grid, nx, ny)) continue;

                int emptyCount = CountUnownedInCell(grid, nx, ny);
                if (emptyCount <= 0) continue;

                // Entry door in the neighbour must be empty.
                var entry = TriAt(grid, nx, ny, entryPos);
                if (entry == null || entry.ownerShapeIndex >= 0) continue;

                // Exit door in the current cell must be owned by this shape.
                int exitPos = GetExitDoorPosition(current.x, current.y, nx, ny);
                if (!IsPosOwnedBy(grid, current.x, current.y, exitPos, group)) continue;

                valid.Add((nx, ny, entryPos, emptyCount));
            }

            if (valid.Count == 0) return false;

            // Prefer a completely empty neighbour cell (more room to grow).
            foreach (var v in valid)
            {
                if (v.emptyCount == 4)
                {
                    picked = TriAt(grid, v.nx, v.ny, v.entryPos);
                    return picked != null;
                }
            }

            var pick = valid[rng?.Next(valid.Count) ?? 0];
            picked = TriAt(grid, pick.nx, pick.ny, pick.entryPos);
            return picked != null;
        }

        // ============ PRIORITY 3: ANY OWNED CELL (rule ignored) ============

        static bool TryPickFromAnyOwnedCell(ShapeData shape, GridBuilder grid, out Triangle picked)
        {
            picked = null;

            var seenCells = new HashSet<int>();
            var owned = shape.OccupiedTriangles;

            for (int i = 0; i < owned.Count; i++)
            {
                var tri = owned[i];
                if (tri == null || !seenCells.Add(tri.boxIndex)) continue;

                for (int pos = 1; pos <= 4; pos++)
                {
                    var t = TriAt(grid, tri.x, tri.y, pos);
                    if (t != null && t.ownerShapeIndex < 0)
                    {
                        picked = t;
                        return true;
                    }
                }
            }

            return false;
        }

        // ============ HELPERS ============

        static Triangle TriAt(GridBuilder grid, int x, int y, int posIndex)
            => grid.TryGetTriangle(x, y, posIndex, out var t) ? t : null;

        static bool CellExists(GridBuilder grid, int x, int y)
            => x >= 0 && x < grid.GridWidth && y >= 0 && y < grid.GridHeight;

        static int CountOwnedInCell(GridBuilder grid, int x, int y, int group)
        {
            int count = 0;
            for (int pos = 1; pos <= 4; pos++)
            {
                var t = TriAt(grid, x, y, pos);
                if (t != null && t.ownerShapeIndex == group) count++;
            }
            return count;
        }

        static int CountUnownedInCell(GridBuilder grid, int x, int y)
        {
            int count = 0;
            for (int pos = 1; pos <= 4; pos++)
            {
                var t = TriAt(grid, x, y, pos);
                if (t != null && t.ownerShapeIndex < 0) count++;
            }
            return count;
        }

        static bool IsPosOwnedBy(GridBuilder grid, int x, int y, int posIndex, int group)
        {
            var t = TriAt(grid, x, y, posIndex);
            return t != null && t.ownerShapeIndex == group;
        }

        static bool BothPerpendicularOwned(GridBuilder grid, int x, int y, int currentPos, int group)
        {
            int opposite = Triangle.OppositePosIndex(currentPos);

            int posA = -1, posB = -1;
            for (int p = 1; p <= 4; p++)
            {
                if (p == currentPos || p == opposite) continue;
                if (posA < 0) posA = p;
                else posB = p;
            }

            return IsPosOwnedBy(grid, x, y, posA, group) &&
                   IsPosOwnedBy(grid, x, y, posB, group);
        }

        /// <summary>Positions you can flow to from <paramref name="currentPos"/> within a cell.</summary>
        static int[] GetAdjacentPositions(int currentPos) => currentPos switch
        {
            1 => new[] { 2, 3 },
            2 => new[] { 1, 4 },
            3 => new[] { 1, 4 },
            4 => new[] { 2, 3 },
            _ => System.Array.Empty<int>()
        };

        /// <summary>
        /// Position in the source cell through which we exit when moving to (<paramref name="toX"/>,
        /// <paramref name="toY"/>).
        /// </summary>
        static int GetExitDoorPosition(int fromX, int fromY, int toX, int toY)
        {
            int dx = toX - fromX;
            int dy = toY - fromY;

            if (dx == 1) return 2;   // moving RIGHT -> exit via RIGHT
            if (dx == -1) return 3;  // moving LEFT  -> exit via LEFT
            if (dy == 1) return 1;   // moving UP    -> exit via UP
            if (dy == -1) return 4;  // moving DOWN  -> exit via DOWN

            return -1;
        }
    }
}
