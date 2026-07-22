# 6. Handoff Summary

> 실드 파괴(OnShieldBreak) 트리거 + 드림캐쳐 2종 인계. 최신 계약은 [README.md](README.md) + 번호 문서 우선.

## Commit

- `5959fc08` unit 0 — `DcTriggerKind.OnShieldBreak` + `ShieldBreakEventsSingleton` + DamageApplicationSystem 탐지/emit + 채널
- `eb5e86ee` unit 1 — `DcPayloadKind.AreaSleep` 정의 + bake
- `4fafdbff` unit 2 — `DrainShieldBreakEvents` 실행(SelfTileAoe 폭발 / AreaSleep 수면)
- `5cf880b0` unit 3 — 카드 2종(`Card_ShieldBurst`/`Card_ShieldLull`) + 카탈로그
- `7140b4b4` unit 5 — 배틀로그 `shield_break_events`(트리거 + 대상별 효과)
- `40a71b48` tune — 고요한 파문 범위 2→1·대상 3→2

## Implemented

- **트리거**: 부여된 실드가 **피격으로 완전 소진(Sum>0→0)** 되는 순간 발동. `DamageApplicationSystem` 의 `ShieldMath.Absorb` 전후 Sum 비교로 감지 → host `DcTriggerSlot`(OnKill 선례 RO) OnShieldBreak 슬롯 읽어 emit. **시간만료 배제**(Absorb 경로 전용, 애초에 실드에 duration 없음).
- **산산조각**(`shield_burst`, OnShieldBreak+SelfTileAoe): host 중심 3×3 폭발 80. `SpawnProjectile` SkyFall×TileAoe(OnDeath 폭발 동형).
- **고요한 파문**(`shield_lull`, OnShieldBreak+AreaSleep): host 중심 3×3(튜닝 후) 가까운 2명 2.5초 수면. `AoeTargetCap`(결정론) + `EffectSpawner.ApplyCc(Sleep)`.
- **배틀로그**: `shield_break_events[]` = host_unit·tile·payload·affected_count·time + targets[]{tile,effect,magnitude}. 수면=적용대상 정확, 폭발=cast 시점 범위 스냅샷.

## Key Files

- `Scripts/Data/Dreamcatcher/DcMechanic.cs`(OnShieldBreak·AreaSleep enum) · `Battle/Units/ShieldBreakEvent.cs`
- `Battle/Units/DamageApplicationSystem.cs`(탐지/emit) · `Bridge/BattleBridge.cs`(`DrainShieldBreakEvents`·`CollectShieldBreakTargets`·채널) · `Bridge/BattleBridge.Dreamcatcher.cs`(AreaSleep bake)
- `Logging/BattleLogSchema.cs`·`BattleLogger.cs`(`RecordShieldBreak`)
- `Data/Dreamcatcher/Card_ShieldBurst.asset`·`Card_ShieldLull.asset`·`DreamcatcherCardCatalog.asset`

## Verified

- 컴파일 CS 에러 0(전 유닛). 카탈로그 sync 테스트 어서션 충족.
- **사용자 Play + 로그 진단**: `shield_break_events` 실측 — AreaSleep 대상 전부 Chebyshev ≤ 범위·개수 cap 준수(범위/개수 정상). "넓게 체감" 원인 = 재실드→재파열 연발(9초 5회)로 판정 → 튜닝(범위/대상 완화)으로 대응.

## Notes (되돌리면 안 되는 의도)

- 파열 감지 = **Absorb 로 Sum>0→0** 전용. 시간만료가 후에 생겨도 그 경로는 안 타므로 배제 유지.
- 호스트 = **실드 받은 유닛**(shield-guardian 등이 씌운 실드). 발동 중심 = host 위치.
- A(데미지)=SelfTileAoe 재사용(트리거 무관 bake), B(수면)=신규 AreaSleep(신규 DcPayloadSpec 필드 0, magnitude=M·tileRange=N·duration=L 재사용).
- 로그 중복 타일 = 같은 칸 적 겹침(per-엔티티, 버그 아님).

## Follow-up

- **재발동 쿨다운**(옵션 1): host별 파열 후 N초 재발동 억제 — 연발 근본 완화(현재는 수치 튜닝만).
- 로그 개선: 대상 dedup(같은 칸 1건) + host_unit `<unknown>`(파열 프레임 host 미등록) 보정.
- 실드 파열 전용 VFX/사운드. `AreaCc` 일반화(ccKind Stun/Sleep) — 현재 Sleep 전용.
