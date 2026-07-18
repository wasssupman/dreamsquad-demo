using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wassup.Data;

namespace Wassup.UI
{
    // dreamcatcher-deck-page unit 0 — the left 1/3 detail: card art backdrop + a
    // unified card (name / category badge / effect text via DreamcatcherCardText.Body
    // / add·remove action + hint). Mirrors SquadUnitDetailView but simpler — the
    // image is a static Sprite (no live Spine). ActionClicked is interpreted by the
    // orchestrator (add a copy vs remove the selected deck slot).
    public class DreamcatcherCardDetailView : MonoBehaviour
    {
        [SerializeField] private Image artImage;      // backdrop (scene/builder wired)
        [SerializeField] private RectTransform cardRoot;
        [SerializeField] private TMP_FontAsset font;

        public event Action ActionClicked;

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
        private Image _actionBg;
        private TMP_Text _actionLabel;
        private TMP_Text _hintText;

        public void ShowCard(DreamcatcherCard card, bool deckSlotMode, bool canAdd, string hint)
        {
            EnsureBuilt();
            BindArt(card);

            if (_nameText != null)
                _nameText.text = card == null ? "" : (string.IsNullOrEmpty(card.displayName) ? card.id : card.displayName);
            if (_catBadgeBg != null) _catBadgeBg.color = CardCategoryStyle.Frame(card);
            if (_catBadgeText != null) _catBadgeText.text = CardCategoryStyle.Label(card);
            if (_effectText != null) _effectText.text = card == null ? "" : DreamcatcherCardText.Body(card);

            if (deckSlotMode)
            {
                if (_actionLabel != null) _actionLabel.text = "덱에서 제거";
                if (_actionBg != null) _actionBg.color = RemoveColor;
                SetActionInteractable(true);
                if (_hintText != null) _hintText.text = "";
            }
            else
            {
                if (_actionLabel != null) _actionLabel.text = "덱에 추가";
                if (_actionBg != null) _actionBg.color = canAdd ? AddColor : DisabledColor;
                SetActionInteractable(canAdd);
                if (_hintText != null) _hintText.text = hint ?? "";
            }
        }

        public void Clear()
        {
            EnsureBuilt();
            BindArt(null);
            if (_nameText != null) _nameText.text = "";
            if (_effectText != null) _effectText.text = "";
            if (_catBadgeText != null) _catBadgeText.text = "";
            if (_hintText != null) _hintText.text = "";
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

        private Button _actionButton;
        private void SetActionInteractable(bool on)
        {
            if (_actionButton != null) _actionButton.interactable = on;
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
            vlg.padding = new RectOffset(20, 20, 16, 16);
            vlg.spacing = 10;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;

            _nameText = MakeText(cardRoot, "", 32, TextAlignmentOptions.Left, 42);

            var badge = new GameObject("CatBadge", typeof(RectTransform), typeof(Image));
            badge.transform.SetParent(cardRoot, false);
            _catBadgeBg = badge.GetComponent<Image>();
            var ble = badge.AddComponent<LayoutElement>();
            ble.minWidth = 120; ble.preferredWidth = 120; ble.minHeight = 30; ble.preferredHeight = 30;
            _catBadgeText = MakeText(badge.transform, "", 18, TextAlignmentOptions.Center, 0);
            var brt = _catBadgeText.rectTransform;
            brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one; brt.offsetMin = Vector2.zero; brt.offsetMax = Vector2.zero;

            _effectText = MakeText(cardRoot, "", 20, TextAlignmentOptions.TopLeft, 120);
            _effectText.enableWordWrapping = true;

            _hintText = MakeText(cardRoot, "", 16, TextAlignmentOptions.Left, 24);
            _hintText.color = HintColor;
            _hintText.fontStyle = FontStyles.Italic;

            var btnGo = new GameObject("Action", typeof(RectTransform), typeof(Image), typeof(Button));
            btnGo.transform.SetParent(cardRoot, false);
            _actionBg = btnGo.GetComponent<Image>();
            _actionBg.color = AddColor;
            _actionButton = btnGo.GetComponent<Button>();
            _actionButton.transition = Selectable.Transition.None;
            var le = btnGo.AddComponent<LayoutElement>();
            le.minHeight = 52; le.preferredHeight = 52;
            _actionButton.onClick.AddListener(() => ActionClicked?.Invoke());
            _actionLabel = MakeText(btnGo.transform, "덱에 추가", 24, TextAlignmentOptions.Center, 0);
            _actionLabel.raycastTarget = false;
            var lrt = _actionLabel.rectTransform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
        }

        private TMP_Text MakeText(Transform parent, string text, int size, TextAlignmentOptions align, float preferredHeight)
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
