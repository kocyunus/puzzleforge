using System.Collections.Generic;
using UnityEngine;
using Yunus.Game.Core;

namespace Yunus.Game.Gameplay
{
    /// <summary>
    /// Snaps a dragged shape onto the board. All-or-nothing: unless every triangle of the shape
    /// has a free, same-<c>posIndex</c> board slot within <see cref="SnapDistance"/>, nothing moves.
    /// Otherwise triangles are matched to slots greedily (nearest first, no slot double-booked),
    /// moved onto their slots, and both sides flagged <c>isSnapped</c>.
    /// </summary>
    public static class SnapUtil
    {
        /// <summary>Max XY world distance from a shape triangle to its board slot (triangle spacing is 10).</summary>
        public const float SnapDistance = 5f;

        /// <summary>Local Z given to a triangle once it sits on the board.</summary>
        const float BoardLocalZOnSnap = -2f;

        readonly struct Pair
        {
            public readonly Triangle Item;
            public readonly Triangle Slot;
            public readonly float Dist;
            public Pair(Triangle item, Triangle slot, float dist) { Item = item; Slot = slot; Dist = dist; }
        }

        /// <summary>
        /// Attempts to snap <paramref name="shapeTriangles"/> onto <paramref name="boardTriangles"/>.
        /// Returns true and commits the snap on success; returns false and leaves everything in place
        /// otherwise.
        /// </summary>
        public static bool TrySnapToBoard(
            Transform shapeRoot,
            List<Triangle> shapeTriangles,
            Transform boardRoot,
            List<Triangle> boardTriangles,
            Transform shapeParent)
        {
            if (!boardRoot || shapeTriangles == null || boardTriangles == null) return false;

            int need = 0;
            for (int i = 0; i < shapeTriangles.Count; i++) if (shapeTriangles[i]) need++;
            if (need == 0) return false;

            if (!EveryTriangleHasACandidate(shapeTriangles, boardTriangles)) return false;

            var assigned = AssignGreedy(CollectPairs(shapeTriangles, boardTriangles));
            if (assigned.Count != need) return false;   // all-or-nothing: every triangle must land
            if (AnyAssignedSlotOccupied(assigned)) return false;

            var shapeData = shapeParent != null ? shapeParent.GetComponent<ShapeData>() : null;
            if (shapeParent != null && shapeData == null)
            {
                Debug.LogWarning($"[SnapUtil] ShapeData not found on {shapeParent.name}; aborting snap.");
                return false;
            }

            foreach (var (item, slot) in assigned)
            {
                MoveOntoSlot(boardRoot, item, slot);
                item.SnapState(true);
                slot.SnapState(true);
                shapeData?.RegisterBoardTriangle(slot);
            }

            GameLog.Info($"[SnapUtil] snapped {assigned.Count} triangles");
            return true;
        }

        // --- steps ---

        /// <summary>Gate: every shape triangle must have at least one in-range, same-posIndex slot.</summary>
        static bool EveryTriangleHasACandidate(List<Triangle> items, List<Triangle> slots)
        {
            foreach (var item in items)
            {
                if (!item) return false;

                bool found = false;
                Vector2 itemXY = item.transform.position;

                foreach (var slot in slots)
                {
                    if (!slot || ReferenceEquals(item, slot) || item.posIndex != slot.posIndex) continue;
                    if (Vector2.Distance(itemXY, (Vector2)slot.transform.position) <= SnapDistance)
                    {
                        found = true;
                        break;
                    }
                }

                if (!found) return false;
            }
            return true;
        }

        static List<Pair> CollectPairs(List<Triangle> items, List<Triangle> slots)
        {
            var pairs = new List<Pair>(items.Count * 4);

            foreach (var item in items)
            {
                if (!item) continue;
                Vector2 itemXY = item.transform.position;

                foreach (var slot in slots)
                {
                    if (!slot || ReferenceEquals(item, slot) || item.posIndex != slot.posIndex) continue;

                    float d = Vector2.Distance(itemXY, (Vector2)slot.transform.position);
                    if (d <= SnapDistance) pairs.Add(new Pair(item, slot, d));
                }
            }

            pairs.Sort((a, b) => a.Dist.CompareTo(b.Dist));
            return pairs;
        }

        static Dictionary<Triangle, Triangle> AssignGreedy(List<Pair> pairs)
        {
            var assigned = new Dictionary<Triangle, Triangle>();
            var usedSlots = new HashSet<Triangle>();

            foreach (var p in pairs)
            {
                if (assigned.ContainsKey(p.Item) || usedSlots.Contains(p.Slot)) continue;
                assigned[p.Item] = p.Slot;
                usedSlots.Add(p.Slot);
            }
            return assigned;
        }

        static bool AnyAssignedSlotOccupied(Dictionary<Triangle, Triangle> assigned)
        {
            foreach (var kv in assigned)
                if (kv.Value && kv.Value.isSnapped) return true;
            return false;
        }

        static void MoveOntoSlot(Transform boardRoot, Triangle item, Triangle slot)
        {
            if (!item || !slot) return;

            Vector3 slotWorld = slot.transform.position;
            Vector3 slotLocal = boardRoot.InverseTransformPoint(slotWorld);
            slotLocal.z = BoardLocalZOnSnap;
            float worldZ = boardRoot.TransformPoint(slotLocal).z;

            item.transform.position = new Vector3(slotWorld.x, slotWorld.y, worldZ);
        }
    }
}
