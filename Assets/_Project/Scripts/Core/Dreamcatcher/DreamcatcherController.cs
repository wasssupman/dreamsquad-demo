using System.Collections.Generic;
using UnityEngine;
using Wassup.Bridge;
using Wassup.Core.TimeControl;
using Wassup.Data;
using Wassup.UI;

namespace Wassup.Core
{
    // ingame-dreamcatcher Unit 3 — drives the in-game card selection. Subscribes to
    // BattleBridge triggers (first placement + every 5th wave), draws 3 cards from
    // the deck, and applies the chosen one. Scene-local MonoBehaviour, not a
    // singleton. Unit 4 wires the selection UI; until then OnTrigger auto-picks the
    // first drawn card so the flow is testable.
    public class DreamcatcherController : MonoBehaviour
    {
        [SerializeField] private BattleBridge bridge;
        [SerializeField] private DreamcatcherDeck deck;
        [SerializeField] private DreamcatcherSelectionView selectionView;
        // dreamcatcher-deck-builder Unit 3 — saved-deck carry-in. When the profile
        // has a valid selected deck, draws come from it; otherwise from the
        // serialized `deck` asset (C fallback).
        [SerializeField] private PlayerProfileSO profileSO;
        [SerializeField] private DreamcatcherCardCatalog cardCatalog;

        private List<DreamcatcherCard> _resolvedDeck;
        // time-manager Unit 5 — 카드 선택 중 전투 정지를 TimeManager lease 로 소유(구 Time.timeScale=0
        // 대체). Battle 도메인만 멈추고 선택 UI 는 실시간으로 살아있다.
        private TimeLease _pauseLease;

        private void OnEnable()
        {
            // First pick happens BEFORE placement (changed flow: 맵 설정 → 첫 드캐
            // 선택 → 배치). Entering the Placement phase is the trigger; it fires
            // once per match (SetPhase only fires on change) and works for both
            // squad and draft entries. The 5-wave picks come from the bridge.
            if (GameManager.Instance != null)
                GameManager.Instance.PhaseChanged += OnPhaseChanged;
            if (bridge != null)
                bridge.WaveMilestoneReached += OnWaveMilestone;
        }

        private void OnDisable()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.PhaseChanged -= OnPhaseChanged;
            if (bridge != null)
                bridge.WaveMilestoneReached -= OnWaveMilestone;
            // time-manager Unit 5 — 선택 도중 비활성화돼도 정지 lease 가 남지 않도록 해제(멱등).
            _pauseLease.Dispose();
        }

        private void OnPhaseChanged(GamePhase phase)
        {
            if (phase != GamePhase.Placement) return;
            _resolvedDeck = ResolveDeck(); // fresh resolve per match entry
            OnSelectionTrigger();
        }

        // Selected saved deck (validated) resolved via the card catalog; falls back
        // to the serialized deck asset when no valid saved deck exists.
        private List<DreamcatcherCard> ResolveDeck()
        {
            var save = (profileSO != null && profileSO.profile != null) ? profileSO.profile.SelectedDeck() : null;
            if (save != null && cardCatalog != null && DeckRules.Validate(save.cardIds, cardCatalog, out _))
            {
                var list = new List<DreamcatcherCard>(save.cardIds.Count);
                foreach (var id in save.cardIds)
                {
                    var card = cardCatalog.ById(id);
                    if (card != null) list.Add(card);
                }
                if (list.Count > 0) return list;
            }
            // fallback: serialized default deck
            var fallback = new List<DreamcatcherCard>();
            if (deck != null && deck.cards != null)
                foreach (var c in deck.cards) if (c != null) fallback.Add(c);
            return fallback;
        }

        private void OnWaveMilestone(int waveNumber) => OnSelectionTrigger();

        private void OnSelectionTrigger()
        {
            var three = Draw3();
            if (three.Count == 0) return;

            if (selectionView != null)
            {
                // Pause so enemies/cooldowns freeze while the player chooses.
                // 전투 도메인만 정지(priority 100) — 선택 UI 는 인터랙션 도메인이라 실시간 유지.
                _pauseLease.Dispose(); // 혹시 남은 lease 정리(멱등)
                _pauseLease = TimeManager.Instance.Request(TimeDomain.Battle, 0f, priority: 100);
                selectionView.Show(three, OnPicked);
            }
            else
            {
                // No UI wired (tests / headless): auto-pick the first draw.
                Pick(three[0]);
            }
        }

        private void OnPicked(DreamcatcherCard card)
        {
            _pauseLease.Dispose(); // 전투 재개
            Pick(card);
        }

        // Samples up to 3 cards from the resolved deck. Duplicate deck entries stay
        // as independent draws (they stack on pick).
        private List<DreamcatcherCard> Draw3()
        {
            var result = new List<DreamcatcherCard>();
            if (_resolvedDeck == null) _resolvedDeck = ResolveDeck();
            var src = _resolvedDeck;
            if (src == null || src.Count == 0) return result;

            var idx = new List<int>();
            for (int i = 0; i < src.Count; i++)
                if (src[i] != null) idx.Add(i);

            int take = Mathf.Min(3, idx.Count);
            for (int i = 0; i < take; i++)
            {
                int j = Random.Range(i, idx.Count);
                (idx[i], idx[j]) = (idx[j], idx[i]);
                result.Add(src[idx[i]]);
            }
            return result;
        }

        public void Pick(DreamcatcherCard card)
        {
            if (card == null || bridge == null) return;
            bridge.ApplyDreamcatcherCard(card);
        }
    }
}
