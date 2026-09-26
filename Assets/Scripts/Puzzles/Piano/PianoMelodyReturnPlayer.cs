using System;
using System.Collections;
using ReturnToTheEigth.CameraSystem;
using ReturnToTheEigth.Core;
using ReturnToTheEigth.Player;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ReturnToTheEigth.Puzzles.Piano
{
    /// <summary>Persists the melody audio source across the Hall scene load and restores player control afterward.</summary>
    [DisallowMultipleComponent]
    public sealed class PianoMelodyReturnPlayer : MonoBehaviour
    {
        private const string HallSceneName = "Hall";
        private const string MissingMelodyWarning = "Piano melody clip is missing; Hall will load without music.";
        private const string FailedLoadErrorFormat = "Could not return to Hall after the piano puzzle: {0}";
        private const float Zero = 0f;
        private const float One = 1f;
        private const float FadeDuration = 0.35f;
        private const float BlackoutDuration = 1f;
        private const int BlackoutCanvasSortingOrder = 10000;
        private const int FirstShakeIndex = 0;
        private const int ShakeCount = 5;
        private static readonly float[] EarthquakeShakeDurations = { 0.22f, 0.28f, 0.34f, 0.42f, 0.85f };
        private static readonly float[] EarthquakeShakeMagnitudes = { 0.12f, 0.22f, 0.38f, 0.68f, 1.6f };
        private static readonly Vector3 UpperLeftRoomDestination = new Vector3(-3.787f, 3.158f, 0f);
        private static readonly Color TransparentBlack = new Color(0f, 0f, 0f, 0f);
        private static readonly Color OpaqueBlack = Color.black;
        private static readonly Vector2 FullScreenAnchorMin = Vector2.zero;
        private static readonly Vector2 FullScreenAnchorMax = Vector2.one;
        private static readonly Vector2 FullScreenOffset = Vector2.zero;

        private GameManager gameManager;
        private AudioSource audioSource;
        private AudioClip melodyClip;
        private TopDownCharacterController playerController;
        private TopDownCameraFollow cameraFollow;
        private CameraShake earthquakeCameraShake;
        private Image blackoutImage;
        private bool hasStarted;

        /// <summary>Loads Hall, plays the supplied melody from a persistent 2D audio source, and restores exploration state.</summary>
        public void ReturnToHallAndPlay(AudioClip clip, GameManager manager)
        {
            if (hasStarted)
            {
                return;
            }

            hasStarted = true;
            melodyClip = clip;
            gameManager = manager;
            DontDestroyOnLoad(gameObject);
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = Zero;
            StartCoroutine(LoadHallAndPlayMelody());
        }

        private IEnumerator LoadHallAndPlayMelody()
        {
            AsyncOperation loadOperation = null;
            bool loadFailed = false;
            SceneManager.sceneLoaded -= HandleHallSceneLoaded;
            SceneManager.sceneLoaded += HandleHallSceneLoaded;
            try
            {
                loadOperation = SceneManager.LoadSceneAsync(HallSceneName, LoadSceneMode.Single);
            }
            catch (Exception exception)
            {
                Debug.LogError(string.Format(FailedLoadErrorFormat, exception.Message), this);
                loadFailed = true;
            }

            if (loadFailed || loadOperation == null)
            {
                if (!loadFailed)
                {
                    Debug.LogError(string.Format(FailedLoadErrorFormat, HallSceneName), this);
                }
                RestoreExplorationAndDestroy();
                yield break;
            }

            yield return loadOperation;
            yield return null;

            if (melodyClip == null || audioSource == null)
            {
                Debug.LogWarning(MissingMelodyWarning, this);
                RestoreExplorationAndDestroy();
                yield break;
            }

            audioSource.clip = melodyClip;
            audioSource.Play();
            while (audioSource != null && audioSource.isPlaying)
            {
                yield return null;
            }

            yield return StartCoroutine(PlayEarthquakeTransition());
            RestoreExplorationAndDestroy();
        }

        private void HandleHallSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!string.Equals(scene.name, HallSceneName, StringComparison.Ordinal))
            {
                return;
            }

            playerController = FindAnyObjectByType<TopDownCharacterController>();
            playerController?.SetMovementEnabled(false);
        }

        private IEnumerator PlayEarthquakeTransition()
        {
            playerController ??= FindAnyObjectByType<TopDownCharacterController>();
            cameraFollow = FindAnyObjectByType<TopDownCameraFollow>();
            Camera mainCamera = Camera.main;
            if (cameraFollow != null)
            {
                cameraFollow.enabled = false;
            }

            if (mainCamera != null)
            {
                earthquakeCameraShake = mainCamera.GetComponent<CameraShake>();
                if (earthquakeCameraShake == null)
                {
                    earthquakeCameraShake = mainCamera.gameObject.AddComponent<CameraShake>();
                }
            }

            for (int index = FirstShakeIndex; index < ShakeCount; index++)
            {
                earthquakeCameraShake?.Shake(EarthquakeShakeDurations[index], EarthquakeShakeMagnitudes[index]);
                yield return new WaitForSecondsRealtime(EarthquakeShakeDurations[index]);
                while (earthquakeCameraShake != null && earthquakeCameraShake.IsShaking)
                {
                    yield return null;
                }
            }

            CreateBlackoutOverlay();
            yield return FadeBlackoutTo(One, FadeDuration);
            yield return new WaitForSecondsRealtime(BlackoutDuration);
            TeleportPlayerToUpperLeftRoom();

            if (cameraFollow != null)
            {
                cameraFollow.enabled = true;
                cameraFollow.RefreshRoomFraming();
            }

            yield return FadeBlackoutTo(Zero, FadeDuration);
        }

        private void CreateBlackoutOverlay()
        {
            GameObject canvasObject = new GameObject("PianoEarthquakeBlackout", typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(transform, false);
            Canvas blackoutCanvas = canvasObject.GetComponent<Canvas>();
            blackoutCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            blackoutCanvas.sortingOrder = BlackoutCanvasSortingOrder;

            GameObject imageObject = new GameObject("BlackoutImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(canvasObject.transform, false);
            RectTransform imageRect = imageObject.GetComponent<RectTransform>();
            imageRect.anchorMin = FullScreenAnchorMin;
            imageRect.anchorMax = FullScreenAnchorMax;
            imageRect.offsetMin = FullScreenOffset;
            imageRect.offsetMax = FullScreenOffset;

            blackoutImage = imageObject.GetComponent<Image>();
            blackoutImage.color = TransparentBlack;
            blackoutImage.raycastTarget = false;
        }

        private IEnumerator FadeBlackoutTo(float targetAlpha, float duration)
        {
            if (blackoutImage == null)
            {
                yield break;
            }

            Color color = blackoutImage.color;
            float startAlpha = color.a;
            float elapsed = Zero;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                color.a = Mathf.Lerp(startAlpha, targetAlpha, Mathf.Clamp01(elapsed / duration));
                blackoutImage.color = color;
                yield return null;
            }

            color = targetAlpha >= One ? OpaqueBlack : TransparentBlack;
            blackoutImage.color = color;
        }

        private void TeleportPlayerToUpperLeftRoom()
        {
            playerController ??= FindAnyObjectByType<TopDownCharacterController>();
            if (playerController == null)
            {
                return;
            }

            Rigidbody2D body = playerController.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.position = new Vector2(UpperLeftRoomDestination.x, UpperLeftRoomDestination.y);
                body.linearVelocity = Vector2.zero;
            }
            else
            {
                playerController.transform.position = UpperLeftRoomDestination;
            }

            Physics2D.SyncTransforms();
        }

        private void RestoreExplorationAndDestroy()
        {
            SceneManager.sceneLoaded -= HandleHallSceneLoaded;
            if (gameManager != null)
            {
                gameManager.SetGameState(GameState.Exploration);
            }

            playerController?.SetMovementEnabled(true);
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= HandleHallSceneLoaded;
            if (hasStarted && gameManager != null && gameManager.CurrentGameState == GameState.Puzzle)
            {
                gameManager.SetGameState(GameState.Exploration);
            }

            if (playerController != null)
            {
                playerController.SetMovementEnabled(true);
            }
        }
    }
}
