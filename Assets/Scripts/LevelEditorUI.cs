using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HexaAway.Core
{
    /// <summary>
    /// Handles UI specific interactions for the level editor
    /// </summary>
    public class LevelEditorUI : MonoBehaviour
    {
        [Header("Main Components")]
        [SerializeField] private LevelEditorManager editorManager;
        [SerializeField] private GameObject editorPanel;
        [SerializeField] private Button toggleEditorButton;
        
        [Header("Tab Navigation")]
        [SerializeField] private GameObject[] tabPanels;
        [SerializeField] private Button[] tabButtons;
        
        [Header("Direction Selection")]
        [SerializeField] private Button[] directionButtons;
        [SerializeField] private GameObject directionIndicator;
        
        [Header("Help Panel")]
        [SerializeField] private GameObject helpPanel;
        [SerializeField] private Button helpButton;
        [SerializeField] private Button closeHelpButton;
        
        private int currentTabIndex = 0;
        
        private void Awake()
        {
            // Find editor manager if not assigned
            if (editorManager == null)
            {
                editorManager = FindObjectOfType<LevelEditorManager>();
            }
            
            // Setup toggle button
            if (toggleEditorButton != null)
            {
                toggleEditorButton.onClick.AddListener(ToggleEditorPanel);
            }
            
            // Setup tab buttons
            if (tabButtons != null)
            {
                for (int i = 0; i < tabButtons.Length; i++)
                {
                    int tabIndex = i; // Copy for closure
                    tabButtons[i].onClick.AddListener(() => SwitchToTab(tabIndex));
                }
            }
            
            // Setup help panel
            if (helpButton != null)
            {
                helpButton.onClick.AddListener(ToggleHelpPanel);
            }
            
            if (closeHelpButton != null)
            {
                closeHelpButton.onClick.AddListener(CloseHelpPanel);
            }
            
            // Setup direction buttons
            SetupDirectionButtons();
            
            // Initial state
            if (editorPanel != null)
                editorPanel.SetActive(false);
                
            if (helpPanel != null)
                helpPanel.SetActive(false);
                
            // Show first tab
            SwitchToTab(0);
        }
        
        private void SetupDirectionButtons()
        {
            if (directionButtons == null || directionButtons.Length != 6)
                return;
                
            // Create visual indicators for each direction
            string[] directionLabels = new string[] { "E", "SE", "SW", "W", "NW", "NE" };
            
            for (int i = 0; i < directionButtons.Length; i++)
            {
                // Create label if it doesn't exist
                TextMeshProUGUI buttonText = directionButtons[i].GetComponentInChildren<TextMeshProUGUI>();
                if (buttonText == null)
                {
                    // Create a text component
                    GameObject textObj = new GameObject("Text");
                    textObj.transform.SetParent(directionButtons[i].transform);
                    buttonText = textObj.AddComponent<TextMeshProUGUI>();
                    
                    // Position the text
                    RectTransform textRect = textObj.GetComponent<RectTransform>();
                    textRect.anchorMin = Vector2.zero;
                    textRect.anchorMax = Vector2.one;
                    textRect.offsetMin = Vector2.zero;
                    textRect.offsetMax = Vector2.zero;
                }
                
                // Set direction label
                buttonText.text = directionLabels[i];
                buttonText.alignment = TextAlignmentOptions.Center;
                buttonText.fontSize = 16;
                
                // Add click event
                int dirIndex = i; // Copy for closure
                directionButtons[i].onClick.AddListener(() => SelectDirection(dirIndex));
            }
            
            // Initially select East direction
            SelectDirection(0);
        }
        
        public void ToggleEditorPanel()
        {
            if (editorPanel != null)
            {
                bool isActive = !editorPanel.activeSelf;
                editorPanel.SetActive(isActive);
                
                // Toggle editor mode in the manager
                if (editorManager != null)
                {
                    // This method should toggle the editor mode in the manager
                    editorManager.ToggleEditorMode();
                }
                
                // Update button text if needed
                TextMeshProUGUI buttonText = toggleEditorButton.GetComponentInChildren<TextMeshProUGUI>();
                if (buttonText != null)
                {
                    buttonText.text = isActive ? "Close Editor" : "Open Editor";
                }
            }
        }
        
        public void SwitchToTab(int tabIndex)
        {
            if (tabPanels == null || tabIndex < 0 || tabIndex >= tabPanels.Length)
                return;
                
            // Hide all panels
            for (int i = 0; i < tabPanels.Length; i++)
            {
                if (tabPanels[i] != null)
                {
                    tabPanels[i].SetActive(i == tabIndex);
                }
                
                // Update button visuals
                if (tabButtons != null && i < tabButtons.Length)
                {
                    ColorBlock colors = tabButtons[i].colors;
                    if (i == tabIndex)
                    {
                        // Selected tab
                        colors.normalColor = new Color(0.8f, 0.8f, 1f);
                        tabButtons[i].transform.localScale = new Vector3(1.1f, 1.1f, 1.1f);
                    }
                    else
                    {
                        // Unselected tab
                        colors.normalColor = Color.white;
                        tabButtons[i].transform.localScale = Vector3.one;
                    }
                    tabButtons[i].colors = colors;
                }
            }
            
            currentTabIndex = tabIndex;
        }
        
        public void SelectDirection(int directionIndex)
        {
            if (directionIndex < 0 || directionIndex >= 6)
                return;
                
            // Update UI
            for (int i = 0; i < directionButtons.Length; i++)
            {
                if (directionButtons[i] != null)
                {
                    // Highlight the selected button
                    ColorBlock colors = directionButtons[i].colors;
                    if (i == directionIndex)
                    {
                        colors.normalColor = new Color(0.8f, 0.8f, 1f);
                        directionButtons[i].transform.localScale = new Vector3(1.1f, 1.1f, 1.1f);
                        
                        // Position the indicator if available
                        if (directionIndicator != null)
                        {
                            directionIndicator.transform.SetParent(directionButtons[i].transform);
                            directionIndicator.transform.localPosition = Vector3.zero;
                            directionIndicator.SetActive(true);
                        }
                    }
                    else
                    {
                        colors.normalColor = Color.white;
                        directionButtons[i].transform.localScale = Vector3.one;
                    }
                    directionButtons[i].colors = colors;
                }
            }
            
            // Tell the editor manager to use this direction
            if (editorManager != null)
            {
                // Call a public method on the editor manager
                // This would need to be implemented in LevelEditorManager
                var setDirectionMethod = typeof(LevelEditorManager).GetMethod("SetDirection");
                if (setDirectionMethod != null)
                {
                    setDirectionMethod.Invoke(editorManager, new object[] { (HexDirection)directionIndex });
                }
            }
        }
        
        public void ToggleHelpPanel()
        {
            if (helpPanel != null)
            {
                helpPanel.SetActive(!helpPanel.activeSelf);
            }
        }
        
        public void CloseHelpPanel()
        {
            if (helpPanel != null)
            {
                helpPanel.SetActive(false);
            }
        }
        
        public void ShowConfirmationDialog(string message, System.Action onConfirm)
        {
            // This could be implemented with a UI dialog panel
            if (UnityEngine.Application.isEditor)
            {
                // In editor, we can use EditorUtility
#if UNITY_EDITOR
                bool confirm = UnityEditor.EditorUtility.DisplayDialog("Confirm", message, "Yes", "No");
                if (confirm && onConfirm != null)
                {
                    onConfirm.Invoke();
                }
#else
                // Simple fallback for builds - could be replaced with a proper UI dialog
                if (onConfirm != null)
                {
                    onConfirm.Invoke();
                }
#endif
            }
            else
            {
                // In a build, this should create a UI dialog
                // For now, just confirm automatically
                if (onConfirm != null)
                {
                    onConfirm.Invoke();
                }
            }
        }
        
        // Additional UI helper methods can be added here
    }
}