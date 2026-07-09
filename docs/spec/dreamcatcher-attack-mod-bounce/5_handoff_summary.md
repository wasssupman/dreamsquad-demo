# 5 — Handoff Summary

> dreamcatcher-attack-mod-bounce 구현 종료 인계. 최신 계약은 README/번호 문서, 구현 상세는 코드/커밋 우선.

## Commit

- `6df8c9eb` spec (설계 critic 반영)
- `cf979bba` unit 0 — 정의계층 `DcAttackModKind/Spec` + 카드 `attackMods[]` + `ProjectileState/Request` bounce 3필드
- `d047cfdf` unit 1 — `BounceRetarget.FindNext` 순수함수 + EditMode 6/6
- `a9f0aa7b` unit 2 — `ProjectileHitSystem` 튕김 arm + bridge `SpawnProjectile` bounce 피핑
- unit 3~4 — `DcAttackModSlot` + 부착 확장 + AttackSystem 주입 + `Card_BouncyBead.asset` (이 커밋)

## Implemented

- 드림캐쳐 카드 부류 (c): **트리거 없는 상시 공격 개조**. 축 매칭 스탯%(a)·트리거형(b)과 별개.
- **투사체 튕김 프리미티브** (드림캐쳐 무관 Combat 능력): 착탄 해결 후 `bounceRemaining>0` 이면 파괴 대신 재타겟·재비행. 같은 엔티티 재사용 → 뷰/트레일 연속 공짜. 신규 시스템/드레인/태그 0.
- 재타겟 = `BounceRetarget.FindNext` 순수 기하(float3+인덱스, Entity 무참조 — 아키텍처-중립, 월드 없이 EditMode). 직전 대상만 제외 → **A→B→A 재히트 허용**(v1 확정).
- 감쇠 = 튕길 때마다 state.damage + outputs Damage magnitude ×damageMul (계약 3).
- 카드 → `DcAttackModSlot`(Combat, 트리거 없음) 부착. AttackSystem 이 Homing 기본공격 request 에 집계 주입(count 합/mul 곱/range max). **ballistic·dc-트리거 캐리어 투사체는 제외**(계약 4). 근접(ProjectileRef 없음) = warn+skip.
- 통통 구슬 = 유닛 **기본 화살**을 2회 튕김. 카드에 투사체 필드 없음.

## Key Files

- `Assets/_Project/Scripts/Data/Dreamcatcher/DcMechanic.cs` — `DcAttackModKind/Spec` (정의계층)
- `Assets/_Project/Scripts/Battle/Combat/Projectile/BounceRetarget.cs` — 순수 재타겟 (아키텍처-중립 로직층)
- `Assets/_Project/Scripts/Battle/Combat/Projectile/ProjectileHitSystem.cs` — 튕김 arm (ECS 글루)
- `Assets/_Project/Scripts/Battle/Combat/DcAttackModSlot.cs`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 부착(attackMods 루프) + SpawnProjectile 피핑
- `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs` — Homing request 집계 주입
- `Assets/_Project/Data/Dreamcatcher/Card_BouncyBead.asset`

## Verified

- 컴파일 클린. EditMode 588 그린(BounceRetargetTests 6/6 포함, 회귀 0).
- ecs-reviewer(unit 2): CRITICAL/HIGH 0, 6체크포인트 CONFIRMED SAFE(RefRO+ecb.SetComponent / outputs RW / 이중감쇠 없음 / excludeIndex 유효 / Temp 무누수 / bounce=0 무회귀).
- 부착 정적 검증(unit 3): 슬롯 값·근접 거절·2장 독립. 사용자 육안(unit 4): bounce 아처 화살 재비행.

## Notes (되돌리면 안 되는 의도)

- **`FindNext` 는 float3 를 받는다** — LocalTransform 로 바꿔 aoePositions 할당 없애지 말 것(아키텍처-중립 > Temp 1개, 사용자 확정).
- **A→B→A 재히트 = 의도** (직전만 제외). 적 2마리 상황에서 튕김 지속 목적. 전체 히스토리 제외는 후속.
- **감쇠 state.damage + outputs 양쪽** (splash/fallback 이 state.damage 사용) — 이중 차감 아님.
- **bounce 는 기본공격 Homing 만** — ballistic/dc캐리어 제외.

## Follow-up (README 후속 후보)

- non-Damage output 감쇠(현재 Damage 만) · bounceDamageMul 하한 가드(unit 3 부착) · 튕김 히스토리 전체 제외 옵션 · ballistic/TileAoe 튕김 해석 · bounce 를 유닛 고유 능력으로(authoring 노출) · 전용 투사체 뷰/SFX · pierce/crit 등 개조형 kind 확장 · 튕김 히트 로깅(shooter 참조).
