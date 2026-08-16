using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wassup.Core;
using Wassup.Core.TimeControl;
using Wassup.Data;
using Wassup.UI.Layout;

namespace Wassup.UI
{
    // battle-hud-score-timer-menu Unit 2 — pause-style menu popup. The MENU button no
    // longer exits straight to the outgame scene; it opens this popup, which pauses the
    // battle (TimeManager, Battle domain) and offers [재개] resume / [나가기] exit.
    // The incoming-wave (attack pattern) strip is driven from here in Unit 3.
    public class MenuPopup : MonoBehaviour
    {
        [Header("Korean-capable font (Jua SDF). Null -> TMP default.")]
        [SerializeField] private TMP_FontAsset buttonFont;

        [Header("Attack-pattern strip (drives FadeIn/Roll + sorting boost)")]
        [SerializeField] private Wassup.UI.Draft.WavePatternStripView wavePatternStrip;

        // Buttons sit above the boosted strip; the strip's own dim overlay is the
        // backdrop, so this popup adds no dim of its own (avoids double-dimming).
        private const int StripSortingOrder = 950;
        private const int PopupSortingOrder = 960;

        private GameObject _root;
        private Button _resumeButton;
        private Button _exitButton;
        private TextMeshProUGUI _exitLabel;
        private Image _exitImage;
        private bool _submitMode;
        private Wassup.Bridge.BattleBridge _bridge;
        private bool _built;
        private bool _open;
        private TimeLease _pauseLease;

        public bool IsOpen => _open;

        private void Awake()
        {
            BuildCanvas();
            if (_root != null) _root.SetActive(false);
        }

        private void OnDisable()
        {
            // Safety: never leave the battle paused if this object is torn down while open.
            if (_open) { _pauseLease.Dispose(); _open = false; }
        }

        public void Open()
        {
            if (_open) return;               // idempotent
            if (!_built) BuildCanvas();
            _open = true;
            _pauseLease = TimeManager.Instance.Request(TimeDomain.Battle, 0f, priority: 100);
            if (_root != null) _root.SetActive(true);
            RefreshExitButton();
            if (wavePatternStrip != null)
            {
                // random-map-pool unit 7 — 표시 직전에 이번 판(선택된 맵)의 실전 웨이브 플랜으로
                // 재빌드한다. SquadPrepView 가 준비해 둔 정적 deck(WaveA) 카드를 덮어써, 랜덤으로
                // 뽑힌 맵의 덱(예: TwinLane→WaveB)을 정확히 예고한다. bridge 부재면 기존 카드 유지.
                GeneratedWavePlan plan = GameManager.Instance != null
                    ? GameManager.Instance.BuildBriefingWavePlan()
                    : default;
                if (plan.waves != null) wavePatternStrip.RebuildFromPlan(plan);
                // Lift the strip above this popup so its own dim overlay reads as the
                // backdrop (no double-dim), then reveal it. Buttons sit on a higher
                // canvas (see BuildCanvas) so they stay above the strip.
                wavePatternStrip.SetSortingOverride(true, StripSortingOrder);
                wavePatternStrip.FadeIn();
            }
        }

        public void Close()
        {
            if (!_open) return;
            _open = false;
            _pauseLease.Dispose();           // idempotent no-op on double dispose
            if (wavePatternStrip != null)
            {
                wavePatternStrip.Roll();
                wavePatternStrip.SetSortingOverride(false);
            }
            if (_root != null) _root.SetActive(false);
        }

        private void OnResume() => Close();

        // three-minute-kill-race unit 3 — **버튼 하나가 P1 을 기점으로 정체를 바꾼다.**
        // 여는 순간 한 번만 판정하면 되는 이유: 팝업이 열려 있는 동안 전투는 멈춰 있어
        // (Open 이 Battle 도메인 0배속 lease 를 잡는다) 시계가 안 흐른다 — 열린 채로
        // 정체가 바뀌는 일이 구조적으로 없다.
        //
        // 「포기」류 어휘를 제출 버튼에 쓰지 않는다(사용자 확정). 0점 포기는 제출이
        // 불가능한 60초 동안에만 존재하고, 그 뒤로는 사라진다.
        private void RefreshExitButton()
        {
            // 브리지 참조는 여기서 찾는다(의도된 예외 — spec unit 3 §4). 원칙은 SerializeField
            // 배선이지만 BattleScene 이 다른 세션의 미커밋 WIP 를 물고 있어 지금 저장하면
            // 그 작업이 통째로 박힌다. 팝업은 드물게 열리므로 1회 탐색이 무해하다.
            if (_bridge == null) _bridge = FindFirstObjectByType<Wassup.Bridge.BattleBridge>();
            _submitMode = _bridge != null && _bridge.CanSubmit;

            if (_exitLabel != null) _exitLabel.text = _submitMode ? "제출" : "나가기";
            if (_exitImage != null)
                _exitImage.color = _submitMode
                    ? new Color(0.16f, 0.42f, 0.62f, 0.96f)   // 제출 = 확정. 경고색이 아니다
                    : new Color(0.6f, 0.2f, 0.2f, 0.96f);     // 나가기 = 0점 포기
        }

        private void OnExit()
        {
            // unit 3 — 제출이 열렸으면 이 버튼은 **성적 확정**이다: 현재 킬로 판을 끝내고
            // 결과 화면으로 간다(로비 직행 아님 — 확정해놓고 리더보드를 못 보면 안 된다).
            if (_submitMode && _bridge != null)
            {
                Close();                 // 일시정지 lease 를 놓아야 마감이 정상 진행된다
                _bridge.SubmitMatch();
                return;
            }

            // Release the pause before leaving; the scene teardown + TimeManager.ResetAll
            // at the match boundary also clears it, so double-release is harmless.
            if (_open) { _pauseLease.Dispose(); _open = false; }
            // abandoned-match-reconciliation unit 2 — exiting a live battle abandons
            // the tournament attempt: submit a 0 now while the app is still alive.
            // tournament-deck-info unit 4 — 덱도 같이 싣는다. 씬이 아직 살아 있어 로거가
            // 이 attempt 의 덱을 그대로 준다(세션 부재면 null → 키 생략).
            Wassup.Core.Api.TournamentMatchReporter.AbandonMatch(
                GameManager.Instance?.Logger?.DeckInfoJson());
            // outgame-tutorial unit 8 rev — 나가기로 끝낸 판도 히스토리에 자기 엔트리를 남기므로
            // (바로 위 AbandonMatch) "플레이한 판" 으로 센다. 로비 챕터 D 가 가르치는 게 그
            // 히스토리라, 카운터와 안내가 같은 것을 세야 한다.
            // **씬 전환 앞이어야 한다** — 전환 뒤엔 GameManager 가 이미 파괴돼 기록이 유실된다.
            GameManager.Instance?.RecordMatchPlayed();
            SceneTransition.Go(SceneNames.Outgame);
        }

        private void BuildCanvas()
        {
            if (_built) return;
            _built = true;

            var roots = UiCanvasSetup.Ensure(gameObject, sortingOrder: PopupSortingOrder);

            _root = new GameObject("PopupRoot", typeof(RectTransform));
            _root.transform.SetParent(roots.SafeAreaRoot, false);
            var rrt = (RectTransform)_root.transform;
            rrt.anchorMin = Vector2.zero;
            rrt.anchorMax = Vector2.one;
            rrt.offsetMin = Vector2.zero;
            rrt.offsetMax = Vector2.zero;

            // No dim backdrop here — the strip's own full-screen overlay (boosted above
            // this canvas' gameplay layers) provides the dim and blocks gameplay input.
            // Buttons live at the bottom, clear of the upper-third strip (no overlap).
            _resumeButton = MakeButton("ResumeButton", "재개", new Vector2(-150f, 120f),
                new Color(0.16f, 0.5f, 0.28f, 0.96f), OnResume);
            _exitButton = MakeButton("ExitButton", "나가기", new Vector2(150f, 120f),
                new Color(0.6f, 0.2f, 0.2f, 0.96f), OnExit);
            _exitImage = _exitButton.GetComponent<Image>();
            _exitLabel = _exitButton.GetComponentInChildren<TextMeshProUGUI>();

            UiLayer.Apply(gameObject);
        }

        private Button MakeButton(string name, string label, Vector2 bottomAnchoredPos, Color color, Action onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(_root.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = bottomAnchoredPos;
            rt.sizeDelta = new Vector2(260f, 96f);
            go.GetComponent<Image>().color = color;
            var btn = go.GetComponent<Button>();
            btn.onClick.AddListener(() => onClick());

            var labelGO = new GameObject("Label", typeof(RectTransform));
            labelGO.transform.SetParent(go.transform, false);
            var lrt = (RectTransform)labelGO.transform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
            var tmp = labelGO.AddComponent<TextMeshProUGUI>();
            if (buttonFont != null) tmp.font = buttonFont;
            tmp.text = label;
            tmp.fontSize = 40;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            return btn;
        }
    }
}
