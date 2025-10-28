using System.Collections.Generic;
using UnityEngine;
using Yunus.Game.Gameplay;
namespace Yunus.Game.Generation
{
    /// <summary>
    /// Triangle growth neighbor selection algorithm (3-Priority System).
    /// 
    /// ROLE:
    /// Determines which triangle to claim next during shape expansion.
    /// Ensures balanced, varied shapes that fill 100% of grid.
    /// 
    /// 3-PRIORITY ALGORITHM:
    /// 
    /// PRIORITY 1: SAME-BOX NEIGHBORS (With minTrianglesPerBox Rule)
    /// Purpose: Fill up to minTrianglesPerBox triangles in current box
    /// Logic:
    ///   - Count owned triangles in current box
    ///   - If less than minTrianglesPerBox: expand to same box
    ///   - Find adjacent triangles in same box (4-connected)
    ///   - Pick nearest unowned neighbor
    /// Example: Box has 2 triangles, minTrianglesPerBox=4 → expand within box
    /// 
    /// PRIORITY 2: ADJACENT-BOX NEIGHBORS (Entry/Exit Door Logic)
    /// Purpose: Expand to neighboring boxes via entry/exit doors
    /// Logic:
    ///   - Find adjacent grid cells (up, right, down, left)
    ///   - Check if entry door exists (empty position in neighbor)
    ///   - Check if exit door exists (owned position in current box)
    ///   - Pick cell with most empty triangles
    /// Entry/Exit Door Mapping:
    ///   - Up (1) ↔ Down (4)      (crosses vertical boundary)
    ///   - Right (2) ↔ Left (3)   (crosses horizontal boundary)
    /// Example: Current box has 4 triangles, try expanding into adjacent box
    /// 
    /// PRIORITY 3: FINAL ATTEMPT (Ignore minTrianglesPerBox, Complete Any Box)
    /// Purpose: Guarantee 100% grid coverage
    /// Logic:
    ///   - Find ALL owned boxes for this shape
    ///   - For each owned box: find any empty triangle
    ///   - Pick first empty triangle found
    ///   - Ignores minTrianglesPerBox rule (fallback)
    /// Example: Box A maxed out, other boxes have empty slots → fill those
    /// 
    /// GUARANTEES:
    /// - 100% grid coverage (no unused triangles)
    /// - Balanced shape sizes (Priority 1 prevents hogging)
    /// - Varied results (random entry/exit door selection)
    /// - Smooth borders (entry/exit door logic creates natural boundaries)
    /// </summary>
    public static class NeighborSelector
    {
        /// <summary>
        /// Main entry point: tries to pick next triangle using 3-priority system.
        /// Returns true if neighbor found, false if shape cannot expand further.
        /// </summary>
        public static bool TryPickNext(
            Triangle current,
            List<Triangle> pool,
            List<ShapeData> shapes,
            List<Triangle> allTriangles,
            int currentGroupId,
            int minTrianglesPerBox,
            out int pickedIdx,
            out bool canContinue)
        {
            pickedIdx = -1;
            canContinue = true;

            // Priority 1: Same cell neighbors (with minimum rule)
            if (TryPickFromSameCell(current, pool, shapes, currentGroupId, minTrianglesPerBox, out pickedIdx))
                return true;

            // Priority 2: Adjacent cell
            if (TryPickFromAdjacentCell(current, pool, shapes, allTriangles, currentGroupId, out pickedIdx))
                return true;

            // Priority 3: All owned boxes FINAL ATTEMPT (ignore minTriangles rule, complete any box)
            if (TryPickFromAnyOwnedBoxFinalAttempt(shapes, currentGroupId, pool, out pickedIdx))
                return true;

            // No valid neighbors found
            canContinue = false;
            return false;
        }

        // ============ SAME CELL SELECTION (WITH RULE) ============

        /// <summary>
        /// PRIORITY 1: Tries to pick a triangle from the same grid cell.
        /// Only expands if owned count is less than minTrianglesPerBox.
        /// 
        /// WHY THIS RULE?
        /// - Prevents shape from abandoning a cell too early
        /// - Ensures each cell is "filled" before expanding elsewhere
        /// - Creates more compact, organized shapes
        /// </summary>
        static bool TryPickFromSameCell(
            Triangle current,
            List<Triangle> pool,
            List<ShapeData> shapes,
            int groupId,
            int minTrianglesPerBox,
            out int pickedIdx)
        {
            pickedIdx = -1;

            // Count how many triangles this shape owns in current box
            int ownedInBox = CountOwnedInBox(current.boxIndex, groupId, shapes);

            // If already filled to minimum, skip this priority
            if (ownedInBox >= minTrianglesPerBox)
            {
                return false;
            }

            // Find empty neighbors in same cell
            var inCell = FindSameCellCandidates(current, pool);
            if (inCell.Count == 0) return false;

            foreach (int pos in GetAdjacentPositions(current.posIndex))
            {
                int idx = FindPositionInList(inCell, pos, pool);
                if (idx >= 0)
                {
                    pickedIdx = idx;
                    return true;
                }
            }

            int opposite = Triangle.OppositePosIndex(current.posIndex);
            int oppIdx = FindPositionInList(inCell, opposite, pool);
            if (oppIdx >= 0 && BothAdjacentOwned(current.gridPos, current.posIndex, groupId, shapes))
            {
                pickedIdx = oppIdx;
                return true;
            }

            return false;
        }

        /// <summary>
        /// PRIORITY 3: Final attempt to expand from ANY owned box.
        /// Ignores minTrianglesPerBox rule to guarantee grid coverage.
        /// Used when Priorities 1 and 2 fail.
        /// </summary>
        static bool TryPickFromAnyOwnedBoxFinalAttempt(List<ShapeData> shapes, int groupId, List<Triangle> pool, out int pickedIdx)
        {
            pickedIdx = -1;

            // Find all boxes owned by this shape
            var ownedBoxes = new List<int>();
            foreach (var shape in shapes)
            {
                if (shape == null || shape.ShapeIndex != groupId) continue;
                foreach (var tri in shape.OccupiedTriangles)
                {
                    if (tri != null && !ownedBoxes.Contains(tri.boxIndex))
                        ownedBoxes.Add(tri.boxIndex);
                }
            }

            // Try to fill any owned box that still has empty triangles
            foreach (int boxIndex in ownedBoxes)
            {
                var emptyInBox = FindEmptyTrianglesInBox(boxIndex, pool);
                if (emptyInBox.Count == 0) continue;

                // Take first empty triangle (any within the box)
                pickedIdx = emptyInBox[0];
                Debug.Log($"[NeighborSelector] Final attempt: Taking empty triangle in owned box {boxIndex} (rule ignored)");
                return true;
            }

            return false;
        }

        /// <summary>
        /// Helper: Finds all empty triangles in a specific grid box.
        /// </summary>
        static List<int> FindEmptyTrianglesInBox(int boxIndex, List<Triangle> pool)
        {
            var triangles = new List<int>();
            for (int i = 0; i < pool.Count; i++)
            {
                var tri = pool[i];
                if (tri != null && tri.boxIndex == boxIndex)
                    triangles.Add(i);
            }
            return triangles;
        }

        /// <summary>
        /// Helper: Counts how many triangles a shape owns in a specific box.
        /// Used for minTrianglesPerBox rule checking.
        /// </summary>
        static int CountOwnedInBox(int boxIndex, int groupId, List<ShapeData> shapes)
        {
            int count = 0;

            foreach (var shape in shapes)
            {
                if (shape == null || shape.ShapeIndex != groupId) continue;

                foreach (var tri in shape.OccupiedTriangles)
                {
                    if (tri == null) continue;
                    if (tri.boxIndex == boxIndex)
                        count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Helper: Finds all empty triangles in current box.
        /// </summary>
        static List<int> FindSameCellCandidates(Triangle current, List<Triangle> pool)
        {
            var candidates = new List<int>();
            for (int i = 0; i < pool.Count; i++)
            {
                var tri = pool[i];
                if (tri != null && tri.boxIndex == current.boxIndex)
                    candidates.Add(i);
            }
            return candidates;
        }

        /// <summary>
        /// Helper: Maps adjacent positions for expanding within a cell.
        /// Returns positions you can move to from current position.
        /// </summary>
        static int[] GetAdjacentPositions(int currentPos) => currentPos switch
        {
            1 => new[] { 2, 3 },
            2 => new[] { 1, 4 },
            3 => new[] { 1, 4 },
            4 => new[] { 2, 3 },
            _ => System.Array.Empty<int>()
        };

        /// <summary>
        /// Helper: Checks if both adjacent positions (perpendicular to current)
        /// are already owned in the same cell.
        /// Used to prevent incomplete fills.
        /// </summary>
        static bool BothAdjacentOwned(Vector2Int cell, int currentPos, int groupId, List<ShapeData> shapes)
        {
            int opposite = Triangle.OppositePosIndex(currentPos);

            int posA = -1, posB = -1;
            for (int p = 1; p <= 4; p++)
            {
                if (p == currentPos || p == opposite) continue;
                if (posA < 0) posA = p;
                else posB = p;
            }

            return IsPositionOwned(cell, posA, groupId, shapes) &&
                   IsPositionOwned(cell, posB, groupId, shapes);
        }

        /// <summary>
        /// Helper: Finds a triangle with specific position in list.
        /// </summary>
        static int FindPositionInList(List<int> indices, int wantedPos, List<Triangle> pool)
        {
            foreach (int i in indices)
            {
                var tri = pool[i];
                if (tri != null && tri.posIndex == wantedPos)
                    return i;
            }
            return -1;
        }

        // ============ ADJACENT CELL SELECTION ============

        /// <summary>
        /// PRIORITY 2: Tries to pick a triangle from an ADJACENT grid cell.
        /// Uses entry/exit door logic to ensure smooth borders.
        /// 
        /// ENTRY/EXIT DOOR LOGIC:
        /// Entry = position in neighbor cell where we enter
        /// Exit = position in current cell where we exit
        /// 
        /// Example: Expanding UP
        ///   - Current cell box: (0,0)
        ///   - Neighbor cell: (0,1) [above]
        ///   - Exit door: position 1 (UP) in current box
        ///   - Entry door: position 4 (DOWN) in neighbor box
        ///   - Only expand if: exit owned AND entry empty
        /// </summary>
        static bool TryPickFromAdjacentCell(
            Triangle current,
            List<Triangle> pool,
            List<ShapeData> shapes,
            List<Triangle> allTriangles,
            int groupId,
            out int pickedIdx)
        {
            pickedIdx = -1;

            // Find all adjacent cells with empty triangles
            var neighborCells = FindNeighborCells(current, pool, allTriangles);
            if (neighborCells.Count == 0) return false;

            // Pick best neighbor (most empty triangles, valid doors)
            var bestCell = SelectBestNeighborCell(current, neighborCells, pool, groupId, shapes);
            if (bestCell == null) return false;

            // Expand into best neighbor's entry position
            int entryPos = bestCell.entryPosition;
            pickedIdx = FindTriangleInCell(bestCell.position, entryPos, pool);

            return pickedIdx >= 0;
        }

        /// <summary>
        /// Helper: Information about a neighboring cell (for decision making).
        /// </summary>
        class NeighborCellInfo
        {
            public Vector2Int position;
            public int emptyCount;
            public int totalCount;
            public int entryPosition;
        }

        /// <summary>
        /// Helper: Finds all neighboring cells with empty triangles.
        /// Checks up/right/down/left directions.
        /// </summary>
        static List<NeighborCellInfo> FindNeighborCells(Triangle current, List<Triangle> pool, List<Triangle> allTriangles)
        {
            var cells = new List<NeighborCellInfo>();

            // Direction mapping: offset and entry position (entry is opposite of exit)
            var directions = new (Vector2Int offset, int entryPos)[]
            {
                (Vector2Int.up,    4),     // Going UP: enter via DOWN (pos 4)
                (Vector2Int.right, 3),     // Going RIGHT: enter via LEFT (pos 3)
                (Vector2Int.left,  2),     // Going LEFT: enter via RIGHT (pos 2)
                (Vector2Int.down,  1)      // Going DOWN: enter via UP (pos 1)
            };

            foreach (var (offset, entryPos) in directions)
            {
                var neighborPos = current.gridPos + offset;
                var cellInfo = AnalyzeCell(neighborPos, entryPos, pool, allTriangles);
                if (cellInfo != null)
                    cells.Add(cellInfo);
            }

            return cells;
        }

        /// <summary>
        /// Helper: Analyzes a single cell to determine if it's a valid neighbor.
        /// Counts empty triangles and checks if cell exists.
        /// </summary>
        static NeighborCellInfo AnalyzeCell(Vector2Int pos, int entryPos, List<Triangle> pool, List<Triangle> allTriangles)
        {
            int emptyCount = 0;
            foreach (var tri in pool)
            {
                if (tri != null && tri.gridPos == pos)
                    emptyCount++;
            }

            bool cellExists = false;
            foreach (var tri in allTriangles)
            {
                if (tri != null && tri.gridPos == pos)
                {
                    cellExists = true;
                    break;
                }
            }

            if (!cellExists) return null;

            return new NeighborCellInfo
            {
                position = pos,
                emptyCount = emptyCount,
                totalCount = 4,
                entryPosition = entryPos
            };
        }

        /// <summary>
        /// Helper: Selects the best neighbor cell to expand into.
        /// Validates entry/exit doors and picks cell with most empty triangles.
        /// </summary>
        static NeighborCellInfo SelectBestNeighborCell(
            Triangle current,
            List<NeighborCellInfo> cells,
            List<Triangle> pool,
            int groupId,
            List<ShapeData> shapes)
        {
            var validCells = new List<NeighborCellInfo>();

            foreach (var cell in cells)
            {
                if (cell.emptyCount <= 0) continue;

                // Check if entry door exists (empty)
                bool entryEmpty = IsPositionEmpty(cell.position, cell.entryPosition, pool);
                if (!entryEmpty) continue;

                // Check if exit door exists (owned in current box)
                int exitDoorPos = GetExitDoorPosition(current.gridPos, cell.position);
                bool hasExitDoor = IsPositionOwned(current.gridPos, exitDoorPos, groupId, shapes);
                if (!hasExitDoor) continue;

                validCells.Add(cell);
            }

            if (validCells.Count == 0) return null;

            // Prefer completely empty cells (more space to grow)
            foreach (var cell in validCells)
            {
                if (cell.emptyCount == cell.totalCount)
                    return cell;
            }

            // Otherwise pick random valid cell
            return validCells[Random.Range(0, validCells.Count)];
        }

        /// <summary>
        /// Helper: Determines the exit door position when moving from one cell to another.
        /// Exit door = position in source cell where we exit.
        /// </summary>
        static int GetExitDoorPosition(Vector2Int from, Vector2Int to)
        {
            int dx = to.x - from.x;
            int dy = to.y - from.y;

            if (dx == 1) return 2;     // Moving RIGHT: exit via RIGHT (pos 2)
            if (dx == -1) return 3;    // Moving LEFT: exit via LEFT (pos 3)
            if (dy == 1) return 1;     // Moving UP: exit via UP (pos 1)
            if (dy == -1) return 4;    // Moving DOWN: exit via DOWN (pos 4)

            return -1;
        }

        /// <summary>
        /// Helper: Checks if a specific position is empty (available) in a cell.
        /// </summary>
        static bool IsPositionEmpty(Vector2Int cellPos, int posIndex, List<Triangle> pool)
        {
            foreach (var tri in pool)
            {
                if (tri == null) continue;
                if (tri.gridPos == cellPos && tri.posIndex == posIndex)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Helper: Finds a triangle in pool at specific cell position.
        /// </summary>
        static int FindTriangleInCell(Vector2Int cellPos, int posIndex, List<Triangle> pool)
        {
            for (int i = 0; i < pool.Count; i++)
            {
                var tri = pool[i];
                if (tri != null && tri.gridPos == cellPos && tri.posIndex == posIndex)
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// Helper: Checks if a shape owns a specific position in a cell.
        /// </summary>
        static bool IsPositionOwned(Vector2Int cellPos, int posIndex, int groupId, List<ShapeData> shapes)
        {
            foreach (var shape in shapes)
            {
                if (shape == null || shape.ShapeIndex != groupId) continue;

                foreach (var tri in shape.OccupiedTriangles)
                {
                    if (tri == null) continue;
                    if (tri.gridPos == cellPos && tri.posIndex == posIndex)
                        return true;
                }
            }

            return false;
        }
    }
}