using System.Collections.Generic;

namespace Wassup.Core
{
    // loadout-preset-page unit 1 — 프리셋의 유닛/카드 id 를 프로필의 "선택된" 스쿼드·덱에 기록하는
    // 순수 변이 헬퍼. 디스크 저장은 하지 않는다(호출처가 ProfileStore.Save). 스톤 4슬롯은 미변경.
    // SO→id 매핑은 호출처(UI 컨트롤러) 책임 — 이 헬퍼는 id 문자열만 받아 EditMode 순수 테스트 가능.
    public static class PresetApply
    {
        // 선택 덱이 없을 때 생성/선택할 기본 덱 id — DreamcatcherDeckPageController 의 find-or-create 와 동일.
        private const string DefaultDeckId = "deck_1";
        private const string DefaultDeckName = "Deck 1";

        // 선택 스쿼드의 7슬롯에 unitIds 를, 선택 덱에 cardIds 를 기록. profile/선택 스쿼드가 null 이면 false.
        public static bool WriteToProfile(PlayerProfile profile,
            IReadOnlyList<string> unitIds, IReadOnlyList<string> cardIds)
        {
            if (profile == null) return false;

            var squad = profile.SelectedSquad();
            if (squad == null) return false;

            // 유닛: 정확히 7슬롯. 초과분은 무시, 부족분/null 은 "" (스톤은 NormalizeSlots 가 보존만).
            squad.NormalizeSlots();
            for (int i = 0; i < SquadSave.SlotCount; i++)
            {
                bool has = unitIds != null && i < unitIds.Count && unitIds[i] != null;
                squad.unitIds[i] = has ? unitIds[i] : "";
            }

            // 덱: 선택 덱이 없으면 deck_1 을 찾거나 생성해 선택(계약 2 null-branch). 있으면 그대로 덮어쓰고 선택 유지.
            var deck = profile.SelectedDeck();
            if (deck == null)
            {
                if (profile.dreamcatcherDecks == null) profile.dreamcatcherDecks = new List<DeckSave>();
                for (int i = 0; i < profile.dreamcatcherDecks.Count; i++)
                {
                    var d = profile.dreamcatcherDecks[i];
                    if (d != null && d.id == DefaultDeckId) { deck = d; break; }
                }
                if (deck == null)
                {
                    deck = new DeckSave { id = DefaultDeckId, name = DefaultDeckName };
                    profile.dreamcatcherDecks.Add(deck);
                }
                profile.selectedDeckId = DefaultDeckId;
            }

            // 카드: 제공된 그대로 기록(캡·검증 없음, 계약 4). null 항목만 제외.
            var cards = new List<string>();
            if (cardIds != null)
                for (int i = 0; i < cardIds.Count; i++)
                    if (cardIds[i] != null) cards.Add(cardIds[i]);
            deck.cardIds = cards;

            return true;
        }
    }
}
