using UnityEngine;

namespace HexaAway.Core
{
    public class HexCell : MonoBehaviour
    {
        [SerializeField] private MeshRenderer cellRenderer;
        [SerializeField] private Color defaultColor = new Color(0.8f, 0.8f, 0.8f, 0.5f);
        [SerializeField] private float cellYOffset = 0.01f; // Small Y offset to prevent Z-fighting

        private Vector2Int coordinates;
        private Hexagon currentHexagon;
        private HexagonStack currentHexagonStack;
        
        // The cell is considered occupied if it has either a hexagon or a stack.
        public bool IsOccupied => currentHexagon != null || currentHexagonStack != null;
        public Hexagon OccupyingHexagon => currentHexagon; // Added explicit property for diagnostic access
        public Vector2Int Coordinates => coordinates;
        public Hexagon CurrentHexagon => currentHexagon;
        public HexagonStack CurrentHexagonStack
        {
            get => currentHexagonStack;
            set 
            {
                Debug.Log($"[SetCurrentHexagonStack] Cell {coordinates} - Setting stack from {(currentHexagonStack != null ? currentHexagonStack.name : "null")} to {(value != null ? value.name : "null")}");
                currentHexagonStack = value;
            }
        }

        public void Initialize(Vector2Int coords)
        {
            coordinates = coords;
            
            // Apply a small Y offset to ensure the cell is visible above the ground plane
            Vector3 originalPos = transform.position;
            transform.position = new Vector3(transform.position.x, cellYOffset, transform.position.z);
            Debug.Log($"[Initialize] Cell at {coords} initialized. Position adjusted from {originalPos} to {transform.position}");
            
            // Set the material if available
            if (cellRenderer != null && cellRenderer.material != null)
            {
                // Create a new instance of the material to avoid shared material changes
                Material newMat = new Material(cellRenderer.material);
                newMat.color = defaultColor;
                cellRenderer.material = newMat;
                Debug.Log($"[Initialize] Cell at {coords} material set to default color");
            }
            else
            {
                Debug.LogWarning($"[Initialize] Cell at {coords} missing renderer or material");
            }
        }
        
        public void PlaceHexagon(Hexagon hexagon)
        {
            Debug.Log($"[PlaceHexagon] Cell {coordinates} - Attempting to place hexagon {(hexagon != null ? hexagon.name : "null")}");
            Debug.Log($"[BUG_TRACE] Cell {coordinates} - PlaceHexagon called with hexagon {(hexagon != null ? hexagon.name : "null")}");
    
            if (IsOccupied)
            {
                Debug.LogWarning($"[PlaceHexagon] Cell {coordinates} - Already occupied by hexagon: {(currentHexagon != null ? currentHexagon.name : "null")}, stack: {(currentHexagonStack != null ? currentHexagonStack.name : "null")}");
            }
    
            Debug.Log($"[PlaceHexagon] Cell {coordinates} - Clearing current occupant");
            ClearOccupant();
    
            currentHexagon = hexagon;
            Debug.Log($"[PlaceHexagon] Cell {coordinates} - Set currentHexagon to {(hexagon != null ? hexagon.name : "null")}");
    
            if (hexagon != null)
            {
                Debug.Log($"[PlaceHexagon] Cell {coordinates} - Setting hexagon's cell reference");
                hexagon.SetCell(this);
                Vector3 originalPos = hexagon.transform.position;
                Vector3 targetPos = transform.position + new Vector3(0, 0.2f, 0);
                Debug.Log($"[PlaceHexagon] Placing {hexagon.name} at cell {Coordinates}, moving from {originalPos} to targetPos={targetPos}");
                Debug.Log($"[BUG_TRACE] Cell {coordinates} - About to change hexagon position from {originalPos} to {targetPos}");
                hexagon.transform.position = targetPos;
        
                // Additional diagnostics for hexagon state
                Debug.Log($"[PlaceHexagon] Cell {coordinates} - Hexagon state after placement: position={hexagon.transform.position}, rotation={hexagon.transform.rotation.eulerAngles}");
                Debug.Log($"[BUG_TRACE] Cell {coordinates} - Final hexagon position after PlaceHexagon: {hexagon.transform.position}");
            }
        }

        /// <summary>
        /// Place a hexagon stack on this cell.
        /// </summary>
        public void PlaceHexagonStack(HexagonStack stack)
        {
            Debug.Log($"[PlaceHexagonStack] Cell {coordinates} - Attempting to place stack {(stack != null ? stack.name : "null")}");
            if (IsOccupied)
            {
                Debug.LogWarning($"[PlaceHexagonStack] Cell {coordinates} - Already occupied by hexagon: {(currentHexagon != null ? currentHexagon.name : "null")}, stack: {(currentHexagonStack != null ? currentHexagonStack.name : "null")}");
            }
            
            // Clear any existing occupant
            Debug.Log($"[PlaceHexagonStack] Cell {coordinates} - Clearing current occupant");
            ClearOccupant();
            
            currentHexagonStack = stack;
            Debug.Log($"[PlaceHexagonStack] Cell {coordinates} - Set currentHexagonStack to {(stack != null ? stack.name : "null")}");
            
            if (stack != null)
            {
                Vector3 originalPos = stack.transform.position;
                Vector3 targetPos = transform.position + new Vector3(0, 0.2f, 0);
                Debug.Log($"[PlaceHexagonStack] Cell {coordinates} - Moving stack from {originalPos} to {targetPos}");
                
                // Set the stack's position to match the cell's position with an offset
                stack.transform.position = targetPos;
                
                // Additional diagnostics for stack state
                Debug.Log($"[PlaceHexagonStack] Cell {coordinates} - Stack state after placement: position={stack.transform.position}");
            }
        }
        
        /// <summary>
        /// Clears any occupant (either a single hexagon or a stack) from this cell.
        /// </summary>
        public void ClearOccupant()
        {
            Debug.Log($"[ClearOccupant] Cell {coordinates} - Clearing occupant. Current hexagon: {(currentHexagon != null ? currentHexagon.name : "null")}, current stack: {(currentHexagonStack != null ? currentHexagonStack.name : "null")}");
            
            if (currentHexagon != null)
            {
                Debug.Log($"[ClearOccupant] Cell {coordinates} - Clearing hexagon cell reference for {currentHexagon.name}");
                currentHexagon.SetCell(null);
                currentHexagon = null;
                Debug.Log($"[ClearOccupant] Cell {coordinates} - Set currentHexagon to null");
            }
            
            if (currentHexagonStack != null)
            {
                Debug.Log($"[ClearOccupant] Cell {coordinates} - Set currentHexagonStack to null from {currentHexagonStack.name}");
                currentHexagonStack = null;
            }
        }
        
        /// <summary>
        /// Clears the hexagon occupant. This is used externally (for example, when removing a hexagon).
        /// </summary>
        public void ClearHexagon()
        {
            Debug.Log($"[ClearHexagon] Cell {coordinates} - External call to clear hexagon. Current hexagon: {(currentHexagon != null ? currentHexagon.name : "null")}");
            ClearOccupant();
        }
        
#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            // Skip if we're in Play Mode as the game handles visualization
            if (Application.isPlaying)
                return;
                
            // Visual debugging: Draw the cell outline and coordinates in the scene view
            Gizmos.color = IsOccupied ? Color.green : Color.gray;
            float radius = 0.4f;
            
            // Draw hexagon shape
            for (int i = 0; i < 6; i++)
            {
                float startAngle = ((i * 60f) - 30f) * Mathf.Deg2Rad;
                float endAngle = (((i + 1) * 60f) - 30f) * Mathf.Deg2Rad;
                
                Vector3 start = transform.position + new Vector3(
                    radius * Mathf.Cos(startAngle),
                    0.01f,
                    radius * Mathf.Sin(startAngle)
                );
                
                Vector3 end = transform.position + new Vector3(
                    radius * Mathf.Cos(endAngle),
                    0.01f,
                    radius * Mathf.Sin(endAngle)
                );
                
                Gizmos.DrawLine(start, end);
            }
            
            // Draw coordinates
            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.1f, $"{coordinates.x},{coordinates.y}");
        }
#endif
    }
}