using System.Collections.Generic;
using UnityEngine;

namespace Yunus.Game.Gameplay
{
    public class ShapeData : MonoBehaviour
{
    [Header("Identity")]
    public int ShapeIndex;
    public Color ShapeColor;

    [Header("Growth Settings")]
    public int MovesPerTurn;

    [Header("Occupied Triangles")]
    public List<Triangle> OccupiedTriangles = new List<Triangle>();

    [Header("Board Snapped Triangles")]
    public List<Triangle> BoardSnappedTriangles = new List<Triangle>();

    [System.NonSerialized]
    public Queue<Triangle> GrowthQueue;

    private void Awake()
    {
        GrowthQueue = new Queue<Triangle>();
        OccupiedTriangles = new List<Triangle>();
        BoardSnappedTriangles = new List<Triangle>();
    }

    // ===== OccupiedTriangles (Shape's own triangles) =====
    public void RegisterTriangle(Triangle tri)
    {
        if (tri == null) return;
        if (!OccupiedTriangles.Contains(tri))
            OccupiedTriangles.Add(tri);
    }

    public void UnregisterTriangle(Triangle tri)
    {
        if (tri == null) return;
        OccupiedTriangles.Remove(tri);
    }

    public void ClearTriangles()
    {
        OccupiedTriangles.Clear();
    }

    // ===== BoardSnappedTriangles (Triangles snapped to board) =====

    /// <summary>
    /// Registers a board triangle (when snapped).
    /// </summary>
    public void RegisterBoardTriangle(Triangle boardTri)
    {
        if (boardTri == null) return;
        if (!BoardSnappedTriangles.Contains(boardTri))
            BoardSnappedTriangles.Add(boardTri);
    }

    /// <summary>
    /// Unregisters a board triangle.
    /// </summary>
    public void UnregisterBoardTriangle(Triangle boardTri)
    {
        if (boardTri == null) return;
        BoardSnappedTriangles.Remove(boardTri);
    }

    /// <summary>
    /// Clears all board snaps and resets triangle flags.
    /// </summary>
    public void ClearBoardSnaps(bool setIsSnappedFalse = true)
    {
        if (setIsSnappedFalse)
        {
            // Reset board triangles to false
            foreach (var tri in BoardSnappedTriangles)
            {
                if (tri != null)
                    tri.SnapState(false);
            }
        }
        BoardSnappedTriangles.Clear();
    }

    /// <summary>
    /// Resets both shape and board triangle snap states.
    /// </summary>
    public void ResetAllSnaps()
    {
        // Reset shape triangles to false
        ResetOccupiedSnaps();

        // Reset board triangles to false and clear list
        ClearBoardSnaps(setIsSnappedFalse: true);
    }
    public void ResetOccupiedSnaps() {
        foreach (var tri in OccupiedTriangles)
        {
            if (tri != null)
                tri.SnapState(false);
        }
    }
    
    // Debug helpers
    public int TriangleCount => OccupiedTriangles.Count;
    public int BoardSnapCount => BoardSnappedTriangles.Count;

    /// <summary>
    /// Resets all runtime state for pool return (despawn).
    /// Detaches triangles from shape, clears snap flags, and empties queue/lists.
    /// </summary>
    public void ResetForPool(Transform fallbackParent = null)
    {
        // 1) Disable snap flag and clear generation ownership for all triangles
        foreach (var tri in OccupiedTriangles)
        {
            if (!tri) continue;
            tri.SnapState(false);
            tri.ownerShapeIndex = -1;
            // Don't reparent during OnDisable() - causes Unity errors
            // Triangles will be managed by pool
        }
        OccupiedTriangles.Clear();

        // 2) Clear board-side records and flags
        ClearBoardSnaps(setIsSnappedFalse: true);

        // 3) Reset growth queue (other fields untouched)
        GrowthQueue?.Clear();
    }

    /// <summary>
    /// Auto-reset when despawned via SetActive(false).
    /// </summary>
    private void OnDisable()
    {
        ResetForPool();
    }
    }
}