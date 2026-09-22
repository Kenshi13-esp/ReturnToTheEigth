using UnityEngine;
using UnityEngine.Rendering;

namespace ReturnToTheEigth.Puzzles
{
    /// <summary>Translucent pulsing light on a candidate cell. All highlights look identical so the correct one is never revealed.</summary>
    [DisallowMultipleComponent]
    public sealed class PlacementHighlight : MonoBehaviour
    {
        private const string MissingBoardWarning = "PlacementHighlight could not find a KnightPlacementBoard in the scene.";
        private const string SurfaceProperty = "_Surface";
        private const string BlendProperty = "_Blend";
        private const string SrcBlendProperty = "_SrcBlend";
        private const string DstBlendProperty = "_DstBlend";
        private const string SrcBlendAlphaProperty = "_SrcBlendAlpha";
        private const string DstBlendAlphaProperty = "_DstBlendAlpha";
        private const string ZWriteProperty = "_ZWrite";
        private const string RenderTypeTag = "RenderType";
        private const string TransparentTag = "Transparent";
        private const string TransparentKeyword = "_SURFACE_TYPE_TRANSPARENT";
        private const string PremultiplyKeyword = "_ALPHAPREMULTIPLY_ON";
        private const float TransparentSurface = 1f;
        private const float AlphaBlend = 0f;
        private const float ZWriteOff = 0f;
        private const float DefaultPulseSpeed = 3f;
        private const float DefaultPulseAmplitude = 0.15f;
        private const float One = 1f;
        private const float Zero = 0f;
        private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");

        [SerializeField] private KnightPlacementBoard board;
        [SerializeField] private PuzzleGrid grid;
        [SerializeField] private Vector2Int cell;
        [SerializeField] private MeshRenderer highlightRenderer;
        [SerializeField] private Color highlightColor = new Color(1f, 0.95f, 0.6f, 0.45f);
        [SerializeField, Min(Zero)] private float pulseSpeed = DefaultPulseSpeed;
        [SerializeField, Range(Zero, One)] private float pulseAmplitude = DefaultPulseAmplitude;
        private Material highlightMaterial;

        public Vector2Int Cell => cell;

        private void Awake()
        {
            if (board == null) board = FindAnyObjectByType<KnightPlacementBoard>();
            if (board == null) Debug.LogWarning(MissingBoardWarning, this);
            if (grid == null) grid = board != null ? board.Grid : FindAnyObjectByType<PuzzleGrid>();
            if (highlightRenderer == null) highlightRenderer = GetComponentInChildren<MeshRenderer>();
            highlightMaterial = ConfigureTransparent(highlightRenderer);
            if (grid != null)
            {
                Vector2 center = grid.CellToWorld(cell);
                transform.position = new Vector3(center.x, center.y, transform.position.z);
            }
        }

        private void OnEnable()
        {
            if (board != null) board.Register(this);
        }

        private void OnDisable()
        {
            if (board != null) board.Unregister(this);
        }

        private void OnDestroy()
        {
            if (highlightMaterial != null) Destroy(highlightMaterial);
        }

        private void Update()
        {
            if (highlightMaterial == null) return;
            Color tint = highlightColor;
            float pulse = One + Mathf.Sin(Time.time * pulseSpeed) * pulseAmplitude;
            tint.a = Mathf.Clamp01(tint.a * pulse);
            highlightMaterial.SetColor(BaseColorProperty, tint);
        }

        /// <summary>Shows or hides the light without unregistering the cell from the board.</summary>
        public void SetVisible(bool visible)
        {
            if (highlightRenderer != null) highlightRenderer.enabled = visible;
        }

        private static Material ConfigureTransparent(MeshRenderer renderer)
        {
            if (renderer == null) return null;
            Material material = renderer.material;
            if (material.HasProperty(SurfaceProperty)) material.SetFloat(SurfaceProperty, TransparentSurface);
            if (material.HasProperty(BlendProperty)) material.SetFloat(BlendProperty, AlphaBlend);
            if (material.HasProperty(SrcBlendProperty)) material.SetFloat(SrcBlendProperty, (float)BlendMode.SrcAlpha);
            if (material.HasProperty(DstBlendProperty)) material.SetFloat(DstBlendProperty, (float)BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty(SrcBlendAlphaProperty)) material.SetFloat(SrcBlendAlphaProperty, (float)BlendMode.One);
            if (material.HasProperty(DstBlendAlphaProperty)) material.SetFloat(DstBlendAlphaProperty, (float)BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty(ZWriteProperty)) material.SetFloat(ZWriteProperty, ZWriteOff);
            material.SetOverrideTag(RenderTypeTag, TransparentTag);
            material.EnableKeyword(TransparentKeyword);
            material.DisableKeyword(PremultiplyKeyword);
            material.renderQueue = (int)RenderQueue.Transparent;
            return material;
        }
    }
}
