# 3 — priority 직접 피해 +20% + Threat 동기

## 목적

`끝을 보는 눈`의 잠긴 주 대상이 **실제 Damage-kind 피해자**가 될 때만 그 피해를 `damageMulSnapshot`(기본 1.2, 2장 1.44)배 한다. `IncomingDamage`와 `ThreatTable.TryCredit`에 **동일 finalDamage**를 써서 desync를 막는다. splash/secondary/비-priority는 기본 피해.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs` — 근접 Damage output + 투사체 request에 priority 전달.
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 투사체 request→state drain에 `priorityTarget`/`priorityDamageMul` 복사.
- `Assets/_Project/Scripts/Battle/Combat/Projectile/ProjectileHitSystem.cs` — direct victim/TileAoe victim이 priority일 때 배율.
- `Assets/_Project/Tests/EditMode/` — 피해 통합 테스트.

## 구현

### 근접(멜리) — AttackSystem RESOLVE outputs 루프

- lock이 active·targetIsPriority이고 `hitTarget == lock.target`인 Damage output만 `dmg *= lock.damageMulSnapshot`.
- 그 dmg를 `IncomingDamage`와 `ThreatTable.TryCredit` 양쪽에 동일 사용. secondary(다중타겟)는 미적용.

### 투사체 request — AttackSystem RESOLVE

- homing/ballistic `ProjectileSpawnRequest`에 `priorityTarget = targetIsPriority ? lock.target : Null`, `priorityDamageMul = targetIsPriority ? damageMulSnapshot : 0`.
- Bridge drain이 request→state로 verbatim 복사(기존 bounce/owner 선례).

### ProjectileHitSystem — victim 시점 적용

- SingleSplash direct(outputs Damage + no-outputs) 및 bounce direct victim: `target == state.priorityTarget`면 `mul = priorityDamageMul > 0 ? priorityDamageMul : 1`. `IncomingDamage`+`TryCredit` 동일 적용. **splash secondary는 미적용**(priority여도 base).
- TileAoe victim 루프: `victims[i] == state.priorityTarget`면 동일 배율.
- bounce는 direct victim이 매 홉 바뀌므로 A→B→A 복귀 시 A에 다시 적용(자연 성립).

## 완료 기준

- [x] compile green (rig batchmode). — 2026-07-14
- [x] 피해 통합 테스트 green: melee primary ×1.2/secondary base, homing direct ×1.2, splash secondary base(priority여도), TileAoe priority만 ×1.2, non-match/fallback base. Threat는 IncomingDamage와 **동일 변수(dmg/vdmg)** 라 desync 구조적 불가. — 2026-07-14
- [x] 기존 EditMode 스위트 무회귀: total 743 / passed 741 / failed 0 / skipped 2. — 2026-07-14
- [x] non-priority/폴백 대상 배율 없음 확인(Frontmost_Fallback_NoPriorityBonus). — 2026-07-14
