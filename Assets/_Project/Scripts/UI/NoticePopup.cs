using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wassup.UI.Layout;

namespace Wassup.UI
{
    // tournament-flow-guards unit 0 — 공용 알림 팝업.
    //
    // 두 모드를 한 컴포넌트로: busy("매칭 중" 점 애니메이션, 논-디스미스)와
    // alert(제목+메시지+[다시 시도]/[닫기]). play 게이팅(unit 1)과 score 실패
    // 알림(unit 2)이 공유하며, 정적 TournamentMatchReporter 콜백에서도 불러야 하므로
    // DontDestroyOnLoad 단일 인스턴스 + 정적 API 로 둔다(SceneTransition 선례).
    //
    // 부트스트랩 부재/헤드리스(EditMode)에서는 Instance==null 이라 정적 메서드가 no-op
    // (SceneTransition 의 null-instance 하드컷과 같은 degrade 규약).
    //
    // EventSystem 은 씬 것을 쓴다(로비·배틀 모두 보유) — Input System UI 모듈 의존을 피하려
    // 자체 생성하지 않는다.
    public class NoticePopup : MonoBehaviour
    {
        private const int SortingOrder = 3000; // ResultScreen(2000) 위

        // 팔레트/사이즈 — HUD 언어와 맞춘 시각 상수(튜닝 노브 아님).
        private static readonly Color PanelBg = new Color(0.07f, 0.08f, 0.12f, 0.98f);
        private static readonly Color Gold = new Color(1f, 0.78f, 0.28f, 1f);
        private static readonly Color Neutral = new Color(0.28f, 0.30f, 0.36f, 1f);
        private static readonly Color BtnTextDark = new Color(0.10f, 0.09f, 0.06f, 1f);
        private const float PanelW = 860f;
        private const float PanelH = 440f;

        public static NoticePopup Instance { get; private set; }

        private Image _dim;
        private RectTransform _panel;
        private TextMeshProUGUI _title;
        private TextMeshProUGUI _message;
        private Button _retryButton;
        private Button _closeButton;
        private Image _retryImg;
        private Image _closeImg;
        private TextMeshProUGUI _retryLabel;
        private TextMeshProUGUI _closeLabel;
        private RectTransform _retryRt;
        private RectTransform _closeRt;
        private bool _built;

        private bool _busy;
        private string _busyBase;
        private float _busyT;
        private Action _onRetry;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("NoticePopup");
            go.AddComponent<NoticePopup>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Build();
            gameObject.SetActive(false); // rest = 숨김
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── 정적 API ─────────────────────────────────────────────────────────
        // 매칭/입장 대기 등 무응답 방어용 논-디스미스 오버레이.
        public static void ShowBusy(string message)
        {
            if (Instance == null) { Debug.LogWarning("[NoticePopup] 인스턴스 없음 — ShowBusy 무시."); return; }
            Instance.ShowBusyInternal(message);
        }

        // 실패 안내. onRetry != null 이면 [다시 시도] 노출.
        public static void ShowAlert(string title, string message, Action onRetry = null)
        {
            if (Instance == null) { Debug.LogWarning("[NoticePopup] 인스턴스 없음 — ShowAlert 무시."); return; }
            Instance.ShowAlertInternal(title, message, onRetry);
        }

        public static void Hide()
        {
            if (Instance == null) return;
            Instance.HideInternal();
        }

        public static bool IsShowing => Instance != null && Instance.gameObject.activeSelf;

        // ── 인스턴스 로직 ────────────────────────────────────────────────────
        private void ShowBusyInternal(string message)
        {
            if (!_built) Build();
            _busy = true;
            _busyBase = message ?? string.Empty;
            _busyT = 0f;
            _onRetry = null;

            _title.gameObject.SetActive(false);
            _message.gameObject.SetActive(true);
            _message.text = _busyBase;
            _retryButton.gameObject.SetActive(false);
            _closeButton.gameObject.SetActive(false);
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        private void ShowAlertInternal(string title, string message, Action onRetry)
        {
            if (!_built) Build();
            _busy = false;
            _onRetry = onRetry;

            bool hasTitle = !string.IsNullOrEmpty(title);
            _title.gameObject.SetActive(hasTitle);
            _title.text = title ?? string.Empty;
            _message.gameObject.SetActive(true);
            _message.text = message ?? string.Empty;

            bool retry = onRetry != null;
            _retryButton.gameObject.SetActive(retry);
            _closeButton.gameObject.SetActive(true);
            // 주 액션 = gold: 재시도 있으면 재시도가 주 액션, 없으면 닫기가 주 액션.
            StyleButton(_retryImg, _retryLabel, primary: true);
            StyleButton(_closeImg, _closeLabel, primary: !retry);
            LayoutButtons(retry);

            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        private void HideInternal()
        {
            _busy = false;
            _onRetry = null;
            gameObject.SetActive(false);
        }

        private void OnClose() => HideInternal();

        private void OnRetry()
        {
            var cb = _onRetry;
            HideInternal();
            cb?.Invoke();
        }

        private void Update()
        {
            if (!_busy || _message == null) return;
            _busyT += Time.unscaledDeltaTime;
            int dots = (int)(_busyT * 2f) % 4; // 0..3, 0.5s 간격
            _message.text = _busyBase + new string('.', dots);
        }

        private static void StyleButton(Image img, TextMeshProUGUI label, bool primary)
        {
            if (img != null) img.color = primary ? Gold : Neutral;
            if (label != null) label.color = primary ? BtnTextDark : Color.white;
        }

        // 버튼 배치: [다시 시도][닫기] 또는 [닫기] 단독(중앙).
        private void LayoutButtons(bool retry)
        {
            if (retry)
            {
                _retryRt.anchoredPosition = new Vector2(-150f, 54f);
                _closeRt.anchoredPosition = new Vector2(150f, 54f);
            }
            else
            {
                _closeRt.anchoredPosition = new Vector2(0f, 54f);
            }
        }

        // ── Build ────────────────────────────────────────────────────────────
        private void Build()
        {
            if (_built) return;
            _built = true;

            var roots = UiCanvasSetup.Ensure(gameObject, sortingOrder: SortingOrder);

            // 전체화면 dim(입력 차단, 탭으로 안 닫힘 — 버튼 전용).
            var dimGo = new GameObject("Dim", typeof(RectTransform), typeof(Image));
            dimGo.transform.SetParent(roots.FullBleedRoot, false);
            var drt = (RectTransform)dimGo.transform;
            drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one;
            drt.offsetMin = Vector2.zero; drt.offsetMax = Vector2.zero;
            _dim = dimGo.GetComponent<Image>();
            _dim.color = UiOverlay.Dim;

            // 중앙 패널.
            var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panelGo.transform.SetParent(roots.SafeAreaRoot, false);
            _panel = (RectTransform)panelGo.transform;
            _panel.anchorMin = _panel.anchorMax = _panel.pivot = new Vector2(0.5f, 0.5f);
            _panel.sizeDelta = new Vector2(PanelW, PanelH);
            panelGo.GetComponent<Image>().color = PanelBg;

            _title = CreateLabel(_panel, "Title", "", 46, TextAlignmentOptions.Center, Gold, FontStyles.Bold);
            var trt = (RectTransform)_title.transform;
            trt.anchorMin = new Vector2(0f, 1f); trt.anchorMax = new Vector2(1f, 1f);
            trt.pivot = new Vector2(0.5f, 1f);
            trt.offsetMin = new Vector2(40f, 0f); trt.offsetMax = new Vector2(-40f, 0f);
            trt.sizeDelta = new Vector2(trt.sizeDelta.x, 96f);
            trt.anchoredPosition = new Vector2(0f, -40f);

            _message = CreateLabel(_panel, "Message", "", 34, TextAlignmentOptions.Center, Color.white, FontStyles.Normal);
            _message.textWrappingMode = TextWrappingModes.Normal;
            var mrt = (RectTransform)_message.transform;
            mrt.anchorMin = new Vector2(0f, 0.28f); mrt.anchorMax = new Vector2(1f, 0.78f);
            mrt.offsetMin = new Vector2(50f, 0f); mrt.offsetMax = new Vector2(-50f, 0f);

            _closeButton = MakeButton("닫기", out _closeImg, out _closeLabel, out _closeRt);
            _retryButton = MakeButton("다시 시도", out _retryImg, out _retryLabel, out _retryRt);
            _closeButton.onClick.AddListener(OnClose);
            _retryButton.onClick.AddListener(OnRetry);

            UiLayer.Apply(gameObject);
        }

        private Button MakeButton(string label, out Image img, out TextMeshProUGUI labelTmp, out RectTransform rt)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(_panel, false);
            rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(260f, 96f);
            img = go.GetComponent<Image>();
            img.color = Neutral; // ShowAlertInternal 가 상태별로 재지정

            labelTmp = CreateLabel(rt, "Label", label, 34, TextAlignmentOptions.Center, Color.white, FontStyles.Bold);
            var lrt = (RectTransform)labelTmp.transform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;

            return go.GetComponent<Button>();
        }

        private static TextMeshProUGUI CreateLabel(Transform parent, string name, string text, int fontSize,
            TextAlignmentOptions align, Color color, FontStyles style)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = align;
            tmp.fontStyle = style;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.raycastTarget = false;
            return tmp;
        }
    }
}
