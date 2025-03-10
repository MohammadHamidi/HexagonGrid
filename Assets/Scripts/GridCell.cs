using UnityEngine;

namespace HexaAway.Core
{
    public class HexCell : MonoBehaviour
    {
        [SerializeField] private MeshRenderer cellRenderer;
        
        private Vector2Int coordinates;
        private Hexagon currentHexagon;
        
        public Vector2Int Coordinates => coordinates;
        public bool IsOccupied => currentHexagon != null;
        public Hexagon CurrentHexagon => currentHexagon;
        
        public void Initialize(Vector2Int coords)
        {
            coordinates = coords;
            
            // Set the material if available
            if (cellRenderer != null && GridManager.Instance.GridConfig.gridCellMaterial != null)
            {
                cellRenderer.material = GridManager.Instance.GridConfig.gridCellMaterial;
            }
        }
        
        public void PlaceHexagon(Hexagon hexagon)
        {
            // Clear any existing hexagon
            ClearHexagon();
            
            // Assign the new hexagon
            currentHexagon = hexagon;
            
            if (hexagon != null)
            {
                // Set this as the hexagon's cell
                hexagon.SetCell(this);
                
                // Position the hexagon
// Position the hexagon
                hexagon.transform.position = transform.position + new Vector3(0, 0.2f, 0);            }
        }
        
        public void ClearHexagon()
        {
            if (currentHexagon != null)
            {
                // Clear the reference in the hexagon
                currentHexagon.SetCell(null);
                currentHexagon = null;
            }
        }
        
        private void OnDrawGizmos()
        {
            // Visual debugging
            if (GridManager.Instance != null && GridManager.Instance.GridConfig.showGridOutlines)
            {
                Gizmos.color = GridManager.Instance.GridConfig.gridOutlineColor;
                Gizmos.DrawWireSphere(transform.position, 0.5f);
                
                // Show if occupied
                if (IsOccupied)
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawSphere(transform.position, 0.2f);
                }
            }
        }
    }
}