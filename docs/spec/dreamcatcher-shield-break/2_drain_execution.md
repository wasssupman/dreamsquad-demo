# 2. DrainShieldBreakEvents 실행 [ECS]

## 목적

유닛 0 의 로그 stub 을 실제 페이로드 실행으로 교체. payload 분기 — SelfTileAoe 폭발(A) / AreaSleep 수면(B). 실드 파열 → 실제 효과 발현.

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `DrainShieldBreakEvents` 본문(로그 → 분기 실행) + 헬퍼 `ApplyShieldBreakAreaSleep`.

## 구현

`DrainShieldBreakEvents` while 루프 내 `evt.payload` 분기:

1. **SelfTileAoe(A, 폭발)**: `evt.aoeDataIndex >= 0` 이면 `SpawnProjectile(ProjectileSpawnRequest{ movement=SkyFall, payload=TileAoe, impact=evt.position, damage=evt.magnitude, impactTileRange=evt.tileRange, flightTime=0, dataIndex=evt.aoeDataIndex, visualScale=1 }, Entity.Null)`. **OnDeath 폭발(`DrainDefenderDeathEvents`)/메테오와 동형** — Combat 투사체 코드 불변.
2. **AreaSleep(B, 수면)** → `ApplyShieldBreakAreaSleep(evt)`:
   - `centerCell = GridMath.WorldToCell(evt.position, tileSize, gridSize, _boardOrigin)` (bridge line 1883 선례).
   - 적 쿼리 `CreateEntityQuery(AttackUnitTag, LocalTransform)` → 각 적 `WorldToCell` → `TileAoe.IsInTileRange(cell, centerCell, evt.tileRange)` 통과분만 수집 + impact 중심 거리²(dx²+dz²). **bomb-thrower AoE(ProjectileHitSystem) 패턴 미러**.
   - `AoeTargetCap.SelectNearest(distSq, cap=(int)evt.magnitude, selected)` — 가까운 순 M명(동률=인덱스, 결정론).
   - 각 선정 적에 `EffectSpawner.ApplyCc(_em, victim, CcEffect{ kind=Sleep, remainingTime=evt.duration })`. 적은 CcEffect 버퍼 보유(스폰 시), `ApplyCc`=Effects 외부 CC 적용 choke point. Temp 컨테이너 dispose.

## 완료 기준

- Unity 재컴파일 CS 에러 0.
- (유닛 3~4 Play 에서 실증) A: 실드 파열 시 host 중심 TileAoe 데미지. B: N타일 내 가장 가까운 M명이 L초 수면(범위 밖/초과분 제외).
- 맥락 경계: 적 CC 적용은 `EffectSpawner.ApplyCc`(Effects choke point)로만. 폭발은 기존 `SpawnProjectile` 프리미티브.
