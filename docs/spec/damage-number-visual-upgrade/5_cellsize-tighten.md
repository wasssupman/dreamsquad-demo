# 5 — cellSize 축소 튜닝 (rev)

## 목적

`damage-number-popup` unit 6(히트별 개별 폰트)로 프레임당 폰트 수가 늘면서, 겹침 방지 격자가 숫자를 넓게 흩뿌리는 문제가 생겼다. 겹치지 않으면서 **히트 지점 근처에 촘촘히** 모이도록 배치 격자 셀을 줄인다.

## 배경

unit 0 의 점유 격자(`DamageNumberStyle.cellSize`, 카메라축 투영 world 단위)가 숫자를 셀 단위로 분산시킨다. 셀이 크면 다발 히트가 화면에 넓게 퍼져 "어느 적이 맞았는지" 응집력이 떨어진다.

## 변경 대상

- `BattleScene` 의 `VfxSpawner` GameObject → `DamageNumberSpawner.style.cellSize` (씬 직렬화 값)

## 구현

- `cellSize` (0.85, 0.55) → **(0.45, 0.32)**. 코드 변경 없음 — 순수 authored 값 튜닝.
- 씬 저장 diff 가 cellSize 한 줄인지 검증(WIP 오염/sparkColorBoost 재유입 없음 확인 — lessons).
- 값은 시각 판단 대상: 더 촘촘/여유는 이 필드로 재튜닝(Play 중 런타임 조정 후 확정값만 저장).

## 완료 기준

- [x] cellSize 축소, 씬 저장 diff = 1줄
- [x] 다발 히트가 겹치지 않으면서 더 촘촘히 응집 — Play 육안
- 완료 확인: 2026-07-09 — 사용자 승인. 이 문서와 동일 커밋.
