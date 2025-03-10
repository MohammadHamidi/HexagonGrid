using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace HexaAway.Core
{
    public class Hexagon : MonoBehaviour
    {
        [SerializeField] private MeshRenderer hexRenderer;
        [SerializeField] private Transform arrowTransform;
        [SerializeField] private float moveDuration = 0.5f;
        [SerializeField] private float moveDistance = 2f;
        
        private HexCell currentCell;
        private HexDirection direction;
        private Color hexColor;
        private bool isInteractable = true;
        
        public HexCell CurrentCell => currentCell;
        public HexDirection Direction => direction;
        public Color HexColor => hexColor;
        
        public event Action<Hexagon> OnHexagonUnlocked;
        
        private void OnValidate()
        {
            if (hexRenderer == null)
                hexRenderer = GetComponentInChildren<MeshRenderer>();
        }
        
        public void Initialize(Color color, HexDirection dir)
        {
            hexColor = color;
            direction = dir;
            
            // Set color
            if (hexRenderer != null)
            {
                Material mat = hexRenderer.material;
                mat.color = hexColor;
            }
            
            // Set arrow direction
            UpdateArrowDirection();
        }
        
        private void UpdateArrowDirection()
        {
            if (arrowTransform != null)
            {
                float rotation = HexDirectionHelper.GetRotationDegrees(direction);
                arrowTransform.localRotation = Quaternion.Euler(0, rotation, 0);
            }
        }
        
        public void SetCell(HexCell cell)
        {
            currentCell = cell;
        }
        
        public void SetDirection(HexDirection newDirection)
        {
            direction = newDirection;
            UpdateArrowDirection();
        }
        
        public void SetInteractable(bool interactable)
        {
            isInteractable = interactable;
        }
        
        private void OnMouseDown()
        {
            if (isInteractable)
            {
                // Unlock the hexagon when clicked
                Unlock();
            }
        }
        
public void Unlock()
{
    // Make non-interactable during animation
    SetInteractable(false);

    // Clear from current cell
    if (currentCell != null)
    {
        currentCell.ClearHexagon();
    }

    // Get movement direction vector
    Vector3 directionVector = HexDirectionHelper.GetMovementVector(direction);

    // Get GridManager reference
    GridManager gridManager = GameManager.Instance != null ? GameManager.Instance.gridManager : null;
    if (gridManager == null)
    {
        Debug.LogError("GridManager reference is null in Hexagon.Unlock()");
        SimpleUnlockAnimation(directionVector);
        return;
    }

    // Determine current coordinates (cache if cell reference is lost)
    Vector2Int currentCoords = currentCell != null ? currentCell.Coordinates : gridManager.WorldToHex(transform.position);
    if (currentCell == null)
    {
        Debug.LogWarning($"Hexagon at {transform.position} has no cell reference, derived coords: {currentCoords}");
    }

    // Calculate how many valid (inside–grid) roll steps we can take
    Vector2Int lastValidCoords = currentCoords;
    int insideRollCount = 0;
    Vector2Int nextCoords = CalculateNextCellCoords(lastValidCoords, direction);
    while (gridManager.HasCell(nextCoords))
    {
        lastValidCoords = nextCoords;
        insideRollCount++;
        nextCoords = CalculateNextCellCoords(lastValidCoords, direction);
    }

    // If there are valid inside rolls, animate them first, then perform a single extra roll off the grid.
    if (insideRollCount > 0)
    {
        HexCell lastCell = gridManager.GetCell(lastValidCoords);
        Vector3 insideTargetPosition = lastCell.transform.position;
        Debug.Log($"Hexagon rolling {insideRollCount} step(s) inside the grid from {currentCoords}.");

        // Animate roll inside the grid
        RollAnimation(insideTargetPosition, directionVector, insideRollCount, () =>
        {
            // Now animate a single extra roll off the grid.
            Vector3 extraTargetPosition = insideTargetPosition + (directionVector.normalized * gridManager.GridConfig.hexHorizontalSpacing);
            Debug.Log("Performing a single extra roll off the grid.");
            RollAnimation(extraTargetPosition, directionVector, 1, () =>
            {
                // Once the extra roll is complete, trigger falling off the grid.
                FallOffGrid(lastValidCoords, directionVector, gridManager);
            });
        });
    }
    else
    {
        // If no valid inside roll exists (i.e. already at edge), do a single extra roll directly.
        Vector3 extraTargetPosition = transform.position + (directionVector.normalized * gridManager.GridConfig.hexHorizontalSpacing);
        Debug.Log("No inside roll available – performing a single extra roll off the grid.");
        RollAnimation(extraTargetPosition, directionVector, 1, () =>
        {
            FallOffGrid(currentCoords, directionVector, gridManager);
        });
    }
}

/// <summary>
/// Animates a roll from the current position to the given target position over a number of roll steps.
/// </summary>
/// <param name="targetPosition">The destination position for this roll phase.</param>
/// <param name="directionVector">The movement direction.</param>
/// <param name="rollCount">How many roll steps to simulate (each step rotates 120°).</param>
/// <param name="onComplete">Callback when the roll animation finishes.</param>
private void RollAnimation(Vector3 targetPosition, Vector3 directionVector, int rollCount, System.Action onComplete)
{
    float rollDuration = 0.3f * rollCount;
    float totalRotationAngle = 120f * rollCount;
    Vector3 rotationAxis = Vector3.Cross(Vector3.up, directionVector).normalized;

    Debug.Log($"Rolling from {transform.position} to {targetPosition} with {rollCount} roll(s) (total rotation {totalRotationAngle}°)");

    DOTween.Kill(transform);

    Sequence rollSequence = DOTween.Sequence();

    rollSequence.Append(
        transform.DOMove(targetPosition, rollDuration)
                 .SetEase(Ease.OutQuad)
    );

    rollSequence.Join(
        transform.DORotate(rotationAxis * totalRotationAngle, rollDuration, RotateMode.WorldAxisAdd)
                 .SetEase(Ease.OutQuad)
    );

    rollSequence.AppendInterval(0.2f);

    rollSequence.OnComplete(() =>
    {
        transform.rotation = Quaternion.identity;
        onComplete?.Invoke();
    });
}


// Added coroutine to delay the vanish
        private IEnumerator DelayedVanish()
        {
            // Wait a moment before vanishing
            yield return new WaitForSeconds(0.1f);
    
            Debug.Log("Starting vanish animation");
    
            // Now vanish
            Vanish();
        }

// Fall off the grid edge
private void FallOffGrid(Vector2Int currentCoords, Vector3 directionVector, GridManager gridManager)
{
    // Get current position for reference
    Vector3 startPosition = transform.position;
    
    // Calculate edge position - estimate where next cell would be
    Vector3 edgeDirection = directionVector.normalized;
    float cellSpacing = gridManager.GridConfig.hexHorizontalSpacing;
    
    // Calculate a position just past the edge
    Vector3 edgePosition = startPosition + (edgeDirection * cellSpacing * 1.2f);
    
    // Calculate rotation axis
    Vector3 rotationAxis = Vector3.Cross(Vector3.up, directionVector).normalized;
    
    // Animation durations
    float tipDuration = 0.3f;
    float fallDuration = 0.5f;
    
    // Create animation sequence
    Sequence fallSequence = DOTween.Sequence();
    
    // First phase: tip over the edge
    Vector3 tipPosition = startPosition + (edgeDirection * cellSpacing * 0.7f);
    tipPosition.y = 0.2f; // Maintain height
    
    fallSequence.Append(
        transform.DOMove(tipPosition, tipDuration)
        .SetEase(Ease.OutQuad)
    );
    
    // Tip over rotation
    fallSequence.Join(
        transform.DORotate(rotationAxis * 45f, tipDuration, RotateMode.WorldAxisAdd)
        .SetEase(Ease.OutQuad)
    );
    
    // Second phase: fall with gravity
    Vector3 fallTarget = edgePosition;
    fallTarget.y = -5f; // Fall below the grid
    
    fallSequence.Append(
        transform.DOMove(fallTarget, fallDuration)
        .SetEase(Ease.InQuad)
    );
    
    // Add tumbling as it falls
    Vector3 tumbleRotation = new Vector3(
        rotationAxis.x * 360f + UnityEngine.Random.Range(-90f, 90f),
        UnityEngine.Random.Range(-180f, 180f),
        rotationAxis.z * 360f + UnityEngine.Random.Range(-90f, 90f)
    );
    
    fallSequence.Join(
        transform.DORotate(tumbleRotation, fallDuration, RotateMode.LocalAxisAdd)
        .SetEase(Ease.InQuad)
    );
    
    fallSequence.OnComplete(() => {
        // Notify that hexagon was unlocked
        OnHexagonUnlocked?.Invoke(this);
        
        // Vanish
        Vanish();
    });
}

// Simple animation for fallback
private void SimpleUnlockAnimation(Vector3 directionVector)
{
    transform.DOMove(transform.position + directionVector * 2f, 0.5f)
        .SetEase(Ease.OutQuad)
        .OnComplete(() => {
            OnHexagonUnlocked?.Invoke(this);
            Vanish();
        });
}

// Helper method to calculate next cell coordinates (unchanged)
private Vector2Int CalculateNextCellCoords(Vector2Int currentCoords, HexDirection dir)
{
    switch (dir)
    {
        case HexDirection.East:
            return new Vector2Int(currentCoords.x + 1, currentCoords.y);
        case HexDirection.SouthEast:
            return new Vector2Int(currentCoords.x + 1, currentCoords.y - 1);
        case HexDirection.SouthWest:
            return new Vector2Int(currentCoords.x, currentCoords.y - 1);
        case HexDirection.West:
            return new Vector2Int(currentCoords.x - 1, currentCoords.y);
        case HexDirection.NorthWest:
            return new Vector2Int(currentCoords.x - 1, currentCoords.y + 1);
        case HexDirection.NorthEast:
            return new Vector2Int(currentCoords.x, currentCoords.y + 1);
        default:
            return currentCoords;
    }
}     
        private void Vanish()
        {
            // Scale down to zero
            transform.DOScale(Vector3.zero, 0.2f)
                .SetEase(Ease.InBack)
                .OnComplete(() => {
                    // Notify the game manager
                    GameManager.Instance.OnHexagonRemoved();
                    
                    // Destroy the gameobject
                    Destroy(gameObject);
                });
        }
    }
}