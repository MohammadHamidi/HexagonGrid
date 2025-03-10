using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace HexaAway.Core
{
    public class UIManager : MonoBehaviour
    {
        [Header("Level Info")]
        [SerializeField] private TextMeshProUGUI levelTitleText;
        [SerializeField] private TextMeshProUGUI movesText;
        
        [Header("Panels")]
        [SerializeField] private GameObject levelCompletedPanel;
        [SerializeField] private GameObject levelFailedPanel;
        
        [Header("Buttons")]
        [SerializeField] private Button restartButton;
        [SerializeField] private Button nextLevelButton;
        
        private void Awake()
        {
            // Hide completion panels
            if (levelCompletedPanel != null)
                levelCompletedPanel.SetActive(false);
                
            if (levelFailedPanel != null)
                levelFailedPanel.SetActive(false);
                
            // Setup button listeners
            if (restartButton != null)
                restartButton.onClick.AddListener(OnRestartButtonClicked);
                
            if (nextLevelButton != null)
                nextLevelButton.onClick.AddListener(OnNextLevelButtonClicked);
        }
        
        private void Start()
        {
            // Subscribe to game manager events
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnMovesUpdated += UpdateMovesText;
                GameManager.Instance.OnLevelCompleted += ShowLevelCompletedPanel;
                GameManager.Instance.OnLevelFailed += ShowLevelFailedPanel;
            }
        }
        
        private void OnDestroy()
        {
            // Unsubscribe from events
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnMovesUpdated -= UpdateMovesText;
                GameManager.Instance.OnLevelCompleted -= ShowLevelCompletedPanel;
                GameManager.Instance.OnLevelFailed -= ShowLevelFailedPanel;
            }
        }
        
        private void UpdateMovesText(int current, int total)
        {
            if (movesText != null)
            {
                movesText.text = $"{total - current} Moves";
            }
        }
        
        private void ShowLevelCompletedPanel()
        {
            if (levelCompletedPanel != null)
            {
                levelCompletedPanel.SetActive(true);
            }
            
            // You could also play a sound, animation, etc.
            Debug.Log("Level completed!");
        }
        
        private void ShowLevelFailedPanel()
        {
            if (levelFailedPanel != null)
            {
                levelFailedPanel.SetActive(true);
            }
            
            Debug.Log("Level failed!");
        }
        
        private void OnRestartButtonClicked()
        {
            // Hide panels
            if (levelCompletedPanel != null)
                levelCompletedPanel.SetActive(false);
                
            if (levelFailedPanel != null)
                levelFailedPanel.SetActive(false);
                
            // Restart level
            if (GameManager.Instance != null)
            {
                GameManager.Instance.RestartLevel();
            }
        }
        
        private void OnNextLevelButtonClicked()
        {
            // Hide panels
            if (levelCompletedPanel != null)
                levelCompletedPanel.SetActive(false);
                
            // Load next level
            if (GameManager.Instance != null)
            {
                GameManager.Instance.LoadNextLevel();
            }
        }
        
        // Set level info at the start of a level
        public void SetLevelInfo(string levelName, int moveLimit)
        {
            if (levelTitleText != null)
            {
                levelTitleText.text = levelName;
            }
            
            if (movesText != null)
            {
                movesText.text = $"{moveLimit} Moves";
            }
        }
    }
}