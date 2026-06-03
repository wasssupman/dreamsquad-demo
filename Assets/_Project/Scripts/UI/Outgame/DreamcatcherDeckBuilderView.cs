using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Wassup.Core;
using Wassup.Data;

namespace Wassup.UI
{
    // dreamcatcher-deck-builder Unit 2 — deck builder inside DreamcatcherPanel.
    // Tap owned cards to add (up to 10, unique<=2), tap deck slots to remove, SAVE
    // when valid. Persists to PlayerProfile.dreamcatcherDecks (deck_1). MVP: all 6
    // cards owned.
    public class DreamcatcherDeckBuilderView : MonoBehaviour
    {
        [SerializeField] private DreamcatcherCardCatalog catalog;
        [SerializeField] private PlayerProfileSO profileSO;
        [SerializeField] private RectTransform deckContainer;
        [SerializeField] private RectTransform ownedContainer;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Button saveButton;
        [SerializeField] private TMP_FontAsset font;

        private const string DeckId = "deck_1";
        private readonly List<string> _working = new List<string>();
        private bool _ownedBuilt;

        private static readonly Color NormalColor = new Color(0.18f, 0.32f, 0.62f, 1f);
        private static readonly Color UniqueColor = new Color(0.72f, 0.42f, 0.12f, 1f);

        private void OnEnable()
        {
            BuildOwnedOnce();
            LoadWorking();
            if (saveButton != null) saveButton.onClick.AddListener(OnSave);
            Refresh();
        }

        private void OnDisable()
        {
            if (saveButton != null) saveButton.onClick.RemoveListener(OnSave);
        }

        private void LoadWorking()
        {
            _working.Clear();
            var deck = (profileSO != null && profileSO.profile != null) ? profileSO.profile.SelectedDeck() : null;
            if (deck != null && deck.cardIds != null)
                foreach (var id in deck.cardIds) if (!string.IsNullOrEmpty(id)) _working.Add(id);
        }

        private void BuildOwnedOnce()
        {
            if (_ownedBuilt || catalog == null) return;
            _ownedBuilt = true;
            foreach (var id in catalog.AllIds())
            {
                var card = catalog.ById(id);
                string captured = id;
                var btn = CreateCardButton(ownedContainer, card, () => AddCard(captured));
            }
        }

        private void Refresh()
        {
            // rebuild deck slot views from _working
            for (int i = deckContainer.childCount - 1; i >= 0; i--) Destroy(deckContainer.GetChild(i).gameObject);
            for (int i = 0; i < _working.Count; i++)
            {
                var card = catalog != null ? catalog.ById(_working[i]) : null;
                int index = i;
                CreateCardButton(deckContainer, card, () => RemoveAt(index));
            }

            bool valid = DeckRules.Validate(_working, catalog, out var reason);
            int unique = DeckRules.UniqueCount(_working, catalog);
            if (statusText != null)
                statusText.text = $"{_working.Count}/{DeckRules.DeckSize}  ·  unique {unique}/{DeckRules.MaxUnique}  ·  {reason}";
            if (saveButton != null) saveButton.interactable = valid;
        }

        private void AddCard(string id)
        {
            if (catalog == null) return;
            if (_working.Count >= DeckRules.DeckSize) return;
            var card = catalog.ById(id);
            if (card == null) return;
            if (card.category == CardCategory.Unique && DeckRules.UniqueCount(_working, catalog) >= DeckRules.MaxUnique) return;
            _working.Add(id);
            Refresh();
        }

        private void RemoveAt(int index)
        {
            if (index < 0 || index >= _working.Count) return;
            _working.RemoveAt(index);
            Refresh();
        }

        private void OnSave()
        {
            if (profileSO == null || profileSO.profile == null) return;
            if (!DeckRules.Validate(_working, catalog, out _)) return;
            var profile = profileSO.profile;
            var deck = profile.SelectedDeck();
            if (deck == null || deck.id != DeckId)
            {
                deck = null;
                if (profile.dreamcatcherDecks != null)
                    foreach (var d in profile.dreamcatcherDecks) if (d != null && d.id == DeckId) deck = d;
                if (deck == null)
                {
                    deck = new DeckSave { id = DeckId, name = "Deck 1" };
                    profile.dreamcatcherDecks.Add(deck);
                }
            }
            deck.cardIds = new List<string>(_working);
            profile.selectedDeckId = DeckId;
            ProfileStore.Save(profile);
            if (statusText != null) statusText.text = "Saved";
        }

        private Button CreateCardButton(Transform parent, DreamcatcherCard card, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Card", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(150, 64);
            go.GetComponent<Image>().color = (card != null && card.category == CardCategory.Unique) ? UniqueColor : NormalColor;
            go.GetComponent<Button>().onClick.AddListener(onClick);
            var l = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            l.transform.SetParent(go.transform, false);
            var lrt = l.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.offsetMin = new Vector2(4, 4); lrt.offsetMax = new Vector2(-4, -4);
            var tmp = l.GetComponent<TextMeshProUGUI>();
            tmp.text = card != null ? Summary(card) : "?";
            tmp.alignment = TextAlignmentOptions.Center; tmp.fontSize = 16; tmp.color = Color.white;
            if (font != null) tmp.font = font;
            return go.GetComponent<Button>();
        }

        private static string Summary(DreamcatcherCard card)
        {
            return string.IsNullOrEmpty(card.displayName) ? card.id : card.displayName;
        }
    }
}
