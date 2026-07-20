using System.Collections.Generic;

namespace Wassup.Core
{
    // loadout-preset-page unit 1 — 프리셋의 유닛/카드 id 를 프로필의 "선택된" 스쿼드·덱에 기록하는
    // 순수 변이 헬퍼. 디스크 저장은 하지 않는다(호출처가 ProfileStore.Save). 스톤 4슬롯은 미변경.
    // SO→id 매핑은 호출처(UI 컨트롤러) 책임 — 이 헬퍼는 id 문자열만 받아 EditMode 순수 테스트 가능.
    public static class PresetApply
    {
        // 선택 스쿼드의 7슬롯에 unitIds 를, 선택 덱에 cardIds 를 기록. 선택 스쿼드/덱이 없으면 no-op(false).
        // 기본 로드아웃(squad_1/deck_1) 주입은 ProfileStore(EnsureDefaultSquad/EnsureDefaultDeck, load 시점)
        // 단독 소유 — 프리셋 적용은 그 위에 덮어쓰기만 하고 기본값을 생성하지 않는다.
        public static bool WriteToProfile(PlayerProfile profile,
            IReadOnlyList<string> unitIds, IReadOnlyList<string> cardIds)
        {
            if (profile == null) return false;

            // 전제: 선택 스쿼드·덱은 이미 존재한다(ProfileStore 가 신규유저 load 시점에 주입). 둘 중 하나라도
            // 없으면 부분 적용 없이 no-op — 기본값 생성은 여기 책임이 아니다(가드가 쓰기보다 앞 = 원자성).
            var squad = profile.SelectedSquad();
            var deck = profile.SelectedDeck();
            if (squad == null || deck == null) return false;

            // 유닛: 정확히 7슬롯. 초과분은 무시, 부족분/null 은 "" (스톤은 NormalizeSlots 가 보존만).
            squad.NormalizeSlots();
            for (int i = 0; i < SquadSave.SlotCount; i++)
            {
                bool has = unitIds != null && i < unitIds.Count && unitIds[i] != null;
                squad.unitIds[i] = has ? unitIds[i] : "";
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
