using UnityEditor;
using UnityEngine;
using HexaAway.Core;
using System;

[CustomEditor(typeof(LevelConfig))]
public class LevelConfigEditor : Editor
{
    // Size (in pixels) for each cell preview button
    private const float cellSize = 50f;
    // Spacing between cell buttons
    private const float cellSpacing = 5f;
    
    // Tracks the currently selected hexagon data index in the hexagons array; -1 means none selected.
    private int selectedHexagonDataIndex = -1;
    // Stores the coordinate of the selected cell
    private Vector2Int selectedCoord;

    public override void OnInspectorGUI()
    {
        LevelConfig level = (LevelConfig)target;
        serializedObject.Update();

        // -- Basic Level Fields --
        EditorGUILayout.LabelField("Level Info", EditorStyles.boldLabel);
        level.levelNumber = EditorGUILayout.IntField("Level Number", level.levelNumber);
        level.levelName = EditorGUILayout.TextField("Level Name", level.levelName);
        level.moveLimit = EditorGUILayout.IntField("Move Limit", level.moveLimit);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Colors", EditorStyles.boldLabel);
        SerializedProperty colorsProp = serializedObject.FindProperty("colorPalette");
        EditorGUILayout.PropertyField(colorsProp, true);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Grid Settings", EditorStyles.boldLabel);
        SerializedProperty gridConfigProp = serializedObject.FindProperty("gridConfig");
        EditorGUILayout.PropertyField(gridConfigProp);

        // -- Grid Preview --
        if (gridConfigProp.objectReferenceValue != null)
        {
            GridConfig gridConfig = (GridConfig)gridConfigProp.objectReferenceValue;
            if (gridConfig.gridRows != null && gridConfig.gridRows.Length > 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Grid Preview", EditorStyles.boldLabel);
                for (int row = 0; row < gridConfig.gridRows.Length; row++)
                {
                    GridRow gridRow = gridConfig.gridRows[row];
                    if (gridRow == null || gridRow.cells == null)
                        continue;
                    
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(10);
                    for (int col = 0; col < gridRow.cells.Length; col++)
                    {
                        if (gridRow.cells[col])
                        {
                            Vector2Int cellCoord = new Vector2Int(col, row);
                            int hexIndex = FindHexagonDataIndex(cellCoord);
                            string buttonLabel = "";
                            if (hexIndex >= 0)
                            {
                                // Get the current hexagon data for display.
                                SerializedProperty hexagonDataProp = GetHexagonDataProperty(hexIndex);
                                SerializedProperty dirProp = hexagonDataProp.FindPropertyRelative("direction");
                                SerializedProperty colorIndexProp = hexagonDataProp.FindPropertyRelative("colorIndex");
                                buttonLabel = $"C:{colorIndexProp.intValue}\n{((HexDirection)dirProp.enumValueIndex).ToString()}";
                            }
                            else
                            {
                                buttonLabel = "Empty";
                            }

                            // Create a style that highlights the selected cell.
                            GUIStyle style = new GUIStyle(GUI.skin.button);
                            if (hexIndex >= 0 && selectedHexagonDataIndex == hexIndex)
                            {
                                style.normal.textColor = Color.yellow;
                            }

                            if (GUILayout.Button(buttonLabel, style, GUILayout.Width(cellSize), GUILayout.Height(cellSize)))
                            {
                                if (hexIndex < 0)
                                {
                                    // Add a new hexagon data if none exists, then select it.
                                    AddHexagonData(cellCoord);
                                    selectedHexagonDataIndex = FindHexagonDataIndex(cellCoord);
                                    selectedCoord = cellCoord;
                                }
                                else
                                {
                                    // Toggle selection: if already selected, deselect; otherwise, select this cell.
                                    if (selectedHexagonDataIndex == hexIndex)
                                        selectedHexagonDataIndex = -1;
                                    else
                                    {
                                        selectedHexagonDataIndex = hexIndex;
                                        selectedCoord = cellCoord;
                                    }
                                }
                            }
                            GUILayout.Space(cellSpacing);
                        }
                        else
                        {
                            GUILayout.Space(cellSize + cellSpacing);
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                    GUILayout.Space(cellSpacing);
                }
            }
        }

        // -- Hexagon Data Editing Panel --
        if (selectedHexagonDataIndex >= 0)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Edit Hexagon Data", EditorStyles.boldLabel);
            SerializedProperty hexagonDataProp = GetHexagonDataProperty(selectedHexagonDataIndex);
            
            // Display the coordinate (read-only).
            SerializedProperty coordinatesProp = hexagonDataProp.FindPropertyRelative("coordinates");
            int x = coordinatesProp.FindPropertyRelative("x").intValue;
            int y = coordinatesProp.FindPropertyRelative("y").intValue;
            EditorGUILayout.LabelField("Coordinates", $"({x}, {y})");

            // -- Direction Selection --
            EditorGUILayout.LabelField("Direction");
            EditorGUILayout.BeginHorizontal();
            foreach (HexDirection dir in Enum.GetValues(typeof(HexDirection)))
            {
                GUIStyle dirButtonStyle = new GUIStyle(GUI.skin.button);
                SerializedProperty dirProp = hexagonDataProp.FindPropertyRelative("direction");
                bool isSelected = (dirProp.enumValueIndex == (int)dir);
                if (isSelected)
                    dirButtonStyle.normal.textColor = Color.yellow;
                if (GUILayout.Button(dir.ToString(), dirButtonStyle))
                {
                    dirProp.enumValueIndex = (int)dir;
                }
            }
            EditorGUILayout.EndHorizontal();

            // -- Color Selection --
            EditorGUILayout.LabelField("Hexagon Color");
            EditorGUILayout.BeginHorizontal();
            for (int i = 0; i < level.colorPalette.Length; i++)
            {
                Color col = level.colorPalette[i];
                GUIStyle colorButtonStyle = new GUIStyle(GUI.skin.button);
                Color originalBg = GUI.backgroundColor;
                GUI.backgroundColor = col;
                SerializedProperty colorIndexProp = hexagonDataProp.FindPropertyRelative("colorIndex");
                bool isColorSelected = (colorIndexProp.intValue == i);
                if (isColorSelected)
                    colorButtonStyle.normal.textColor = Color.black;
                if (GUILayout.Button(i.ToString(), colorButtonStyle, GUILayout.Width(40), GUILayout.Height(40)))
                {
                    colorIndexProp.intValue = i;
                }
                GUI.backgroundColor = originalBg;
            }
            EditorGUILayout.EndHorizontal();

            // -- Remove Button --
            if (GUILayout.Button("Remove Hexagon"))
            {
                if (EditorUtility.DisplayDialog("Remove Hexagon?", $"Remove hexagon at ({selectedCoord.x}, {selectedCoord.y})?", "Remove", "Cancel"))
                {
                    RemoveHexagonData(selectedHexagonDataIndex);
                    selectedHexagonDataIndex = -1;
                }
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Hexagons", EditorStyles.boldLabel);
        SerializedProperty hexagonsArrayProp = serializedObject.FindProperty("hexagons");
        EditorGUILayout.PropertyField(hexagonsArrayProp, true);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Level Objectives", EditorStyles.boldLabel);
        level.targetHexagonsToRemove = EditorGUILayout.IntField("Target Hexagons To Remove", level.targetHexagonsToRemove);

        serializedObject.ApplyModifiedProperties();
    }

    /// <summary>
    /// Searches the Hexagons array for an element with matching coordinates.
    /// Returns the index if found, or -1 if not found.
    /// </summary>
    private int FindHexagonDataIndex(Vector2Int coord)
    {
        SerializedProperty hexagonsProp = serializedObject.FindProperty("hexagons");
        for (int i = 0; i < hexagonsProp.arraySize; i++)
        {
            SerializedProperty element = hexagonsProp.GetArrayElementAtIndex(i);
            SerializedProperty coordinatesProp = element.FindPropertyRelative("coordinates");
            int x = coordinatesProp.FindPropertyRelative("x").intValue;
            int y = coordinatesProp.FindPropertyRelative("y").intValue;
            if (x == coord.x && y == coord.y)
                return i;
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
    private void AddHexagonData(Vector2Int coord)
    {
        SerializedProperty hexagonsProp = serializedObject.FindProperty("hexagons");
        hexagonsProp.arraySize++;
        SerializedProperty newElement = hexagonsProp.GetArrayElementAtIndex(hexagonsProp.arraySize - 1);
        SerializedProperty coordinatesProp = newElement.FindPropertyRelative("coordinates");
        coordinatesProp.FindPropertyRelative("x").intValue = coord.x;
        coordinatesProp.FindPropertyRelative("y").intValue = coord.y;
        SerializedProperty dirProp = newElement.FindPropertyRelative("direction");
        dirProp.enumValueIndex = (int)HexDirection.East;
        SerializedProperty colorIndexProp = newElement.FindPropertyRelative("colorIndex");
        colorIndexProp.intValue = 0;
    }

    /// <summary>
    /// Removes the HexagonData element at the specified index.
    /// </summary>
    private void RemoveHexagonData(int index)
    {
        SerializedProperty hexagonsProp = serializedObject.FindProperty("hexagons");
        hexagonsProp.DeleteArrayElementAtIndex(index);
    }
}
