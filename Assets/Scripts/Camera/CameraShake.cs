using System.Collections;
using UnityEngine;

namespace ReturnToTheEigth.CameraSystem
{
    /// <summary>Camera shake driven by Perlin noise: XY offset plus a slight roll that fade out over the duration, then the rest pose is restored.</summary>
    [DisallowMultipleComponent]
    public sealed class CameraShake : MonoBehaviour
    {
        private const float Zero = 0f;
        private const float One = 1f;
        private const float Half = 0.5f;
        private const float DefaultFrequency = 22f;
        private const float DefaultRollDegrees = 2.5f;
        private const float NoiseRange = 2f;
        private const float NoiseSeedX = 0f;
        private const float NoiseSeedY = 37.1f;
        private const float NoiseSeedRoll = 73.7f;
        private const float MaxRandomSeed = 1000f;

        [SerializeField, Min(Zero)] private float frequency = DefaultFrequency;
        [SerializeField, Min(Zero)] private float rollDegrees = DefaultRollDegrees;

        private Coroutine shakeRoutine;
        private Coroutine shakeSequenceRoutine;
        private Vector3 restPosition;
        private Quaternion restRotation;
        private TopDownCameraFollow topDownCameraFollow;

        public bool IsShaking => shakeRoutine != null;
        public Vector3 CurrentOffset { get; private set; }
        public float CurrentRollDegrees { get; private set; }

        private void Awake()
        {
            topDownCameraFollow = GetComponent<TopDownCameraFollow>();
        }

        /// <summary>Shakes the camera for <paramref name="duration"/> seconds with a peak XY offset of <paramref name="magnitude"/> units. Restarts if already shaking.</summary>
        public void Shake(float duration, float magnitude)
        {
            Shake(duration, magnitude, rollDegrees);
        }

        /// <summary>Shakes the camera with a custom peak XY offset and roll. Restarts if already shaking.</summary>
        public void Shake(float duration, float magnitude, float rollMagnitude)
        {
            if (duration <= Zero || magnitude <= Zero || rollMagnitude < Zero) return;
            Stop();
            restPosition = transform.localPosition;
            restRotation = transform.localRotation;
            shakeRoutine = StartCoroutine(ShakeRoutine(duration, magnitude, rollMagnitude));
        }

        /// <summary>Runs several separated camera shakes, restarting any sequence already in progress.</summary>
        public void ShakeSequence(int shakeCount, float duration, float magnitude, float interval)
        {
            if (shakeCount <= 0 || duration <= Zero || magnitude <= Zero || interval < Zero) return;

            if (shakeSequenceRoutine != null)
            {
                StopCoroutine(shakeSequenceRoutine);
                shakeSequenceRoutine = null;
            }

            Stop();
            shakeSequenceRoutine = StartCoroutine(ShakeSequenceRoutine(shakeCount, duration, magnitude, interval));
        }

        /// <summary>Stops the current shake and any remaining shake sequence, then restores the rest pose.</summary>
        public void StopAllShakes()
        {
            if (shakeSequenceRoutine != null)
            {
                StopCoroutine(shakeSequenceRoutine);
                shakeSequenceRoutine = null;
            }

            Stop();
        }

        /// <summary>Stops the current shake (if any) and restores the rest pose.</summary>
        public void Stop()
        {
            if (shakeRoutine == null) return;
            StopCoroutine(shakeRoutine);
            shakeRoutine = null;
            RestorePose();
        }

        private void OnDisable()
        {
            if (shakeSequenceRoutine != null)
            {
                StopCoroutine(shakeSequenceRoutine);
                shakeSequenceRoutine = null;
            }

            Stop();
        }

        private IEnumerator ShakeSequenceRoutine(int shakeCount, float duration, float magnitude, float interval)
        {
            for (int shakeIndex = 0; shakeIndex < shakeCount; shakeIndex++)
            {
                Shake(duration, magnitude);
                float waitDuration = duration + (shakeIndex < shakeCount - 1 ? interval : Zero);
                yield return new WaitForSecondsRealtime(waitDuration);
            }

            shakeSequenceRoutine = null;
        }

        private IEnumerator ShakeRoutine(float duration, float magnitude, float rollMagnitude)
        {
            float seed = Random.Range(Zero, MaxRandomSeed);
            float elapsed = Zero;
            while (elapsed < duration)
            {
                float falloff = One - elapsed / duration;
                float time = seed + elapsed * frequency;
                float offsetX = SignedNoise(time, NoiseSeedX) * magnitude * falloff;
                float offsetY = SignedNoise(time, NoiseSeedY) * magnitude * falloff;
                float roll = SignedNoise(time, NoiseSeedRoll) * rollMagnitude * falloff;
                Vector3 offset = new Vector3(offsetX, offsetY, Zero);
                if (topDownCameraFollow != null)
                {
                    CurrentOffset = offset;
                    CurrentRollDegrees = roll;
                }
                else
                {
                    transform.localPosition = restPosition + offset;
                    transform.localRotation = restRotation * Quaternion.Euler(Zero, Zero, roll);
                }
                elapsed += Time.deltaTime;
                yield return null;
            }
            shakeRoutine = null;
            RestorePose();
        }

        private void RestorePose()
        {
            CurrentOffset = Vector3.zero;
            CurrentRollDegrees = Zero;
            if (topDownCameraFollow != null) return;
            transform.localPosition = restPosition;
            transform.localRotation = restRotation;
        }

        private static float SignedNoise(float x, float y)
        {
            return (Mathf.PerlinNoise(x, y) - Half) * NoiseRange;
        }
    }
}
