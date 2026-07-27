using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wassup.Data;

namespace Wassup.UI
{
    // dreamcatcher-deck-page unit 0 — the left 1/3 detail: card art backdrop + a
    // unified card (name / category badge / effect text via DreamcatcherCardText.Body
    // / add + remove actions + hint). The image is a static Sprite (no live Spine).
    //
    // ui-fix 2026-07-18 — two action buttons: [덱에 추가] always (disabled+hint when
    // capped/full) and [덱에서 제거] shown only when the card is already in the deck.
    // So tapping an equipped card (grid OR deck strip) offers Remove directly, while
    // duplicate-add stays possible (Normal cards). Orchestrator handles both events.
    public class DreamcatcherCardDetailView : MonoBehaviour
    {
        [SerializeField] private Image artImage;      // backdrop (scene/builder wired)
        [SerializeField] private RectTransform cardRoot;
        [SerializeField] private TMP_FontAsset font;
        // dreamcatcher-attach-requirement unit 5 — "{유닛명} 전용" 접두 해석기.
        // 이 뷰는 런타임 AddComponent 로 생성되므로 씬 와이어가 아니라
        // DreamcatcherDeckPage 의 SetField 주입으로 받는다(기존 artImage/font 와 동일 경로).
        [SerializeField] private DefenderCatalog defenderCatalog;

        public event Action AddClicked;
        public event Action RemoveClicked;

        // ui-polish 2026-07-27 — 설명 본문 가독성. 26 고정 → 26~32 오토사이즈:
        // 짧은 문안은 32 로 크게 읽히고, 줄 수가 많은 카드(BountyMark 3줄 + "전용" 접두 등)는
        // 기존 크기(26)까지만 줄어 hint/버튼 위로 흘러넘치지 않는다.
        private const float EffectFontMin = 26f;
        private const float EffectFontMax = 32f;

        private static readonly Color CardBg = new Color(0.06f, 0.07f, 0.10f, 0.82f);
        private static readonly Color AddColor = new Color(0.20f, 0.55f, 0.28f, 1f);
        private static readonly Color RemoveColor = new Color(0.55f, 0.22f, 0.24f, 1f);
        private static readonly Color DisabledColor = new Color(0.28f, 0.30f, 0.36f, 1f);
        private static readonly Color HintColor = new Color(0.68f, 0.72f, 0.82f, 1f);

        private bool _built;
        private TMP_Text _nameText;
        private Image _catBadgeBg;
        private TMP_Text _catBadgeText;
        private TMP_Text _effectText;
        private GameObject _addGo;
        private Image _addBg;
        private Button _addButton;
        private TMP_Text _addLabel;
        private TMP_Text _hintText;
        private GameObject _removeGo;

        // 드림캐쳐는 유니크(덱에 0/1장). inDeck 이면 [덱에서 제거]만, 아니면 [덱에 추가]만
        // (상호배타). canAdd/addHint 는 미편성일 때 추가 버튼 활성/사유 구동.
        public void ShowCard(DreamcatcherCard card, bool canAdd, string addHint, bool inDeck)
        {
            EnsureBuilt();
            BindArt(card);

            if (_nameText != null)
                _nameText.text = card == null ? "" : (string.IsNullOrEmpty(card.displayName) ? card.id : card.displayName);
            if (_catBadgeBg != null) _catBadgeBg.color = CardCategoryStyle.Frame(card);
            if (_catBadgeText != null) _catBadgeText.text = CardCategoryStyle.Label(card);
            if (_effectText != null)
                _effectText.text = card == null ? "" : DreamcatcherCardText.Body(
                    card, defenderCatalog != null ? defenderCatalog.DisplayNameOf : (Func<string, string>)null);

            if (_addGo != null) _addGo.SetActive(!inDeck);
            if (!inDeck)
            {
                if (_addBg != null) _addBg.color = canAdd ? AddColor : DisabledColor;
                if (_addButton != null) _addButton.interactable = canAdd;
                if (_hintText != null) _hintText.text = canAdd ? "" : (addHint ?? "");
            }
            else if (_hintText != null) _hintText.text = "";

            if (_removeGo != null) _removeGo.SetActive(inDeck);
        }

        public void Clear()
        {
            EnsureBuilt();
            BindArt(null);
            if (_nameText != null) _nameText.text = "";
            if (_effectText != null) _effectText.text = "";
            if (_catBadgeText != null) _catBadgeText.text = "";
            if (_hintText != null) _hintText.text = "";
            if (_removeGo != null) _removeGo.SetActive(false);
        }

        private void BindArt(DreamcatcherCard card)
        {
            if (artImage == null) return;
            if (card != null && card.art != null)
            {
                artImage.sprite = card.art;
                artImage.color = Color.white;
                artImage.preserveAspect = true;
                artImage.enabled = true;
            }
            else
            {
                artImage.sprite = null;
                artImage.color = card != null ? CardCategoryStyle.ArtFallback(card) : new Color(0.1f, 0.11f, 0.15f, 1f);
                artImage.enabled = true;
            }
        }

        private void EnsureBuilt()
        {
            if (_built || cardRoot == null) return;
            _built = true;

            var bg = cardRoot.gameObject.GetComponent<Image>();
            if (bg == null) bg = cardRoot.gameObject.AddComponent<Image>();
            bg.color = CardBg;

            var vlg = cardRoot.gameObject.GetComponent<VerticalLayoutGroup>();
            if (vlg == null) vlg = cardRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(22, 22, 18, 40);
            vlg.spacing = 12;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;

            _nameText = MakeText(cardRoot, "", 42, TextAlignmentOptions.Left, 52);

            var badge = new GameObject("CatBadge", typeof(RectTransform), typeof(Image));
            badge.transform.SetParent(cardRoot, false);
            _catBadgeBg = badge.GetComponent<Image>();
            var ble = badge.AddComponent<LayoutElement>();
            ble.minWidth = 150; ble.preferredWidth = 150; ble.minHeight = 40; ble.preferredHeight = 40;
            _catBadgeText = MakeText(badge.transform, "", 24, TextAlignmentOptions.Center, 0);
            var brt = _catBadgeText.rectTransform;
            brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one; brt.offsetMin = Vector2.zero; brt.offsetMax = Vector2.zero;

            _effectText = MakeText(cardRoot, "", EffectFontMax, TextAlignmentOptions.TopLeft, 150);
            _effectText.enableWordWrapping = true;
            _effectText.enableAutoSizing = true;
            _effectText.fontSizeMin = EffectFontMin;
            _effectText.fontSizeMax = EffectFontMax;
            // 카드 영역(cardRoot)의 잉여 높이를 본문이 흡수 — 이름/배지/hint/버튼은 고정이라
            // 남는 세로 공간이 전부 설명으로 가고, 오토사이즈도 그만큼 큰 글자를 유지한다.
            var effectLayout = _effectText.GetComponent<LayoutElement>();
            if (effectLayout != null) effectLayout.flexibleHeight = 1f;

            _hintText = MakeText(cardRoot, "", 20, TextAlignmentOptions.Left, 28);
            _hintText.color = HintColor;
            _hintText.fontStyle = FontStyles.Italic;

            _addButton = MakeButton(cardRoot, "덱에 추가", AddColor, out _addBg, out _addLabel);
            _addButton.onClick.AddListener(() => AddClicked?.Invoke());
            _addGo = _addButton.gameObject;

            var removeBtn = MakeButton(cardRoot, "덱에서 제거", RemoveColor, out _, out _);
            removeBtn.onClick.AddListener(() => RemoveClicked?.Invoke());
            _removeGo = removeBtn.gameObject;
            _removeGo.SetActive(false);
        }

        private Button MakeButton(Transform parent, string text, Color color, out Image bg, out TMP_Text label)
        {
            var go = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            bg = go.GetComponent<Image>();
            bg.color = color;
            var btn = go.GetComponent<Button>();
            btn.transition = Selectable.Transition.None;
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = 84; le.preferredHeight = 84;
            label = MakeText(go.transform, text, 34, TextAlignmentOptions.Center, 0);
            label.raycastTarget = false;
            var lrt = label.rectTransform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
            return btn;
        }

        private TMP_Text MakeText(Transform parent, string text, float size, TextAlignmentOptions align, float preferredHeight)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.alignment = align; t.color = Color.white;
            t.enableWordWrapping = false;
            if (font != null) t.font = font;
            if (preferredHeight > 0f)
            {
                var le = go.AddComponent<LayoutElement>();
                le.minHeight = preferredHeight; le.preferredHeight = preferredHeight;
            }
            return t;
        }
    }
}
