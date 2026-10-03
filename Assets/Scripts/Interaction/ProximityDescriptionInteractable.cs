using ReturnToTheEigth.Core;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ReturnToTheEigth.Interaction
{
    /// <summary>Provides a proximity-only narrative description without performing an interaction.</summary>
    [RequireComponent(typeof(BoxCollider2D))]
    [DisallowMultipleComponent]
    public sealed class ProximityDescriptionInteractable : InteractableBase, IProximityDescription
    {
        private const string NoPuzzleCompletionGate = "";
        private const string SceneGuideLabelSuffix = " [texto]";
        private static readonly Color SceneGuideColor = new Color(0.2f, 0.85f, 1f, 0.9f);

        [SerializeField, TextArea] private string proximityDescription;
        [SerializeField] private string puzzleIdToHideAfterCompletion = NoPuzzleCompletionGate;

        /// <summary>Gets the narrative description shown while the player is near this object.</summary>
        public string ProximityDescription
        {
            get
            {
                GameManager gameManager = GameManager.Instance;
                if (!string.IsNullOrWhiteSpace(puzzleIdToHideAfterCompletion)
                    && gameManager != null
                    && gameManager.IsPuzzleCompleted(puzzleIdToHideAfterCompletion))
                {
                    return string.Empty;
                }

                return proximityDescription;
            }
        }

        /// <summary>Returns the configured narrative description as this object's interaction prompt.</summary>
        public override string InteractionPrompt => ProximityDescription;

        /// <summary>Does nothing because this object only provides proximity feedback.</summary>
        public override void Interact(GameObject interactor) { }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Camera sceneCamera = Camera.current;
            if (sceneCamera == null || sceneCamera.cameraType != CameraType.SceneView)
            {
                return;
            }

            BoxCollider2D textArea = GetComponent<BoxCollider2D>();
            if (textArea == null)
            {
                return;
            }

            Matrix4x4 previousMatrix = Gizmos.matrix;
            Color previousGizmoColor = Gizmos.color;
            Color previousHandleColor = Handles.color;
            Gizmos.matrix = textArea.transform.localToWorldMatrix;
            Gizmos.color = SceneGuideColor;
            Gizmos.DrawWireCube(textArea.offset, textArea.size);
            Gizmos.matrix = previousMatrix;
            Handles.color = SceneGuideColor;
            Handles.Label(textArea.transform.TransformPoint(textArea.offset),
                gameObject.name + SceneGuideLabelSuffix);
            Gizmos.color = previousGizmoColor;
            Handles.color = previousHandleColor;
        }
#endif
    }
}
