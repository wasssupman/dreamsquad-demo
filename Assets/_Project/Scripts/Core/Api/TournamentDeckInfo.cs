using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Wassup.Core.Api
{
    // tournament-deck-info unit 0 — `complete` 요청의 `deckInfo` 문자열 계약.
    //
    // 서버는 이 값을 **opaque string** 으로만 다룬다(스키마 없음, 검증 없음). 즉
    // 포맷은 클라가 정의하고, 한 번 내보낸 뒤로는 과거 엔트리가 그 포맷으로 굳는다.
    // 그래서 `v` 를 맨 앞에 두고, 파싱은 버전이 맞을 때만 성립시킨다.
    //
    // **id 만 담는다.** 표시명/아트/등급은 카탈로그(DefenderCatalog·DreamstoneCatalog·
    // DreamcatcherCardCatalog)의 ById 가 해석한다 — 로컬 덱 UI 가 이미 id 배열만으로
    // 그리는 것과 같은 경로다. 이름을 스냅샷하지 않는 이유는 `displayName` 이 시트
    // 구동이기 때문이다: 이름을 페이로드에 굳히면 시트에서 이름을 바꾼 순간 옛
    // 엔트리만 옛 이름으로 남아 한 화면에 두 이름이 공존한다.
    public static class TournamentDeckInfo
    {
        public const int Version = 1;

        [Serializable]
        public class SquadDeck
        {
            public List<string> units;   // DefenderUnitData.id
            public List<string> stones;  // DreamstoneData.id
        }

        [Serializable]
        public class DreamcatcherDeck
        {
            public List<string> cards;   // DreamcatcherCard.id
        }

        [Serializable]
        public class Payload
        {
            public int v;
            public SquadDeck squad;
            public DreamcatcherDeck dc;
        }

        // 세 목록이 모두 비면 **빈 문자열**을 돌려준다 — 서버의 "기록 없음"(null)과
        // 동치다. `{"v":1, 빈 배열들}` 을 올리면 "빈 덱으로 플레이함"으로 읽혀 미기록과
        // 구분되지 않는다.
        public static string Serialize(IEnumerable<string> unitIds,
                                       IEnumerable<string> stoneIds,
                                       IEnumerable<string> cardIds)
        {
            var units = Compact(unitIds);
            var stones = Compact(stoneIds);
            var cards = Compact(cardIds);
            if (units.Count == 0 && stones.Count == 0 && cards.Count == 0) return string.Empty;

            return JsonConvert.SerializeObject(new Payload
            {
                v = Version,
                squad = new SquadDeck { units = units, stones = stones },
                dc = new DreamcatcherDeck { cards = cards },
            });
        }

        // 관대한 역직렬화 — 실패는 예외가 아니라 null 이다. 남의 엔트리에서 오는 값이라
        // 빈 값(구 참가)·깨진 값·미래 버전이 전부 정상 입력이다. 성공 시 누락 노드를 빈
        // 리스트로 정규화해 소비처가 null 체크를 반복하지 않게 한다.
        public static Payload Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;

            Payload payload;
            try
            {
                payload = JsonConvert.DeserializeObject<Payload>(json);
            }
            catch (Exception)
            {
                return null;
            }

            // 미래 버전은 null — 구버전 클라가 v2 를 v1 로 오해석하면 있지도 않은 슬롯을
            // 그린다. **과거 버전은 받는다**: v 를 올린 뒤에도 그 이전 기록이 계속 읽혀야
            // 한다(누락 필드는 아래 정규화가 빈 리스트로 흡수). 여기서 하한까지 막으면
            // v 는 확장의 탈출구가 아니라 백카탈로그를 통째로 끊는 단절선이 된다.
            if (payload == null || payload.v < 1 || payload.v > Version) return null;

            if (payload.squad == null) payload.squad = new SquadDeck();
            if (payload.dc == null) payload.dc = new DreamcatcherDeck();
            // 원소 수준까지 정규화한다 — 우리 Serialize 는 빈 원소를 만들지 않지만,
            // 남의 엔트리에서 오는 값이라 [null, "u1"] 같은 배열이 도달할 수 있고 그건
            // 그대로 카탈로그 조회로 흘러간다.
            payload.squad.units = Compact(payload.squad.units);
            payload.squad.stones = Compact(payload.squad.stones);
            payload.dc.cards = Compact(payload.dc.cards);
            return payload;
        }

        // 빈 슬롯을 버리고 순서를 보존한다. 스쿼드/스톤 슬롯의 빈칸은 "" 로 저장되므로
        // (SquadSave.NormalizeSlots) 그대로 넣으면 배열에 빈 문자열이 섞인다. 배열
        // 순서가 곧 표시 순서라 압축은 하되 재정렬은 하지 않는다.
        private static List<string> Compact(IEnumerable<string> ids)
        {
            var list = new List<string>();
            if (ids == null) return list;
            foreach (var id in ids)
                if (!string.IsNullOrWhiteSpace(id)) list.Add(id);
            return list;
        }
    }
}
