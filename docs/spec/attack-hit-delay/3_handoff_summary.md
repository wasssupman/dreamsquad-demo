# 3 — Handoff Summary (attack-hit-delay, units 0~2)

## Commit
- `a20c277` feat(combat) unit 0 — hitDelaySec/hitDelayRemaining 필드 + baking
- `9d124f6` feat(combat) unit 2 — deployDelaySec(배치 지연)
- `3a1260c` feat(combat) unit 1 — AttackSystem fire START/RESOLVE 분리
- 양트랙 리뷰 APPROVE 후속: `e3d5d79`. main 직접 커밋(프로젝트 관행).

## Implemented
- **공격 타이밍 = 도달 모델의 연장**: 공격 시작 → `hitDelaySec` 후 타격 판정(재판정된 타겟). hitDelaySec=0 = 현행 즉시(byte-동일).
- `AttackSystem` fire 를 **START**(애니메이션 + 쿨다운 리셋 + (적)이동정지 + 지연 세팅)와 **RESOLVE**(데미지/투사체/넉백)로 분리. 지연 중 `hitDelayRemaining` tick, 만료 시 RESOLVE.
- **배치 지연(unit 2)**: `DefenderUnitData.deployDelaySec` → 디펜더 배치 시 초기 `cooldownRemaining`. 그동안 공격 X = idle(자동). AttackSystem 무수정.
- 필드: `AttackState.hitDelaySec/hitDelayRemaining`, `AttackUnitData.hitDelaySec`, `DefenderUnitData.hitDelaySec/deployDelaySec`. 모두 기본 0 → 데이터 미설정 시 동작 무변경.

## Key Files
- `Battle/Combat/AttackSystem.cs`(fire START/RESOLVE), `AttackState.cs`(필드)
- `Data/AttackUnitData.cs`, `DefenderUnitData.cs`(필드)
- `Bridge/BattleBridge.cs`(baking: enemy/defender hitDelaySec, defender deployDelaySec→초기 cooldown)

## Verified
- compile 0 · EditMode 26/26.
- Play(더미 guardian, 에디터 포커스 필요): hd=0 전투 정상(guardian Health 1000→360 = 무회귀, RESOLVE 데미지 적용) · hd=3 세팅 적 `hitDelayRemaining=1.57`(윈드업, 데미지 보류) = 지연 동작. deploy delay 는 코드 검증(초기 cooldown + `WithNone<PendingDeployment>`).
- 양트랙 리뷰(code+ecs) APPROVE. M1/M2/TauntGrant hitDelaySec 는 후속.

## Notes (되돌리면 안 되는 의도)
- 쿨다운 기산 = 공격 START. T+N 판정 = **재판정**(시작 타겟은 애니 facing 용). 지연 중 새 공격 시작 안 함.
- hitDelaySec=0 경로가 현행과 byte-동일해야 함(무회귀). 데이터 전부 기본 0.
- 라이브 검증 시 **에디터 포커스 필수**(비포커스면 Play 시뮬 tick 안 함).

## Follow-up
- **M1 standoff/발사 metric 통일** [S] · range<0.5tile soft stall 가드(aggro-standoff 후속).
- **M2 aggro/코너 PlayMode smoke** [S] · 합성 경로 통합 테스트.
- **TauntGrant hitDelaySec authoring** [S] · taunt 부여 AttackState 에 hitDelaySec(현재 0, AggroAttackProfile 필드 필요).
- 애니메이션 타격 프레임 동기(시간 대신 Spine event) [M].
