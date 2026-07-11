using UnityEngine;

namespace Wassup.UI.Layout
{
    /// <summary>
    /// Keeps a full-bleed modal scrim in sync with a safe-area content panel.
    /// The scrim is a sibling under FullBleedRoot, so display cutouts never reveal
    /// an undimmed strip while the content panel remains inset under SafeAreaRoot.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UiFullBleedModalDim : MonoBehaviour
    {
        [SerializeField] private GameObject dim;

        public void Bind(GameObject value)
        {
            dim = value;
            Sync();
        }

        private void OnEnable() => Sync();

        private void OnDisable()
        {
            if (dim != null) dim.SetActive(false);
        }

        private void OnDestroy()
        {
            if (dim != null) dim.SetActive(false);
        }

        private void Sync()
        {
            if (dim != null) dim.SetActive(isActiveAndEnabled);
        }
    }
}
