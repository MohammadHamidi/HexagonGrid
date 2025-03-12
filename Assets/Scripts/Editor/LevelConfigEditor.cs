using UnityEditor;
using UnityEngine;
using HexaAway.Core;
using System;

[CustomEditor(typeof(LevelConfig))]
public class LevelConfigEditor : Editor
{
    // Size (in pixels) for each cell preview button
    private const float cellSize = 60f;
    // Spacing between cell buttons
    private const float cellSpacing = 8f;
    
    // Tracks the currently selected hexagon data index
    private int selectedHexagonDataIndex = -1;
    // Stores the coordinate of the selected cell
    private Vector2Int selectedCoord;

    // Foldout states
    private bool showLevelInfo = true;
    private bool showGridPreview = true;
    private bool showHexagonList = false;
    private bool showObjectives = true;
    
    // Preview settings
    private Vector2 gridScrollPosition;
    private Vector2 hexListScrollPosition;
    private float previewScale = 1.0f;
    private bool showCoordinates = true;
    private bool showDirectionIndicators = true;
    
    // Color schemes for direction arrows
    private readonly Color[] directionColors = new Color[] {
        new Color(1, 0.4f, 0.4f), // East (Red)
        new Color(1, 0.6f, 0.3f), // SouthEast (Orange)
        new Color(1, 1, 0.4f),    // SouthWest (Yellow)
        new Color(0.4f, 1, 0.4f), // West (Green)
        new Color(0.4f, 0.7f, 1), // NorthWest (Light Blue)
        new Color(0.6f, 0.4f, 1)  // NorthEast (Purple)
    };

    public override void OnInspectorGUI()
    {
        LevelConfig level = (LevelConfig)target;
        serializedObject.Update();

        EditorGUILayout.Space();
        DrawHeader();
        
        EditorGUILayout.Space();
        
        // Fix for the Foldout nesting issue - use BeginFoldoutHeaderGroup + EndFoldoutHeaderGroup
        showLevelInfo = EditorGUILayout.Foldout(showLevelInfo, "Level Information", true);
        if (showLevelInfo)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            level.levelNumber = EditorGUILayout.IntField(
                new GUIContent("Level Number", "The sequential number of this level"),
                level.levelNumber);
            
            level.levelName = EditorGUILayout.TextField(
                new GUIContent("Level Name", "Descriptive name for this level"),
                level.levelName);
            
            level.moveLimit = EditorGUILayout.IntField(
                new GUIContent("Move Limit", "Maximum number of moves the player can use"),
                level.moveLimit);
            
            // Color palette field
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("colorPalette"),
                new GUIContent("Color Palette", "Colors available for hexagons in this level"),
                true);
            
            DrawColorPalettePreview(level.colorPalette);
            
            // Grid config field
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("gridConfig"),
                new GUIContent("Grid Configuration", "The grid layout for this level"));
            
            EditorGUILayout.EndVertical();
        }
        
        EditorGUILayout.Space();
        showObjectives = EditorGUILayout.Foldout(showObjectives, "Level Objectives", true);
        if (showObjectives)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            level.targetHexagonsToRemove = EditorGUILayout.IntField(
                new GUIContent("Target Hexagons To Remove", "How many hexagons must be removed to complete the level"),
                level.targetHexagonsToRemove);
            
            if (level.targetHexagonsToRemove > level.hexagons?.Length)
            {
                EditorGUILayout.HelpBox(
                    $"Target ({level.targetHexagonsToRemove}) exceeds total hexagons ({level.hexagons?.Length ?? 0})!",
                    MessageType.Warning);
            }
            
            DrawObjectivesBar(level);
            
            EditorGUILayout.EndVertical();
        }
        
        EditorGUILayout.Space();
        // Only show grid preview if we have a grid config
        if (level.gridConfig != null)
        {
            showGridPreview = EditorGUILayout.Foldout(showGridPreview, "Grid Editor", true);
            if (showGridPreview)
            {
                DrawGridPreview(level);
            }
        }
        else
        {
            EditorGUILayout.HelpBox("Assign a Grid Configuration to edit hexagon placement.", MessageType.Info);
        }
        
        EditorGUILayout.Space();
        showHexagonList = EditorGUILayout.Foldout(showHexagonList, "Hexagon List", true);
        if (showHexagonList)
        {
            DrawHexagonList(level);
        }
        
        serializedObject.ApplyModifiedProperties();
    }
    
    private void DrawHeader()
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.Label("HexaAway Level Editor", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }
    
    private void DrawColorPalettePreview(Color[] colors)
    {
        if (colors == null || colors.Length == 0)
            return;
            
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(10);
        GUILayout.Label("Color Preview:");
        
        for (int i = 0; i < colors.Length; i++)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(30));
            GUI.backgroundColor = colors[i];
            GUILayout.Box("", GUILayout.Width(30), GUILayout.Height(30));
            GUI.backgroundColor = Color.white;
            GUILayout.Label(i.ToString(), EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.EndVertical();
            
            GUILayout.Space(5);
        }
        
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space();
    }
    
    private void DrawObjectivesBar(LevelConfig level)
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Moves:", GUILayout.Width(50));
        EditorGUILayout.LabelField(
            $"{level.moveLimit}",
            EditorStyles.boldLabel,
            GUILayout.Width(40));
        
        Rect progressRect = GUILayoutUtility.GetRect(100, 20, GUILayout.ExpandWidth(true));
        EditorGUI.ProgressBar(progressRect, 1f, $"Move Limit: {level.moveLimit}");
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Target:", GUILayout.Width(50));
        EditorGUILayout.LabelField(
            $"{level.targetHexagonsToRemove} / {level.hexagons?.Length ?? 0}",
            EditorStyles.boldLabel,
            GUILayout.Width(60));
        
        float targetRatio = level.hexagons != null && level.hexagons.Length > 0
            ? (float)level.targetHexagonsToRemove / level.hexagons.Length
            : 0;
        
        Rect targetRect = GUILayoutUtility.GetRect(100, 20, GUILayout.ExpandWidth(true));
        EditorGUI.ProgressBar(targetRect, targetRatio, $"Target: {level.targetHexagonsToRemove} / {level.hexagons?.Length ?? 0}");
        EditorGUILayout.EndHorizontal();
    }
    
    private void DrawGridPreview(LevelConfig level)
    {
        if (level.gridConfig == null || level.gridConfig.gridRows == null || level.gridConfig.gridRows.Length == 0)
            return;
        
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        
        // Preview controls
        EditorGUILayout.BeginHorizontal();
        
        if (GUILayout.Button("Select All", EditorStyles.miniButtonLeft))
        {
            SelectAllCells(level);
        }
        
        if (GUILayout.Button("Clear All", EditorStyles.miniButtonMid))
        {
            if (EditorUtility.DisplayDialog("Clear All Hexagons", "Are you sure you want to remove all hexagons?", "Yes", "No"))
            {
                ClearAllHexagons(level);
            }
        }
        
        if (GUILayout.Button("Randomize Colors", EditorStyles.miniButtonMid))
        {
            RandomizeHexagonColors(level);
        }
        
        if (GUILayout.Button("Randomize Directions", EditorStyles.miniButtonRight))
        {
            RandomizeHexagonDirections(level);
        }
        
        EditorGUILayout.EndHorizontal();
        
        // Display options
        EditorGUILayout.BeginHorizontal();
        showCoordinates = EditorGUILayout.ToggleLeft("Show Coordinates", showCoordinates, GUILayout.Width(130));
        showDirectionIndicators = EditorGUILayout.ToggleLeft("Show Directions", showDirectionIndicators, GUILayout.Width(130));
        
        GUILayout.FlexibleSpace();
        
        GUILayout.Label("Zoom:", GUILayout.Width(40));
        previewScale = EditorGUILayout.Slider(previewScale, 0.5f, 1.5f, GUILayout.Width(100));
        
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space();
        
        // Draw the grid
        gridScrollPosition = EditorGUILayout.BeginScrollView(gridScrollPosition, GUILayout.Height(300));
        
        float adjustedCellSize = cellSize * previewScale;
        float adjustedSpacing = cellSpacing * previewScale;
        
        GridConfig gridConfig = level.gridConfig;
        
        EditorGUILayout.BeginVertical();
        for (int row = 0; row < gridConfig.gridRows.Length; row++)
        {
            GridRow gridRow = gridConfig.gridRows[row];
            if (gridRow == null || gridRow.cells == null)
                continue;
            
            EditorGUILayout.BeginHorizontal();
            
            // Add horizontal offset for odd rows (hexagonal grid)
            if (row % 2 == 1)
            {
                GUILayout.Space(adjustedCellSize * 0.5f);
            }
            
            for (int col = 0; col < gridRow.cells.Length; col++)
            {
                if (gridRow.cells[col])
                {
                    Vector2Int cellCoord = new Vector2Int(col, row);
                    int hexIndex = FindHexagonDataIndex(level, cellCoord);
                    
                    // Prepare button content and style
                    GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
                    buttonStyle.normal.textColor = hexIndex >= 0 ? Color.white : new Color(0.5f, 0.5f, 0.5f);
                    buttonStyle.fontSize = Mathf.RoundToInt(12 * previewScale);
                    
                    // Change background color if selected
                    Color originalBg = GUI.backgroundColor;
                    if (hexIndex >= 0 && selectedHexagonDataIndex == hexIndex)
                    {
                        GUI.backgroundColor = Color.yellow;
                    }
                    else if (hexIndex >= 0)
                    {
                        // Color based on hexagon's color
                        int colorIndex = level.hexagons[hexIndex].colorIndex;
                        if (colorIndex >= 0 && colorIndex < level.colorPalette.Length)
                        {
                            Color hexColor = level.colorPalette[colorIndex];
                            GUI.backgroundColor = new Color(hexColor.r, hexColor.g, hexColor.b, 0.7f);
                        }
                    }
                    
                    // Prepare button content
                    string buttonLabel = "";
                    
                    if (hexIndex >= 0)
                    {
                        HexagonData hexData = level.hexagons[hexIndex];
                        
                        if (showCoordinates)
                        {
                            buttonLabel += $"{col},{row}\n";
                        }
                        
                        buttonLabel += $"C:{hexData.colorIndex}";
                        
                        if (showDirectionIndicators)
                        {
                            // Add direction indicator
                            buttonLabel += "\n" + GetDirectionSymbol(hexData.direction);
                        }
                    }
                    else
                    {
                        buttonLabel = showCoordinates ? $"{col},{row}\n" : "";
                        buttonLabel += "+";
                    }
                    
                    // Draw the cell button
                    if (GUILayout.Button(buttonLabel, buttonStyle, 
                                        GUILayout.Width(adjustedCellSize), 
                                        GUILayout.Height(adjustedCellSize)))
                    {
                        if (hexIndex < 0)
                        {
                            // Add a new hexagon
                            AddHexagonData(level, cellCoord);
                            selectedHexagonDataIndex = FindHexagonDataIndex(level, cellCoord);
                            selectedCoord = cellCoord;
                        }
                        else
                        {
                            // Toggle selection
                            selectedHexagonDataIndex = (selectedHexagonDataIndex == hexIndex) ? -1 : hexIndex;
                            selectedCoord = cellCoord;
                        }
                    }
                    
                    // Reset background color
                    GUI.backgroundColor = originalBg;
                    
                    GUILayout.Space(adjustedSpacing);
                }
                else
                {
                    // Empty space where no cell exists
                    GUILayout.Space(adjustedCellSize + adjustedSpacing);
                }
            }
            
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(adjustedSpacing);
        }
        EditorGUILayout.EndVertical();
        
        EditorGUILayout.EndScrollView();
        
        // Draw hexagon editor panel if a hexagon is selected
        if (selectedHexagonDataIndex >= 0)
        {
            DrawHexagonEditor(level);
        }
        
        EditorGUILayout.EndVertical();
    }
    
    private void DrawHexagonEditor(LevelConfig level)
    {
        EditorGUILayout.Space();
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField($"Edit Hexagon at ({selectedCoord.x}, {selectedCoord.y})", EditorStyles.boldLabel);
        
        SerializedProperty hexagonDataProp = GetHexagonDataProperty(selectedHexagonDataIndex);
        
        // Direction selection
        EditorGUILayout.LabelField("Direction", EditorStyles.boldLabel);
        SerializedProperty dirProp = hexagonDataProp.FindPropertyRelative("direction");
        
        EditorGUILayout.BeginHorizontal();
        foreach (HexDirection dir in Enum.GetValues(typeof(HexDirection)))
        {
            GUIStyle dirButtonStyle = new GUIStyle(GUI.skin.button);
            bool isSelected = (dirProp.enumValueIndex == (int)dir);
            
            if (isSelected)
            {
                dirButtonStyle.normal.textColor = Color.yellow;
                dirButtonStyle.fontStyle = FontStyle.Bold;
            }
            
            // Use direction colors for buttons
            Color originalBg = GUI.backgroundColor;
            GUI.backgroundColor = directionColors[(int)dir];
            
            if (GUILayout.Button(dir.ToString(), dirButtonStyle))
            {
                dirProp.enumValueIndex = (int)dir;
            }
            
            GUI.backgroundColor = originalBg;
        }
        EditorGUILayout.EndHorizontal();
        
        // Graphical direction indicators
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        DrawDirectionIndicators(dirProp.enumValueIndex);
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space();
        
        // Color selection
        EditorGUILayout.LabelField("Hexagon Color", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        SerializedProperty colorIndexProp = hexagonDataProp.FindPropertyRelative("colorIndex");
        
        for (int i = 0; i < level.colorPalette.Length; i++)
        {
            Color col = level.colorPalette[i];
            GUIStyle colorButtonStyle = new GUIStyle(GUI.skin.button);
            Color originalBg = GUI.backgroundColor;
            GUI.backgroundColor = col;
            
            bool isColorSelected = (colorIndexProp.intValue == i);
            if (isColorSelected)
            {
                colorButtonStyle.normal.textColor = IsColorDark(col) ? Color.white : Color.black;
                colorButtonStyle.fontStyle = FontStyle.Bold;
            }
            else
            {
                colorButtonStyle.normal.textColor = IsColorDark(col) ? Color.white : Color.black;
            }
            
            if (GUILayout.Button(i.ToString(), colorButtonStyle, GUILayout.Width(40), GUILayout.Height(40)))
            {
                colorIndexProp.intValue = i;
            }
            
            GUI.backgroundColor = originalBg;
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space();
        
        // Remove button
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Remove Hexagon", GUILayout.Height(30)))
        {
            if (EditorUtility.DisplayDialog("Remove Hexagon", 
                                           $"Are you sure you want to remove the hexagon at ({selectedCoord.x}, {selectedCoord.y})?", 
                                           "Yes", "No"))
            {
                RemoveHexagonData(selectedHexagonDataIndex);
                selectedHexagonDataIndex = -1;
            }
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.EndVertical();
    }
    
    private void DrawHexagonList(LevelConfig level)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        
        if (level.hexagons == null || level.hexagons.Length == 0)
        {
            EditorGUILayout.HelpBox("No hexagons defined for this level.", MessageType.Info);
        }
        else
        {
            EditorGUILayout.LabelField($"Total Hexagons: {level.hexagons.Length}", EditorStyles.boldLabel);
            
            // Action buttons
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Sort by Coordinates"))
            {
                SortHexagonsByCoordinates(level);
            }
            
            if (GUILayout.Button("Sort by Color"))
            {
                SortHexagonsByColor(level);
            }
            EditorGUILayout.EndHorizontal();
            
            // List header
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUILayout.LabelField("Coordinates", EditorStyles.toolbarButton, GUILayout.Width(90));
            EditorGUILayout.LabelField("Color", EditorStyles.toolbarButton, GUILayout.Width(50));
            EditorGUILayout.LabelField("Direction", EditorStyles.toolbarButton, GUILayout.Width(100));
            EditorGUILayout.LabelField("Actions", EditorStyles.toolbarButton, GUILayout.Width(100));
            EditorGUILayout.EndHorizontal();
            
            // Scrollable list of hexagons
            hexListScrollPosition = EditorGUILayout.BeginScrollView(hexListScrollPosition, GUILayout.Height(200));
            
            for (int i = 0; i < level.hexagons.Length; i++)
            {
                HexagonData hexData = level.hexagons[i];
                
                // Create a style to highlight the selected item
                GUIStyle rowStyle = new GUIStyle();
                if (selectedHexagonDataIndex == i)
                {
                    rowStyle.normal.background = EditorGUIUtility.whiteTexture;
                    rowStyle.normal.textColor = Color.black;
                }
                
                EditorGUILayout.BeginHorizontal(rowStyle);
                
                // Coordinates
                EditorGUILayout.LabelField($"({hexData.coordinates.x}, {hexData.coordinates.y})", 
                                          GUILayout.Width(90));
                
                // Color preview
                if (hexData.colorIndex >= 0 && hexData.colorIndex < level.colorPalette.Length)
                {
                    Color hexColor = level.colorPalette[hexData.colorIndex];
                    Color originalBg = GUI.backgroundColor;
                    GUI.backgroundColor = hexColor;
                    GUILayout.Box($"{hexData.colorIndex}", GUILayout.Width(30), GUILayout.Height(20));
                    GUI.backgroundColor = originalBg;
                }
                else
                {
                    GUILayout.Box("N/A", GUILayout.Width(30), GUILayout.Height(20));
                }
                
                GUILayout.Space(20);
                
                // Direction
                GUIStyle dirStyle = new GUIStyle(EditorStyles.label);
                dirStyle.normal.textColor = directionColors[(int)hexData.direction];
                dirStyle.fontStyle = FontStyle.Bold;
                EditorGUILayout.LabelField(hexData.direction.ToString(), dirStyle, GUILayout.Width(100));
                
                // Action buttons
                if (GUILayout.Button("Select", EditorStyles.miniButtonLeft, GUILayout.Width(50)))
                {
                    selectedHexagonDataIndex = i;
                    selectedCoord = hexData.coordinates;
                }
                
                if (GUILayout.Button("X", EditorStyles.miniButtonRight, GUILayout.Width(25)))
                {
                    if (EditorUtility.DisplayDialog("Remove Hexagon", 
                                                  $"Are you sure you want to remove the hexagon at ({hexData.coordinates.x}, {hexData.coordinates.y})?", 
                                                  "Yes", "No"))
                    {
                        RemoveHexagonData(i);
                        if (selectedHexagonDataIndex == i)
                        {
                            selectedHexagonDataIndex = -1;
                        }
                        break;
                    }
                }
                
                EditorGUILayout.EndHorizontal();
                
                if (i < level.hexagons.Length - 1)
                {
                    EditorGUILayout.Space(2);
                }
            }
            
            EditorGUILayout.EndScrollView();
        }
        
        EditorGUILayout.EndVertical();
    }
    
    private void DrawDirectionIndicators(int directionIndex)
    {
        // Draw a graphical representation of the direction
        Rect indicatorRect = GUILayoutUtility.GetRect(80, 80);
        
        Handles.BeginGUI();
        
        // Draw a circle
        Handles.color = Color.gray;
        Vector3 center = new Vector3(indicatorRect.x + indicatorRect.width / 2, indicatorRect.y + indicatorRect.height / 2, 0);
        Handles.DrawWireDisc(center, Vector3.forward, 35);
        
        // Draw the direction arrow
        Handles.color = directionColors[directionIndex];
        float angle = GetDirectionAngle((HexDirection)directionIndex);
        Vector3 direction = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad), 0).normalized;
        Vector3 start = center;
        Vector3 end = start + direction * 30;
        
        Handles.DrawLine(start, end);
        
        // Draw arrowhead
        Vector3 right = Quaternion.Euler(0, 0, -30) * direction * 10;
        Vector3 left = Quaternion.Euler(0, 0, 30) * direction * 10;
        Handles.DrawLine(end, end - right);
        Handles.DrawLine(end, end - left);
        
        Handles.EndGUI();
    }
    
    private float GetDirectionAngle(HexDirection direction)
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
    
    private string GetDirectionSymbol(HexDirection direction)
    {
        switch (direction)
        {
            case HexDirection.East: return "→";
            case HexDirection.SouthEast: return "↘";
            case HexDirection.SouthWest: return "↙";
            case HexDirection.West: return "←";
            case HexDirection.NorthWest: return "↖";
            case HexDirection.NorthEast: return "↗";
            default: return "•";
        }
    }
    
    private bool IsColorDark(Color color)
    {
        // Determine if text should be white or black based on background brightness
        float brightness = color.r * 0.299f + color.g * 0.587f + color.b * 0.114f;
        return brightness < 0.5f;
    }
    
    /// <summary>
    /// Searches the Hexagons array for an element with matching coordinates.
    /// Returns the index if found, or -1 if not found.
    /// </summary>
    private int FindHexagonDataIndex(LevelConfig level, Vector2Int coord)
    {
        if (level.hexagons == null)
            return -1;
            
        for (int i = 0; i < level.hexagons.Length; i++)
        {
            if (level.hexagons[i].coordinates.x == coord.x && 
                level.hexagons[i].coordinates.y == coord.y)
            {
                return i;
            }
        }
        return -1;
    }
    
    /// <summary>
    /// Returns the SerializedProperty representing the HexagonData at the given index.
    /// </summary>
    private SerializedProperty GetHexagonDataProperty(int index)
    {
        SerializedProperty hexagonsProp = serializedObject.FindProperty("hexagons");
        return hexagonsProp.GetArrayElementAtIndex(index);
    }
    
    /// <summary>
    /// Adds a new HexagonData element with default values at the given coordinate.
    /// </summary>
    private void AddHexagonData(LevelConfig level, Vector2Int coord)
    {
        // Create array if it doesn't exist
        if (level.hexagons == null)
        {
            level.hexagons = new HexagonData[0];
        }
        
        // Create new data
        HexagonData newData = new HexagonData
        {
            coordinates = coord,
            direction = HexDirection.East,
            colorIndex = 0
        };
        
        // Add to array
        ArrayUtility.Add(ref level.hexagons, newData);
        serializedObject.Update();
    }
    
    /// <summary>
    /// Removes the HexagonData element at the specified index.
    /// </summary>
    private void RemoveHexagonData(int index)
    {
        SerializedProperty hexagonsProp = serializedObject.FindProperty("hexagons");
        hexagonsProp.DeleteArrayElementAtIndex(index);
    }
    
    /// <summary>
    /// Selects all cells in the grid that don't already have hexagons.
    /// </summary>
    private void SelectAllCells(LevelConfig level)
    {
        if (level.gridConfig == null || level.gridConfig.gridRows == null)
            return;
            
        GridConfig grid = level.gridConfig;
        bool anyAdded = false;
        
        for (int row = 0; row < grid.gridRows.Length; row++)
        {
            GridRow gridRow = grid.gridRows[row];
            if (gridRow == null || gridRow.cells == null)
                continue;
                
            for (int col = 0; col < gridRow.cells.Length; col++)
            {
                if (gridRow.cells[col])
                {
                    Vector2Int coord = new Vector2Int(col, row);
                    if (FindHexagonDataIndex(level, coord) < 0)
                    {
                        AddHexagonData(level, coord);
                        anyAdded = true;
                    }
                }
            }
        }
        
        if (anyAdded)
        {
            serializedObject.Update();
        }
    }
    
    /// <summary>
    /// Removes all hexagons from the level.
    /// </summary>
    private void ClearAllHexagons(LevelConfig level)
    {
        level.hexagons = new HexagonData[0];
        selectedHexagonDataIndex = -1;
        serializedObject.Update();
    }
    
    /// <summary>
    /// Randomizes the colors of all hexagons in the level.
    /// </summary>
    private void RandomizeHexagonColors(LevelConfig level)
    {
        if (level.hexagons == null || level.hexagons.Length == 0 || 
            level.colorPalette == null || level.colorPalette.Length == 0)
            return;
            
        for (int i = 0; i < level.hexagons.Length; i++)
        {
            level.hexagons[i].colorIndex = UnityEngine.Random.Range(0, level.colorPalette.Length);
        }
        
        serializedObject.Update();
    }
    
    /// <summary>
    /// Randomizes the directions of all hexagons in the level.
    /// </summary>
    private void RandomizeHexagonDirections(LevelConfig level)
    {
        if (level.hexagons == null || level.hexagons.Length == 0)
            return;
            
        for (int i = 0; i < level.hexagons.Length; i++)
        {
            level.hexagons[i].direction = (HexDirection)UnityEngine.Random.Range(0, 6);
        }
        
        serializedObject.Update();}
    
    /// <summary>
    /// Sorts hexagons by their grid coordinates.
    /// </summary>
    private void SortHexagonsByCoordinates(LevelConfig level)
    {
        if (level.hexagons == null || level.hexagons.Length <= 1)
            return;
            
        Array.Sort(level.hexagons, (a, b) => {
            int rowCompare = a.coordinates.y.CompareTo(b.coordinates.y);
            return rowCompare != 0 ? rowCompare : a.coordinates.x.CompareTo(b.coordinates.x);
        });
        
        serializedObject.Update();
    }
    
    /// <summary>
    /// Sorts hexagons by their color index.
    /// </summary>
    private void SortHexagonsByColor(LevelConfig level)
    {
        if (level.hexagons == null || level.hexagons.Length <= 1)
            return;
            
        Array.Sort(level.hexagons, (a, b) => a.colorIndex.CompareTo(b.colorIndex));
        
        serializedObject.Update();
    }
}