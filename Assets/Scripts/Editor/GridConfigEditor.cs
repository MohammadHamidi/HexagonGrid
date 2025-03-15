using UnityEditor;
using UnityEngine;
using HexaAway.Core;

[CustomEditor(typeof(GridConfig))]
public class GridConfigEditor : Editor
{
    private bool showAdvancedOptions = false;
    
    // For visual preview
    private float previewScale = 80;
    private Vector2 scrollPosition;
    private Color gridCellColor = new Color(0.8f, 0.8f, 0.8f, 0.3f);
    private Color gridLineColor = new Color(0.5f, 0.5f, 0.5f, 0.8f);
    private bool showCoordinates = true;
    
    public override void OnInspectorGUI()
    {
        GridConfig config = (GridConfig)target;
        serializedObject.Update();
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("HexaAway Grid Configuration", EditorStyles.boldLabel);
        EditorGUILayout.Space();
        
        // Basic settings with tooltips
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Spacing Settings", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        
        config.hexHorizontalSpacing = EditorGUILayout.FloatField(
            new GUIContent("Horizontal Spacing", "Distance between adjacent hexagons on the X axis"),
            config.hexHorizontalSpacing);
        
        config.hexVerticalSpacing = EditorGUILayout.FloatField(
            new GUIContent("Vertical Spacing", "Distance between adjacent rows of hexagons on the Z axis"),
            config.hexVerticalSpacing);
        
        EditorGUI.indentLevel--;
        EditorGUILayout.EndVertical();
        
        EditorGUILayout.Space();
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Grid Layout", EditorStyles.boldLabel);
        
        // Quick grid setup options
        EditorGUILayout.BeginHorizontal();
        
        if (GUILayout.Button("Create 3x3 Grid"))
        {
            CreatePredefinedGrid(3, 3);
        }
        
        if (GUILayout.Button("Create 5x5 Grid"))
        {
            CreatePredefinedGrid(5, 5);
        }
        
        if (GUILayout.Button("Create 7x5 Grid"))
        {
            CreatePredefinedGrid(7, 5);
        }
        
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space();
        
        // Grid size adjustment
        EditorGUILayout.BeginHorizontal();
        
        // Add/remove row buttons
        if (GUILayout.Button("Add Row"))
        {
            AddRow(config);
        }
        
        if (GUILayout.Button("Remove Last Row") && config.gridRows != null && config.gridRows.Length > 0)
        {
            ArrayUtility.RemoveAt(ref config.gridRows, config.gridRows.Length - 1);
        }
        
        EditorGUILayout.EndHorizontal();
        
        SerializedProperty gridRowsProp = serializedObject.FindProperty("gridRows");
        
        if (gridRowsProp != null && gridRowsProp.arraySize > 0)
        {
            // Find the maximum row width to standardize grid display
            int maxWidth = 0;
            for (int i = 0; i < gridRowsProp.arraySize; i++)
            {
                SerializedProperty rowProp = gridRowsProp.GetArrayElementAtIndex(i);
                SerializedProperty cellsProp = rowProp.FindPropertyRelative("cells");
                if (cellsProp != null && cellsProp.arraySize > maxWidth)
                {
                    maxWidth = cellsProp.arraySize;
                }
            }
            
            // Draw grid controls
            EditorGUILayout.Space();
            DrawGridControls(gridRowsProp, maxWidth);
            
            // Visual grid preview
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Grid Preview", EditorStyles.boldLabel);
            
            // Set a fixed size for the preview area
            Rect previewRect = GUILayoutUtility.GetRect(100, 300, GUILayout.ExpandWidth(true));
            DrawGridPreview(previewRect, config);
        }
        else
        {
            EditorGUILayout.HelpBox("No rows defined. Add rows to create a grid.", MessageType.Info);
        }
        
        EditorGUILayout.EndVertical();
        
        // Advanced options
        EditorGUILayout.Space();
        showAdvancedOptions = EditorGUILayout.Foldout(showAdvancedOptions, "Advanced Options");
        if (showAdvancedOptions)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            gridCellColor = EditorGUILayout.ColorField("Preview Cell Color", gridCellColor);
            gridLineColor = EditorGUILayout.ColorField("Preview Line Color", gridLineColor);
            showCoordinates = EditorGUILayout.Toggle("Show Coordinates", showCoordinates);
            previewScale = EditorGUILayout.Slider("Preview Scale", previewScale, 60, 160f);
            
            EditorGUILayout.EndVertical();
        }
        
        serializedObject.ApplyModifiedProperties();
    }
    
    private void DrawGridControls(SerializedProperty gridRowsProp, int maxWidth)
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(150));
        
        for (int i = 0; i < gridRowsProp.arraySize; i++)
        {
            SerializedProperty rowProp = gridRowsProp.GetArrayElementAtIndex(i);
            SerializedProperty cellsProp = rowProp.FindPropertyRelative("cells");
            
            EditorGUILayout.BeginHorizontal();
            
            // Row label
            EditorGUILayout.LabelField($"Row {i}", GUILayout.Width(50));
            
            // Row operations
            if (GUILayout.Button("+", GUILayout.Width(25)))
            {
                AddCell(cellsProp);
            }
            
            if (GUILayout.Button("-", GUILayout.Width(25)) && cellsProp.arraySize > 0)
            {
                cellsProp.arraySize--;
            }
            
            // Fill/clear row
            if (GUILayout.Button("Fill", GUILayout.Width(40)))
            {
                for (int j = 0; j < cellsProp.arraySize; j++)
                {
                    cellsProp.GetArrayElementAtIndex(j).boolValue = true;
                }
            }
            
            if (GUILayout.Button("Clear", GUILayout.Width(40)))
            {
                for (int j = 0; j < cellsProp.arraySize; j++)
                {
                    cellsProp.GetArrayElementAtIndex(j).boolValue = false;
                }
            }
            
            // Delete row
            if (GUILayout.Button("✕", GUILayout.Width(25)))
            {
                gridRowsProp.DeleteArrayElementAtIndex(i);
                break;
            }
            
            EditorGUILayout.EndHorizontal();
            
            // Cell toggles
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(60); // Space to align with row label
            
            if (cellsProp != null)
            {
                for (int j = 0; j < cellsProp.arraySize; j++)
                {
                    SerializedProperty cellProp = cellsProp.GetArrayElementAtIndex(j);
                    
                    // Offset every other row to represent hex grid
                    if (i % 2 == 1 && j == 0)
                    {
                        GUILayout.Space(15);
                    }
                    
                    cellProp.boolValue = EditorGUILayout.Toggle(cellProp.boolValue, GUILayout.Width(20));
                }
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(5);
        }
        
        EditorGUILayout.EndScrollView();
    }
    
    private void DrawGridPreview(Rect rect, GridConfig config)
    {
        if (config.gridRows == null || config.gridRows.Length == 0)
            return;
            
        GUI.Box(rect, "");
        
        // Calculate the hexagon dimensions
        float hexRadius = previewScale / 2;
        float hexWidth = hexRadius * 2;
        float hexHeight = hexRadius * Mathf.Sqrt(3);
        
        // Find grid bounds
        int maxColumns = 0;
        for (int i = 0; i < config.gridRows.Length; i++)
        {
            if (config.gridRows[i] != null && config.gridRows[i].cells != null)
            {
                maxColumns = Mathf.Max(maxColumns, config.gridRows[i].cells.Length);
            }
        }
        
        // Calculate total grid dimensions
        float totalWidth = maxColumns * hexWidth * 0.75f + hexWidth / 2;
        float totalHeight = config.gridRows.Length * hexHeight * 0.75f + hexHeight / 4;
        
        // Center the grid in the preview area
        float startX = rect.x + (rect.width - totalWidth) / 2 + hexRadius;
        float startY = rect.y + (rect.height - totalHeight) / 2 + hexRadius;
        
        // Draw all the hexagons
        for (int row = 0; row < config.gridRows.Length; row++)
        {
            GridRow gridRow = config.gridRows[row];
            if (gridRow == null || gridRow.cells == null)
                continue;
                
            // Calculate row y-position
            float y = startY + row * hexHeight * 0.75f;
            
            for (int col = 0; col < gridRow.cells.Length; col++)
            {
                // Calculate column x-position with odd row offset
                float x = startX + col * hexWidth * 0.75f;
                if (row % 2 == 1)
                {
                    x += hexWidth * 0.375f; // Half of 0.75f to offset odd rows
                }
                
                if (gridRow.cells[col])
                {
                    // Draw the hexagon
                    DrawHexagon(new Vector2(x, y), hexRadius, gridCellColor, gridLineColor);
                    
                    // Draw coordinates if enabled
                    if (showCoordinates)
                    {
                        Rect labelRect = new Rect(x - 15, y - 8, 30, 16);
                        GUI.Label(labelRect, $"{col},{row}", new GUIStyle(EditorStyles.miniLabel)
                        {
                            alignment = TextAnchor.MiddleCenter,
                            normal = { textColor = Color.black }
                        });
                    }
                }
            }
        }
    }
    
    private void DrawHexagon(Vector2 center, float radius, Color fillColor, Color lineColor)
    {
        // For pointy-top hexagon
        Vector3[] corners = new Vector3[6];
        for (int i = 0; i < 6; i++)
        {
            float angle = (i * 60f - 30f) * Mathf.Deg2Rad; // Start at top (-30 degrees)
            corners[i] = new Vector3(
                center.x + radius * Mathf.Cos(angle),
                center.y + radius * Mathf.Sin(angle),
                0
            );
        }
        
        // Draw filled hexagon
        Handles.BeginGUI();
        Handles.color = fillColor;
        Handles.DrawAAConvexPolygon(corners);
        
        // Draw outline
        Handles.color = lineColor;
        for (int i = 0; i < 6; i++)
        {
            Handles.DrawLine(corners[i], corners[(i + 1) % 6]);
        }
        Handles.EndGUI();
    }
    
    private void AddRow(GridConfig config)
    {
        // Create new row with same width as the last row
        GridRow newRow = new GridRow();
        if (config.gridRows != null && config.gridRows.Length > 0)
        {
            GridRow lastRow = config.gridRows[config.gridRows.Length - 1];
            if (lastRow != null && lastRow.cells != null)
            {
                newRow.cells = new bool[lastRow.cells.Length];
            }
            else
            {
                newRow.cells = new bool[5]; // Default size
            }
        }
        else
        {
            newRow.cells = new bool[5]; // Default size
        }
        
        ArrayUtility.Add(ref config.gridRows, newRow);
    }
    
    private void AddCell(SerializedProperty cellsProp)
    {
        int newSize = cellsProp.arraySize + 1;
        cellsProp.arraySize = newSize;
    }
    
    private void CreatePredefinedGrid(int width, int height)
    {
        GridConfig config = (GridConfig)target;
        
        // Create rows
        config.gridRows = new GridRow[height];
        
        for (int i = 0; i < height; i++)
        {
            config.gridRows[i] = new GridRow();
            config.gridRows[i].cells = new bool[width];
            
            // Default to all cells active
            for (int j = 0; j < width; j++)
            {
                config.gridRows[i].cells[j] = true;
            }
        }
        
        serializedObject.Update();
    }
    
    // Add this method for more accurate representation of the grid in the Scene view
    [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
    private static void DrawGizmos(GridConfig gridConfig, GizmoType gizmoType)
    {
        if (gridConfig?.gridRows == null || gridConfig.gridRows.Length == 0)
            return;
            
        // Draw the grid in the scene view to help with visualization
        for (int row = 0; row < gridConfig.gridRows.Length; row++)
        {
            GridRow gridRow = gridConfig.gridRows[row];
            if (gridRow == null || gridRow.cells == null)
                continue;
                
            for (int col = 0; col < gridRow.cells.Length; col++)
            {
                if (gridRow.cells[col])
                {
                    // Calculate world position based on grid system
                    float horizontalSpacing = gridConfig.hexHorizontalSpacing;
                    float verticalSpacing = gridConfig.hexVerticalSpacing;
                    
                    float xPos = col * horizontalSpacing;
                    if (row % 2 == 1)
                        xPos += horizontalSpacing * 0.5f;
                        
                    float zPos = row * verticalSpacing;
                    
                    Vector3 position = new Vector3(xPos, 0, zPos);
                    
                    // Draw hex outline
                    DrawHexGizmo(position, horizontalSpacing / 2, (row * 100 + col) % 2 == 0 ? 
                                 Color.gray : new Color(0.7f, 0.7f, 0.7f, 0.5f));
                }
            }
        }
    }
    
    private static void DrawHexGizmo(Vector3 center, float radius, Color color)
    {
        // For a pointy-top hexagon in 3D space
        Vector3[] vertices = new Vector3[6];
        for (int i = 0; i < 6; i++)
        {
            float angle = (i * 60f - 30f) * Mathf.Deg2Rad; // Start at top
            vertices[i] = center + new Vector3(
                radius * Mathf.Cos(angle),
                0,
                radius * Mathf.Sin(angle)
            );
        }
        
        Gizmos.color = color;
        for (int i = 0; i < 6; i++)
        {
            Gizmos.DrawLine(vertices[i], vertices[(i + 1) % 6]);
        }
        
        // Draw coordinate
        Handles.color = Color.black;
        Handles.Label(center + Vector3.up * 0.1f, $"({center.x:F1},{center.z:F1})");
    }
}