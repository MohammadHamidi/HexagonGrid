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
            
            // Register with the cell
            if (cell != null)
            {
                cell.PlaceHexagonStack(this);
            }
            
            // Create three hexagons, stacking them vertically (adjust offset as needed)
            for (int i = 0; i < 3; i++)
            {
                // Ensure the prefab is valid
                if (hexagonPrefab == null)
                {
                    Debug.LogError("Hexagon prefab is null in HexagonStack.Initialize");
                    return;
                }
                
                // The bottom hexagon is at parent's position; each subsequent one is raised
                Vector3 offset = new Vector3(0, i * 0.2f, 0);
                GameObject hexObj = Instantiate(hexagonPrefab, transform.position + offset, Quaternion.identity, transform);
                hexObj.name = $"Hexagon_{i}";
                
                // Get or add the Hexagon component
                Hexagon hex = hexObj.GetComponent<Hexagon>();
                if (hex == null)
                    hex = hexObj.AddComponent<Hexagon>();
                
                // Important: associate the cell with each hexagon in the stack
                hex.SetCell(cell);
                
                // Initialize the hexagon with color and direction
                hex.Initialize(color, direction);
                
                // Optionally disable individual hexagon colliders so the stack handles clicks
                Collider col = hexObj.GetComponent<Collider>();
                if (col != null)
                    col.enabled = false;
                
                // Add to our list
                hexagons.Add(hex);
            }
        }

        /// <summary>
        /// When the stack is clicked, unlock all hexagons from top to bottom.
        /// </summary>
        private void OnMouseDown()
        {
            if (!isUnlocking && hexagons.Count > 0)
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
            
            // Ensure we have hexagons to unlock
            if (hexagons.Count == 0)
            {
                isUnlocking = false;
                yield break;
            }
            
            // Clear the stack from the cell - do this only ONCE for the whole stack
            if (associatedCell != null)
            {
                associatedCell.ClearOccupant();
            }
            
            // Track how many hexagons actually moved (were removed)
            int removedHexagons = 0;
            
            // Unlock from top (last in list) to bottom (first in list)
            for (int i = hexagons.Count - 1; i >= 0; i--)
            {
                Hexagon hexagon = hexagons[i];
                if (hexagon != null)
                {
                    // Important: Make sure each hexagon has a valid cell reference
                    if (hexagon.CurrentCell == null)
                    {
                        hexagon.SetCell(associatedCell);
                    }
                    
                    // Create a copy of the current position before unlocking
                    Vector3 originalPosition = hexagon.transform.position;
                    
                    // Detach from stack to allow it to move freely
                    hexagon.transform.SetParent(null, true);
                    
                    // Unlock the hexagon
                    hexagon.Unlock();
                    
                    // Wait before unlocking the next one
                    yield return new WaitForSeconds(unlockDelay);
                    
                    // Check if the hexagon actually moved (if it was blocked, it will still be at its original position)
                    if (Vector3.Distance(hexagon.transform.position, originalPosition) > 0.1f)
                    {
                        removedHexagons++;
                    }
                }
            }
            
            // Only destroy the stack if at least one hexagon was removed
            if (removedHexagons > 0)
            {
                // Clear our list since all hexagons are now independently managed
                hexagons.Clear();
                
                // Clean up after all hexagons are gone
                yield return new WaitForSeconds(unlockDelay);
                Destroy(gameObject);
            }
            else
            {
                // If no hexagons moved, restore the stack's state
                if (associatedCell != null)
                {
                    associatedCell.PlaceHexagonStack(this);
                }
            }
            
            isUnlocking = false;
        }
    }
}