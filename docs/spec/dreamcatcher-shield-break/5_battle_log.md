# 5. 배틀로그 기록 — 트리거 + 대상별 효과 [ECS/logging]

## 목적

실드 파열(OnShieldBreak) 발동과 **누구에게 어떤 효과가 부여됐는지**를 배틀로그 JSON 에 남긴다(관측·분석·회귀). 사용자 요청. 시뮬 무영향(BattleLogger 가 유일 writer, 계약).

## 변경 대상

- `Assets/_Project/Scripts/Logging/BattleLogSchema.cs` — `ShieldBreakLog` + `ShieldBreakTargetLog` + `BattleLogEntry.shield_break_events[]`.
- `Assets/_Project/Scripts/Logging/BattleLogger.cs` — `RecordShieldBreak(log)` (time/affected_count 스탬프).
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `DrainShieldBreakEvents` 가 로그 구축 + `CollectShieldBreakTargets` 공유 헬퍼(수면 apply + 데미지 로그 스냅샷 공유).

## 구현

- **스키마**: `shield_break_events[]` 각 항목 = { host_unit(실드 깨진 유닛), tile, payload("SelfTileAoe"/"AreaSleep"), affected_count, time, targets[] }. `targets[]` = { tile(대상 적), effect("Damage"/"Sleep"), magnitude(데미지량/수면 초) }.
- **수집 공유**(`CollectShieldBreakTargets`): unit 2 의 인라인 수집을 공유 헬퍼로 승격 — WorldToCell+IsInTileRange 범위 → AoeTargetCap(cap). `cap<=0` = 범위 전체(투사체 폭발과 동일 집합).
  - **AreaSleep**: cap=M 으로 수집 → 각 적 `ApplyCc(Sleep,L)` + 로그(effect=Sleep, magnitude=L). **실제 적용 대상 = 로그 대상**(정확).
  - **SelfTileAoe**: 실제 데미지는 투사체(ProjectileHitSystem)가 해결. 로그는 cap=0 으로 **cast 시점 범위 내 적 스냅샷**(effect=Damage, magnitude=raw). flightTime 0·동일 range 로직이라 투사체 실제 타격 집합과 사실상 일치(다만 raw 값·스냅샷임을 명시).
- **호스트 유닛명**: `FindDefenderData(evt.host)?.displayName ?? "<unknown>"`. **시간**: logger 가 스탬프.

## 완료 기준

- Unity 재컴파일 CS 에러 0.
- (Play) OnShieldBreak 카드 유닛의 실드가 피격 파열 시, 세션 JSON `shield_break_events[]` 에 host·payload·대상별(tile/effect/magnitude) 1건 기록. 수면=적용 대상 정확, 폭발=범위 스냅샷.
- 로그 실패/부재(logger null)에도 시뮬 무영향(가드).
