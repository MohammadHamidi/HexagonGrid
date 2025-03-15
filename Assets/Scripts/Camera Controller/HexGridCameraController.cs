using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HexaAway.Core
{
    [RequireComponent(typeof(Camera))]
    public class HexGridCameraController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GridManager gridManager;

        [Header("Camera Position Settings")]
        [SerializeField] private float cameraHeight = 10.0f;
        [SerializeField] private Vector3 cameraAngle = new Vector3(60, 0, 0);
        [SerializeField] private bool centerImmediately = true;
        [SerializeField] private float smoothTime = 0.3f;
        [Header("Orientation Settings")]
        [SerializeField] private float landscapeSizeMultiplier = 1.2f; // Add this to your class variables

// Then in CalculateIdealOrthographicSize():

        [Header("Screen Edge Margins")]
        [Range(0f, 0.5f)]
        [SerializeField] private float topMargin = 0.05f;
        [Range(0f, 0.5f)]
        [SerializeField] private float bottomMargin = 0.05f;
        [Range(0f, 0.5f)]
        [SerializeField] private float leftMargin = 0.05f;
        [Range(0f, 0.5f)]
        [SerializeField] private float rightMargin = 0.05f;

        [Header("Camera Size Settings")]
        [SerializeField] private float minOrthographicSize = 5.0f;
        [SerializeField] private float extraPadding = 0.5f;
        [SerializeField] private float aspectRatioThreshold = 1.05f; // Threshold to determine landscape vs portrait

        [Header("Advanced Settings")]
        [SerializeField] private bool autoAdjustForAspectRatio = true;
        [SerializeField] private bool useOptimizedBounds = true;
        [SerializeField] private float hexagonRadius = 0.6f;

        private Camera _camera;
        private Vector3 _targetPosition;
        private Quaternion _targetRotation;
        private float _targetOrthographicSize;
        private Vector3 _positionVelocity = Vector3.zero;
        private float _sizeVelocity = 0f;
        private Quaternion _initialRotation;
        private bool _wasPortraitMode;
        private float _lastAspectRatio;

        private Vector3 _gridCenter;
        private Bounds _gridBounds;
        private bool _initialized = false;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            if (_camera == null)
            {
                Debug.LogError("Camera component not found!");
                enabled = false;
                return;
            }

            // Find GridManager if not assigned
            if (gridManager == null)
            {
                gridManager = FindObjectOfType<GridManager>();
                if (gridManager == null)
                {
                    Debug.LogError("GridManager reference is missing!");
                    enabled = false;
                    return;
                }
            }

            // Store initial rotation for reuse
            _initialRotation = Quaternion.Euler(cameraAngle);
            _wasPortraitMode = IsPortraitMode();
            _lastAspectRatio = GetCurrentAspectRatio();
        }

        private void Start()
        {
            // Wait until the grid has finished generating
            StartCoroutine(InitializeAfterGridGeneration());
        }

        private IEnumerator InitializeAfterGridGeneration()
        {
            // Wait a frame to ensure grid is generated
            yield return null;
            yield return null; // Extra frame to be sure
            
            // Calculate grid bounds and position the camera
            CalculateGridBounds();
            
            if (centerImmediately)
            {
                CenterCameraImmediately();
            }
            else
            {
                UpdateCameraTargets();
            }
            
            _initialized = true;
        }

        private void Update()
        {
            if (!_initialized)
                return;

            // Check for significant aspect ratio changes
            DetectAspectRatioChanges();
                
            // Smooth transition to target position, rotation and size
            transform.position = Vector3.SmoothDamp(transform.position, _targetPosition, ref _positionVelocity, smoothTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, _targetRotation, Time.deltaTime / smoothTime);
            
            if (_camera.orthographic)
            {
                _camera.orthographicSize = Mathf.SmoothDamp(
                    _camera.orthographicSize, 
                    _targetOrthographicSize, 
                    ref _sizeVelocity, 
                    smoothTime
                );
            }
        }

        private void DetectAspectRatioChanges()
        {
            if (!autoAdjustForAspectRatio)
                return;
                
            float currentAspect = GetCurrentAspectRatio();
            bool isPortraitNow = IsPortraitMode();
            
            // Check if orientation changed or aspect ratio changed significantly
            if (isPortraitNow != _wasPortraitMode || 
                Mathf.Abs(currentAspect - _lastAspectRatio) > 0.05f)
            {
                RecenterCamera();
                _wasPortraitMode = isPortraitNow;
                _lastAspectRatio = currentAspect;
                Debug.Log($"Aspect ratio changed: {_lastAspectRatio:F2}, Portrait: {_wasPortraitMode}");
            }
        }

        private bool IsPortraitMode()
        {
            return GetCurrentAspectRatio() < 1.0f;
        }

        private float GetCurrentAspectRatio()
        {
            return (float)Screen.width / Screen.height;
        }

        /// <summary>
        /// Calculates the bounds of the grid based on all cell positions
        /// </summary>
        private void CalculateGridBounds()
        {
            List<Vector2Int> cellCoords = gridManager.GetAllCellCoordinates();
            if (cellCoords.Count == 0)
            {
                Debug.LogWarning("No cells found in the grid");
                _gridBounds = new Bounds(Vector3.zero, Vector3.one * 10);
                _gridCenter = Vector3.zero;
                return;
            }

            if (useOptimizedBounds && cellCoords.Count > 1)
            {
                CalculateOptimizedBounds(cellCoords);
            }
            else
            {
                CalculateAxisAlignedBounds(cellCoords);
            }
        }

        /// <summary>
        /// Calculates standard axis-aligned bounding box
        /// </summary>
        private void CalculateAxisAlignedBounds(List<Vector2Int> cellCoords)
        {
            // Use first cell to initialize bounds
            Vector3 firstCellPos = gridManager.HexToWorld(cellCoords[0]);
            _gridBounds = new Bounds(firstCellPos, Vector3.zero);

            // Expand bounds to include all cells with a slight buffer for hexagon size
            foreach (Vector2Int coords in cellCoords)
            {
                Vector3 cellPos = gridManager.HexToWorld(coords);
                
                // Create a small bounds for each cell
                Bounds cellBounds = new Bounds(cellPos, new Vector3(hexagonRadius * 2, 0.1f, hexagonRadius * 2));
                
                // Expand the grid bounds to include this cell
                _gridBounds.Encapsulate(cellBounds);
            }

            // Calculate center of the grid
            _gridCenter = _gridBounds.center;
            _gridCenter.y = 0; // Keep the same Y level
        }

        /// <summary>
        /// Calculates a more optimized oriented bounding box
        /// </summary>
        private void CalculateOptimizedBounds(List<Vector2Int> cellCoords)
        {
            // Convert all cells to world positions
            List<Vector3> cellPositions = new List<Vector3>();
            foreach (Vector2Int coords in cellCoords)
            {
                cellPositions.Add(gridManager.HexToWorld(coords));
            }

            // Find min/max X and Z to determine extents
            float minX = float.MaxValue, maxX = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;
            Vector3 sum = Vector3.zero;

            foreach (Vector3 pos in cellPositions)
            {
                minX = Mathf.Min(minX, pos.x);
                maxX = Mathf.Max(maxX, pos.x);
                minZ = Mathf.Min(minZ, pos.z);
                maxZ = Mathf.Max(maxZ, pos.z);
                sum += pos;
            }

            // Calculate center and size
            _gridCenter = sum / cellPositions.Count;
            _gridCenter.y = 0; // Ensure Y is at grid level

            // Create bounds that include all cells with a bit of extra padding
            float width = maxX - minX;
            float height = maxZ - minZ;
            
            // Add a small buffer for hexagon size
            width += hexagonRadius * 2; // Extra buffer for hexagon width
            height += hexagonRadius * 2; // Extra buffer for hexagon height

            _gridBounds = new Bounds(_gridCenter, new Vector3(width, 0.1f, height));
        }

        /// <summary>
        /// Calculate the ideal camera position and rotation to center on the grid
        /// </summary>
        private void UpdateCameraTargets()
        {
            // Set target rotation from the inspector angle
            _targetRotation = Quaternion.Euler(cameraAngle);
            
            // Set target position using the grid center and height
            _targetPosition = new Vector3(_gridCenter.x, cameraHeight, _gridCenter.z);
            
            // Calculate the orthographic size needed
            _targetOrthographicSize = CalculateIdealOrthographicSize();
        }

        /// <summary>
        /// Calculate the ideal orthographic size to fit the grid
        /// </summary>
        private float CalculateIdealOrthographicSize()
        {
            if (!_camera.orthographic)
                return 0f;
        
            // Get the screen aspect ratio
            float aspect = GetCurrentAspectRatio();
            bool isPortrait = aspect < aspectRatioThreshold; // Use the threshold you defined
    
            // Calculate the effective viewable area with margins
            float effectiveWidth = 1.0f - (leftMargin + rightMargin);
            float effectiveHeight = 1.0f - (topMargin + bottomMargin);
    
            // Adjust the aspect ratio to account for margins
            float effectiveAspect = (aspect * effectiveWidth) / effectiveHeight;
    
            // Calculate the required size to fit the grid
            float gridWidth = _gridBounds.size.x + (extraPadding * 2);
            float gridHeight = _gridBounds.size.z + (extraPadding * 2);
    
            // Calculate the size needed to fit the width and height
            float widthSize = gridWidth / (2 * effectiveAspect);
            float heightSize = gridHeight / 2;
    
            // For landscape mode, we need to adjust the calculation
            if (!isPortrait)
            {
                // In landscape, we need additional adjustment to prevent the grid from appearing too small
                widthSize *= 1.0f; // Keep as is
                heightSize /= effectiveHeight; // Adjust height calculation
            }
    
            // Use the larger of the two to ensure grid fits fully
            float targetSize = Mathf.Max(widthSize, heightSize);
    
            if (!isPortrait)
            {
                targetSize *= landscapeSizeMultiplier;
            }
            else
            {
                targetSize *= ((1 / landscapeSizeMultiplier)* 0.8f);
            }

            // Apply minimum size constraint
            return Mathf.Max(targetSize, minOrthographicSize);
        }
        /// <summary>
        /// Public method to center the camera with a smooth transition
        /// </summary>
        public void RecenterCamera()
        {
            CalculateGridBounds();
            UpdateCameraTargets();
            
            Debug.Log($"Camera recentered: Position={_targetPosition}, " +
                      $"Size={_targetOrthographicSize}, " +
                      $"Aspect={GetCurrentAspectRatio():F2}, " +
                      $"Effective Margins: L:{leftMargin:F2} R:{rightMargin:F2} T:{topMargin:F2} B:{bottomMargin:F2}");
        }
        
        /// <summary>
        /// Centers the camera immediately without any smooth transition
        /// </summary>
        public void CenterCameraImmediately()
        {
            UpdateCameraTargets();
            
            transform.position = _targetPosition;
            transform.rotation = _targetRotation;
            
            if (_camera.orthographic)
            {
                _camera.orthographicSize = _targetOrthographicSize;
            }
            
            Debug.Log($"Camera immediately centered: Position={_targetPosition}, " +
                      $"Size={_targetOrthographicSize}, " +
                      $"Aspect={GetCurrentAspectRatio():F2}");
        }

        /// <summary>
        /// Update camera rotation
        /// </summary>
        public void SetCameraAngle(Vector3 newAngle)
        {
            cameraAngle = newAngle;
            if (_initialized)
            {
                _targetRotation = Quaternion.Euler(cameraAngle);
            }
        }

        /// <summary>
        /// Set all margin values at once
        /// </summary>
        public void SetMargins(float top, float bottom, float left, float right)
        {
            topMargin = Mathf.Clamp01(top);
            bottomMargin = Mathf.Clamp01(bottom);
            leftMargin = Mathf.Clamp01(left);
            rightMargin = Mathf.Clamp01(right);
            
            if (_initialized)
            {
                RecenterCamera();
            }
        }
        
        // Properties for individual margins
        public float TopMargin 
        { 
            get => topMargin; 
            set { topMargin = Mathf.Clamp(value, 0f, 0.5f); if (_initialized) RecenterCamera(); } 
        }
        
        public float BottomMargin 
        { 
            get => bottomMargin; 
            set { bottomMargin = Mathf.Clamp(value, 0f, 0.5f); if (_initialized) RecenterCamera(); } 
        }
        
        public float LeftMargin 
        { 
            get => leftMargin; 
            set { leftMargin = Mathf.Clamp(value, 0f, 0.5f); if (_initialized) RecenterCamera(); } 
        }
        
        public float RightMargin 
        { 
            get => rightMargin; 
            set { rightMargin = Mathf.Clamp(value, 0f, 0.5f); if (_initialized) RecenterCamera(); } 
        }
        
        private void OnDrawGizmos()
        {
            if (!_initialized || !Application.isPlaying)
                return;
                
            // Draw grid bounds for debugging
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(_gridBounds.center, _gridBounds.size);
            
            // Draw effective camera view with margins applied
            Gizmos.color = Color.cyan;
            
            if (_camera.orthographic)
            {
                // Calculate frustum corners for orthographic camera with margins
                float size = _camera.orthographicSize;
                float aspect = _camera.aspect;
                Vector3 pos = transform.position;
                
                // Calculate the viewable area with margins
                float left = -size * aspect * (1f - leftMargin * 2f);
                float right = size * aspect * (1f - rightMargin * 2f);
                float top = size * (1f - topMargin * 2f);
                float bottom = -size * (1f - bottomMargin * 2f);
                
                // Compensate for the camera's rotation
                Vector3 forward = transform.forward;
                Vector3 right3D = transform.right;
                Vector3 up3D = transform.up;
                
                // Project the frustum onto the ground plane (y=0)
                float distToGround = pos.y;
                float scale = distToGround / Mathf.Abs(forward.y);
                
                // Draw the view rectangle corners
                Vector3 topLeft = pos + (forward * scale) 
                               + (right3D * left)
                               + (up3D * top);
                Vector3 topRight = pos + (forward * scale) 
                                + (right3D * right)
                                + (up3D * top);
                Vector3 bottomLeft = pos + (forward * scale) 
                                  + (right3D * left)
                                  + (up3D * bottom);
                Vector3 bottomRight = pos + (forward * scale) 
                                   + (right3D * right)
                                   + (up3D * bottom);
                
                // Project points to ground plane (y=0)
                topLeft.y = 0.01f;
                topRight.y = 0.01f;
                bottomLeft.y = 0.01f;
                bottomRight.y = 0.01f;
                
                // Draw the view rectangle
                Gizmos.DrawLine(topLeft, topRight);
                Gizmos.DrawLine(topRight, bottomRight);
                Gizmos.DrawLine(bottomRight, bottomLeft);
                Gizmos.DrawLine(bottomLeft, topLeft);
                
                // Draw the center point
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(_gridCenter, 0.2f);
            }
        }
    }
}