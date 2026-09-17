using UnityEngine;

namespace ReturnToTheEigth.CameraSystem
{
    /// <summary>Front-facing orthographic XY camera; optionally follows the player without rotating.</summary>
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    public sealed class TopDownCameraFollow : MonoBehaviour
    {
        private const float CameraDepth = -10f;
        private const float DefaultSmoothTime = 0.15f;
        private const float MinimumSmoothTime = 0.01f;
        private const float DefaultPixelsPerUnit = 100f;
        private const float Zero = 0f;
        private const float One = 1f;
        [SerializeField] private Transform target;
        [SerializeField] private bool followTarget;
        [SerializeField, Min(MinimumSmoothTime)] private float smoothTime = DefaultSmoothTime;
        [SerializeField, Min(One)] private float pixelsPerUnit = DefaultPixelsPerUnit;
        private Vector3 followVelocity;
        private Vector3 smoothPosition;

        private void Start()
        {
            GetComponent<Camera>().orthographic = true;
            transform.rotation = Quaternion.identity;
            smoothPosition = new Vector3(transform.position.x, transform.position.y, CameraDepth);
            transform.position = smoothPosition;
        }

        private void LateUpdate()
        {
            if (!followTarget || target == null)
            {
                return;
            }
            Vector3 goal = new Vector3(target.position.x, target.position.y, CameraDepth);
            smoothPosition = Vector3.SmoothDamp(smoothPosition, goal, ref followVelocity, smoothTime);
            transform.position = new Vector3(Mathf.Round(smoothPosition.x * pixelsPerUnit) / pixelsPerUnit,
                Mathf.Round(smoothPosition.y * pixelsPerUnit) / pixelsPerUnit, CameraDepth);
            transform.rotation = Quaternion.identity;
        }

        /// <summary>Assigns the XY follow target; leaves the full-map framing unchanged when following is disabled.</summary>
        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            followVelocity = Vector3.zero;
            if (followTarget && target != null)
            {
                smoothPosition = new Vector3(target.position.x, target.position.y, CameraDepth);
                transform.position = smoothPosition;
            }
        }
    }
}
