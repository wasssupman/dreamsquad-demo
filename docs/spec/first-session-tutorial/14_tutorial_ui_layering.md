# 14 — 튜토리얼 UI 레이어 우선순위

## 목적

튜토리얼이 메뉴 아래에 렌더되고 입력도 새는 결함을 수정한다. 일반 UI보다 위,
결과·알림·씬 전환보다 아래에 두며 첫 배치 마커/안내문의 화면 겹침도 막는다.

## 변경 대상

- `Assets/_Project/Scripts/UI/Tutorial/TutorialGuidanceStyle.cs`
- `Assets/_Project/Data/Config/TutorialGuidanceStyle_Default.asset`
- `Assets/_Project/Scripts/UI/Tutorial/TutorialGuidanceView.cs`
- `Assets/_Project/Scripts/UI/Tutorial/FirstSessionTutorialController.cs`
- `Assets/_Project/Scripts/UI/Outgame/Tutorial/OutgameTutorialOverlay.cs`
- `Assets/_Project/Scripts/UI/Outgame/Tutorial/OutgameTutorialController.cs`
- `Assets/_Project/Tests/PlayMode/FirstSessionTutorialSmokeTest.cs`
- `docs/spec/first-session-tutorial/README.md`
- `docs/spec/outgame-tutorial/README.md`

## 구현

### 레이어 계약

SO가 dim/guidance/elevated `1499 / 1500 / 1501`을 소유한다. 메뉴 상한 1000보다 높고
Result 2000·Notice 3000·SceneTransition 10000보다 낮다.

### 적용

guidance의 build/elevated와 아웃게임 dim이 Style order를 쓴다. dim 홀에는 Graphic이
없으므로 Start/Squad/Dreamcatcher 입력은 아래 Canvas로 통과한다.

### 첫 배치 안내 배치

- Goal beat 안내문은 `worldMarkerMessageTopOffset=320`으로 상단 제목·spawn과 분리한다.
- 하단 `방어 목표` 라벨은 마커 위에 놓아 defender tray를 가리지 않는다.
- 5초 뒤 `BeginPick()`은 마커를 닫고 일반 offset으로 복귀한다.
- 조기 arm/드래그도 `ClearWorldMarkers()`로 동일하게 복귀한다.

### 회귀 테스트

PlayMode에서 Canvas order/elevated 원복과 marker offset, goal 라벨 방향,
`BeginPick()` 정리를 고정한다.

## 완료 기준

- [x] 컴파일 에러 0
- [x] PlayMode 신규 Canvas order 테스트 통과
- [x] 기존 `FirstSessionTutorialSmokeTest` 회귀 없음
- [x] 런타임 BattleScene에서 `FirstSessionTutorial` order가 메뉴보다 높고 씬 전환보다 낮다
- [x] 마커/안내 배치 회귀 테스트 통과
- [x] BattleScene 첫 Goal beat와 배치 방법 안내에 spawn/goal/tray 겹침이 없다
- [ ] 로비 챕터 A/B에서 dim < guidance이며 홀 버튼 실제 클릭이 유지된다
- [ ] 클래스 안내 탭 캐처 활성 중 `MenuReturnCanvas`가 탭을 가로채지 않는다
- [ ] 사용자 Play 확인 후 확인 일자와 커밋 해시 기록

자동 검증 2026-07-25: EditMode 1279 pass/0 fail/2 skip. PlayMode 튜토리얼 5/5 통과.
전체 PlayMode 43/48 통과, 기존 환경·상태 의존 실패 5개(Auth/Deck/Dreamstone/SceneTransition/Squad).
