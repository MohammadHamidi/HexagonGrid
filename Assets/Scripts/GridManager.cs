using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HexaAway.Core
{
    public class GridManager : MonoBehaviour
    {
    
        [SerializeField] private GameObject hexCellPrefab;
        [SerializeField] private bool debugMode = false;
        
        private Dictionary<Vector2Int, HexCell> cells = new Dictionary<Vector2Int, HexCell>();
        private Transform gridContainer;
        private GridConfig currentGridConfig;

        
        // Singleton pattern
        private static GridManager _instance;
        public static GridManager Instance 
        { 
            get { return _instance; }
            private set { _instance = value; }
        }
        
        
        private void Awake()
        {
            // Safe singleton initialization
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }
        }
        
        private void OnDestroy()
        {
            // Clean up singleton reference when destroyed
            if (Instance == this)
            {
                Instance = null;
            }
        }

        
        public void InitializeGrid(GridConfig gridConfig)
        {
            // Clear any existing grid
            ClearGrid();
            
            // Store the current grid configuration
            currentGridConfig = gridConfig;
            
            if (gridConfig == null)
            {
                Debug.LogError("Grid Configuration is missing!");
                return;
            }
            
            // Create container for grid cells
            if (gridContainer == null)
            {
                gridContainer = new GameObject("Grid Container").transform;
                gridContainer.SetParent(transform);
                gridContainer.localPosition = Vector3.zero;
            }
            
            GenerateGrid();
        }
        public void GenerateGrid()
        {
            if (currentGridConfig == null || currentGridConfig.gridRows == null || currentGridConfig.gridRows.Length == 0)
            {
                Debug.LogError("Grid layout is empty or GridConfig is null.");
                return;
            }
            
            if (hexCellPrefab == null)
            {
                Debug.LogError("Hex Cell Prefab is not assigned!");
                return;
            }
            
            // Create all cells based on the grid configuration
            for (int row = 0; row < currentGridConfig.gridRows.Length; row++)
            {
                GridRow gridRow = currentGridConfig.gridRows[row];
                if (gridRow == null || gridRow.cells == null)
                    continue;
                
                for (int col = 0; col < gridRow.cells.Length; col++)
                {
                    if (gridRow.cells[col])
                    {
                        CreateCell(new Vector2Int(col, row));
                    }
                }
            }
            
            Debug.Log($"Generated grid with {cells.Count} cells");
        

        }
        
        private void CreateCell(Vector2Int coords)
        {
            if (cells.ContainsKey(coords))
            {
                Debug.LogWarning($"Cell already exists at coordinates: ({coords.x}, {coords.y})");
                return;
            }
            
            if (hexCellPrefab == null || gridContainer == null)
            {
                Debug.LogError("Cannot create cell: missing prefab or container");
                return;
            }
                
            // Instantiate the cell and set its parent
            GameObject cellObject = Instantiate(hexCellPrefab, gridContainer);
            if (cellObject == null)
            {
                Debug.LogError("Failed to instantiate hex cell prefab");
                return;
            }
            
            cellObject.name = $"HexCell_{coords.x}_{coords.y}";
            
            // Compute the world position based on grid coordinates
            Vector3 worldPos = HexToWorld(coords);
            cellObject.transform.position = worldPos;
            
            // Get or add the HexCell component
            HexCell cell = cellObject.GetComponent<HexCell>();
            if (cell == null)
            {
                cell = cellObject.AddComponent<HexCell>();
                Debug.LogWarning($"HexCell component missing from prefab, adding component to {cellObject.name}");
            }
                
            // Initialize the cell
            cell.Initialize(coords);
            cells.Add(coords, cell);
            
            if (debugMode)
            {
                Debug.Log($"Created cell at ({coords.x}, {coords.y}) with world position: {worldPos}");
            }
        }
        
        /// <summary>
        /// Converts hex grid coordinates to world position.
        /// Using a pointy-top hexagonal grid with odd-row offset.
        /// </summary>
        public Vector3 HexToWorld(Vector2Int hexCoords)
        {
            if (currentGridConfig == null)
                return Vector3.zero;
                
            float horizontalSpacing = currentGridConfig.hexHorizontalSpacing;
            float verticalSpacing = currentGridConfig.hexVerticalSpacing;
            
            int col = hexCoords.x;
            int row = hexCoords.y;
            
            // Calculate the base position
            float xPos = col * horizontalSpacing;
            float zPos = row * verticalSpacing;
            
            // Apply offset for odd rows (needed for proper hexagonal grid)
            if (row % 2 == 1)
            {
                xPos += horizontalSpacing * 0.5f;
            }
            
            return new Vector3(xPos, 0, zPos);
        }
        
        /// <summary>
        /// Alias for GridToWorld to maintain compatibility
        /// </summary>
        public Vector3 GridToWorld(Vector2Int gridCoords)
        {
            return HexToWorld(gridCoords);
        }
        
        /// <summary>
        /// Converts a world position to hex grid coordinates.
        /// </summary>
        public Vector2Int WorldToHex(Vector3 worldPos)
        {
            if (currentGridConfig == null)
                return Vector2Int.zero;
                
            float horizontalSpacing = currentGridConfig.hexHorizontalSpacing;
            float verticalSpacing = currentGridConfig.hexVerticalSpacing;
            
            // Calculate the row first
            int row = Mathf.RoundToInt(worldPos.z / verticalSpacing);
            
            // Adjust for odd row offset when calculating column
            float xOffset = (row % 2 == 1) ? horizontalSpacing * 0.5f : 0;
            int col = Mathf.RoundToInt((worldPos.x - xOffset) / horizontalSpacing);
            
            return new Vector2Int(col, row);
        }
        /// <summary>
        /// Retrieves the cell at the given coordinates.
        /// </summary>
        public HexCell GetCell(Vector2Int coords)
        {
            if (cells == null)
                return null;
                
            if (cells.TryGetValue(coords, out HexCell cell))
                return cell;
            
            if (debugMode)
            {
                Debug.LogWarning($"Cell not found at coordinates: ({coords.x}, {coords.y})");
            }
            return null;
        }
        
        /// <summary>
        /// Checks if a cell exists at the given coordinates.
        /// </summary>
        public bool HasCell(Vector2Int coords)
        {
            return cells != null && cells.ContainsKey(coords);
        }
        
        private void ClearGrid()
        {
            if (cells == null)
                return;
                
            foreach (HexCell cell in cells.Values)
            {
                if (cell != null && cell.gameObject != null)
                {
                    if (Application.isPlaying)
                        Destroy(cell.gameObject);
                    else
                        DestroyImmediate(cell.gameObject);
                }
            }
            cells.Clear();
        }
        
        public List<Vector2Int> GetAllCellCoordinates()
        {
            return cells != null ? new List<Vector2Int>(cells.Keys) : new List<Vector2Int>();
        }
        
        /// <summary>
        /// Get the coordinate for a neighboring cell in the specified direction
        /// </summary>
        public Vector2Int GetNeighborCoordinate(Vector2Int coords, HexDirection direction)
        {
            int x = coords.x;
            int y = coords.y;
            bool oddRow = (y % 2 == 1);
            
            // Coordinate offsets for each direction, accounting for odd/even rows
            switch (direction)
            {
                case HexDirection.East: // Right
                    return new Vector2Int(x + 1, y);
                    
                case HexDirection.West: // Left
                    return new Vector2Int(x - 1, y);
                    
                case HexDirection.NorthEast: // Top-right
                    return oddRow ? new Vector2Int(x + 1, y + 1) : new Vector2Int(x, y + 1);
                    
                case HexDirection.NorthWest: // Top-left
                    return oddRow ? new Vector2Int(x, y + 1) : new Vector2Int(x - 1, y + 1);
                    
                case HexDirection.SouthEast: // Bottom-right
                    return oddRow ? new Vector2Int(x + 1, y - 1) : new Vector2Int(x, y - 1);
                    
                case HexDirection.SouthWest: // Bottom-left
                    return oddRow ? new Vector2Int(x, y - 1) : new Vector2Int(x - 1, y - 1);
                    
                default:
                    return coords;
            }
        }

        
        private void DrawHexGizmo(Vector3 center, float radius)
        {
            // For a pointy-top hexagon
            Vector3[] vertices = new Vector3[6];
            for (int i = 0; i < 6; i++)
            {
                float angle = ((i * 60f) - 30f) * Mathf.Deg2Rad; // Start at the top point
                vertices[i] = center + new Vector3(
                    radius * Mathf.Cos(angle),
                    0.01f, // Slight elevation so it doesn't clip with ground
                    radius * Mathf.Sin(angle)
                );
            }
            
            for (int i = 0; i < 6; i++)
            {
                Gizmos.DrawLine(vertices[i], vertices[(i + 1) % 6]);
            }
        }
    }
}