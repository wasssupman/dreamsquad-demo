# 1 — 지속 토글 (배치/전투 중 공격패턴·맵설정 열람)

## 목적

자동 진행(Unit 0) 이후에도 공격패턴("!" 토글)과 맵 설정("MAP SETTINGS" 토글)이 살아 있어, 사전 배치 중·전투 중 언제든 토글로 다시 열람/조정할 수 있게 한다. 드캐 3중1 선택 중에는 모달이 위를 덮어 토글이 차단된다.

## 변경 대상

- `Assets/_Project/Scripts/UI/Outgame/SquadPrepView.cs` (Unit 0 재작성에 포함)
- (씬 와이어링 검증) `SquadPrepView` GameObject 와 `WavePatternStripView` / `MapSettingsPanelView` 의 부모 관계

## 구현

Unit 0 의 재작성으로 이미 충족된다. 핵심은 **자식을 비활성화하지 않는 것**이다.

- `SquadPrepView` 는 진입 시 strip 과 mapSettings 를 `SetActive(true)` 로 켜고, 이후 어떤 경로에서도 끄지 않는다. (기존 `OnStartClicked` 의 `wavePatternStrip.gameObject.SetActive(false)` / `mapSettings.gameObject.SetActive(false)` 제거됨)
- `MapSettingsPanelView` 는 자체 "MAP SETTINGS" 토글 버튼 + 접이식 패널을 이미 갖는다(좌상단 `(40,-40)`). `Build()` 에서 패널은 기본 접힘.
- `WavePatternStripView` 의 "!" 토글 버튼은 좌상단 `(40,-110)` — 맵설정 토글 아래라 겹치지 않음. `SetToggleEnabled(true)` 로 항상 사용 가능.

핵심 계약:

- strip/맵설정 생존은 `SquadPrepView` GameObject(=두 자식의 Canvas 호스트, `sortingOrder=8`)가 진입 이후에도 active 로 남는 것에 의존. 코드 전체에서 `SquadPrepView`/strip/맵설정을 비활성화하는 경로는 없다.
- 두 자식은 `SquadPrepView` 의 자식(또는 그 Canvas 하위)이어야 한다 — Build 이 자체 Canvas 를 추가하지 않으므로 ancestor Canvas 필요.

## 드캐 차단 (코드 없음, 검증 항목)

- `DreamcatcherSelectionView` canvas `sortingOrder=50` 풀스크린 모달이 strip/맵설정(`8`) 위에 렌더 → 드캐 선택 중 두 토글 모두 클릭 불가.
- 검증: 드캐 모달이 떠 있는 동안 토글 버튼이 모달 뒤에 가려 눌리지 않는지 확인. (모달이 raycast 를 막지 못하는 경우에만 후속으로 `SetToggleEnabled(false/true)` 명시 제어 추가 — 기본 가정은 모달 차단으로 충분.)

## 완료 기준

- compile: CS 에러 0.
- Play (Squad 모드): **사전 배치 중** "!" 토글로 공격패턴, "MAP SETTINGS" 토글로 맵 설정 패널을 펼치고 접을 수 있다.
- Play: **전투 시작 후(GamePhase.Battle)** 에도 두 토글이 계속 동작한다.
- Play: 드캐 3중1 모달이 떠 있는 동안에는 토글이 (모달에 가려) 동작하지 않는다.
- Play: 토글로 공격패턴을 연 상태에서 닫으면 오버레이가 해제돼(`SnapHidden`) 배치/전투 입력이 막히지 않는다.
- ✅ 2026-06-04 Play 확인 통과 (사용자). 커밋: `9a9fa09`
