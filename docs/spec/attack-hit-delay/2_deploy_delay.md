# 2 — 배치 지연 (deploy delay)

## 목적

디펜더가 **배치된 직후 `deployDelaySec` 초 동안 공격하지 않고 idle**, 이후 정상 공격. (예열/소환 텀.) hit-delay(per-attack)와 독립. **디펜더 전용**(적은 배치가 아니라 스폰).

## 변경 대상

- `Assets/_Project/Scripts/Data/DefenderUnitData.cs` — `deployDelaySec = 0f`.
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 디펜더 `AttackState` baking 에서 `cooldownRemaining = unitData.deployDelaySec`(기존 `0f`).

## 구현

- `cooldownRemaining`(공격 가능까지 남은 시간)을 **배치 시 `deployDelaySec` 로 초기화**. AttackSystem 이 이미 `cooldownRemaining ≤ 0` 일 때만 발사하므로, 그 시간 동안 자동으로 발사 안 함.
- **idle = 자동**: 공격 이벤트가 없으면 `SpineUnitPool` 이 idle 애니메이션 유지 → 별도 로직 불필요.
- AttackState 는 `WithNone<PendingDeployment>` 라, 지연은 **배치 확정(PendingDeployment 제거) 시점부터** tick → "배치 직후" 정확.
- AttackSystem **무수정**(unit 1 과 독립). `deployDelaySec=0` → 현행 즉시.

## 완료 기준

- compile 0 에러. EditMode 무회귀(26).
- Play: `deployDelaySec>0` 디펜더가 배치 후 그 시간 동안 idle(공격 X), 이후 공격 시작.
