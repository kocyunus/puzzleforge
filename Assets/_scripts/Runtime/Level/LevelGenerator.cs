using UnityEngine;
using Yunus.Game.Core;
using Yunus.Game.Domain.Ports;
using Yunus.Game.Services;
using Yunus.Game.Generation;

namespace Yunus.Game.Level
{
    public class LevelGenerator : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private GameObject trianglePrefab;
    [SerializeField] private GameObject shapePrefab;

    private LevelData currentLevel;
    private GridBuilder gridBuilder;
    private ShapeGenerator shapeGenerator;
    private IColorPalette colorPalette;

    // Pool handles (each manages its own pool)
    private IPrefabPool trianglePool;
    private IPrefabPool shapePool;

    public GridBuilder Grid => gridBuilder;
    public ShapeGenerator ShapeGen => shapeGenerator;

    void Awake()
    {
        ServiceLocator.TryGet<IColorPalette>(out colorPalette);
    }

    public void Initialize(LevelData levelData)
    {
        currentLevel = levelData;
        Debug.Log($"[LevelGenerator] Initialized with: {currentLevel}");
    }

    public void GenerateLevel()
    {
        if (currentLevel == null)
        {
            Debug.LogError("[LevelGenerator] ❌ Not initialized! Call Initialize() first.");
            return;
        }

        // 0) pooler → iki ayrı handle (varsa tekrar prewarm etmez)
        if (ServiceLocator.TryGet<IPrefabPooler>(out var pooler))
        {
            var prewarmCount = currentLevel.gridHeight * currentLevel.gridWidth * 4;
            trianglePool ??= pooler.CreatePool(trianglePrefab, prewarmCount);
            shapePool ??= pooler.CreatePool(shapePrefab, prewarmCount: 12);
        }

        ClearShapes();
        ClearGrid();

        transform.position = Vector3.zero;
        
        // 1) Build Grid
        gridBuilder = new GridBuilder(
            trianglePool,
            transform,
            currentLevel.gridWidth,
            currentLevel.gridHeight 
        );
        gridBuilder.BuildGrid();
        transform.position = new Vector3(
            0,
            -currentLevel.gridHeight * 7 ,
            0
        );
        
        // 2) Generate Shapes
        shapeGenerator = new ShapeGenerator(
            gridBuilder,
            shapePool,
            transform,
            colorPalette,
            currentLevel.minTrianglesPerBox
        );
        shapeGenerator.ShapeCount = currentLevel.shapeCount;
        shapeGenerator.SeedMinCellDistance = currentLevel.seedMinCellDistance;  // Use from LevelData
        shapeGenerator.PreSeedCorners = true;
        shapeGenerator.GenerateShapes();
        ScatterShapes();

        Debug.Log($"[LevelGenerator] ✅ Generated: {gridBuilder.AllTriangles.Count} triangles, {shapeGenerator.Shapes.Count} shapes");
    }

    public void ClearGrid()
    {
        gridBuilder?.DeSpawn();   // Returns grid objects to pool
        Debug.Log("[LevelGenerator] Cleared grid.");
    }

    public void ClearShapes()
    {
        shapeGenerator?.Clear();  // Returns shape roots to pool
        Debug.Log("[LevelGenerator] Cleared shapes.");
    }

    private void ScatterShapes(
        float tileSize = 10f,
        float paddingFactor = 0.15f,   // Less edge padding
        float spacingFactor = 0.40f,   // Closer to each other
        float rectScale = 0.60f,       // Shrink area slightly
        int? seed = null,
        float z = 0f)
    {
        if (currentLevel == null || shapeGenerator == null ||
            shapeGenerator.Shapes == null || shapeGenerator.Shapes.Count == 0)
            return;

        if (!ServiceLocator.TryGet<IShapeScatter>(out var scatter))
            return;

        float w = currentLevel.gridWidth * tileSize;
        float h = currentLevel.gridHeight * tileSize;

        // Center pivot assumption → base rectangle
        float padding = tileSize * paddingFactor;
        var rect = new Rect(
            -w * 0.5f + padding,
            -h * 0.5f + padding,
            w - 2f * padding,
            h - 2f * padding
        );

        // Upper band: use top 50% of the area (middle and top)
        float bandHeight = rect.height * 0.50f;       // Can be adjusted between 0.35-0.6 if needed
        float yMax = rect.yMax;
        float yMin = Mathf.Max(rect.center.y, yMax - bandHeight);
        rect = new Rect(rect.xMin, yMin, rect.width, yMax - yMin);

        var opts = new Yunus.Game.Services.ShapeScatterOptions
        {
            MinSpacing = tileSize * spacingFactor,
            RectScale = rectScale,
            Z = z,
            Seed = seed
        };

        scatter.Scatter(shapeGenerator.Shapes, rect, opts);
    }
    }
}
