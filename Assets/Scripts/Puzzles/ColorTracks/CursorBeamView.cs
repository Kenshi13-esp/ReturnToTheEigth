using UnityEngine;
using UnityEngine.Rendering;

namespace ReturnToTheEigth.Puzzles
{
    /// <summary>Light-beam cursor: a translucent cell highlight plus a vertical column that pulses and can follow a piece.</summary>
    [DisallowMultipleComponent]
    public sealed class CursorBeamView : MonoBehaviour
    {
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
        private const float DefaultBeamTopY = 1.1f;
        private const float DefaultPulseSpeed = 3f;
        private const float DefaultPulseAmplitude = 0.1f;
        private const float DefaultFollowSmoothing = 20f;
        private const float Half = 0.5f;
        private const float One = 1f;
        private const float Zero = 0f;
        private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");

        [SerializeField] private MeshRenderer cellHighlight;
        [SerializeField] private MeshRenderer beamColumn;
        [SerializeField] private float beamTopY = DefaultBeamTopY;
        [SerializeField] private Color beamColor = new Color(1f, 0.95f, 0.7f, 0.35f);
        [SerializeField] private Color selectedColor = new Color(0.6f, 1f, 0.8f, 0.45f);
        [SerializeField, Min(Zero)] private float pulseSpeed = DefaultPulseSpeed;
        [SerializeField, Range(Zero, One)] private float pulseAmplitude = DefaultPulseAmplitude;
        [SerializeField, Min(Zero)] private float followSmoothing = DefaultFollowSmoothing;
        private Material highlightMaterial;
        private Material columnMaterial;
        private Transform followTarget;
        private bool isSelected;

        private void Awake()
        {
            highlightMaterial = ConfigureTransparent(cellHighlight);
            columnMaterial = ConfigureTransparent(beamColumn);
        }

        private void OnDestroy()
        {
            if (highlightMaterial != null) Destroy(highlightMaterial);
            if (columnMaterial != null) Destroy(columnMaterial);
        }

        /// <summary>Stops following and places the beam on the supplied world XY position immediately.</summary>
        public void MoveTo(Vector2 worldPosition)
        {
            followTarget = null;
            transform.position = new Vector3(worldPosition.x, worldPosition.y, transform.position.z);
        }

        /// <summary>Smoothly follows the target's XY position until <see cref="MoveTo"/> is called or the target is destroyed.</summary>
        public void FollowTarget(Transform target)
        {
            followTarget = target;
        }

        /// <summary>Switches between the idle and the selected beam tint.</summary>
        public void SetSelected(bool selected)
        {
            isSelected = selected;
        }

        private void LateUpdate()
        {
            if (followTarget != null)
            {
                Vector3 current = transform.position;
                Vector3 goal = new Vector3(followTarget.position.x, followTarget.position.y, current.z);
                float blend = One - Mathf.Exp(-followSmoothing * Time.deltaTime);
                transform.position = Vector3.Lerp(current, goal, blend);
            }
            UpdateColumn();
            UpdateTint();
        }

        private void UpdateColumn()
        {
            if (beamColumn == null) return;
            Transform column = beamColumn.transform;
            float height = Mathf.Max(Zero, beamTopY - transform.position.y);
            Vector3 scale = column.localScale;
            column.localScale = new Vector3(scale.x, height, scale.z);
            Vector3 local = column.localPosition;
            column.localPosition = new Vector3(local.x, height * Half, local.z);
        }

        private void UpdateTint()
        {
            Color tint = isSelected ? selectedColor : beamColor;
            float pulse = One + Mathf.Sin(Time.time * pulseSpeed) * pulseAmplitude;
            tint.a = Mathf.Clamp01(tint.a * pulse);
            if (highlightMaterial != null) highlightMaterial.SetColor(BaseColorProperty, tint);
            if (columnMaterial != null) columnMaterial.SetColor(BaseColorProperty, tint);
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
