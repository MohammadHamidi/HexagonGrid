using UnityEngine;
using System;

namespace HexaAway.Core
{
    [CreateAssetMenu(fileName = "GridConfig", menuName = "HexaAway/Grid Configuration")]
    public class GridConfig : ScriptableObject
    {
        [Header("Grid Settings")]
        [Tooltip("The horizontal spacing between hexagons")]
        public float hexHorizontalSpacing = 1.0f;
        
        [Tooltip("The vertical spacing between hexagons")]
        public float hexVerticalSpacing = 0.866f;
        
        [Header("Grid Layout (2D Array)")]
        public GridRow[] gridRows;
    }

    [Serializable]
    public class GridRow
    {
        public bool[] cells;
    }
}