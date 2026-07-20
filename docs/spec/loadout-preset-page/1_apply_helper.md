# 1 — 프리셋 적용 헬퍼 (Core)

## 목적

프리셋의 유닛/카드 id 리스트를 프로필의 **선택된** 스쿼드·덱에 기록하는 순수 변이 헬퍼. edge case
(선택 덱 없음, 스톤 보존, 슬롯 정규화)를 한곳에 모으고 EditMode 로 회귀 방지.

## 변경 대상

- 신규: `Assets/_Project/Scripts/Core/Profile/PresetApply.cs` (`Wassup.Core`)
- 신규 테스트: `Assets/_Project/Tests/EditMode/Profile/PresetApplyTests.cs`

## 구현

```csharp
public static class PresetApply
{
    // 선택된 스쿼드의 7슬롯에 unitIds 를, 선택된 덱에 cardIds 를 기록. 스톤은 건드리지 않음.
    // 디스크 저장은 하지 않음(호출처가 ProfileStore.Save). profile/선택 스쿼드가 null 이면 false.
    public static bool WriteToProfile(PlayerProfile profile,
        IReadOnlyList<string> unitIds, IReadOnlyList<string> cardIds);
}
```

로직:

1. `profile == null` → false.
2. `squad = SelectedSquad()`, `deck = SelectedDeck()` 를 먼저 얻고 **둘 중 하나라도 null 이면 false**
   (가드가 쓰기보다 앞 = 원자성, 부분 적용 없음). **기본값을 생성하지 않는다** — 신규유저 기본 로드아웃
   (squad_1/deck_1)은 `ProfileStore.EnsureDefaultSquad/EnsureDefaultDeck`(load 시점, `DreamcatcherDeck_Default`
   시드)가 단독 주입하므로, 적용 시점엔 이미 존재한다. 여기서 deck_1 을 재생성하면 그 책임을 중복 소유하게 됨.
3. `squad.NormalizeSlots()` 후 `squad.unitIds[i]` = `unitIds[i]`(없으면 `""`), i = 0..`SquadSave.SlotCount-1`.
   → **스톤 슬롯은 미변경**(계약 3).
4. `deck.cardIds` = 제공된 카드 **그대로** 기록(null 항목만 제외). 카드는 슬롯 배열이 아닌 가변 길이 리스트라
   유닛(7슬롯 캡)과 달리 캡하지 않는다(구조 차이). 덱 규칙 검증도 없음(계약 4) — authoring 책임이며,
   라이브 `EffectiveDeckSize`(현 10, 데이터 주도값)와 다른 장수면 START 게이트가 잡는다.
5. return true. `selectedDeckId`/`selectedSquadId` 는 건드리지 않는다(선택 대상 불변).

원칙:

- 순수 값 변이(제약 10) — `EntityManager`/`Time`/디스크 접근 없음. `List<string>` 입출력.
- `null` 항목은 `""` 로 치환(JSON 안정성, `SquadSave.NormalizeSlots` 규약과 일치).
- SO→id 매핑은 **호출처(UI 컨트롤러)** 책임. 헬퍼는 id 문자열만 받는다 — 이는 어셈블리 경계가 아니라
  (런타임 전체가 단일 `Wassup.Runtime.asmdef`) EditMode 순수 테스트 가능성을 위한 의존 최소화 규약이다
  (`PresetApply.cs` 에 `using Wassup.Data` 불요).

## 완료 기준

- [ ] EditMode 테스트 통과:
  - 유닛 7개 기록 → `unitIds` 정확히 반영, 8번째 이상 무시, 부족분 `""`.
  - 카드 10개 기록 → `cardIds` 정확히 반영.
  - 카드 **초과(예: 12장) 기록 → 캡 없이 12장 그대로** 반영(유닛과 비대칭, 의도된 동작). 부족(예: 8장) → 8장.
  - 기존 `stoneIds` 4슬롯이 적용 후에도 **불변**.
  - `SelectedDeck()==null` 프로필 → **false + 덱 미생성 + 스쿼드 미변경**(원자성; 기본값 생성은 ProfileStore 소유).
  - `profile==null` / 선택 스쿼드 null → false, 예외 없음.
- [ ] `ProfileStore.Save` 는 호출하지 않음(테스트가 디스크에 의존하지 않음).
- 확인 2026-07-20 (커밋 05c7c7b8): EditMode 9/9 통과(7슬롯 캡·초과무시·부족패딩·카드 verbatim·초과무캡·스톤 보존·deck_1 생성·null 가드 2). Save 미호출.
