# 5 — 최종 인계 요약

## Commit

- `986efa09` unit 0 — 순수 로드아웃 게이트 판정
- `650135f8` unit 1 — 신규 프로필 기본 덱 시딩
- `91dd96a5` unit 4 — visible 기준 신규 유저 기본 덱 정합화

## Implemented

- 로비 START가 스쿼드 7명과 유효한 드림캐쳐 덱을 검사한다.
- 미충족이면 씬 전환 없이 사유와 편성 화면 이동 버튼을 노출한다.
- 신규 프로필과 덱 없는 프로필은 `ProfileStore` 소유의 기본 덱을 받는다.
- 기본 덱의 숨김 `cost1_as`를 visible 카드 `guardian_as`로 교체했다.
- 기본 덱의 숨김 `cost1_hp`를 visible 카드 `ranger_hp`로 교체했다.
- `BuildDefaultDeck`은 null, id 없음, `visible=0` 카드를 건너뛰고 정원까지 계속 찾는다.
- 기존 사용자의 선택 덱은 덮어쓰지 않는다.

## Key Files

- `Assets/_Project/Data/Dreamcatcher/DreamcatcherDeck_Default.asset`
- `Assets/_Project/Scripts/Core/Profile/ProfileStore.cs`
- `Assets/_Project/Scripts/Core/Profile/LoadoutGate.cs`
- `Assets/_Project/Scripts/UI/Outgame/OutgameMenuController.cs`
- `Assets/_Project/Scripts/UI/Outgame/LoadoutGatePopup.cs`
- `Assets/_Project/Tests/EditMode/ProfileStoreDefaultDeckTests.cs`

## Verified

- Unity compile 에러 0.
- EditMode `Wassup.Tests.EditMode` 1353/1353 완료, 실패 0.
- 실제 기본 덱의 10장 순서와 전 카드 `visible != 0`을 회귀 테스트로 고정했다.
- 실제 에셋을 사용한 `ProfileStore.CreateDefault` 결과가 정확히 10장이다.
- 생성된 기본 덱이 `DeckRules.Validate`를 통과한다.
- 기존 선택 덱 불변 회귀가 계속 통과한다.

## Notes

- 변경은 신규/덱 없는 프로필과 dev `DEFAULT LOADOUT`에만 적용된다.
- 기존 선택 덱의 숨김 카드 정리는 기존 `HiddenCardDeckPruner` 계약을 유지한다.
- 카탈로그 앞 N장을 자동 선택하지 않고 저작된 기본 덱 순서를 source of truth로 둔다.
- `DEFAULT LOADOUT`은 프로필 전체를 리셋하므로 라이브 사용자 프로필로 누르지 않았다.
- BattleScene Play 중 `JarFigurePile.cs:158`의 기존 반복 NRE가 관측됐다.
  이번 unit의 변경 파일과 무관하며 별도 런타임 이슈다.

## Follow-up

- 없음. 기본 덱 교체와 시딩 방어는 자동 검증으로 닫았다.
