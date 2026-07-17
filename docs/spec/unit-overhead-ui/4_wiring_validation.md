# 4 — 배선·검증

## 목적

신규 스타일과 Layer를 BattleScene에 배선하고 UnifiedOverhead를 기본 경로로 전환한다.

## 변경 대상

- `Assets/_Project/Data/Config/UnitOverheadUiStyle.asset`
- `Assets/_Project/Scenes/BattleScene.unity`
- 관련 EditMode/PlayMode 테스트

## 구현

- 검증 전 Legacy, compile/EditMode 후 UnifiedOverhead로 scene 저장.
- Battle/teardown/Placement 재진입, 적/방어 사망, 카드 회수를 확인한다.

## 완료 기준

- compile 0, layout EditMode 통과.
- Play: 상시 바, 5px/5px, 진영 스타일, 최대 3장, 사망/리셋 회수, 중복 레거시 없음.
- Android 1920×1080 reference scaling과 화면 외곽 위치 확인.

## 2026-07-18 검증 기록

- BattleScene 기본값을 `UnifiedOverhead`로 저장하고 Style/Hand/Layer 참조를 배선했다.
- Unity 6000.4.3f1 Editor가 신규 스크립트를 포함해 domain reload를 완료했다. C# compile error 0.
- `git diff --check` 통과.
- EditMode 실행 및 실제 BattleScene 시각/Android 검증은 실행 중 Play 세션 종료 후 수행한다.

## 2026-07-18 코드 리뷰 반영

- 만피 alpha 적용 대상을 HealthBar로 한정해 드림캐쳐 카드 밝기를 분리했다.
- 진영별 bar/card Sprite를 Layer 수명 공유 캐시로 전환했다. 카드 프레임은 최초 카드 표시 때 지연 생성한다.
- Overhead Canvas order를 3으로 내려 손패(5)·점수(6) 등 전투 HUD 아래에 배치했다.
- 5px vertical contract, NaN/극소 폭 방어 EditMode 테스트와 view reconcile/공유/teardown PlayMode smoke를 추가했다.
- 리뷰 반영 후 Unity domain reload 완료, C# compile error 0. 실제 테스트 실행과 BattleScene 시각 검증은 대기 중이다.

## 2026-07-18 플레이 캡처 피드백 반영

- 축소 화면에서 사라지던 외곽선을 2~2.5 reference px로 강화하고 bar 폭·높이를 확대했다.
- dark drop shadow + faction frame + track + fill + top highlight의 5층 구조로 변경했다.
- 무기/방패 Renderer Bounds에 끌려가던 X 중심을 visual pivot 기준으로 분리했다. Y는 renderer top을 유지한다.
- Unity domain reload 완료, C# compile error 0. 변경 후 실제 BattleScene 캡처 재검증은 대기 중이다.
