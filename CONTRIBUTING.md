# Contributing to PuzzleForge

Thank you for your interest in contributing! This document explains how to contribute to the project.

## 📋 Table of Contents

1. [Before You Start](#before-you-start)
2. [Development Environment](#development-environment)
3. [Code Standards](#code-standards)
4. [Git Workflow](#git-workflow)
5. [Pull Request Process](#pull-request-process)

---

## Before You Start

### Check Existing Issues
- Check if a similar issue already exists
- If it does, comment on that issue
- If you're proposing a new feature, open an issue first to discuss

### Understand the Project Structure
- Review the folder structure in `Assets/_scripts/Runtime/`
- Read `README.md` completely
- Run the game to understand how it works

---

## Development Environment

### Requirements
- **Unity:** 2022.3 LTS or newer
- **Visual Studio:** 2022 or JetBrains Rider
- **Git:** Accessible from command line
- **GitHub Account:** For fork and PR

### Setup

```bash
# 1. Fork the repo (on GitHub)
# 2. Clone locally
git clone https://github.com/your-username/puzzleforge.git
cd puzzleforge

# 3. Create a new branch
git checkout -b feature/your-feature-name

# 4. Open in Unity
# - Unity Hub → Add Project → Select this directory
# - Wait (~2-3 minutes for first import)
```

### Editor Configuration

**We recommend Visual Studio 2022:**
1. Edit → Preferences → External Tools
2. Select Visual Studio 2022
3. Tools → Generate .csproj files

**If using Rider:**
1. Tools → Generate Visual Studio project files
2. Rider will auto-load

---

## Code Standards

### Naming Conventions

```csharp
// ✅ CORRECT
public interface IColorPalette { }
public class ColorPaletteService : IColorPalette { }
private int _cachedValue;
public void ProcessShape() { }

// ❌ WRONG
public interface ColorPalette { }  // Interface must start with I
public class colorPaletteService { }  // Use PascalCase
private int cachedValue;  // Private must start with _
public void process_shape() { }  // Don't use snake_case
```

### Class Size Limit

**Rule: No class should exceed 250 lines**

```csharp
// ❌ WRONG - Too large
public class LargeProcessor
{
    // 300+ lines
    // Should be split
}

// ✅ CORRECT - Split responsibilities
public class ShapeValidator { }
public class ShapeProcessor { }
public class ShapeOptimizer { }
```

### XML Documentation

**Comment every public method:**

```csharp
/// <summary>
/// Generates grid with specified dimensions.
/// </summary>
/// <param name="width">Grid width (4-6)</param>
/// <param name="height">Grid height (4-6)</param>
/// <returns>Generated Grid object</returns>
/// <exception cref="ArgumentException">
/// Throws if dimensions are outside valid range
/// </exception>
public Grid CreateGrid(int width, int height)
{
    // Implementation
}
```

### Access Modifiers

```csharp
// ✅ Default to private
private int _value;
private void HelperMethod() { }

// Only public when necessary
public int Value { get; private set; }
public void PublicMethod() { }

// Interfaces should be public
public interface IMyInterface { }
```

### SOLID Principles

1. **Single Responsibility** - One class, one job
2. **Open/Closed** - Open for extension, closed for modification
3. **Liskov Substitution** - Subclasses can substitute parents
4. **Interface Segregation** - Split large interfaces
5. **Dependency Inversion** - Depend on abstractions, not concretions

---

## Git Workflow

### Branch Naming

```bash
# Feature development
git checkout -b feature/add-undo-system
git checkout -b feature/improve-snap-algorithm

# Bug fix
git checkout -b fix/snap-misalignment
git checkout -b fix/color-palette-crash

# Documentation
git checkout -b docs/algorithm-explanation
git checkout -b docs/api-reference

# Performance
git checkout -b perf/optimize-grid-lookup
git checkout -b perf/reduce-memory-usage
```

### Commit Messages

**Format:**
```
<type>(<scope>): <subject>

<body>

<footer>
```

**Types:**
- `feat:` New feature
- `fix:` Bug fix
- `docs:` Documentation
- `style:` Formatting (no logic change)
- `refactor:` Code restructuring
- `perf:` Performance improvement
- `test:` Add tests
- `chore:` Build, dependencies, etc.

**Examples:**
```bash
git commit -m "feat(generation): add multi-seed algorithm"
git commit -m "fix(snapping): resolve misalignment on edge cases"
git commit -m "docs(readme): clarify level loading process"
git commit -m "perf(grid): optimize neighbor lookup to O(1)"
```

### Commit Best Practices

```bash
# ✅ CORRECT - Small, atomic commits
git commit -m "feat(board): add drag handler"
git commit -m "feat(board): implement snap-to-grid"
git commit -m "test(board): add snap algorithm tests"

# ❌ WRONG - Too large commit
git commit -m "Add drag, snapping, testing, and refactoring"

# ✅ CORRECT - Structured message
git commit -m "feat(colors): shuffle palette per level

- Implement shuffle algorithm
- Add seed parameter for determinism
- Update color distribution logic"

# Open editor for detailed commit
git commit  # Editor opens
```

---

## Pull Request Process

### PR Preparation

1. **Update your branch**
```bash
git fetch origin
git rebase origin/main
```

2. **Test your code**
```bash
# Run in Unity
# Check Console for any errors
# Play through several levels
```

3. **Check linting**
```csharp
// Review C# standards
// Address Analyzer warnings
```

### Opening a PR

1. Go to your fork on GitHub and click "Compare & pull request"
2. **Write PR title:**
```
[Feature/Fix] Brief description of what this PR does
```

3. **Write PR description:**
```markdown
## Description
What does this PR do? (2-3 sentences)

## Changes
- Change 1
- Change 2
- Change 3

## Related Issue
Closes #123

## Testing
How did you test this?
- [ ] Tested with Easy difficulty
- [ ] Tested with Hard difficulty
- [ ] No new console errors

## Checklist
- [ ] Code follows style guidelines
- [ ] No new warnings
- [ ] Documentation updated
- [ ] Classes under 250 lines
```

### Review Process

**Maintainers check:**
- ✅ Code quality and standards
- ✅ Performance implications
- ✅ Architecture consistency
- ✅ Testing sufficiency
- ✅ Documentation clarity

**When you receive feedback:**
1. Read all comments
2. Make necessary changes
3. Commit and push (don't rebase!)
4. Leave a comment: "Ready for re-review"

---

## Special Development Areas

### 1. Algorithm Development

Want to improve shape generation?

**Files:**
- `Assets/_scripts/Runtime/Generation/Shapes/ShapeGenerator.cs`
- `Assets/_scripts/Runtime/Generation/Shapes/NeighborSelector.cs`

**Testing Process:**
1. Set `LevelManager.specificLevelId = "level-9"`
2. Make your algorithm change
3. Run 10 times, verify different results each time

### 2. Rendering Improvements

The custom `TriangleMeshRenderer` handles all triangle rendering:

**File:** `Assets/_scripts/Runtime/Gameplay/Components/TriangleMeshRenderer.cs`
**Shader:** `Assets/Shaders/SimpleTriangleColor.shader`

**Features:**
- MaterialPropertyBlock for efficient updates
- Vertex colors for live editing
- Editor real-time updates
- No external asset dependencies

### 3. UI Enhancement

Want to add UI features?

**File:** `Assets/_scripts/Runtime/Ui/LevelCompleteUI.cs`

**Ideas:**
- [ ] Level select screen
- [ ] Difficulty selector
- [ ] Score/timer display
- [ ] Hint system

### 4. Performance Optimization

To improve performance:

1. Open Profiler (Window → Analysis → Profiler)
2. Measure Memory and CPU
3. Find bottleneck
4. Optimize and benchmark

---

## Common Mistakes and Solutions

### ❌ "Code isn't working but I don't know why"

```csharp
// WRONG - No debug info
if (result == null)
    return false;

// CORRECT - Add debug info
if (result == null) {
    Debug.LogError("Shape generation failed: result is null");
    return false;
}
```

### ❌ "Class got too large"

```csharp
// WRONG - 400+ lines
public class ShapeGenerator
{
    // Everything here
}

// CORRECT - Split responsibilities
public class ShapeGenerator { }   // Seed selection
public class ShapeGrower { }      // Growth algorithm
public class ShapeFinalizer { }   // Grid completion
```

### ❌ "Algorithm isn't deterministic"

```csharp
// WRONG - No control over randomness
Random.Range(0, 10)

// CORRECT - Use seed (testable)
System.Random seededRandom = new System.Random(seed);
seededRandom.Next(0, 10);
```

---

## Questions?

- **Report Issue:** Use GitHub Issues
- **Discuss:** Use GitHub Discussions
- **Detailed:** Open PR and discuss there

---

**Happy coding! 🚀**
