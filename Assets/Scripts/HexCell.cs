using UnityEngine;

namespace HexaAway.Core
{
    public class HexCell : MonoBehaviour
    {
        [SerializeField] private MeshRenderer cellRenderer;

        private Vector2Int coordinates;
        private Hexagon currentHexagon;
        private HexagonStack currentHexagonStack;
        
        // The cell is considered occupied if it has either a hexagon or a stack.
        public bool IsOccupied => currentHexagon != null || currentHexagonStack != null;
        public Vector2Int Coordinates => coordinates;
        public Hexagon CurrentHexagon => currentHexagon;
        public HexagonStack CurrentHexagonStack => currentHexagonStack;
        
        public void Initialize(Vector2Int coords)
        {
            coordinates = coords;
            
            // Set the material if available from the GridConfig (with additional null checks)
            // if (cellRenderer != null &&
            //     GridManager.Instance != null &&
            //     GridManager.Instance.GridConfig != null &&
            //     GridManager.Instance.GridConfig.gridCellMaterial != null)
            // {
            //     cellRenderer.material = GridManager.Instance.GridConfig.gridCellMaterial;
            // }
        }
        
        public void PlaceHexagon(Hexagon hexagon)
        {
            // Clear any existing occupant
            ClearOccupant();
            
            // Assign the new hexagon
            currentHexagon = hexagon;
            
            if (hexagon != null)
            {
                // Set this as the hexagon's cell
                hexagon.SetCell(this);
                
                // Position the hexagon at the cell's position with an offset
                hexagon.transform.position = transform.position + new Vector3(0, 0.2f, 0);
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
            
            if (currentHexagonStack != null)
            {
                // Optionally add additional cleanup for stacks if needed
                currentHexagonStack = null;
            }
        }
        
        /// <summary>
        /// Clears the hexagon occupant. This is used externally (for example, when removing a hexagon).
        /// </summary>
        public void ClearHexagon()
        {
            ClearOccupant();
        }
        
        private void OnDrawGizmos()
        {
            // // Visual debugging: Draw the cell outline if the grid is configured to show it.
            // if (GridManager.Instance != null &&
            //     GridManager.Instance.GridConfig != null &&
            //     GridManager.Instance.GridConfig.showGridOutlines)
            // {
            //     Gizmos.color = GridManager.Instance.GridConfig.gridOutlineColor;
            //     Gizmos.DrawWireSphere(transform.position, 0.5f);
            //     
            //     // Indicate occupied cells with a small green sphere.
            //     if (IsOccupied)
            //     {
            //         Gizmos.color = Color.green;
            //         Gizmos.DrawSphere(transform.position, 0.2f);
            //     }
            // }
        }
    }
}
