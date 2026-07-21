using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
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
        private UnitOverheadSpriteSet _sprites;
        private bool _built;
        private bool _defender;
        private float _ratio = 1f;
        private float _trailRatio = 1f;
        // 계약 8 동적 정규화 — HP+실드 > 100% 면 두 세그먼트를 함께 압축(분모 =
        // max(1, hp+shield)). 풀피+실드도 실드가 항상 보인다.
        private float _displayScale = 1f;
        private float _barInnerWidth;
        private UnitOverheadUiStyle _style;
        private UnitOverheadUiStyle.BarSkin _skin;

        public void Show(Vector2 anchorLocal, float tileWidthReference, bool defender, float ratio,
            IReadOnlyList<DreamcatcherCard> cards, UnitOverheadUiStyle style,
            UnitOverheadSpriteSet sprites, bool resetHealth, float shieldRatio = 0f)
        {
            if (style == null || sprites == null) return;
            if (!_built || _defender != defender || _sprites != sprites) Rebuild(defender, style, sprites);
            _style = style;
            _skin = defender ? style.Defender : style.Enemy;
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
            _barCanvasGroup.alpha = (ratio >= 0.999f && !hasShield) ? _skin.fullHealthAlpha : 1f;
            if (ratio < _ratio) _trailRatio = Mathf.Max(_trailRatio, _ratio);
            else if (ratio > _trailRatio) _trailRatio = ratio;
            _ratio = ratio;

            ShowCards(defender ? cards : null, tileWidthReference, width);
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!_built || _style == null || _trailRatio <= _ratio) return;
            _trailRatio = Mathf.MoveTowards(_trailRatio, _ratio,
                _style.DamageTrailCatchup * Time.unscaledDeltaTime);
            SetInnerRect(_trail, _barInnerWidth * _trailRatio * _displayScale,
                Mathf.Max(1f, _skin.height - 2f * _skin.inset), _skin.inset);
        }

        private void Rebuild(bool defender, UnitOverheadUiStyle style, UnitOverheadSpriteSet sprites)
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
            _cards.Clear();
            _defender = defender;
            _style = style;
            _sprites = sprites;
            _skin = defender ? style.Defender : style.Enemy;
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

        private void ShowCards(IReadOnlyList<DreamcatcherCard> source, float tileWidth, float barWidth)
        {
            int count = source != null ? Mathf.Min(3, source.Count) : 0;
            if (count == 0)
            {
                for (int i = 0; i < _cards.Count; i++) _cards[i].root.gameObject.SetActive(false);
                return;
            }
            EnsureCardSlots(count);
            float maxRowWidth = Mathf.Min(tileWidth * _style.CardRowTileWidthFraction, barWidth);
            float spacing = UnitOverheadLayout.CardSpacing(_style.CardSpacing, count, maxRowWidth);
            Vector2 size = UnitOverheadLayout.CardSize(_style.CardHeight, spacing, count, maxRowWidth);
            float step = size.x + spacing;
            float origin = -0.5f * step * (count - 1);
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
