# PuzzleForge 🧩

**Procedurally generated puzzle games engine** - Guaranteed-solvable, always different.

A production-ready Unity puzzle framework demonstrating advanced procedural generation algorithms, clean architecture patterns, and custom rendering optimization. This project serves as both a fully playable game and a reference implementation for complex grid-based puzzle systems.

---

## 🎯 Core Features

- **✨ Procedural Generation:** 100% different layouts every run
- **✅ Guaranteed Solvable:** Mathematical guarantees ensure every level is winnable
- **🎮 Drag & Drop Gameplay:** Intuitive shape-snapping puzzle mechanics
- **📊 Configurable Difficulty:** Easy, Medium, Hard with JSON-based level definitions
- **⚡ Optimized Performance:** Object pooling, O(1) grid-registry lookups, incremental ownership tracking
- **🏗️ Clean Architecture:** Service locator pattern with clear separation of concerns
- **🔄 Multi-Seed Shape Synthesis:** Balanced, distributed shape generation
- **🎨 Custom Rendering:** Zero external asset dependencies - custom triangle mesh renderer

---

## 🚀 Quick Start

### Prerequisites
- Unity 2022.3+
- C# 9.0+
- Visual Studio or Rider

### Installation

```bash
git clone https://github.com/kocyunus/puzzleforge.git
cd puzzleforge
# Open in Unity Hub
```

---

## 🎥 Gameplay Demonstrations

### Demo
![Easy Gameplay](Recordings/Movie_001.gif)

### Demo
![Medium Gameplay](Recordings/Movie_003.gif)

### Demo
![Hard Gameplay](Recordings/Movie_004.gif)

---

## 🎮 How to Play

1. **Select a Shape** - Click on any colored shape
2. **Drag the Shape** - Move it to the puzzle grid
3. **Place & Snap** - Release to snap into position
4. **Complete Level** - Fill the entire grid to win

---

## ⚙️ Level Selection & Difficulty

You can easily change the difficulty level and select specific levels through the **LevelManager** Inspector:

1. **Select Main Camera** in scene hierarchy
2. **Find LevelManager component** in Inspector
3. **Change "Selected Difficulty":** Easy, Medium, or Hard
4. **Optional - Play Specific Level:** Set "Specific Level Id" to play a particular level (e.g., "easy-1", "hard-3")


---

## 🏗️ Architecture Overview

### Directory Structure
```
Assets/_scripts/Runtime/
├── Composition/        # Service bootstrap
├── Core/              # Interfaces & abstractions
├── Data/              # ScriptableObjects
├── Domain/            # Value types & logic
├── Gameplay/          
│   ├── Boards/       # PuzzleBoard management
│   ├── Components/   # Triangle, ShapeData, TriangleMeshRenderer
│   ├── Input/        # Drag & drop input
│   └── Snapping/     # Snap-to-grid algorithm
├── Generation/        
│   ├── Grid/         # GridBuilder
│   ├── Seeding/      # SeedCellSelector (pure, unit-tested)
│   └── Shapes/       # ShapeGenerator, NeighborSelector
├── Level/            # Level loading
├── Services/         # Infrastructure (pooling, etc)
└── Ui/               # Minimal UI

Assets/_scripts/Runtime/     # PuzzleForge.Runtime.asmdef

Assets/Tests/
├── EditMode/          # SeedCellSelector unit tests
└── PlayMode/          # ShapeGenerator coverage / partition tests
```

### Key Design Patterns

| Pattern | Location | Purpose |
|---------|----------|---------|
| **Service Locator** | `Composition/` | Global service registry |
| **Object Pooling** | `Services/Pooling/` | Reuse objects efficiently |
| **Factory Pattern** | `Generation/` | Shape creation |
| **Strategy Pattern** | `Generation/Shapes/` | Neighbor selection rules |
| **Component-Based** | `Gameplay/` | Entity-component design |

---

## 🧠 Algorithm Deep Dive

### 0. Grid Structure (Visual Reference)

Each puzzle uses a grid of squares, where each square contains exactly 4 triangles (Up, Right, Down, Left):

![Grid Structure Diagram](docs/grid-structure.png)

**posIndex mapping:**
- `1` = Up (↑)
- `2` = Right (→)
- `3` = Left (←)
- `4` = Down (↓)

This position-based indexing is crucial for the entry/exit door logic during shape expansion.

---

### 1. Multi-Seed Shape Generation (BFS-based)

**Problem:** Generate balanced, non-overlapping shapes that fill 100% of grid.

**Solution:** Distributed seed placement + turn-based expansion with priority rules.

#### Seed Distribution Strategy

Cell choice lives in `SeedCellSelector` — a pure, unit-tested class with **no
`MonoBehaviour` dependency**. It always returns exactly `min(shapeCount, cellCount)`
distinct in-bounds cells, so generation can never stall or crash on a tight config.

```csharp
public static List<Vector2Int> Select(
    int gridWidth, int gridHeight, int shapeCount,
    int minCellDistance, bool preSeedCorners, System.Random rng)
{
    int target = Clamp(shapeCount, 0, gridWidth * gridHeight);

    // Step 1: pre-seed up to 4 corners (natural starting points)
    if (preSeedCorners) TakeCorners(chosen, target);

    // Step 2: greedy "far-first" fill at the requested distance, then
    //         relax the distance step-by-step (d, d-1, ... 0)
    for (int d = minCellDistance; d >= 0 && chosen.Count < target; d--)
        FillGreedy(chosen, edges, centers, phase, d, target);

    // Step 3: last resort - take any remaining free cell
    if (chosen.Count < target) FillAnyRemaining(chosen, target);

    return chosen;
}
```

`ShapeGenerator` just resolves each chosen cell to a concrete triangle:

```csharp
List<Triangle> PickDistributedSeeds(int K)
{
    var cells = SeedCellSelector.Select(
        grid.GridWidth, grid.GridHeight, K,
        SeedMinCellDistance, PreSeedCorners, seedRng);

    var seeds = new List<Triangle>(cells.Count);
    foreach (var c in cells)
    {
        var t = FindTriangleAt(c.x, c.y);   // O(1) via the grid registry
        if (t != null) seeds.Add(t);
    }
    return seeds;
}
```

**Why This Works:**
- Corners first → Natural starting points
- Far-first selection → Balanced distribution
- Minimum distance rule → Avoids clustering
- Progressive relaxation → `SeedCellSelector` always returns exactly
  `min(shapeCount, gridWidth × gridHeight)` cells: it honours the minimum
  distance where the grid allows, then relaxes it step-by-step, then fills from
  any free cell. Generation can never stall or crash on a tight config.

#### Shape Growth Algorithm
```csharp
// Turn-based expansion. Ownership lives on Triangle.ownerShapeIndex, and a
// running `unownedCount` replaces the old per-move rebuild of an "unowned" list.
void GrowShapes()
{
    bool anyProgress = true;

    while (anyProgress && unownedCount > 0)
    {
        anyProgress = false;

        foreach (var shape in Shapes)
        {
            for (int m = 0; m < shape.MovesPerTurn          // Random(1, 2)
                            && shape.GrowthQueue.Count > 0
                            && unownedCount > 0; m++)
            {
                var current = shape.GrowthQueue.Dequeue();  // Queue<Triangle>

                // 3-priority selector, then a retry from any owned triangle
                if (NeighborSelector.TryPickNext(
                        current, shape, gridBuilder, minTrianglesPerBox, out var picked)
                    || TryGrowFromAnyOwned(shape, current, out picked))
                {
                    ClaimTriangle(picked, shape);           // O(1): owner + count
                    shape.GrowthQueue.Enqueue(picked);
                    anyProgress = true;
                }
                // else: this frontier triangle can't expand - drop it
            }
        }
    }

    EnsureFullCoverage();   // BFS mop-up: 100% coverage, guaranteed
}
```

**Key Properties:**
- ✅ Turn-based prevents one shape hogging
- ✅ Queue-based ensures all shapes progress
- ✅ Random moves per turn = varied results each run
- ✅ **Guaranteed 100% grid coverage** — after the turn-based phase, `EnsureFullCoverage`
  runs a BFS from the owned frontier and assigns every remaining triangle to an adjacent
  shape. The grid's triangle graph is connected and every shape has a seed, so coverage is
  total by construction (a PlayMode test asserts it for every level config).
- ✅ O(1) bookkeeping — ownership is a field on `Triangle` (`ownerShapeIndex`) plus a running
  count; no per-move rebuild of an "unowned" list, and `NeighborSelector` reads the grid via
  O(1) registry lookups.

---

### 2. Three-Priority Neighbor Selection

**Problem:** Expand shapes smoothly across grid without overlaps.

**Solution:** Priority-based selection with entry/exit door logic.

All grid queries go through `grid.TryGetTriangle(x, y, pos, out t)` (an O(1)
dictionary) plus a bounds check — there is no linear scan of an "unowned pool".

```csharp
public static bool TryPickNext(
    Triangle current,
    ShapeData shape,
    GridBuilder grid,
    int minTrianglesPerBox,
    out Triangle picked)
{
    int group = shape.ShapeIndex;

    // PRIORITY 1: fill the current cell first (up to minTrianglesPerBox)
    if (TryPickFromSameCell(current, grid, group, minTrianglesPerBox, out picked))
        return true;

    // PRIORITY 2: step into an adjacent cell through a matching entry/exit door
    if (TryPickFromAdjacentCell(current, grid, group, out picked))
        return true;

    // PRIORITY 3: fill any empty position in a cell the shape already owns
    if (TryPickFromAnyOwnedCell(shape, grid, out picked))
        return true;

    picked = null;
    return false;
}
```

#### Entry/Exit Door Logic
```csharp
// Only expand via matching positions
static int GetExitDoorPosition(int fromX, int fromY, int toX, int toY)
{
    int dx = toX - fromX;
    int dy = toY - fromY;
    
    // Up (1) ↔ Down (4), Right (2) ↔ Left (3)
    if (dx == 1) return 2;   // Moving RIGHT: exit via RIGHT
    if (dx == -1) return 3;  // Moving LEFT: exit via LEFT
    if (dy == 1) return 1;   // Moving UP: exit via UP
    if (dy == -1) return 4;  // Moving DOWN: exit via DOWN
    
    return -1;
}
```

**Why Entry/Exit Doors?**
- Smooth borders between shapes
- Natural flow (no weird zigs/zags)
- Prevents fragmented expansions

---

### 3. Custom Triangle Rendering (Zero External Assets)

**Problem:** Need efficient, dynamic triangle rendering without external assets.

**Solution:** Mesh generation + vertex colors + MaterialPropertyBlock.

```csharp
public class TriangleMeshRenderer : MonoBehaviour
{
    [SerializeField] private Vector3 vertexA = new(0, 0.5f, 0);
    [SerializeField] private Vector3 vertexB = new(-0.5f, -0.5f, 0);
    [SerializeField] private Vector3 vertexC = new(0.5f, -0.5f, 0);
    [SerializeField] private Color triangleColor = Color.white;
    
    private Mesh triangleMesh;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private MaterialPropertyBlock mpb;
    
    private void Initialize()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        
        // Create material with custom shader
        var shader = Shader.Find("Custom/SimpleTriangleColor");
        var mat = new Material(shader);
        meshRenderer.material = mat;
        
        // Reusable property block (no material duplication)
        mpb = new MaterialPropertyBlock();
        
        // Create mesh
        triangleMesh = new Mesh();
        meshFilter.mesh = triangleMesh;
    }
    
    public void UpdateMesh()
    {
        triangleMesh.Clear();
        
        // 3 vertices
        triangleMesh.vertices = new Vector3[] 
        { 
            vertexA, vertexB, vertexC 
        };
        
        // Double-sided (front + back)
        triangleMesh.triangles = new int[] 
        { 
            0, 1, 2,  // Front
            0, 2, 1   // Back
        };
        
        triangleMesh.RecalculateNormals();
        triangleMesh.RecalculateBounds();
    }
    
    public void UpdateColor()
    {
        // Vertex colors
        Color[] colors = new Color[3];
        for (int i = 0; i < 3; i++)
            colors[i] = triangleColor;
        triangleMesh.colors = colors;
        
        // Apply via MaterialPropertyBlock (efficient!)
        if (mpb != null && meshRenderer != null)
        {
            mpb.SetColor("_Color", triangleColor);
            meshRenderer.SetPropertyBlock(mpb);  // No new material created
        }
    }
    
    public void SetColor(Color newColor)
    {
        triangleColor = newColor;
        UpdateColor();
    }
}
```

**Why MaterialPropertyBlock?**
- Single update applies to all triangles
- No new material instances (saves memory)
- Efficient batch rendering

---

### 4. Snap-to-Grid Algorithm

**Problem:** User drags shape, need to snap to valid grid positions.

**Solution:** Greedy distance-based matching with validation.

```csharp
public void TrySnap()
{
    var trianglesOnBoard = boardGrid.AllTriangles;
    var shapeTris = GetOwnedTriangles();
    
    var matches = new List<(Triangle shapeTri, Triangle boardTri, float dist)>();
    
    // Find candidate matches
    foreach (var shapeTri in shapeTris)
    {
        foreach (var boardTri in trianglesOnBoard)
        {
            // Only same pos-indices snap
            if (shapeTri.posIndex != boardTri.posIndex)
                continue;
            
            // Only within snap distance
            float dist = Vector3.Distance(
                shapeTri.transform.position,
                boardTri.transform.position);
            
            if (dist <= SNAP_DIST)
                matches.Add((shapeTri, boardTri, dist));
        }
    }
    
    // Sort by distance (nearest first)
    matches.Sort((a, b) => a.dist.CompareTo(b.dist));
    
    // Greedy assignment (no double-booking)
    var assigned = new HashSet<Triangle>();
    foreach (var (shapeTri, boardTri, _) in matches)
    {
        if (!assigned.Contains(boardTri))
        {
            boardTri.RegisterSnap(this);
            assigned.Add(boardTri);
        }
    }
    
    // Move to board if all triangles snapped
    if (assigned.Count == shapeTris.Count)
        transform.SetParent(boardTransform, true);
}
```

---

## 🎨 Customization

### Change Level Difficulty
Edit `Assets/levels.json`:

```json
{
  "levels": [
    {
      "levelId": "level-1",
      "difficulty": "easy",
      "gridWidth": 4,
      "gridHeight": 4,
      "shapeCount": 5,
      "minTrianglesPerBox": 4,
      "seedMinCellDistance": 1
    },
    {
      "levelId": "level-7",
      "difficulty": "hard",
      "gridWidth": 6,
      "gridHeight": 6,
      "shapeCount": 10,
      "minTrianglesPerBox": 3,
      "seedMinCellDistance": 1
    }
  ]
}
```

Level ids are `level-1` … `level-9` (three per difficulty). `LevelData.IsValid()`
enforces the ranges: grid 4–6, `shapeCount` 5–12, `minTrianglesPerBox` 2–4,
`seedMinCellDistance` 1–3.

### Modify Colors
1. Create new ColorPaletteSO in `Assets/Resources/`
2. Assign to LevelManager Inspector
3. Changes apply to all new levels

---

## 🔧 Configuration

### Level Parameters

| Parameter | Type | Effect |
|-----------|------|--------|
| `gridWidth` | int | Grid width in squares (4–6) |
| `gridHeight` | int | Grid height in squares (4–6) |
| `shapeCount` | int | Number of shapes to generate (5–12) |
| `minTrianglesPerBox` | int | Min triangles per square before a shape spreads (2–4) |
| `seedMinCellDistance` | int | Preferred spacing between shape seeds (1–3); relaxed automatically if the grid can't fit that many |

### Loading Levels

```csharp
// From local JSON
levelManager.localJsonFile = levels;
levelManager.downloadFromServer = false;

// From server (with fallback)
levelManager.serverUrl = "https://api.example.com/levels.json";
levelManager.downloadFromServer = true;

// Test specific level
levelManager.specificLevelId = "level-5";
```

---

## 📚 Patterns & Architecture Decisions

### Service Locator (Global Registry)
```csharp
// Register service
ServiceLocator.Register<IColorPalette>(new ColorPaletteService());

// Retrieve globally
ServiceLocator.TryGet<IColorPalette>(out var palette);
```

**Trade-off:** Convenient global access vs hidden dependencies.
**When to use:** For singleton-like services (pools, managers, config).

### Object Pooling (Reuse, Don't Recreate)
```csharp
// Create pool
var trianglePool = pooler.CreatePool(trianglePrefab, count: 256);

// Get from pool
var tri = trianglePool.SpawnImmediate(pos, rot, parent);

// Return to pool
trianglePool.Despawn(tri);
```

**Benefit:** Zero GC allocation after pool warmup.

### Strategy Pattern (Flexible Expansion)
```csharp
// NeighborSelector implements multiple strategies
// - Priority 1: Same-cell neighbors
// - Priority 2: Adjacent-cell neighbors  
// - Priority 3: Fallback

// Easy to modify rules without changing core algorithm
```

---

## 🧪 Tests

Automated tests use the **Unity Test Framework** (NUnit). Runtime code is in the
`PuzzleForge.Runtime` assembly so test assemblies can reference it.

**Run them:**
- Editor: `Window ▸ General ▸ Test Runner ▸` (EditMode / PlayMode) `▸ Run All`
- CLI: `Unity -batchmode -runTests -testPlatform EditMode -projectPath . -logFile -`
  (and again with `-testPlatform PlayMode`)

**Current coverage:**

| Suite | Mode | What it checks |
|-------|------|----------------|
| `SeedCellSelectorTests` | EditMode | Seed placement returns exactly `min(shapeCount, cells)` distinct in-bounds cells for every `levels.json` config (regression for the generation lock-up), plus min-distance, clamping, determinism, and degenerate inputs. |
| `ShapeGenerationTests` | PlayMode | Every level config fills 100% of the grid, shapes form a disjoint partition of the triangles, `ownerShapeIndex` agrees with each shape, generation is reproducible for a fixed seed. Also covers the two configs that used to crash. |

The seed logic is isolated in `SeedCellSelector` (its own assembly, no `MonoBehaviour`
dependency) so it can be unit-tested directly; the PlayMode suite drives the real
`GridBuilder` / `ShapeGenerator` through a lightweight fake object pool.

---

## 🧪 Testing Your Own Implementation

### Test Determinism
```csharp
// Use seed for reproducible generation
Random.InitState(42);

LevelGenerator gen1 = new LevelGenerator();
gen1.GenerateLevel(); // Get level A

Random.InitState(42);

LevelGenerator gen2 = new LevelGenerator();
gen2.GenerateLevel(); // Get level B (should be identical)
```

### Test Solvability
```csharp
// Verify every shape can snap to board
var board = GetBoard();
foreach (var shape in GeneratedShapes)
{
    int snapCount = board.CountPossibleSnaps(shape);
    Assert.Greater(snapCount, 0);
}
```

### Test Coverage
```csharp
// Verify 100% grid fill
int totalTriangles = gridWidth * gridHeight * 4;
int ownedTriangles = shapes.SelectMany(s => s.OccupiedTriangles).Count();
Assert.AreEqual(totalTriangles, ownedTriangles);
```

---

## 📖 How to Extend

### Add New Neighbor Selection Rule
1. Add method to `NeighborSelector` (e.g., `TryPickFromDiagonal()`)
2. Insert in the `TryPickNext` priority chain before/after existing priorities
3. Run the PlayMode `ShapeGenerationTests` to confirm 100% coverage and a clean
   disjoint partition still hold for every level config

### Add New Difficulty Tier
1. Add entry to `levels.json` with custom parameters
2. Update UI to show new difficulty
3. Adjust `minTrianglesPerBox` for different shape variety

### Implement Async Level Loading
1. Move `GenerateLevel()` to coroutine
2. Yield between major phases (seed picking → growing → coloring)
3. Update UI progress bar

---

## 🐛 Known Limitations

- **UI Minimal:** Only level complete screen
- **No Audio:** No music or effects
- **No Undo:** No move history
- **Single Player:** No multiplayer
- **Generation RNG not fully seeded:** seed placement is injectable, but `MovesPerTurn`
  and shape colour still use the global `UnityEngine.Random`, so a single seed does not
  reproduce a run bit-for-bit yet

---

## 📄 License

MIT License - See LICENSE file

---

## 👨‍💻 Development

### Code Examples
- Full algorithm implementations in `ShapeGenerator.cs`
- Entry/exit door logic in `NeighborSelector.cs`
- Custom rendering in `TriangleMeshRenderer.cs`
- XML documentation throughout codebase

### Repository
- Clean Architecture with clear separation
- Service Locator for dependency management
- Object pooling for performance
- Grid-based spatial lookups (O(1))

---

**Last Updated:** September 2, 2026  
**Unity:** 2022.3 LTS (tested on 2022.3.62f3)  
**C#:** 9.0+

---

Made with 💪 - Demonstrates procedural generation, algorithm design, and clean architecture patterns.
