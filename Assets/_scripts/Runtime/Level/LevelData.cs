using UnityEngine;
using System.Collections.Generic;

namespace Yunus.Game.Level
{
    /// <summary>
    /// Single level configuration.
    /// Simple, minimal, server-friendly.
    /// </summary>
    [System.Serializable]
    public class LevelData
{
    public string levelId;           // "level-1", "level-2", etc.
    public string difficulty;        // "easy", "medium", "hard"

    public int gridWidth;            // 4-6
    public int gridHeight;           // 4-6
    public int shapeCount;           // 5-12
    public int minTrianglesPerBox;   // 2-4
    public int seedMinCellDistance;  // 1-3 (seed constraint)

    /// <summary>
    /// Validate config meets case requirements.
    /// </summary>
    public bool IsValid()
    {
        if (gridWidth < 4 || gridWidth > 6) return false;
        if (gridHeight < 4 || gridHeight > 6) return false;
        if (shapeCount < 5 || shapeCount > 12) return false;
        if (minTrianglesPerBox < 2 || minTrianglesPerBox > 4) return false;
        if (seedMinCellDistance < 1 || seedMinCellDistance > 3) return false;
        if (string.IsNullOrEmpty(levelId)) return false;

        return true;
    }

    public override string ToString()
    {
        return $"{levelId} [{difficulty}] {gridWidth}×{gridHeight}, {shapeCount} shapes, min:{minTrianglesPerBox}, seedDist:{seedMinCellDistance}";
    }
}

/// <summary>
/// Container for multiple levels in single JSON.
/// Server downloads this, not individual level files.
/// </summary>
[System.Serializable]
public class LevelsContainer
{
    public List<LevelData> levels;

    /// <summary>
    /// Get level by ID.
    /// </summary>
    public LevelData GetLevel(string levelId)
    {
        if (levels == null) return null;
        return levels.Find(l => l.levelId == levelId);
    }

    /// <summary>
    /// Get random level by difficulty.
    /// </summary>
    public LevelData GetRandomLevel(string difficulty)
    {
        if (levels == null) return null;

        var matching = levels.FindAll(l => l.difficulty == difficulty);
        if (matching.Count == 0) return null;

        return matching[Random.Range(0, matching.Count)];
    }

    /// <summary>
    /// Export to JSON.
    /// </summary>
    public string ToJson()
    {
        return JsonUtility.ToJson(this, true);
    }

    /// <summary>
    /// Import from JSON.
    /// </summary>
    public static LevelsContainer FromJson(string json)
    {
        return JsonUtility.FromJson<LevelsContainer>(json);
    }
    }
}