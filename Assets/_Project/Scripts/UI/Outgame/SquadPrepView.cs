using UnityEngine;
using UnityEngine.UI;
using Wassup.Core;
using Wassup.UI.Draft;

namespace Wassup.UI
{
    // squad map-setup — pre-placement step for squad mode.
    // prep-attack-pattern-flow: on entry the attack-pattern preview auto-unrolls,
    // holds ~1s, rolls away, and then the flow auto-advances to placement (no START
    // gate). Map settings and the wave preview each keep their own toggle button
    // (top-left), staying reachable through placement and battle.
    public class SquadPrepView : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;
        [SerializeField] private DraftController draftController;
        // Self-contained toggle (its own "MAP SETTINGS" button + collapsible panel).
        // Kept active past entry so the player can adjust the map during placement.
        [SerializeField] private MapSettingsPanelView mapSettings;
        // Attack-pattern preview (the draft stage's wave strip). Has its own "!"
        // toggle button; kept active so it can be re-opened anytime.
        [SerializeField] private WavePatternStripView wavePatternStrip;
        // prep-attack-pattern-flow Unit 0 — how long the auto-intro holds the wave
        // strip on screen before rolling it away.
        [SerializeField] private float introDwellSec = 1f;

        private bool _built;
        private bool _advanced;
        private Coroutine _introRoutine;

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
                wavePatternStrip.gameObject.SetActive(true);
                wavePatternStrip.RebuildFromDeck();
                wavePatternStrip.SnapHidden();
                wavePatternStrip.SetToggleEnabled(true);
                if (_introRoutine != null) StopCoroutine(_introRoutine);
                _introRoutine = StartCoroutine(PlayIntro());
            }
            else
            {
                // Headless / no strip wired: advance immediately.
                AdvanceToPlacement();
            }
        }

        // Auto-intro: unroll (wait until fully shown) → hold ~1s → roll away (wait
        // until fully hidden) → auto-advance to placement. timeScale stays 1 the
        // whole time, so both animations play out before the dreamcatcher modal
        // pauses time — the strip is gone before the picker appears.
        private System.Collections.IEnumerator PlayIntro()
        {
            wavePatternStrip.Unroll();
            // Unroll's staggered card animation takes >1s; wait for it to finish
            // before starting the hold, otherwise we'd advance mid-unroll.
            while (wavePatternStrip.CurrentState == WavePatternStripView.State.Unrolling)
                yield return null;

            yield return new WaitForSecondsRealtime(introDwellSec);

            // Skip auto-roll if the player already closed it via toggle.
            if (wavePatternStrip.CurrentState == WavePatternStripView.State.Shown)
            {
                wavePatternStrip.Roll();
                while (wavePatternStrip.CurrentState == WavePatternStripView.State.Rolling)
                    yield return null;
            }

            _introRoutine = null;
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
