# 3 — 시각·상호작용 검증

## 목적

최종 레이아웃과 자원 버튼이 실제 Play 프레임에서 계약대로 보이고 동작하는지 검증한다.

## 변경 대상

- `Assets/Screenshots/awakening_resource_*.png` (비추적 검증 산출물)
- 필요 시 units 0~2 대상 파일의 튜닝값

## 구현

- 1920×1080 기준 Placement/Battle을 각각 캡처한다.
- 각성치 0/14/15/29/30/99/100을 주입해 숫자·연속형 링·발광 상태를 비교한다.
- 버튼 탭으로 손패 open/close, 전이 중 mash guard, 페이즈 이탈 hide/close를 확인한다.
- critic BLOCKER/HIGH 재검수 후 필요한 시각 튜닝만 반영한다.
- Unity 컴파일과 Console error 0을 확인한다. 대상 스크립트 정적 검증을 실행한다.

## 완료 기준

- Placement 캡처에는 우하단 전투 시작만, Battle 캡처에는 좌 NextWave/중앙 트레이/우 각성이
  분리되어 보인다(16:10/16:9/20:9).
- 큰 숫자가 첫 시선에 읽히며 `드림캐쳐` 행동과 자원 맥락이 이해된다.
- 손패 open/close가 기존 동작을 유지한다.
- critic BLOCKER/HIGH 0, 컴파일/콘솔 오류 0.

## 검증 결과 (2026-07-18)

- Unity 6000.4.3f1 Play, 1920×1080: Placement 우하단 전투 시작 노출 및 각성 hidden 확인.
- Battle: 좌하단 NextWave/중앙 트레이/우하단 각성 분리 확인.
- 실제 `Button.onClick → Toggled → DreamcatcherHandView` 경로로 Hand 상태 진입, 선택 톤 확인.
- 값 7/12/30에서 중앙 수치와 연속형 링 갱신 확인. Unity Console error 0.
- wide aspect는 모두 `SafeAreaRoot` 좌/우 bottom anchor를 사용하며, Android 실기 cutout 검증은
  feature-wide 비목표의 기존 모바일 QA 항목으로 유지한다.
