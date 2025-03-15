using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HexaAway.Core
{
    public class HexagonStack : MonoBehaviour
    {
        public List<Hexagon> hexagons = new List<Hexagon>();
        public float unlockDelay = 0.2f;
        private bool isUnlocking = false;
        private HexCell associatedCell;
        private Coroutine unlockCoroutine = null;

        public void Initialize(GameObject hexagonPrefab, Color color, HexDirection direction, HexCell cell)
        {
            associatedCell = cell;
            if (cell != null)
            {
                cell.PlaceHexagonStack(this);
            }

            // Create 3 hexagons stacked on top of each other
            for (int i = 0; i < 3; i++)
            {
                if (hexagonPrefab == null)
                {
                    Debug.LogError("Hexagon prefab is null in HexagonStack.Initialize");
                    return;
                }

                // Stack hexagons with proper vertical spacing
                Vector3 offset = new Vector3(0, i * 0.2f, 0);
                GameObject hexObj = Instantiate(hexagonPrefab, transform.position + offset, Quaternion.identity, transform);
                hexObj.name = $"Hexagon_{i}";
                
                Hexagon hex = hexObj.GetComponent<Hexagon>();
                if (hex == null)
                    hex = hexObj.AddComponent<Hexagon>();
                
                // Initialize but don't set interactable - the stack handles clicks
                hex.SetCell(cell);
                hex.Initialize(color, direction);
                hex.SetInteractable(false); // Disable individual hexagon clicks
                
                // Disable collider on individual hexagons to prevent click interference
                Collider col = hexObj.GetComponent<Collider>();
                if (col != null)
                    col.enabled = false;
                
                hexagons.Add(hex);
            }

            // Ensure the stack has a collider for mouse interaction
            BoxCollider stackCollider = GetComponent<BoxCollider>();
            if (stackCollider == null)
            {
                stackCollider = gameObject.AddComponent<BoxCollider>();
            }
            // Size the collider to cover all hexagons in the stack
            stackCollider.size = new Vector3(0.8f, 0.8f, 0.8f);
            stackCollider.center = new Vector3(0, 0.3f, 0); // Center on the stack
        }

        private void OnMouseDown()
        {
            Debug.Log($"[HexagonStack] Stack {name} clicked. isUnlocking={isUnlocking}, hexagons.Count={hexagons.Count}");
            
            if (!isUnlocking && hexagons.Count > 0)
            {
                // Stop any existing unlock coroutine
                if (unlockCoroutine != null)
                {
                    StopCoroutine(unlockCoroutine);
                }

                // Start new unlock coroutine
                unlockCoroutine = StartCoroutine(UnlockStack());
            }
        }

private IEnumerator UnlockStack()
{
    Debug.Log($"[HexagonStack] Starting to unlock stack with {hexagons.Count} hexagons");
    isUnlocking = true;

    // Clear the stack reference from the cell, but keep individual hexagons
    if (associatedCell != null)
    {
        // Don't call ClearOccupant() here as it would clear both stack and hexagons
        associatedCell.CurrentHexagonStack = null;
        Debug.Log($"[HexagonStack] Cleared stack reference from cell {associatedCell.Coordinates}");
    }

    // Track if any hexagons moved successfully
    bool anyMoved = false;
    List<Hexagon> movedHexagons = new List<Hexagon>();

    // Unlock hexagons from top to bottom
    for (int i = hexagons.Count - 1; i >= 0; i--)
    {
        Hexagon hexagon = hexagons[i];
        if (hexagon != null)
        {
            Debug.Log($"[HexagonStack] Processing hexagon {i} ({hexagon.name})");
            
            // Calculate proper stack height based on position in stack
            float stackHeight = 0.2f + (i * 0.05f);
            
            // Set up event handler to track if this hexagon moves
            bool thisHexagonMoved = false;
            hexagon.OnHexagonUnlocked += (h) => { 
                thisHexagonMoved = true;
                anyMoved = true;
                movedHexagons.Add(h);
            };
            
            // Ensure this hexagon has the correct cell reference and position
            if (hexagon.CurrentCell == null || hexagon.CurrentCell != associatedCell)
            {
                hexagon.SetCell(associatedCell);
                // Position the hexagon at the cell with proper height offset
                Vector3 targetPos = associatedCell.transform.position + new Vector3(0, stackHeight, 0);
                hexagon.transform.position = targetPos;
                Debug.Log($"[HexagonStack] Positioned hexagon {hexagon.name} at {targetPos} with stackHeight={stackHeight}");
            }
            
            // Store the stack height in the hexagon for proper bump recovery
            hexagon.SetStackHeight(stackHeight);
            Debug.Log($"[HexagonStack] Set stackHeight={stackHeight} for {hexagon.name}");
            
            // Detach from stack parent
            hexagon.transform.SetParent(null, true);
            
            // Make interactable so it can be clicked directly if it doesn't move
            hexagon.SetInteractable(true);
            
            // Unlock the hexagon (trigger movement)
            Debug.Log($"[HexagonStack] Calling Unlock() on hexagon {hexagon.name}");
            hexagon.Unlock();
            
            // Add delay between hexagons to create a cascade effect
            yield return new WaitForSeconds(unlockDelay);
        }
    }

    // Wait a bit for animations to potentially complete
    float timeoutCounter = 0f;
    float maxTimeout = 2.0f;
    
    while (movedHexagons.Count < hexagons.Count && timeoutCounter < maxTimeout)
    {
        timeoutCounter += Time.deltaTime;
        yield return null;
    }

    // Handle aftermath
    if (anyMoved)
    {
        Debug.Log($"[HexagonStack] At least one hexagon moved successfully, cleaning up");
        
        // Remove any moved hexagons from our list
        foreach (Hexagon movedHex in movedHexagons)
        {
            hexagons.Remove(movedHex);
        }
        
        // Clear the list and destroy the stack object if all moved
        if (hexagons.Count == 0)
        {
            yield return new WaitForSeconds(unlockDelay);
            Destroy(gameObject);
        }
        else
        {
            // Some hexagons couldn't move (bumped)
            Debug.Log($"[HexagonStack] {hexagons.Count} hexagons couldn't move, rebuilding partial stack");
            RebuildStack();
        }
    }
    else
    {
        // No hexagons moved - all were blocked
        Debug.Log($"[HexagonStack] No hexagons moved, rebuilding full stack");
        RebuildStack();
    }

    isUnlocking = false;
    unlockCoroutine = null;
}        
private void RebuildStack()
{
    // First ensure each hexagon has its original cell reference cleared to avoid confusion
    foreach (Hexagon hex in hexagons)
    {
        // Disable interactability again as we're putting them back in the stack
        hex.SetInteractable(false);
        // Clear cell to prevent conflicting references
        hex.SetCell(null);
    }

    // Re-stack the hexagons
    for (int i = 0; i < hexagons.Count; i++)
    {
        Hexagon hex = hexagons[i];
        hex.transform.SetParent(transform, true);
        
        // Calculate the appropriate stack height for this position
        float stackHeight = 0.2f + (i * 0.05f);
        
        // Update the hexagon's stack height property
        hex.SetStackHeight(stackHeight);
        
        // Reset local position with proper spacing
        hex.transform.localPosition = new Vector3(0, i * 0.2f, 0);
        
        Debug.Log($"[RebuildStack] Repositioned hexagon {hex.name} at local position y={i * 0.2f} with stackHeight={stackHeight}");
    }

    // Reconnect the stack to the cell
    if (associatedCell != null)
    {
        // Clear any previous occupants to be safe
        associatedCell.ClearOccupant();
        associatedCell.PlaceHexagonStack(this);

        // Make sure each hexagon references this cell
        foreach (Hexagon hex in hexagons)
        {
            hex.SetCell(associatedCell);
        }
        
        Debug.Log($"[HexagonStack] Stack rebuilt and placed on cell {associatedCell.Coordinates}");
    }
}    }
}