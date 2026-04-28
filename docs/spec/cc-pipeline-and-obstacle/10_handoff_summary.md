# CC Pipeline & Obstacle — Handoff Summary

**완료일**: 2026-04-29

## Commit

| Unit | 해시 | 설명 |
|---|---|---|
| 0–9, 5b | c788a6c ~ 2ee808d | CC enum/systems, Slow migration, impulse, knockback/push, obstacle, trim |

**최종**: 431726c (epsilon-inset) + C1 fix-up (NativeContainer dispose)

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

- **Knockback 방향 (실제 구현)**: relative-velocity (dir=normalize(D-E), D=impulse/E=flow, fallback D). spec 5 simple form superseded by fix-commits. 게임감 우수. **정책**: defender==enemy 위치 시 dir=normalize(-E) → 적 미세 전진 (거의 발생 없음, 후속 fix 후보).
- **Unit 8 옵션 B**: IsWallCell + `blockedCells.Contains` OR. goal exception = IsWallCell 우선 (goal-cube 디버그 통과).
- **CcApplySystem 비-Burst**: ECB structural change (AddBuffer) 때문에 의도적. 후속 주석 추가 권장.
- **CC 진입점 두 갈래**: 큐 (AttackSystem) + 즉시 (EffectSpawner/push). 동일 merge 정책. 3번째 caller 시 helper 추출 후보.
- **ObstacleDebugMenu**: Battle/Effects 폴더 (4 맥락 제약 유지).

## Follow-up

- Cleanup: W1 비-Burst 주석, W4 TriggerDeploymentOnPlaceSkill 방어 주석
- I2: `kBoundaryEpsilon` const 명명
- I3: Obstacle.worldPosition "presentation-only" 주석
- 기존 후보: 적 공격/HP, 멀티셀 큐브, 적-적 분산, 게임 통합, 추가 CcKind, VFX, incremental 갱신
