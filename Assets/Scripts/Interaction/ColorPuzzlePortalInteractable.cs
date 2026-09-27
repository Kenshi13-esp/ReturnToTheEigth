using ReturnToTheEigth.Core;
using ReturnToTheEigth.Player;
using ReturnToTheEigth.TimeTravel;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ReturnToTheEigth.Interaction
{
    /// <summary>Loads the colour-track puzzle when the player interacts with its softly glowing marker.</summary>
    [RequireComponent(typeof(BoxCollider2D), typeof(LineRenderer))]
    [DisallowMultipleComponent]
    public sealed class ColorPuzzlePortalInteractable : InteractableBase, IInteractionHighlightTarget
    {
        private const string PuzzleSceneName = "ColorTrackPuzzle";
        private const string PuzzleId = "ColorTrackPuzzle";
        private const string InteractionPromptText = "Entrar al puzle de colores";
        private const string SpriteShaderName = "Universal Render Pipeline/2D/Sprite-Unlit-Default";
        private const float HalfExtent = 0.3f;
        private const float HighlightRadius = 0.9f;
        private const float HaloRadiusPadding = 0.09f;
        private const float HaloWidth = 0.035f;
        private const float MinimumHaloAlpha = 0.55f;
        private const float HaloPulseSpeed = 3.2f;
        private const float Zero = 0f;
        private const float One = 1f;
        private const float TwoPi = Mathf.PI * 2f;
        private const int HaloPointCount = 32;
        private const int HaloSortingOrder = 10;
        private const string HaloSortingLayer = "Assets and player";
        private static readonly Color HaloColor = new Color(1f, 0.82f, 0.42f, 1f);

        /// <summary>Maximum query radius required to find colour portals.</summary>
        public const float MaximumInteractionRadius = HighlightRadius;

        private BoxCollider2D interactionCollider;
        private LineRenderer haloRenderer;
        private Material runtimeHaloMaterial;
        private bool isInteractionHighlighted;

        /// <summary>Gets the interaction radius of the colour portal.</summary>
        public float InteractionRadius => HighlightRadius;

        /// <summary>Gets the world-space interaction point at the marker centre.</summary>
        public Vector2 InteractionPoint => transform.position;

        /// <summary>Gets the prompt shown while the player can interact with this marker.</summary>
        public override string InteractionPrompt => InteractionPromptText;

        private void Awake()
        {
            interactionCollider = GetComponent<BoxCollider2D>();
            interactionCollider.isTrigger = true;
            interactionCollider.size = new Vector2(HalfExtent * 2f, HalfExtent * 2f);

            haloRenderer = GetComponent<LineRenderer>();
            ConfigureHalo();
            Shader haloShader = Shader.Find(SpriteShaderName);
            if (haloShader != null)
            {
                runtimeHaloMaterial = new Material(haloShader);
                haloRenderer.material = runtimeHaloMaterial;
            }
            SetInteractionHighlighted(false);

            if (GameManager.Instance != null && GameManager.Instance.IsPuzzleCompleted(PuzzleId))
            {
                DisablePortal();
            }
        }

        private void Update()
        {
            if (!isInteractionHighlighted || haloRenderer == null) return;
            float pulse = MinimumHaloAlpha + (One - MinimumHaloAlpha) * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * HaloPulseSpeed));
            Color pulseColor = new Color(HaloColor.r, HaloColor.g, HaloColor.b, pulse);
            haloRenderer.startColor = pulseColor;
            haloRenderer.endColor = pulseColor;
            if (runtimeHaloMaterial != null) runtimeHaloMaterial.color = pulseColor;
        }

        private void OnDestroy()
        {
            if (runtimeHaloMaterial != null) Destroy(runtimeHaloMaterial);
        }

        /// <summary>Shows or hides the warm circular halo when this is the player's current interactable.</summary>
        public void SetInteractionHighlighted(bool highlighted)
        {
            isInteractionHighlighted = highlighted;
            if (haloRenderer == null) return;
            haloRenderer.enabled = highlighted;
            Color color = highlighted ? HaloColor : Color.clear;
            haloRenderer.startColor = color;
            haloRenderer.endColor = color;
            if (runtimeHaloMaterial != null) runtimeHaloMaterial.color = color;
        }

        /// <summary>Saves the player's position and timeline, then loads the colour-track puzzle.</summary>
        public override void Interact(GameObject interactor)
        {
            if (interactor == null
                || (GameManager.Instance != null && GameManager.Instance.IsPuzzleCompleted(PuzzleId)))
            {
                return;
            }

            TimelineEra returnEra = FindAnyObjectByType<TimeTravelManager>()?.CurrentEra ?? TimelineEra.Present;
            GameManager.Instance?.SetPendingPlayerReturnState(interactor.transform.position, returnEra);
            SceneManager.LoadSceneAsync(PuzzleSceneName, LoadSceneMode.Single);
        }

        private void DisablePortal()
        {
            SetInteractionHighlighted(false);
            interactionCollider.enabled = false;
            enabled = false;
        }

        private void ConfigureHalo()
        {
            haloRenderer.useWorldSpace = false;
            haloRenderer.loop = true;
            haloRenderer.positionCount = HaloPointCount;
            haloRenderer.startWidth = HaloWidth;
            haloRenderer.endWidth = HaloWidth;
            haloRenderer.sortingLayerName = HaloSortingLayer;
            haloRenderer.sortingOrder = HaloSortingOrder;
            haloRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            haloRenderer.receiveShadows = false;
            haloRenderer.enabled = false;

            float radius = HalfExtent + HaloRadiusPadding;
            for (int index = 0; index < HaloPointCount; index++)
            {
                float angle = TwoPi * index / HaloPointCount;
                haloRenderer.SetPosition(index, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
            }
        }
    }
}
