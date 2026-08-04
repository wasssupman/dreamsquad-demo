using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wassup.Core;
using Wassup.Core.Session;
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
        private bool _built;
        private bool _open;
        // unit 13-C — 정지 lease 를 이 뷰가 직접 들지 않는다. `SetPaused` 는 커맨드 자격이 있는
        // 유일한 시간 제어(청사진 ① §2)이고 lease 소유는 세션으로 갔다. 어댑터가 쓰는 파라미터는
        // 여기 있던 것과 동일하다(`TimeDomain.Battle, 0f, priority: 100`) — 행동 변화 0.
        // teardown 안전망도 세션이 갖는다: 어댑터 `Dispose` 가 lease 를 반납하고, 매치 경계의
        // `TimeManager.ResetAll` 이 이중 안전망이다.

        public bool IsOpen => _open;

        private void Awake()
        {
            BuildCanvas();
            if (_root != null) _root.SetActive(false);
        }

        private void OnDisable()
        {
            // Safety: never leave the battle paused if this object is torn down while open.
            // 세션이 이미 사라진 순서라도 어댑터 Dispose 가 lease 를 반납하므로 정지가 남지 않는다.
            if (_open) { SendPaused(false); _open = false; }
        }

        private static void SendPaused(bool paused)
            => MatchSession.Send(seq => MatchCommand.SetPaused(seq, paused));

        public void Open()
        {
            if (_open) return;               // idempotent
            if (!_built) BuildCanvas();
            _open = true;
            SendPaused(true);
            if (_root != null) _root.SetActive(true);
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
            SendPaused(false);               // 어댑터가 `_paused` 로 멱등을 보장한다
            if (wavePatternStrip != null)
            {
                wavePatternStrip.Roll();
                wavePatternStrip.SetSortingOverride(false);
            }
            if (_root != null) _root.SetActive(false);
        }

        private void OnResume() => Close();

        private void OnExit()
        {
            // Release the pause before leaving; the scene teardown + TimeManager.ResetAll
            // at the match boundary also clears it, so double-release is harmless.
            if (_open) { SendPaused(false); _open = false; }
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
