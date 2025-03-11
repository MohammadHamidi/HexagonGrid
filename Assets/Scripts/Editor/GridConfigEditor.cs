using UnityEditor;
using UnityEngine;
using HexaAway.Core;

[CustomEditor(typeof(GridConfig))]
public class GridConfigEditor : Editor
{
    public override void OnInspectorGUI()
    {
        GridConfig config = (GridConfig)target;
        
        // Draw the spacing settings
        config.hexHorizontalSpacing = EditorGUILayout.FloatField("Hex Horizontal Spacing", config.hexHorizontalSpacing);
        config.hexVerticalSpacing = EditorGUILayout.FloatField("Hex Vertical Spacing", config.hexVerticalSpacing);
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Grid Layout (2D Array)", EditorStyles.boldLabel);
        
        SerializedProperty gridRowsProp = serializedObject.FindProperty("gridRows");
        if (gridRowsProp != null)
        {
            if (GUILayout.Button("Add Row"))
            {
                ArrayUtility.Add(ref config.gridRows, new GridRow());
                serializedObject.Update();
            }
            
            // Loop through each row
            for (int i = 0; i < gridRowsProp.arraySize; i++)
            {
                SerializedProperty rowProp = gridRowsProp.GetArrayElementAtIndex(i);
                SerializedProperty cellsProp = rowProp.FindPropertyRelative("cells");
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Row " + i, GUILayout.Width(50));
                if (cellsProp != null)
                {
                    if (GUILayout.Button("Add Cell", GUILayout.Width(70)))
                    {
                        int newSize = cellsProp.arraySize + 1;
                        cellsProp.arraySize = newSize;
                    }
                    
                    // Draw each cell toggle
                    for (int j = 0; j < cellsProp.arraySize; j++)
                    {
                        SerializedProperty cellProp = cellsProp.GetArrayElementAtIndex(j);
                        cellProp.boolValue = EditorGUILayout.Toggle(cellProp.boolValue, GUILayout.Width(20));
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
        }
        
        serializedObject.ApplyModifiedProperties();
    }
}
