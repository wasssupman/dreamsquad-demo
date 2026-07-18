using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wassup.Data;

namespace Wassup.UI
{
    // dreamcatcher-deck-page unit 2 — the deck tray: 10 slots + validity status
    // + Save button (gated on DeckRules.Validate). Parity with the old view's
    // "MY DECK" frame + statusText + save-only-when-valid. Slot tap raises
    // SlotTapped(index); the orchestrator shows that card in the detail (remove mode).
    public class DreamcatcherDeckStrip : MonoBehaviour
    {
        [SerializeField] private DreamcatcherCardCatalog catalog;
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private Vector2 slotSize = new Vector2(70, 100);

        public event Action<int> SlotTapped;
        public event Action SaveClicked;

        private static readonly Color EmptySlot = new Color(0.16f, 0.18f, 0.24f, 1f);
        private static readonly Color SaveOn = new Color(0.20f, 0.55f, 0.28f, 1f);
        private static readonly Color SaveOff = new Color(0.28f, 0.30f, 0.36f, 1f);

        private class SlotW { public Image bg; public Image art; public TMP_Text plus; }

        private bool _built;
        private readonly List<SlotW> _slots = new List<SlotW>();
        private TMP_Text _statusText;
        private Image _saveBg;
        private Button _saveButton;

        public void Refresh(List<string> cardIds)
        {
            EnsureBuilt();
            int deckSize = DeckRules.EffectiveDeckSize(catalog);
            for (int i = 0; i < _slots.Count; i++)
            {
                string id = (cardIds != null && i < cardIds.Count) ? cardIds[i] : "";
                var card = (!string.IsNullOrEmpty(id) && catalog != null) ? catalog.ById(id) : null;
                bool filled = card != null;
                _slots[i].bg.color = filled ? CardCategoryStyle.Frame(card) : EmptySlot;
                if (filled && card.art != null) { _slots[i].art.sprite = card.art; _slots[i].art.color = Color.white; _slots[i].art.enabled = true; }
                else if (filled) { _slots[i].art.sprite = null; _slots[i].art.color = CardCategoryStyle.ArtFallback(card); _slots[i].art.enabled = true; }
                else { _slots[i].art.enabled = false; }
                _slots[i].plus.enabled = !filled;
            }

            int count = cardIds != null ? cardIds.Count : 0;
            bool valid = DeckRules.Validate(cardIds ?? new List<string>(), catalog, out string reason);
            int sq = DeckRules.SquadCount(cardIds ?? new List<string>(), catalog);
            int sqMax = DeckRules.EffectiveMax(catalog, CardType.Squad);
            if (_statusText != null)
                _statusText.text = count + "/" + deckSize + "  ·  squad " + sq + "/" + sqMax + "  ·  " + reason;
            if (_saveButton != null) _saveButton.interactable = valid;
            if (_saveBg != null) _saveBg.color = valid ? SaveOn : SaveOff;
        }

        private void EnsureBuilt()
        {
            if (_built) return;
            _built = true;

            var hlg = gameObject.GetComponent<HorizontalLayoutGroup>();
            if (hlg == null) hlg = gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(12, 12, 8, 8);
            hlg.spacing = 6;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = false; hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;

            int deckSize = DeckRules.EffectiveDeckSize(catalog);
            for (int i = 0; i < deckSize; i++)
            {
                int index = i;
                _slots.Add(MakeSlot(() => SlotTapped?.Invoke(index)));
            }

            // status label (flexible width)
            var statusGo = new GameObject("Status", typeof(RectTransform), typeof(TextMeshProUGUI));
            statusGo.transform.SetParent(transform, false);
            _statusText = statusGo.GetComponent<TextMeshProUGUI>();
            _statusText.alignment = TextAlignmentOptions.MidlineLeft; _statusText.fontSize = 20; _statusText.color = Color.white;
            _statusText.enableWordWrapping = false;
            if (font != null) _statusText.font = font;
            var sle = statusGo.AddComponent<LayoutElement>();
            sle.flexibleWidth = 1f; sle.minWidth = 200; sle.minHeight = slotSize.y;

            // Save button
            var saveGo = new GameObject("Save", typeof(RectTransform), typeof(Image), typeof(Button));
            saveGo.transform.SetParent(transform, false);
            _saveBg = saveGo.GetComponent<Image>();
            _saveBg.color = SaveOff;
            _saveButton = saveGo.GetComponent<Button>();
            _saveButton.transition = Selectable.Transition.None;
            _saveButton.onClick.AddListener(() => SaveClicked?.Invoke());
            var svle = saveGo.AddComponent<LayoutElement>();
            svle.minWidth = 140; svle.preferredWidth = 140; svle.minHeight = 56; svle.preferredHeight = 56;
            var saveLabel = MakeText(saveGo.transform, "저장", 24, TextAlignmentOptions.Center);
            saveLabel.raycastTarget = false;
            var srt = saveLabel.rectTransform;
            srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one; srt.offsetMin = Vector2.zero; srt.offsetMax = Vector2.zero;

            UiLayer.Apply(gameObject);
        }

        private SlotW MakeSlot(Action onTap)
        {
            var root = new GameObject("Slot", typeof(RectTransform), typeof(Image), typeof(Button));
            root.transform.SetParent(transform, false);
            ((RectTransform)root.transform).sizeDelta = slotSize;
            var bg = root.GetComponent<Image>();
            bg.color = EmptySlot;
            root.GetComponent<Button>().onClick.AddListener(() => onTap());

            var artGo = new GameObject("Art", typeof(RectTransform), typeof(Image));
            artGo.transform.SetParent(root.transform, false);
            var art_rt = (RectTransform)artGo.transform;
            art_rt.anchorMin = Vector2.zero; art_rt.anchorMax = Vector2.one;
            art_rt.offsetMin = new Vector2(4, 4); art_rt.offsetMax = new Vector2(-4, -4);
            var art = artGo.GetComponent<Image>();
            art.preserveAspect = true; art.raycastTarget = false; art.enabled = false;

            var plusGo = new GameObject("Plus", typeof(RectTransform), typeof(TextMeshProUGUI));
            plusGo.transform.SetParent(root.transform, false);
            var prt = (RectTransform)plusGo.transform;
            prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one; prt.offsetMin = Vector2.zero; prt.offsetMax = Vector2.zero;
            var plus = plusGo.GetComponent<TextMeshProUGUI>();
            plus.text = "+"; plus.alignment = TextAlignmentOptions.Center; plus.fontSize = 28; plus.color = new Color(1, 1, 1, 0.55f);
            plus.raycastTarget = false;
            if (font != null) plus.font = font;

            return new SlotW { bg = bg, art = art, plus = plus };
        }

        private TMP_Text MakeText(Transform parent, string text, int size, TextAlignmentOptions align)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.alignment = align; t.color = Color.white;
            if (font != null) t.font = font;
            return t;
        }
    }
}
