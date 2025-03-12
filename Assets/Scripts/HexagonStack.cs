using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HexaAway.Core
{
    public class HexagonStack : MonoBehaviour
    {
        Coroutine coroutine = null;
        public List<Hexagon> hexagons = new List<Hexagon>();
        public float unlockDelay = 0.2f;
        private bool isUnlocking = false;
        private HexCell associatedCell;

        public void Initialize(GameObject hexagonPrefab, Color color, HexDirection direction, HexCell cell)
        {
            associatedCell = cell;
            if (cell != null)
            {
                cell.PlaceHexagonStack(this);
            }

            for (int i = 0; i < 3; i++)
            {
                if (hexagonPrefab == null)
                {
                    Debug.LogError("Hexagon prefab is null in HexagonStack.Initialize");
                    return;
                }

                Vector3 offset = new Vector3(0, i * 0.2f, 0);
                GameObject hexObj = Instantiate(hexagonPrefab, transform.position + offset, Quaternion.identity,
                    transform);
                hexObj.name = $"Hexagon_{i}";
                Hexagon hex = hexObj.GetComponent<Hexagon>();
                if (hex == null)
                    hex = hexObj.AddComponent<Hexagon>();
                hex.SetCell(cell);
                hex.Initialize(color, direction);
                Collider col = hexObj.GetComponent<Collider>();
                if (col != null)
                    col.enabled = false;
                hexagons.Add(hex);
            }
        }

        private void OnMouseDown()
        {
            if (!isUnlocking && hexagons.Count > 0)
            {
                if (coroutine != null)
                {
                    StopCoroutine(coroutine);
                }

                coroutine = StartCoroutine(UnlockStack());
            }
        }

        private IEnumerator UnlockStack()
        {
            isUnlocking = true;

            // Clear the stack reference from the cell, but keep individual hexagons
            if (associatedCell != null)
            {
                // Don't call ClearOccupant() here as it would clear both stack and hexagons
                associatedCell.CurrentHexagonStack = null;
            }

            bool anyMoved = false;

            // Unlock hexagons from top to bottom
            for (int i = hexagons.Count - 1; i >= 0; i--)
            {
                Hexagon hexagon = hexagons[i];
                if (hexagon != null)
                {
                    // CRITICAL FIX: Make sure each hexagon has the correct cell reference AND position
                    if (hexagon.CurrentCell == null || hexagon.CurrentCell != associatedCell)
                    {
                        hexagon.SetCell(associatedCell);
                        // Ensure physical position matches the cell
                        Vector3 targetPos = associatedCell.transform.position + new Vector3(0, 0.2f + (i * 0.05f), 0);
                        hexagon.transform.position = targetPos;
                        Debug.Log($"[UnlockStack] Positioned hexagon {hexagon.name} at {targetPos}");
                    }

                    hexagon.OnHexagonUnlocked += (h) => { anyMoved = true; };
            
                    // Detach from parent
                    hexagon.transform.SetParent(null, true);
            
                    // Unlock it
                    hexagon.Unlock();
            
                    yield return new WaitForSeconds(unlockDelay);
                }
            }

            // Rest of method remains similar...
         
            

            if (anyMoved)
            {
                // Normal behavior: at least one hexagon moved.
                hexagons.Clear();
                yield return new WaitForSeconds(unlockDelay);
                Destroy(gameObject);
            }
            else
            {
                // Bump case: none of the hexagons moved.
                // First ensure each hexagon has its original cell reference cleared to avoid confusion
                for (int i = 0; i < hexagons.Count; i++)
                {
                    hexagons[i].SetCell(null);
                }

                // Roll back: reassemble the stack as it was originally.
                for (int i = 0; i < hexagons.Count; i++)
                {
                    Hexagon hex = hexagons[i];
                    hex.transform.SetParent(transform, true);
                    // Reset local position (using the same offset as in Initialize).
                    hex.transform.localPosition = new Vector3(0, i * 0.2f, 0);
                    hex.SetInteractable(true);
                }

                // Now reconnect the stack to the cell
                if (associatedCell != null)
                {
                    // Clear any previous occupants to be safe
                    associatedCell.ClearOccupant();
                    associatedCell.PlaceHexagonStack(this);

                    // Make sure each hexagon references this cell
                    for (int i = 0; i < hexagons.Count; i++)
                    {
                        hexagons[i].SetCell(associatedCell);
                    }
                }
            }

            isUnlocking = false;
        }
    }
}