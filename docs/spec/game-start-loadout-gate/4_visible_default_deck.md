# 4 — 신규 유저 기본 드림캐쳐 visible 정합화

## 목적

신규 유저와 `DEFAULT LOADOUT` 리셋이 받는 기본 드림캐쳐 덱을 현재 카드
`visible` 계약과 맞춘다. 숨김 카드가 기본 덱에 들어가 로그인 직후 prune되고
8/10 미달로 START가 막히는 경로를 제거한다.

## 변경 대상

- `Assets/_Project/Data/Dreamcatcher/DreamcatcherDeck_Default.asset`
- `Assets/_Project/Scripts/Core/Profile/ProfileStore.cs`
- `Assets/_Project/Tests/EditMode/ProfileStoreDefaultDeckTests.cs`
- `docs/spec/game-start-loadout-gate/README.md`

## 구현

1. 현재 기본 덱 10장 중 `visible=0`인 두 장을 같은 스탯 계열의 visible 카드로
   1:1 교체한다. 나머지 8장과 전체 순서는 유지한다.

   | 슬롯 | 기존 | 변경 |
   |---:|---|---|
   | 4 | `cost1_as` (`visible=0`) | `guardian_as` (`visible=1`) |
   | 6 | `cost1_hp` (`visible=0`) | `ranger_hp` (`visible=1`) |

   최종 순서:
   `ranger_atk`, `poke_needle`, `ranger_as`, `bouncy_bead`, `guardian_as`,
   `thornmail`, `ranger_hp`, `guardian_hp`, `farewell`, `guardian_fortress`.
2. `ProfileStore.BuildDefaultDeck`은 null/id 없는 카드뿐 아니라
   `visible == 0` 카드도 건너뛴다. 기본 덱 에셋의 실수나 이후 visibility 변경이
   신규 프로필에 숨김 카드를 직접 시딩하지 못하게 한다.
3. 시딩 소유권은 기존처럼 `DreamcatcherDeck_Default.asset`의 저작 순서 +
   `ProfileStore`에 둔다. 카탈로그 앞 N장을 자동 선택하거나 별도 기본값 정의를
   만들지 않는다.
4. 기존 선택 덱은 덮어쓰지 않는다. 이미 저장된 덱의 숨김 카드 제거는 기존
   `HiddenCardDeckPruner` 계약을 그대로 사용한다.

## 비목표

- 기존 유저의 8/10 덱 자동 보충
- Active/무의식 카드를 신규 기본 덱에 편입
- `visible` import·prune 시점 변경
- 덱 크기나 타입 제한 변경

## 완료 기준

- Unity compile 에러 0.
- EditMode: `BuildDefaultDeck`이 숨김 카드를 건너뛰고 뒤의 visible 카드로
  `deckSize`까지 계속 채운다.
- 실제 `DreamcatcherDeck_Default.asset`이 위 10장 순서와 일치하고 전부
  `visible != 0`이다.
- 실제 에셋으로 `ProfileStore.CreateDefault`를 실행한 결과가 정확히 10장이고
  `DeckRules.Validate`를 통과한다.
- 기존 선택 덱 불변 테스트가 계속 통과한다.

자동 검증 2026-07-26 — Unity compile 에러 0 · EditMode
`Wassup.Tests.EditMode` **1353건 완료 / 실패 0**. 신규 회귀 2건:
숨김 카드 skip 후 뒤의 visible 카드로 정원 충족 +
실제 기본 덱 10장 순서/visible/`CreateDefault`/`DeckRules.Validate` 통과.
BattleScene Play 중 관측한 `JarFigurePile.cs:158` 반복 NRE는 본 unit 변경 전부터의
별도 런타임 오류이며 변경 파일과 무관하다.
사용자 완료 승인 2026-07-26 · 커밋 `(이 커밋)`.
