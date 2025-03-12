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
        [SerializeField] private float rollDuration = 0.3f;
        [SerializeField] private float hexRadius = 0.5f; // Radius of the hexagon for pivot calculations
        [SerializeField] private float rollbackDelay = 0.2f; // Delay before rolling back after collision
        
        private HexCell currentCell;
        private HexDirection direction;
        private Color hexColor;
        private bool isInteractable = true;
        private bool isAnimating = false;
        private bool isBeingRemoved = false; // Flag to track if this hexagon is being removed
        
        public HexCell CurrentCell => currentCell;
        public HexDirection Direction => direction;
        public Color HexColor => hexColor;
        
        public event Action<Hexagon> OnHexagonUnlocked;
        
        private void OnValidate()
        {
            if (hexRenderer == null)
                hexRenderer = GetComponentInChildren<MeshRenderer>();
        }
        
        /// <summary>
        /// Initialize the hexagon's color and arrow direction.
        /// </summary>
        public void Initialize(Color color, HexDirection dir)
        {
            hexColor = color;
            direction = dir;
            
            // Set color
            if (hexRenderer != null)
            {
                Material mat = hexRenderer.material;
                if (mat != null)
                {
                    mat.color = hexColor;
                }
            }
            
            // Set arrow direction
            UpdateArrowDirection();
        }
        
        /// <summary>
        /// Update the arrowTransform to match the HexDirection.
        /// </summary>
        private void UpdateArrowDirection()
        {
            if (arrowTransform != null)
            {
                float rotation = HexDirectionHelper.GetRotationDegrees(direction);
                arrowTransform.localRotation = Quaternion.Euler(90, rotation, 0);
            }
        }

        /// <summary>
        /// Set the cell that this hexagon currently occupies.
        /// </summary>
        public void SetCell(HexCell cell)
        {
            currentCell = cell;
        }
        
        /// <summary>
        /// Change the hexagon's direction and update its arrow.
        /// </summary>
        public void SetDirection(HexDirection newDirection)
        {
            direction = newDirection;
            UpdateArrowDirection();
        }
        
        /// <summary>
        /// Enable or disable interaction with this hexagon.
        /// </summary>
        public void SetInteractable(bool interactable)
        {
            isInteractable = interactable;
        }
        
        /// <summary>
        /// When clicked, attempt to unlock (roll off the grid).
        /// </summary>
        private void OnMouseDown()
        {
            if (isInteractable && !isAnimating && !isBeingRemoved)
            {
                Unlock();
            }
        }

        /// <summary>
        /// Public method to unlock this hexagon by rolling it off the grid.
        /// </summary>
        public void Unlock()
        {
            if (isAnimating || isBeingRemoved) return;
            
            // Start the unlocking process as a coroutine
            StartCoroutine(UnlockRoutine());
        }

        /// <summary>
        /// Coroutine that handles rolling inside the grid, checking for collisions,
        /// rolling off-grid if path is clear, or rolling back if blocked.
        /// </summary>
        private IEnumerator UnlockRoutine()
        {
            isAnimating = true;
            
            // Make non-interactable during animation
            SetInteractable(false);

            // Get GridManager reference
            if (GameManager.Instance == null || GameManager.Instance.gridManager == null)
            {
                Debug.LogError("GridManager reference is null in Hexagon.UnlockRoutine()");
                // Fallback simple animation if no grid
                SimpleUnlockAnimation(HexDirectionHelper.GetMovementVector(direction));
                isAnimating = false;
                yield break;
            }
            
            GridManager gridManager = GameManager.Instance.gridManager;

            // Handle the case where the current cell is null
            if (currentCell == null)
            {
                Debug.LogWarning("Current cell is null in Hexagon.UnlockRoutine(). Attempting to find cell from position.");
                
                // Try to find the cell based on our world position
                Vector2Int coords = gridManager.WorldToHex(transform.position);
                
                // Get the cell at these coordinates
                currentCell = gridManager.GetCell(coords);
                
                // If still null, we can't proceed with rolling
                if (currentCell == null)
                {
                    Debug.LogError("Failed to find a valid cell for hexagon position. Using fallback animation.");
                    SimpleUnlockAnimation(HexDirectionHelper.GetMovementVector(direction));
                    isAnimating = false;
                    yield break;
                }
            }
            
            Vector2Int currentCoords = currentCell.Coordinates;
            
            // Check if the next step is possible - if it's blocked or off grid
            Vector2Int nextCoords = gridManager.GetNeighborCoordinate(currentCoords, direction);
            
            // Check if the next position is off the grid (we can roll off) 
            if (!gridManager.HasCell(nextCoords))
            {
                // We can roll off the grid
                isBeingRemoved = true;
                
                // Clear from current cell
                currentCell.ClearHexagon();
                
                // Roll off the grid
                Vector3 startPos = currentCell.transform.position;
                Vector3 offGridPos = gridManager.HexToWorld(nextCoords);
                
                yield return RollBetweenPositions(startPos, offGridPos, rollDuration);
                
                // Fall off
                FallOffGrid(nextCoords, direction, gridManager);
                isAnimating = false;
                yield break;
            }
            
            // Check if the next position is blocked (occupied by another hexagon)
            if (IsPathBlocked(nextCoords, gridManager))
            {
                // Path is blocked, just do a bump animation
                yield return BumpAnimation();
                
                // We're still in our original position, make interactable again
                SetInteractable(true);
                isAnimating = false;
                yield break;
            }
            
            // The path is clear for at least one step
            
            // Temporarily remove from the current cell
            currentCell.ClearHexagon();
            
            // Roll to the next cell
            Vector3 fromPos = currentCell.transform.position;
            Vector3 toPos = gridManager.GetCell(nextCoords).transform.position;
            
            yield return RollBetweenPositions(fromPos, toPos, rollDuration);
            
            // Check if we can continue rolling
            Vector2Int currentPathCoords = nextCoords;
            bool pathClear = true;
            bool reachedEdge = false;
            
            // Keep track of all cells we've visited to roll back if needed
            List<Vector2Int> visitedCells = new List<Vector2Int> { currentCoords, currentPathCoords };
            
            while (pathClear && !reachedEdge)
            {
                // Get the next position in our direction
                Vector2Int nextPathCoords = gridManager.GetNeighborCoordinate(currentPathCoords, direction);
                
                // Check if we're off the grid
                if (!gridManager.HasCell(nextPathCoords))
                {
                    reachedEdge = true;
                    isBeingRemoved = true;
                    
                    // Roll off the grid and fall
                    Vector3 lastCellPos = gridManager.GetCell(currentPathCoords).transform.position;
                    Vector3 offGridPos = gridManager.HexToWorld(nextPathCoords);
                    
                    yield return RollBetweenPositions(lastCellPos, offGridPos, rollDuration);
                    
                    // Animate falling off
                    FallOffGrid(nextPathCoords, direction, gridManager);
                    isAnimating = false;
                    yield break;
                }
                
                // Check if the next cell is blocked
                if (IsPathBlocked(nextPathCoords, gridManager))
                {
                    pathClear = false;
                    
                    // Wait a moment before rolling back
                    yield return new WaitForSeconds(rollbackDelay);
                    
                    // Roll back to original position by traversing our path in reverse
                    for (int i = visitedCells.Count - 1; i > 0; i--)
                    {
                        Vector2Int fromCoords = visitedCells[i];
                        Vector2Int toCoords = visitedCells[i-1];
                        
                        Vector3 fromRollbackPos = gridManager.GetCell(fromCoords).transform.position;
                        Vector3 toRollbackPos = gridManager.GetCell(toCoords).transform.position;
                        
                        yield return RollBetweenPositions(fromRollbackPos, toRollbackPos, rollDuration);
                    }
                    
                    // Put back in the original cell
                    currentCell.PlaceHexagon(this);
                    SetInteractable(true);
                    isAnimating = false;
                    yield break;
                }
                
                // Path is clear, roll to next cell
                Vector3 curPos = gridManager.GetCell(currentPathCoords).transform.position;
                Vector3 nextPos = gridManager.GetCell(nextPathCoords).transform.position;
                
                yield return RollBetweenPositions(curPos, nextPos, rollDuration);
                
                // Update current position and add to our path
                currentPathCoords = nextPathCoords;
                visitedCells.Add(currentPathCoords);
            }
            
            isAnimating = false;
        }
        
        /// <summary>
        /// Returns the opposite direction
        /// </summary>
        private HexDirection GetOppositeDirection(HexDirection dir)
        {
            switch (dir)
            {
                case HexDirection.East: return HexDirection.West;
                case HexDirection.West: return HexDirection.East;
                case HexDirection.NorthEast: return HexDirection.SouthWest;
                case HexDirection.SouthWest: return HexDirection.NorthEast;
                case HexDirection.NorthWest: return HexDirection.SouthEast;
                case HexDirection.SouthEast: return HexDirection.NorthWest;
                default: return dir;
            }
        }
        
        /// <summary>
        /// Checks if the path at the given coordinates is blocked (cell is occupied)
        /// </summary>
        private bool IsPathBlocked(Vector2Int coords, GridManager gridManager)
        {
            HexCell cell = gridManager.GetCell(coords);
            return cell != null && cell.IsOccupied;
        }
        
        /// <summary>
        /// Small animation for when the hexagon bumps into an obstacle
        /// </summary>
        private IEnumerator BumpAnimation()
        {
            // Get movement direction
            Vector3 moveDir = HexDirectionHelper.GetMovementVector(direction).normalized;
            
            // Small move forward
            Vector3 startPos = transform.position;
            Vector3 bumpPos = startPos + (moveDir * 0.1f);
            
            // Bump sequence
            Sequence bumpSequence = DOTween.Sequence();
            
            // Move slightly forward
            bumpSequence.Append(
                transform.DOMove(bumpPos, 0.1f)
                    .SetEase(Ease.OutQuad)
            );
            
            // Then back
            bumpSequence.Append(
                transform.DOMove(startPos, 0.1f)
                    .SetEase(Ease.OutQuad)
            );
            
            // Wait for completion
            yield return bumpSequence.WaitForCompletion();
        }

        /// <summary>
        /// Rolls the hexagon between two world positions by pivoting around the edge
        /// </summary>
        private IEnumerator RollBetweenPositions(Vector3 fromPos, Vector3 toPos, float duration)
        {
            // Calculate the direction vector from start to end
            Vector3 moveDirection = (toPos - fromPos).normalized;
            
            // Find the pivot point (edge of the hexagon in the direction of movement)
            Vector3 pivotPos = fromPos + (moveDirection * hexRadius);
            pivotPos.y = fromPos.y; // Keep on the same y-plane
            
            // Create pivot object
            GameObject pivotObject = new GameObject("RollPivot");
            pivotObject.transform.position = pivotPos;
            
            // Make the hexagon a child of the pivot
            Transform originalParent = transform.parent;
            Vector3 originalLocalPos = transform.localPosition;
            Quaternion originalLocalRot = transform.localRotation;
            
            transform.SetParent(pivotObject.transform, true);
            
            // Determine rotation axis (perpendicular to movement direction in the horizontal plane)
            Vector3 rotationAxis = Vector3.Cross(Vector3.up, moveDirection).normalized;
            
            // Get initial pivot rotation
            Quaternion startRotation = pivotObject.transform.rotation;
            
            // Create rotation target (typically 60° for pointy-top hex grid)
            Quaternion targetRotation = Quaternion.AngleAxis(120f, rotationAxis) * startRotation;
            
            // Track completion
            bool rollComplete = false;
            
            // Animation sequence
            Sequence rollSequence = DOTween.Sequence();
            
            // Rotate the pivot which results in the hexagon rolling
            rollSequence.Append(
                pivotObject.transform.DORotateQuaternion(targetRotation, duration)
                    .SetEase(Ease.OutQuad)
            );
            
            // After rotation, ensure the hexagon is at the target position
            rollSequence.OnComplete(() => {
                transform.position = toPos + new Vector3(0, transform.position.y - toPos.y, 0);
                transform.SetParent(originalParent);
                
                // Clean up the pivot object
                Destroy(pivotObject);
                rollComplete = true;
            });
            
            // Wait for roll to complete
            while (!rollComplete)
                yield return null;
        }

        /// <summary>
        /// Animate the hex falling off the grid once it has rolled beyond the edge.
        /// </summary>
        private void FallOffGrid(Vector2Int offGridCoords, HexDirection direction, GridManager gridManager)
        {
            // The hexagon's current position is the extra roll position (off-grid)
            Vector3 startPosition = transform.position;
            
            // Calculate a fall target further in the same direction
            Vector3 directionVector = HexDirectionHelper.GetMovementVector(direction);
            Vector3 fallTarget = startPosition + directionVector * 2f;
            
            // Set the fall target's Y to below the grid
            fallTarget.y = -10f;
            
            // Determine the direction from the start to the fall target
            Vector3 moveDir = (fallTarget - startPosition).normalized;
            
            // Choose a tip position between start and fall target
            Vector3 tipPosition = Vector3.Lerp(startPosition, fallTarget, 0.1f);
            tipPosition.y = startPosition.y; // keep the same height for the tip phase
            
            // Calculate rotation axis for tipping over
            Vector3 rotationAxis = Vector3.Cross(Vector3.up, moveDir).normalized;
            
            float fallDuration = 0.5f;
            
            Sequence fallSequence = DOTween.Sequence();
            
            // First phase: small tip to start the fall
            fallSequence.Append(
                transform.DORotate(rotationAxis * 30f, 0.1f, RotateMode.LocalAxisAdd)
                         .SetEase(Ease.OutQuad)
            );
            
            // Second phase: fall with gravity to the calculated target
            fallSequence.Append(
                transform.DOMove(fallTarget, fallDuration)
                         .SetEase(Ease.InQuad)
            );
            
            // Add tumbling during the fall
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
                OnHexagonUnlocked?.Invoke(this);
                Vanish();
            });
        }

        /// <summary>
        /// Fallback simple unlock animation if there's no valid GridManager.
        /// </summary>
        private void SimpleUnlockAnimation(Vector3 directionVector)
        {
            isBeingRemoved = true;
            
            transform.DOMove(transform.position + directionVector * 2f, 0.5f)
                .SetEase(Ease.OutQuad)
                .OnComplete(() => {
                    OnHexagonUnlocked?.Invoke(this);
                    Vanish();
                });
        }

        /// <summary>
        /// Vanish the hex (scale down, then destroy).
        /// </summary>
        private void Vanish()
        {
            // Only vanish if this hexagon is actually being removed
            if (!isBeingRemoved) return;
            
            // Scale down to zero
            transform.DOScale(Vector3.zero, 0.2f)
                .SetEase(Ease.InBack)
                .OnComplete(() => {
                    // Notify the game manager
                    if (GameManager.Instance != null)
                    {
                        GameManager.Instance.OnHexagonRemoved();
                    }
                    
                    // Destroy the gameobject
                    Destroy(gameObject);
                });
        }
    }
}