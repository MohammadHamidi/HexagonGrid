using System.Collections.Generic;
using UnityEngine;

namespace HexaAway.Core
{
    public class LevelGenerator : MonoBehaviour
    {
        [Header("Level Generation Settings")]
        [SerializeField] private int defaultHexagonCount = 7;
        [SerializeField] private int minHexagonCount = 3;
        [SerializeField] private int maxHexagonCount = 12;
        
        [Header("Grid Settings")]
        [SerializeField] private int gridRadius = 2; // How far from center to place hexagons
        
        [Header("Colors")]
        [SerializeField] private Color[] colorPalette = new Color[]
        {
            new Color(0.2f, 0.6f, 1f),    // Blue
            new Color(0.4f, 0.9f, 0.4f),  // Green
            new Color(1f, 0.4f, 0.4f)     // Red
        };
        
        [Header("Game Settings")]
        [SerializeField] private int baseMoveLimit = 10;
        [SerializeField] private float moveLimitMultiplier = 1.5f;
        
        private GameManager gameManager;
        
        private void Awake()
        {
            gameManager = GetComponent<GameManager>();
            
            // If we don't have a game manager on this object, try to find one
            if (gameManager == null)
            {
                gameManager = FindObjectOfType<GameManager>();
            }
        }
        
        private void Start()
        {
            // Wait for other components to initialize
            Invoke(nameof(GenerateTestLevel), 0.5f); // Increased delay for grid to initialize
        }
        
        public void GenerateTestLevel()
        {
            if (gameManager == null)
            {
                Debug.LogError("GameManager not found!");
                return;
            }
            
            // Generate a test level
            LevelConfig testLevel = GenerateRandomLevel(1, "Test Level");
            
            // Set it in the game manager using reflection (since the levels array is private)
            var levelsField = typeof(GameManager).GetField("levels", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (levelsField != null)
            {
                levelsField.SetValue(gameManager, new LevelConfig[] { testLevel });
                
                // Restart the current level to apply changes
                gameManager.RestartLevel();
            }
            else
            {
                Debug.LogError("Couldn't access levels field in GameManager");
            }
        }
        
        public LevelConfig GenerateRandomLevel(int levelNumber, string levelName)
        {
            // Create a new level config
            LevelConfig level = ScriptableObject.CreateInstance<LevelConfig>();
            level.levelNumber = levelNumber;
            level.levelName = levelName;
            
            // Determine number of hexagons for this level
            int hexagonCount = Mathf.Min(minHexagonCount + levelNumber, maxHexagonCount);
            if (hexagonCount <= 0)
                hexagonCount = defaultHexagonCount;
                
            // Set the target number of hexagons to remove (always less than total count)
            level.targetHexagonsToRemove = Mathf.Max(1, hexagonCount - 2);
            
            // Set move limit (scales with level difficulty)
            level.moveLimit = Mathf.RoundToInt(baseMoveLimit + hexagonCount * moveLimitMultiplier);
            
            // Set color palette
            level.colorPalette = colorPalette;
            
            // Get valid cells from the grid
            List<Vector2Int> validCells = GetValidCellCoordinates();
            
            if (validCells.Count == 0)
            {
                Debug.LogError("No valid cells found in the grid!");
                // Create a simple pattern if no cells are available
                level.hexagons = new HexagonData[]
                {
                    new HexagonData { coordinates = new Vector2Int(0, 0), direction = HexDirection.East, colorIndex = 0 }
                };
                return level;
            }
            
            // Generate hexagon data
            level.hexagons = GenerateHexagonData(hexagonCount, validCells);
            
            return level;
        }
        
        private List<Vector2Int> GetValidCellCoordinates()
        {
            if (GridManager.Instance == null)
            {
                Debug.LogError("GridManager instance not found!");
                return new List<Vector2Int>();
            }
            
            // Get all cell coordinates from the grid
            List<Vector2Int> allCells = GridManager.Instance.GetAllCellCoordinates();
            
            // Log all cells for debugging
            string cellsStr = "Valid cells: ";
            foreach (Vector2Int cell in allCells)
            {
                cellsStr += $"({cell.x}, {cell.y}) ";
            }
            Debug.Log(cellsStr);
            
            return allCells;
        }
        
        private HexagonData[] GenerateHexagonData(int count, List<Vector2Int> validCells)
        {
            List<HexagonData> hexagons = new List<HexagonData>();
            HashSet<Vector2Int> usedCoordinates = new HashSet<Vector2Int>();
            
            // Shuffle the valid cells to ensure random placement
            ShuffleList(validCells);
            
            // Use as many cells as we need, up to the available count
            int cellsToUse = Mathf.Min(count, validCells.Count);
            
            for (int i = 0; i < cellsToUse; i++)
            {
                Vector2Int coords = validCells[i];
                
                // Create hexagon data
                HexagonData data = new HexagonData
                {
                    coordinates = coords,
                    direction = GetRandomDirection(),
                    colorIndex = Random.Range(0, colorPalette.Length)
                };
                
                hexagons.Add(data);
            }
            
            return hexagons.ToArray();
        }
        
        private void ShuffleList<T>(List<T> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = Random.Range(0, n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }
        
        private HexDirection GetRandomDirection()
        {
            return (HexDirection)Random.Range(0, 6);
        }
        
        // GUI for testing
        private void OnGUI()
        {
            if (GUI.Button(new Rect(10, 10, 120, 30), "Generate New Level"))
            {
                GenerateTestLevel();
            }
        }
    }
}