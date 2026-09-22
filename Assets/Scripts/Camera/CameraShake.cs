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
        private Vector3 restPosition;
        private Quaternion restRotation;

        public bool IsShaking => shakeRoutine != null;

        /// <summary>Shakes the camera for <paramref name="duration"/> seconds with a peak XY offset of <paramref name="magnitude"/> units. Restarts if already shaking.</summary>
        public void Shake(float duration, float magnitude)
        {
            if (duration <= Zero || magnitude <= Zero) return;
            Stop();
            restPosition = transform.localPosition;
            restRotation = transform.localRotation;
            shakeRoutine = StartCoroutine(ShakeRoutine(duration, magnitude));
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
            Stop();
        }

        private IEnumerator ShakeRoutine(float duration, float magnitude)
        {
            float seed = Random.Range(Zero, MaxRandomSeed);
            float elapsed = Zero;
            while (elapsed < duration)
            {
                float falloff = One - elapsed / duration;
                float time = seed + elapsed * frequency;
                float offsetX = SignedNoise(time, NoiseSeedX) * magnitude * falloff;
                float offsetY = SignedNoise(time, NoiseSeedY) * magnitude * falloff;
                float roll = SignedNoise(time, NoiseSeedRoll) * rollDegrees * falloff;
                transform.localPosition = restPosition + new Vector3(offsetX, offsetY, Zero);
                transform.localRotation = restRotation * Quaternion.Euler(Zero, Zero, roll);
                elapsed += Time.deltaTime;
                yield return null;
            }
            shakeRoutine = null;
            RestorePose();
        }

        private void RestorePose()
        {
            transform.localPosition = restPosition;
            transform.localRotation = restRotation;
        }

        private static float SignedNoise(float x, float y)
        {
            return (Mathf.PerlinNoise(x, y) - Half) * NoiseRange;
        }
    }
}
