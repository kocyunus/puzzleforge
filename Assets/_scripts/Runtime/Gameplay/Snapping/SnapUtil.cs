using System.Collections.Generic;
using UnityEngine;

namespace Yunus.Game.Gameplay
{
    public static class SnapUtil
{
    // === Ayarlar ===
    static float SNAP_DIST_WORLD_XY = 5f;
    const float BOARD_LOCAL_Z_ON_SNAP = -2f;
    const bool ENFORCE_NAME_MATCH = false;
    const bool REQUIRE_POSINDEX_MATCH = true;

    static string KeyOf(GameObject go) => (go?.name ?? "").Replace("(Clone)", "").Trim().ToLowerInvariant();

    // -------------------------------------------------------------------------
    // Basit snap (cache/flag yok)
    public static void SnapOnEndDrag(Transform endDragRoot, List<Triangle> endDragList,
                                     Transform boardRoot, List<Triangle> boardList)
    {
        if (!ValidateInputs(endDragRoot, endDragList, boardRoot, boardList)) return;
        if (!GateAllWithinThreshold(endDragList, boardList)) return;

        var pairs = CollectPairsWithinThreshold(endDragList, boardList);
        var assigned = AssignGreedy(pairs);
        if (assigned.Count == 0) return;
        if (!AllAssignedSlotsFree(assigned)) return;

        ApplyWorldSnap(boardRoot, assigned);
    }

    // -------------------------------------------------------------------------
    // Snap + ShapeData + Flag
    public static bool TrySnapToBoard(Transform endDragRoot, List<Triangle> endDragList,
                                      Transform boardRoot, List<Triangle> boardList,
                                      Transform shapeParent,
                                      bool setFlags = true, bool setSlotFlags = true)
    {
        Debug.Log($"[SnapUtil] TrySnapToBoard STARTED - endDragList:{endDragList?.Count}, boardList:{boardList?.Count}");

        if (!ValidateInputs(endDragRoot, endDragList, boardRoot, boardList))
        {
            Debug.Log("[SnapUtil] ❌ ValidateInputs FAILED");
            return false;
        }

        if (!GateAllWithinThreshold(endDragList, boardList))
        {
            Debug.Log("[SnapUtil] ❌ GateAllWithinThreshold FAILED");
            return false;
        }

        var pairs = CollectPairsWithinThreshold(endDragList, boardList);
        Debug.Log($"[SnapUtil] Pairs collected: {pairs.Count}");

        var assigned = AssignGreedy(pairs);
        Debug.Log($"[SnapUtil] Assigned count: {assigned.Count}");

        if (assigned.Count == 0)
        {
            Debug.Log("[SnapUtil] ❌ No assignments");
            return false;
        }

        if (!AllAssignedSlotsFree(assigned))
        {
            Debug.Log("[SnapUtil] ❌ AllAssignedSlotsFree FAILED - slots already occupied");
            return false;
        }

        ApplyWorldSnap(boardRoot, assigned);
        Debug.Log($"[SnapUtil] ✅ WorldSnap applied");

        if (shapeParent != null)
        {
            var shapeData = shapeParent.GetComponent<ShapeData>();
            if (shapeData == null)
            {
                Debug.LogWarning($"[SnapUtil] ❌ ShapeData component not found on {shapeParent.name}");
                return false;
            }

            HashSet<Triangle> itemsToFlag = setFlags ? new HashSet<Triangle>() : null;
            HashSet<Triangle> slotsToFlag = setSlotFlags ? new HashSet<Triangle>() : null;

            foreach (var kv in assigned)
            {
                var itemTri = kv.Key;
                var slotTri = kv.Value;

                if (setFlags) itemsToFlag?.Add(itemTri);
                if (setSlotFlags) slotsToFlag?.Add(slotTri);

                shapeData.RegisterBoardTriangle(slotTri);
            }

            Debug.Log($"[SnapUtil] ✅ Registered {assigned.Count} board triangles to ShapeData");

            // Call SnapState directly
            if (setFlags && itemsToFlag != null && itemsToFlag.Count > 0)
            {
                foreach (var tri in itemsToFlag)
                {
                    if (tri != null)
                        tri.SnapState(true);
                }
            }

            if (setSlotFlags && slotsToFlag != null && slotsToFlag.Count > 0)
            {
                foreach (var tri in slotsToFlag)
                {
                    if (tri != null)
                        tri.SnapState(true);
                }
            }
        }

        Debug.Log("[SnapUtil] ✅✅✅ TrySnapToBoard SUCCESS - returning TRUE");
        return true;
    }

    // -------------------------------------------------------------------------
    // Internal flow helpers
    static bool ValidateInputs(Transform endDragRoot, List<Triangle> endDragList,
                               Transform boardRoot, List<Triangle> boardList)
        => boardRoot && endDragList != null && boardList != null;

    static bool GateAllWithinThreshold(List<Triangle> items, List<Triangle> slots)
    {
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (!item) return false;

            Vector2 itemW = new Vector2(item.transform.position.x, item.transform.position.y);
            float nearest = float.MaxValue;
            Triangle nearestSlot = null;

            foreach (var slot in slots)
            {
                if (!slot) continue;
                if (ReferenceEquals(item, slot)) continue;
                if (ENFORCE_NAME_MATCH && KeyOf(item.gameObject) != KeyOf(slot.gameObject)) continue;

                if (REQUIRE_POSINDEX_MATCH && item.posIndex != slot.posIndex) continue;

                Vector2 slotW = new Vector2(slot.transform.position.x, slot.transform.position.y);
                float d = Vector2.Distance(itemW, slotW);
                if (d < nearest) { nearest = d; nearestSlot = slot; }
            }

            if (!(nearestSlot != null && nearest <= SNAP_DIST_WORLD_XY))
                return false;
        }
        return true;
    }

    struct Pair { public Triangle item; public Triangle slot; public float d; }

    static List<Pair> CollectPairsWithinThreshold(List<Triangle> items, List<Triangle> slots)
    {
        var pairs = new List<Pair>(items.Count * 4);

        foreach (var item in items)
        {
            if (!item) continue;
            Vector2 itemW = new Vector2(item.transform.position.x, item.transform.position.y);

            foreach (var slot in slots)
            {
                if (!slot) continue;
                if (ReferenceEquals(item, slot)) continue;
                if (ENFORCE_NAME_MATCH && KeyOf(item.gameObject) != KeyOf(slot.gameObject)) continue;

                if (REQUIRE_POSINDEX_MATCH && item.posIndex != slot.posIndex) continue;

                Vector2 slotW = new Vector2(slot.transform.position.x, slot.transform.position.y);
                float d = Vector2.Distance(itemW, slotW);
                if (d <= SNAP_DIST_WORLD_XY)
                    pairs.Add(new Pair { item = item, slot = slot, d = d });
            }
        }

        pairs.Sort((a, b) => a.d.CompareTo(b.d));
        return pairs;
    }

    static Dictionary<Triangle, Triangle> AssignGreedy(List<Pair> pairs)
    {
        var assigned = new Dictionary<Triangle, Triangle>();
        var usedSlots = new HashSet<Triangle>();

        for (int i = 0; i < pairs.Count; i++)
        {
            var p = pairs[i];
            if (assigned.ContainsKey(p.item)) continue;
            if (usedSlots.Contains(p.slot)) continue;

            assigned[p.item] = p.slot;
            usedSlots.Add(p.slot);
        }
        return assigned;
    }

    static bool AllAssignedSlotsFree(Dictionary<Triangle, Triangle> assigned, bool log = false)
    {
        foreach (var kv in assigned)
        {
            var slotTri = kv.Value;
            if (!slotTri) continue;
            if (slotTri.isSnapped) return false;
        }
        return true;
    }

    static int ApplyWorldSnap(Transform boardRoot, Dictionary<Triangle, Triangle> assigned)
    {
        int snapCount = 0;

        foreach (var kv in assigned)
        {
            var itemTri = kv.Key;
            var slotTri = kv.Value;
            if (!itemTri || !slotTri) continue;

            Vector3 slotWorld = slotTri.transform.position;
            Vector3 slotLocalInBoard = boardRoot.InverseTransformPoint(slotWorld);
            slotLocalInBoard.z = BOARD_LOCAL_Z_ON_SNAP;
            float targetWorldZ = boardRoot.TransformPoint(slotLocalInBoard).z;

            itemTri.transform.position = new Vector3(slotWorld.x, slotWorld.y, targetWorldZ);
            snapCount++;
        }
        return snapCount;
    }

    public static void NudgeSnap(float delta)
    {
        SNAP_DIST_WORLD_XY = Mathf.Max(0f, SNAP_DIST_WORLD_XY + delta);
    }
    }
}