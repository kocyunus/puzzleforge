using UnityEngine;
using UnityEngine.UI;
using Yunus.Game.Level;

namespace Yunus.Game.UI
{
    /// <summary>
    /// Level complete screen UI.
    /// Shows when puzzle is solved, allows loading next level.
    /// </summary>
    public class LevelCompleteUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private LevelManager levelManager;
    [SerializeField] private GameObject panel;
    [SerializeField] private Button nextLevelButton;

    void Awake()
    {
        // Panel ba�ta kapal�
        if (panel != null)
            panel.SetActive(false);

        // Button'a listener ekle
        if (nextLevelButton != null)
            nextLevelButton.onClick.AddListener(OnNextLevelClicked);
    }

    /// <summary>
    /// Show level complete panel.
    /// </summary>
    public void Show()
    {
        if (panel != null)
            panel.SetActive(true);


        Debug.Log("[LevelCompleteUI] Panel shown");
    }

    /// <summary>
    /// Hide panel.
    /// </summary>
    public void Hide()
    {
        if (panel != null)
            panel.SetActive(false);
    }

    void OnNextLevelClicked()
    {
        Debug.Log("[LevelCompleteUI] Next Level clicked");

        Hide();

        if (levelManager != null)
        {
            levelManager.LoadNextLevel();
        }
        else
        {
            Debug.LogError("[LevelCompleteUI] LevelManager reference missing!");
        }
    }

    void OnDestroy()
    {
        // Cleanup listener
        if (nextLevelButton != null)
            nextLevelButton.onClick.RemoveListener(OnNextLevelClicked);
    }
    }
}