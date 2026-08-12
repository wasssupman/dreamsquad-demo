using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wassup.Data;
using Wassup.UI.Layout;

namespace Wassup.UI
{
    // unit-dreamcatcher-inspect unit 2 → selection-hand-attach unit 11 재설계.
    //
    // 선택 유닛의 상세 패널. 원래는 유닛 옆(스크린 좌표 +46px)에 붙어 따라다녔는데, 그러면
    // 콜아웃 좌측 87px 와 리티클 우측 20px 를 **항상** 먹는다(기하학적으로 확정 — 패널 폭 460,
    // 세로 중앙 정렬). 그래서 앵커 추종을 버리고 **화면 좌측에 고정**한다:
    //
    //  - 초점 침범이 0 이 된다. 유닛이 어디 있든 패널은 같은 자리다.
    //  - 위치가 학습된다("부착·스탯은 항상 저기").
    //  - 카메라 포즈 의존이 사라져 LateUpdate/Follow 자체가 불필요해졌다.
    //
    // 유닛↔패널의 공간적 연결은 **리티클**이 진다(README 계약 11) — 그래서 리티클은 간소화하되
    // 없애지 않는다.
    //
    // 뷰는 Entity/BattleBridge 를 모른다 — 컨트롤러가 이름·포트레이트·스탯·부착을 해석해 넘긴다.
    public class DcInspectPanelView : MonoBehaviour
    {
        private const int PanelSortingOrder = 9; // SquadPrep(8) 위, MenuPopup(960) 아래
        private const float HiddenScale = 0.94f;
        private const float LerpK = 18f;

        [SerializeField] private TMP_FontAsset labelFont;
        // dreamcatcher-attach-requirement unit 5 — "{유닛명} 전용" 접두 해석기.
        // null 이면 포매터가 id 문자열로 폴백한다.
        [SerializeField] private Wassup.Data.DefenderCatalog defenderCatalog;

        [Header("Dock")]
        [Tooltip("safe area 좌측에서 패널까지(캔버스 단위). MenuButton 과 같은 좌측 정렬")]
        [SerializeField] private float dockLeft = 24f;
        [Tooltip("safe area 상단에서 패널 상단까지. MenuButton(24+64) 아래로 내린 값")]
        // MenuButton 하단(24+64=88)에 붙인다. 아래쪽 여유가 빠듯해서 위를 아낀다 —
        // 트레이 상단은 1080 기준 y 912(placementSize.y 136 + anchoredY 32, bottom pivot).
        [SerializeField] private float dockTop = 88f;
        // 폰트를 키운 만큼 넓혀야 설명이 덜 접힌다(줄 수가 곧 패널 높이다). 좌측 대역은
        // 어차피 비어 있어 폭은 여유가 있고, 아낄 것은 세로다.
        [SerializeField] private float panelWidth = 430f;

        // 폰트는 **크게** 간다(사용자 결정 2026-07-30). 플레이어층에 40대가 포함돼 있어
        // 모바일 화면에서 작은 글자는 그냥 안 읽힌다. 값은 1920×1080 캔버스 기준.
        [Header("Font sizes")]
        [SerializeField] private float fontUnitName = 38f;
        [SerializeField] private float fontSectionLabel = 21f;
        [SerializeField] private float fontStatLabel = 27f;
        [SerializeField] private float fontStatValue = 34f;
        [SerializeField] private float fontStatValueMin = 20f; // autosize 하한(값이 길 때만 줄어든다)
        [SerializeField] private float fontChip = 23f;
        [SerializeField] private float fontAttachName = 27f;
        [SerializeField] private float fontAttachKind = 22f;
        [SerializeField] private float fontAttachDesc = 22f;

        [Header("Layout")]
        [SerializeField] private float pad = 18f;
        [SerializeField] private float sectionGap = 10f;
        [SerializeField] private float rowGap = 8f;
        [SerializeField] private float portraitSize = 84f;
        [SerializeField] private float statRowHeight = 54f;
        [SerializeField] private float hpBarHeight = 12f;
        [SerializeField] private float attachArtHeight = 78f;
        [Tooltip("부착 카드 설명의 최대 줄 수. 넘치면 말줄임 — 패널이 아래 트레이를 침범하지 않게 성장을 묶는다.")]
        [SerializeField] private int descMaxLines = 2;
        [SerializeField] private float actionButtonHeight = 60f;
        [SerializeField] private float fontActionButton = 26f;

        [Header("Colors")]
        [SerializeField] private Color fill = new Color(0.07f, 0.06f, 0.13f, 1f);        // 툴팁과 동일 네이비
        [SerializeField] private Color squadBorder = new Color(1f, 0.82f, 0.35f, 1f);    // 골드 — Squad hosted
        [SerializeField] private Color unitBorder = new Color(0.45f, 0.85f, 1f, 1f);     // 청록 — Unit 부착
        [SerializeField] private Color labelColor = new Color(0.55f, 0.52f, 0.66f, 1f);
        [SerializeField] private Color valueColor = new Color(0.94f, 0.93f, 0.98f, 1f);
        [SerializeField] private Color deltaUpColor = new Color(1f, 0.82f, 0.35f, 1f);   // 상승 = 골드
        [SerializeField] private Color deltaDownColor = new Color(1f, 0.48f, 0.42f, 1f); // 하락 = 코랄
        [SerializeField] private Color hpFillColor = new Color(0.35f, 0.86f, 0.55f, 1f);

        // 델타가 이 값 미만이면 칩을 숨긴다. 표시 단위가 다른 스탯을 한 값으로 재는 것이라
        // 넉넉하게 잡는다(rate 는 0.1 단위, 체력은 정수 단위).
        [SerializeField] private float deltaEpsilon = UnitStatMath.DefaultDeltaEpsilon;

        private class StatRow
        {
            public RectTransform rect;
            public TextMeshProUGUI label;
            public TextMeshProUGUI value;
            public RectTransform chip;
            public Image chipBg;
            public TextMeshProUGUI chipText;
        }

        private class AttachRow
        {
            public GameObject root;
            public RectTransform rect;
            public Image bg;
            public Image art;
            public TextMeshProUGUI name;
            public TextMeshProUGUI kind;
            public TextMeshProUGUI desc;
            public float height; // 측정으로 정해진 행 높이(설명 줄 수에 따라 가변)
        }

        private readonly List<AttachRow> _attachRows = new List<AttachRow>();
        private readonly StatRow[] _statRows = new StatRow[3];

        private RectTransform _safeRoot;
        private GameObject _root;
        private RectTransform _rect;
        private CanvasGroup _group;
        private Sprite _squadPlate;
        private Sprite _unitPlate;
        private Sprite _chipPlate;

        // 헤더
        private Image _portrait;
        private TextMeshProUGUI _unitName;
        // 스탯 섹션
        private RectTransform _statsSection;
        private TextMeshProUGUI _statsLabel;
        private RectTransform _hpBarBg;
        private RectTransform _hpBarFill;
        // 부착 섹션
        private RectTransform _attachSection;
        private TextMeshProUGUI _attachLabel;
        // 액션(이동) — 유닛 주변 플립북을 대체한 진입구. unit 15.
        private RectTransform _moveButton;
        private UnityEngine.UI.Button _moveButtonComp;
        private TextMeshProUGUI _moveLabel;   // defender-relocation unit 10 — 라벨에 코스트를 싣는다
        private System.Action _onMove;

        private bool _visible;
        private bool _built;

        private void Awake() => Build();

        // ── 공개 API ──────────────────────────────────────────────────────────

        // defender-relocation unit 10 — 이동 버튼의 잠금/코스트를 매 프레임 받는다. 코스트는
        // 슬로모 중에도 계속 차므로 Show 때 한 번 받아서는 "모아지면 풀린다" 를 표현할 수 없다.
        //
        // 잠금은 **숨김이 아니라 흐림**이다 — 왜 못 누르는지가 코스트 숫자로 읽혀야 한다.
        // 래치를 두어 값이 바뀔 때만 문자열·색을 다시 쓴다(매 프레임 TMP 재빌드 방지).
        private (bool enabled, int cost)? _moveState;

        public void SetMoveState(bool enabled, int cost)
        {
            if (!_built || _moveButton == null || !_moveButton.gameObject.activeSelf) return;
            if (_moveState.HasValue && _moveState.Value.enabled == enabled && _moveState.Value.cost == cost) return;
            _moveState = (enabled, cost);

            _moveButtonComp.interactable = enabled;
            if (_moveLabel != null)
            {
                _moveLabel.text = cost > 0 ? $"이동  {cost}" : "이동";
                var c = unitBorder;
                c.a = enabled ? 1f : 0.4f;
                _moveLabel.color = c;
            }
            var img = _moveButtonComp.targetGraphic as Image;
            if (img != null)
            {
                var c = img.color;
                c.a = enabled ? 1f : 0.45f;
                img.color = c;
            }
        }

        // 구조 갱신(선택/부착 변경). 스탯 값은 SetStats 가 매 프레임 따로 넣는다.
        //
        // unit 11 — **부착 0장이어도 띄운다.** 예전에는 보여줄 카드가 없으면 Hide 했지만
        // ("빈 상태 UI 는 만들지 않는다"), 스탯이 생기면서 0장이어도 보여줄 것이 있다.
        // 부착 섹션만 접는다.
        // onMove 가 null 이면 이동 버튼을 감춘다(재배치 컨트롤러 미배선 등).
        public void Show(string unitName, Sprite portrait,
            IReadOnlyList<DreamcatcherCard> cards, IReadOnlyList<int> costs,
            System.Action onMove = null)
        {
            Build();
            EnsurePlates();
            _onMove = onMove;
            _moveButton.gameObject.SetActive(onMove != null);
            _moveState = null; // unit 10 — 대상이 바뀌었으니 잠금 래치를 비운다(다음 push 가 다시 칠한다)

            // 측정 전에 루트를 켠다. 비활성 계층에서 AddComponent 된 TMP 는 Awake 가 돌지
            // 않아 기본 설정이 로드되지 않고, 그 상태의 GetPreferredValues 는 정답의 1/10 을
            // 답한다(실측 2026-07-15: 헤더 2.21 vs 22.0).
            bool wasHidden = !_root.activeSelf;
            _root.SetActive(true);

            _unitName.text = string.IsNullOrEmpty(unitName) ? "" : unitName;
            bool hasPortrait = portrait != null;
            _portrait.gameObject.SetActive(hasPortrait);
            if (hasPortrait) _portrait.sprite = portrait;

            int count = cards != null ? cards.Count : 0;
            BuildAttachRows(cards, costs, count);
            Layout(count);

            _visible = true;
            if (wasHidden)
            {
                _group.alpha = 0f;
                _rect.localScale = new Vector3(HiddenScale, HiddenScale, 1f);
            }
        }

        // 실효 스탯 갱신 — 컨트롤러가 매 프레임 부른다(대상 1 엔티티라 비용 무시 가능).
        //
        // has=false 는 심이 그 엔티티를 못 내주는 프레임이다(사망 직후 등). **직전 값을 유지**한다 —
        // 대시로 바꾸면 사망 순간 숫자가 깜빡이고, 어차피 컨트롤러의 앵커 liveness 가 곧 닫는다.
        public void SetStats(in UnitStatReadout readout, bool has)
        {
            if (!_built || !has || !_root.activeSelf) return;

            SetStat(0, Mathf.Round(readout.hp) + " / " + Mathf.Round(readout.hpMax),
                readout.hpMaxBase, readout.hpMax, "F0");
            SetStat(1, readout.damage.ToString("0.#"), readout.damageBase, readout.damage, "0.#");
            SetStat(2, readout.attackRate.ToString("0.0") + "/s",
                readout.attackRateBase, readout.attackRate, "0.0");

            float ratio = readout.hpMax > 0f ? Mathf.Clamp01(readout.hp / readout.hpMax) : 0f;
            float inner = panelWidth - pad * 2f;
            _hpBarFill.sizeDelta = new Vector2(inner * ratio, hpBarHeight);
        }

        // 멱등 — 컨트롤러의 Close 는 미선택 상태에서도 불린다.
        public void Hide() => _visible = false;

        // ── 내부 ──────────────────────────────────────────────────────────────

        private void SetStat(int index, string valueText, float baseValue, float effValue, string fmt)
        {
            var row = _statRows[index];
            row.value.text = valueText;

            int sign = UnitStatMath.ResolveDelta(baseValue, effValue,
                Mathf.Max(1e-5f, deltaEpsilon), out float magnitude);
            if (sign == 0)
            {
                // 변화 없으면 칩을 그리지 않는다 — 항상 ▲0 을 띄우면 노이즈가 되고,
                // 변화가 있을 때만 눈에 들어와야 부착의 피드백 루프가 닫힌다.
                row.chip.gameObject.SetActive(false);
                return;
            }

            row.chip.gameObject.SetActive(true);
            row.chipText.text = (sign > 0 ? "▲" : "▼") + magnitude.ToString(fmt);
            row.chipText.color = sign > 0 ? deltaUpColor : deltaDownColor;
            var c = sign > 0 ? deltaUpColor : deltaDownColor;
            row.chipBg.color = new Color(c.r, c.g, c.b, 0.16f);

            row.chipText.ForceMeshUpdate();
            float w = row.chipText.GetPreferredValues(row.chipText.text).x + 18f;
            row.chip.sizeDelta = new Vector2(w, fontChip * 1.5f); // 높이도 폰트 파생
        }

        // 세로 한 열로 쌓는다. 좌측 대역은 세로로 열려 있고 가로로 좁으므로 세우는 것이 의도다.
        private void Layout(int attachCount)
        {
            float inner = panelWidth - pad * 2f;
            float y = pad;

            // 헤더 — 포트레이트 + 이름. 포트레이트가 없는 유닛(아트 미할당)이면 자리를 비우지
            // 않고 이름을 좌측으로 당긴다 — 안 그러면 빈 84px 들여쓰기가 남는다.
            // 텍스트 블록 높이는 전부 폰트에서 파생한다 — 폰트를 키우면 상자도 같이 커져야
            // 글자가 잘리거나 겹치지 않는다(1.3 = 한글 라인 높이 여유).
            float nameH = fontUnitName * 1.3f;
            float labelH = fontSectionLabel * 1.3f;
            float attNameH = fontAttachName * 1.3f;
            float attKindH = fontAttachKind * 1.3f;

            bool hasPortrait = _portrait.gameObject.activeSelf;
            float headerH = hasPortrait ? portraitSize : nameH;
            if (hasPortrait)
            {
                var prt = (RectTransform)_portrait.transform;
                prt.anchoredPosition = new Vector2(pad, -y);
                prt.sizeDelta = new Vector2(portraitSize, portraitSize);
            }

            float nameX = hasPortrait ? pad + portraitSize + 14f : pad;
            float nameY = hasPortrait ? y + (portraitSize - nameH) * 0.5f : y;
            var nrt = _unitName.rectTransform;
            nrt.anchoredPosition = new Vector2(nameX, -nameY);
            nrt.sizeDelta = new Vector2(panelWidth - nameX - pad, nameH);

            y += headerH + sectionGap;

            // 스탯 섹션
            _statsSection.anchoredPosition = new Vector2(0f, -y);
            float sy = 0f;
            _statsLabel.rectTransform.anchoredPosition = new Vector2(pad, -sy);
            _statsLabel.rectTransform.sizeDelta = new Vector2(inner, labelH);
            sy += labelH + 4f;

            for (int i = 0; i < _statRows.Length; i++)
            {
                var row = _statRows[i];
                row.rect.anchoredPosition = new Vector2(0f, -sy);
                row.rect.sizeDelta = new Vector2(panelWidth, statRowHeight);

                // 3레인: 라벨 / 값(우측정렬) / 칩. 값 레인은 "340 / 420" 같은 최장 문자열을
                // 담아야 하므로 라벨보다 넉넉히 준다(모자라면 autosize 가 글자를 줄인다).
                row.label.rectTransform.anchoredPosition = new Vector2(pad, 0f);
                row.label.rectTransform.sizeDelta = new Vector2(inner * 0.34f, statRowHeight);
                row.value.rectTransform.anchoredPosition = new Vector2(pad + inner * 0.34f, 0f);
                row.value.rectTransform.sizeDelta = new Vector2(inner * 0.38f, statRowHeight);
                float chipH = fontChip * 1.5f;
                row.chip.anchoredPosition = new Vector2(pad + inner * 0.74f, -(statRowHeight - chipH) * 0.5f);

                sy += statRowHeight;
                if (i == 0) // 체력 행 아래에 게이지
                {
                    _hpBarBg.anchoredPosition = new Vector2(pad, -sy);
                    _hpBarBg.sizeDelta = new Vector2(inner, hpBarHeight);
                    _hpBarFill.sizeDelta = new Vector2(0f, hpBarHeight);
                    sy += hpBarHeight + rowGap;
                }
            }
            _statsSection.sizeDelta = new Vector2(panelWidth, sy);
            y += sy + sectionGap;

            // 부착 섹션 — 0장이면 접는다
            bool hasAttach = attachCount > 0;
            _attachSection.gameObject.SetActive(hasAttach);
            if (hasAttach)
            {
                _attachSection.anchoredPosition = new Vector2(0f, -y);
                float ay = 0f;
                _attachLabel.rectTransform.anchoredPosition = new Vector2(pad, -ay);
                _attachLabel.rectTransform.sizeDelta = new Vector2(inner, labelH);
                ay += labelH + 4f;

                float artW = attachArtHeight * (2f / 3f); // 타로 아트 2:3
                for (int i = 0; i < _attachRows.Count; i++)
                {
                    var row = _attachRows[i];
                    if (!row.root.activeSelf) continue;

                    float tx = 8f + artW + 12f;
                    float tw = inner - tx - 10f;

                    // 설명은 줄 수가 카드마다 달라 **측정으로** 행 높이를 정한다. 갓 생성된
                    // TMP 는 첫 레이아웃 전까지 폰트 스케일이 서지 않으므로 ForceMeshUpdate 선행.
                    //
                    // 상한이 필요하다: 부착은 최대 3장이고 시한폭탄처럼 효과가 3줄인 카드가
                    // 있어, 묶지 않으면 패널이 아래 트레이까지 자란다(실측 3장 = 717px).
                    // 줄 수를 잘라 성장을 예측 가능하게 만든다 — 넘치는 부분은 말줄임.
                    row.desc.ForceMeshUpdate();
                    float descH = string.IsNullOrEmpty(row.desc.text)
                        ? 0f
                        : row.desc.GetPreferredValues(row.desc.text, tw, 0f).y;
                    if (descH > 0f && descMaxLines > 0)
                    {
                        row.desc.maxVisibleLines = descMaxLines;
                        descH = Mathf.Min(descH, descMaxLines * row.desc.fontSize * 1.3f);
                    }

                    // 이름과 종류가 한 줄이므로 높이는 둘 중 큰 쪽 하나만 든다.
                    float titleH = Mathf.Max(attNameH, attKindH);
                    float textH = 12f + titleH + (descH > 0f ? descH + 4f : 0f) + 10f;
                    float rowH = Mathf.Max(attachArtHeight + 12f, textH);
                    row.height = rowH;

                    row.rect.anchoredPosition = new Vector2(pad, -ay);
                    row.rect.sizeDelta = new Vector2(inner, rowH);

                    var art = (RectTransform)row.art.transform;
                    art.anchoredPosition = new Vector2(8f, -6f);
                    art.sizeDelta = new Vector2(artW, attachArtHeight);

                    // 제목 줄: 이름(좌) + 종류·코스트(우) — 같은 y, 폭을 나눠 쓴다.
                    float kindW = tw * 0.34f;
                    row.name.rectTransform.anchoredPosition = new Vector2(tx, -12f);
                    row.name.rectTransform.sizeDelta = new Vector2(tw - kindW - 6f, attNameH);
                    row.kind.rectTransform.anchoredPosition = new Vector2(tx + tw - kindW, -12f);
                    row.kind.rectTransform.sizeDelta = new Vector2(kindW, titleH);
                    row.desc.rectTransform.anchoredPosition = new Vector2(tx, -(12f + titleH + 4f));
                    row.desc.rectTransform.sizeDelta = new Vector2(tw, Mathf.Max(0f, descH));

                    ay += rowH + rowGap;
                }
                if (attachCount > 0) ay -= rowGap;
                _attachSection.sizeDelta = new Vector2(panelWidth, ay);
                y += ay + sectionGap;
            }

            // 액션 버튼 — 패널 맨 아래 전폭. 정보(위)와 조작(아래)이 분리돼 읽기 순서가 선다.
            if (_moveButton.gameObject.activeSelf)
            {
                _moveButton.anchoredPosition = new Vector2(pad, -y);
                _moveButton.sizeDelta = new Vector2(inner, actionButtonHeight);
                y += actionButtonHeight + sectionGap;
            }

            y += pad - sectionGap;
            _rect.sizeDelta = new Vector2(panelWidth, y);
        }

        private void BuildAttachRows(IReadOnlyList<DreamcatcherCard> cards, IReadOnlyList<int> costs, int count)
        {
            EnsureAttachRows(count);
            for (int i = 0; i < _attachRows.Count; i++)
            {
                var row = _attachRows[i];
                bool used = i < count;
                row.root.SetActive(used);
                if (!used) continue;

                var card = cards[i];
                bool isSquad = card.type == CardType.Squad;
                row.bg.sprite = isSquad ? _squadPlate : _unitPlate;

                string title = string.IsNullOrEmpty(card.displayName) ? card.id : card.displayName;
                row.name.text = title;
                row.name.color = valueColor;

                int cost = costs != null && i < costs.Count ? costs[i] : 0;
                row.kind.text = (isSquad ? "스쿼드" : "유닛") + "  ·  " + cost;
                row.kind.color = isSquad ? squadBorder : unitBorder;

                // unit 11 rev — 효과만(트리거 뒤). 이미 부착된 카드라 "언제"보다 "무엇이
                // 달라지나"가 알고 싶은 것이고, 좁은 셀에 트리거까지 넣으면 효과가 밀린다.
                // 문안 소유는 포매터 하나다(사본 금지 — 손패 툴팁과 어긋나면 안 된다).
                row.desc.text = DreamcatcherCardText.EffectOnly(
                    card, defenderCatalog != null ? defenderCatalog.DisplayNameOf : (System.Func<string, string>)null);
                row.desc.color = labelColor;

                bool hasArt = card.art != null;
                row.art.gameObject.SetActive(hasArt);
                if (hasArt) row.art.sprite = card.art;
            }
            _attachLabel.text = "부착 드림캐쳐 " + count;
        }

        // 알파/스케일만 — 위치가 고정이라 카메라 포즈와 무관하다(구 LateUpdate/Follow 는 제거).
        private void Update()
        {
            if (_root == null || !_root.activeSelf) return;
            float a = 1f - Mathf.Exp(-LerpK * Time.deltaTime);
            _group.alpha = Mathf.Lerp(_group.alpha, _visible ? 1f : 0f, a);
            float s = Mathf.Lerp(_rect.localScale.x, _visible ? 1f : HiddenScale, a);
            _rect.localScale = new Vector3(s, s, 1f);
            // unit 15 — 보이는 동안만 입력을 받는다. 닫히는 중에 이동 버튼이 눌리면 이미
            // 해제된 선택으로 재배치를 시작하게 된다.
            _group.blocksRaycasts = _visible;
            if (!_visible && _group.alpha < 0.02f) _root.SetActive(false);
        }

        // ── 생성 ──────────────────────────────────────────────────────────────

        private void Build()
        {
            if (_built) return;
            var roots = UiCanvasSetup.Ensure(gameObject, PanelSortingOrder);
            _safeRoot = roots.SafeAreaRoot;

            _root = new GameObject("InspectPanel", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            _root.transform.SetParent(_safeRoot, false);
            _rect = (RectTransform)_root.transform;
            // 좌상단 고정 — 앵커 추종 없음(unit 11). pivot 도 좌상단이라 sizeDelta 가 아래로 자란다.
            _rect.anchorMin = new Vector2(0f, 1f);
            _rect.anchorMax = new Vector2(0f, 1f);
            _rect.pivot = new Vector2(0f, 1f);
            _rect.anchoredPosition = new Vector2(dockLeft, -dockTop);

            var panelBg = _root.GetComponent<Image>();
            panelBg.sprite = UiRoundedSprite.Make(16f, 2f, fill, unitBorder);
            panelBg.type = Image.Type.Sliced;
            panelBg.raycastTarget = false; // 계약 — 카드 드롭/조준 판정 비간섭

            _group = _root.GetComponent<CanvasGroup>();
            _group.alpha = 0f;
            // unit 15 — 이동 버튼이 생기며 인터랙티브가 됐다. blocksRaycasts 는 Update 에서
            // _visible 과 함께 움직인다(페이드 중/닫힌 뒤에 눌리지 않게). 그룹이 열려도 실제
            // 히트 대상은 버튼 하나뿐이다 — 나머지 그래픽은 raycastTarget=false 다.
            _group.blocksRaycasts = false;
            _group.interactable = true;

            // 헤더
            var portraitGo = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
            portraitGo.transform.SetParent(_root.transform, false);
            var prt = (RectTransform)portraitGo.transform;
            prt.anchorMin = new Vector2(0f, 1f); prt.anchorMax = new Vector2(0f, 1f);
            prt.pivot = new Vector2(0f, 1f);
            _portrait = portraitGo.GetComponent<Image>();
            _portrait.raycastTarget = false;
            _portrait.preserveAspect = true;

            _unitName = BuildLabel(_root.transform, "UnitName", fontUnitName, TextAlignmentOptions.TopLeft);
            _unitName.fontStyle = FontStyles.Bold;
            _unitName.color = valueColor;

            // 스탯 섹션
            _statsSection = NewSection("StatsSection");
            _statsLabel = BuildLabel(_statsSection, "StatsLabel", fontSectionLabel, TextAlignmentOptions.TopLeft);
            _statsLabel.color = labelColor;
            _statsLabel.characterSpacing = 6f;
            _statsLabel.text = "스탯";

            string[] names = { "체력", "공격력", "공격속도" };
            for (int i = 0; i < _statRows.Length; i++) _statRows[i] = BuildStatRow(names[i]);

            var barBg = new GameObject("HpBarBg", typeof(RectTransform), typeof(Image));
            barBg.transform.SetParent(_statsSection, false);
            _hpBarBg = (RectTransform)barBg.transform;
            _hpBarBg.anchorMin = new Vector2(0f, 1f); _hpBarBg.anchorMax = new Vector2(0f, 1f);
            _hpBarBg.pivot = new Vector2(0f, 1f);
            var bgImg = barBg.GetComponent<Image>();
            bgImg.color = new Color(1f, 1f, 1f, 0.09f);
            bgImg.raycastTarget = false;

            var barFill = new GameObject("HpBarFill", typeof(RectTransform), typeof(Image));
            barFill.transform.SetParent(barBg.transform, false);
            _hpBarFill = (RectTransform)barFill.transform;
            _hpBarFill.anchorMin = new Vector2(0f, 0f); _hpBarFill.anchorMax = new Vector2(0f, 0f);
            _hpBarFill.pivot = new Vector2(0f, 0f);
            _hpBarFill.anchoredPosition = Vector2.zero;
            var fillImg = barFill.GetComponent<Image>();
            fillImg.color = hpFillColor;
            fillImg.raycastTarget = false;

            // 부착 섹션
            _attachSection = NewSection("AttachSection");
            _attachLabel = BuildLabel(_attachSection, "AttachLabel", fontSectionLabel, TextAlignmentOptions.TopLeft);
            _attachLabel.color = labelColor;
            _attachLabel.characterSpacing = 6f;

            BuildMoveButton();

            _root.SetActive(false);
            _built = true;
        }

        // selection-hand-attach unit 15 — 유닛 주변 아이콘 플립북을 대체한 이동 진입구.
        //
        // **패널에서 유일하게 레이캐스트를 받는 요소다.** 나머지 그래픽은 전부
        // raycastTarget=false 로 두어 "카드 드롭/조준 판정 비간섭" 계약을 지킨다 — 버튼 하나만
        // 히트 대상이므로 패널 자리에서 막히는 것은 그 버튼 사각형뿐이다.
        // CanvasGroup 의 blocksRaycasts 는 그룹 전체를 껐다 켜므로 Update 에서 _visible 과
        // 함께 움직인다(페이드 아웃 중에 눌리지 않게).
        private void BuildMoveButton()
        {
            var go = new GameObject("MoveButton", typeof(RectTransform), typeof(Image),
                typeof(UnityEngine.UI.Button));
            go.transform.SetParent(_root.transform, false);
            _moveButton = (RectTransform)go.transform;
            _moveButton.anchorMin = new Vector2(0f, 1f);
            _moveButton.anchorMax = new Vector2(0f, 1f);
            _moveButton.pivot = new Vector2(0f, 1f);

            var img = go.GetComponent<Image>();
            img.sprite = UiRoundedSprite.Make(12f, 2f, fill, unitBorder); // 패널과 같은 색 언어
            img.type = Image.Type.Sliced;
            img.raycastTarget = true; // 계약상 유일한 예외

            _moveButtonComp = go.GetComponent<UnityEngine.UI.Button>();
            _moveButtonComp.targetGraphic = img;
            var colors = _moveButtonComp.colors;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.fadeDuration = 0.06f;
            _moveButtonComp.colors = colors;
            _moveButtonComp.onClick.AddListener(() => { if (_onMove != null) _onMove(); });

            var label = BuildLabel(go.transform, "Label", fontActionButton, TextAlignmentOptions.Center);
            label.fontStyle = FontStyles.Bold;
            label.color = unitBorder;
            label.text = "이동";
            _moveLabel = label; // unit 10 — SetMoveState 가 코스트를 실어 다시 쓴다
            var lrt = label.rectTransform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.pivot = new Vector2(0.5f, 0.5f);
            lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;

            go.SetActive(false);
        }

        private RectTransform NewSection(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(_root.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            return rt;
        }

        private StatRow BuildStatRow(string label)
        {
            var row = new StatRow();
            var go = new GameObject("Stat_" + label, typeof(RectTransform));
            go.transform.SetParent(_statsSection, false);
            row.rect = (RectTransform)go.transform;
            row.rect.anchorMin = new Vector2(0f, 1f);
            row.rect.anchorMax = new Vector2(0f, 1f);
            row.rect.pivot = new Vector2(0f, 1f);

            row.label = BuildLabel(go.transform, "Label", fontStatLabel, TextAlignmentOptions.Left);
            row.label.color = labelColor;
            row.label.text = label;

            row.value = BuildLabel(go.transform, "Value", fontStatValue, TextAlignmentOptions.Right);
            row.value.fontStyle = FontStyles.Bold;
            row.value.color = valueColor;
            // 값은 **절대 줄바꿈되면 안 된다** — "340 / 420" 이 두 줄로 접히면 바로 아래 HP
            // 게이지를 침범한다(실측). 폭이 모자라면 줄을 늘리는 대신 글자를 줄인다.
            row.value.textWrappingMode = TextWrappingModes.NoWrap;
            row.value.enableAutoSizing = true;
            row.value.fontSizeMin = fontStatValueMin;
            row.value.fontSizeMax = fontStatValue;

            var chipGo = new GameObject("Chip", typeof(RectTransform), typeof(Image));
            chipGo.transform.SetParent(go.transform, false);
            row.chip = (RectTransform)chipGo.transform;
            row.chip.anchorMin = new Vector2(0f, 1f);
            row.chip.anchorMax = new Vector2(0f, 1f);
            row.chip.pivot = new Vector2(0f, 1f);
            row.chipBg = chipGo.GetComponent<Image>();
            row.chipBg.sprite = _chipPlate;
            row.chipBg.type = Image.Type.Sliced;
            row.chipBg.raycastTarget = false;

            row.chipText = BuildLabel(chipGo.transform, "ChipText", fontChip, TextAlignmentOptions.Center);
            row.chipText.fontStyle = FontStyles.Bold;
            row.chipText.textWrappingMode = TextWrappingModes.NoWrap; // 칩 폭은 텍스트에서 파생된다
            var ct = row.chipText.rectTransform;
            ct.anchorMin = Vector2.zero; ct.anchorMax = Vector2.one;
            ct.pivot = new Vector2(0.5f, 0.5f);
            ct.offsetMin = Vector2.zero; ct.offsetMax = Vector2.zero;

            chipGo.SetActive(false);
            return row;
        }

        private void EnsureAttachRows(int count)
        {
            while (_attachRows.Count < count)
            {
                var row = new AttachRow();
                row.root = new GameObject("Attach" + _attachRows.Count, typeof(RectTransform), typeof(Image));
                row.root.transform.SetParent(_attachSection, false);
                row.rect = (RectTransform)row.root.transform;
                row.rect.anchorMin = new Vector2(0f, 1f);
                row.rect.anchorMax = new Vector2(0f, 1f);
                row.rect.pivot = new Vector2(0f, 1f);

                row.bg = row.root.GetComponent<Image>();
                row.bg.type = Image.Type.Sliced;
                row.bg.color = Color.white;
                row.bg.raycastTarget = false;

                var artGo = new GameObject("Art", typeof(RectTransform), typeof(Image));
                artGo.transform.SetParent(row.root.transform, false);
                var art = (RectTransform)artGo.transform;
                art.anchorMin = new Vector2(0f, 1f);
                art.anchorMax = new Vector2(0f, 1f);
                art.pivot = new Vector2(0f, 1f);
                row.art = artGo.GetComponent<Image>();
                row.art.raycastTarget = false;
                row.art.preserveAspect = true;

                row.name = BuildLabel(row.root.transform, "Name", fontAttachName, TextAlignmentOptions.TopLeft);
                // 종류·코스트는 이름과 **같은 줄 오른쪽**에 붙인다. 줄을 따로 쓰면 부착 3장에서
                // 그것만으로 ~90px 를 먹는데, 종류는 테두리 색이 이미 나르는 정보다.
                row.kind = BuildLabel(row.root.transform, "Kind", fontAttachKind, TextAlignmentOptions.TopRight);
                row.desc = BuildLabel(row.root.transform, "Desc", fontAttachDesc, TextAlignmentOptions.TopLeft);
                row.desc.overflowMode = TextOverflowModes.Ellipsis; // 줄 수 상한을 넘으면 …

                _attachRows.Add(row);
            }
        }

        // 타입별 플레이트 2종 캐시(DcIconStripSpawner.EnsureFrameSprites 선례).
        private void EnsurePlates()
        {
            if (_squadPlate == null) _squadPlate = UiRoundedSprite.Make(12f, 2f, fill, squadBorder);
            if (_unitPlate == null) _unitPlate = UiRoundedSprite.Make(12f, 2f, fill, unitBorder);
            if (_chipPlate == null)
            {
                _chipPlate = UiRoundedSprite.Make(8f, 0f, Color.white, Color.clear);
                for (int i = 0; i < _statRows.Length; i++)
                    if (_statRows[i] != null && _statRows[i].chipBg != null)
                        _statRows[i].chipBg.sprite = _chipPlate;
            }
        }

        private TextMeshProUGUI BuildLabel(Transform parent, string name, float size, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            if (labelFont != null) tmp.font = labelFont;
            tmp.fontSize = size;
            tmp.color = Color.white;
            tmp.alignment = align;
            tmp.raycastTarget = false;
            // 명시 설정 필수 — TMP_Settings 기본은 Normal 이지만 그 로드는 Awake 에서 일어난다.
            // 비활성 계층에서 생성되면 Awake 가 안 돌아 enum 기본값 0(=NoWrap)이 남는다.
            tmp.textWrappingMode = TextWrappingModes.Normal;
            return tmp;
        }
    }
}
