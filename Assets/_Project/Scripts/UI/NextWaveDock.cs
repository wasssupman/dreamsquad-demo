using System.Text;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wassup.Bridge;
using Wassup.Core;
using Wassup.UI.Layout;

namespace Wassup.UI
{
    // battle-hud-score-timer-menu Unit 1 — 좌하단 도크. 상단 행이 판 카운트다운을 갖는다
    // (상단 중앙은 점수가 가져갔다).
    //
    // ─────────────────────────────────────────────────────────────────────────
    // next-wave-dock-legibility rev 3 — **한 박스, 두 상태.** (사용자 결정 2026-08-13)
    //
    // rev 1·2 의 실패는 크기 튜닝이 아니라 전제였다: 적 목록을 **항상 띄워두려니**
    //   · 크게 하면(400×114 패널) 맵을 가리고
    //   · 작게 하면(320×30 툴팁) 안 읽힌다
    // 둘 사이에 답이 없다. 그래서 «항상 보인다»를 버렸다.
    //
    // 남은시간 + 웨이브를 **한 박스**로 묶고, 그 박스가 상태를 바꾼다:
    //   탭 1 → 본문이 몬스터 정보로 전환   ·   탭 2 → 당김
    // 목록이 박스 **본문 전체**를 쓰므로 한 줄에 적 하나씩 크게 들어간다. 평소에는
    // 목록이 화면을 1px 도 안 먹는다.
    //
    // 지켜야 하는 것 셋:
    //  ① **남은시간은 두 상태 모두에 남는다.** 헤더가 갖는다 — 미리보기 때문에 시계가
    //     사라지면 3분 게임에서 가장 중요한 정보가 조작에 인질로 잡힌다.
    //  ② **자동 복귀**(previewHoldSeconds). 탭 하나로 웨이브 진행 표시가 영영 가려지면 안 된다.
    //  ③ **잠금은 미리보기 상태에서 사유를 말한다**(spec 계약 3). 잠겼다고 상태 전환을
    //     막지 않는다 — 왜 못 당기는지 읽을 기회 자체가 사라진다.
    //
    // ⚠ 박스 배경은 `UiRoundedSprite` 로 굽는다. 구 아트(`NextWaveButtonFace` 476×179)는
    // 9-slice 가 아니고 `preserveAspect` 라 rect 를 키우면 늘어나는 대신 투명 레터박스가
    // 생긴다 — 그 제약이 rev 1·2 를 288×108 안에 가둔 원인이었다.
    // ─────────────────────────────────────────────────────────────────────────
    public class NextWaveDock : MonoBehaviour
    {
        [SerializeField] private BattleBridge bridge;

        [Header("Timer")]
        [Tooltip("타이머 전용 폰트(미지정 시 기본 TMP). Anton SDF 권장")]
        [SerializeField] private TMP_FontAsset timerFont;
        [SerializeField] private float timerFontSize = 56f;
        [SerializeField] private Color timerColor = Color.white;
        [SerializeField] private Color timerWarnColor = Color.red;
        [Tooltip("이 초 미만이면 타이머를 경고색으로")]
        [SerializeField] private float warnSeconds = 30f;
        [Tooltip("초가 바뀔 때 pop 강도(일반 / 경고구간)")]
        [SerializeField] private float tickPunch = 0.16f;
        [SerializeField] private float tickPunchWarn = 0.36f;

        // 구 `panelSize`·`timerPlateSize`·`timerPlateOffset`·`buttonSize`·`buttonFaceSprite`·
        // `dockFrameSprite`·`buttonColor`·`buttonFontSize`·`buttonMinFontSize`·
        // `buttonContentPadding`·`backingColor`·`countdown*` 는 은퇴했다(rev 3 이 박스 하나로
        // 합치면서 두 플레이트가 사라졌다). 씬 asset 에 orphan 키로 남지만 무해하다
        // (`fixedWaveIntervalSec` 선례).
        [Header("Box")]
        [SerializeField] private Vector2 panelOffset = new Vector2(40f, 40f);
        [Tooltip("박스 크기. 본문이 적 3종을 한 줄씩 담을 수 있어야 한다.")]
        [SerializeField] private Vector2 boxSize = new Vector2(312f, 244f);
        [SerializeField] private float boxCornerRadius = 20f;
        [SerializeField] private Color boxFill = new Color(0.05f, 0.10f, 0.26f, 0.95f);
        [SerializeField] private Color boxBorder = new Color(0.29f, 0.72f, 0.96f, 1f);
        [Tooltip("미리보기 상태일 때 테두리 색(지금 무엇을 보고 있는지 색으로 구분)")]
        [SerializeField] private Color boxBorderPreview = new Color(1f, 0.82f, 0.34f, 1f);
        [SerializeField] private float boxPadding = 14f;

        // 문구는 **짧게, 크게**다(사용자 결정 2026-08-13). 「남은시간」 캡션처럼 화면이
        // 스스로 설명하는 것은 뺐다 — 큰 `2:41` 이 카운트다운이라는 건 말 안 해도 읽힌다.
        [Header("Rows")]
        [SerializeField] private float headerHeight = 66f;
        [SerializeField] private float footerHeight = 42f;
        [Tooltip("본문 — 웨이브 번호 / 적 목록")]
        [SerializeField] private float bodyFontSize = 31f;
        [SerializeField] private float bodyMinFontSize = 20f;
        [Tooltip("본문 보조 줄(컨셉 · 다음 K초)")]
        [SerializeField] private float bodySubFontSize = 23f;
        [SerializeField] private float footerFontSize = 23f;

        [Header("Colors")]
        [SerializeField] private Color captionColor = new Color(0.5f, 0.92f, 1f, 0.95f);
        [SerializeField] private Color bodyColor = new Color(0.95f, 0.98f, 1f, 1f);
        [Tooltip("컨셉(벌떼·중장) 글자색")]
        [SerializeField] private Color conceptColor = new Color(0.42f, 0.86f, 1f, 1f);
        [Tooltip("당길 수 있을 때 푸터 색")]
        [SerializeField] private Color footerReadyColor = new Color(1f, 0.86f, 0.42f, 1f);
        [Tooltip("잠겼을 때 푸터 색")]
        [SerializeField] private Color footerLockedColor = new Color(0.78f, 0.82f, 0.88f, 0.85f);

        [Header("Interaction")]
        [Tooltip("미리보기 상태가 스스로 닫히기까지의 초(실시간). 웨이브 진행 표시가 영영 가려지면 안 된다.")]
        [SerializeField] private float previewHoldSeconds = 5f;
        [Tooltip("탭 시 박스 pop 강도")]
        [SerializeField] private float tapPunch = 0.10f;

        private GameObject _panel;
        private GameObject _box;
        private Image _boxImage;
        private Sprite _boxSpriteIdle;
        private Sprite _boxSpritePreview;
        private Button _boxButton;
        private TextMeshProUGUI _timerLabel;
        private TextMeshProUGUI _bodyMain;
        private TextMeshProUGUI _bodySub;
        private TextMeshProUGUI _footerLabel;
        private Tween _tapTween;
        private Tween _tickTween;
        private bool _built;
        private bool _subscribed;

        // 미리보기 상태(탭 1회로 열리고 previewHoldSeconds 뒤 스스로 닫힌다).
        private bool _previewOpen;
        private float _previewClosesAt;

        // 매 프레임 문자열 조립을 막는 직전값 캐시. -2 = 아직 표시 전(첫 프레임은 반드시 조립).
        private int _lastShownSec = -1;
        private int _lastWaveNum = -2;
        private int _lastWaveTotal = -2;
        private int _lastToNextSec = -2;
        private int _lastFooterKey = int.MinValue;
        private int _lastPreviewWave = -2;
        private bool _lastPreviewOpen;
        private readonly StringBuilder _listBuilder = new StringBuilder(128);

        // wave-pull-revival unit 4 — 튜토리얼 포커스 링이 감쌀 대상.
        // 선례: ScoreHudView.StressBadgeRect · AwakeningGaugeView.HitRect.
        // 박스 하나가 정보와 액션을 다 갖고 있으므로 통째로 준다.
        public RectTransform PullButtonRect =>
            _box != null ? (RectTransform)_box.transform : null;

        private void Awake()
        {
            BuildCanvas();
            if (_panel != null) _panel.SetActive(false);
        }

        private void OnDisable()
        {
            if (_subscribed && GameManager.Instance != null)
                GameManager.Instance.PhaseChanged -= OnPhaseChanged;
            _subscribed = false;
            // 트윈을 남기면 비활성화 중 스케일이 비-단위로 굳고, 씬 언로드 중이면
            // PrimeTween 이 «OnComplete 무시» 에러를 남긴다.
            if (_tickTween.isAlive) _tickTween.Stop();
            if (_tapTween.isAlive) _tapTween.Stop();
            if (_box != null) _box.transform.localScale = Vector3.one;
        }

        // GameManager.Instance may not be set when OnEnable runs (scene load order), so
        // subscribe lazily in Update — mirrors ScoreHudView.
        private void EnsureSubscribed()
        {
            if (_subscribed) return;
            if (GameManager.Instance == null) return;
            GameManager.Instance.PhaseChanged += OnPhaseChanged;
            _subscribed = true;
            OnPhaseChanged(GameManager.Instance.CurrentPhase);
        }

        // 도크는 Battle 페이즈 표시물이다. 게임오버에도 페이즈는 Battle 이지만 결과
        // 오버레이가 도크를 덮는다.
        private void OnPhaseChanged(GamePhase phase)
        {
            if (_panel == null) return;
            bool battle = phase == GamePhase.Battle;
            if (_panel.activeSelf != battle) _panel.SetActive(battle);
            if (!battle) return;

            ClosePreview();
            _lastShownSec = -1;
            // 표시 캐시도 함께 무효화한다 — 판이 바뀌었는데 직전값이 같으면 조립을 건너뛰어
            // 이전 판 문구가 그대로 남는다.
            _lastWaveNum = _lastWaveTotal = _lastToNextSec = _lastPreviewWave = -2;
            _lastFooterKey = int.MinValue;
        }

        private void Update()
        {
            EnsureSubscribed();
            if (bridge == null || _panel == null || !_panel.activeSelf) return;

            // 미리보기는 **실시간**으로 닫힌다(unscaled) — 튜토리얼이 Battle 도메인을 0으로
            // 세우는 구간에서 영영 안 닫히면 안 된다.
            if (_previewOpen && Time.unscaledTime >= _previewClosesAt) ClosePreview();

            RefreshTimer();
            RefreshBody();
            RefreshFooter();
        }

        // ── 헤더: 남은시간. **두 상태 모두에 남는다.** ────────────────────────────
        private void RefreshTimer()
        {
            if (_timerLabel == null) return;

            float remaining = Mathf.Max(0f, bridge.TimerRemaining);
            int min = (int)(remaining / 60f);
            int sec = (int)(remaining % 60f);
            int totalSec = min * 60 + sec;
            bool warn = remaining < warnSeconds;

            if (totalSec != _lastShownSec)
            {
                _timerLabel.text = $"{min}:{sec:D2}";
                // 초가 바뀔 때마다 pop — 카운트다운이 살아있게 느껴지도록. 경고구간은 더 크게.
                // 첫 표시(_lastShownSec == -1)엔 생략, useUnscaledTime 로 정지/슬로우 중에도 동작.
                if (_lastShownSec >= 0)
                {
                    if (_tickTween.isAlive) _tickTween.Stop();
                    _timerLabel.rectTransform.localScale = Vector3.one;
                    _tickTween = Tween.PunchScale(_timerLabel.rectTransform,
                        Vector3.one * (warn ? tickPunchWarn : tickPunch),
                        warn ? 0.30f : 0.22f, useUnscaledTime: true);
                }
                _lastShownSec = totalSec;
            }

            _timerLabel.color = warn ? timerWarnColor : timerColor;
        }

        // ── 본문: 상태에 따라 «웨이브 진행» ↔ «다음에 오는 적» ───────────────────
        private void RefreshBody()
        {
            if (_bodyMain == null) return;

            bool available = bridge.NextWaveAvailable;
            if (_box != null && _box.activeSelf != available) _box.SetActive(available);
            if (!available) return;

            bool hasNext = bridge.NextWaveHasNext;

            if (_previewOpen)
            {
                // 웨이브가 바뀔 때만 조립한다(플랜은 판 시작에 확정되고 이후 불변).
                int wave = bridge.NextWaveNumber;
                if (_lastPreviewOpen && wave == _lastPreviewWave) return;
                _lastPreviewOpen = true;
                _lastPreviewWave = wave;

                string concept = hasNext ? bridge.NextWaveConceptLabel : "";
                _bodySub.text = concept;
                _bodySub.color = conceptColor;
                _bodyMain.text = hasNext ? BuildEnemyList() : "—";
                _bodyMain.alignment = TextAlignmentOptions.TopLeft;
                return;
            }

            // ── 기본 상태 ──
            int total = bridge.WaveCountTotal;
            // **진행 중인** 웨이브 번호다. `NextWaveNumber`(= _nextWaveIndex + 1)는 "다음에
            // 나올" 번호라 그대로 쓰면 3번 웨이브와 싸우는 동안 `웨이브 4` 가 뜬다.
            int current = Mathf.Clamp(bridge.NextWaveNumber - 1, 1, Mathf.Max(1, total));
            // 0 이하는 «임박»이 아니라 **모름**이다 — `NextWaveSecondsRemaining` 은 첫 웨이브
            // 전(_nextWaveIndex == 0)에도 0 을 준다. 그 자리를 「곧 도착」으로 메우면 판 시작
            // 직후 거짓말이 된다.
            float toNext = bridge.NextWaveSecondsRemaining;
            int toNextSec = hasNext && toNext > 0f ? Mathf.CeilToInt(toNext) : -1;

            if (_lastPreviewOpen || current != _lastWaveNum || total != _lastWaveTotal ||
                toNextSec != _lastToNextSec)
            {
                _lastPreviewOpen = false;
                _lastWaveNum = current;
                _lastWaveTotal = total;
                _lastToNextSec = toNextSec;

                _bodyMain.alignment = TextAlignmentOptions.Center;
                _bodyMain.text = total > 0 ? $"웨이브 {current} / {total}" : $"웨이브 {current}";
                _bodySub.color = captionColor;
                _bodySub.text = toNextSec >= 0 ? $"다음 {toNextSec}초" : "";
            }
        }

        // ── 푸터: 지금 탭하면 무슨 일이 일어나나 ────────────────────────────────
        //
        // 상한에 닿아도 **감추지 않는다**(spec 계약 3). 사라지면 «왜 없지», 무반응이면
        // «고장났나»가 된다. 그리고 잠금 사유는 시각이 아니라 **사건**으로 말한다 —
        // 헤더가 초를 세고 있어서 「N초 뒤」로 읽히면 안 된다.
        private void RefreshFooter()
        {
            if (_footerLabel == null || !bridge.NextWaveAvailable) return;

            bool hasNext = bridge.NextWaveHasNext;
            bool allowed = bridge.PullAllowed;
            int remaining = allowed ? bridge.PullsRemaining : 0;

            // 조립 회피용 키: (상태, hasNext, allowed, 남은횟수)
            int key = (_previewOpen ? 1 : 0)
                    | (hasNext ? 2 : 0)
                    | (allowed ? 4 : 0)
                    | (remaining << 3);
            if (key == _lastFooterKey) return;
            _lastFooterKey = key;

            // 문구는 최소로. 「무엇을 하면 무엇이 되는가」한 조각씩만 남긴다.
            if (!hasNext)
            {
                _footerLabel.text = "마지막 웨이브";
                _footerLabel.color = footerLockedColor;
                return;
            }
            if (!_previewOpen)
            {
                _footerLabel.text = "탭 · 다음 웨이브";
                _footerLabel.color = footerLockedColor;
                return;
            }
            if (allowed)
            {
                // 남은 횟수는 채운 점으로 보여준다 — 숫자로 쓰면 «판 전체 할당량»으로
                // 읽힌다(「×3」·「3회 남음」이 실제로 두 번 오해받았다). 점은 그냥 «지금
                // 이만큼 더»로 읽히고 글자 수도 안 먹는다. 복구 조건은 아래 잠금 문구가 말한다.
                _footerLabel.text = $"당기기 {Pips(remaining)}";
                _footerLabel.color = footerReadyColor;
            }
            else
            {
                // 왜 잠겼는지가 아니라 **무엇을 하면 풀리는지**를 말한다.
                _footerLabel.text = "정리하면 다시";
                _footerLabel.color = footerLockedColor;
            }
        }

        // 남은 당김 횟수 = 채운 점. 6개까지만 찍고 그 위는 숫자로 떨어진다(저작이 커져도
        // 푸터가 안 터지게).
        private static string Pips(int count)
        {
            if (count <= 0) return "";
            if (count > 6) return $"×{count}";
            return count switch
            {
                1 => "●",
                2 => "●●",
                3 => "●●●",
                4 => "●●●●",
                5 => "●●●●●",
                _ => "●●●●●●",
            };
        }

        // 예고는 실스폰과 **같은 데이터**에서 나온다(unit 1 계약 4) — 브리지의 구성 창구를
        // 그대로 읽는다. 한 줄에 적 하나씩, 본문 전체를 쓴다.
        private string BuildEnemyList()
        {
            if (!bridge.TryGetNextWaveComposition(out var groups)) return "";

            _listBuilder.Clear();
            for (int i = 0; i < groups.Count; i++)
            {
                var g = groups[i];
                if (g.unit == null || g.count <= 0) continue;
                if (_listBuilder.Length > 0) _listBuilder.Append('\n');
                _listBuilder
                    .Append(string.IsNullOrWhiteSpace(g.unit.displayName)
                        ? g.unit.name
                        : g.unit.displayName)
                    .Append("  ×").Append(g.count);
            }
            return _listBuilder.ToString();
        }

        // ── 탭: 상태 전환 → 당김 ────────────────────────────────────────────────
        //
        // **플레이어 경로는 여기 하나뿐이다.** `BattleBridge.ForceNextWave`(기제)를 직접
        // 부르지 않는다 — 부르면 겹침 상한이 우회된다.
        private void OnBoxClicked()
        {
            if (bridge == null || !bridge.NextWaveAvailable) return;
            Punch();

            if (!_previewOpen)
            {
                OpenPreview();
                return;
            }

            // 미리보기 중 두 번째 탭 = 당김. 잠겨 있으면 **닫지 않고** 창을 연장한다 —
            // 사유를 읽을 시간을 뺏지 않는다.
            if (!bridge.TryPullNextWave())
            {
                _previewClosesAt = Time.unscaledTime + previewHoldSeconds;
                return;
            }
            ClosePreview();
        }

        private void OpenPreview()
        {
            _previewOpen = true;
            _previewClosesAt = Time.unscaledTime + previewHoldSeconds;
            _lastPreviewWave = -2;   // 본문 재조립 강제
            _lastFooterKey = int.MinValue;
            if (_boxImage != null && _boxSpritePreview != null) _boxImage.sprite = _boxSpritePreview;
        }

        private void ClosePreview()
        {
            if (!_previewOpen && _boxImage != null && _boxImage.sprite == _boxSpriteIdle) return;
            _previewOpen = false;
            _lastWaveNum = -2;       // 본문 재조립 강제
            _lastFooterKey = int.MinValue;
            if (_boxImage != null && _boxSpriteIdle != null) _boxImage.sprite = _boxSpriteIdle;
        }

        private void Punch()
        {
            if (_box == null) return;
            if (_tapTween.isAlive) _tapTween.Stop();
            var rt = (RectTransform)_box.transform;
            rt.localScale = Vector3.one;
            _tapTween = Tween.PunchScale(rt, Vector3.one * tapPunch, 0.20f, useUnscaledTime: true);
        }

        // ── 캔버스 ──────────────────────────────────────────────────────────────
        private void BuildCanvas()
        {
            if (_built) return;
            _built = true;

            var roots = UiCanvasSetup.Ensure(gameObject, sortingOrder: 7);

            _panel = new GameObject("DockPanel", typeof(RectTransform));
            _panel.transform.SetParent(roots.SafeAreaRoot, false);
            var prt = (RectTransform)_panel.transform;
            prt.anchorMin = Vector2.zero;
            prt.anchorMax = Vector2.zero;
            prt.pivot = Vector2.zero;
            prt.anchoredPosition = panelOffset;
            prt.sizeDelta = boxSize;

            _box = new GameObject("WaveBox", typeof(RectTransform), typeof(Image));
            _box.transform.SetParent(_panel.transform, false);
            var brt = (RectTransform)_box.transform;
            brt.anchorMin = Vector2.zero;
            brt.anchorMax = Vector2.one;
            brt.offsetMin = Vector2.zero;
            brt.offsetMax = Vector2.zero;

            // 새 아트를 들이지 않는다 — CostDisplay·ScoreHudView 가 쓰는 같은 경로로 굽는다.
            // 상태별로 두 장을 미리 구워둔다(매 전환마다 Make 를 부르면 텍스처가 쌓인다).
            _boxSpriteIdle = UiRoundedSprite.Make(boxCornerRadius, 3f, boxFill, boxBorder);
            _boxSpritePreview = UiRoundedSprite.Make(boxCornerRadius, 3f, boxFill, boxBorderPreview);
            _boxImage = _box.GetComponent<Image>();
            _boxImage.sprite = _boxSpriteIdle;
            _boxImage.type = Image.Type.Sliced;
            _boxImage.color = Color.white;
            _boxImage.raycastTarget = true;

            _boxButton = _box.AddComponent<Button>();
            _boxButton.targetGraphic = _boxImage;
            // 색 전이는 상태 스프라이트가 맡는다(ColorTint 가 덧칠하면 소유자가 둘이 된다).
            _boxButton.transition = Selectable.Transition.None;
            _boxButton.onClick.AddListener(OnBoxClicked);

            float innerW = boxSize.x - boxPadding * 2f;
            float bodyH = boxSize.y - boxPadding * 2f - headerHeight - footerHeight;

            // 헤더 — 시계만. 캡션(「남은시간」)은 뺐다: 큰 숫자가 스스로 말한다.
            _timerLabel = AddLabel("Timer", timerFontSize, timerColor,
                bold: true, TextAlignmentOptions.Center);
            if (timerFont != null) _timerLabel.font = timerFont;
            Place(_timerLabel, boxPadding, boxSize.y - boxPadding - headerHeight,
                innerW, headerHeight);
            _timerLabel.text = "3:00";

            // 본문 보조 줄(컨셉 / 다음 K초) — 본문 위쪽
            _bodySub = AddLabel("BodySub", bodySubFontSize, captionColor,
                bold: true, TextAlignmentOptions.Center);
            Place(_bodySub, boxPadding, boxPadding + footerHeight + bodyH - bodySubFontSize * 1.4f,
                innerW, bodySubFontSize * 1.4f);
            _bodySub.text = "";

            // 본문 본줄 — 기본: 웨이브 N / M · 미리보기: 적 목록(한 줄에 하나)
            _bodyMain = AddLabel("BodyMain", bodyFontSize, bodyColor,
                bold: true, TextAlignmentOptions.Center);
            _bodyMain.textWrappingMode = TextWrappingModes.NoWrap;
            _bodyMain.fontSizeMin = bodyMinFontSize;
            Place(_bodyMain, boxPadding, boxPadding + footerHeight,
                innerW, bodyH - bodySubFontSize * 1.4f);
            _bodyMain.text = "";

            // 푸터 — 지금 탭하면 무슨 일이 일어나나
            _footerLabel = AddLabel("Footer", footerFontSize, footerLockedColor,
                bold: true, TextAlignmentOptions.Center);
            Place(_footerLabel, boxPadding, boxPadding, innerW, footerHeight);
            _footerLabel.text = "";

            _box.SetActive(false);
            UiLayer.Apply(gameObject);
        }

        private TextMeshProUGUI AddLabel(
            string name, float fontSize, Color color, bool bold, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(_box.transform, false);
            var label = go.AddComponent<TextMeshProUGUI>();
            label.fontSize = fontSize;
            // 자동 축소를 항상 켠다 — 긴 적 이름(`Waypoint Basic Alt`)도 잘리면 안 된다.
            label.enableAutoSizing = true;
            label.fontSizeMin = Mathf.Min(bodyMinFontSize, fontSize);
            label.fontSizeMax = fontSize;
            label.color = color;
            if (bold) label.fontStyle = FontStyles.Bold;
            label.alignment = alignment;
            label.overflowMode = TextOverflowModes.Ellipsis;
            // 박스가 버튼이므로 자식 라벨이 레이캐스트를 먹으면 탭이 통과하지 못한다.
            label.raycastTarget = false;
            ApplyOutline(label, new Color(0.02f, 0.06f, 0.16f, 1f), bold ? 0.16f : 0.12f);
            return label;
        }

        private static void Place(TextMeshProUGUI label, float x, float y, float w, float h)
        {
            var rt = label.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = Vector2.zero;
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
        }

        private static void ApplyOutline(TextMeshProUGUI label, Color color, float width)
        {
            if (label.font == null) return;
            var material = label.fontMaterial;
            material.SetColor(ShaderUtilities.ID_OutlineColor, color);
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, width);
        }
    }
}
