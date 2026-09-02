# PuzzleForge 🧩

**Procedurally generated puzzle games engine** - Guaranteed-solvable, always different.

A compact Unity puzzle project demonstrating procedural generation, a service-locator architecture, object pooling, and a dependency-free triangle mesh renderer. It is a playable vertical slice and a reference implementation for grid-based puzzle generation — not a shipping product (no audio, minimal UI; see [Known Limitations](#-known-limitations)).

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
2. **Drag the Shape** - Move it over the grey puzzle board
3. **Place & Snap** - Release near the matching slots to snap into position
4. **Complete Level** - Place all shapes (they tile the board exactly) to win

---

## ⚙️ Level Selection & Difficulty

Difficulty and level choice live on the **LevelManager** object's Inspector:

1. In the scene hierarchy select the **LevelManager** object
2. Set **Selected Difficulty** to `easy`, `medium`, or `hard` — on Play a random level of that tier is generated
3. Optional: set **Specific Level Id** (e.g. `level-3`, `level-7`) to always load that exact level instead

Level ids are `level-1` … `level-9`, three per difficulty (see `Assets/levels.json`).


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
    System.Random rng,          // tie-breaks among equally-valid adjacent cells
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

**Problem:** the player drops a dragged shape; it must snap onto the board only when it
genuinely lines up with free, matching slots.

**Solution:** `SnapUtil.TrySnapToBoard` — an all-or-nothing gate, then greedy nearest-first
matching. Called from `DragTriangleParentInput.EndDrag`:

```csharp
public static bool TrySnapToBoard(
    Transform dragRoot, List<Triangle> shapeTriangles,
    Transform boardRoot, List<Triangle> boardTriangles,
    Transform shapeParent, bool setFlags = true, bool setSlotFlags = true)
{
    // GATE: every shape triangle must have a same-posIndex board slot within
    //       SNAP_DIST_WORLD_XY (5 world units; triangle spacing is 10). Any miss -> abort.
    if (!GateAllWithinThreshold(shapeTriangles, boardTriangles)) return false;

    // Collect all (shapeTri, boardTri) pairs within the threshold, nearest first
    var pairs = CollectPairsWithinThreshold(shapeTriangles, boardTriangles);   // sorted by dist

    // Greedy assignment: take the closest pair whose shape tri and board slot are both free
    var assigned = AssignGreedy(pairs);                       // Dictionary<shapeTri, boardTri>
    if (assigned.Count == 0) return false;

    // Reject if any target slot is already occupied (tri.isSnapped)
    if (!AllAssignedSlotsFree(assigned)) return false;

    // Commit: move each shape triangle onto its slot's world XY, record the slots on
    // ShapeData, and flag both sides as snapped
    ApplyWorldSnap(boardRoot, assigned);
    foreach (var (shapeTri, slotTri) in assigned)
    {
        shapeParent.GetComponent<ShapeData>().RegisterBoardTriangle(slotTri);
        shapeTri.SnapState(true);
        slotTri.SnapState(true);
    }
    return true;
}
```

On success `PuzzleBoard.OnShapePlaced()` bumps `placedPieceCount`; the level completes when
`placedPieceCount >= shapeCount`. Because the generated shapes are a perfect partition of a
board-identical grid, placing every shape fills the board exactly — which is why the level is
**solvable by construction**.

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
1. Create a palette via **Assets ▸ Create ▸ Game ▸ Color Palette** (or duplicate the existing
   `ColorPaletteSO.asset` in `Assets/_scripts/Runtime/Data/Configs/Palettes/`) and fill its
   `colors` array
2. Assign it to the **Color Palette** field on the **GameBootstrap** object
3. At startup `GameBootstrap` converts it to `Rgba[]` and registers a `DistinctColorPalette`
   on the Service Locator, so every generated level uses it

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

`LevelManager` reads levels on `Start()` via a coroutine. Everything is configured through
its Inspector fields (they are `[SerializeField] private`, not a public API):

| Field | Effect |
|-------|--------|
| **Local Json File** | `TextAsset` for `levels.json` — used by default, and as the fallback |
| **Download From Server** / **Server Url** | when enabled, `UnityWebRequest.Get` the URL and parse it; on any failure it falls back to **Local Json File** |
| **Selected Difficulty** | `easy` / `medium` / `hard` — picks a random matching level |
| **Specific Level Id** | non-empty overrides the random pick with that exact level |

`LevelManager.LoadNextLevel()` / `LoadSpecificLevel(id)` re-run the whole flow at runtime
(the *Next Level* button calls the former).

---

## 📚 Patterns & Architecture Decisions

### Service Locator (Global Registry)
```csharp
// Register (GameBootstrap does this on Awake)
ServiceLocator.Register<IColorPalette>(new DistinctColorPalette(rgba));

// Retrieve anywhere
if (ServiceLocator.TryGet<IColorPalette>(out var palette)) { /* ... */ }
```
Registered services: `IColorPalette` (`DistinctColorPalette`), `IPrefabPooler`
(`PrefabPoolerService`), `IShapeScatter` (`ShapeScatterService`).

**Trade-off:** Convenient global access vs hidden dependencies.
**When to use:** For singleton-like services (pools, managers, config).

### Object Pooling (Reuse, Don't Recreate)
```csharp
// One handle per prefab; prewarms `prewarmCount` inactive instances
var trianglePool = pooler.CreatePool(trianglePrefab, prewarmCount: gridW * gridH * 4);

var tri = trianglePool.SpawnImmediate(pos, rot, parent);  // reuses an idle instance
trianglePool.Despawn(tri);                                // back to the pool, deactivated
```

**Benefit:** no `Instantiate`/`Destroy` churn once the pool is warm.

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

## 🧪 What the Tests Assert

Patterns used by `SeedCellSelectorTests` (EditMode) and `ShapeGenerationTests` (PlayMode):

### Reproducibility
Generation *topology* draws only from the `System.Random` passed to `ShapeGenerator` — seed
placement, `MovesPerTurn`, and neighbour tie-breaks all use it, and nothing uses
`UnityEngine.Random`. Same seed ⇒ identical per-triangle ownership.

```csharp
var gen = new ShapeGenerator(grid, shapePool, root, palette, minPerBox,
                             rng: new System.Random(42));
gen.GenerateShapes();
// grid.AllTriangles.Select(t => t.ownerShapeIndex) is byte-for-byte equal on a rerun
```

(Shape *colour* order still comes from `IColorPalette.Shuffle` on `UnityEngine.Random`; it's
cosmetic — see [Known Limitations](#-known-limitations).)

### Solvability (by construction)
A level is solvable because the generated shapes are a **disjoint partition of a grid identical
to the board** — assert that, not a snap search:

```csharp
var owned = gen.Shapes.SelectMany(s => s.OccupiedTriangles).ToList();
Assert.AreEqual(grid.AllTriangles.Count, owned.Count);        // covers every triangle
Assert.AreEqual(owned.Count, owned.Distinct().Count());       // no triangle shared
```

### Coverage
```csharp
Assert.IsFalse(grid.AllTriangles.Any(t => t.ownerShapeIndex < 0));   // 100% claimed
```

---

## 📖 How to Extend

### Add New Neighbor Selection Rule
1. Add method to `NeighborSelector` (e.g., `TryPickFromDiagonal()`)
2. Insert in the `TryPickNext` priority chain before/after existing priorities
3. Run the PlayMode `ShapeGenerationTests` to confirm 100% coverage and a clean
   disjoint partition still hold for every level config

### Add New Difficulty Tier
1. Add entries to `levels.json` with a new `difficulty` string (keep params inside
   `LevelData.IsValid()`'s ranges)
2. Set **LevelManager ▸ Selected Difficulty** to that string (`GetRandomLevel` filters by it)
3. Tune `minTrianglesPerBox` / `seedMinCellDistance` for the shape feel you want

### Implement Async Level Loading
1. Move `GenerateLevel()` to coroutine
2. Yield between major phases (seed picking → growing → coloring)
3. Update UI progress bar

---

## 🐛 Known Limitations

- **UI Minimal:** only a level-complete panel — no menu, HUD, or difficulty picker
- **No Audio:** no music or effects
- **No move history:** placed shapes can be picked up and re-placed, but there is no undo stack
- **Single Player:** no multiplayer
- **Legacy input:** mouse-only, via the old `Input` manager (not the new Input System)
- **Colour ordering not seeded:** generation topology is fully reproducible from the injected
  `System.Random`, but `IColorPalette.Shuffle` still uses `UnityEngine.Random`, so which shape
  gets which colour varies run to run (cosmetic only)

---

## 📄 License

MIT License - See LICENSE file

---

## 👨‍💻 Development

### Code Examples
- Seed placement in `SeedCellSelector.cs`; growth + coverage in `ShapeGenerator.cs`
- Entry/exit door logic in `NeighborSelector.cs`
- Custom rendering in `TriangleMeshRenderer.cs`
- XML docs on the generation, level, board and UI classes

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
