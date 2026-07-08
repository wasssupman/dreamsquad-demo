using UnityEngine;
using UnityEngine.UI;
using Wassup.Core;
using Wassup.UI.Draft;

namespace Wassup.UI
{
    // squad map-setup — pre-placement step for squad mode.
    // prep-attack-pattern-flow Unit 3: on entry the flow advances straight to
    // placement (dreamcatcher pick) WITHOUT force-showing the attack pattern. The
    // wave strip is kept active but hidden with its "!" toggle enabled, and the map
    // settings keep their own toggle — both reachable through placement and battle.
    // (Unit 0's auto-intro Unroll→dwell→Roll is retired.)
    public class SquadPrepView : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;
        [SerializeField] private DraftController draftController;
        // Self-contained toggle (its own "MAP SETTINGS" button + collapsible panel).
        // Kept active past entry so the player can adjust the map during placement.
        [SerializeField] private MapSettingsPanelView mapSettings;
        // Attack-pattern preview (the draft stage's wave strip). Has its own "!"
        // toggle button; kept active so it can be opened anytime.
        [SerializeField] private WavePatternStripView wavePatternStrip;

        private bool _built;
        private bool _advanced;

        private void OnEnable()
        {
            if (gameManager != null) gameManager.MapSetupRequested += OnMapSetupRequested;
        }

        private void OnDisable()
        {
            if (gameManager != null) gameManager.MapSetupRequested -= OnMapSetupRequested;
        }

        private void OnMapSetupRequested()
        {
            if (!_built) EnsureCanvas();
            _advanced = false;

            if (mapSettings != null)
            {
                // Active with its own toggle; panel starts collapsed (Build hides it).
                mapSettings.Initialize(draftController);
                mapSettings.gameObject.SetActive(true);
            }

            if (wavePatternStrip != null)
            {
                // Keep the strip active but hidden with its "!" toggle live — the
                // attack pattern is now viewable only on demand, never force-shown.
                wavePatternStrip.gameObject.SetActive(true);
                wavePatternStrip.RebuildFromDeck();
                wavePatternStrip.SnapHidden();
                wavePatternStrip.SetToggleEnabled(true);
            }

            // No auto-intro: advance straight to placement (dreamcatcher pick).
            AdvanceToPlacement();
        }

        private void AdvanceToPlacement()
        {
            if (_advanced) return;
            _advanced = true;
            if (gameManager != null) gameManager.RequestPlacement();
        }

        // Just the Canvas host for the map-settings + wave-strip children. No screen
        // chrome of its own anymore (START gate removed — flow auto-advances).
        private void EnsureCanvas()
        {
            if (_built) return;
            _built = true;

            var canvas = gameObject.GetComponent<Canvas>();
            if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 8;
            if (gameObject.GetComponent<CanvasScaler>() == null)
            {
                var scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
            }
            if (gameObject.GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>();
        }
    }
}
