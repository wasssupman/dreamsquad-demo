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
