# CC Pipeline & Obstacle — Handoff Summary

**완료일**: 2026-04-29

## Commit

| Unit | 해시 | 설명 |
|---|---|---|
| 0–9, 5b | c788a6c ~ 2ee808d | CC enum/systems, Slow migration, impulse, knockback/push, obstacle, trim |

**최종**: 431726c (epsilon-inset) → f525341 (C1 OnDestroy dispose) → 41bc973 (closure cleanup batch: W1/W4/I2/I3 주석/명명 + D≈0 guard)

## Implemented

- CC enum (Slow/Impulse) + CcEffect buffer + queue/singleton systems (CcApplySystem/CcDecaySystem/ObstacleLifetimeSystem)
- SlowEffect 제거 → EffectSpawner.ApplySlow thin wrapper (호출부 무수정)
- MovementSystem 통합: Slow/Impulse 합성, zero-flow 복구, cell-trim (경로/OOB/큐브)
- DefenderSO 5필드 (knockback/push) + 배치 push 헬퍼
- Obstacle 싱글턴 (blockedCells NativeHashSet)
- 진입점 (ApplyCc/ApplySlow/SpawnObstacle) + 디버그 메뉴

## Key Files

Effects/: CcEffect.cs, CcApplySystem.cs, CcDecaySystem.cs, EffectSpawner.cs, Obstacle.cs, ObstacleSingleton.cs, ObstacleLifetimeSystem.cs, ObstacleDebugMenu.cs
Movement/: MovementSystem.cs, MovementCellTrim.cs
Combat/: AttackSystem.cs, DefenderCcData.cs
Data/: DefenderUnitData.cs
Bridge/: BattleBridge.cs

## Verified

- 133/133 EditMode ✓, 콘솔 에러/경고 0
- SlowEffect 0 hits, ApplySlow 시그니처 보존 (호출부 3곳 무수정)
- LocalTransform writer = MovementSystem 단독, 시스템 순서 ✓
- NativeContainer lifecycle clean, leak 0 (C1 fix-up applied)
- PlayMode Unit 2/5/5b/6/9 통과, Portal/Tornado 의미 동치

## Notes

- **Knockback 방향 (실제 구현)**: relative-velocity (dir=normalize(D-E), D=impulse/E=flow, fallback D). spec 5 simple form superseded by fix-commits. 게임감 우수. **D≈0 guard (41bc973)**: defender 와 enemy 위치 동일 시 enqueue 스킵 (degenerate -E 방향 방지).
- **Unit 8 옵션 B**: IsWallCell + `blockedCells.Contains` OR. goal exception = IsWallCell 우선 (goal-cube 디버그 통과).
- **CcApplySystem 비-Burst (의도)**: ECB structural change (AddBuffer) 때문. 41bc973 에서 사유 주석 추가됨.
- **CC 진입점 두 갈래**: 큐 (AttackSystem) + 즉시 (EffectSpawner/push). 동일 merge 정책. 3번째 caller 시 helper 추출 후보.
- **ObstacleDebugMenu**: Battle/Effects 폴더 (4 맥락 제약 유지).

## Follow-up

- 적 공격/HP/Taunt — 별도 spec, 적 공격 시스템 신설
- 큐브 spawn 게임 통합 (디펜더 능력 / 스킬 카드 연결)
- 큐브 시각 Presenter (`ObstaclePresenter` MonoBehaviour, mesh/particle)
- Stun/Root/Reverse/Pull/Push 추가 CcKind (enum + switch case 추가)
- 멀티셀 큐브 / 적-적 분산
- I1: 3번째 CC caller 등장 시 merge helper 추출 (현재 EffectSpawner.ApplyCc 와 CcApplySystem.MergeOrAdd 듀얼 구현)
- I4: 큐브 수 16 초과 시 ObstacleLifetimeSystem.OnUpdate Burst 분리 + `blockedCells` incremental 갱신
