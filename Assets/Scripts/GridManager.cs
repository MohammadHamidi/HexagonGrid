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
            
            gridContainer = new GameObject("Grid Container").transform;
            gridContainer.SetParent(transform);
            gridContainer.localPosition = Vector3.zero;
            
            GenerateGrid();
        }
        
        public void GenerateGrid()
        {
            ClearGrid();
            
            if (gridConfig.gridRows == null || gridConfig.gridRows.Length == 0)
            {
                Debug.LogError("Grid layout is empty in GridConfig.");
                return;
            }
            
            for (int row = 0; row < gridConfig.gridRows.Length; row++)
            {
                GridRow gridRow = gridConfig.gridRows[row];
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
                return;
                
            GameObject cellObject = Instantiate(hexCellPrefab, gridContainer);
            cellObject.name = $"HexCell_{coords.x}_{coords.y}";
            
            // Compute the world position based on grid coordinates.
            Vector3 worldPos = HexToWorld(coords);
            cellObject.transform.position = worldPos;
            
            HexCell cell = cellObject.GetComponent<HexCell>();
            if (cell == null)
                cell = cellObject.AddComponent<HexCell>();
                
            cell.Initialize(coords);
            cells.Add(coords, cell);
        }
        
        /// <summary>
        /// Converts grid coordinates to world position.
        /// Uses an offset grid conversion (pointy-top hex layout).
        /// </summary>
        public Vector3 GridToWorld(Vector2Int gridCoords)
        {
            int col = gridCoords.x;
            int row = gridCoords.y;
            // For odd rows, shift the x position by half the horizontal spacing.
            float x = col * gridConfig.hexHorizontalSpacing + (row % 2 == 1 ? gridConfig.hexHorizontalSpacing / 2f : 0);
            float z = row * (gridConfig.hexVerticalSpacing * 0.75f);
            return new Vector3(x, 0, z);
        }
        
        /// <summary>
        /// Alias for GridToWorld to match Hexagon code expectations.
        /// </summary>
        public Vector3 HexToWorld(Vector2Int hexCoords)
        {
            return GridToWorld(hexCoords);
        }
        
        /// <summary>
        /// Converts a world position to grid coordinates.
        /// This assumes the same offset grid conversion as HexToWorld.
        /// </summary>
        public Vector2Int WorldToHex(Vector3 worldPos)
        {
            float zSpacing = gridConfig.hexVerticalSpacing * 0.75f;
            int row = Mathf.RoundToInt(worldPos.z / zSpacing);
            float offset = (row % 2 == 1) ? gridConfig.hexHorizontalSpacing / 2f : 0;
            int col = Mathf.RoundToInt((worldPos.x - offset) / gridConfig.hexHorizontalSpacing);
            return new Vector2Int(col, row);
        }
        
        /// <summary>
        /// Retrieves the cell at the given coordinates.
        /// </summary>
        public HexCell GetCell(Vector2Int coords)
        {
            if (cells.TryGetValue(coords, out HexCell cell))
                return cell;
            
            Debug.LogWarning($"Cell not found at coordinates: ({coords.x}, {coords.y})");
            return null;
        }
        
        /// <summary>
        /// Checks if a cell exists at the given coordinates.
        /// </summary>
        public bool HasCell(Vector2Int coords)
        {
            return cells.ContainsKey(coords);
        }
        
        private void ClearGrid()
        {
            foreach (HexCell cell in cells.Values)
            {
                if (cell != null && cell.gameObject != null)
                {
                    Destroy(cell.gameObject);
                }
            }
            cells.Clear();
        }
        
        public List<Vector2Int> GetAllCellCoordinates()
        {
            return new List<Vector2Int>(cells.Keys);
        }
    }
}
