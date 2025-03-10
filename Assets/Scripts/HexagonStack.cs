using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HexaAway.Core
{
    public class HexagonStack : MonoBehaviour
    {
        // List to store the three hexagon instances
        public List<Hexagon> hexagons = new List<Hexagon>();
        // Delay between unlocking each hexagon (from top to bottom)
        public float unlockDelay = 0.2f;
        private bool isUnlocking = false;
        private HexCell associatedCell;

        /// <summary>
        /// Initialize the stack by creating 3 hexagons as children.
        /// </summary>
        /// <param name="hexagonPrefab">The prefab for a single hexagon.</param>
        /// <param name="color">Color to apply to all hexagons in the stack.</param>
        /// <param name="direction">The starting direction.</param>
        /// <param name="cell">The HexCell where the bottom hexagon is placed.</param>
        public void Initialize(GameObject hexagonPrefab, Color color, HexDirection direction, HexCell cell)
        {
            associatedCell = cell;
            // Create three hexagons, stacking them vertically (adjust offset as needed)
            for (int i = 0; i < 3; i++)
            {
                // The bottom hexagon is at parent's position; each subsequent one is raised
                Vector3 offset = new Vector3(0, i * 0.2f, 0);
                GameObject hexObj = Instantiate(hexagonPrefab, transform.position + offset, Quaternion.identity, transform);
                hexObj.name = $"Hexagon_{i}";
                
                // Get or add the Hexagon component
                Hexagon hex = hexObj.GetComponent<Hexagon>();
                if (hex == null)
                    hex = hexObj.AddComponent<Hexagon>();
                
                // Initialize the hexagon with color and direction
                hex.Initialize(color, direction);
                
                // Optionally disable individual hexagon colliders so the stack handles clicks
                Collider col = hexObj.GetComponent<Collider>();
                if (col != null)
                    col.enabled = false;
                
                // Add to our list
                hexagons.Add(hex);
            }
            
            // Mark the cell as occupied (if your HexCell class supports it)
            // cell.PlaceHexagonStack(this);
        }

        /// <summary>
        /// When the stack is clicked, unlock all hexagons from top to bottom.
        /// </summary>
        private void OnMouseDown()
        {
            if (!isUnlocking)
            {
                StartCoroutine(UnlockStack());
            }
        }

        /// <summary>
        /// Coroutine to sequentially unlock hexagons with a delay.
        /// </summary>
        private IEnumerator UnlockStack()
        {
            isUnlocking = true;
            // Unlock from top (last in list) to bottom (first in list)
            for (int i = hexagons.Count - 1; i >= 0; i--)
            {
                hexagons[i].Unlock();
                yield return new WaitForSeconds(unlockDelay);
            }
        }
    }
}