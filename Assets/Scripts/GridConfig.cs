using UnityEngine;

namespace HexaAway.Core
{
    [CreateAssetMenu(fileName = "GridConfig", menuName = "HexaAway/Grid Configuration")]
    public class GridConfig : ScriptableObject
    {
        [Header("Grid Settings")]
        [Tooltip("The horizontal spacing between hexagons")]
        public float hexHorizontalSpacing = 1.0f;
        
        [Tooltip("The vertical spacing between hexagons")]
        public float hexVerticalSpacing = 0.866f; // sqrt(3)/2
        
        [Header("Layout Settings")]
        [Tooltip("Default grid width in hexes")]
        public int defaultGridWidth = 5;
        
        [Tooltip("Default grid height in hexes")]
        public int defaultGridHeight = 5;
        
        [Header("Visual Settings")]
        [Tooltip("The material to use for empty grid cells")]
        public Material gridCellMaterial;
        
        [Tooltip("Whether to show the grid outlines")]
        public bool showGridOutlines = true;
        
        [Tooltip("The color for grid outlines")]
        public Color gridOutlineColor = new Color(1f, 1f, 1f, 0.3f);
    }
}