# 0 — 로드아웃 게이트 규칙 (순수 함수)

## 목적

"스쿼드와 덱이 시작 가능한 상태인가" 를 **씬/UI 없이** 판정하는 순수 함수를 만든다. 판정 결과는 통과 여부 + **무엇이 몇 개 모자란지**를 함께 내야 한다 — unit 1 의 팝업이 그대로 렌더할 재료이기 때문이다.

이 unit 은 compile-safe 하고 씬을 건드리지 않는다. 호출처 배선은 unit 1.

## 변경 대상

- `Assets/_Project/Scripts/Core/Profile/LoadoutGate.cs` (신규)
- `Assets/_Project/Tests/EditMode/LoadoutGateTests.cs` (신규)

## 구현

`ProfileStore` 와 같은 자리·같은 shape 을 따른다: `Core/Profile/`, `namespace Wassup.Core`, `using Wassup.Data`, static 유틸(Manager 아님 — 제약 5).

```csharp
public enum LoadoutTarget { Squad, Deck }

public readonly struct LoadoutShortfall
{
    public readonly LoadoutTarget target;
    public readonly int have;
    public readonly int need;
    public readonly string reason;   // 카운트로 설명 안 되는 실패용 (무효 카드 id 등)
}

public static class LoadoutGate
{
    // 전부 충족 = true (shortfalls 비어 있음). 아니면 false + shortfalls 채움.
    // shortfalls 는 진입 시 내부에서 Clear 한다 (호출자가 잊을 수 없게). null 허용.
    public static bool Check(PlayerProfile p, DefenderCatalog units,
                             DreamcatcherCardCatalog cards, List<LoadoutShortfall> shortfalls)
}
```

**스쿼드 판정**: `SquadDraw.Resolve(squad.unitIds)` **를 호출해** 배치될 id 목록을 얻고, 그중 `units.ById(id) != null` 인 개수를 센다. `need = min(SquadSave.SlotCount, SquadDraw.FieldCount)`. `have != need` 면 shortfall.

> **dedup/캡 로직을 복제하지 않는다.** "어떤 id 가 실제로 필드에 오르는가" 의 소유자는 `SquadDraw.Resolve`(빈칸 제거 → dedup → `FieldCount` 컷)이고, `GameManager.StartSquadMatch` 가 매치 시작 시 부르는 게 그 함수다. 게이트가 같은 로직을 다시 쓰면 어긋난다 — 실제로 복제본은 "`SlotCount` 로 슬라이스 후 dedup" 이라 `[u1,u1,u2..u7]`(8칸) 에서 Resolve 는 7을 배치하는데 게이트는 6으로 차단했다 (critic M1). 위임하면 일치가 정의상 보장된다.
>
> 게이트가 Resolve 위에 더하는 것은 **카탈로그 해석 하나뿐**이다 (Resolve 는 의도적으로 catalog 를 모른다 — `SquadDraw.cs:10`). 이게 stale id 를 걸러낸다.
>
> `FilledCount()` 를 쓰지 않는다. 그건 빈 문자열이 아닌 슬롯을 셀 뿐이라 stale id 도 중복도 "충족" 으로 통과시킨다. `SquadBuilderView` 는 픽 시점에 중복을 거부하지만(`:359-360`) 손편집된 `profile.json` 은 막지 못한다.
>
> `need` 에 `min()` 을 쓰는 이유: `SlotCount`(슬롯 수)와 `FieldCount`(배치 수)는 독립 하드코딩된 7이다. `SlotCount` 만 8로 늘면 Resolve 는 최대 7만 반환하므로 요구치 8은 **영구 도달 불가**가 된다.

**덱 판정**: `p.SelectedDeck()` → `DeckRules.Validate(save.cardIds, cards, out var reason)` 를 그대로 호출. 실패 시 `have = save?.Count() ?? 0`, `need = DeckRules.EffectiveDeckSize(cards)`, `reason` 은 Validate 가 준 문자열. **덱 규칙을 여기서 재정의하지 않는다.**

**null 처리**: `p == null` → 스쿼드/덱 둘 다 shortfall (`have = 0`). `SelectedSquad()`/`SelectedDeck()` 이 null 인 경우도 동일하게 `have = 0`.

**카탈로그는 사전조건이다 — shortfall 이 아니다.** 카탈로그 null 은 배선 오류이고, shortfall 로 흘리면 플레이어가 절대 못 고치는 요구치가 뜬다(카드 카탈로그 null → `EffectiveDeckSize` 폴백 10 → "덱 8/10" 인데 빌더는 8에서 막음 = 영구 잠금. critic C1). `Check` 는 이 사전조건을 XML/주석으로 명시만 하고 자체 분기를 두지 않는다. 차단은 호출자 책임 (unit 2).

**순서 고정**: shortfalls 는 항상 Squad → Deck 순으로 담는다. 팝업 줄 순서가 흔들리지 않게.

## 완료 기준

- [x] compile clean. — 에디터 콘솔 에러 0.
- [x] EditMode 테스트 green (`LoadoutGateTests` 11/11), 아래 케이스 전부:
  - 유효 유닛 7 + 유효 카드 8 → `true`, shortfalls 비어 있음 — `FullSquadAndDeck_Passes`
  - 유닛 5 → `false`, `Squad(have:5, need:7)` 1건 — `ShortSquad_ReportsSquadShortfall`
  - 선택 덱 없음 → `false`, `Deck(have:0, need:8)` 1건 — `NoDeckSelected_ReportsDeckShortfall`
  - 둘 다 미충족 → `false`, 2건, **Squad 가 먼저** — `BothUnmet_ReportsSquadFirst`
  - **stale 유닛 id 7개**(카탈로그에 없는 id) → `false`, `Squad(have:0, need:7)` — `StaleUnitIds_DoNotCount` (핵심 회귀)
  - **중복 유닛**(유효 id 7슬롯 중 2개 동일) → `false`, `Squad(have:6, need:7)` — `DuplicateUnits_CountOnce`
  - **8칸 + 앞쪽 중복**(`[u1,u1,u2..u7]`) → `true` — `OverlongListWithLeadingDuplicate_MatchesSquadDraw`. `SquadDraw.Resolve` 가 7을 배치하므로 게이트도 통과해야 한다. **복제 구현에서는 실패하던 케이스** (critic M1 회귀 잠금)
  - 카드 8장이지만 그중 하나가 무효 id → `false`, `Deck` `reason` 비어있지 않음 — `FullDeckWithUnknownCard_ReportsReason`
  - `p == null` → `false`, 2건 — `NullProfile_ReportsBoth`
  - `shortfalls` 에 값이 있어도 `Check` 가 Clear — `Check_ClearsCallerList`
  - `shortfalls == null` 이어도 throw 안 함 — `NullShortfallList_DoesNotThrow`
- [x] 기존 EditMode 테스트 전량 green — 847 중 845 passed / **0 failed** / 2 skipped(기존부터 문서화된 Ignore 2건, 무관).
- [x] `DeckRules` / `PlayerProfile` / `SquadDraw` / `OutgameMenuController` diff 0 — 신규 파일 2개만 추가. `git diff --stat` 로 확인.

확인 2026-07-16 — 순수 게이트 판정. 설계 critic 반영: 스쿼드 판정을 `SquadDraw.Resolve` 위임으로 교체(복제본이 "슬라이스 후 dedup" 으로 이미 드리프트해 있었음 — `OverlongListWithLeadingDuplicate_MatchesSquadDraw` 가 그 회귀를 잠근다). 카탈로그 null 은 shortfall 이 아니라 사전조건으로 문서화 — 차단은 unit 2 의 호출자 책임이므로, **unit 2 전까지는 게이트가 아직 아무데도 연결돼 있지 않다**(순수 함수만 추가).
