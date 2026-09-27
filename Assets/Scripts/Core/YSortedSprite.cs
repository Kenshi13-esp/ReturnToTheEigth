using UnityEngine;

namespace ReturnToTheEigth.Core
{
    /// <summary>Continuously reorders a SpriteRenderer within its sorting layer so lower world-Y positions draw in front, giving correct top-down depth between movable sprites (e.g. player vs. pushable boxes).</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class YSortedSprite : MonoBehaviour
    {
        private const int OrderPerUnit = 100;

        [SerializeField] private Transform positionSource;
        [SerializeField] private int orderOffset;
        private SpriteRenderer spriteRenderer;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (positionSource == null) positionSource = transform;
        }

        private void LateUpdate()
        {
            spriteRenderer.sortingOrder = orderOffset - Mathf.RoundToInt(positionSource.position.y * OrderPerUnit);
        }
    }
}
