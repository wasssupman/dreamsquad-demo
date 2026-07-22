using System.Collections;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wassup.Core;
using Wassup.Data;
using Wassup.UI.Layout;

namespace Wassup.UI.Draft
{
    // Sub-state machine that orchestrates the unified draft sequence.
    //   Idle → Unrolling → Dwelling → Rolling → Drafting → Confirming → Idle
    // Triggers are: DraftController.DraftStarted (entry), discard×3 (auto
    // confirm via TryConfirm after the 3rd toss completes), DraftConfirmed
    // (cleanup). Re-entrant DraftStarted (rapid Redraft) is guarded by
    // stopping all in-flight tweens and snapping sub-views back to clean state.
    public class DraftView : MonoBehaviour
    {
        public enum State { Idle, Unrolling, Dwelling, Rolling, Drafting, Confirming }

        [SerializeField] private DraftController controller;
        [SerializeField] private WavePatternStripView strip;
        [SerializeField] private DraftCardFanView fan;
        [SerializeField] private MapSettingsPanelView mapSettings;

        [SerializeField] private float dwellSeconds = 2.0f;

        private RectTransform _skillPanel;
        private bool _canvasBuilt;
        private bool _dwellResolved;
        private Coroutine _flow;
        private State _state = State.Idle;

        public State CurrentState => _state;

        private void Awake()
        {
            BuildCanvas();
            HideSubviews();
        }

        private void OnEnable()
        {
            if (controller == null) return;
            controller.DraftStarted += OnDraftStarted;
            controller.DraftConfirmed += OnDraftConfirmed;
            if (strip != null) strip.OnDwellInterrupt += OnDwellInterrupt;
            if (fan != null) fan.CardDiscardRequested += OnCardDiscardRequested;
        }

        private void OnDisable()
        {
            if (controller != null)
            {
                controller.DraftStarted -= OnDraftStarted;
                controller.DraftConfirmed -= OnDraftConfirmed;
            }
            if (strip != null) strip.OnDwellInterrupt -= OnDwellInterrupt;
            if (fan != null) fan.CardDiscardRequested -= OnCardDiscardRequested;
            StopFlow();
            CancelAllTweens();
        }

        private void OnDraftStarted()
        {
            // Re-entrant guard: if a previous sequence is mid-play, snap clean
            // before restarting. Confirming → Idle is the natural terminal so
            // it's safe to ignore.
            if (_state != State.Idle && _state != State.Confirming)
            {
                StopFlow();
                CancelAllTweens();
                if (strip != null) strip.SnapHidden();
                if (fan != null) fan.CancelInProgress();
                _state = State.Idle;
            }
            ShowSubviews();
            RebuildSkillSlots();
            _flow = StartCoroutine(RunFlow());
        }

        private IEnumerator RunFlow()
        {
            _state = State.Unrolling;
            if (strip != null)
            {
                // random-map-pool unit 6 — 선택된 맵의 실전 덱·시드 플랜으로 브리핑(맵마다 정확).
                // bridge 부재/비생성이면 정적 deck 폴백.
                GeneratedWavePlan plan = controller != null ? controller.BuildBriefingWavePlan() : default;
                if (plan.waves != null) strip.RebuildFromPlan(plan);
                else strip.RebuildFromDeck();
            }
            // Fan stays hidden during the strip phase — cards must not peek
            // from below while the wave preview is on screen.
            if (fan != null) fan.gameObject.SetActive(false);

            if (strip != null) yield return strip.Unroll().ToYieldInstruction();

            _state = State.Dwelling;
            _dwellResolved = false;
            float t0 = Time.unscaledTime;
            while (!_dwellResolved && Time.unscaledTime - t0 < dwellSeconds) yield return null;

            _state = State.Rolling;
            if (strip != null) yield return strip.Roll().ToYieldInstruction();

            _state = State.Drafting;
            if (fan != null)
            {
                fan.gameObject.SetActive(true);
                fan.Build(controller.Session.Pool, controller.Session);
                yield return fan.PlayEnterSequence().ToYieldInstruction();
            }
            // Drafting state continues until 3 cards discarded.
        }

        private void OnDwellInterrupt()
        {
            if (_state == State.Dwelling) _dwellResolved = true;
        }

        private void OnCardDiscardRequested(DraftCardView card)
        {
            if (_state != State.Drafting) return;
            if (controller == null || card == null) return;
            if (!controller.ToggleDiscard(card.Unit))
            {
                // Cap exceeded — snap card home (DraftCardView already handles
                // sub-threshold drag complementarily via OutBack). Defensive only.
                Tween.UIAnchoredPosition(card.Rect, card.HomePosition, 0.25f, Ease.OutBack);
                return;
            }
            bool isLast = controller.Session.IsFull;
            var tossSeq = fan != null ? fan.PlayDiscardCard(card) : Sequence.Create();
            if (fan != null) fan.LayoutRemaining();
            if (isLast)
            {
                _state = State.Confirming;
                tossSeq.OnComplete(() =>
                {
                    if (controller != null) controller.TryConfirm();
                });
            }
        }

        private void OnDraftConfirmed()
        {
            CancelAllTweens();
            HideSubviews();
            _state = State.Idle;
        }

        private void StopFlow()
        {
            if (_flow != null) { StopCoroutine(_flow); _flow = null; }
        }

        private void CancelAllTweens()
        {
            Tween.StopAll(this);
            if (strip != null) Tween.StopAll(strip);
            if (fan != null) Tween.StopAll(fan);
        }

        private void ShowSubviews()
        {
            if (strip != null) strip.gameObject.SetActive(true);
            // Fan is activated by RunFlow only after the strip Roll completes.
            if (fan != null) fan.gameObject.SetActive(false);
            if (mapSettings != null) mapSettings.gameObject.SetActive(true);
            if (_skillPanel != null) _skillPanel.gameObject.SetActive(true);
        }

        private void HideSubviews()
        {
            if (strip != null) { strip.SnapHidden(); strip.gameObject.SetActive(false); }
            if (fan != null) fan.gameObject.SetActive(false);
            if (mapSettings != null) mapSettings.gameObject.SetActive(false);
            if (_skillPanel != null) _skillPanel.gameObject.SetActive(false);
        }

        // --- Canvas + skill panel build (legacy carry-over; spec keeps current visuals) ---

        private void BuildCanvas()
        {
            if (_canvasBuilt) return;
            _canvasBuilt = true;

            var roots = UiCanvasSetup.Ensure(gameObject, sortingOrder: 5);

            BuildSkillPanel(roots.SafeAreaRoot);
            if (mapSettings != null) mapSettings.Initialize(controller);

            UiLayer.Apply(gameObject);
        }

        private void BuildSkillPanel(RectTransform safeAreaRoot)
        {
            var panelGO = new GameObject("SkillLoadoutPanel", typeof(RectTransform), typeof(Image));
            panelGO.transform.SetParent(safeAreaRoot, false);
            _skillPanel = (RectTransform)panelGO.transform;
            _skillPanel.anchorMin = new Vector2(1f, 0.5f);
            _skillPanel.anchorMax = new Vector2(1f, 0.5f);
            _skillPanel.pivot = new Vector2(1f, 0.5f);
            _skillPanel.anchoredPosition = new Vector2(-40f, 40f);
            _skillPanel.sizeDelta = new Vector2(240f, 360f);
            panelGO.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.15f, 0.9f);

            var titleTmp = CreateLabel(_skillPanel, "Title", "이번 라운드\n스킬",
                fontSize: 20, alignment: TextAlignmentOptions.Center);
            var titleRt = (RectTransform)titleTmp.transform;
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -10f);
            titleRt.sizeDelta = new Vector2(0f, 60f);

            var col = new GameObject("SlotColumn", typeof(RectTransform), typeof(VerticalLayoutGroup));
            col.transform.SetParent(_skillPanel, false);
            var crt = (RectTransform)col.transform;
            crt.anchorMin = new Vector2(0f, 0f);
            crt.anchorMax = new Vector2(1f, 1f);
            crt.offsetMin = new Vector2(12f, 20f);
            crt.offsetMax = new Vector2(-12f, -76f);
            var vlg = col.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 10f;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = true;
            vlg.childAlignment = TextAnchor.UpperCenter;
        }

        private void RebuildSkillSlots()
        {
            if (_skillPanel == null) return;
            var column = _skillPanel.Find("SlotColumn");
            if (column == null) return;

            for (int i = column.childCount - 1; i >= 0; i--)
                Destroy(column.GetChild(i).gameObject);

            var loadout = GameManager.Instance != null ? GameManager.Instance.SkillLoadout : null;
            if (loadout == null) return;
            var picked = loadout.Picked;
            for (int i = 0; i < picked.Count; i++)
            {
                var skill = picked[i];
                if (skill == null) continue;
                var slot = new GameObject($"Slot_{skill.id}", typeof(RectTransform), typeof(Image));
                slot.transform.SetParent(column, false);
                slot.GetComponent<Image>().color = skill.uiTint;

                var nameTmp = CreateLabel(slot.transform, "Name", skill.displayName,
                    fontSize: 22, alignment: TextAlignmentOptions.Center);
                var nrt = (RectTransform)nameTmp.transform;
                nrt.anchorMin = new Vector2(0f, 0.4f);
                nrt.anchorMax = new Vector2(1f, 1f);
                nrt.offsetMin = new Vector2(4f, 0f);
                nrt.offsetMax = new Vector2(-4f, -4f);

                var costTmp = CreateLabel(slot.transform, "Cost", $"코스트 {skill.cost}",
                    fontSize: 18, alignment: TextAlignmentOptions.Center);
                var rt = (RectTransform)costTmp.transform;
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(1f, 0.4f);
                rt.offsetMin = new Vector2(4f, 4f);
                rt.offsetMax = new Vector2(-4f, 0f);
            }

            UiLayer.Apply(gameObject);
        }

        private static TextMeshProUGUI CreateLabel(Transform parent, string name, string text,
            int fontSize, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = Color.white;
            tmp.alignment = alignment;
            tmp.enableWordWrapping = true;
            return tmp;
        }
    }
}
