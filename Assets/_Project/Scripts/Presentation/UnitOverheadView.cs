using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Wassup.Data;

namespace Wassup.Presentation
{
    // unit-overhead-ui — 공통 수명주기/레이아웃, defender/enemy 는 skin만 다르다.
    public class UnitOverheadView : MonoBehaviour
    {
        private RectTransform _root;
        private RectTransform _bar;
        private RectTransform _shadow;
        private RectTransform _trail;
        private RectTransform _fill;
        // shield-guardian-defender unit 2 — HP fill 끝에 이어붙는 실드 세그먼트.
        private RectTransform _shield;
        private Image _trailImage;
        private Image _shadowImage;
        private Image _fillImage;
        private Image _shieldImage;
        private Image _highlightImage;
        private CanvasGroup _barCanvasGroup;
        private readonly List<(RectTransform root, Image frame, Image art)> _cards = new();
        // 확장(unit 7) — 스택 아이콘 행 슬롯(아이콘 + 카운트 배지) + 가시 항목 임시 버퍼.
        private readonly List<(RectTransform root, Image icon, RectTransform badge, Image badgePlate, TMP_Text badgeText)> _stacks = new();
        private readonly List<(Sprite sprite, int count)> _visibleStacks = new();
        private UnitOverheadSpriteSet _sprites;
        private bool _built;
        // three-minute-survival unit 1 — Rebuild 판정의 기준. _defender 는 카드행 게이트로만
        // 남는다(Defender 스킨에서만 카드가 뜬다).
        private OverheadBarSkin _skinKind;
        private bool _defender;
        private float _ratio = 1f;
        private float _trailRatio = 1f;
        // 계약 8 동적 정규화 — HP+실드 > 100% 면 두 세그먼트를 함께 압축(분모 =
        // max(1, hp+shield)). 풀피+실드도 실드가 항상 보인다.
        private float _displayScale = 1f;
        private float _barInnerWidth;
        private UnitOverheadUiStyle _style;
        private UnitOverheadUiStyle.BarSkin _skin;

        // three-minute-survival unit 1 — `bool defender` → OverheadBarSkin.
        // three-minute-kill-race unit 2 — `valueLabel` 인자는 제거했다. 바 위 수치는 골 안정도
        // 스킨 전용이었고 그 스킨 자체가 은퇴했다(마음은 게이지로 그리지 않는다).
        public void Show(Vector2 anchorLocal, float tileWidthReference, OverheadBarSkin skinKind, float ratio,
            IReadOnlyList<DreamcatcherCard> cards, UnitOverheadUiStyle style,
            UnitOverheadSpriteSet sprites, bool resetHealth, float shieldRatio = 0f,
            IReadOnlyList<OverheadStackEntry> stacks = null, StackIconRegistry stackIcons = null)
        {
            if (style == null || sprites == null) return;
            if (!_built || _skinKind != skinKind || _sprites != sprites) Rebuild(skinKind, style, sprites);
            bool defender = skinKind == OverheadBarSkin.Defender;
            _style = style;
            _skin = style.Skin(skinKind);
            _root.anchoredPosition = anchorLocal;

            ratio = Mathf.Clamp01(ratio);
            shieldRatio = Mathf.Max(0f, shieldRatio);
            float displayTotal = ratio + shieldRatio;
            _displayScale = displayTotal > 1f ? 1f / displayTotal : 1f;
            if (resetHealth)
            {
                _ratio = ratio;
                _trailRatio = ratio;
            }

            float width = UnitOverheadLayout.BarWidth(tileWidthReference, _skin.tileWidthFraction,
                _skin.minWidth, _skin.maxWidth);
            Vector2 vertical = UnitOverheadLayout.VerticalOffsets(style.HeadGap, _skin.height, style.CardGap);
            _bar.sizeDelta = new Vector2(width, _skin.height);
            _bar.anchoredPosition = new Vector2(0f, vertical.x);
            _shadow.sizeDelta = new Vector2(width + _skin.border * 2f, _skin.height + _skin.border);
            _shadow.anchoredPosition = new Vector2(1f, vertical.x - _skin.shadowOffset);
            _shadowImage.color = _skin.shadow;
            _barInnerWidth = Mathf.Max(1f, width - 2f * _skin.inset);
            float innerHeight = Mathf.Max(1f, _skin.height - 2f * _skin.inset);
            SetInnerRect(_trail, _barInnerWidth * _trailRatio * _displayScale, innerHeight, _skin.inset);
            SetInnerRect(_fill, _barInnerWidth * ratio * _displayScale, innerHeight, _skin.inset);
            // 실드 세그먼트 — HP fill 끝에서 시작(가림 없음), 폭은 같은 스케일.
            bool hasShield = shieldRatio > 0.0001f;
            _shield.gameObject.SetActive(hasShield);
            if (hasShield)
            {
                _shield.sizeDelta = new Vector2(
                    Mathf.Max(0f, _barInnerWidth * shieldRatio * _displayScale), innerHeight);
                _shield.anchoredPosition = new Vector2(
                    _skin.inset + _barInnerWidth * ratio * _displayScale, 0f);
                _shieldImage.color = _skin.shield;
            }
            _highlightImage.rectTransform.sizeDelta = new Vector2(_barInnerWidth, _skin.highlightHeight);
            _highlightImage.rectTransform.anchoredPosition = new Vector2(_skin.inset, -_skin.inset);
            _fillImage.color = UnitOverheadUiStyle.EvaluateFill(_skin, ratio);
            _trailImage.color = _skin.damageTrail;
            _highlightImage.color = _skin.highlight;
            // 실드 보유 중엔 만피 감쇠를 끈다 — 실드가 있는 유닛은 바가 정보를 담는다.
            // heart-stress-axis unit 1 — 감쇠 지점은 **스킨이 정한다**. 원래 뜻은 「정보가 없을 때
            // 흐리게」이고 체력바에서는 그 지점이 만피다. 차오르는 바(스트레스)는 **빈 쪽**이
            // 정보 없음이고 만점은 판이 끝나기 직전이라, 거기서 흐려지면 정확히 거꾸로다.
            bool atRest = _skin.fadeAtEmpty ? ratio <= 0.001f : ratio >= 0.999f;
            _barCanvasGroup.alpha = (atRest && !hasShield) ? _skin.fullHealthAlpha : 1f;
            if (ratio < _ratio) _trailRatio = Mathf.Max(_trailRatio, _ratio);
            else if (ratio > _trailRatio) _trailRatio = ratio;
            _ratio = ratio;

            float cardRowHeight = ShowCards(defender ? cards : null, tileWidthReference, width);
            ShowStacks(stacks, stackIcons, tileWidthReference, width, cardRowHeight);
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void Update()
        {
            TickCardPulse(); // use-flow unit 3 — 발동 펄스(데미지 트레일과 독립 조건)
            if (!_built || _style == null || _trailRatio <= _ratio) return;
            _trailRatio = Mathf.MoveTowards(_trailRatio, _ratio,
                _style.DamageTrailCatchup * Time.unscaledDeltaTime);
            SetInnerRect(_trail, _barInnerWidth * _trailRatio * _displayScale,
                Mathf.Max(1f, _skin.height - 2f * _skin.inset), _skin.inset);
        }

        private void Rebuild(OverheadBarSkin skinKind, UnitOverheadUiStyle style, UnitOverheadSpriteSet sprites)
        {
            bool defender = skinKind != OverheadBarSkin.Enemy; // 안정도는 방어유닛 스프라이트(둥근 프레임)를 재사용
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
            _cards.Clear();
            _stacks.Clear();
            _skinKind = skinKind;
            _defender = defender;
            _style = style;
            _sprites = sprites;
            _skin = style.Skin(skinKind);
            _root = gameObject.GetComponent<RectTransform>();
            if (_root == null) _root = gameObject.AddComponent<RectTransform>();
            _root.anchorMin = _root.anchorMax = new Vector2(0.5f, 0.5f);
            _root.pivot = new Vector2(0.5f, 0f);
            _root.sizeDelta = Vector2.zero;
            Sprite barSprite = defender ? sprites.defenderBar : sprites.enemyBar;
            Sprite fillSprite = defender ? sprites.defenderFill : sprites.enemyFill;
            _shadow = MakeRect("DropShadow", _root); // 먼저 생성해 health/card 아래에 렌더한다.
            _shadow.anchorMin = _shadow.anchorMax = new Vector2(0.5f, 0f);
            _shadow.pivot = new Vector2(0.5f, 0f);
            _shadowImage = AddImage(_shadow.gameObject, fillSprite, Image.Type.Sliced);
            _bar = MakeRect("HealthBar", _root);
            _bar.anchorMin = _bar.anchorMax = new Vector2(0.5f, 0f);
            _bar.pivot = new Vector2(0.5f, 0f);
            _barCanvasGroup = _bar.gameObject.AddComponent<CanvasGroup>();
            _barCanvasGroup.blocksRaycasts = false;
            _barCanvasGroup.interactable = false;
            AddImage(_bar.gameObject, barSprite, Image.Type.Sliced);

            _trail = MakeRect("DamageTrail", _bar);
            _trail.anchorMin = _trail.anchorMax = new Vector2(0f, 0.5f);
            _trail.pivot = new Vector2(0f, 0.5f);
            _trailImage = AddImage(_trail.gameObject, fillSprite, Image.Type.Sliced);
            _fill = MakeRect("Fill", _bar);
            _fill.anchorMin = _fill.anchorMax = new Vector2(0f, 0.5f);
            _fill.pivot = new Vector2(0f, 0.5f);
            _fillImage = AddImage(_fill.gameObject, fillSprite, Image.Type.Sliced);
            _shield = MakeRect("Shield", _bar);
            _shield.anchorMin = _shield.anchorMax = new Vector2(0f, 0.5f);
            _shield.pivot = new Vector2(0f, 0.5f);
            _shieldImage = AddImage(_shield.gameObject, fillSprite, Image.Type.Sliced);
            _shield.gameObject.SetActive(false);

            var hi = MakeRect("Highlight", _bar);
            hi.anchorMin = hi.anchorMax = new Vector2(0f, 1f);
            hi.pivot = new Vector2(0f, 1f);
            _highlightImage = AddImage(hi.gameObject, null, Image.Type.Simple);

            _built = true;
        }

        // 확장(unit 7) — 카드행 높이를 반환(스택행이 그 위에 얹히도록). 카드 없으면 0.
        // use-flow unit 3 — 부착 카드 발동 펄스(행 전체, host 단위 귀속 — 사용자 결정
        // 2026-07-29). localScale 만 만지므로 매 프레임 ShowCards 의 sizeDelta/anchoredPosition
        // 재기록과 충돌하지 않는다. 연타는 타이머 재시작으로 자연 코얼레스(과누적 방지).
        // 화이트 플래시는 뺐다 — UI Image 틴트는 곱셈이라 '밝힘'이 불가(Spine 틴트와 같은 이유).
        //
        // 아이콘이 ~29px 라 펀치 단독으론 비가시(사용자 피드백 2026-07-29) → 행 중심에서
        // 확산·페이드하는 링 버스트를 함께 쏜다(락온 확정 펄스와 같은 시각 문법 = 시안 링).
        private float _cardPulseT = -1f;
        private RectTransform _pulseRing;
        private Image _pulseRingImg;
        private float _cardRowCenterY; // ShowCards 캐시 — 링 앵커(행 중심)
        private float _cardRowWidth;   // ShowCards 캐시 — 링 시작 지름

        public void PulseCards()
        {
            if (_cards.Count == 0) return;
            _cardPulseT = 0f;
            // Rebuild 가 자식을 전부 파괴하므로 lazy 재생성(UnityEngine null = 파괴 포함).
            if (_pulseRing == null)
            {
                _pulseRing = MakeRect("DcPulseRing", _root);
                _pulseRing.anchorMin = _pulseRing.anchorMax = new Vector2(0.5f, 0f);
                _pulseRing.pivot = new Vector2(0.5f, 0.5f);
                _pulseRingImg = AddImage(_pulseRing.gameObject,
                    Wassup.UI.UiRoundedSprite.MakeCircle(128, Color.clear, 10f, Color.white),
                    Image.Type.Simple);
                _pulseRingImg.raycastTarget = false;
            }
            _pulseRing.SetAsFirstSibling(); // 아이콘/바 뒤에서 퍼진다(가림 방지)
            _pulseRing.gameObject.SetActive(true);
        }

        private void TickCardPulse()
        {
            if (_cardPulseT < 0f || _style == null) return;
            // ecs-review L1 — 같은 Update 의 트레일과 타임소스 통일(unscaled; timeScale=1 고정
            // 프로젝트라 동작 동일하지만 가정이 깨져도 안전).
            _cardPulseT += Time.unscaledDeltaTime;
            float u = _cardPulseT / _style.CardPulseSec;
            if (u >= 1f)
            {
                _cardPulseT = -1f;
                SetCardsScale(1f);
                if (_pulseRing != null) _pulseRing.gameObject.SetActive(false);
                return;
            }
            // 단봉 펀치: 빠르게 부풀었다 완만히 복귀.
            SetCardsScale(1f + (_style.CardPulseScale - 1f) * Mathf.Sin(u * Mathf.PI));
            // 링 버스트: ease-out 팽창 + 선형 페이드(확정 펄스와 같은 감각).
            if (_pulseRing != null)
            {
                float grow = 1f - (1f - u) * (1f - u);
                float dia = Mathf.Max(_cardRowWidth, _style.CardHeight * 2f)
                            * Mathf.Lerp(1f, _style.CardPulseRingScale, grow);
                _pulseRing.sizeDelta = new Vector2(dia, dia);
                _pulseRing.anchoredPosition = new Vector2(0f, _cardRowCenterY);
                var c = _style.CardPulseRingColor;
                c.a *= 1f - u;
                _pulseRingImg.color = c;
            }
        }

        private void SetCardsScale(float s)
        {
            for (int i = 0; i < _cards.Count; i++)
                _cards[i].root.localScale = new Vector3(s, s, 1f);
        }

        // 뷰 풀 반환/화면 밖 비활성 시 잔류 펄스 리셋(재사용 시 튐 방지).
        private void OnDisable()
        {
            _cardPulseT = -1f;
            SetCardsScale(1f);
            if (_pulseRing != null) _pulseRing.gameObject.SetActive(false);
        }

        private float ShowCards(IReadOnlyList<DreamcatcherCard> source, float tileWidth, float barWidth)
        {
            int count = source != null ? Mathf.Min(3, source.Count) : 0;
            if (count == 0)
            {
                for (int i = 0; i < _cards.Count; i++) _cards[i].root.gameObject.SetActive(false);
                return 0f;
            }
            EnsureCardSlots(count);
            float maxRowWidth = Mathf.Min(tileWidth * _style.CardRowTileWidthFraction, barWidth);
            float spacing = UnitOverheadLayout.CardSpacing(_style.CardSpacing, count, maxRowWidth);
            Vector2 size = UnitOverheadLayout.CardSize(_style.CardHeight, spacing, count, maxRowWidth);
            float step = size.x + spacing;
            float origin = -0.5f * step * (count - 1);
            // use-flow unit 3 — 링 버스트 앵커/시작 지름 캐시(행 중심·행 폭).
            float rowY = UnitOverheadLayout.VerticalOffsets(_style.HeadGap, _skin.height, _style.CardGap).y;
            _cardRowCenterY = rowY + size.y * 0.5f;
            _cardRowWidth = step * (count - 1) + size.x;
            for (int i = 0; i < _cards.Count; i++)
            {
                var slot = _cards[i];
                bool used = i < count;
                slot.root.gameObject.SetActive(used);
                if (!used) continue;
                var card = source[i];
                slot.root.sizeDelta = size;
                slot.root.anchoredPosition = new Vector2(origin + step * i,
                    UnitOverheadLayout.VerticalOffsets(_style.HeadGap, _skin.height, _style.CardGap).y);
                slot.frame.sprite = _sprites.CardFrame(card != null && card.type == CardType.Squad);
                slot.art.sprite = card != null ? card.art : null;
                slot.art.enabled = slot.art.sprite != null;
            }
            return size.y;
        }

        private void EnsureCardSlots(int count)
        {
            while (_cards.Count < count)
            {
                var rt = MakeRect("Dreamcatcher" + _cards.Count, _root);
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
                rt.pivot = new Vector2(0.5f, 0f);
                var frame = AddImage(rt.gameObject, _sprites.CardFrame(false), Image.Type.Sliced);
                var artRt = MakeRect("Art", rt);
                artRt.anchorMin = Vector2.zero;
                artRt.anchorMax = Vector2.one;
                artRt.offsetMin = Vector2.one;
                artRt.offsetMax = -Vector2.one;
                var art = AddImage(artRt.gameObject, null, Image.Type.Simple);
                art.preserveAspect = false;
                _cards.Add((rt, frame, art));
            }
        }

        // 확장(unit 7) — 드림캐쳐 행 위 스택 이상효과 아이콘 행. 레지스트리에 아이콘 있는
        // 스택(count>0)만, StackRowMax 개까지. 아이콘 부재 = 표시 생략(무크래시). ShowCards 미러.
        private void ShowStacks(IReadOnlyList<OverheadStackEntry> stacks, StackIconRegistry registry,
            float tileWidth, float barWidth, float cardRowHeight)
        {
            _visibleStacks.Clear();
            int max = _style.StackRowMax;
            if (stacks != null && registry != null && max > 0)
            {
                for (int i = 0; i < stacks.Count && _visibleStacks.Count < max; i++)
                {
                    var e = stacks[i];
                    if (e.count <= 0) continue;
                    var sprite = registry.IconFor(e.kind);
                    if (sprite == null) continue; // 아이콘 미도착/미매핑 → 생략
                    _visibleStacks.Add((sprite, e.count));
                }
            }

            int count = _visibleStacks.Count;
            if (count == 0)
            {
                for (int i = 0; i < _stacks.Count; i++) _stacks[i].root.gameObject.SetActive(false);
                return;
            }
            EnsureStackSlots(count);

            float iconH = _style.StackIconHeight;
            float iconW = iconH; // 정사각
            float spacing = _style.StackSpacing;
            float maxRowWidth = Mathf.Min(tileWidth * _style.StackRowTileWidthFraction, barWidth);
            float row = iconW * count + spacing * (count - 1);
            if (row > maxRowWidth)
            {
                float available = Mathf.Max(0f, maxRowWidth - spacing * (count - 1));
                iconW = available / count;
                iconH = iconW;
            }
            float step = iconW + spacing;
            float origin = -0.5f * step * (count - 1);
            float cardRowBottom = UnitOverheadLayout.VerticalOffsets(_style.HeadGap, _skin.height, _style.CardGap).y;
            float rowBottom = UnitOverheadLayout.StackRowBottom(cardRowBottom, cardRowHeight, _style.StackGap);
            float badgeH = Mathf.Max(1f, iconH * _style.StackBadgeHeightFraction);

            for (int i = 0; i < _stacks.Count; i++)
            {
                var slot = _stacks[i];
                bool used = i < count;
                slot.root.gameObject.SetActive(used);
                if (!used) continue;
                var (sprite, cnt) = _visibleStacks[i];
                slot.root.sizeDelta = new Vector2(iconW, iconH);
                slot.root.anchoredPosition = new Vector2(origin + step * i, rowBottom);
                slot.icon.sprite = sprite;
                slot.icon.enabled = true;

                // 카운트 배지 — 아이콘 우상단. count>=1 항상 표기(계약).
                slot.badge.sizeDelta = new Vector2(badgeH * 1.35f, badgeH);
                slot.badgePlate.color = _style.StackBadgePlate;
                slot.badgeText.text = cnt.ToString();
                slot.badgeText.color = _style.StackBadgeColor;
                slot.badgeText.fontSize = badgeH * 0.78f;
            }
        }

        private void EnsureStackSlots(int count)
        {
            while (_stacks.Count < count)
            {
                var rt = MakeRect("Stack" + _stacks.Count, _root);
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
                rt.pivot = new Vector2(0.5f, 0f);
                var icon = AddImage(rt.gameObject, null, Image.Type.Simple);
                icon.preserveAspect = true;

                var badgeRt = MakeRect("Badge", rt);
                badgeRt.anchorMin = badgeRt.anchorMax = new Vector2(1f, 1f); // 아이콘 우상단
                badgeRt.pivot = new Vector2(1f, 1f);
                var plate = AddImage(badgeRt.gameObject, null, Image.Type.Simple);

                var textRt = MakeRect("Count", badgeRt);
                textRt.anchorMin = Vector2.zero;
                textRt.anchorMax = Vector2.one;
                textRt.offsetMin = Vector2.zero;
                textRt.offsetMax = Vector2.zero;
                var tmp = textRt.gameObject.AddComponent<TextMeshProUGUI>();
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.enableWordWrapping = false;
                tmp.raycastTarget = false;

                _stacks.Add((rt, icon, badgeRt, plate, tmp));
            }
        }

        private static void SetInnerRect(RectTransform rt, float width, float height, float inset)
        {
            rt.sizeDelta = new Vector2(Mathf.Max(0f, width), height);
            rt.anchoredPosition = new Vector2(inset, 0f);
        }

        private static RectTransform MakeRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static Image AddImage(GameObject go, Sprite sprite, Image.Type type)
        {
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.type = sprite != null ? type : Image.Type.Simple;
            image.raycastTarget = false;
            return image;
        }
    }
}
