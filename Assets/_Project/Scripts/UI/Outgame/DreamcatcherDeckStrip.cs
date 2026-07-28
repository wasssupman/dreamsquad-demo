using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wassup.Data;

namespace Wassup.UI
{
    // dreamcatcher-deck-page unit 2 — the deck tray: 10 slots + validity status.
    // Slot tap raises
    // SlotTapped(index); the orchestrator shows that card in the detail (remove mode).
    public class DreamcatcherDeckStrip : MonoBehaviour
    {
        [SerializeField] private DreamcatcherCardCatalog catalog;
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private Vector2 slotSize = new Vector2(70, 100);
        public event Action<int> SlotTapped;

        private static readonly Color EmptySlot = new Color(0.16f, 0.18f, 0.24f, 1f);
        private static readonly Color SelectedOutline = new Color(1f, 0.9f, 0.45f, 1f); // same yellow as SquadHeaderStrip

        private class SlotW { public string id; public Image bg; public Image art; public TMP_Text plus; public GameObject outline; }

        private bool _built;
        private string _selectedCardId;
        private readonly List<SlotW> _slots = new List<SlotW>();
        private TMP_Text _statusText;

        public void Refresh(List<string> cardIds)
        {
            EnsureBuilt();
            int deckSize = DeckRules.EffectiveDeckSize(catalog);
            for (int i = 0; i < _slots.Count; i++)
            {
                string id = (cardIds != null && i < cardIds.Count) ? cardIds[i] : "";
                var card = (!string.IsNullOrEmpty(id) && catalog != null) ? catalog.ById(id) : null;
                bool filled = card != null;
                _slots[i].id = id;
                _slots[i].bg.color = filled ? CardCategoryStyle.Frame(card) : EmptySlot;
                if (filled && card.art != null) { _slots[i].art.sprite = card.art; _slots[i].art.color = Color.white; _slots[i].art.enabled = true; }
                else if (filled) { _slots[i].art.sprite = null; _slots[i].art.color = CardCategoryStyle.ArtFallback(card); _slots[i].art.enabled = true; }
                else { _slots[i].art.enabled = false; }
                _slots[i].plus.enabled = !filled;
            }

            // ui-polish 2026-07-18 — 레거시 "squad {n}/{max}" 표기 제거(Squad 타입 캡 은퇴,
            // EffectiveMax(Squad)=-1 로 의미 없던 잔재). 수량 = 총 장수/덱크기 + 유효성 사유만.
            int count = cardIds != null ? cardIds.Count : 0;
            DeckRules.Validate(cardIds ?? new List<string>(), catalog, out string reason);
            if (_statusText != null)
            {
                string status = count + "/" + deckSize;
                if (!string.IsNullOrEmpty(reason)) status += "  ·  " + reason;
                _statusText.text = status;
            }
            ApplySelection();
        }

        // unit 6 — outlines the deck slot holding the card shown in the detail
        // panel. null/undecked id clears (driven by the orchestrator).
        public void SetSelected(string cardId)
        {
            _selectedCardId = cardId;
            if (_built) ApplySelection();
        }

        private void ApplySelection()
        {
            for (int i = 0; i < _slots.Count; i++)
                if (_slots[i].outline != null)
                    _slots[i].outline.SetActive(
                        !string.IsNullOrEmpty(_selectedCardId) && _slots[i].id == _selectedCardId);
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
            _statusText.alignment = TextAlignmentOptions.MidlineLeft; _statusText.fontSize = 26; _statusText.color = Color.white;
            _statusText.enableWordWrapping = false;
            if (font != null) _statusText.font = font;
            var sle = statusGo.AddComponent<LayoutElement>();
            sle.flexibleWidth = 1f; sle.minWidth = 200; sle.minHeight = slotSize.y;

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

            var outlineGo = new GameObject("Outline", typeof(RectTransform), typeof(Image));
            outlineGo.transform.SetParent(root.transform, false);
            var ort = (RectTransform)outlineGo.transform;
            ort.anchorMin = Vector2.zero; ort.anchorMax = Vector2.one;
            ort.offsetMin = new Vector2(-4, -4); ort.offsetMax = new Vector2(4, 4);
            var oimg = outlineGo.GetComponent<Image>();
            oimg.color = SelectedOutline; oimg.raycastTarget = false;
            outlineGo.SetActive(false);

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
            plus.text = "+"; plus.alignment = TextAlignmentOptions.Center; plus.fontSize = 34; plus.color = new Color(1, 1, 1, 0.55f);
            plus.raycastTarget = false;
            if (font != null) plus.font = font;

            return new SlotW { bg = bg, art = art, plus = plus, outline = outlineGo };
        }

    }
}
