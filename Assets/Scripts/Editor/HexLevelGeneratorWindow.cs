using UnityEditor;
using UnityEngine;

namespace HexaAway.Core
{
    /// <summary>
    /// Editor window for level generation with UI controls
    /// </summary>
#if UNITY_EDITOR
    public class HexLevelGeneratorWindow : EditorWindow
    {
        private DifficultyConfig difficultyConfig = new DifficultyConfig();
        private GridConfig templateGridConfig;
        private int levelCount = 10;
        private int startLevelNumber = 1;
        private string savePath = "Assets/Levels";
        private Vector2 scrollPosition;
        
        [MenuItem("HexaAway/Level Generator Window")]
        public static void ShowWindow()
        {
            GetWindow<HexLevelGeneratorWindow>("Hex Level Generator");
        }
        
        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            EditorGUILayout.LabelField("Grid Configuration", EditorStyles.boldLabel);
            
            templateGridConfig = (GridConfig)EditorGUILayout.ObjectField(
                "Template Grid", templateGridConfig, typeof(GridConfig), false);
                
            if (templateGridConfig == null)
            {
                EditorGUILayout.HelpBox("Please assign a template GridConfig asset", MessageType.Warning);
            }
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Difficulty Configuration", EditorStyles.boldLabel);
            
            difficultyConfig.targetMoveCount = EditorGUILayout.IntSlider("Target Move Count", difficultyConfig.targetMoveCount, 1, 30);
            difficultyConfig.hexagonCount = EditorGUILayout.IntSlider("Hexagon Count", difficultyConfig.hexagonCount, 3, 30);
            difficultyConfig.directionChangeRate = EditorGUILayout.Slider("Direction Change Rate", difficultyConfig.directionChangeRate, 0, 1);
            difficultyConfig.bottleneckCount = EditorGUILayout.IntSlider("Bottleneck Count", difficultyConfig.bottleneckCount, 0, 5);
            difficultyConfig.specialHexagonRate = EditorGUILayout.Slider("Special Hexagon Rate", difficultyConfig.specialHexagonRate, 0, 1);
            difficultyConfig.difficultyTolerance = EditorGUILayout.Slider("Difficulty Tolerance", difficultyConfig.difficultyTolerance, 0, 0.5f);
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Level Objectives", EditorStyles.boldLabel);
            
            difficultyConfig.hexagonsToRemovePercentage = EditorGUILayout.Slider("Hexagons to Remove %", difficultyConfig.hexagonsToRemovePercentage, 0.1f, 1f);
            difficultyConfig.moveLimitMultiplier = EditorGUILayout.Slider("Move Limit Multiplier", difficultyConfig.moveLimitMultiplier, 1f, 3f);
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Generation Settings", EditorStyles.boldLabel);
            
            levelCount = EditorGUILayout.IntField("Number of Levels", levelCount);
            startLevelNumber = EditorGUILayout.IntField("Starting Level Number", startLevelNumber);
            savePath = EditorGUILayout.TextField("Save Path", savePath);
            
            if (GUILayout.Button("Browse..."))
            {
                string path = EditorUtility.SaveFolderPanel("Save Generated Levels", "Assets", "GeneratedLevels");
                if (!string.IsNullOrEmpty(path))
                {
                    string projectPath = System.IO.Path.GetFullPath("Assets");
                    if (path.StartsWith(projectPath))
                    {
                        savePath = "Assets" + path.Substring(projectPath.Length);
                    }
                }
            }
            
            EditorGUILayout.Space();
            
            if (GUILayout.Button("Generate Levels"))
            {
                if (templateGridConfig == null)
                {
                    EditorUtility.DisplayDialog("Error", "Please assign a template GridConfig asset", "OK");
                    return;
                }
                
                GenerateLevels();
            }
            
            if (GUILayout.Button("Preview Single Level"))
            {
                if (templateGridConfig == null)
                {
                    EditorUtility.DisplayDialog("Error", "Please assign a template GridConfig asset", "OK");
                    return;
                }
                
                PreviewLevel();
            }
            
            EditorGUILayout.EndScrollView();
        }
        
        private void GenerateLevels()
        {
            // Create a temporary generator
            HexLevelGenerator generator = CreateGenerator();
            generator.GenerateLevels(levelCount, savePath, startLevelNumber);
            DestroyImmediate(generator.gameObject);
        }
        
        private void PreviewLevel()
        {
            // Create a temporary generator
            HexLevelGenerator generator = CreateGenerator();
            LevelConfig level = generator.GenerateLevel(startLevelNumber);
            
            // Display the level
            string preview = $"Level Preview ({level.levelName})\n";
            preview += $"Hexagons: {level.hexagons.Length}\n";
            preview += $"Move Limit: {level.moveLimit}\n";
            preview += $"Target to Remove: {level.targetHexagonsToRemove}\n\n";
            
            preview += "Hexagons:\n";
            foreach (var hex in level.hexagons)
            {
                preview += $"- Hex({hex.coordinates.x},{hex.coordinates.y}) Dir:{hex.direction} Color:{hex.colorIndex}\n";
            }
            
            Debug.Log(preview);
            
            // Clean up
            DestroyImmediate(generator.gameObject);
        }
        
        private HexLevelGenerator CreateGenerator()
        {
            GameObject go = new GameObject("TempLevelGenerator");
            HexLevelGenerator generator = go.AddComponent<HexLevelGenerator>();
            generator.templateGridConfig = templateGridConfig;
            generator.difficultyConfig = difficultyConfig;
            return generator;
        }
    }
#endif
}