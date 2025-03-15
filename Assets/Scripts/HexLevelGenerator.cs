using UnityEngine;
using System.Collections.Generic;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HexaAway.Core
{
    /// <summary>
    /// Difficulty parameters for level generation
    /// </summary>
    [System.Serializable]
    public class DifficultyConfig
    {
        [Range(1, 30)]
        public int targetMoveCount = 10;
        
        [Range(3, 30)]
        public int hexagonCount = 8;
        
        [Range(0, 1)]
        public float directionChangeRate = 0.3f;
        
        [Range(0, 5)]
        public int bottleneckCount = 1;
        
        [Range(0, 1)]
        public float specialHexagonRate = 0.0f;
        
        // How much variance is allowed in the difficulty
        [Range(0, 0.5f)]
        public float difficultyTolerance = 0.2f;
        
        // Target percentage of hexagons to remove (objective)
        [Range(0.1f, 1.0f)]
        public float hexagonsToRemovePercentage = 0.6f;
        
        // Move limit as a multiplier of the optimal solution length
        [Range(1.0f, 3.0f)]
        public float moveLimitMultiplier = 1.5f;
    }

    /// <summary>
    /// Main class that handles level generation
    /// </summary>
    public class HexLevelGenerator : MonoBehaviour
    {
        // Input configuration
        public GridConfig templateGridConfig;
        public DifficultyConfig difficultyConfig;
        public Color[] defaultColorPalette = new Color[] { 
            new Color(0.2f, 0.4f, 0.8f), // Blue
            new Color(0.2f, 0.8f, 0.4f), // Green
            new Color(0.8f, 0.3f, 0.3f), // Red
            new Color(0.8f, 0.7f, 0.2f), // Yellow
            new Color(0.6f, 0.3f, 0.8f), // Purple
            new Color(0.9f, 0.5f, 0.2f)  // Orange
        };
        
        // Internal representation of the grid and its state
        private Dictionary<Vector2Int, InternalHexagonData> currentGrid = new Dictionary<Vector2Int, InternalHexagonData>();
        private HashSet<Vector2Int> validCells = new HashSet<Vector2Int>();
        private List<SolutionMove> generatedSolution = new List<SolutionMove>();
        private System.Random random;
        
        // Direction vectors for each HexDirection
        private static readonly Vector2Int[] DIRECTION_VECTORS = new Vector2Int[]
        {
            new Vector2Int(1, 0),    // East
            new Vector2Int(1, -1),   // SouthEast
            new Vector2Int(0, -1),   // SouthWest
            new Vector2Int(-1, 0),   // West
            new Vector2Int(-1, 1),   // NorthWest
            new Vector2Int(0, 1)     // NorthEast
        };

        // Opposite direction mapping
        private static readonly HexDirection[] OPPOSITE_DIRECTIONS = new HexDirection[]
        {
            HexDirection.West,      // Opposite of East
            HexDirection.NorthWest, // Opposite of SouthEast
            HexDirection.NorthEast, // Opposite of SouthWest
            HexDirection.East,      // Opposite of West
            HexDirection.SouthEast, // Opposite of NorthWest
            HexDirection.SouthWest  // Opposite of NorthEast
        };

        /// <summary>
        /// Internal representation of a hexagon for generation purposes
        /// </summary>
        private class InternalHexagonData
        {
            public Vector2Int position;
            public HexDirection direction;
            public int colorIndex;
            
            public InternalHexagonData(Vector2Int pos, HexDirection dir, int color)
            {
                position = pos;
                direction = dir;
                colorIndex = color;
            }
            
            public override string ToString()
            {
                return $"Hex({position.x},{position.y}) {direction} Color:{colorIndex}";
            }
        }

        /// <summary>
        /// Represents a move in the solution path
        /// </summary>
        private class SolutionMove
        {
            public Vector2Int startPosition;
            public Vector2Int endPosition;
            public HexDirection direction;
            
            public SolutionMove(Vector2Int start, Vector2Int end, HexDirection dir)
            {
                startPosition = start;
                endPosition = end;
                direction = dir;
            }
        }

        private void Start()
        {
            random = new System.Random();
        }
        
        /// <summary>
        /// Generates a level based on the current grid and difficulty config
        /// </summary>
        public LevelConfig GenerateLevel(int levelNumber, int seed = 0)
        {
            // Initialize with a specific seed if provided
            if (seed != 0)
                random = new System.Random(seed);
            else
                random = new System.Random();
                
            // Reset state
            currentGrid.Clear();
            validCells.Clear();
            generatedSolution.Clear();
            
            // Initialize the grid based on the template grid config
            InitializeGridFromTemplate();
            
            // Generate the puzzle by working backward from a solved state
            GeneratePuzzleBackward();
            
            // Create the level configuration
            LevelConfig levelConfig = ScriptableObject.CreateInstance<LevelConfig>();
            
            // Set basic properties
            levelConfig.levelNumber = levelNumber;
            levelConfig.levelName = $"Level {levelNumber}";
            levelConfig.moveLimit = CalculateMoveLimit();
            
            // Set color palette
            levelConfig.colorPalette = GetColorPalette();
            
            // Set grid configuration
            levelConfig.gridConfig = templateGridConfig;
            
            // Set hexagons
            levelConfig.hexagons = ConvertToHexagonData();
            
            // Set level objectives
            levelConfig.targetHexagonsToRemove = CalculateTargetHexagonsToRemove();
            
            return levelConfig;
        }
        
        /// <summary>
        /// Generate and save multiple levels as scriptable objects
        /// </summary>
        public void GenerateLevels(int count, string saveFolder, int startingLevelNumber = 1)
        {
#if UNITY_EDITOR
            for (int i = 0; i < count; i++)
            {
                int levelNumber = startingLevelNumber + i;
                
                // Generate level with a different seed for each
                LevelConfig levelConfig = GenerateLevel(levelNumber, i + 1);
                
                // Create directory if it doesn't exist
                if (!System.IO.Directory.Exists(saveFolder))
                    System.IO.Directory.CreateDirectory(saveFolder);
                    
                // Save as asset
                string assetPath = $"{saveFolder}/Level_{levelNumber}.asset";
                AssetDatabase.CreateAsset(levelConfig, assetPath);
            }
            
            AssetDatabase.SaveAssets();
            Debug.Log($"Generated and saved {count} levels to {saveFolder}");
#else
            Debug.LogWarning("Level saving is only available in the Editor");
#endif
        }
        
        /// <summary>
        /// Initialize the grid based on the template grid config
        /// </summary>
        private void InitializeGridFromTemplate()
        {
            if (templateGridConfig == null || templateGridConfig.gridRows == null)
            {
                Debug.LogError("No valid grid template provided");
                return;
            }
            
            // Read the grid from the template
            for (int y = 0; y < templateGridConfig.gridRows.Length; y++)
            {
                GridRow row = templateGridConfig.gridRows[y];
                if (row.cells == null) continue;
                
                for (int x = 0; x < row.cells.Length; x++)
                {
                    if (row.cells[x])
                    {
                        // This is a valid cell
                        validCells.Add(new Vector2Int(x, y));
                    }
                }
            }
            
            Debug.Log($"Initialized grid with {validCells.Count} valid cells");
        }
        
        /// <summary>
        /// Main puzzle generation algorithm that works backward from a solved state
        /// </summary>
        private void GeneratePuzzleBackward()
        {
            // Start with a few hexagons at "final" positions
            int initialHexagons = Mathf.Max(2, difficultyConfig.hexagonCount / 3);
            PlaceInitialHexagons(initialHexagons);
            
            int remainingHexagons = difficultyConfig.hexagonCount - initialHexagons;
            int movesMade = 0;
            int maxMoves = difficultyConfig.targetMoveCount * 2; // Set a limit to prevent infinite loops
            
            // Main generation loop
            while (movesMade < difficultyConfig.targetMoveCount && movesMade < maxMoves)
            {
                // Find hexagons that can be moved backward
                List<InternalHexagonData> candidateHexagons = FindHexagonsWithValidBackwardMoves();
                
                if (candidateHexagons.Count == 0)
                {
                    // If we can't move any existing hexagons, add a new one if possible
                    if (remainingHexagons > 0)
                    {
                        if (PlaceNewHexagon())
                        {
                            remainingHexagons--;
                        }
                        else
                        {
                            // If we can't place a new hexagon, we're stuck
                            Debug.LogWarning("Couldn't place more hexagons, ending generation early");
                            break;
                        }
                    }
                    else
                    {
                        // If we can't add more hexagons or move existing ones, we're done
                        break;
                    }
                }
                else
                {
                    // Pick a random hexagon to move backward
                    InternalHexagonData hexagon = candidateHexagons[random.Next(candidateHexagons.Count)];
                    
                    // Make a backward move
                    if (MoveHexagonBackward(hexagon))
                    {
                        movesMade++;
                    }
                }
            }
            
            // Reverse the solution path so it goes from start to finish
            generatedSolution.Reverse();
            
            Debug.Log($"Generated puzzle with {currentGrid.Count} hexagons and {generatedSolution.Count} moves in the solution");
        }
        
        /// <summary>
        /// Place the initial hexagons in their "final" positions
        /// </summary>
        private void PlaceInitialHexagons(int count)
        {
            List<Vector2Int> availableCells = new List<Vector2Int>(validCells);
            
            for (int i = 0; i < count && availableCells.Count > 0; i++)
            {
                // Pick a random cell
                int cellIndex = random.Next(availableCells.Count);
                Vector2Int position = availableCells[cellIndex];
                availableCells.RemoveAt(cellIndex);
                
                // Pick a random direction and color
                HexDirection direction = (HexDirection)random.Next(6);
                int colorIndex = random.Next(defaultColorPalette.Length);
                
                // Create and place the hexagon
                InternalHexagonData hexagon = new InternalHexagonData(position, direction, colorIndex);
                currentGrid[position] = hexagon;
            }
        }
        
        /// <summary>
        /// Place a new hexagon in a valid position
        /// </summary>
        private bool PlaceNewHexagon()
        {
            // Get available cells
            List<Vector2Int> availableCells = validCells
                .Where(c => !currentGrid.ContainsKey(c))
                .ToList();
                
            if (availableCells.Count == 0)
                return false;
                
            // Place hexagon
            Vector2Int position = availableCells[random.Next(availableCells.Count)];
            HexDirection direction = (HexDirection)random.Next(6);
            int colorIndex = random.Next(defaultColorPalette.Length);
            
            InternalHexagonData hexagon = new InternalHexagonData(position, direction, colorIndex);
            currentGrid[position] = hexagon;
            
            return true;
        }
        
        /// <summary>
        /// Find all hexagons that can be moved backward
        /// </summary>
        private List<InternalHexagonData> FindHexagonsWithValidBackwardMoves()
        {
            List<InternalHexagonData> result = new List<InternalHexagonData>();
            
            foreach (var hexagon in currentGrid.Values)
            {
                // Check all six possible backward directions
                for (int i = 0; i < 6; i++)
                {
                    Vector2Int dirVector = DIRECTION_VECTORS[i];
                    
                    // Calculate backward position
                    Vector2Int backwardPos = new Vector2Int(
                        hexagon.position.x + dirVector.x,
                        hexagon.position.y + dirVector.y
                    );
                    
                    // Check if this is a valid backward move
                    if (validCells.Contains(backwardPos) && !currentGrid.ContainsKey(backwardPos))
                    {
                        result.Add(hexagon);
                        break; // Only add the hexagon once even if it has multiple valid backward moves
                    }
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Move a hexagon backward to create a more complex puzzle
        /// </summary>
        private bool MoveHexagonBackward(InternalHexagonData hexagon)
        {
            List<Vector2Int> validBackwardPositions = new List<Vector2Int>();
            List<HexDirection> correspondingDirections = new List<HexDirection>();
            
            // Check all six directions for valid backward moves
            for (int i = 0; i < 6; i++)
            {
                Vector2Int dirVector = DIRECTION_VECTORS[i];
                
                // Calculate backward position
                Vector2Int backwardPos = new Vector2Int(
                    hexagon.position.x + dirVector.x,
                    hexagon.position.y + dirVector.y
                );
                
                // Check if this is a valid backward move
                if (validCells.Contains(backwardPos) && !currentGrid.ContainsKey(backwardPos))
                {
                    validBackwardPositions.Add(backwardPos);
                    
                    // When moving backward, the hexagon would need to be pointing in the opposite direction
                    HexDirection reverseDir = OPPOSITE_DIRECTIONS[i];
                    correspondingDirections.Add(reverseDir);
                }
            }
            
            if (validBackwardPositions.Count == 0)
                return false;
                
            // Choose a random backward position
            int index = random.Next(validBackwardPositions.Count);
            Vector2Int newPosition = validBackwardPositions[index];
            HexDirection newDirection = correspondingDirections[index];
            
            // Check for direction change - this increases difficulty
            bool isDirectionChange = newDirection != hexagon.direction;
            
            // Decide whether to force a direction change based on the difficulty setting
            if (!isDirectionChange && random.NextDouble() < difficultyConfig.directionChangeRate)
            {
                // Choose a different direction that would still allow the hexagon to move to its final position
                List<HexDirection> validDirections = new List<HexDirection>();
                
                for (int i = 0; i < 6; i++)
                {
                    if ((HexDirection)i != newDirection && (HexDirection)i != hexagon.direction)
                    {
                        validDirections.Add((HexDirection)i);
                    }
                }
                
                if (validDirections.Count > 0)
                {
                    newDirection = validDirections[random.Next(validDirections.Count)];
                    isDirectionChange = true;
                }
            }
            
            // Record the move for the solution path
            Vector2Int oldPosition = hexagon.position;
            generatedSolution.Add(new SolutionMove(newPosition, oldPosition, newDirection));
            
            // Move the hexagon
            currentGrid.Remove(oldPosition);
            hexagon.position = newPosition;
            hexagon.direction = newDirection;
            currentGrid[newPosition] = hexagon;
            
            return true;
        }
        
        /// <summary>
        /// Calculate the move limit based on solution length and difficulty config
        /// </summary>
        private int CalculateMoveLimit()
        {
            return Mathf.CeilToInt(generatedSolution.Count * difficultyConfig.moveLimitMultiplier);
        }
        
        /// <summary>
        /// Calculate the target number of hexagons to remove based on total hexagons
        /// </summary>
        private int CalculateTargetHexagonsToRemove()
        {
            return Mathf.CeilToInt(currentGrid.Count * difficultyConfig.hexagonsToRemovePercentage);
        }
        
        /// <summary>
        /// Convert internal hexagon data to the game's HexagonData format
        /// </summary>
        private HexagonData[] ConvertToHexagonData()
        {
            HexagonData[] result = new HexagonData[currentGrid.Count];
            int index = 0;
            
            foreach (var hexagon in currentGrid.Values)
            {
                // Create a new HexagonData instance
                var hexData = new HexagonData
                {
                    coordinates = hexagon.position,
                    direction = hexagon.direction,
                    colorIndex = hexagon.colorIndex
                };
                
                result[index] = hexData;
                index++;
            }
            
            return result;
        }
        
        /// <summary>
        /// Get the color palette for the level
        /// </summary>
        private Color[] GetColorPalette()
        {
            // For simplicity, just return the default palette
            // In a more advanced version, you could generate different palettes
            return defaultColorPalette;
        }
        
        /// <summary>
        /// Calculate the actual difficulty of the generated level
        /// </summary>
        private int CalculateDifficulty()
        {
            int difficulty = 0;
            
            // Base difficulty from number of moves
            difficulty += generatedSolution.Count * 10;
            
            // Additional difficulty from direction changes
            int directionChanges = 0;
            HexDirection lastDirection = HexDirection.East;
            bool firstMove = true;
            
            foreach (var move in generatedSolution)
            {
                if (firstMove)
                {
                    lastDirection = move.direction;
                    firstMove = false;
                }
                else if (move.direction != lastDirection)
                {
                    directionChanges++;
                    lastDirection = move.direction;
                }
            }
            
            difficulty += directionChanges * 5;
            
            // Additional difficulty from the number of hexagons
            difficulty += currentGrid.Count * 3;
            
            return difficulty;
        }
        
        /// <summary>
        /// Debug method to visualize the current grid
        /// </summary>
        public void DebugDrawGrid()
        {
            string output = "Grid visualization:\n";
            
            // Find bounds
            int minQ = int.MaxValue, maxQ = int.MinValue;
            int minR = int.MaxValue, maxR = int.MinValue;
            
            foreach (var pos in validCells)
            {
                minQ = Mathf.Min(minQ, pos.x);
                maxQ = Mathf.Max(maxQ, pos.x);
                minR = Mathf.Min(minR, pos.y);
                maxR = Mathf.Max(maxR, pos.y);
            }
            
            // Draw grid
            for (int r = minR; r <= maxR; r++)
            {
                // Add indent based on row for hexagonal layout
                string indent = new string(' ', r - minR);
                output += indent;
                
                for (int q = minQ; q <= maxQ; q++)
                {
                    Vector2Int pos = new Vector2Int(q, r);
                    
                    if (validCells.Contains(pos))
                    {
                        if (currentGrid.TryGetValue(pos, out InternalHexagonData hex))
                        {
                            output += GetHexSymbol(hex) + " ";
                        }
                        else
                        {
                            output += ". ";
                        }
                    }
                    else
                    {
                        output += "  ";
                    }
                }
                output += "\n";
            }
            
            Debug.Log(output);
        }
        
        /// <summary>
        /// Get a symbol representing a hexagon for debug visualization
        /// </summary>
        private string GetHexSymbol(InternalHexagonData hex)
        {
            char colorSymbol = "RBGYPO"[hex.colorIndex % 6];
            
            // Get direction symbol
            char dirSymbol;
            switch (hex.direction)
            {
                case HexDirection.East: dirSymbol = '→'; break;
                case HexDirection.SouthEast: dirSymbol = '↘'; break;
                case HexDirection.SouthWest: dirSymbol = '↙'; break;
                case HexDirection.West: dirSymbol = '←'; break;
                case HexDirection.NorthWest: dirSymbol = '↖'; break;
                case HexDirection.NorthEast: dirSymbol = '↗'; break;
                default: dirSymbol = '?'; break;
            }
            
            return colorSymbol.ToString();
        }
        
#if UNITY_EDITOR
        /// <summary>
        /// Editor utility to generate levels
        /// </summary>
        [MenuItem("HexaAway/Generate Levels")]
        static void GenerateLevelsMenuItem()
        {
            HexLevelGenerator generator = FindObjectOfType<HexLevelGenerator>();
            if (generator == null)
            {
                Debug.LogError("No HexLevelGenerator found in the scene");
                return;
            }
            
            string path = EditorUtility.SaveFolderPanel("Save Generated Levels", "Assets", "GeneratedLevels");
            if (string.IsNullOrEmpty(path))
                return;
                
            // Convert to project-relative path
            string projectPath = System.IO.Path.GetFullPath("Assets");
            if (path.StartsWith(projectPath))
            {
                path = "Assets" + path.Substring(projectPath.Length);
            }
            else
            {
                Debug.LogError("Selected folder must be inside the project");
                return;
            }
            
            int levelCount = EditorUtility.DisplayDialogComplex(
                "Generate Levels",
                "How many levels do you want to generate?",
                "10",
                "25",
                "Cancel"
            );
            
            if (levelCount == 2) // Cancel
                return;
                
            generator.GenerateLevels(levelCount == 0 ? 10 : 25, path);
        }
#endif
    }

#if UNITY_EDITOR

#endif
}