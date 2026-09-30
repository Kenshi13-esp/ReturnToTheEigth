using UnityEngine;

namespace ReturnToTheEigth.UI
{
    /// <summary>Opens a configurable external URL when invoked by a UI button.</summary>
    [DisallowMultipleComponent]
    public sealed class ExternalLinkButton : MonoBehaviour
    {
        [SerializeField] private string url = "";

        /// <summary>Opens the configured URL. Assign the destination in the Inspector before release.</summary>
        public void OpenUrl()
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                Debug.LogWarning("ExternalLinkButton needs a URL before it can open a page.", this);
                return;
            }

            Application.OpenURL(url);
        }
    }
}
