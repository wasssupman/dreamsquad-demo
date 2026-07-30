using System.Collections.Generic;
using Wassup.Data;

namespace Wassup.Core
{
    // dreamcatcher-card-visibility unit 2 — 저장 덱에서 숨김 카드(visible == 0)를
    // 걷어내는 순수 판정. 아키텍처 타입에 의존하지 않는 값 연산이라 static 순수 함수로
    // 두고 EditMode 로 검증한다(제약 10). 호출처가 결과를 보고 저장 여부를 정한다.
    public static class DeckPrune
    {
        // 프로필의 모든 덱에서 숨김 카드를 제거하고 제거한 장수를 돌려준다.
        // 0 = 바뀐 것 없음 → 호출처는 저장하지 않는다.
        //
        // page-local-presets unit 5 — "모든 덱"은 이제 **프리셋 30개 전체**를 뜻한다.
        // 순회가 확정분 하나로 좁혀져 있지 않으므로 별도 대응이 필요 없다. 확정분만 훑으면
        // 시트에서 숨긴 카드가 나머지 프리셋에 영구 잔존한다 — 그 좁히기를 하지 말 것.
        // 프루닝은 시스템 주도 변경이라 플레이어의 [저장] 대상이 아니고 즉시 저장이 맞다.
        // 호출 시점은 로그인 직후(HiddenCardDeckPruner)라 페이지 진입 전이므로 편집 중인
        // 작업본과 충돌하지 않는다.
        //
        // 카탈로그가 모르는 id 는 남긴다: 미해결 id 는 이 기능의 관심사가 아니고,
        // 조용히 지우면 LoadoutGate 의 "카탈로그가 모르는 id" 진단이 사라진다.
        public static int RemoveHiddenCards(PlayerProfile profile, DreamcatcherCardCatalog catalog)
        {
            if (profile == null || catalog == null) return 0;      // 배선 오류로 덱을 훼손하지 않는다
            if (profile.dreamcatcherDecks == null) return 0;

            int removed = 0;
            foreach (var deck in profile.dreamcatcherDecks)
            {
                if (deck?.cardIds == null) continue;
                removed += RemoveHiddenFrom(deck.cardIds, catalog);
            }
            return removed;
        }

        // 한 덱의 id 리스트에서 숨김 카드를 제거한다. 뒤에서부터 지워 인덱스가 밀리지 않게 한다.
        private static int RemoveHiddenFrom(List<string> cardIds, DreamcatcherCardCatalog catalog)
        {
            int removed = 0;
            for (int i = cardIds.Count - 1; i >= 0; i--)
            {
                var card = catalog.ById(cardIds[i]);
                if (card == null || card.visible != 0) continue;   // 미해결 id 는 보존
                cardIds.RemoveAt(i);
                removed++;
            }
            return removed;
        }
    }
}
