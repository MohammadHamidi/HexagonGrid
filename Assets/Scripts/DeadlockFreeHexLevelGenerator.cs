using UnityEngine;
using System.Collections.Generic;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HexaAway.Core
{
    public class DeadlockFreeHexLevelGenerator : MonoBehaviour
    {
        public GridConfig templateGridConfig;
        public DifficultyConfig difficultyConfig;
        public Color[] defaultColorPalette;

        private System.Random random;
        private HashSet<Vector2Int> validCells = new HashSet<Vector2Int>();
        
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

        // Opposite directions
        private static readonly HexDirection[] OPPOSITE_DIRECTIONS = new HexDirection[]
        {
            HexDirection.West,      // Opposite of East
            HexDirection.NorthWest, // Opposite of SouthEast
            HexDirection.NorthEast, // Opposite of SouthWest
            HexDirection.East,      // Opposite of West
            HexDirection.SouthEast, // Opposite of NorthWest
            HexDirection.SouthWest  // Opposite of NorthEast
        };

        private class HexagonState
        {
            public Vector2Int position;
            public HexDirection direction;
            public int colorIndex;
            
            public HexagonState(Vector2Int pos, HexDirection dir, int color)
            {
                position = pos;
                direction = dir;
                colorIndex = color;
            }
            
            // Create a deep copy
            public HexagonState Clone()
            {
                return new HexagonState(position, direction, colorIndex);
            }
        }

        private class GameState
        {
            public Dictionary<Vector2Int, HexagonState> hexagons = new Dictionary<Vector2Int, HexagonState>();
            
            // Create a deep copy of the game state
            public GameState Clone()
            {
                GameState newState = new GameState();
                foreach (var kvp in hexagons)
                {
                    newState.hexagons[kvp.Key] = kvp.Value.Clone();
                }
                return newState;
            }
        }

        private class Move
        {
            public Vector2Int hexPosition;
            public HexDirection moveDirection;
            
            public Move(Vector2Int pos, HexDirection dir)
            {
                hexPosition = pos;
                moveDirection = dir;
            }
        }

        private void Start()
        {
            random = new System.Random();
        }

        public LevelConfig GenerateLevel(int levelNumber, int seed = 0)
        {
            // Initialize with seed if provided
            random = seed != 0 ? new System.Random(seed) : new System.Random();
            
            // Initialize grid from template
            InitializeGridFromTemplate();
            
            // Generate a solvable level
            (GameState startState, List<Move> solution) = GenerateSolvableLevel();
            
            // Create level config
            LevelConfig levelConfig = ScriptableObject.CreateInstance<LevelConfig>();
            levelConfig.levelNumber = levelNumber;
            levelConfig.levelName = $"Level {levelNumber}";
            levelConfig.moveLimit = CalculateMoveLimit(solution.Count);
            levelConfig.colorPalette = defaultColorPalette;
            levelConfig.gridConfig = templateGridConfig;
            levelConfig.hexagons = ConvertToHexagonData(startState);
            levelConfig.targetHexagonsToRemove = CalculateTargetHexagonsToRemove(startState.hexagons.Count);
            
            return levelConfig;
        }
        
        private void InitializeGridFromTemplate()
        {
            validCells.Clear();
            
            if (templateGridConfig == null || templateGridConfig.gridRows == null)
            {
                Debug.LogError("No valid grid template provided");
                return;
            }
            
            for (int y = 0; y < templateGridConfig.gridRows.Length; y++)
            {
                GridRow row = templateGridConfig.gridRows[y];
                if (row.cells == null) continue;
                
                for (int x = 0; x < row.cells.Length; x++)
                {
                    if (row.cells[x])
                    {
                        validCells.Add(new Vector2Int(x, y));
                    }
                }
            }
        }
        
        private (GameState, List<Move>) GenerateSolvableLevel()
        {
            int maxAttempts = 5; // Limit the number of generation attempts
            
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                // Start with an empty game state
                GameState finalState = new GameState();
                List<Move> solution = new List<Move>();
                
                // Place some "static" hexagons that won't be part of the solution
                PlaceStaticHexagons(finalState, Mathf.Max(1, difficultyConfig.hexagonCount / 4));
                
                // Create the starting state by working backward
                GameState startState = finalState.Clone();
                
                // Create solution by adding hexagons and valid moves
                int targetMoves = difficultyConfig.targetMoveCount;
                for (int i = 0; i < targetMoves; i++)
                {
                    if (!CreateValidMove(startState, solution, finalState))
                    {
                        break;
                    }
                }
                
                // Reverse the solution path since we built it backward
                solution.Reverse();
                
                // Validate solution and check for deadlocks
                if (solution.Count > 0 && 
                    ValidateSolution(startState.Clone(), solution) &&
                    !HasDeadlocks(startState))
                {
                    return (startState, solution);
                }
                
                Debug.Log($"Generation attempt {attempt + 1} failed, retrying...");
            }
            
            // If all attempts fail, use the guaranteed simple method
            Debug.LogWarning("All generation attempts failed. Using super simple generation.");
            return GenerateSimpleSolvableLevel();
        }
        
        // Create truly simple levels that are guaranteed solvable
        private (GameState, List<Move>) GenerateSimpleSolvableLevel()
        {
            GameState state = new GameState();
            List<Move> solution = new List<Move>();
            
            // Add some static hexagons that won't be part of the solution
            PlaceStaticHexagons(state, Mathf.Max(1, difficultyConfig.hexagonCount / 4));
            
            // Add one new removable hexagon at a time, with completely isolated paths
            List<Vector2Int> usedCells = new List<Vector2Int>(state.hexagons.Keys);
            int movesToCreate = Mathf.Min(difficultyConfig.targetMoveCount, 
                                      (validCells.Count - usedCells.Count) / 3);
            
            for (int i = 0; i < movesToCreate; i++)
            {
                // Find cells that aren't already used
                var availableCells = validCells
                    .Where(c => !usedCells.Contains(c))
                    .ToList();
                
                if (availableCells.Count == 0) break;
                
                // Choose a random cell for the hexagon
                Vector2Int position = availableCells[random.Next(availableCells.Count)];
                
                // Find a valid direction that doesn't create path conflicts
                List<HexDirection> safeDirections = FindNonConflictingDirections(state, position);
                
                if (safeDirections.Count == 0) continue;
                
                // Choose a random safe direction
                HexDirection direction = safeDirections[random.Next(safeDirections.Count)];
                
                // Create and add the hexagon
                int colorIndex = random.Next(defaultColorPalette.Length);
                HexagonState hexagon = new HexagonState(position, direction, colorIndex);
                state.hexagons[position] = hexagon;
                
                // Calculate the move path
                Vector2Int dirVector = DIRECTION_VECTORS[(int)direction];
                Vector2Int pathEnd = CalculateMovementEndpoint(state, position, direction);
                
                // Mark all cells along the path as used
                Vector2Int current = position;
                while (!current.Equals(pathEnd))
                {
                    usedCells.Add(current);
                    current = new Vector2Int(current.x + dirVector.x, current.y + dirVector.y);
                    if (validCells.Contains(current))
                    {
                        usedCells.Add(current);
                    }
                }
                
                // Add to solution
                solution.Add(new Move(position, direction));
            }
            
            return (state, solution);
        }
        
        // Find directions that won't cause path conflicts with existing hexagons
        private List<HexDirection> FindNonConflictingDirections(GameState state, Vector2Int position)
        {
            List<HexDirection> safeDirections = new List<HexDirection>();
            
            for (int i = 0; i < 6; i++)
            {
                HexDirection direction = (HexDirection)i;
                HexDirection oppositeDirection = OPPOSITE_DIRECTIONS[i];
                Vector2Int dirVector = DIRECTION_VECTORS[i];
                
                // Test if this direction would create a path conflict
                bool hasConflict = false;
                Vector2Int current = position;
                
                while (validCells.Contains(current) && !hasConflict)
                {
                    // Move one step in the direction
                    current = new Vector2Int(current.x + dirVector.x, current.y + dirVector.y);
                    
                    // Check if we've hit the grid boundary
                    if (!validCells.Contains(current))
                        break;
                    
                    // Check if we've hit an existing hexagon
                    if (state.hexagons.TryGetValue(current, out HexagonState existingHex))
                    {
                        // Check if the hexagon is pointing toward us (creates deadlock)
                        if (existingHex.direction == oppositeDirection)
                        {
                            hasConflict = true;
                        }
                        break;
                    }
                }
                
                if (!hasConflict)
                {
                    safeDirections.Add(direction);
                }
            }
            
            return safeDirections;
        }
        
        private bool CreateValidMove(GameState state, List<Move> solution, GameState finalState)
        {
            // Get list of available cells
            List<Vector2Int> availableCells = validCells
                .Where(c => !state.hexagons.ContainsKey(c))
                .ToList();
                
            if (availableCells.Count == 0)
                return false;
                
            // Try positions in random order
            List<Vector2Int> shuffledCells = availableCells.OrderBy(x => random.Next()).ToList();
            
            foreach (Vector2Int position in shuffledCells)
            {
                // Find directions that won't create deadlocks
                List<HexDirection> safeDirections = FindNonConflictingDirections(state, position);
                
                if (safeDirections.Count == 0)
                    continue;
                
                // Choose a random safe direction
                HexDirection direction = safeDirections[random.Next(safeDirections.Count)];
                
                // Create the hexagon
                int colorIndex = random.Next(defaultColorPalette.Length);
                HexagonState hexagon = new HexagonState(position, direction, colorIndex);
                
                // Test if this move would create a deadlock
                state.hexagons[position] = hexagon;
                if (HasDeadlocks(state))
                {
                    // Remove the hexagon and try another direction or position
                    state.hexagons.Remove(position);
                    continue;
                }
                
                // Add the move to the solution
                solution.Add(new Move(position, direction));
                return true;
            }
            
            // Couldn't find a valid move
            return false;
        }
        
        // Check if the current state has any deadlocks
        private bool HasDeadlocks(GameState state)
        {
            // For each hexagon, check if it can move without hitting another hexagon
            // that's pointing toward it
            foreach (var hexEntry in state.hexagons)
            {
                Vector2Int position = hexEntry.Key;
                HexagonState hexagon = hexEntry.Value;
                
                // Get the direction vector
                Vector2Int dirVector = DIRECTION_VECTORS[(int)hexagon.direction];
                HexDirection oppositeDirection = OPPOSITE_DIRECTIONS[(int)hexagon.direction];
                
                // Check the path
                Vector2Int currentPos = position;
                
                while (true)
                {
                    // Move one step
                    currentPos = new Vector2Int(
                        currentPos.x + dirVector.x,
                        currentPos.y + dirVector.y
                    );
                    
                    // Check if we've hit the grid boundary
                    if (!validCells.Contains(currentPos))
                        break;
                    
                    // Check if we've hit another hexagon
                    if (state.hexagons.TryGetValue(currentPos, out HexagonState otherHex))
                    {
                        // If the other hexagon is pointing toward this one, it's a deadlock
                        if (otherHex.direction == oppositeDirection)
                        {
                            return true;
                        }
                        break;
                    }
                }
            }
            
            return false;
        }
        
        private void PlaceStaticHexagons(GameState state, int count)
        {
            List<Vector2Int> availableCells = validCells
                .Where(c => !state.hexagons.ContainsKey(c))
                .ToList();
                
            for (int i = 0; i < count && availableCells.Count > 0; i++)
            {
                // Choose a random cell
                int cellIndex = random.Next(availableCells.Count);
                Vector2Int position = availableCells[cellIndex];
                availableCells.RemoveAt(cellIndex);
                
                // Create a random hexagon
                HexDirection direction = (HexDirection)random.Next(6);
                int colorIndex = random.Next(defaultColorPalette.Length);
                
                // Add to the state
                state.hexagons[position] = new HexagonState(position, direction, colorIndex);
            }
        }
        
        // Validate that the solution actually works
        private bool ValidateSolution(GameState state, List<Move> solution)
        {
            foreach (Move move in solution)
            {
                if (!state.hexagons.TryGetValue(move.hexPosition, out HexagonState hexagon))
                {
                    Debug.LogError($"Solution validation failed: No hexagon at {move.hexPosition}");
                    return false;
                }
                
                // Apply the move
                if (!ApplyMove(state, move))
                {
                    Debug.LogError($"Solution validation failed: Invalid move {move.hexPosition} -> {move.moveDirection}");
                    return false;
                }
            }
            
            return true;
        }
        
        // Apply a move to the game state
        private bool ApplyMove(GameState state, Move move)
        {
            if (!state.hexagons.TryGetValue(move.hexPosition, out HexagonState hexagon))
                return false;
                
            // Get direction vector
            int dirIndex = (int)move.moveDirection;
            Vector2Int dirVector = DIRECTION_VECTORS[dirIndex];
            
            // Remove the hexagon from its current position
            state.hexagons.Remove(move.hexPosition);
            
            // Calculate the final position after sliding
            Vector2Int endPosition = CalculateMovementEndpoint(state, move.hexPosition, move.moveDirection);
            
            // If the hexagon didn't move, it's an invalid move
            if (endPosition.Equals(move.hexPosition))
                return false;
                
            // Place the hexagon at its new position
            hexagon.position = endPosition;
            state.hexagons[endPosition] = hexagon;
            
            return true;
        }
        
        // Calculate where a hexagon would end up when moved in a direction
        private Vector2Int CalculateMovementEndpoint(GameState state, Vector2Int startPos, HexDirection direction)
        {
            Vector2Int dirVector = DIRECTION_VECTORS[(int)direction];
            Vector2Int currentPos = startPos;
            Vector2Int nextPos;
            
            while (true)
            {
                nextPos = new Vector2Int(
                    currentPos.x + dirVector.x,
                    currentPos.y + dirVector.y
                );
                
                // Check if next position is valid and empty
                if (validCells.Contains(nextPos) && !state.hexagons.ContainsKey(nextPos))
                {
                    currentPos = nextPos;
                }
                else
                {
                    // Hit boundary or obstacle
                    break;
                }
            }
            
            return currentPos;
        }
        
        private int CalculateMoveLimit(int solutionLength)
        {
            return Mathf.CeilToInt(solutionLength * difficultyConfig.moveLimitMultiplier);
        }
        
        private int CalculateTargetHexagonsToRemove(int totalHexagons)
        {
            return Mathf.CeilToInt(totalHexagons * difficultyConfig.hexagonsToRemovePercentage);
        }
        
        private HexagonData[] ConvertToHexagonData(GameState state)
        {
            HexagonData[] result = new HexagonData[state.hexagons.Count];
            int index = 0;
            
            foreach (var hexagon in state.hexagons.Values)
            {
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
        
          // Visualize the current level in the console for debugging
     
        
#if UNITY_EDITOR
        public void GenerateLevels(int count, string saveFolder, int startingLevelNumber = 1)
        {
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
        }
        
        [MenuItem("HexaAway/Generate Deadlock-Free Levels")]
        static void GenerateLevelsMenuItem()
        {
            DeadlockFreeHexLevelGenerator generator = FindObjectOfType<DeadlockFreeHexLevelGenerator>();
            if (generator == null)
            {
                Debug.LogError("No DeadlockFreeHexLevelGenerator found in the scene");
                return;
            }
            
            string path = EditorUtility.SaveFolderPanel("Save Generated Levels", "Assets", "GeneratedLevels");
            if (string.IsNullOrEmpty(path))
                return;
                
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
}