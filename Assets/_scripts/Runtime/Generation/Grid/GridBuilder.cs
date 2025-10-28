using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using Yunus.Game.Core;
using Yunus.Game.Gameplay;
namespace Yunus.Game.Generation
{
    /// <summary>
    /// Builds the puzzle grid structure: creates triangles and organizes them spatially.
    /// 
    /// GRID STRUCTURE:
    /// - Grid consists of W×H squares (cells)
    /// - Each square contains exactly 4 triangles (Up, Right, Down, Left)
    /// - Total triangles = W × H × 4
    /// - Triangles are stored in TriangleGameObjects list
    /// - Spatial lookup via AllTriangles dictionary (box + posIndex → triangle)
    /// 
    /// INDEXING:
    /// posIndex mapping:
    ///   1 = Up (↑)
    ///   2 = Right (→)
    ///   3 = Left (←)
    ///   4 = Down (↓)
    /// 
    /// USAGE:
    /// 1. Create GridBuilder with dimensions
    /// 2. Call GenerateGrid() to spawn triangles
    /// 3. Access AllTriangles for spatial queries
    /// 4. Access TriangleGameObjects for iteration/pooling
    /// </summary>
    public class GridBuilder
{
    private readonly IPrefabPool trianglePool;
    private readonly Transform parentTransform;

    // Configuration
    public int GridWidth { get; set; }
    public int GridHeight { get; set; }
    public float TriangleSize { get; set; }

    const float defaultTriangleSize = 10f;
    
    // Output
    public List<Triangle> AllTriangles { get; private set; }
    public List<GameObject> TriangleGameObjects { get; private set; }
    public Dictionary<long, Triangle> TriangleRegistry { get; private set; }

    private const int TRIANGLES_PER_SQUARE = 4;

    public GridBuilder(IPrefabPool trianglePool, Transform parent, int width, int height)
    {
        this.trianglePool = trianglePool;
        this.parentTransform = parent;
        this.GridWidth = width;
        GridWidth = math.clamp(GridWidth, 4, 6); // Clamp to case requirements
        this.GridHeight = height;
        GridHeight = math.clamp(GridHeight, 4, 6); // Clamp to case requirements
        this.TriangleSize = defaultTriangleSize;

        AllTriangles = new List<Triangle>();
        TriangleGameObjects = new List<GameObject>();
        TriangleRegistry = new Dictionary<long, Triangle>();
    }

    public void BuildGrid()
    {
        Clear();

        for (int x = 0; x < GridWidth; x++)
            for (int y = 0; y < GridHeight; y++)
                CreateSquare(x, y);

        Debug.Log($"[GridBuilder] Created {AllTriangles.Count} triangles ({GridWidth}×{GridHeight})");
    }

    void CreateSquare(int x, int y)
    {
        int boxIndex = y * GridWidth + x;

        for (int i = 0; i < TRIANGLES_PER_SQUARE; i++)
        {
            int angle = i * 90;
            Vector3 position = new Vector3(x * TriangleSize, y * TriangleSize, 0);

            // Spawn from pool
            GameObject go = trianglePool.SpawnImmediate(position, Quaternion.Euler(0, 0, angle), parentTransform);

            var tri = go.GetComponent<Triangle>();
            if (!tri)
            {
                Debug.LogError("[GridBuilder] Triangle component missing!");
                trianglePool.Despawn(go); // Return to pool
                continue;
            }
           
            tri.Init(x, y, angle, boxIndex);
            tri.posIndex = Triangle.FacingToPosIndex(tri.facing);

            long key = PackKey(x, y, tri.posIndex);
            TriangleRegistry[key] = tri;
            AllTriangles.Add(tri);
            TriangleGameObjects.Add(go);
        }
    }

    void Clear()
    {
        AllTriangles.Clear();
        TriangleGameObjects.Clear();
        TriangleRegistry.Clear();
    }

    /// <summary>
    /// Returns all triangles to the pool and clears internal lists.
    /// </summary>
    public void DeSpawn()
    {
        foreach (var go in TriangleGameObjects)
            if (go) trianglePool.Despawn(go); // Return to pool

        Clear();
    }

    public static long PackKey(int x, int y, int posIndex)
        => ((long)x << 32) | ((long)y << 16) | (long)posIndex;

    public bool TryGetTriangle(int x, int y, int posIndex, out Yunus.Game.Gameplay.Triangle tri)
        => TriangleRegistry.TryGetValue(PackKey(x, y, posIndex), out tri);
    }
}
