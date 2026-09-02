using UnityEngine;
using Yunus.Game.Board;
using Yunus.Game.Core;

namespace Yunus.Game.Gameplay
{
    /// <summary>
    /// Mouse drag-and-drop for shape pieces. On mouse-down it grabs the <see cref="ShapeData"/>
    /// root of whatever triangle was hit (2D physics), drags it on a plane facing the camera, and
    /// on mouse-up asks <see cref="SnapUtil"/> to snap it onto the puzzle board.
    /// </summary>
    [DisallowMultipleComponent]
    public class DragTriangleParentInput : MonoBehaviour
    {
        const float DragZ = -8f; // keep the dragged shape in front of the board while moving

        [Header("Camera")]
        [SerializeField] private Camera cam;

        [Header("References")]
        [SerializeField] private PuzzleBoard puzzleBoard;

        [Header("Snap Settings")]
        [SerializeField] private bool snapToBoard = true;
        [SerializeField] private bool revertIfNoSnap = false;

        private Transform draggingRoot;
        private Vector3 dragOffset;
        private Vector3 dragStartPos;
        private ShapeData currentShapeData;

        private static readonly RaycastHit2D[] hitBuffer = new RaycastHit2D[32];

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
            var hit = PickTriangleUnderMouse();
            if (!hit) return;

            var root = hit.parent ? hit.parent : hit;
            currentShapeData = root.GetComponent<ShapeData>();
            if (currentShapeData == null)
            {
                GameLog.Info($"[DragInput] no ShapeData on {root.name}");
                return;
            }

            draggingRoot = root;
            dragStartPos = root.position;

            // Picking up an already-placed shape frees its board slots.
            if (currentShapeData.BoardSnappedTriangles.Count > 0)
            {
                puzzleBoard.OnShapeRemoved();
                currentShapeData.ResetAllSnaps();
            }

            dragOffset = MouseOnDragPlane(draggingRoot) - draggingRoot.position;
            GameLog.Info($"[DragInput] begin drag: {draggingRoot.name}");
        }

        void ContinueDrag()
        {
            var mouseWorld = MouseOnDragPlane(draggingRoot);
            draggingRoot.position = new Vector3(mouseWorld.x - dragOffset.x, mouseWorld.y - dragOffset.y, DragZ);
        }

        void EndDrag()
        {
            if (!draggingRoot || currentShapeData == null) return;

            bool placed = snapToBoard && SnapUtil.TrySnapToBoard(
                draggingRoot,
                currentShapeData.OccupiedTriangles,
                puzzleBoard.transform,
                puzzleBoard.Triangles,
                draggingRoot);

            if (placed)
            {
                puzzleBoard.OnShapePlaced();
                GameLog.Info("[DragInput] placement ok");
            }
            else
            {
                GameLog.Info("[DragInput] placement failed");
                if (revertIfNoSnap) draggingRoot.position = dragStartPos;
            }

            draggingRoot = null;
            currentShapeData = null;
        }

        /// <summary>Nearest 2D collider under the cursor, or null.</summary>
        Transform PickTriangleUnderMouse()
        {
            var ray = cam.ScreenPointToRay(Input.mousePosition);
            int count = Physics2D.GetRayIntersectionNonAlloc(ray, hitBuffer, Mathf.Infinity);

            Transform best = null;
            float bestDist = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var h = hitBuffer[i];
                if (h.collider && h.distance < bestDist)
                {
                    bestDist = h.distance;
                    best = h.collider.transform;
                }
            }
            return best;
        }

        /// <summary>Cursor position projected onto a camera-facing plane through <paramref name="root"/>.</summary>
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
            var world = cam.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, distance));
            world.z = root.position.z;
            return world;
        }
    }
}
