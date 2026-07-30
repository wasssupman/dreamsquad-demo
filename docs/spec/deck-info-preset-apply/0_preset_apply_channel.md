# 0 — PresetApply 부활 (예약 채널 + 적용 규칙)

## 목적

팝업(요청)과 페이지(생성)를 잇는 **한 슬롯 예약 채널**, 프리셋 이름 규칙, 그리고 "내가 쓸 수 있는 것만 남기는" 적용 필터를 한 파일에 모은다. 전부 EditMode 로 고정한다 — MonoBehaviour 밖에 두는 이유가 그것이다.

## 변경 대상

- 신규: `Assets/_Project/Scripts/Core/Profile/PresetApply.cs` (`Wassup.Core`)
- 신규: `Assets/_Project/Tests/EditMode/Profile/PresetApplyTests.cs`

## 구현

```csharp
public static class PresetApply
{
    public enum Target { Squad, Dreamcatcher }

    public class Request
    {
        public Target target;
        public string presetName;
        public List<string> unitIds;    // Squad 만
        public List<string> stoneIds;   // Squad 만
        public List<string> cardIds;    // Dreamcatcher 만
    }

    public static bool HasPending { get; }
    public static void Stage(Request request);          // 기존 예약을 덮는다
    public static bool TryConsume(Target target, out Request request);
    public static void Clear();

    public static string DeckName(string ownerName);
    public static string UniqueName(IReadOnlyList<string> existingNames, string desired);

    public static List<string> FilterUnits(IReadOnlyList<string> ids, DefenderCatalog c, out int dropped);
    public static List<string> FilterStones(IReadOnlyList<string> ids, DreamstoneCatalog c, out int dropped);
    public static List<string> FilterCards(IReadOnlyList<string> ids, DreamcatcherCardCatalog c, out int dropped);
}
```

**왜 `TournamentDeckInfo.Payload` 를 그대로 싣지 않는가** (spec 리뷰 2026-07-31 — 질문받고 명문화). 세 이유:

1. **소유가 다르다.** `Payload` 는 서버 wire 계약이다 — `v` 버전 게이트를 갖고, 모양이 서버 포맷을 따라간다. `Request` 는 프로필 어휘(`SquadPreset.unitIds`/`stoneIds`, `DreamcatcherPreset.cardIds` 와 같은 plain id 리스트)다. deckInfo 가 v2 로 모양이 바뀌어도 프리셋 채널은 무변경이고, 번역 지점은 패널 하나 — 이미 `Deserialize` 를 부르는 곳이다.
2. **결합이 는다.** `Payload` 를 실으면 두 페이지 컨트롤러가 `Wassup.Core.Api` 를 새로 참조한다(현재 0건) — 프로필 페이지가 토너먼트 wire 포맷에 묶인다. 그리고 미래 소스(내 덱 미리보기·결과 화면·공유 코드)는 wire 포맷 객체를 **지어내야** 예약할 수 있게 된다.
3. **절약이 없다.** `target`·`presetName` 은 `Payload` 에 없으므로 어차피 래퍼 타입이 하나 필요하다. 래퍼 안에 `Payload` 를 넣으나 리스트를 넣으나 타입 수는 같고, 리스트 쪽은 스쿼드 적용에 `dc.cards` 를 죽은 짐으로 끌고 가지 않는다.

`SquadPreset`/`DreamcatcherPreset` 을 직접 싣지도 않는다 — persisted 타입이라 id/name 이 반쯤 채워진 객체가 돌아다니게 되고, 실수로 리스트에 직접 `Add` 하면 `CreatePreset`/`NormalizePresets` 를 우회한다. `Request` 는 의도적으로 **불활성**(프로필에 꽂을 수 없는 plain 데이터)이다.

**예약은 슬롯 하나다.** `TryConsume` 은 예약이 있으면 **대상이 맞든 틀리든 지운다** — 맞으면 돌려주고 `true`, 틀리면 `false`. 대상이 틀렸을 때 남겨두면 그 예약이 한참 뒤 엉뚱한 진입에서 되살아나 그때의 편집과 충돌한다(계약 6). 정상 경로는 예약 직후 그 페이지로 이동하므로 첫 진입이 곧 주인이다.

**`Stage` 는 리스트를 복제해서 담는다** — 패널이 넘기는 것은 `Payload` 안의 살아 있는 리스트라, 참조를 공유하면 채널이 남의 객체 수명에 묶인다(`CopySlots` 의 "복제, 참조 공유 금지" 규율과 동일).

**static 리셋 필수.** 이 에디터는 도메인 리로드 off(`m_EnterPlayModeOptions: 1`)라 static 슬롯이 **Play 세션을 넘어 살아남는다** — 예약만 하고 Play 를 끄면 다음 Play 의 첫 페이지 진입에서 유령 프리셋이 생긴다. `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` 로 `Clear()` 한다(NoticePopup 부트스트랩과 같은 패턴).

`Wassup.Data` 를 참조한다(카탈로그·`DeckRules`). 옛 `PresetApply` 는 이 using 을 피했지만 그건 테스트 의존 최소화 규약이었고, 지금은 "쓸 수 있는가" 판정 자체가 카탈로그 조회다. 런타임은 단일 `Wassup.Runtime.asmdef` 라 어셈블리 경계 문제는 없다.

**이름**: `DeckName(owner)` → `owner` 가 비면 `"불러온 덱"`, 아니면 `$"{owner.Trim()}의 덱"`. `UniqueName` 은 desired 가 목록에 없으면 그대로, 있으면 `" 2"` 부터 빈 번호를 찾는다.

**필터** — 순서를 보존하며 훑고, `dropped` = (빈 문자열 제외한 입력 수) − (남은 수):

- `FilterUnits`: 카탈로그 해석 + **중복 제거**(첫 등장 유지 — `ToggleUnit` 이 같은 유닛의 두 번째 편성을 막는다) + `SquadPreset.SlotCount` 까지.
- `FilterStones`: 카탈로그 해석 + **중복 유지**(4x 동일 유니크 스톤은 설계상 허용) + `StoneSlotCount` 까지.
- `FilterCards`: 카탈로그 해석 + `visible != 0`(숨김 카드는 로그인 prune 이 어차피 떼어낸다) + `category != Subconscious`(선물 전용, 추가 불가 풀) + 중복 제거 + `DeckRules.EffectiveDeckSize` 상한 + 타입별 `DeckRules.EffectiveMax`. 판정 기준을 페이지의 `CanAdd` 와 일치시켜, 적용 결과가 그 페이지에서 손으로 만들 수 있는 덱과 같아지게 한다.
- **카탈로그가 null 이면 전량 제외**(`dropped` = 입력 수). 미배선을 조용한 빈 프리셋으로 위장하지 않는다 — 픽업이 안내를 띄운다.

## 완료 기준

- [x] 컴파일 그린
- [x] EditMode 테스트 통과:
  - `Stage` → `TryConsume(같은 대상)` = true + 내용 일치, 두 번째 호출은 false
  - `Stage(Squad)` → `TryConsume(Dreamcatcher)` = false **이고 예약이 사라진다**(`HasPending` false)
  - `Stage` 두 번 → 뒤엣것만 남는다
  - `Stage` 후 원본 리스트를 변조해도 예약 내용은 불변(복제 확인)
  - `Clear()` → `HasPending` false (리셋 메서드가 곧 도메인 리로드 off 대비 훅)
  - `DeckName`: 정상 / 공백 / null → `"불러온 덱"`
  - `UniqueName`: 미충돌 그대로 / 1회 충돌 → ` 2` / `2` 까지 있으면 → ` 3`
  - `FilterUnits`: 미해석 제외 + dropped 수 · 중복 제거 · 8개 이상 입력 → 7개
  - `FilterStones`: 같은 id 4개 → 4개 유지 · 5개 → 4개
  - `FilterCards`: 상한 초과 잘림 · 타입 제한 초과 제외 · `visible == 0` 제외 · `Subconscious` 제외 · 중복 제외
  - 세 필터 모두 `catalog == null` → 빈 리스트 + dropped = 입력 수, 예외 없음
  - 입력 `null` → 빈 리스트 + dropped 0
