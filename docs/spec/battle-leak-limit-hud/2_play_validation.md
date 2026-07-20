# 2 — Play 통합 검증

## 목적

스트레스 HUD가 실제 Battle 프레임의 점수 아래에서 읽히고, BattleBridge 상태 변화와 페이즈 수명주기를
정확히 반영하는지 확인한다.

## 변경 대상

- `Assets/Screenshots/battle_leak_limit_*.png` — 비추적 검증 산출물
- 필요 시 units 0~1의 직렬화 튜닝값

## 구현

- 1920×1080 Battle에서 점수 배지와 스트레스 배지의 정렬·간격·safe area를 캡처한다.
- 기본 상태 `0 / 10`, 정상 `1 / 10`, 경고 `7 / 10`, 치명 `9 / 10`을 확인한다.
- 몽마의 계약 상당의 limit 감소 `0 / 10 → 0 / 9`가 즉시 표시되는지 확인한다.
- 누수 증가/limit 감소 시 1회 punch와 색 flash, 안정 상태에서 무상시 pulse를 확인한다.
- Placement/Result에서 점수와 누수 배지가 함께 숨는지 확인한다.
- Unity 컴파일 및 Console error 0을 확인한다.

## 완료 기준

- 점수 아래 `스트레스 current / limit`이 겹침 없이 읽히고 점수보다 작은 위계를 유지한다.
- HUD의 분모와 BattleBridge의 패배 비교가 같은 `EffectiveLeakLimit()`를 사용한다.
- 정상/경고/치명 색과 갱신 모션이 계약대로 동작한다.
- 기존 점수 증가 연출과 누수 상태가 서로 값을 변경하지 않는다.
- Console error 0이며 BattleScene의 다른 작업 hunk를 변경하지 않는다.

## 검증 결과 (2026-07-18)

- Unity 6000.4.3f1, 1920×1080 Battle 캡처에서 점수 바로 아래 360×64 배지 정렬을 확인했다.
- `0 / 10`, `7 / 10` 주황 경고, `9 / 10` 적색 치명 상태를 확인했다.
- 실제 `BeginPlacement → TryPayLeakAllowance(1)` 경로에서 remaining `10 → 9`, HUD `0 / 9`를 확인했다.
- GamePhase Placement에서 ScorePanel hidden, Battle에서 visible을 확인했다.
- 기존 점수 숫자/연출 코드와 누수 값 저장·표시 경로가 독립임을 코드 diff로 확인했다.
- BattleScene 저장/변경 없이 코드 기본 직렬화값으로 렌더했으며 Unity Console error 0이다.
- 플레이어 노출 용어를 `스트레스`로 개정한 뒤 4글자 라벨이 gold pill 안에서 잘림 없이 표시됨을
  1920×1080 Play 캡처로 재확인했다.

확인: 2026-07-18 · 사용자 마감 확인 · 구현 `f4cc4371`, 용어 개정 `d4cd9f0f`
