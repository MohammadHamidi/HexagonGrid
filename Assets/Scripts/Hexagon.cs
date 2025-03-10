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
                mat.color = hexColor;
            }
            
            // Set arrow direction
            UpdateArrowDirection();
        }
        
        /// <summary>
        /// Update the arrowTransform to match the HexDirection.
        /// 
        /// Note: Using (90, rotation, 0) so the arrow lies flat on top of the hex.
        /// Adjust as needed if your model orientation differs.
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
            if (isInteractable)
            {
                Unlock();
            }
        }

        /// <summary>
        /// Public method to unlock this hexagon by rolling it off the grid.
        /// </summary>
        public void Unlock()
        {
            // Start the unlocking process as a coroutine
            StartCoroutine(UnlockRoutine());
        }

        /// <summary>
        /// Coroutine that handles rolling inside the grid, rolling off-grid, then falling.
        /// </summary>
        private IEnumerator UnlockRoutine()
        {
            // Make non-interactable during animation
            SetInteractable(false);

            // Clear from current cell
            if (currentCell != null)
            {
                currentCell.ClearHexagon();
            }

            // Get GridManager reference
            GridManager gridManager = GameManager.Instance != null ? GameManager.Instance.gridManager : null;
            if (gridManager == null)
            {
                Debug.LogError("GridManager reference is null in Hexagon.UnlockRoutine()");
                // Fallback simple animation if no grid
                SimpleUnlockAnimation(HexDirectionHelper.GetMovementVector(direction));
                yield break;
            }

            // Determine current coordinates (in case cell reference was lost)
            Vector2Int currentCoords = currentCell != null 
                ? currentCell.Coordinates 
                : gridManager.WorldToHex(transform.position);

            // Build a path of inside-grid cells from currentCoords to the edge
            List<Vector2Int> pathCoords = new List<Vector2Int>();
            pathCoords.Add(currentCoords);

            // Step along the direction while the next cell is inside the grid
            Vector2Int tempCoords = currentCoords;
            while (gridManager.HasCell(CalculateNextCellCoords(tempCoords, direction)))
            {
                tempCoords = CalculateNextCellCoords(tempCoords, direction);
                pathCoords.Add(tempCoords);
            }

            // If pathCoords has more than 1 entry, we have inside-grid steps
            if (pathCoords.Count > 1)
            {
                // Roll inside the grid cell-by-cell with pivot-based animation
                yield return StartCoroutine(RollPathCoroutine(pathCoords, 0.3f, 120f));
            }

            // After rolling inside the grid, we do one final pivot roll off-grid
            Vector2Int offGridCoords = CalculateNextCellCoords(pathCoords[pathCoords.Count - 1], direction);
            Vector3 offGridPos = gridManager.HexToWorld(offGridCoords) + new Vector3(0, 0.2f, 0);

            bool offRollDone = false;
            PivotRollSingleStep(
                transform.position,   // current position
                offGridPos,           // just beyond the edge
                120f,                 // roll angle
                0.3f,                 // duration
                () => offRollDone = true
            );
            // Wait for that off-grid roll to finish
            while (!offRollDone)
                yield return null;

            // Finally, animate falling off the grid
            FallOffGrid(offGridCoords, direction, gridManager);
        }

        /// <summary>
        /// Rolls (tips) this hex from one cell center (startPos) to an adjacent cell center (endPos)
        /// by rotating around the shared edge. This is a single-step pivot roll.
        /// </summary>
        private void PivotRollSingleStep(
            Vector3 startPos,
            Vector3 endPos,
            float rollAngle,
            float rollDuration,
            System.Action onComplete)
        {
            // 1. Determine the direction from start to end
            Vector3 moveDir = (endPos - startPos);
            Vector3 direction = moveDir.normalized;

            // 2. Position a pivot halfway between the cell centers on the ground plane.
            //    Adjust this radius to match your hex size. For a typical 1.0f diameter, 0.5 is correct.
            float hexRadius = 0.5f;
            Vector3 pivotPos = startPos + direction * hexRadius;
            pivotPos.y = startPos.y; // keep the same Y

            // 3. Create a pivot GameObject at pivotPos
            GameObject pivotObj = new GameObject("HexPivot");
            pivotObj.transform.position = pivotPos;

            // 4. Make this hex a child of pivotObj, so rotating pivotObj rotates the hex about that edge
            transform.SetParent(pivotObj.transform, true);

            // 5. Compute the rotation axis: cross the upward vector with the move direction
            Vector3 rotationAxis = Vector3.Cross(Vector3.up, direction).normalized;

            // 6. Tween the pivot’s rotation using a quaternion target
            Quaternion startRotation = pivotObj.transform.rotation;
            Quaternion targetRotation = Quaternion.AngleAxis(rollAngle, rotationAxis) * startRotation;

            Sequence seq = DOTween.Sequence();
            seq.Append(
                pivotObj.transform.DORotateQuaternion(targetRotation, rollDuration)
                        .SetEase(Ease.OutQuad)
            );

            // 7. After rotating, move pivotObj to the end position so the hex is centered on that cell
            seq.AppendCallback(() =>
            {
                pivotObj.transform.position = endPos;
            });

            // 8. On complete, unparent the hex and destroy the pivot
            seq.OnComplete(() =>
            {
                transform.SetParent(null, true);
                Destroy(pivotObj);
                onComplete?.Invoke();
            });
        }

        /// <summary>
        /// Coroutine to roll along a path of cell coordinates, pivoting step by step.
        /// </summary>
        private IEnumerator RollPathCoroutine(List<Vector2Int> pathCoords, float stepDuration, float rollAngle)
        {
            GridManager gm = GameManager.Instance.gridManager;
            for (int i = 0; i < pathCoords.Count - 1; i++)
            {
                // Start and end cell positions
                Vector3 startPos = gm.GetCell(pathCoords[i]).transform.position;
                Vector3 endPos   = gm.GetCell(pathCoords[i + 1]).transform.position;

                bool stepDone = false;
                PivotRollSingleStep(startPos, endPos, rollAngle, stepDuration, () => stepDone = true);

                // Wait until the single step finishes
                while (!stepDone)
                    yield return null;
            }
        }

        // ------------------------------------------------------
        // Existing code below is left mostly unchanged
        // ------------------------------------------------------

        // Added coroutine to delay the vanish
        private IEnumerator DelayedVanish()
        {
            // Wait a moment before vanishing
            yield return new WaitForSeconds(0.1f);
            Debug.Log("Starting vanish animation");
            // Now vanish
            Vanish();
        }

        /// <summary>
        /// Animate the hex falling off the grid once it has rolled beyond the edge.
        /// </summary>
        private void FallOffGrid(Vector2Int offGridCoords, HexDirection direction, GridManager gridManager)
        {
            // The hexagon's current position is the extra roll position (off-grid)
            Vector3 startPosition = transform.position;
            
            // Calculate the next off–grid coordinate where the hexagon should fall to
            Vector2Int fallCoords = CalculateNextCellCoords(offGridCoords, direction);
            // Convert that coordinate to world position and add the same Y offset as used in placement
            Vector3 fallTarget = gridManager.HexToWorld(fallCoords) + new Vector3(0, 0f, 0);
            // Set the fall target's Y to below the grid
            fallTarget.y = -10f;
            
            // Determine the direction from the start to the fall target
            Vector3 moveDir = (fallTarget - startPosition).normalized;
            
            // Choose a tip position between start and fall target (e.g., 30% of the distance)
            Vector3 tipPosition = Vector3.Lerp(startPosition, fallTarget, 0.1f);
            tipPosition.y = startPosition.y; // keep the same height for the tip phase
            
            // Calculate rotation axis for tipping over
            Vector3 rotationAxis = Vector3.Cross(Vector3.up, moveDir).normalized;
            
            float tipDuration = 0f;
            float fallDuration = 0.2f;
            
            Sequence fallSequence = DOTween.Sequence();
            
            // First phase: tip over the edge
            fallSequence.Append(
                transform.DOMove(tipPosition, tipDuration)
                         .SetEase(Ease.OutQuad)
            );
            
            fallSequence.Join(
                transform.DORotate(rotationAxis * 45f, tipDuration, RotateMode.LocalAxisAdd)
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
            transform.DOMove(transform.position + directionVector * 2f, 0.5f)
                .SetEase(Ease.OutQuad)
                .OnComplete(() => {
                    OnHexagonUnlocked?.Invoke(this);
                    Vanish();
                });
        }

        /// <summary>
        /// Compute the next axial coordinates in the given direction.
        /// </summary>
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

        /// <summary>
        /// Vanish the hex (scale down, then destroy).
        /// </summary>
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
