# 0 — deckInfo 페이로드 포맷 + 순수 시리얼/디시리얼

## 목적

서버가 `deckInfo` 를 opaque string 으로만 다루므로, **포맷은 클라가 정의하고 한 번 정하면 과거 기록이 그 포맷으로 굳는다.** v1 계약을 코드로 고정하고, 아키텍처를 모르는 순수 static 함수 한 쌍으로 만든다 (제약 10 — 비자명한 정규화 + 2 호출처 + 회귀 테스트 가치).

## 변경 대상

- 신규 `Assets/_Project/Scripts/Core/Api/TournamentDeckInfo.cs`
- 신규 `Assets/_Project/Tests/EditMode/Api/TournamentDeckInfoTests.cs`

## 구현

```csharp
namespace Wassup.Core.Api
{
    public static class TournamentDeckInfo
    {
        public const int Version = 1;

        [Serializable] public class SquadDeck   { public List<string> units; public List<string> stones; }
        [Serializable] public class Dreamcatcher{ public List<string> cards; }
        [Serializable] public class Payload     { public int v; public SquadDeck squad; public Dreamcatcher dc; }

        public static string  Serialize(IEnumerable<string> unitIds,
                                        IEnumerable<string> stoneIds,
                                        IEnumerable<string> cardIds);
        public static Payload Deserialize(string json);
    }
}
```

직렬화는 Newtonsoft (`TournamentApi` 선례, 같은 폴더).

**Serialize 규칙**

- 빈/공백 id 는 **버린다**. 스쿼드 슬롯의 빈칸은 `""` 로 저장되므로(`SquadSave.NormalizeSlots`) 그대로 넣으면 배열에 빈 문자열이 섞인다.
- **슬롯 순서를 보존한다.** 배열 순서가 곧 표시 순서다.
- 세 목록이 모두 비면 **빈 문자열을 반환한다.** 보낼 덱이 없다는 뜻이고, 서버의 "기록 없음"(null)과 동치다. `{"v":1,...빈배열...}` 을 올려 "빈 덱으로 플레이함"처럼 보이게 하지 않는다.

**Deserialize 규칙** (전부 예외 없이 `null` 반환)

- 입력이 null/빈/공백
- JSON 파싱 실패
- `v < 1 || v > Version` — **비대칭 게이트**다. 미래 버전은 막고(구버전 클라가 오해석하면 없는 슬롯을 그린다), **과거 버전은 받는다**. `Version` 을 올린 뒤에도 그 이전 기록이 계속 읽혀야 한다 — 하한까지 막으면 백카탈로그 전체가 "덱 정보 없음"이 된다.
- 성공 시 누락 노드(`squad`/`dc`/각 배열)를 **빈 리스트로 정규화**하고, 리스트 **원소**도 `Serialize` 와 같은 규칙으로 압축한다(null·공백 제거). 남의 엔트리에서 오는 값이라 `[null, "u1"]` 이 도달할 수 있고, 그대로 두면 카탈로그 조회로 흘러간다.

## 완료 기준

확인: 2026-07-30 EditMode green (`TournamentDeckInfoTests` 10건).

- [x] 컴파일 통과
- [x] EditMode 테스트 통과:
  - 라운드트립 — 세 목록이 순서까지 그대로 복원
  - 빈 문자열 슬롯이 배열에서 빠지고 나머지 순서는 유지
  - 세 목록 전부 비면 `Serialize` 가 빈 문자열 반환
  - `Deserialize`: 빈 문자열 / 깨진 JSON / `{"v":Version+1,...}` / `{"v":0}` → 전부 `null` (예외 없음)
  - `Deserialize`: `1..Version` 범위의 버전은 전부 수용 (과거 버전 하위호환)
  - `Deserialize`: `{"v":1}` 처럼 노드 누락 → `null` 아닌 Payload + 빈 리스트 3개
  - `Deserialize`: `[null,"u1",""]` 같은 원소 → `["u1"]` 로 압축
  - 직렬화 결과의 실제 키가 `v` / `squad.units` / `squad.stones` / `dc.cards` 인지 문자열로 확인 (필드명이 곧 서버에 굳는 계약이므로 리팩터로 조용히 바뀌면 안 된다)
