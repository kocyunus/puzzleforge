using UnityEngine;
using System.Collections;
using UnityEngine.Networking;
using Yunus.Game.Board;

namespace Yunus.Game.Level
{
    /// <summary>
    /// Manages level loading from JSON (local or server).
    /// Orchestrates LevelGenerator and PuzzleBoard.
    /// </summary>
    public class LevelManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private LevelGenerator levelGenerator;
    [SerializeField] private PuzzleBoard puzzleBoard;

    [Header("JSON Source")]
    [SerializeField] private TextAsset localJsonFile;  // Local fallback
    [SerializeField] private bool downloadFromServer = false;
    [SerializeField] private string serverUrl = "https://drive.google.com/uc?export=download&id=YOUR_FILE_ID";

    [Header("Level Selection")]
    [SerializeField] private string selectedDifficulty = "medium";  // easy, medium, hard
    [SerializeField] private string specificLevelId = "";  // If empty, selects random

    private LevelsContainer levelsContainer;
    private LevelData currentLevel;

    void Start()
    {
        StartCoroutine(InitializeGame());
    }

    IEnumerator InitializeGame()
    {
        Debug.Log("[LevelManager] === GAME START ===");

        // 1. Load levels JSON
        yield return LoadLevelsJson();

        if (levelsContainer == null || levelsContainer.levels == null || levelsContainer.levels.Count == 0)
        {
            Debug.LogError("[LevelManager] ❌ No levels loaded! Cannot start game.");
            yield break;
        }

        // 2. Select level
        SelectLevel();

        if (currentLevel == null)
        {
            Debug.LogError("[LevelManager] ❌ No valid level selected!");
            yield break;
        }

        // 3. Initialize game
        InitializeLevel();

        Debug.Log("[LevelManager] ✅ Game initialized successfully!");
    }

    IEnumerator LoadLevelsJson()
    {
        if (downloadFromServer)
        {
            Debug.Log($"[LevelManager] Downloading from server: {serverUrl}");
            yield return DownloadFromServer();
        }
        else
        {
            Debug.Log("[LevelManager] Loading from local file...");
            LoadFromLocal();
        }
    }

    void LoadFromLocal()
    {
        if (localJsonFile == null)
        {
            Debug.LogError("[LevelManager] ❌ Local JSON file not assigned!");
            return;
        }

        string json = localJsonFile.text;
        levelsContainer = LevelsContainer.FromJson(json);

        if (levelsContainer != null && levelsContainer.levels != null)
        {
            Debug.Log($"[LevelManager] ✅ Loaded {levelsContainer.levels.Count} levels from local file");
        }
        else
        {
            Debug.LogError("[LevelManager] ❌ Failed to parse local JSON!");
        }
    }

    IEnumerator DownloadFromServer()
    {
        using (UnityWebRequest request = UnityWebRequest.Get(serverUrl))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string json = request.downloadHandler.text;
                levelsContainer = LevelsContainer.FromJson(json);

                if (levelsContainer != null && levelsContainer.levels != null)
                {
                    Debug.Log($"[LevelManager] ✅ Downloaded {levelsContainer.levels.Count} levels from server");
                }
                else
                {
                    Debug.LogError("[LevelManager] ❌ Failed to parse downloaded JSON!");
                    LoadFromLocal();  // Fallback to local
                }
            }
            else
            {
                Debug.LogError($"[LevelManager] ❌ Download failed: {request.error}");
                LoadFromLocal();  // Fallback to local
            }
        }
    }

    void SelectLevel()
    {
        if (!string.IsNullOrEmpty(specificLevelId))
        {
            // Specific level by ID
            currentLevel = levelsContainer.GetLevel(specificLevelId);
            Debug.Log($"[LevelManager] Selected specific level: {specificLevelId}");
        }
        else
        {
            // Random level by difficulty
            currentLevel = levelsContainer.GetRandomLevel(selectedDifficulty);
            Debug.Log($"[LevelManager] Selected random {selectedDifficulty} level");
        }

        if (currentLevel != null)
        {
            Debug.Log($"[LevelManager] 🎮 LEVEL: {currentLevel}");
        }
    }

    void InitializeLevel()
    {
        // 1. Initialize LevelGenerator
        if (levelGenerator != null)
        {
            levelGenerator.Initialize(currentLevel);
            levelGenerator.GenerateLevel();
        }
        else
        {
            Debug.LogError("[LevelManager] ❌ LevelGenerator reference missing!");
        }

        // 2. Initialize PuzzleBoard
        if (puzzleBoard != null)
        {
            puzzleBoard.Initialize(currentLevel);
        }
        else
        {
            Debug.LogError("[LevelManager] ❌ PuzzleBoard reference missing!");
        }
    }

    /// <summary>
    /// Load next random level.
    /// </summary>
    public void LoadNextLevel()
    {
        Debug.Log("[LevelManager] Loading next level...");
        // Load new level
        StartCoroutine(InitializeGame());
    }

    public void LoadSpecificLevel(string levelId)
    {
        specificLevelId = levelId;
        StartCoroutine(InitializeGame());
    }
    }
}