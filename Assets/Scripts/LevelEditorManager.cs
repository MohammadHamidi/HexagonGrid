using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

using UnityEngine;
using UnityEngine.UI;
using TMPro;



namespace HexaAway.Core
{
    /// <summary>
    /// Runtime level editor manager for in-game level creation
    /// </summary>
    public class LevelEditorManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GridManager gridManager;
        [SerializeField] private GameObject hexagonPrefab;
        [SerializeField] private Transform gridContainer;
        
        [Header("UI Components")]
        [SerializeField] private TMP_InputField levelNameInput;
        [SerializeField] private TMP_InputField levelNumberInput;
        [SerializeField] private TMP_InputField moveLimitInput;
        [SerializeField] private TMP_InputField targetHexagonsInput;
        [SerializeField] private Slider gridWidthSlider;
        [SerializeField] private Slider gridHeightSlider;
        [SerializeField] private TextMeshProUGUI gridSizeText;
        [SerializeField] private Button[] directionButtons;
        [SerializeField] private Image[] colorButtons;
        [SerializeField] private Button saveButton;
        [SerializeField] private Button loadButton;
        [SerializeField] private Button clearButton;
        [SerializeField] private GameObject editorPanel;
        [SerializeField] private GameObject messagePanel;
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private TextMeshProUGUI statsText;
        
        [Header("Editor Settings")]
        [SerializeField] private GridConfig gridConfig;
        [SerializeField] private Color[] defaultColorPalette = new Color[3] 
        { 
            new Color(0.2f, 0.6f, 1f),  // Blue
            new Color(0.5f, 0.9f, 0.3f), // Green
            new Color(1f, 0.4f, 0.4f)    // Red
        };
        
        // Editor state
        private List<Color> colorPalette = new List<Color>();
        private Dictionary<Vector2Int, HexagonEditorData> placedHexagons = new Dictionary<Vector2Int, HexagonEditorData>();
        private HexDirection currentDirection = HexDirection.East;
        private int selectedColorIndex = 0;
        private bool isEditing = false;
        private int gridWidth = 5;
        private int gridHeight = 5;
        private Camera mainCamera;
        private GameObject previewHexagon;
        
        // Level info
        private string levelName = "New Level";
        private int levelNumber = 1;
        private int moveLimit = 15;
        private int targetHexagonsToRemove = 3;
        
        private float raycastDistance = 100f;
        private LayerMask hexCellLayer;
        
        private void Awake()
        {
            mainCamera = Camera.main;
            
            // Initialize color palette
            colorPalette.Clear();
            foreach (Color color in defaultColorPalette)
            {
                colorPalette.Add(color);
            }
            
            // Set default layer mask for raycasting
            hexCellLayer = LayerMask.GetMask("Default"); // Change to your specific layer if needed
            
            // Initialize UI
            InitializeUI();
        }
        
        private void Start()
        {
            // Ensure grid manager is assigned
            if (gridManager == null)
            {
                gridManager = FindObjectOfType<GridManager>();
                if (gridManager == null)
                {
                    Debug.LogError("Grid Manager not found!");
                }
            }
            
            // Create preview hexagon
            previewHexagon = CreatePreviewHexagon();
            previewHexagon.SetActive(false);
            
            // Set initial grid size
            UpdateGridSize(gridWidth, gridHeight);
        }
        
        private void Update()
        {
            if (!isEditing) return;
            
            HandleEditorInput();
            UpdatePreview();
        }
        
        #region UI Initialization
        
        private void InitializeUI()
        {
            // Setup input fields
            if (levelNameInput != null)
            {
                levelNameInput.text = levelName;
                levelNameInput.onEndEdit.AddListener(OnLevelNameChanged);
            }
            
            if (levelNumberInput != null)
            {
                levelNumberInput.text = levelNumber.ToString();
                levelNumberInput.onEndEdit.AddListener(OnLevelNumberChanged);
            }
            
            if (moveLimitInput != null)
            {
                moveLimitInput.text = moveLimit.ToString();
                moveLimitInput.onEndEdit.AddListener(OnMoveLimitChanged);
            }
            
            if (targetHexagonsInput != null)
            {
                targetHexagonsInput.text = targetHexagonsToRemove.ToString();
                targetHexagonsInput.onEndEdit.AddListener(OnTargetHexagonsChanged);
            }
            
            // Setup grid size sliders
            if (gridWidthSlider != null)
            {
                gridWidthSlider.minValue = 3;
                gridWidthSlider.maxValue = 10;
                gridWidthSlider.value = gridWidth;
                gridWidthSlider.onValueChanged.AddListener(OnGridWidthChanged);
            }
            
            if (gridHeightSlider != null)
            {
                gridHeightSlider.minValue = 3;
                gridHeightSlider.maxValue = 10;
                gridHeightSlider.value = gridHeight;
                gridHeightSlider.onValueChanged.AddListener(OnGridHeightChanged);
            }
            
            // Setup direction buttons
            if (directionButtons != null && directionButtons.Length == 6)
            {
                for (int i = 0; i < 6; i++)
                {
                    int dirIndex = i; // Copy for closure
                    directionButtons[i].onClick.AddListener(() => SetDirection((HexDirection)dirIndex));
                }
            }
            
            // Setup color buttons
            if (colorButtons != null)
            {
                for (int i = 0; i < colorButtons.Length && i < colorPalette.Count; i++)
                {
                    colorButtons[i].color = colorPalette[i];
                    int colorIndex = i; // Copy for closure
                    colorButtons[i].GetComponent<Button>().onClick.AddListener(() => SetSelectedColor(colorIndex));
                }
            }
            
            // Setup action buttons
            if (saveButton != null)
                saveButton.onClick.AddListener(SaveLevel);
                
            if (loadButton != null)
                loadButton.onClick.AddListener(LoadLevel);
                
            if (clearButton != null)
                clearButton.onClick.AddListener(ClearAllHexagons);
                
            // Hide message panel initially
            if (messagePanel != null)
                messagePanel.SetActive(false);
                
            // Update stats display
            UpdateStatsDisplay();
        }
        
        #endregion
        
        #region Editor Controls
        
        public void ToggleEditorMode()
        {
            isEditing = !isEditing;
            
            if (editorPanel != null)
                editorPanel.SetActive(isEditing);
                
            if (previewHexagon != null)
                previewHexagon.SetActive(false);
                
            if (isEditing)
            {
                // Refresh the grid if needed
                UpdateGridSize(gridWidth, gridHeight);
            }
        }
        
        private void HandleEditorInput()
        {
            if (Input.GetMouseButtonDown(0))
            {
                // Try to place or remove a hexagon
                Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
                RaycastHit hit;
                
                if (Physics.Raycast(ray, out hit, raycastDistance, hexCellLayer))
                {
                    // Try to get a hex cell from the hit object
                    HexCell cell = hit.collider.GetComponent<HexCell>();
                    
                    if (cell != null)
                    {
                        ToggleHexagonAt(cell);
                    }
                }
            }
        }
        
        private void UpdatePreview()
        {
            if (previewHexagon == null) return;
            
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            
            if (Physics.Raycast(ray, out hit, raycastDistance, hexCellLayer))
            {
                // Try to get a hex cell from the hit object
                HexCell cell = hit.collider.GetComponent<HexCell>();
                
                if (cell != null)
                {
                    // Position the preview
                    previewHexagon.transform.position = cell.transform.position;
                    previewHexagon.SetActive(true);
                    
                    // Check if there's already a hexagon here and adjust preview accordingly
                    if (placedHexagons.ContainsKey(cell.Coordinates))
                    {
                        // Make preview red to indicate removal
                        MeshRenderer renderer = previewHexagon.GetComponent<MeshRenderer>();
                        if (renderer != null)
                        {
                            Material mat = renderer.material;
                            mat.color = new Color(1f, 0.3f, 0.3f, 0.5f);
                        }
                    }
                    else
                    {
                        // Set preview to show current color
                        MeshRenderer renderer = previewHexagon.GetComponent<MeshRenderer>();
                        if (renderer != null && selectedColorIndex < colorPalette.Count)
                        {
                            Material mat = renderer.material;
                            Color previewColor = colorPalette[selectedColorIndex];
                            previewColor.a = 0.5f;
                            mat.color = previewColor;
                        }
                    }
                }
                else
                {
                    previewHexagon.SetActive(false);
                }
            }
            else
            {
                previewHexagon.SetActive(false);
            }
        }
        
        private void ToggleHexagonAt(HexCell cell)
        {
            if (cell == null) return;
            
            if (placedHexagons.ContainsKey(cell.Coordinates))
            {
                // Remove existing hexagon
                RemoveHexagonAt(cell.Coordinates);
            }
            else
            {
                // Place new hexagon
                PlaceHexagonAt(cell);
            }
            
            // Update stats display
            UpdateStatsDisplay();
        }
        
        private void PlaceHexagonAt(HexCell cell)
        {
            if (cell == null) return;
            
            // Create the hexagon data
            HexagonEditorData data = new HexagonEditorData
            {
                coordinates = cell.Coordinates,
                direction = currentDirection,
                colorIndex = selectedColorIndex
            };
            
            // Add to dictionary
            placedHexagons[cell.Coordinates] = data;
            
            // Create visual representation
            GameObject hexObj = CreateHexagonVisual(cell.transform.position, currentDirection, colorPalette[selectedColorIndex]);
            data.visualObject = hexObj;
        }
        
        private void RemoveHexagonAt(Vector2Int coordinates)
        {
            if (placedHexagons.TryGetValue(coordinates, out HexagonEditorData data))
            {
                // Destroy visual object
                if (data.visualObject != null)
                {
                    Destroy(data.visualObject);
                }
                
                // Remove from dictionary
                placedHexagons.Remove(coordinates);
            }
        }
        
        private GameObject CreateHexagonVisual(Vector3 position, HexDirection direction, Color color)
        {
            // Create hexagon object
            GameObject hexObj = Instantiate(hexagonPrefab, position, Quaternion.identity, gridContainer);
            hexObj.name = $"EditorHexagon_{position.x}_{position.z}";
            
            // Set color
            MeshRenderer renderer = hexObj.GetComponentInChildren<MeshRenderer>();
            if (renderer != null)
            {
                Material mat = new Material(renderer.material);
                mat.color = color;
                renderer.material = mat;
            }
            
            // Set arrow direction
            Transform arrowTransform = hexObj.transform.Find("Arrow");
            if (arrowTransform != null)
            {
                float rotation = HexDirectionHelper.GetRotationDegrees(direction);
                arrowTransform.localRotation = Quaternion.Euler(0, rotation, 0);
            }
            
            return hexObj;
        }
        
        private GameObject CreatePreviewHexagon()
        {
            // Create a simple hexagon object
            GameObject hexObj = Instantiate(hexagonPrefab);
            hexObj.name = "PreviewHexagon";
            
            // Make it semi-transparent
            MeshRenderer renderer = hexObj.GetComponentInChildren<MeshRenderer>();
            if (renderer != null)
            {
                Material mat = new Material(renderer.material);
                Color previewColor = colorPalette[selectedColorIndex];
                previewColor.a = 0.5f;
                mat.color = previewColor;
                renderer.material = mat;
            }
            
            // Set initial arrow direction
            Transform arrowTransform = hexObj.transform.Find("Arrow");
            if (arrowTransform != null)
            {
                float rotation = HexDirectionHelper.GetRotationDegrees(currentDirection);
                arrowTransform.localRotation = Quaternion.Euler(0, rotation, 0);
            }
            
            return hexObj;
        }
        
        private void UpdatePreviewDirection()
        {
            if (previewHexagon == null) return;
            
            Transform arrowTransform = previewHexagon.transform.Find("Arrow");
            if (arrowTransform != null)
            {
                float rotation = HexDirectionHelper.GetRotationDegrees(currentDirection);
                arrowTransform.localRotation = Quaternion.Euler(0, rotation, 0);
            }
        }
        
        #endregion
        
        #region Grid Management
        
        private void UpdateGridSize(int width, int height)
        {
            gridWidth = width;
            gridHeight = height;
            
            if (gridSizeText != null)
            {
                gridSizeText.text = $"Grid Size: {width} x {height}";
            }
            
            // Update grid config
            if (gridConfig != null)
            {
                gridConfig.defaultGridWidth = width;
                gridConfig.defaultGridHeight = height;
            }
            
            // Regenerate grid
            if (gridManager != null)
            {
                // Clear existing hexagons first
                ClearAllHexagons();
                
                // Call grid generation methods
                var generateMethod = typeof(GridManager).GetMethod("GenerateGrid");
                if (generateMethod != null)
                {
                    generateMethod.Invoke(gridManager, new object[] { width, height });
                }
            }
        }
        
        private void ClearAllHexagons()
        {
            // Destroy all visual objects
            foreach (var data in placedHexagons.Values)
            {
                if (data.visualObject != null)
                {
                    Destroy(data.visualObject);
                }
            }
            
            // Clear dictionary
            placedHexagons.Clear();
            
            // Update stats display
            UpdateStatsDisplay();
        }
        
        #endregion
        
        #region UI Event Handlers
        
        private void OnLevelNameChanged(string value)
        {
            levelName = value;
        }
        
        private void OnLevelNumberChanged(string value)
        {
            if (int.TryParse(value, out int result))
            {
                levelNumber = Mathf.Max(1, result);
                levelNumberInput.text = levelNumber.ToString();
            }
            else
            {
                levelNumberInput.text = levelNumber.ToString();
            }
        }
        
        private void OnMoveLimitChanged(string value)
        {
            if (int.TryParse(value, out int result))
            {
                moveLimit = Mathf.Max(1, result);
                moveLimitInput.text = moveLimit.ToString();
            }
            else
            {
                moveLimitInput.text = moveLimit.ToString();
            }
        }
        
        private void OnTargetHexagonsChanged(string value)
        {
            if (int.TryParse(value, out int result))
            {
                targetHexagonsToRemove = Mathf.Max(1, result);
                targetHexagonsInput.text = targetHexagonsToRemove.ToString();
            }
            else
            {
                targetHexagonsInput.text = targetHexagonsToRemove.ToString();
            }
        }
        
        private void OnGridWidthChanged(float value)
        {
            int newWidth = Mathf.RoundToInt(value);
            if (newWidth != gridWidth)
            {
                UpdateGridSize(newWidth, gridHeight);
            }
        }
        
        private void OnGridHeightChanged(float value)
        {
            int newHeight = Mathf.RoundToInt(value);
            if (newHeight != gridHeight)
            {
                UpdateGridSize(gridWidth, newHeight);
            }
        }
        
        private void SetDirection(HexDirection direction)
        {
            currentDirection = direction;
            UpdatePreviewDirection();
            
            // Update UI to show selected direction
            if (directionButtons != null)
            {
                for (int i = 0; i < directionButtons.Length; i++)
                {
                    ColorBlock colors = directionButtons[i].colors;
                    if ((int)direction == i)
                    {
                        colors.normalColor = new Color(0.8f, 0.8f, 1f);
                    }
                    else
                    {
                        colors.normalColor = Color.white;
                    }
                    directionButtons[i].colors = colors;
                }
            }
        }
        
        private void SetSelectedColor(int colorIndex)
        {
            if (colorIndex >= 0 && colorIndex < colorPalette.Count)
            {
                selectedColorIndex = colorIndex;
                
                // Update UI to show selected color
                if (colorButtons != null)
                {
                    for (int i = 0; i < colorButtons.Length && i < colorPalette.Count; i++)
                    {
                        if (i == selectedColorIndex)
                        {
                            colorButtons[i].transform.localScale = new Vector3(1.2f, 1.2f, 1.2f);
                        }
                        else
                        {
                            colorButtons[i].transform.localScale = Vector3.one;
                        }
                    }
                }
            }
        }
        
        #endregion
        
        #region Level Management
        
        private void SaveLevel()
        {
            if (placedHexagons.Count == 0)
            {
                ShowMessage("Cannot save empty level. Place at least one hexagon.");
                return;
            }
            
            // Create new level config
            LevelConfig levelConfig = ScriptableObject.CreateInstance<LevelConfig>();
            
            // Set basic properties
            levelConfig.levelNumber = levelNumber;
            levelConfig.levelName = levelName;
            levelConfig.moveLimit = moveLimit;
            levelConfig.targetHexagonsToRemove = Mathf.Min(targetHexagonsToRemove, placedHexagons.Count);
            
            // Set color palette
            levelConfig.colorPalette = colorPalette.ToArray();
            
            // Convert editor hexagons to level data
            List<HexagonData> hexList = new List<HexagonData>();
            foreach (var hexPlacement in placedHexagons.Values)
            {
                HexagonData hexData = new HexagonData
                {
                    coordinates = hexPlacement.coordinates,
                    direction = hexPlacement.direction,
                    colorIndex = hexPlacement.colorIndex
                };
                
                hexList.Add(hexData);
            }
            levelConfig.hexagons = hexList.ToArray();
            
            // Save the level to Resources folder or another location
            // In a runtime build, we would need to serialize this data differently
            #if UNITY_EDITOR
            string path = UnityEditor.EditorUtility.SaveFilePanelInProject(
                "Save Level",
                $"Level_{levelNumber}_{levelName.Replace(" ", "_")}",
                "asset",
                "Save level asset"
            );
            
            if (!string.IsNullOrEmpty(path))
            {
                UnityEditor.AssetDatabase.CreateAsset(levelConfig, path);
                UnityEditor.AssetDatabase.SaveAssets();
                
                ShowMessage($"Level saved to {path}");
            }
            #else
            // For runtime builds, we could use PlayerPrefs or a custom serialization solution
            SaveLevelToPlayerPrefs(levelConfig);
            #endif
        }
        
        private void LoadLevel()
        {
            #if UNITY_EDITOR
            string path = UnityEditor.EditorUtility.OpenFilePanel("Load Level", "Assets", "asset");
            
            if (!string.IsNullOrEmpty(path))
            {
                // Convert to project-relative path
                if (path.StartsWith(Application.dataPath))
                {
                    path = "Assets" + path.Substring(Application.dataPath.Length);
                }
                
                // Load the asset
                LevelConfig levelConfig = UnityEditor.AssetDatabase.LoadAssetAtPath<LevelConfig>(path);
                
                if (levelConfig != null)
                {
                    LoadLevelConfig(levelConfig);
                    ShowMessage($"Level loaded from {path}");
                }
                else
                {
                    ShowMessage("Failed to load the level asset.");
                }
            }
            #else
            // For runtime builds, we could use PlayerPrefs or a custom serialization solution
            LevelConfig levelConfig = LoadLevelFromPlayerPrefs();
            if (levelConfig != null)
            {
                LoadLevelConfig(levelConfig);
                ShowMessage("Level loaded successfully.");
            }
            else
            {
                ShowMessage("No saved level found.");
            }
            #endif
        }
        
        private void LoadLevelConfig(LevelConfig levelConfig)
        {
            if (levelConfig == null) return;
            
            // Load basic properties
            levelName = levelConfig.levelName;
            levelNumber = levelConfig.levelNumber;
            moveLimit = levelConfig.moveLimit;
            targetHexagonsToRemove = levelConfig.targetHexagonsToRemove;
            
            // Update UI fields
            if (levelNameInput != null) levelNameInput.text = levelName;
            if (levelNumberInput != null) levelNumberInput.text = levelNumber.ToString();
            if (moveLimitInput != null) moveLimitInput.text = moveLimit.ToString();
            if (targetHexagonsInput != null) targetHexagonsInput.text = targetHexagonsToRemove.ToString();
            
            // Load color palette
            colorPalette.Clear();
            foreach (Color color in levelConfig.colorPalette)
            {
                colorPalette.Add(color);
            }
            
            // Update color buttons
            if (colorButtons != null)
            {
                for (int i = 0; i < colorButtons.Length && i < colorPalette.Count; i++)
                {
                    colorButtons[i].color = colorPalette[i];
                }
            }
            
            // Clear existing hexagons
            ClearAllHexagons();
            
            // Create hexagons from level data
            if (levelConfig.hexagons != null)
            {
                foreach (HexagonData hexData in levelConfig.hexagons)
                {
                    // Get the cell at these coordinates
                    HexCell cell = gridManager.GetCell(hexData.coordinates);
                    
                    if (cell != null)
                    {
                        // Create the hexagon data
                        HexagonEditorData editorData = new HexagonEditorData
                        {
                            coordinates = hexData.coordinates,
                            direction = hexData.direction,
                            colorIndex = hexData.colorIndex
                        };
                        
                        // Add to dictionary
                        placedHexagons[hexData.coordinates] = editorData;
                        
                        // Create visual representation
                        Color hexColor = (hexData.colorIndex >= 0 && hexData.colorIndex < colorPalette.Count) 
                            ? colorPalette[hexData.colorIndex] 
                            : Color.white;
                            
                        editorData.visualObject = CreateHexagonVisual(cell.transform.position, hexData.direction, hexColor);
                    }
                }
            }
            
            // Update stats display
            UpdateStatsDisplay();
        }
        
        #if !UNITY_EDITOR
        private void SaveLevelToPlayerPrefs(LevelConfig levelConfig)
        {
            // Simple JSON serialization example - in production, use a more robust solution
            // This is just a placeholder for runtime builds
            
            // Create a serializable structure
            LevelConfigSerializable serializable = new LevelConfigSerializable
            {
                levelName = levelConfig.levelName,
                levelNumber = levelConfig.levelNumber,
                moveLimit = levelConfig.moveLimit,
                targetHexagonsToRemove = levelConfig.targetHexagonsToRemove,
                // Serialize colors and hexagons as needed
            };
            
            // Convert to JSON
            string json = JsonUtility.ToJson(serializable);
            
            // Save to PlayerPrefs
            PlayerPrefs.SetString($"Level_{levelConfig.levelNumber}", json);
            PlayerPrefs.Save();
            
            ShowMessage("Level saved successfully.");
        }
        
        private LevelConfig LoadLevelFromPlayerPrefs()
        {
            // Check if we have a saved level
            string key = $"Level_{levelNumber}";
            if (!PlayerPrefs.HasKey(key))
            {
                return null;
            }
            
            // Get the JSON data
            string json = PlayerPrefs.GetString(key);
            
            // Deserialize
            LevelConfigSerializable serializable = JsonUtility.FromJson<LevelConfigSerializable>(json);
            
            // Create a LevelConfig
            LevelConfig levelConfig = ScriptableObject.CreateInstance<LevelConfig>();
            levelConfig.levelName = serializable.levelName;
            levelConfig.levelNumber = serializable.levelNumber;
            levelConfig.moveLimit = serializable.moveLimit;
            levelConfig.targetHexagonsToRemove = serializable.targetHexagonsToRemove;
            // Deserialize colors and hexagons as needed
            
            return levelConfig;
        }
        
        [System.Serializable]
        private class LevelConfigSerializable
        {
            public string levelName;
            public int levelNumber;
            public int moveLimit;
            public int targetHexagonsToRemove;
            // Add other fields as needed for full serialization
        }
        #endif
        
        #endregion
        
        #region Utilities
        
        private void ShowMessage(string message)
        {
            if (messagePanel != null && messageText != null)
            {
                messageText.text = message;
                messagePanel.SetActive(true);
                
                // Hide message after a delay
                Invoke("HideMessage", 3f);
            }
            else
            {
                Debug.Log(message);
            }
        }
        
        private void HideMessage()
        {
            if (messagePanel != null)
            {
                messagePanel.SetActive(false);
            }
        }
        
        private void UpdateStatsDisplay()
        {
            if (statsText != null)
            {
                statsText.text = $"Hexagons: {placedHexagons.Count}\n" +
                                $"Target: {targetHexagonsToRemove}\n" +
                                $"Moves: {moveLimit}";
            }
        }
        
        // Editor data class for holding hexagon information
        [System.Serializable]
        private class HexagonEditorData
        {
            public Vector2Int coordinates;
            public HexDirection direction;
            public int colorIndex;
            public GameObject visualObject;
        }
        
        #endregion
    }
}