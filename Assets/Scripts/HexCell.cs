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
        public Vector2Int Coordinates => coordinates;
        public Hexagon CurrentHexagon => currentHexagon;
        public HexagonStack CurrentHexagonStack
        {
            get => currentHexagonStack;
            set => currentHexagonStack = value;
        }

        public void Initialize(Vector2Int coords)
        {
            coordinates = coords;
            
            // Apply a small Y offset to ensure the cell is visible above the ground plane
            transform.position = new Vector3(transform.position.x, cellYOffset, transform.position.z);
            
            // Set the material if available
            if (cellRenderer != null && cellRenderer.material != null)
            {
                // Create a new instance of the material to avoid shared material changes
                Material newMat = new Material(cellRenderer.material);
                newMat.color = defaultColor;
                cellRenderer.material = newMat;
            }
        }
        
        public void PlaceHexagon(Hexagon hexagon)
        {
            ClearOccupant();
            currentHexagon = hexagon;
            if (hexagon != null)
            {
                hexagon.SetCell(this);
                Vector3 targetPos = transform.position + new Vector3(0, 0.2f, 0);
                Debug.Log($"[PlaceHexagon] Placing {hexagon.name} at cell {Coordinates}, targetPos={targetPos}");
                hexagon.transform.position = targetPos;
            }
        }

        /// <summary>
        /// Place a hexagon stack on this cell.
        /// </summary>
        public void PlaceHexagonStack(HexagonStack stack)
        {
            // Clear any existing occupant
            ClearOccupant();
            
            currentHexagonStack = stack;
            
            if (stack != null)
            {
                // Set the stack's position to match the cell's position with an offset
                stack.transform.position = transform.position + new Vector3(0, 0.2f, 0);
            }
        }
        
        /// <summary>
        /// Clears any occupant (either a single hexagon or a stack) from this cell.
        /// </summary>
        public void ClearOccupant()
        {
            if (currentHexagon != null)
            {
                currentHexagon.SetCell(null);
                currentHexagon = null;
            }
            
            currentHexagonStack = null;
        }
        
        /// <summary>
        /// Clears the hexagon occupant. This is used externally (for example, when removing a hexagon).
        /// </summary>
        public void ClearHexagon()
        {
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