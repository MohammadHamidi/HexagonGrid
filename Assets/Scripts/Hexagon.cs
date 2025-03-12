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

            UpdateArrowDirection();
        }

        private void UpdateArrowDirection()
        {
            if (arrowTransform != null)
            {
                float rotation = HexDirectionHelper.GetRotationDegrees(direction);
                arrowTransform.localRotation = Quaternion.Euler(90, rotation, 0);
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
            if (isInteractable && !isAnimating && !isBeingRemoved)
            {
                Unlock();
            }
        }

        public void Unlock()
        {
            if (isAnimating || isBeingRemoved) return;
            StartCoroutine(UnlockRoutine());
        }

private IEnumerator UnlockRoutine()
{
    isAnimating = true;
    SetInteractable(false);

    if (GameManager.Instance == null || GameManager.Instance.gridManager == null)
    {
        Debug.LogError("GridManager reference is null in Hexagon.UnlockRoutine()");
        SimpleUnlockAnimation(HexDirectionHelper.GetMovementVector(direction));
        isAnimating = false;
        yield break;
    }

    GridManager gridManager = GameManager.Instance.gridManager;

    // Ensure we have a valid cell reference and POSITION
    if (currentCell == null)
    {
        Debug.LogWarning("Current cell is null in Hexagon.UnlockRoutine(). Attempting to find cell from position.");
        Vector2Int coords = gridManager.WorldToHex(transform.position);
        currentCell = gridManager.GetCell(coords);
        if (currentCell == null)
        {
            Debug.LogError("Failed to find a valid cell for hexagon position. Using fallback animation.");
            SimpleUnlockAnimation(HexDirectionHelper.GetMovementVector(direction));
            isAnimating = false;
            yield break;
        }
    }

    // CRITICAL FIX: Ensure physical position matches logical cell position
    Vector3 cellPosition = currentCell.transform.position + new Vector3(0, 0.2f, 0);
    if (Vector3.Distance(transform.position, cellPosition) > 0.1f)
    {
        Debug.LogWarning($"Hexagon position mismatch detected. Snapping to correct cell position. " +
                         $"Was at {transform.position}, should be at {cellPosition}");
        transform.position = cellPosition;
    }

    // Store the original cell
    HexCell startingCell = currentCell;
    Vector2Int currentCoords = startingCell.Coordinates;
    Vector2Int nextCoords = gridManager.GetNeighborCoordinate(currentCoords, direction);

    // Debug current state
    Debug.Log($"[UnlockRoutine] Hex {name} starting move from cell {currentCoords} to {nextCoords}. " +
              $"Current position: {transform.position}, Cell position: {startingCell.transform.position}");

    // Off-grid case
    if (!gridManager.HasCell(nextCoords))
    {
        isBeingRemoved = true;
        startingCell.ClearHexagon(); // clear the starting cell
        Vector3 startPos = startingCell.transform.position;
        Vector3 offGridPos = gridManager.HexToWorld(nextCoords);
        yield return RollBetweenPositions(startPos, offGridPos, rollDuration);
        // Before falling off, update grid state: clear the last occupied cell
        currentCell?.ClearHexagon();
        FallOffGrid(nextCoords, direction, gridManager);
        isAnimating = false;
        yield break;
    }

    // FIXED: Use the correct coordinates for checking if path is blocked
    if (IsPathBlocked(nextCoords, gridManager))
    {
        Debug.Log($"[UnlockRoutine] Bump triggered - Hex {name} blocked going from {currentCoords} to {nextCoords}. " +
                  $"Current transform pos: {transform.position}");

        // Bump
        yield return BumpAnimation();

        Debug.Log($"[UnlockRoutine] Bump animation finished - Hex {name} at {transform.position}. " +
                  $"Now placing back in cell {startingCell.Coordinates} at {startingCell.transform.position}.");

        // Ensure cell state is consistent
        if (currentCell != startingCell && currentCell != null)
        {
            currentCell.ClearHexagon();
        }

        // Reset current cell to starting cell
        currentCell = startingCell;

        // Snap back to cell center
        startingCell.PlaceHexagon(this);

        Debug.Log($"[UnlockRoutine] After PlaceHexagon - Hex {name} now at {transform.position}.");

        // Make interactable again
        SetInteractable(true);

        isAnimating = false;
        yield break;
    }


    // Retrieve the next cell.
    HexCell nextCell = gridManager.GetCell(nextCoords);
    if (nextCell == null)
    {
        Debug.LogError("Next cell is null even though HasCell returned true.");
        yield return BumpAnimation();
        startingCell.PlaceHexagon(this);
        SetInteractable(true);
        isAnimating = false;
        yield break;
    }

    // Clear the starting cell and roll to the next cell.
    startingCell.ClearHexagon();
    yield return RollBetweenPositions(startingCell.transform.position, nextCell.transform.position, rollDuration);
    // Update currentCell to the new cell.
    currentCell = nextCell;

    // Continue rolling along the path...
    Vector2Int currentPathCoords = nextCoords;
    bool pathClear = true;
    bool reachedEdge = false;
    List<Vector2Int> visitedCells = new List<Vector2Int> { currentCoords, currentPathCoords };

    while (pathClear && !reachedEdge)
    {
        Vector2Int nextPathCoords = gridManager.GetNeighborCoordinate(currentPathCoords, direction);

        if (!gridManager.HasCell(nextPathCoords))
        {
            reachedEdge = true;
            isBeingRemoved = true;

            HexCell currentPathCell = gridManager.GetCell(currentPathCoords);
            if (currentPathCell == null)
            {
                Debug.LogError("Current path cell is null.");
                yield break;
            }
            Vector3 lastCellPos = currentPathCell.transform.position;
            Vector3 offGridPos = gridManager.HexToWorld(nextPathCoords);
            yield return RollBetweenPositions(lastCellPos, offGridPos, rollDuration);
            // Clear the cell where the hexagon last was.
            currentCell?.ClearHexagon();
            FallOffGrid(nextPathCoords, direction, gridManager);
            isAnimating = false;
            yield break;
        }

        // Bump case: next cell is blocked.
        if (IsPathBlocked(nextPathCoords, gridManager))
        {
            Debug.Log($"[UnlockRoutine] Bump triggered - Hex {name} blocked going from {startingCell.Coordinates} to {nextCoords}. " +
                      $"Current transform pos: {transform.position}");

            yield return BumpAnimation(); // Wait for tween completion

            Debug.Log($"[UnlockRoutine] Bump animation finished - Hex {name} at {transform.position}. " +
                      $"Now placing back in cell {startingCell.Coordinates} at {startingCell.transform.position}.");

            // Snap back to cell center
            startingCell.PlaceHexagon(this);

            Debug.Log($"[UnlockRoutine] After PlaceHexagon - Hex {name} now at {transform.position}.");

            // Make interactable again
            SetInteractable(true);
            isAnimating = false;
            yield break;
        }


        // Roll to the next cell.
        HexCell currentCellForRoll = gridManager.GetCell(currentPathCoords);
        HexCell nextCellForRoll = gridManager.GetCell(nextPathCoords);
        if (currentCellForRoll == null || nextCellForRoll == null)
        {
            Debug.LogError("Cell for rolling is null.");
            yield break;
        }
        yield return RollBetweenPositions(currentCellForRoll.transform.position, nextCellForRoll.transform.position, rollDuration);
        // Update currentCell as we progress.
        currentCell = nextCellForRoll;

        currentPathCoords = nextPathCoords;
        visitedCells.Add(currentPathCoords);
    }

    isAnimating = false;
}


        private bool IsPathBlocked(Vector2Int coords, GridManager gridManager)
        {
            HexCell cell = gridManager.GetCell(coords);
            return cell != null && cell.IsOccupied;
        }

        private IEnumerator BumpAnimation()
        {
            Vector3 startPos = transform.position;
            Vector3 moveDir = HexDirectionHelper.GetMovementVector(direction).normalized;
            Vector3 bumpPos = startPos + (moveDir * 0.1f);

            Debug.Log($"[BumpAnimation] START - Hex {name} at {startPos}, bumpPos={bumpPos}");

            // Create a sequence that guarantees the hexagon returns to its exact starting position
            Sequence bumpSequence = DOTween.Sequence();
            bumpSequence.Append(transform.DOMove(bumpPos, 0.1f).SetEase(Ease.OutQuad));
            bumpSequence.Append(transform.DOMove(startPos, 0.1f).SetEase(Ease.OutQuad));

            yield return bumpSequence.WaitForCompletion();
    
            // Force position back to exact starting position to avoid floating point issues
            transform.position = startPos;

            Debug.Log($"[BumpAnimation] DONE - Hex {name} now at {transform.position}");
        }
        private IEnumerator RollBetweenPositions(Vector3 fromPos, Vector3 toPos, float duration)
        {
            Vector3 moveDirection = (toPos - fromPos).normalized;
            Vector3 pivotPos = fromPos + (moveDirection * hexRadius);
            pivotPos.y = fromPos.y;

            GameObject pivotObject = new GameObject("RollPivot");
            pivotObject.transform.position = pivotPos;

            Transform originalParent = transform.parent;
            Vector3 originalLocalPos = transform.localPosition;
            Quaternion originalLocalRot = transform.localRotation;

            transform.SetParent(pivotObject.transform, true);

            Vector3 rotationAxis = Vector3.Cross(Vector3.up, moveDirection).normalized;
            Quaternion startRotation = pivotObject.transform.rotation;
            Quaternion targetRotation = Quaternion.AngleAxis(120f, rotationAxis) * startRotation;

            bool rollComplete = false;
            Sequence rollSequence = DOTween.Sequence();
            rollSequence.Append(pivotObject.transform.DORotateQuaternion(targetRotation, duration).SetEase(Ease.OutQuad));
            rollSequence.OnComplete(() =>
            {
                transform.position = toPos + new Vector3(0, transform.position.y - toPos.y, 0);
                transform.SetParent(originalParent);
                Destroy(pivotObject);
                rollComplete = true;
            });
            while (!rollComplete)
                yield return null;
        }

        private void FallOffGrid(Vector2Int offGridCoords, HexDirection direction, GridManager gridManager)
        {
            // Clear the cell where the hexagon last was.
            currentCell?.ClearHexagon();

            Vector3 startPosition = transform.position;
            Vector3 directionVector = HexDirectionHelper.GetMovementVector(direction);
            Vector3 fallTarget = startPosition + directionVector * 2f;
            fallTarget.y = -10f;
            Vector3 moveDir = (fallTarget - startPosition).normalized;
            Vector3 tipPosition = Vector3.Lerp(startPosition, fallTarget, 0.1f);
            tipPosition.y = startPosition.y;
            Vector3 rotationAxis = Vector3.Cross(Vector3.up, moveDir).normalized;
            float fallDuration = 0.5f;

            Sequence fallSequence = DOTween.Sequence();
            fallSequence.Append(transform.DORotate(rotationAxis * 30f, 0.1f, RotateMode.LocalAxisAdd)
                .SetEase(Ease.OutQuad));
            fallSequence.Append(transform.DOMove(fallTarget, fallDuration)
                .SetEase(Ease.InQuad));
            Vector3 tumbleRotation = new Vector3(
                rotationAxis.x * 360f + UnityEngine.Random.Range(-90f, 90f),
                UnityEngine.Random.Range(-180f, 180f),
                rotationAxis.z * 360f + UnityEngine.Random.Range(-90f, 90f)
            );
            fallSequence.Join(transform.DORotate(tumbleRotation, fallDuration, RotateMode.LocalAxisAdd)
                .SetEase(Ease.InQuad));
            fallSequence.OnComplete(() =>
            {
                OnHexagonUnlocked?.Invoke(this);
                Vanish();
            });
        }


        private void SimpleUnlockAnimation(Vector3 directionVector)
        {
            isBeingRemoved = true;
            transform.DOMove(transform.position + directionVector * 2f, 0.5f)
                .SetEase(Ease.OutQuad)
                .OnComplete(() =>
                {
                    OnHexagonUnlocked?.Invoke(this);
                    Vanish();
                });
        }

        public void Vanish()
        {
            if (!isBeingRemoved) return;
            transform.DOScale(Vector3.zero, 0.2f)
                .SetEase(Ease.InBack)
                .OnComplete(() =>
                {
                    if (GameManager.Instance != null)
                    {
                        GameManager.Instance.OnHexagonRemoved();
                    }
                    Destroy(gameObject);
                });
        }
    }
}
