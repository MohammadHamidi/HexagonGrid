using System.Collections.Generic;
using UnityEngine;

namespace HexaAway.Core
{
    public class GridManager : MonoBehaviour
    {
        [SerializeField] private GridConfig gridConfig;
        [SerializeField] private GameObject hexCellPrefab;
        
        private Dictionary<Vector2Int, HexCell> cells = new Dictionary<Vector2Int, HexCell>();
        private Transform gridContainer;
        
        // Singleton pattern
        public static GridManager Instance { get; private set; }
        
        public GridConfig GridConfig => gridConfig;
        
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
            
            InitializeGrid();
        }
        
        private void InitializeGrid()
        {
            if (gridConfig == null)
            {
                Debug.LogError("Grid Configuration is missing!");
                return;
            }
            
            // Create a container for the grid cells
            gridContainer = new GameObject("Grid Container").transform;
            gridContainer.SetParent(transform);
            gridContainer.localPosition = Vector3.zero;
            
            // Generate the grid
            GenerateGrid(gridConfig.defaultGridWidth, gridConfig.defaultGridHeight);
        }
        
        public void GenerateGrid(int width, int height)
        {
            // Clear any existing grid
            ClearGrid();
            
            // Calculate radius for hexagonal grid shape
            int radius = Mathf.Max(width, height) / 2;
            
            // Create new grid cells using a hexagonal pattern
            for (int q = -radius; q <= radius; q++)
            {
                int r1 = Mathf.Max(-radius, -q - radius);
                int r2 = Mathf.Min(radius, -q + radius);
                
                for (int r = r1; r <= r2; r++)
                {
                    CreateCell(new Vector2Int(q, r));
                }
            }
            
            // Log the generated coordinates for debugging
            Debug.Log($"Generated grid with {cells.Count} cells");
            string cellCoords = "Cell coordinates: ";
            foreach (Vector2Int coord in cells.Keys)
            {
                cellCoords += $"({coord.x}, {coord.y}) ";
            }
            Debug.Log(cellCoords);
        }
        
        private void CreateCell(Vector2Int coords)
        {
            // Skip if cell already exists
            if (cells.ContainsKey(coords))
                return;
                
            // Create the cell object
            GameObject cellObject = Instantiate(hexCellPrefab, gridContainer);
            cellObject.name = $"HexCell_{coords.x}_{coords.y}";
            
            // Set position
            Vector3 worldPos = HexToWorld(coords);
            cellObject.transform.position = worldPos;
            
            // Setup the cell component
            HexCell cell = cellObject.GetComponent<HexCell>();
            if (cell == null)
                cell = cellObject.AddComponent<HexCell>();
                
            cell.Initialize(coords);
            
            // Add to dictionary
            cells.Add(coords, cell);
        }
        
        public Vector3 HexToWorld(Vector2Int hexCoords)
        {
            // Convert from axial coordinates to world position
            // Using formula for pointy-top hexagons
            float x = gridConfig.hexHorizontalSpacing * (hexCoords.x + hexCoords.y/2f);
            float z = gridConfig.hexVerticalSpacing * 1.5f * hexCoords.y;
            
            return new Vector3(x, 0, z);
        }
        
        public Vector2Int WorldToHex(Vector3 worldPos)
        {
            // Convert world position to axial coordinates
            // Using formula for pointy-top hexagons
            float q = (worldPos.x / gridConfig.hexHorizontalSpacing) - (worldPos.z / (gridConfig.hexVerticalSpacing * 3f));
            float r = worldPos.z / (gridConfig.hexVerticalSpacing * 1.5f);
            
            // Round to nearest hex
            return HexRound(new Vector2(q, r));
        }
        
        private Vector2Int HexRound(Vector2 hex)
        {
            // Convert axial to cube coordinates
            float x = hex.x;
            float z = hex.y;
            float y = -x - z;
            
            // Round cube coordinates
            float rx = Mathf.Round(x);
            float ry = Mathf.Round(y);
            float rz = Mathf.Round(z);
            
            // Fix rounding errors
            float xDiff = Mathf.Abs(rx - x);
            float yDiff = Mathf.Abs(ry - y);
            float zDiff = Mathf.Abs(rz - z);
            
            if (xDiff > yDiff && xDiff > zDiff)
                rx = -ry - rz;
            else if (yDiff > zDiff)
                ry = -rx - rz;
            else
                rz = -rx - ry;
            
            // Convert back to axial
            return new Vector2Int(Mathf.RoundToInt(rx), Mathf.RoundToInt(rz));
        }
        
        public HexCell GetCell(Vector2Int coords)
        {
            if (cells.TryGetValue(coords, out HexCell cell))
                return cell;
            
            // Debugging - print a warning about missing cells
            Debug.LogWarning($"Cell not found at coordinates: ({coords.x}, {coords.y})");
            
            return null;
        }
        
        public List<HexCell> GetNeighbors(Vector2Int coords)
        {
            List<HexCell> neighbors = new List<HexCell>();
            
            // The 6 neighboring directions in axial coordinates
            Vector2Int[] directions = new Vector2Int[]
            {
                new Vector2Int(1, 0),   // East
                new Vector2Int(1, -1),  // Southeast
                new Vector2Int(0, -1),  // Southwest
                new Vector2Int(-1, 0),  // West
                new Vector2Int(-1, 1),  // Northwest
                new Vector2Int(0, 1)    // Northeast
            };
            
            foreach (Vector2Int dir in directions)
            {
                Vector2Int neighborCoord = coords + dir;
                HexCell neighbor = GetCell(neighborCoord);
                
                if (neighbor != null)
                {
                    neighbors.Add(neighbor);
                }
            }
            
            return neighbors;
        }
        
        private void ClearGrid()
        {
            // Destroy all cell objects
            foreach (HexCell cell in cells.Values)
            {
                if (cell != null && cell.gameObject != null)
                {
                    Destroy(cell.gameObject);
                }
            }
            
            // Clear the dictionary
            cells.Clear();
        }
        
        // Helper method to check if a coordinate exists in the grid
        public bool HasCell(Vector2Int coords)
        {
            return cells.ContainsKey(coords);
        }
        
        // Get all cell coordinates
        public List<Vector2Int> GetAllCellCoordinates()
        {
            return new List<Vector2Int>(cells.Keys);
        }
    }
}