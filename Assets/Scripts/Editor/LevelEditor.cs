using UnityEngine;
using UnityEditor;
using System.IO;
using UnityEngine.Tilemaps;
using System.Collections.Generic;
using System.Linq;

namespace HexaAway.Editor
{
    using Core;

    [InitializeOnLoad]
    public class TilemapLevelEditor : EditorWindow
    {
        // Add static constructor for InitializeOnLoad
        static bool checkedForSprite = false;
   

        // Editor preferences
        private Vector2 scrollPosition;
        private bool showGridSettings = true;
        private bool showHexagonTools = true;

        // Debug options
        private bool showDebugInfo = false;

        // Tilemap references
        private Grid hexGrid;
        private Tilemap hexTilemap;
        private TilemapRenderer tilemapRenderer;

        // Grid settings
        private int gridWidth = 5;
        private int gridHeight = 5;
        private float cellSize = 1.0f;

        // Level settings
        private string levelName = "New Level";
        private int levelNumber = 1;
        private int moveLimit = 15;
        private int targetHexagonsToRemove = 3;

        // Color palette
        private List<Color> colorPalette = new List<Color>()
        {
            Color.blue,
            Color.green,
            Color.red
        };

        private int selectedColorIndex = 0;

        // Hexagon placement
        private Dictionary<Vector3Int, HexTileData> placedHexagons = new Dictionary<Vector3Int, HexTileData>();
        private HexDirection currentDirection = HexDirection.East;

        // Editor state
        private bool isDirty = false;
        private HexTile[] hexTiles;
        private int selectedTileIndex = 0;

        // Layout type
        private bool isPointyTop = true; // If false, it's flat-top hexagons

        // Serialized properties for undo/redo support
        private SerializedObject serializedObject;

        [MenuItem("HexaAway/Tilemap Level Editor")]
        public static void ShowWindow()
        {
            var window = GetWindow<TilemapLevelEditor>("HexaAway Tilemap Editor");
            window.minSize = new Vector2(300, 500);
        }

        private void OnEnable()
        {
            
            // Check for sprite only when the window is enabled
            if (!checkedForSprite)
            {
                if (Resources.Load<Sprite>("HexTileSprite") == null)
                {
                    Debug.LogWarning(
                        "HexTileSprite not found in Resources. Please generate one using HexaAway/Generate Hex Sprite menu item.");
                }
                checkedForSprite = true;
            }
        
            // Rest of your OnEnable code...
            InitializeTilemap();
            LoadHexTiles();
            SceneView.duringSceneGui += OnSceneGUI;
            serializedObject = new SerializedObject(this);
        
            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.FrameSelected();
            }
            // Ensure we have a hex sprite
            if (Resources.Load<Sprite>("HexTileSprite") == null)
            {
                Debug.LogWarning(
                    "HexTileSprite not found in Resources. Please generate one using HexaAway/Generate Hex Sprite menu item.");
            }

            // Initialize tilemap if it doesn't exist
            InitializeTilemap();

            // Load or create hex tiles
            LoadHexTiles();

            // Register for scene view events
            SceneView.duringSceneGui += OnSceneGUI;

            // Create serialized object for undo support
            serializedObject = new SerializedObject(this);

            // Ensure the scene view is visible
            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.FrameSelected();
            }
        }

        private void OnDisable()
        {
            // Unregister from scene view events
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            // Title
            EditorGUILayout.LabelField("HexaAway Tilemap Level Editor", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // Grid Settings
            showGridSettings = EditorGUILayout.Foldout(showGridSettings, "Grid Settings", true);
            if (showGridSettings)
            {
                EditorGUI.indentLevel++;

                EditorGUI.BeginChangeCheck();
                gridWidth = EditorGUILayout.IntSlider("Grid Width", gridWidth, 3, 10);
                gridHeight = EditorGUILayout.IntSlider("Grid Height", gridHeight, 3, 10);
                cellSize = EditorGUILayout.Slider("Cell Size", cellSize, 0.5f, 2.0f);

                // Hex orientation
                EditorGUI.BeginChangeCheck();
                isPointyTop = EditorGUILayout.Toggle("Pointy-Top Hexagons", isPointyTop);
                if (EditorGUI.EndChangeCheck())
                {
                    // Reinitialize tilemap with new orientation
                    CleanupTilemap();
                    InitializeTilemap();
                }

                if (EditorGUI.EndChangeCheck())
                {
                    UpdateGridSize();
                }

                // Manual actions
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Refresh Grid"))
                {
                    RegenerateTilemap();
                }

                if (GUILayout.Button("Debug Grid"))
                {
                    DebugTilemapSetup();
                }

                EditorGUILayout.EndHorizontal();

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();

            // Level Settings
            EditorGUILayout.LabelField("Level Settings", EditorStyles.boldLabel);
            levelName = EditorGUILayout.TextField("Level Name", levelName);
            levelNumber = EditorGUILayout.IntField("Level Number", levelNumber);
            moveLimit = EditorGUILayout.IntField("Move Limit", moveLimit);
            targetHexagonsToRemove = EditorGUILayout.IntField("Target Hexagons to Remove", targetHexagonsToRemove);

            EditorGUILayout.Space();

            // Color Palette
            EditorGUILayout.LabelField("Color Palette", EditorStyles.boldLabel);

            for (int i = 0; i < colorPalette.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();

                // Color selection indicator
                if (selectedColorIndex == i)
                {
                    EditorGUILayout.LabelField("►", GUILayout.Width(15));
                }
                else
                {
                    EditorGUILayout.LabelField(" ", GUILayout.Width(15));
                }

                // Color field
                EditorGUI.BeginChangeCheck();
                colorPalette[i] = EditorGUILayout.ColorField($"Color {i + 1}", colorPalette[i]);
                if (EditorGUI.EndChangeCheck())
                {
                    UpdateTileColors();
                }

                // Select color button
                if (GUILayout.Button("Select", GUILayout.Width(60)))
                {
                    selectedColorIndex = i;
                }

                // Remove color button
                if (colorPalette.Count > 1 && GUILayout.Button("X", GUILayout.Width(25)))
                {
                    colorPalette.RemoveAt(i);
                    if (selectedColorIndex >= colorPalette.Count)
                    {
                        selectedColorIndex = colorPalette.Count - 1;
                    }

                    UpdateTileColors();
                }

                EditorGUILayout.EndHorizontal();
            }

            // Add color button
            if (GUILayout.Button("Add Color"))
            {
                colorPalette.Add(new Color(Random.value, Random.value, Random.value));
                UpdateTileColors();
            }

            EditorGUILayout.Space();

            // Hexagon Tools
            showHexagonTools = EditorGUILayout.Foldout(showHexagonTools, "Hexagon Tools", true);
            if (showHexagonTools)
            {
                EditorGUI.indentLevel++;

                // Direction selection
                EditorGUILayout.LabelField("Arrow Direction:");
                EditorGUILayout.BeginHorizontal();

                // Create direction buttons in a circular layout
                string[] directionLabels = new string[] {"E", "SE", "SW", "W", "NW", "NE"};
                for (int i = 0; i < 6; i++)
                {
                    HexDirection dir = (HexDirection) i;
                    GUI.color = (currentDirection == dir) ? Color.green : Color.white;

                    if (GUILayout.Button(directionLabels[i], GUILayout.Width(30)))
                    {
                        currentDirection = dir;
                        Repaint();
                    }

                    if (i == 2) // Break after SW to start a new row
                    {
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.BeginHorizontal();
                    }
                }

                EditorGUILayout.EndHorizontal();
                GUI.color = Color.white;

                // Tile selection
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Placement Mode:");
                EditorGUILayout.BeginHorizontal();

                // Create buttons for each color
                for (int i = 0; i < colorPalette.Count; i++)
                {
                    GUI.backgroundColor = colorPalette[i];
                    if (GUILayout.Button("", GUILayout.Width(30), GUILayout.Height(30)))
                    {
                        selectedTileIndex = i;
                        selectedColorIndex = i;
                    }

                    GUI.backgroundColor = Color.white;
                }

                // Eraser button
                GUI.color = Color.white;
                if (GUILayout.Button("Eraser", GUILayout.Width(60)))
                {
                    selectedTileIndex = -1; // Special case for eraser
                }

                EditorGUILayout.EndHorizontal();
                GUI.color = Color.white;

                // Hexagon stats
                EditorGUILayout.LabelField($"Placed Hexagons: {placedHexagons.Count}");

                // Clear button
                if (GUILayout.Button("Clear All Hexagons"))
                {
                    if (EditorUtility.DisplayDialog("Clear Hexagons",
                            "Are you sure you want to clear all placed hexagons?", "Yes", "No"))
                    {
                        Undo.RecordObject(this, "Clear All Hexagons");
                        placedHexagons.Clear();
                        hexTilemap.ClearAllTiles();
                        isDirty = true;
                        RegenerateTilemap();
                    }
                }

                EditorGUI.indentLevel--;
            }

            // Debug info
            showDebugInfo = EditorGUILayout.Foldout(showDebugInfo, "Debug Information", true);
            if (showDebugInfo)
            {
                EditorGUI.indentLevel++;

                if (hexGrid != null)
                {
                    EditorGUILayout.LabelField("Grid Cell Layout: " + hexGrid.cellLayout.ToString());
                    EditorGUILayout.LabelField("Grid Cell Size: " + hexGrid.cellSize.ToString());
                    EditorGUILayout.LabelField("Grid Cell Swizzle: " + hexGrid.cellSwizzle.ToString());
                }
                else
                {
                    EditorGUILayout.LabelField("Grid: Not initialized");
                }

                if (hexTilemap != null)
                {
                    EditorGUILayout.LabelField("Tilemap Origin: " + hexTilemap.origin.ToString());
                    EditorGUILayout.LabelField("Tilemap Size: " + hexTilemap.size.ToString());
                }
                else
                {
                    EditorGUILayout.LabelField("Tilemap: Not initialized");
                }

                // Display the first few hexagons
                if (placedHexagons.Count > 0)
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Placed Hexagons (first 5):");

                    int count = 0;
                    foreach (var kvp in placedHexagons)
                    {
                        EditorGUILayout.LabelField(
                            $"- Position: {kvp.Key}, Color: {kvp.Value.colorIndex}, Direction: {kvp.Value.direction}");

                        count++;
                        if (count >= 5) break;
                    }
                }

                // Resource check
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Resource Check:");
                Sprite hexSprite = Resources.Load<Sprite>("HexTileSprite");
                EditorGUILayout.LabelField("HexTileSprite: " + (hexSprite != null ? "Found" : "Missing"));

                if (hexSprite != null)
                {
                    EditorGUILayout.LabelField("Sprite Size: " + hexSprite.rect.size.ToString());
                    EditorGUILayout.LabelField("Sprite Pivot: " + hexSprite.pivot.ToString());
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();

            // Save/Load Buttons
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Save Level"))
            {
                SaveLevel();
            }

            if (GUILayout.Button("Load Level"))
            {
                LoadLevel();
            }

            if (GUILayout.Button("New Level"))
            {
                if (!isDirty || EditorUtility.DisplayDialog("Unsaved Changes",
                        "You have unsaved changes. Create a new level anyway?", "Yes", "No"))
                {
                    ResetEditor();
                }
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndScrollView();

            // If anything changed, redraw
            if (GUI.changed)
            {
                isDirty = true;
                Repaint();
            }
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (hexTilemap == null)
                return;

            // Handle events
            Event e = Event.current;

            // Only process mouse events
            if (e.type == EventType.MouseDown || e.type == EventType.MouseDrag)
            {
                // Convert mouse position to world position
                Vector2 mousePosition = HandleUtility.GUIPointToWorldRay(e.mousePosition).origin;

                // Convert world position to cell position
                Vector3Int cellPosition = hexGrid.WorldToCell(mousePosition);

                // Only proceed if we're within the valid grid bounds
                if (IsWithinGridBounds(cellPosition))
                {
                    // Handle left mouse button (place)
                    if (e.button == 0)
                    {
                        PlaceOrRemoveHexagon(cellPosition);
                        e.Use();
                    }
                }

                // Force repaint of scene view
                sceneView.Repaint();
            }
            else if (e.type == EventType.Repaint)
            {
                // Draw a custom cursor or preview at the mouse position
                Vector2 mousePosition = HandleUtility.GUIPointToWorldRay(e.mousePosition).origin;
                Vector3Int cellPosition = hexGrid.WorldToCell(mousePosition);

                if (IsWithinGridBounds(cellPosition))
                {
                    // Draw a preview of the tile at the current position
                    Handles.color = selectedTileIndex >= 0 && selectedTileIndex < colorPalette.Count
                        ? colorPalette[selectedTileIndex]
                        : Color.white;

                    Vector3 worldPos = hexGrid.GetCellCenterWorld(cellPosition);
                    Handles.DrawWireDisc(worldPos, Vector3.back, cellSize * 0.4f);

                    // Draw direction indicator
                    Vector3 dirVector = GetDirectionVector(currentDirection) * cellSize * 0.3f;
                    Handles.DrawLine(worldPos, worldPos + dirVector);
                    Handles.DrawSolidDisc(worldPos + dirVector, Vector3.back, cellSize * 0.1f);

                    // Draw coordinates for debugging
                    if (showDebugInfo)
                    {
                        Handles.Label(worldPos + Vector3.up * cellSize * 0.6f,
                            $"Cell: {cellPosition}");
                    }
                }
            }
        }

        private void InitializeTilemap()
        {
            // Find or create the grid game object
            GameObject gridObj = GameObject.Find("HexaAwayEditorGrid");
            if (gridObj == null)
            {
                gridObj = new GameObject("HexaAwayEditorGrid");
                gridObj.hideFlags = HideFlags.DontSave;
            }

            // Add or get a Grid component
            hexGrid = gridObj.GetComponent<Grid>();
            if (hexGrid == null)
            {
                hexGrid = gridObj.AddComponent<Grid>();
                hexGrid.cellLayout = GridLayout.CellLayout.Hexagon;
                hexGrid.cellSize = new Vector3(cellSize, cellSize, 0);
                hexGrid.cellSwizzle = isPointyTop ? GridLayout.CellSwizzle.XYZ : GridLayout.CellSwizzle.YXZ;
            }
            else
            {
                // Update grid properties
                hexGrid.cellLayout = GridLayout.CellLayout.Hexagon;
                hexGrid.cellSize = new Vector3(cellSize, cellSize, 0);
                hexGrid.cellSwizzle = isPointyTop ? GridLayout.CellSwizzle.XYZ : GridLayout.CellSwizzle.YXZ;
            }

            // Find or create the tilemap game object
            GameObject tilemapObj = gridObj.transform.Find("HexTilemap")?.gameObject;
            if (tilemapObj == null)
            {
                tilemapObj = new GameObject("HexTilemap");
                tilemapObj.transform.SetParent(gridObj.transform);
                tilemapObj.hideFlags = HideFlags.DontSave;
            }

            // Add or get Tilemap component
            hexTilemap = tilemapObj.GetComponent<Tilemap>();
            if (hexTilemap == null)
            {
                hexTilemap = tilemapObj.AddComponent<Tilemap>();
            }

            // Add or get TilemapRenderer component
            tilemapRenderer = tilemapObj.GetComponent<TilemapRenderer>();
            if (tilemapRenderer == null)
            {
                tilemapRenderer = tilemapObj.AddComponent<TilemapRenderer>();
                tilemapRenderer.sortingOrder = 0;
            }

            // Position the grid at the origin
            gridObj.transform.position = Vector3.zero;

            // Add collider for raycasting if needed
            TilemapCollider2D collider = tilemapObj.GetComponent<TilemapCollider2D>();
            if (collider == null)
            {
                tilemapObj.AddComponent<TilemapCollider2D>();
            }

            // Focus camera on the grid
            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.pivot =
                    new Vector3(gridWidth * cellSize / 2, gridHeight * cellSize / 2, 0);
                SceneView.lastActiveSceneView.size = Mathf.Max(gridWidth, gridHeight) * cellSize * 1.5f;
                SceneView.lastActiveSceneView.Repaint();
            }

            // Create a test pattern to show the grid
            CreateTestPattern();

            // Show a helpful message in the console
            Debug.Log(
                "HexaAway Tilemap Editor initialized. If you don't see the grid in the scene view, click the 'Debug Grid' button in the editor window.");
            Debug.Log(
                "Remember to generate the hex sprite first using HexaAway/Generate Hex Sprite if you haven't already.");
        }

        private void CleanupTilemap()
        {
            // Destroy the grid object
            GameObject gridObj = GameObject.Find("HexaAwayEditorGrid");
            if (gridObj != null)
            {
                DestroyImmediate(gridObj);
            }

            hexGrid = null;
            hexTilemap = null;
            tilemapRenderer = null;
        }

        private void CreateTestPattern()
        {
            if (hexTilemap == null) return;

            // Clear the tilemap
            hexTilemap.ClearAllTiles();

            // Create a simple checker pattern to visualize the grid
            for (int x = 0; x < gridWidth; x++)
            {
                for (int y = 0; y < gridHeight; y++)
                {
                    Vector3Int cellPos = new Vector3Int(x, y, 0);

                    // Create a dummy tile just for visualization
                    var tile = ScriptableObject.CreateInstance<Tile>();
                    tile.color = new Color(0.8f, 0.8f, 0.8f, 0.3f);

                    // Load sprite
                    Sprite hexSprite = Resources.Load<Sprite>("HexTileSprite");
                    if (hexSprite != null)
                    {
                        tile.sprite = hexSprite;
                        hexTilemap.SetTile(cellPos, tile);
                    }
                    else
                    {
                        Debug.LogError("Could not find HexTileSprite in Resources. Please generate it first.");
                    }
                }
            }

            // Force refresh
            hexTilemap.RefreshAllTiles();
        }

        private void DebugTilemapSetup()
        {
            Debug.Log("=== HexaAway Tilemap Editor Debug Info ===");

            // Check if grid exists
            Debug.Log($"Grid: {(hexGrid != null ? "Found" : "Missing")}");
            if (hexGrid != null)
            {
                Debug.Log($"- Cell Layout: {hexGrid.cellLayout}");
                Debug.Log($"- Cell Size: {hexGrid.cellSize}");
                Debug.Log($"- Cell Swizzle: {hexGrid.cellSwizzle}");
            }

            // Check if tilemap exists
            Debug.Log($"Tilemap: {(hexTilemap != null ? "Found" : "Missing")}");
            if (hexTilemap != null)
            {
                // Debug.Log($"- Tile Count: {hexTilemap.GetTilesRangeCount(hexTilemap.cellBounds)}");
                Debug.Log($"- Bounds: {hexTilemap.cellBounds}");
            }

            // Check if sprite exists
            Sprite hexSprite = Resources.Load<Sprite>("HexTileSprite");
            Debug.Log($"HexTileSprite: {(hexSprite != null ? "Found" : "Missing")}");
            if (hexSprite != null)
            {
                Debug.Log($"- Sprite Size: {hexSprite.rect.size}");
                Debug.Log($"- Sprite Pivot: {hexSprite.pivot}");
            }

            // Check all tile assets
            Debug.Log($"Hex Tiles Array: {(hexTiles != null ? hexTiles.Length.ToString() : "null")}");
            if (hexTiles != null)
            {
                for (int i = 0; i < hexTiles.Length; i++)
                {
                    Debug.Log($"- Tile {i}: {(hexTiles[i] != null ? "Valid" : "Null")}");
                }
            }

            // Try to create a test tile at 0,0
            Debug.Log("Attempting to place a test tile at position (0,0)...");
            try
            {
                if (hexTiles != null && hexTiles.Length > 0 && hexTiles[0] != null)
                {
                    hexTilemap.SetTile(new Vector3Int(0, 0, 0), hexTiles[0]);
                    Debug.Log("- Test tile placed successfully.");
                }
                else
                {
                    Debug.Log("- Cannot place test tile: No valid hex tiles available.");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"- Error placing test tile: {e.Message}");
            }

            Debug.Log("=== End Debug Info ===");
        }

        private void LoadHexTiles()
        {
            // We'll create the tiles in memory for each session
            // rather than trying to load them from resources
            CreateHexTiles();

            // Log what we've created
            Debug.Log($"Created {hexTiles.Length} hex tiles in memory");
        }

        private void CreateHexTiles()
        {
            // Create a hex tile for each color
            hexTiles = new HexTile[colorPalette.Count];

            // Load the sprite that should be used by all tiles
            Sprite hexSprite = Resources.Load<Sprite>("HexTileSprite");

            if (hexSprite == null)
            {
                Debug.LogError(
                    "HexTileSprite not found in Resources. Please generate it using the 'HexaAway/Generate Hex Sprite' menu item.");
                return;
            }

            for (int i = 0; i < colorPalette.Count; i++)
            {
                HexTile tile = ScriptableObject.CreateInstance<HexTile>();
                tile.name = $"HexTile_{i}";
                tile.color = colorPalette[i];
                tile.direction = HexDirection.East;
                tile.sprite = hexSprite; // Store the sprite reference directly
                hexTiles[i] = tile;
            }
        }

        private void UpdateTileColors()
        {
            // Update existing tile colors or create new ones
            if (hexTiles != null && hexTiles.Length > 0)
            {
                // Resize array if needed
                if (hexTiles.Length != colorPalette.Count)
                {
                    System.Array.Resize(ref hexTiles, colorPalette.Count);
                }

                for (int i = 0; i < colorPalette.Count; i++)
                {
                    if (hexTiles[i] == null)
                    {
                        hexTiles[i] = ScriptableObject.CreateInstance<HexTile>();
                        hexTiles[i].sprite = Resources.Load<Sprite>("HexTileSprite");
                    }

                    hexTiles[i].color = colorPalette[i];
                }
            }
            else
            {
                CreateHexTiles();
            }

            // Refresh the grid to show updated colors
            RefreshTilemapDisplay();
        }

        private void UpdateGridSize()
        {
            if (hexGrid != null)
            {
                // Update grid cell size
                hexGrid.cellSize = new Vector3(cellSize, cellSize, 0);

                // Clear and rebuild the grid
                RegenerateTilemap();
            }
        }

        private void RegenerateTilemap()
        {
            if (hexTilemap == null)
                return;

            // Clear the tilemap
            hexTilemap.ClearAllTiles();

            // Create the test pattern first
            CreateTestPattern();

            // Place all hexagons from our data
            foreach (var kvp in placedHexagons)
            {
                Vector3Int pos = kvp.Key;
                HexTileData data = kvp.Value;

                if (IsWithinGridBounds(pos))
                {
                    PlaceTileAt(pos, data.colorIndex, data.direction);
                }
            }

            // Update the tilemap display
            RefreshTilemapDisplay();
        }

        private void RefreshTilemapDisplay()
        {
            if (hexTilemap != null)
            {
                hexTilemap.RefreshAllTiles();

                // Ensure the grid is visible in scene view
                SceneView.RepaintAll();
            }
        }

        private bool IsWithinGridBounds(Vector3Int cellPosition)
        {
            // Convert from offset coordinates to axial coordinates for better bounds checking
            // This depends on your specific hex grid layout (pointy-top vs flat-top)

            // Simplified check for pointy-top hexagons
            if (isPointyTop)
            {
                int q = cellPosition.x;
                int r = cellPosition.y - (cellPosition.x + (cellPosition.x & 1)) / 2;

                return q >= 0 && q < gridWidth && r >= 0 && r < gridHeight;
            }
            else
            {
                // Flat top hexagons
                int q = cellPosition.x - (cellPosition.y + (cellPosition.y & 1)) / 2;
                int r = cellPosition.y;

                return q >= 0 && q < gridWidth && r >= 0 && r < gridHeight;
            }
        }

        private void PlaceOrRemoveHexagon(Vector3Int cellPosition)
        {
            Undo.RecordObject(this, "Place or Remove Hexagon");

            if (selectedTileIndex == -1)
            {
                // Eraser mode - remove tile
                if (placedHexagons.ContainsKey(cellPosition))
                {
                    placedHexagons.Remove(cellPosition);
                    hexTilemap.SetTile(cellPosition, null);
                    isDirty = true;
                }
            }
            else if (selectedTileIndex >= 0 && selectedTileIndex < hexTiles.Length)
            {
                // Place new tile or replace existing
                PlaceTileAt(cellPosition, selectedTileIndex, currentDirection);

                // Update our data structure
                HexTileData tileData = new HexTileData
                {
                    colorIndex = selectedTileIndex,
                    direction = currentDirection
                };

                placedHexagons[cellPosition] = tileData;
                isDirty = true;
            }
        }

        private void PlaceTileAt(Vector3Int cellPosition, int colorIndex, HexDirection direction)
        {
            if (hexTilemap == null || hexTiles == null || colorIndex < 0 || colorIndex >= hexTiles.Length)
                return;

            // Get the tile for this color
            HexTile tile = hexTiles[colorIndex];

            // Set the direction on the tile (this will update the sprite during rendering)
            tile.direction = direction;

            // Place the tile
            hexTilemap.SetTile(cellPosition, tile);
        }

        private Vector3 GetDirectionVector(HexDirection direction)
        {
            if (isPointyTop)
            {
                switch (direction)
                {
                    case HexDirection.East: return new Vector3(1, 0, 0);
                    case HexDirection.SouthEast: return new Vector3(0.5f, -0.866f, 0);
                    case HexDirection.SouthWest: return new Vector3(-0.5f, -0.866f, 0);
                    case HexDirection.West: return new Vector3(-1, 0, 0);
                    case HexDirection.NorthWest: return new Vector3(-0.5f, 0.866f, 0);
                    case HexDirection.NorthEast: return new Vector3(0.5f, 0.866f, 0);
                    default: return Vector3.right;
                }
            }
            else
            {
                // Flat-top hexagons
                switch (direction)
                {
                    case HexDirection.East: return new Vector3(0.866f, 0.5f, 0);
                    case HexDirection.SouthEast: return new Vector3(0, 1, 0);
                    case HexDirection.SouthWest: return new Vector3(-0.866f, 0.5f, 0);
                    case HexDirection.West: return new Vector3(-0.866f, -0.5f, 0);
                    case HexDirection.NorthWest: return new Vector3(0, -1, 0);
                    case HexDirection.NorthEast: return new Vector3(0.866f, -0.5f, 0);
                    default: return Vector3.right;
                }
            }
        }

        private void SaveLevel()
        {
            // Create new level config
            LevelConfig levelConfig = ScriptableObject.CreateInstance<LevelConfig>();

            // Set basic properties
            levelConfig.levelNumber = levelNumber;
            levelConfig.levelName = levelName;
            levelConfig.moveLimit = moveLimit;
            levelConfig.targetHexagonsToRemove = targetHexagonsToRemove;

            // Set color palette
            levelConfig.colorPalette = colorPalette.ToArray();

            // Set hexagons
            List<HexagonData> hexList = new List<HexagonData>();
            foreach (var kvp in placedHexagons)
            {
                Vector3Int cellPos = kvp.Key;
                HexTileData tileData = kvp.Value;

                HexagonData hexData = new HexagonData
                {
                    // Convert from Tilemap coordinates to your game coordinates
                    coordinates = new Vector2Int(cellPos.x, cellPos.y),
                    direction = tileData.direction,
                    colorIndex = tileData.colorIndex
                };

                hexList.Add(hexData);
            }

            levelConfig.hexagons = hexList.ToArray();

            // Save the asset
            string path = EditorUtility.SaveFilePanelInProject(
                "Save Level",
                $"Level_{levelNumber}_{levelName.Replace(" ", "_")}",
                "asset",
                "Save level asset"
            );

            if (!string.IsNullOrEmpty(path))
            {
                AssetDatabase.CreateAsset(levelConfig, path);
                AssetDatabase.SaveAssets();

                // Show the asset in the project window
                EditorUtility.FocusProjectWindow();
                Selection.activeObject = levelConfig;

                Debug.Log($"Level saved to {path}");

                // Mark as not dirty
                isDirty = false;
            }
        }

        private void LoadLevel()
        {
            string path = EditorUtility.OpenFilePanel("Load Level", "Assets", "asset");

            if (!string.IsNullOrEmpty(path))
            {
                // Convert to project-relative path
                path = "Assets" + path.Substring(Application.dataPath.Length);

                // Load the asset
                LevelConfig levelConfig = AssetDatabase.LoadAssetAtPath<LevelConfig>(path);

                if (levelConfig != null)
                {
                    Undo.RecordObject(this, "Load Level");

                    // Load basic properties
                    levelName = levelConfig.levelName;
                    levelNumber = levelConfig.levelNumber;
                    moveLimit = levelConfig.moveLimit;
                    targetHexagonsToRemove = levelConfig.targetHexagonsToRemove;

                    // Load color palette
                    colorPalette.Clear();
                    foreach (Color color in levelConfig.colorPalette)
                    {
                        colorPalette.Add(color);
                    }

                    // Update tile colors
                    UpdateTileColors();

                    // Clear existing hexagons
                    placedHexagons.Clear();
                    hexTilemap.ClearAllTiles();

                    // Load hexagons
                    foreach (HexagonData hexData in levelConfig.hexagons)
                    {
                        // Convert from your game coordinates to Tilemap coordinates
                        Vector3Int cellPos = new Vector3Int(hexData.coordinates.x, hexData.coordinates.y, 0);

                        HexTileData tileData = new HexTileData
                        {
                            direction = hexData.direction,
                            colorIndex = hexData.colorIndex
                        };

                        placedHexagons[cellPos] = tileData;
                    }

                    // Update the grid
                    RegenerateTilemap();

                    Debug.Log($"Level loaded from {path}");

                    // Mark as not dirty
                    isDirty = false;
                }
                else
                {
                    EditorUtility.DisplayDialog("Load Failed",
                        "The selected file is not a valid Level Configuration asset.", "OK");
                }
            }
        }

        private void ResetEditor()
        {
            Undo.RecordObject(this, "Reset Editor");

            // Reset level settings
            levelName = "New Level";
            levelNumber = 1;
            moveLimit = 15;
            targetHexagonsToRemove = 3;

            // Reset color palette
            colorPalette.Clear();
            colorPalette.Add(Color.blue);
            colorPalette.Add(Color.green);
            colorPalette.Add(Color.red);

            // Reset hexagons
            placedHexagons.Clear();

            // Reset editor state
            selectedColorIndex = 0;
            selectedTileIndex = 0;
            currentDirection = HexDirection.East;
            isDirty = false;

            // Update tiles and grid
            UpdateTileColors();
            RegenerateTilemap();
        }

// Helper class for tile data
        [System.Serializable]
        private class HexTileData
        {
            public int colorIndex;
            public HexDirection direction;
        }
    }
    
    // Custom tile for hex grid
    public class HexTile : TileBase
    {
        public Color color = Color.white;
        public HexDirection direction = HexDirection.East;
        public Sprite sprite; // Store the sprite reference directly
    
        public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
        {
            // Ensure we have a sprite
            if (sprite == null)
            {
                // Try to load sprite from resources as a fallback
                sprite = Resources.Load<Sprite>("HexTileSprite");
            
                if (sprite == null)
                {
                    Debug.LogError("HexTile is missing sprite reference. Please generate the HexTileSprite.");
                    return;
                }
            }
        
            // Set sprite
            tileData.sprite = sprite;
        
            // Set color
            tileData.color = color;
        
            // Set transform based on direction
            tileData.transform = Matrix4x4.TRS(
                Vector3.zero,
                Quaternion.Euler(0, 0, HexDirectionHelper.GetRotationDegrees(direction)),
                Vector3.one
            );
        
            // Set game object to null (we don't need one for this tile)
            tileData.gameObject = null;
        
            // Set the flags
            tileData.flags = TileFlags.LockColor;
        
            // Set the collider type (if needed)
            tileData.colliderType = Tile.ColliderType.Grid;
        }
    }

// HexDirection helper
    public static class HexDirectionHelper
    {
        public static float GetRotationDegrees(HexDirection direction)
        {
            switch (direction)
            {
                case HexDirection.East: return 0f;
                case HexDirection.SouthEast: return 60f;
                case HexDirection.SouthWest: return 120f;
                case HexDirection.West: return 180f;
                case HexDirection.NorthWest: return 240f;
                case HexDirection.NorthEast: return 300f;
                default: return 0f;
            }
        }
    }
}