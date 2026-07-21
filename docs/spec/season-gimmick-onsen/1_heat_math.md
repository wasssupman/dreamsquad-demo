# 1. HeatMath — 열기 델타 순수 함수 [중립]

## 목적

Part A 의 회복↔손실 반전 규칙을 **아키텍처 무관 순수 함수**로 못박는다. 입력은 plain 값, 출력은 부호 있는 HP 델타 하나. ECS/Mono 어느 쪽도 모른다 — Mono 로 옮겨도 이 파일은 그대로 복붙(제약 10 seam, `ModifierMath` 모범 동형). EditMode 로 반전 경계·HP1 바닥·오버힐 클램프를 회귀 고정.

## 변경 대상

- **신규**: `Assets/_Project/Scripts/Battle/Effects/HeatMath.cs` (`static class HeatMath`, ns `Wassup.Battle.Effects`)
- **신규**: `Assets/_Project/Tests/EditMode/HeatMathTests.cs`

## 구현

`float Delta(int stacks, int flipThreshold, float maxHp, float currentHp, float healPercent, float lossPercent)`

- **회복 구간** (`stacks ≤ flipThreshold`): `min(maxHp × healPercent, max(0, maxHp − currentHp))` → **≥ 0**. 최대체력 초과분은 잘라 실제 증가량만 반환(오버힐 없음 → 만피 유닛 VFX 스팸 방지).
- **과열 구간** (`stacks > flipThreshold`): `−min(maxHp × lossPercent, max(0, currentHp − 1))` → **≤ 0**. HP 1 밑으로 안 내림(열기는 사망 원인 불가).
- **반환 규약**: `>0` 회복 적용 · `<0` 피해 적용 · `0` no-op(소비 측이 enqueue 스킵).

호출 측(유닛 2 `HeatAccrualSystem`)은 이 값의 부호만 보고 `IncomingHeal`/`IncomingDamage` 로 라우팅한다. 값 자체는 아키텍처를 모른다.

## 완료 기준

- Unity 재컴파일 CS 에러 0.
- `HeatMathTests` EditMode 전부 green. 최소 케이스:
  - 회복: `stacks≤flip` → `+maxHp×heal`(헤드룸 내).
  - 반전 경계: `stacks==flip` = 회복, `stacks==flip+1` = 손실.
  - 오버힐 클램프: 만피/거의만피 → 실제 증가량만(또는 0).
  - HP1 바닥: 저체력 과열 → 손실이 HP 1 에서 멈춤(`currentHp==1` → 0).
  - 스케일: `maxHp` 다른 값에서 비율 정상.
