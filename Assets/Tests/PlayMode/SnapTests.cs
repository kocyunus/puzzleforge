using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Yunus.Game.Gameplay;

namespace Yunus.Game.Tests
{
    /// <summary>
    /// Behaviour tests for <see cref="SnapUtil.TrySnapToBoard"/>: the all-or-nothing gate,
    /// same-<c>posIndex</c> matching, occupied-slot rejection, and the commit (positions moved,
    /// both sides flagged).
    /// </summary>
    [TestFixture]
    public class SnapTests
    {
        readonly List<GameObject> _spawned = new();
        Transform _boardRoot;
        Transform _shapeRoot;
        ShapeData _shapeData;

        [SetUp]
        public void SetUp()
        {
            _boardRoot = New("Board").transform;
            _shapeRoot = New("Shape").transform;
            _shapeData = _shapeRoot.gameObject.AddComponent<ShapeData>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned) if (go) Object.DestroyImmediate(go);
            _spawned.Clear();
        }

        [Test]
        public void Snap_WhenAligned_MovesTrianglesAndFlagsBothSides()
        {
            var board = Cell(_boardRoot, Vector2.zero);
            var shape = Cell(_shapeRoot, new Vector2(0.4f, -0.3f));   // within SnapDistance (5)

            bool ok = SnapUtil.TrySnapToBoard(_shapeRoot, shape, _boardRoot, board, _shapeRoot);

            Assert.IsTrue(ok);
            Assert.IsTrue(board.All(t => t.isSnapped), "board slots not flagged");
            Assert.IsTrue(shape.All(t => t.isSnapped), "shape triangles not flagged");
            Assert.AreEqual(4, _shapeData.BoardSnappedTriangles.Count);
            for (int i = 0; i < 4; i++)
                Assert.That(Vector2.Distance(shape[i].transform.position, board[i].transform.position),
                    Is.LessThan(0.001f), "shape triangle not moved onto its slot");
        }

        [Test]
        public void Snap_WhenTooFar_DoesNothing()
        {
            var board = Cell(_boardRoot, Vector2.zero);
            var shape = Cell(_shapeRoot, new Vector2(100f, 100f));

            bool ok = SnapUtil.TrySnapToBoard(_shapeRoot, shape, _boardRoot, board, _shapeRoot);

            Assert.IsFalse(ok);
            Assert.IsTrue(board.All(t => !t.isSnapped));
            Assert.IsTrue(shape.All(t => !t.isSnapped));
        }

        [Test]
        public void Snap_WhenATargetSlotIsOccupied_Fails()
        {
            var board = Cell(_boardRoot, Vector2.zero);
            var shape = Cell(_shapeRoot, new Vector2(0.2f, 0.2f));
            board[2].SnapState(true);   // pre-occupy one slot

            bool ok = SnapUtil.TrySnapToBoard(_shapeRoot, shape, _boardRoot, board, _shapeRoot);

            Assert.IsFalse(ok);
        }

        [Test]
        public void Snap_WhenPosIndexDoesNotMatch_Fails()
        {
            var board = Cell(_boardRoot, Vector2.zero);
            var shape = Cell(_shapeRoot, new Vector2(0.2f, 0.2f));
            foreach (var t in shape) t.posIndex = 1;   // all the same -> only one board slot matches

            bool ok = SnapUtil.TrySnapToBoard(_shapeRoot, shape, _boardRoot, board, _shapeRoot);

            Assert.IsFalse(ok);
        }

        // --- helpers ---

        GameObject New(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            return go;
        }

        /// <summary>Four triangles (posIndex 1..4) at <paramref name="worldXY"/>, parented under <paramref name="parent"/>.</summary>
        List<Triangle> Cell(Transform parent, Vector2 worldXY)
        {
            var list = new List<Triangle>(4);
            for (int pos = 1; pos <= 4; pos++)
            {
                var go = New($"{parent.name}_tri{pos}");
                go.transform.SetParent(parent, false);
                go.transform.position = new Vector3(worldXY.x, worldXY.y, 0f);
                var tri = go.AddComponent<Triangle>();
                tri.Init(0, 0, 0);
                tri.posIndex = pos;
                list.Add(tri);
            }
            return list;
        }
    }
}
