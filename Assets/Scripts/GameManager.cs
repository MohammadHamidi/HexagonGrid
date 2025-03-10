using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HexaAway.Core
{
    public class GameManager : MonoBehaviour
    {
        [Header("References")]
        public GridManager gridManager;
        
        [Header("Prefabs")]
        [SerializeField] private GameObject hexagonPrefab;
        
        [Header("Level Settings")]
        [SerializeField] private LevelConfig[] levels;
        [SerializeField] private int currentLevelIndex = 0;
        
        // Game state
        private int movesUsed = 0;
        private int hexagonsRemoved = 0;
        private LevelConfig currentLevel;
        
        // Events
        public event Action<int, int> OnMovesUpdated; // current/total
        public event Action<int, int> OnHexagonsUpdated; // removed/target
        public event Action OnLevelCompleted;
        public event Action OnLevelFailed;
        
        // Singleton pattern
        public static GameManager Instance { get; private set; }
        
        private void Awake()
        {
            // Setup singleton
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }
        
        private void Start()
        {
            // Start the game
            StartLevel(currentLevelIndex);
        }
        
        public void StartLevel(int levelIndex)
        {
            // Validate level index
            if (levelIndex < 0 || levelIndex >= levels.Length)
            {
                Debug.LogError($"Invalid level index: {levelIndex}");
                return;
            }
            
            // Reset game state
            movesUsed = 0;
            hexagonsRemoved = 0;
            
            // Set current level
            currentLevelIndex = levelIndex;
            currentLevel = levels[levelIndex];
            
            // Generate grid if needed
            if (gridManager == null)
            {
                gridManager = FindObjectOfType<GridManager>();
                
                if (gridManager == null)
                {
                    Debug.LogError("Grid Manager not found!");
                    return;
                }
            }
            
            // Clear any existing hexagons
            ClearAllHexagons();
            
            // Create hexagons based on level config
            CreateLevelHexagons();
            
            // Notify UI of initial state
            UpdateUI();
        }
        
        private void CreateLevelHexagons()
        {
            if (currentLevel == null || currentLevel.hexagons == null)
                return;
        
            foreach (HexagonData hexData in currentLevel.hexagons)
            {
                // Get the cell at these coordinates
                HexCell cell = gridManager.GetCell(hexData.coordinates);
        
                if (cell != null && !cell.IsOccupied)
                {
                    // Instead of creating a single hexagon, create a stack
                    CreateHexagonStack(cell, hexData.direction, hexData.colorIndex);
                }
                else
                {
                    Debug.LogWarning($"Unable to place hexagon at {hexData.coordinates}. Cell not found or already occupied.");
                }
            }
        }

        private void CreateHexagonStack(HexCell cell, HexDirection direction, int colorIndex)
        {
            // Get color from palette
            Color hexColor = Color.white;
            if (currentLevel != null && colorIndex >= 0 && colorIndex < currentLevel.colorPalette.Length)
            {
                hexColor = currentLevel.colorPalette[colorIndex];
            }
    
            // Create a parent GameObject for the stack at the cell's position (with a small Y offset)
            GameObject stackObj = new GameObject($"HexagonStack_{cell.Coordinates.x}_{cell.Coordinates.y}");
            stackObj.transform.position = cell.transform.position + new Vector3(0, 0.2f, 0);
    
            // Optionally add a collider for click detection (so the stack handles clicks instead of each hexagon)
            BoxCollider collider = stackObj.AddComponent<BoxCollider>();
            collider.size = new Vector3(1, 1, 1);
    
            // Add the HexagonStack script (see code below)
            HexagonStack hexagonStack = stackObj.AddComponent<HexagonStack>();
            hexagonStack.Initialize(hexagonPrefab, hexColor, direction, cell);
        }

        
        private Hexagon CreateHexagon(HexCell cell, HexDirection direction, int colorIndex)
        {
            if (cell == null || hexagonPrefab == null)
                return null;
                
            // Get color from palette
            Color hexColor = Color.white;
            if (currentLevel != null && colorIndex >= 0 && colorIndex < currentLevel.colorPalette.Length)
            {
                hexColor = currentLevel.colorPalette[colorIndex];
            }
            
            // Create the hexagon object
            GameObject hexObject = Instantiate(hexagonPrefab, cell.transform.position+new Vector3(0,0.2f,0), Quaternion.identity);
            hexObject.name = $"Hexagon_{cell.Coordinates.x}_{cell.Coordinates.y}";
            
            // Get the Hexagon component
            Hexagon hexagon = hexObject.GetComponent<Hexagon>();
            if (hexagon == null)
                hexagon = hexObject.AddComponent<Hexagon>();
                
            // Initialize hexagon
            hexagon.Initialize(hexColor, direction);
            
            // Subscribe to unlock event
            hexagon.OnHexagonUnlocked += OnHexagonUnlocked;
            
            // Place on the cell
            cell.PlaceHexagon(hexagon);
            
            return hexagon;
        }
        
        private void OnHexagonUnlocked(Hexagon hexagon)
        {
            // Unsubscribe from the event
            hexagon.OnHexagonUnlocked -= OnHexagonUnlocked;
            
            // Increment moves used
            movesUsed++;
            
            // Update UI
            UpdateUI();
            
            // Check for level completion/failure
            CheckLevelState();
        }
        
        public void OnHexagonRemoved()
        {
            // Increment hexagons removed counter
            hexagonsRemoved++;
            
            // Update UI
            UpdateUI();
            
            // Check for level completion
            CheckLevelState();
        }
        
        private void CheckLevelState()
        {
            if (currentLevel == null)
                return;
                
            // Check if level completed
            if (hexagonsRemoved >= currentLevel.targetHexagonsToRemove)
            {
                // Level completed
                OnLevelCompleted?.Invoke();
                
                // Show completion UI or load next level
                Debug.Log("Level completed!");
            }
            // Check if level failed (out of moves)
            else if (movesUsed >= currentLevel.moveLimit)
            {
                // Level failed
                OnLevelFailed?.Invoke();
                
                // Show failure UI
                Debug.Log("Level failed - out of moves!");
            }
        }
        
        private void UpdateUI()
        {
            if (currentLevel == null)
                return;
                
            // Update moves
            OnMovesUpdated?.Invoke(movesUsed, currentLevel.moveLimit);
            
            // Update hexagons
            OnHexagonsUpdated?.Invoke(hexagonsRemoved, currentLevel.targetHexagonsToRemove);
        }
        
        private void ClearAllHexagons()
        {
            // Find all hexagons in the scene
            Hexagon[] hexagons = FindObjectsOfType<Hexagon>();
            
            // Destroy them
            foreach (Hexagon hex in hexagons)
            {
                if (hex != null)
                {
                    // Unsubscribe from events
                    hex.OnHexagonUnlocked -= OnHexagonUnlocked;
                    
                    // Clear from cell
                    if (hex.CurrentCell != null)
                    {
                        hex.CurrentCell.ClearHexagon();
                    }
                    
                    // Destroy the object
                    Destroy(hex.gameObject);
                }
            }
        }
        
        public void RestartLevel()
        {
            StartLevel(currentLevelIndex);
        }
        
        public void LoadNextLevel()
        {
            StartLevel(currentLevelIndex + 1);
        }
        
        // Example of creating a simple level at runtime
        public void CreateSimpleLevel()
        {
            // Create a new level config
            LevelConfig level = ScriptableObject.CreateInstance<LevelConfig>();
            level.levelNumber = 1;
            level.levelName = "Simple Level";
            level.moveLimit = 10;
            level.colorPalette = new Color[] { Color.blue, Color.green, Color.red };
            level.targetHexagonsToRemove = 3;
            
            // Create hexagon data
            level.hexagons = new HexagonData[]
            {
                new HexagonData { coordinates = new Vector2Int(0, 0), direction = HexDirection.East, colorIndex = 0 },
                new HexagonData { coordinates = new Vector2Int(1, -1), direction = HexDirection.NorthWest, colorIndex = 1 },
                new HexagonData { coordinates = new Vector2Int(-1, 1), direction = HexDirection.SouthEast, colorIndex = 2 }
            };
            
            // Set as current level and start
            levels = new LevelConfig[] { level };
            currentLevelIndex = 0;
            StartLevel(currentLevelIndex);
        }
    }
}