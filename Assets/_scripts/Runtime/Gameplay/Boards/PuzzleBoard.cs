using System.Collections.Generic;
using UnityEngine;
using Yunus.Game.Core;          // IPrefabPooler, IPrefabPool, ServiceLocator
using Yunus.Game.Generation;
using Yunus.Game.Gameplay;
using Yunus.Game.Level;
using Yunus.Game.UI;

namespace Yunus.Game.Board
{
    [DisallowMultipleComponent]
    public class PuzzleBoard : MonoBehaviour
    {
        private IPrefabPool trianglePool;

        [Header("Grid Configuration")]
        [SerializeField] private GameObject trianglePrefab;

        [Header("Appearance")]
        [SerializeField] private Color baseColor = new Color(0.75f, 0.75f, 0.75f, 1f);

        [Header("Game State")]
        public int placedPieceCount = 0;
        public int totalPiecesNeeded = 0;

        [Header("UI")]
        [SerializeField] private LevelCompleteUI levelCompleteUI;

        // kendi grid'i
        private GridBuilder solutionGrid;

        // Public access
        public List<Yunus.Game.Gameplay.Triangle> Triangles => solutionGrid?.AllTriangles;

        public int TotalSlots => solutionGrid?.AllTriangles.Count ?? 0;

        public bool IsPuzzleComplete => placedPieceCount >= totalPiecesNeeded && totalPiecesNeeded > 0;

        private LevelData currentLevel;

        public void Initialize(LevelData levelData)
        {
            currentLevel = levelData;

            if (!trianglePrefab)
            {
                Debug.LogError("[PuzzleBoard] Triangle prefab missing!");
                return;
            }
            ClearGrid();
            BuildSolutionGrid();

            totalPiecesNeeded = levelData.shapeCount;
            placedPieceCount = 0;

            Debug.Log($"[PuzzleBoard] Initialized: {levelData.gridWidth}×{levelData.gridHeight}, need {totalPiecesNeeded} pieces");
        }

        void BuildSolutionGrid()
        {
            ClearGrid();

            // Create pool handle if not exists
            if (trianglePool == null)
            {
                var prewarmCount = currentLevel.gridHeight * currentLevel.gridWidth*4;
                if (ServiceLocator.TryGet<IPrefabPooler>(out var pooler))
                    trianglePool = pooler.CreatePool(trianglePrefab,prewarmCount);
                else
                {
                    Debug.LogError("[PuzzleBoard] IPrefabPooler not found via ServiceLocator.");
                    return;
                }
            }

            solutionGrid = new GridBuilder(
                trianglePool,
                transform,
                currentLevel.gridWidth,
                currentLevel.gridHeight
            );
            solutionGrid.BuildGrid();

            TintAllTriangles(baseColor);

            Debug.Log($"[PuzzleBoard] Solution grid built: {TotalSlots} slots");
        }
 
        public void ClearGrid()
        {
            solutionGrid?.DeSpawn(); // Returns all grid objects to pool
        }

        void TintAllTriangles(Color color)
        {
            if (solutionGrid == null) return;

            foreach (var tri in solutionGrid.AllTriangles)
            {
                // Use TriangleMeshRenderer instead of Shapes.Triangle
                var meshRenderer = tri.GetComponent<TriangleMeshRenderer>();
                if (meshRenderer != null)
                {
                    meshRenderer.SetColor(color);
                }
            }
        }

        // ---------------- Game State ----------------

        public void OnShapePlaced()
        {
            placedPieceCount++;

            if (IsPuzzleComplete)
            {
                Debug.Log("[PuzzleBoard] 🎉 PUZZLE COMPLETE!");
                OnPuzzleComplete();
            }
        }

        public void OnShapeRemoved()
        {
            placedPieceCount--;
            if (placedPieceCount < 0) placedPieceCount = 0;
        }

        void OnPuzzleComplete()
        {
            if (levelCompleteUI != null)
                levelCompleteUI.Show();
            else
                Debug.LogWarning("[PuzzleBoard] LevelCompleteUI reference missing!");
        }
    }
}
