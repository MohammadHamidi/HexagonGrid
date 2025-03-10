using System;
using UnityEngine;

namespace HexaAway.Core
{
    [Serializable]
    public class HexagonData
    {
        public Vector2Int coordinates;
        public HexDirection direction;
        public int colorIndex; // Index into the color palette
    }
    
    [CreateAssetMenu(fileName = "Level", menuName = "HexaAway/Level Configuration")]
    public class LevelConfig : ScriptableObject
    {
        [Header("Level Info")]
        public int levelNumber = 1;
        public string levelName = "Level 1";
        public int moveLimit = 15;
        
        [Header("Colors")]
        public Color[] colorPalette = new Color[] {
            Color.blue, Color.green, Color.red
        };
        
        [Header("Hexagons")]
        public HexagonData[] hexagons;
        
        [Header("Level Objectives")]
        public int targetHexagonsToRemove = 3;
    }
}