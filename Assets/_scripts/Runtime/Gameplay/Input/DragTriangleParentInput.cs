using System.Collections.Generic;
using UnityEngine;
using Yunus.Game.Board;

namespace Yunus.Game.Gameplay
{
    /// <summary>
    /// Handles drag and drop input for shape pieces: on mouse-down it grabs the
    /// <see cref="ShapeData"/> root of whatever triangle was hit, drags it on a plane facing the
    /// camera, and on mouse-up asks <see cref="SnapUtil"/> to snap it onto the puzzle board.
    /// </summary>
    [DisallowMultipleComponent]
    public class DragTriangleParentInput : MonoBehaviour
{
    [Header("Camera")]
    [SerializeField] private Camera cam;

    [Header("References")]
    [SerializeField] private PuzzleBoard puzzleBoard;

    [Header("Snap Settings")]
    [SerializeField] private bool snapToBoard = true;
    [SerializeField] private bool revertIfNoSnap = false;

    [Header("Debug")]
    [SerializeField] private bool log = false;

    // Drag state
    private Transform draggingRoot;
    private Vector3 dragOffset;
    private Vector3 dragStartPos;
    private int dragFrames;
    private ShapeData currentShapeData;

    // Raycast buffers
    private static readonly RaycastHit2D[] hits2D = new RaycastHit2D[32];
    private static readonly RaycastHit[] hits3D = new RaycastHit[32];

    // Public API
    public bool IsDragging => draggingRoot != null;
    public ShapeData DraggingShape => currentShapeData;

    void Awake()
    {
        if (!cam) cam = Camera.main;
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0)) TryBeginDrag();
        if (Input.GetMouseButton(0) && draggingRoot) ContinueDrag();
        if (Input.GetMouseButtonUp(0)) EndDrag();
    }

    void TryBeginDrag()
    {
        Transform hitTransform = GetHitTransform();
        if (!hitTransform) return;

        var root = hitTransform.parent ? hitTransform.parent : hitTransform;

        currentShapeData = root.GetComponent<ShapeData>();
        if (currentShapeData == null)
        {
            if (log) Debug.LogWarning($"[DragInput] ShapeData not found on {root.name}");
            return;
        }

        draggingRoot = root;
        dragStartPos = root.position;

        if (currentShapeData.BoardSnappedTriangles.Count > 0)
        {
            puzzleBoard.OnShapeRemoved();
            currentShapeData.ResetAllSnaps();
        }

        var mouseWorld = MouseOnDragPlane(draggingRoot);
        dragOffset = mouseWorld - draggingRoot.position;
        dragFrames = 0;

        if (log) Debug.Log($"[DragInput] Begin drag: {draggingRoot.name}");
    }

    void ContinueDrag()
    {
        var mouseWorld = MouseOnDragPlane(draggingRoot);
        // Z'yi -10'da tut
        draggingRoot.position = new Vector3(
            mouseWorld.x - dragOffset.x,
            mouseWorld.y - dragOffset.y,
            -8f
        );
        dragFrames++;
    }
    void EndDrag()
    {
        if (!draggingRoot || currentShapeData == null)
        {
            if (log) Debug.Log("[DragInput] EndDrag - no active drag");
            return;
        }

        if (log) Debug.Log($"[DragInput] EndDrag - attempting placement");

        bool placementSuccessful = snapToBoard && SnapUtil.TrySnapToBoard(
            draggingRoot,
            currentShapeData.OccupiedTriangles,
            puzzleBoard.transform,
            puzzleBoard.Triangles,
            draggingRoot
        );

        if (placementSuccessful)
        {
            puzzleBoard.OnShapePlaced();
            if (log) Debug.Log($"[DragInput] ✅ Placement successful!");
        }
        else
        {
            if (log) Debug.Log("[DragInput] ❌ Placement failed");

            if (revertIfNoSnap)
            {
                draggingRoot.position = dragStartPos;
            }
        }

        draggingRoot = null;
        currentShapeData = null;
        dragFrames = 0;
    }

    Transform GetHitTransform()
    {
        var ray = cam.ScreenPointToRay(Input.mousePosition);
        Transform hitTransform = null;

        int count2D = Physics2D.GetRayIntersectionNonAlloc(ray, hits2D, Mathf.Infinity);
        if (count2D > 0)
        {
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < count2D; i++)
            {
                var hit = hits2D[i];
                if (!hit.collider) continue;
                if (hit.distance < bestDistance)
                {
                    bestDistance = hit.distance;
                    hitTransform = hit.collider.transform;
                }
            }
        }
        else
        {
            int count3D = Physics.RaycastNonAlloc(ray, hits3D, Mathf.Infinity);
            if (count3D > 0)
            {
                float bestDistance = float.PositiveInfinity;
                for (int i = 0; i < count3D; i++)
                {
                    var hit = hits3D[i];
                    if (!hit.collider) continue;
                    if (hit.distance < bestDistance)
                    {
                        bestDistance = hit.distance;
                        hitTransform = hit.collider.transform;
                    }
                }
            }
        }

        return hitTransform;
    }

    Vector3 MouseOnDragPlane(Transform root)
    {
        var ray = cam.ScreenPointToRay(Input.mousePosition);
        var plane = new Plane(-cam.transform.forward, root.position);

        if (plane.Raycast(ray, out float enter))
        {
            var point = ray.GetPoint(enter);
            point.z = root.position.z;
            return point;
        }

        float distance = Mathf.Abs(root.position.z - cam.transform.position.z);
        var worldPoint = cam.ScreenToWorldPoint(new Vector3(
            Input.mousePosition.x,
            Input.mousePosition.y,
            distance
        ));
        worldPoint.z = root.position.z;
        return worldPoint;
    }
    }
}