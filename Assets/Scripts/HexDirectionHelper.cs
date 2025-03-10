using UnityEngine;

namespace HexaAway.Core
{
    public static class HexDirectionHelper
    {
        // Directional vectors in axial coordinates
        private static readonly Vector2Int[] DirectionVectors = new Vector2Int[]
        {
            new Vector2Int(1, 0),   // East
            new Vector2Int(1, -1),  // Southeast
            new Vector2Int(0, -1),  // Southwest
            new Vector2Int(-1, 0),  // West
            new Vector2Int(-1, 1),  // Northwest
            new Vector2Int(0, 1)    // Northeast
        };
        
        // Get the directional vector for a direction
        public static Vector2Int GetDirectionVector(HexDirection direction)
        {
            return DirectionVectors[(int)direction];
        }
        
        // Get the rotation in degrees for a direction (for arrow display)
        public static float GetRotationDegrees(HexDirection direction)
        {
            switch (direction)
            {
                case HexDirection.East: return 0f;
                case HexDirection.SouthEast: return 60f;
                case HexDirection.SouthWest: return 120f;
                case HexDirection.West: return 180f;
                case HexDirection.NorthWest: return 240f;
                case HexDirection.NorthEast: return 300f;
                default: return 0f;
            }
        }
        
        // Get 3D movement vector for visual movement
        public static Vector3 GetMovementVector(HexDirection direction)
        {
            switch (direction)
            {
                case HexDirection.East: return new Vector3(1, 0, 0);
                case HexDirection.SouthEast: return new Vector3(0.5f, 0, -0.866f);
                case HexDirection.SouthWest: return new Vector3(-0.5f, 0, -0.866f);
                case HexDirection.West: return new Vector3(-1, 0, 0);
                case HexDirection.NorthWest: return new Vector3(-0.5f, 0, 0.866f);
                case HexDirection.NorthEast: return new Vector3(0.5f, 0, 0.866f);
                default: return Vector3.zero;
            }
        }
        
        // Get a random direction
        public static HexDirection GetRandomDirection()
        {
            return (HexDirection)Random.Range(0, 6);
        }
    }
}