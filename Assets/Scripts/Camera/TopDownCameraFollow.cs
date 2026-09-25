using UnityEngine;

namespace ReturnToTheEigth.CameraSystem
{
    /// <summary>Frames exactly one XY room in edit and play mode, cutting at room boundaries.</summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    public sealed class TopDownCameraFollow : MonoBehaviour
    {
        private const float CameraDepth = -10f;
        private const float Half = 0.5f;
        private const float Zero = 0f;
        private const float One = 1f;
        private const float MinimumRoomDimension = 0.001f;
        private const float DefaultRoomWidth = 4.26f;
        private const float DefaultRoomHeight = 2.4f;
        private const float DefaultOriginX = -4.26f;
        private const float DefaultOriginY = -4.8f;
        private const int FirstRoom = 0;
        private const int MinimumRoomCount = 1;
        private const int DefaultColumns = 2;
        private const int DefaultRows = 4;
        private static readonly Vector2 DefaultRoomSize = new Vector2(DefaultRoomWidth, DefaultRoomHeight);
        private static readonly Vector2 DefaultGridOrigin = new Vector2(DefaultOriginX, DefaultOriginY);
        private static readonly Vector2Int DefaultRoomCount = new Vector2Int(DefaultColumns, DefaultRows);

        [SerializeField] private Transform target;
        [SerializeField] private Vector2 roomSize = DefaultRoomSize;
        [SerializeField] private Vector2 gridOrigin = DefaultGridOrigin;
        [SerializeField] private Vector2Int roomCount = DefaultRoomCount;
        private Camera roomCamera;
        public Vector2Int CurrentRoom { get; private set; }

        private void Awake()
        {
            roomCamera = GetComponent<Camera>();
        }

        private void OnEnable()
        {
            RefreshRoomFraming();
        }

        private void LateUpdate()
        {
            RefreshRoomFraming();
        }

        private void OnValidate()
        {
            roomSize.x = Mathf.Max(MinimumRoomDimension, roomSize.x);
            roomSize.y = Mathf.Max(MinimumRoomDimension, roomSize.y);
            roomCount.x = Mathf.Max(MinimumRoomCount, roomCount.x);
            roomCount.y = Mathf.Max(MinimumRoomCount, roomCount.y);
        }

        /// <summary>Assigns the player and immediately frames its room instead of centering on the player.</summary>
        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            RefreshRoomFraming();
        }

        /// <summary>Applies the exact room bounds and a matching viewport, including after a display resize.</summary>
        public void RefreshRoomFraming()
        {
            if (roomCamera == null) roomCamera = GetComponent<Camera>();
            if (target == null || roomCamera == null) return;

            float width = Mathf.Max(MinimumRoomDimension, roomSize.x);
            float height = Mathf.Max(MinimumRoomDimension, roomSize.y);
            int column = Mathf.Clamp(Mathf.FloorToInt((target.position.x - gridOrigin.x) / width),
                FirstRoom, Mathf.Max(MinimumRoomCount, roomCount.x) - MinimumRoomCount);
            int row = Mathf.Clamp(Mathf.FloorToInt((target.position.y - gridOrigin.y) / height),
                FirstRoom, Mathf.Max(MinimumRoomCount, roomCount.y) - MinimumRoomCount);
            CurrentRoom = new Vector2Int(column, row);
            transform.SetPositionAndRotation(new Vector3(gridOrigin.x + (column + Half) * width,
                gridOrigin.y + (row + Half) * height, CameraDepth), Quaternion.identity);

            roomCamera.orthographic = true;
            roomCamera.orthographicSize = height * Half;
            float roomAspect = width / height;
            // Screen dimensions can describe a different editor window outside Play mode.
            // Recover this camera's full render surface from its pixel viewport instead.
            float outputWidth = roomCamera.targetTexture != null ? roomCamera.targetTexture.width
                : Application.isPlaying ? Screen.width
                : roomCamera.pixelWidth / Mathf.Max(MinimumRoomDimension, roomCamera.rect.width);
            float outputHeight = roomCamera.targetTexture != null ? roomCamera.targetTexture.height
                : Application.isPlaying ? Screen.height
                : roomCamera.pixelHeight / Mathf.Max(MinimumRoomDimension, roomCamera.rect.height);
            float outputAspect = Mathf.Max(One, outputWidth) / Mathf.Max(One, outputHeight);
            if (outputAspect > roomAspect)
            {
                float viewportWidth = roomAspect / outputAspect;
                roomCamera.rect = new Rect((One - viewportWidth) * Half, Zero, viewportWidth, One);
            }
            else
            {
                float viewportHeight = outputAspect / roomAspect;
                roomCamera.rect = new Rect(Zero, (One - viewportHeight) * Half, One, viewportHeight);
            }
            // Set the projection aspect explicitly to avoid subpixel viewport rounding revealing adjacent rooms.
            roomCamera.aspect = roomAspect;
        }
    }
}
