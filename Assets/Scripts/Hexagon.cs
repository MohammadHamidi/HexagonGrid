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
        [SerializeField] private float hexRadius = 0.5f;
        [SerializeField] private float rollbackDelay = 0.2f;

        private HexCell currentCell;
        private HexDirection direction;
        private Color hexColor;
        private bool isInteractable = true;
        private bool isAnimating = false;
        private bool isBeingRemoved = false;
        private float stackHeight = 0.2f; // Default height for single hexagons

        // Cached tween variable for all animations.
        private Tween _currentTween;

        // NEW: Field to track visited cell coordinates during movement.
        private List<Vector2Int> visitedCellCoordinates = new List<Vector2Int>();

        public HexCell CurrentCell => currentCell;
        public HexDirection Direction => direction;
        public Color HexColor => hexColor;

        public event Action<Hexagon> OnHexagonUnlocked;

        private void OnValidate()
        {
            if (hexRenderer == null)
                hexRenderer = GetComponentInChildren<MeshRenderer>();
        }

        public void SetStackHeight(float height)
        {
            stackHeight = height;
            Debug.Log($"[SetStackHeight] Hex {name} stack height set to {height}");
        }

        public void Initialize(Color color, HexDirection dir)
        {
            hexColor = color;
            direction = dir;
            Debug.Log($"[Initialize] Hex {name} initialized with color {color} and direction {dir}");

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
                Debug.Log(
                    $"[UpdateArrowDirection] Hex {name} arrow rotated to {rotation} degrees for direction {direction}");
            }
        }

        public void SetCell(HexCell cell)
        {
            Debug.Log($"[SetCell] Hex {name} set to cell {(cell != null ? cell.Coordinates.ToString() : "null")}");
            currentCell = cell;
        }

        public void SetDirection(HexDirection newDirection)
        {
            Debug.Log($"[SetDirection] Hex {name} direction changed from {direction} to {newDirection}");
            direction = newDirection;
            UpdateArrowDirection();
        }

        public void SetInteractable(bool interactable)
        {
            Debug.Log($"[SetInteractable] Hex {name} interactable changed from {isInteractable} to {interactable}");
            isInteractable = interactable;
        }

        private void OnMouseDown()
        {
            Debug.Log(
                $"[OnMouseDown] Hex {name} clicked. isInteractable={isInteractable}, isAnimating={isAnimating}, isBeingRemoved={isBeingRemoved}");
            if (isInteractable && !isAnimating && !isBeingRemoved)
            {
                Unlock();
            }
            else
            {
                Debug.Log(
                    $"[OnMouseDown] Ignoring click on Hex {name} due to state: isInteractable={isInteractable}, isAnimating={isAnimating}, isBeingRemoved={isBeingRemoved}");
            }
        }

        public void Unlock()
        {
            Debug.Log(
                $"[Unlock] Attempting to unlock Hex {name}. isAnimating={isAnimating}, isBeingRemoved={isBeingRemoved}");
            if (isAnimating || isBeingRemoved)
            {
                Debug.Log(
                    $"[Unlock] Hex {name} cannot be unlocked due to current state: isAnimating={isAnimating}, isBeingRemoved={isBeingRemoved}");
                return;
            }

            StartCoroutine(UnlockRoutine());
        }

        private IEnumerator UnlockRoutine()
        {
            Debug.Log(
                $"[UnlockRoutine] START - Hex {name} starting unlock routine. isAnimating={isAnimating}, isBeingRemoved={isBeingRemoved}");
            if (isAnimating || isBeingRemoved)
            {
                Debug.Log(
                    $"[UnlockRoutine] ABORT - Hex {name} already in animation state: isAnimating={isAnimating}, isBeingRemoved={isBeingRemoved}");
                yield break; // Prevent multiple concurrent animations
            }

            isAnimating = true;
            SetInteractable(false);
            Debug.Log($"[UnlockRoutine] Hex {name} animation state set: isAnimating=true, isInteractable=false");

            // STANDARDIZE Y VALUES - This is critical for consistency
            float baseY = 0.01f; // Cell base Y position
            float hexYOffset = 0.2f; // Hexagon Y offset from cell

            // ---------- SETUP AND VALIDATION ----------
            if (GameManager.Instance == null || GameManager.Instance.gridManager == null)
            {
                Debug.LogError(
                    $"[UnlockRoutine] Hex {name} - GridManager reference is null in Hexagon.UnlockRoutine()");
                SimpleUnlockAnimation(HexDirectionHelper.GetMovementVector(direction));
                isAnimating = false;
                yield break;
            }

            GridManager gridManager = GameManager.Instance.gridManager;
            Debug.Log($"[UnlockRoutine] Hex {name} got valid gridManager reference");

            // Ensure we have a valid cell reference
            if (currentCell == null)
            {
                Debug.LogWarning(
                    $"[UnlockRoutine] Hex {name} - Current cell is null. Attempting to find cell from position.");
                Vector2Int coords = gridManager.WorldToHex(transform.position);
                Debug.Log(
                    $"[UnlockRoutine] Hex {name} - Looking up cell at converted coords {coords} from position {transform.position}");
                currentCell = gridManager.GetCell(coords);
                if (currentCell == null)
                {
                    Debug.LogError(
                        $"[UnlockRoutine] Hex {name} - Failed to find a valid cell for hexagon position. Using fallback animation.");
                    SimpleUnlockAnimation(HexDirectionHelper.GetMovementVector(direction));
                    isAnimating = false;
                    yield break;
                }

                Debug.Log($"[UnlockRoutine] Hex {name} - Found cell at {coords}");
            }

            // CRITICAL FIX: Enforce standard Y value for consistency
            Vector3 standardizedCellPosition = new Vector3(
                currentCell.transform.position.x,
                baseY,
                currentCell.transform.position.z
            );
            Debug.Log($"[UnlockRoutine] Hex {name} - Standardized cell position: {standardizedCellPosition}");

            // Standardize the hexagon position
            Vector3 standardizedHexPosition = new Vector3(
                standardizedCellPosition.x,
                baseY + hexYOffset,
                standardizedCellPosition.z
            );
            Debug.Log($"[UnlockRoutine] Hex {name} - Standardized hex position: {standardizedHexPosition}");

            // Check for position mismatch using only X and Z (ignoring Y)
            Vector2 hexXZ = new Vector2(transform.position.x, transform.position.z);
            Vector2 cellXZ = new Vector2(standardizedCellPosition.x, standardizedCellPosition.z);
            float positionDifference = Vector2.Distance(hexXZ, cellXZ);
            Debug.Log(
                $"[UnlockRoutine] Hex {name} - Position check: hex XZ={hexXZ}, cell XZ={cellXZ}, difference={positionDifference}");

            if (positionDifference > 0.1f)
            {
                Debug.LogWarning(
                    $"[UnlockRoutine] Hex {name} - Position mismatch detected. Snapping to correct cell position. Was at {transform.position}, should be at {standardizedHexPosition}");
                transform.position = standardizedHexPosition;
            }
            else if (Mathf.Abs(transform.position.y - standardizedHexPosition.y) > 0.01f)
            {
                // Fix Y position even if X/Z are correct
                Debug.LogWarning(
                    $"[UnlockRoutine] Hex {name} - Y position mismatch. Fixing Y from {transform.position.y} to {standardizedHexPosition.y}");
                transform.position = new Vector3(transform.position.x, standardizedHexPosition.y, transform.position.z);
            }
            else
            {
                Debug.Log($"[UnlockRoutine] Hex {name} - Position is correct, no adjustment needed");
            }

            // ---------- VISITED CELLS TRACKING ----------
            // Clear visited cells list at start and add the starting cell coordinates.
            visitedCellCoordinates.Clear();
            HexCell startingCell = currentCell;
            Vector2Int currentCoords = startingCell.Coordinates;
            visitedCellCoordinates.Add(currentCoords);

            // Determine the next cell based on the current direction.
            Vector2Int nextCoords = gridManager.GetNeighborCoordinate(currentCoords, direction);
            Debug.Log(
                $"[UnlockRoutine] Hex {name} - Starting cell={currentCoords}, next cell={nextCoords}, direction={direction}");

            // ---------- OFF-GRID CASE ----------
            bool hasNextCell = gridManager.HasCell(nextCoords);
            Debug.Log($"[UnlockRoutine] Hex {name} - HasCell check for {nextCoords}: {hasNextCell}");
            if (!hasNextCell)
            {
                Debug.Log($"[UnlockRoutine] Hex {name} - Detected off-grid movement to {nextCoords}");
                isBeingRemoved = true;
                startingCell.ClearHexagon(); // clear the starting cell

                Vector3 startPos = standardizedCellPosition;
                Vector3 offGridPos = gridManager.HexToWorld(nextCoords);
                offGridPos.y = baseY; // Standardize Y
                Debug.Log($"[UnlockRoutine] Hex {name} - Rolling off grid from {startPos} to {offGridPos}");

                yield return RollBetweenPositions(startPos, offGridPos, rollDuration);

                // Before falling off, update grid state: clear the last occupied cell
                Debug.Log($"[UnlockRoutine] Hex {name} - Clearing current cell before falling off grid");
                currentCell?.ClearHexagon();
                Debug.Log($"[UnlockRoutine] Hex {name} - Executing FallOffGrid animation");
                FallOffGrid(nextCoords, direction, gridManager);
                isAnimating = false;
                Debug.Log($"[UnlockRoutine] Hex {name} - Off-grid case completed, animation state cleared");
                yield break;
            }

            // ---------- BLOCKED PATH CASE ----------
            bool isBlocked = IsPathBlocked(nextCoords, gridManager);
            Debug.Log($"[UnlockRoutine] Hex {name} - Path blocked check for {nextCoords}: {isBlocked}");
            if (isBlocked)
            {
                Debug.Log(
                    $"[UnlockRoutine] Bump triggered - Hex {name} blocked going from {currentCoords} to {nextCoords}.");

                // Dump hexagon orientation and state before bump
                Debug.Log(
                    $"[UnlockRoutine] PRE-BUMP STATE - Hex {name}: position={transform.position}, rotation={transform.rotation.eulerAngles}, direction={direction}, arrow rotation={(arrowTransform != null ? arrowTransform.localRotation.eulerAngles.ToString() : "null")}");

                // Bump animation
                Debug.Log($"[UnlockRoutine] Hex {name} - Starting bump animation");
                yield return BumpAnimation();

                Debug.Log($"[UnlockRoutine] Bump animation finished - Hex {name}. Returning to standard position.");

                // Dump hexagon orientation and state after bump
                Debug.Log(
                    $"[UnlockRoutine] POST-BUMP STATE - Hex {name}: position={transform.position}, rotation={transform.rotation.eulerAngles}, direction={direction}, arrow rotation={(arrowTransform != null ? arrowTransform.localRotation.eulerAngles.ToString() : "null")}");

                // Ensure cell state is consistent
                if (currentCell != startingCell && currentCell != null)
                {
                    Debug.Log(
                        $"[UnlockRoutine] Hex {name} - Clearing inconsistent cell reference {currentCell.Coordinates}");
                    currentCell.ClearHexagon();
                }

                // Reset current cell to starting cell
                Debug.Log(
                    $"[UnlockRoutine] Hex {name} - Resetting cell reference to starting cell {startingCell.Coordinates}");
                currentCell = startingCell;

                // Snap back to cell center with STANDARD Y
                Debug.Log(
                    $"[UnlockRoutine] Hex {name} - Placing hexagon back on starting cell {startingCell.Coordinates}");
                // startingCell.PlaceHexagon(this);  // This remains commented out

                // Ensure Y is consistent
                Debug.Log(
                    $"[UnlockRoutine] Hex {name} - Forcing position to standardized position {standardizedHexPosition}");
                transform.position = standardizedHexPosition;

                // Make interactable again
                Debug.Log($"[UnlockRoutine] Hex {name} - Re-enabling interaction and clearing animation state");
                SetInteractable(true);
                isAnimating = false;
                yield break;
            }

            // ---------- SUCCESSFUL MOVEMENT CASE ----------
            // Retrieve the next cell.
            HexCell nextCell = gridManager.GetCell(nextCoords);
            Debug.Log(
                $"[UnlockRoutine] Hex {name} - Retrieved next cell: {(nextCell != null ? nextCoords.ToString() : "null")}");
            if (nextCell == null)
            {
                Debug.LogError(
                    $"[UnlockRoutine] Hex {name} - Next cell is null even though HasCell returned true for {nextCoords}");
                yield return BumpAnimation();
                startingCell.PlaceHexagon(this);
                transform.position = standardizedHexPosition;
                SetInteractable(true);
                isAnimating = false;
                yield break;
            }

            // Clear the starting cell and roll to the next cell.
            Debug.Log($"[UnlockRoutine] Hex {name} - Clearing starting cell {startingCell.Coordinates} before roll");
            startingCell.ClearHexagon();

            // Use standardized positions for the roll
            Vector3 startStandardPos = standardizedCellPosition;
            Vector3 nextStandardPos = new Vector3(
                nextCell.transform.position.x,
                baseY,
                nextCell.transform.position.z
            );
            Debug.Log($"[UnlockRoutine] Hex {name} - Rolling from {startStandardPos} to {nextStandardPos}");

            yield return RollBetweenPositions(startStandardPos, nextStandardPos, rollDuration);

            // Update currentCell to the new cell.
            currentCell = nextCell;
            // Track the new cell in visited cells.
            visitedCellCoordinates.Add(nextCoords);
            Debug.Log($"[UnlockRoutine] Hex {name} - Updated current cell to {nextCell.Coordinates}");

            // Continue rolling along the path with consistent positions...
            Vector2Int currentPathCoords = nextCoords;
            bool pathClear = true;
            bool reachedEdge = false;
            Debug.Log($"[UnlockRoutine] Hex {name} - Starting path following from {currentPathCoords}");

            while (pathClear && !reachedEdge)
            {
                Vector2Int nextPathCoords = gridManager.GetNeighborCoordinate(currentPathCoords, direction);
                Debug.Log(
                    $"[UnlockRoutine] Hex {name} - Checking next path coord {nextPathCoords} from current {currentPathCoords}");

                hasNextCell = gridManager.HasCell(nextPathCoords);
                Debug.Log(
                    $"[UnlockRoutine] Hex {name} - HasCell check for next path coord {nextPathCoords}: {hasNextCell}");
                if (!hasNextCell)
                {
                    Debug.Log(
                        $"[UnlockRoutine] Hex {name} - Reached edge at {currentPathCoords}, next would be {nextPathCoords}");
                    reachedEdge = true;
                    isBeingRemoved = true;

                    HexCell currentPathCell = gridManager.GetCell(currentPathCoords);
                    if (currentPathCell == null)
                    {
                        Debug.LogError(
                            $"[UnlockRoutine] Hex {name} - Current path cell is null for {currentPathCoords}");
                        isAnimating = false;
                        yield break;
                    }

                    // Use standardized positions
                    Vector3 lastCellStandardPos = new Vector3(
                        currentPathCell.transform.position.x,
                        baseY,
                        currentPathCell.transform.position.z
                    );

                    Vector3 offGridPos = gridManager.HexToWorld(nextPathCoords);
                    offGridPos.y = baseY; // Standardize Y
                    Debug.Log(
                        $"[UnlockRoutine] Hex {name} - Rolling off edge from {lastCellStandardPos} to {offGridPos}");

                    yield return RollBetweenPositions(lastCellStandardPos, offGridPos, rollDuration);

                    // Clear the cell where the hexagon last was.
                    Debug.Log(
                        $"[UnlockRoutine] Hex {name} - Clearing current cell {(currentCell != null ? currentCell.Coordinates.ToString() : "null")} before falling off grid");
                    currentCell?.ClearHexagon();
                    FallOffGrid(nextPathCoords, direction, gridManager);
                    isAnimating = false;
                    Debug.Log($"[UnlockRoutine] Hex {name} - Falling off grid completed");
                    yield break;
                }

                // Bump case: next cell is blocked.
                isBlocked = IsPathBlocked(nextPathCoords, gridManager);
                Debug.Log(
                    $"[UnlockRoutine] Hex {name} - Path blocked check for next path coord {nextPathCoords}: {isBlocked}");
                if (isBlocked)
                {
                    Debug.Log($"[UnlockRoutine] Bump in path - Hex {name} blocked at {currentPathCoords}.");

                    // Dump hexagon orientation and state before bump
                    Debug.Log(
                        $"[UnlockRoutine] PRE-PATH-BUMP STATE - Hex {name}: position={transform.position}, rotation={transform.rotation.eulerAngles}, direction={direction}, arrow rotation={(arrowTransform != null ? arrowTransform.localRotation.eulerAngles.ToString() : "null")}");

                    // Bump from current position
                    Debug.Log($"[UnlockRoutine] Hex {name} - Starting in-path bump animation");
                    yield return BumpAnimation();

                    // Dump hexagon orientation and state after bump
                    Debug.Log(
                        $"[UnlockRoutine] POST-PATH-BUMP STATE - Hex {name}: position={transform.position}, rotation={transform.rotation.eulerAngles}, direction={direction}, arrow rotation={(arrowTransform != null ? arrowTransform.localRotation.eulerAngles.ToString() : "null")}");

                    // THE BUG FIX: REMOVE THE POSITION FORCING AFTER BUMP ANIMATION
                    // BumpAnimation already handled returning the hexagon to the original cell
                    // Do not override the position here

                    // Make interactable again
                    Debug.Log($"[UnlockRoutine] Hex {name} - Re-enabling interaction after in-path bump");
                    SetInteractable(true);
                    pathClear = false;
                    yield break;
                }
                else
                {
                    // Roll to the next cell with STANDARD POSITIONS
                    HexCell currentCellForRoll = gridManager.GetCell(currentPathCoords);
                    HexCell nextCellForRoll = gridManager.GetCell(nextPathCoords);
                    if (currentCellForRoll == null || nextCellForRoll == null)
                    {
                        Debug.LogError(
                            $"[UnlockRoutine] Hex {name} - Cell for rolling is null. Current: {(currentCellForRoll != null ? currentPathCoords.ToString() : "null")}, Next: {(nextCellForRoll != null ? nextPathCoords.ToString() : "null")}");
                        isAnimating = false;
                        yield break;
                    }

                    // Use standardized positions
                    Vector3 fromStandardPos = new Vector3(
                        currentCellForRoll.transform.position.x,
                        baseY,
                        currentCellForRoll.transform.position.z
                    );

                    Vector3 toStandardPos = new Vector3(
                        nextCellForRoll.transform.position.x,
                        baseY,
                        nextCellForRoll.transform.position.z
                    );
                    Debug.Log(
                        $"[UnlockRoutine] Hex {name} - Rolling in path from {fromStandardPos} to {toStandardPos}");

                    yield return RollBetweenPositions(fromStandardPos, toStandardPos, rollDuration);

                    // Update currentCell as we progress.
                    currentCell = nextCellForRoll;
                    currentPathCoords = nextPathCoords;
                    // Track visited path coordinates.
                    visitedCellCoordinates.Add(nextPathCoords);
                    Debug.Log(
                        $"[UnlockRoutine] Hex {name} - Updated current cell to {currentCell.Coordinates} and path coords to {currentPathCoords}");
                }
            }

            // Always make sure we end the animation state
            isAnimating = false;
            Debug.Log($"[UnlockRoutine] Hex {name} - Unlock routine completed, animation state cleared");
        }

        private bool IsPathBlocked(Vector2Int coords, GridManager gridManager)
        {
            HexCell cell = gridManager.GetCell(coords);
            bool isBlocked = cell != null && cell.IsOccupied;
            Debug.Log(
                $"[IsPathBlocked] Hex {name} - Checking if {coords} is blocked: cell={(cell != null ? "exists" : "null")}, isOccupied={(cell != null ? cell.IsOccupied.ToString() : "N/A")}, result={isBlocked}");
            if (isBlocked && cell != null && cell.OccupyingHexagon != null)
            {
                Debug.Log(
                    $"[IsPathBlocked] Hex {name} - Cell {coords} is occupied by hexagon {cell.OccupyingHexagon.name}");
            }

            return isBlocked;
        }


        private IEnumerator BumpAnimation()
        {
            // ---- PREPARATION ----
            // Cancel any existing tween
            if (_currentTween != null)
            {
                _currentTween.Kill();
                _currentTween = null;
            }

            // Mark as animating to prevent other interactions
            isAnimating = true;
            SetInteractable(false);

            // Standard heights
            float baseY = 0.01f;
            // We'll use stackHeight instead of a fixed hexYOffset

            // Store current position
            Vector3 currentPosition = transform.position;
            Vector3 startCellPosition = Vector3.zero;
            HexCell originalCell = null;

            // NEW: Use visited cells to determine the original cell position
            if (visitedCellCoordinates.Count > 0)
            {
                Vector2Int originalCellCoords = visitedCellCoordinates[0];
                Debug.Log(
                    $"[BumpAnimation] Hex {name} - Will return to original cell at coordinates {originalCellCoords}, using stackHeight={stackHeight}");

                if (GameManager.Instance != null && GameManager.Instance.gridManager != null)
                {
                    originalCell = GameManager.Instance.gridManager.GetCell(originalCellCoords);
                }

                if (originalCell != null)
                {
                    startCellPosition = new Vector3(
                        originalCell.transform.position.x,
                        baseY + stackHeight, // Use stored stack height instead of fixed value
                        originalCell.transform.position.z
                    );
                    currentCell = originalCell;
                    Debug.Log(
                        $"[BumpAnimation] Hex {name} - Reset currentCell to original cell at {originalCellCoords}, using stackHeight={stackHeight}");
                }
                else
                {
                    Debug.LogWarning(
                        $"[BumpAnimation] Hex {name} - Could not find original cell at {originalCellCoords}, using current cell");
                    if (currentCell != null)
                    {
                        startCellPosition = new Vector3(
                            currentCell.transform.position.x,
                            baseY + stackHeight, // Use stored stack height
                            currentCell.transform.position.z
                        );
                    }
                    else
                    {
                        startCellPosition =
                            new Vector3(currentPosition.x, baseY + stackHeight,
                                currentPosition.z); // Use stored stack height
                    }
                }
            }
            else
            {
                Debug.LogWarning($"[BumpAnimation] Hex {name} - No visited cells recorded, using current cell");
                if (currentCell != null)
                {
                    startCellPosition = new Vector3(
                        currentCell.transform.position.x,
                        baseY + stackHeight, // Use stored stack height
                        currentCell.transform.position.z
                    );
                }
                else
                {
                    startCellPosition =
                        new Vector3(currentPosition.x, baseY + stackHeight,
                            currentPosition.z); // Use stored stack height
                }
            }

            // Reset rotation to identity 
            Quaternion identityRotation = Quaternion.identity;
            Debug.Log(
                $"[BumpAnimation] Starting bump for hex {name} at position {currentPosition}, will return to {startCellPosition}, stackHeight={stackHeight}");

            // FIRST step - force identity rotation before starting the bump 
            transform.rotation = identityRotation;

            // Reset arrow direction to match the hexagon's direction
            if (arrowTransform != null)
            {
                float rotation = HexDirectionHelper.GetRotationDegrees(direction);
                arrowTransform.localRotation = Quaternion.Euler(90, rotation, 0);
                Debug.Log($"[BumpAnimation] Reset arrow rotation to {rotation} degrees");
            }

            // Get direction vector based on hexagon direction
            Vector3 moveDirection = HexDirectionHelper.GetMovementVector(direction).normalized;

            // Create pivot for bump animation (similar to roll animation)
            Vector3 pivotPos = currentPosition + (moveDirection * hexRadius);
            pivotPos.y = baseY;
            GameObject pivotObject = new GameObject("BumpPivot");
            pivotObject.transform.position = pivotPos;

            // Calculate rotation axis
            Vector3 rotationAxis = Vector3.Cross(Vector3.up, moveDirection).normalized;

            // Store original parent and make hexagon a child of pivot
            Transform originalParent = transform.parent;
            transform.SetParent(pivotObject.transform, true);

            // Store pivot's starting rotation
            Quaternion startRotation = pivotObject.transform.rotation;

            // Use a small rotation angle for bump
            float bumpAngle = 15f;
            Quaternion bumpRotation = Quaternion.AngleAxis(bumpAngle, rotationAxis) * startRotation;

            // ---- FORWARD BUMP ANIMATION ----
            bool forwardComplete = false;
            Sequence bumpSequence = DOTween.Sequence();
            _currentTween = bumpSequence;
            bumpSequence.Append(pivotObject.transform.DORotateQuaternion(bumpRotation, 0.1f)
                .SetEase(Ease.Linear));
            bumpSequence.OnComplete(() =>
            {
                forwardComplete = true;
                _currentTween = null;
            });
            while (!forwardComplete)
                yield return null;

            // Small delay before return
            yield return new WaitForSeconds(0.1f);

            // ---- RETURN ROTATION ANIMATION ----
            bool returnRotationComplete = false;
            Sequence returnRotationSequence = DOTween.Sequence();
            _currentTween = returnRotationSequence;
            returnRotationSequence.Append(pivotObject.transform.DORotateQuaternion(startRotation, 0.15f)
                .SetEase(Ease.Linear));
            returnRotationSequence.OnComplete(() =>
            {
                returnRotationComplete = true;
                _currentTween = null;
            });
            while (!returnRotationComplete)
                yield return null;

            // Detach from pivot and reset parent
            transform.SetParent(originalParent);
            transform.rotation = identityRotation;
            if (pivotObject != null)
            {
                Destroy(pivotObject);
            }

            // Get the grid manager for cell lookup
            GridManager gridManager = GameManager.Instance?.gridManager;
            if (gridManager == null)
            {
                Debug.LogError($"[BumpAnimation] Hex {name} - Cannot roll back, GridManager is null");
                transform.position = startCellPosition; // Use position with stack height
                SetInteractable(true);
                isAnimating = false;
                visitedCellCoordinates.Clear();
                yield break;
            }

            // ---- ROLL BACK THROUGH CELLS ----
            // We need to actually roll through cells not just move directly
            if (visitedCellCoordinates.Count > 1)
            {
                Debug.Log(
                    $"[BumpAnimation] Hex {name} - Rolling back through {visitedCellCoordinates.Count} visited cells");

                // Iterate through visited cells in reverse
                for (int i = visitedCellCoordinates.Count - 1; i >= 1; i--)
                {
                    Vector2Int fromCoords = visitedCellCoordinates[i];
                    Vector2Int toCoords = visitedCellCoordinates[i - 1];

                    Debug.Log($"[BumpAnimation] Hex {name} - Rolling from cell {fromCoords} to cell {toCoords}");

                    HexCell fromCell = gridManager.GetCell(fromCoords);
                    HexCell toCell = gridManager.GetCell(toCoords);

                    if (fromCell != null && toCell != null)
                    {
                        Vector3 fromPos = new Vector3(fromCell.transform.position.x, baseY,
                            fromCell.transform.position.z);
                        Vector3 toPos = new Vector3(toCell.transform.position.x, baseY, toCell.transform.position.z);

                        Debug.Log(
                            $"[BumpAnimation] Hex {name} - Rolling from {fromPos} to {toPos}, with stackHeight={stackHeight}");

                        // Use the actual rolling animation to go back to previous cell
                        yield return RollBetweenPositions(fromPos, toPos, rollDuration);

                        // Clear the from cell since we're rolling away from it
                        fromCell.ClearHexagon();

                        // Update current cell reference
                        currentCell = toCell;
                    }
                    else
                    {
                        Debug.LogWarning(
                            $"[BumpAnimation] Hex {name} - Couldn't find cells for coordinates. FromCell: {(fromCell != null)}, ToCell: {(toCell != null)}");
                    }
                }
            }
            else
            {
                Debug.Log($"[BumpAnimation] Hex {name} - No intermediate cells to roll through, doing direct move");
                transform.position = startCellPosition; // Position with stack height
            }

            // Final enforcement of position and rotation with the proper stack height
            transform.position = startCellPosition; // Already includes stack height
            transform.rotation = identityRotation;
            if (arrowTransform != null)
            {
                float rotation = HexDirectionHelper.GetRotationDegrees(direction);
                arrowTransform.localRotation = Quaternion.Euler(90, rotation, 0);
            }

            // Clear intermediate cells that might have been visited
            for (int i = 1; i < visitedCellCoordinates.Count; i++)
            {
                Vector2Int coords = visitedCellCoordinates[i];
                if (GameManager.Instance != null && GameManager.Instance.gridManager != null)
                {
                    HexCell cell = GameManager.Instance.gridManager.GetCell(coords);
                    if (cell != null && cell.OccupyingHexagon == this)
                    {
                        Debug.Log($"[BumpAnimation] Hex {name} - Clearing intermediate cell at {coords}");
                        cell.ClearHexagon();
                    }
                }
            }

            // Ensure hexagon is placed on the original cell
            if (originalCell != null)
            {
                Debug.Log(
                    $"[BumpAnimation] Hex {name} - Placing hexagon back on original cell at {originalCell.Coordinates}");
                originalCell.PlaceHexagon(this);
            }

            SetInteractable(true);
            isAnimating = false;

            // Clear visited cells list after completion
            visitedCellCoordinates.Clear();

            Debug.Log(
                $"[BumpAnimation] Completed bump for hex {name}, reset to start cell position {startCellPosition} with stackHeight={stackHeight}");
        }

        private IEnumerator RollBetweenPositions(Vector3 fromPos, Vector3 toPos, float duration)
        {
            Debug.Log(
                $"[RollBetweenPositions] START - Hex {name} starting roll from {fromPos} to {toPos}, duration={duration}, stackHeight={stackHeight}");

            // Cancel any existing tween if present
            if (_currentTween != null)
            {
                Debug.Log($"[RollBetweenPositions] Hex {name} - Cancelling existing tween");
                _currentTween.Kill();
                _currentTween = null;
            }

            // IMPORTANT: Standardize the Y coordinates but keep track of stack height
            fromPos = new Vector3(fromPos.x, 0.01f, fromPos.z);
            toPos = new Vector3(toPos.x, 0.01f, toPos.z);
            Debug.Log($"[RollBetweenPositions] Hex {name} - Standardized positions: from={fromPos}, to={toPos}");

            // Log the exact positions we're working with to help debug
            Debug.Log($"[RollBetweenPositions] EXACT - Hex {name} rolling from {fromPos} to {toPos}");

            // Ensure we're starting at the right position with stack height
            Vector3 startingPos = new Vector3(fromPos.x, fromPos.y + stackHeight, fromPos.z);
            Debug.Log(
                $"[RollBetweenPositions] Hex {name} - Setting initial position to {startingPos} with stackHeight={stackHeight}, was at {transform.position}");
            transform.position = startingPos;

            // Capture initial state
            Debug.Log(
                $"[RollBetweenPositions] Hex {name} - Initial state: position={transform.position}, rotation={transform.rotation.eulerAngles}, direction={direction}, arrow rotation={(arrowTransform != null ? arrowTransform.localRotation.eulerAngles.ToString() : "null")}");

            // Calculate the precise direction vector
            Vector3 moveDirection = (toPos - fromPos).normalized;
            Debug.Log($"[RollBetweenPositions] Hex {name} - Move direction: {moveDirection}");

            // Create pivot at exact position with precise offset for rolling
            Vector3 pivotPos = fromPos + (moveDirection * hexRadius);
            pivotPos.y = fromPos.y; // Ensure consistent y-value
            Debug.Log($"[RollBetweenPositions] Hex {name} - Creating pivot at {pivotPos}");

            GameObject pivotObject = new GameObject("RollPivot");
            pivotObject.transform.position = pivotPos;

            // Parent to pivot while preserving exact position
            Vector3 localPosBefore = transform.localPosition;
            Debug.Log($"[RollBetweenPositions] Hex {name} - Local position before parenting: {localPosBefore}");
            transform.SetParent(pivotObject.transform, true);
            Debug.Log($"[RollBetweenPositions] Hex {name} - Local position after parenting: {transform.localPosition}");

            // Calculate precise rotation axis perpendicular to movement direction
            Vector3 rotationAxis = Vector3.Cross(Vector3.up, moveDirection).normalized;
            Debug.Log($"[RollBetweenPositions] Hex {name} - Rotation axis: {rotationAxis}");

            // Store the pivot's initial rotation
            Quaternion startRotation = pivotObject.transform.rotation;
            Debug.Log($"[RollBetweenPositions] Hex {name} - Pivot start rotation: {startRotation.eulerAngles}");

            // Target is exactly 120 degrees (for a hexagon to roll one face)
            Quaternion targetRotation = Quaternion.AngleAxis(120f, rotationAxis) * startRotation;
            Debug.Log($"[RollBetweenPositions] Hex {name} - Pivot target rotation: {targetRotation.eulerAngles}");

            bool rollComplete = false;

            // Create sequence with no loops or delays to prevent timing issues
            Sequence rollSequence = DOTween.Sequence();
            _currentTween = rollSequence; // Cache the tween
            Debug.Log($"[RollBetweenPositions] Hex {name} - Created roll sequence");

            // Add the rotation tween with a simple ease for reliability
            rollSequence.Append(pivotObject.transform.DORotateQuaternion(targetRotation, duration)
                .SetEase(Ease.Linear));
            Debug.Log($"[RollBetweenPositions] Hex {name} - Added rotation tween to sequence");

            // OnComplete callback ensures we're at the exact target position
            rollSequence.OnComplete(() =>
            {
                Debug.Log($"[RollBetweenPositions] Hex {name} - Roll sequence complete callback executing");

                // Log state before detaching
                Debug.Log(
                    $"[RollBetweenPositions] Hex {name} - Pre-detach state: position={transform.position}, parent={(transform.parent != null ? transform.parent.name : "null")}");

                // Detach from pivot
                transform.SetParent(null);
                Debug.Log($"[RollBetweenPositions] Hex {name} - Detached from pivot");

                // CRITICAL: Force exact position with the stack height
                Vector3 finalPos = new Vector3(toPos.x, toPos.y + stackHeight, toPos.z);
                Debug.Log(
                    $"[RollBetweenPositions] Hex {name} - Setting final position to exactly {finalPos} with stackHeight={stackHeight}, was at {transform.position}");
                transform.position = finalPos;

                // Clean up pivot
                Debug.Log($"[RollBetweenPositions] Hex {name} - Destroying pivot object");
                Destroy(pivotObject);

                rollComplete = true;
                Debug.Log(
                    $"[RollBetweenPositions] COMPLETED - Hex {name} final position set to exactly {transform.position}");
                // Clear the tween variable
                _currentTween = null;
            });

            // Wait for animation to complete
            Debug.Log($"[RollBetweenPositions] Hex {name} - Waiting for roll to complete");
            while (!rollComplete)
                yield return null;

            Debug.Log(
                $"[RollBetweenPositions] END - Hex {name} roll completed, final position: {transform.position}, stackHeight={stackHeight}");
        }

        private IEnumerator DelayedPositionCheck(Vector3 expectedPosition)
        {
            yield return null; // Wait one frame
            Debug.Log(
                $"[BUG_TRACE] Hex {name} - Position after one frame: {transform.position}, expected: {expectedPosition}, drift: {Vector3.Distance(transform.position, expectedPosition)}");

            yield return new WaitForSeconds(0.05f);
            Debug.Log(
                $"[BUG_TRACE] Hex {name} - Position after 0.05s: {transform.position}, expected: {expectedPosition}, drift: {Vector3.Distance(transform.position, expectedPosition)}");

            yield return new WaitForSeconds(0.1f);
            Debug.Log(
                $"[BUG_TRACE] Hex {name} - Position after 0.15s: {transform.position}, expected: {expectedPosition}, drift: {Vector3.Distance(transform.position, expectedPosition)}");
        }

        private void FallOffGrid(Vector2Int offGridCoords, HexDirection direction, GridManager gridManager)
        {
            Debug.Log($"[FallOffGrid] START - Hex {name} starting fall from {offGridCoords}, direction={direction}");

            // Standardize Y values
            float baseY = 0.01f;
            float hexYOffset = 0.2f;

            // Clear the cell reference
            Debug.Log(
                $"[FallOffGrid] Hex {name} - Clearing cell reference: {(currentCell != null ? currentCell.Coordinates.ToString() : "null")}");
            currentCell?.ClearHexagon();
            currentCell = null;

            // Precise position calculations with standardized Y
            Vector3 startPosition = new Vector3(transform.position.x, baseY + hexYOffset, transform.position.z);
            Vector3 directionVector = HexDirectionHelper.GetMovementVector(direction);
            Debug.Log(
                $"[FallOffGrid] Hex {name} - Start position: {startPosition}, direction vector: {directionVector}");

            // Calculate fall target further away and deeper to ensure it's off-screen
            Vector3 fallTarget = new Vector3(
                startPosition.x + directionVector.x * 3f,
                -15f, // Deep fall
                startPosition.z + directionVector.z * 3f
            );
            Debug.Log($"[FallOffGrid] Hex {name} - Fall target: {fallTarget}");

            // Normalize direction
            Vector3 moveDir = (fallTarget - startPosition).normalized;
            Debug.Log($"[FallOffGrid] Hex {name} - Normalized movement direction: {moveDir}");

            // Rotation axis perpendicular to movement
            Vector3 rotationAxis = Vector3.Cross(Vector3.up, moveDir).normalized;
            Debug.Log($"[FallOffGrid] Hex {name} - Rotation axis: {rotationAxis}");

            // Duration for a smooth fall
            float fallDuration = 0.5f;

            // Cancel any existing tween if present
            if (_currentTween != null)
            {
                Debug.Log($"[FallOffGrid] Hex {name} - Cancelling existing tween");
                _currentTween.Kill();
                _currentTween = null;
            }

            // Force position at start to ensure consistency
            Debug.Log($"[FallOffGrid] Hex {name} - Setting starting position to: {startPosition}");
            transform.position = startPosition;

            // Create simple sequence with no delays or loops
            Sequence fallSequence = DOTween.Sequence();
            _currentTween = fallSequence; // Cache the tween
            Debug.Log($"[FallOffGrid] Hex {name} - Created fall sequence");

            // Simple initial tip before falling
            fallSequence.Append(transform.DORotate(rotationAxis * 30f, 0.15f, RotateMode.LocalAxisAdd)
                .SetEase(Ease.Linear));
            Debug.Log($"[FallOffGrid] Hex {name} - Added initial tip to sequence");

            // Fall with simple acceleration
            fallSequence.Append(transform.DOMove(fallTarget, fallDuration)
                .SetEase(Ease.InQuad));
            Debug.Log($"[FallOffGrid] Hex {name} - Added fall movement to sequence");

            // Add simple rotation with no random values for consistency
            Vector3 tumbleRotation = new Vector3(
                rotationAxis.x * 360f,
                0f,
                rotationAxis.z * 360f
            );
            Debug.Log($"[FallOffGrid] Hex {name} - Tumble rotation: {tumbleRotation}");

            // Join tumble animation with fall
            fallSequence.Join(transform.DORotate(tumbleRotation, fallDuration, RotateMode.LocalAxisAdd)
                .SetEase(Ease.Linear));
            Debug.Log($"[FallOffGrid] Hex {name} - Added tumble rotation to sequence");

            // Final actions after animation completes
            fallSequence.OnComplete(() =>
            {
                Debug.Log($"[FallOffGrid] Hex {name} - Fall sequence completed");
                isBeingRemoved = true;
                Debug.Log($"[FallOffGrid] Hex {name} - Firing OnHexagonUnlocked event");
                OnHexagonUnlocked?.Invoke(this);
                Debug.Log($"[FallOffGrid] Hex {name} - Calling Vanish()");
                Vanish();
                _currentTween = null;
            });

            Debug.Log($"[FallOffGrid] END - Hex {name} fall animation started");
        }

        public void Vanish()
        {
            Debug.Log($"[Vanish] START - Hex {name} starting vanish. isBeingRemoved={isBeingRemoved}");

            if (!isBeingRemoved)
            {
                Debug.Log($"[Vanish] Hex {name} - Skipping vanish as isBeingRemoved=false");
                return;
            }

            // Cancel any existing tween if present
            if (_currentTween != null)
            {
                Debug.Log($"[Vanish] Hex {name} - Cancelling existing tween");
                _currentTween.Kill();
                _currentTween = null;
            }

            // Ensure this method only executes once
            isBeingRemoved = true;
            Debug.Log($"[Vanish] Hex {name} - Set isBeingRemoved=true");

            // Simple scale down with no randomness
            Debug.Log($"[Vanish] Hex {name} - Creating scale tween from {transform.localScale} to zero");
            Tween vanishTween = transform.DOScale(Vector3.zero, 0.2f)
                .SetEase(Ease.InQuad)
                .OnComplete(() =>
                {
                    Debug.Log($"[Vanish] Hex {name} - Scale tween completed");
                    if (GameManager.Instance != null)
                    {
                        Debug.Log($"[Vanish] Hex {name} - Calling GameManager.OnHexagonRemoved()");
                        GameManager.Instance.OnHexagonRemoved();
                    }
                    else
                    {
                        Debug.Log(
                            $"[Vanish] Hex {name} - GameManager.Instance is null, skipping OnHexagonRemoved call");
                    }

                    Debug.Log($"[Vanish] Hex {name} - Destroying game object");
                    Destroy(gameObject);
                });
            _currentTween = vanishTween; // Cache the tween
            Debug.Log($"[Vanish] END - Hex {name} vanish tween started");
        }

        private void SimpleUnlockAnimation(Vector3 directionVector)
        {
            Debug.Log(
                $"[SimpleUnlockAnimation] START - Hex {name} starting simple unlock with direction {directionVector}");
            isBeingRemoved = true;
            Debug.Log($"[SimpleUnlockAnimation] Hex {name} - Set isBeingRemoved=true");

            Vector3 targetPos = transform.position + directionVector * 2f;
            Debug.Log($"[SimpleUnlockAnimation] Hex {name} - Moving from {transform.position} to {targetPos}");

            transform.DOMove(targetPos, 0.5f)
                .SetEase(Ease.OutQuad)
                .OnComplete(() =>
                {
                    Debug.Log($"[SimpleUnlockAnimation] Hex {name} - Movement completed");
                    Debug.Log($"[SimpleUnlockAnimation] Hex {name} - Firing OnHexagonUnlocked event");
                    OnHexagonUnlocked?.Invoke(this);
                    Debug.Log($"[SimpleUnlockAnimation] Hex {name} - Calling Vanish()");
                    Vanish();
                });

            Debug.Log($"[SimpleUnlockAnimation] END - Hex {name} simple unlock tween started");
        }
    }
}