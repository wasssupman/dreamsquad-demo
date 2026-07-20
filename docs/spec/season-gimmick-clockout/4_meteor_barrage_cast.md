# 4. 메테오 barrage cast (BattleBridge)

## 목적

룰 2 후반: `MeteorBarrageRequest` 를 drain 해 맵의 이동(Walk) 타일 임의 `meteorCount`(3)곳에 **SkyFall×TileAoe 메테오를 순차 낙하**(적에게만 피해). 기존 투사체 cast(`SpawnProjectile`)를 그대로 재사용 — Combat 투사체 코드 불변.

## 변경 대상

- `Assets/_Project/Scripts/Core/MatchSeed.cs` — `DeriveMeteorSeed`(결정론 셀 stream, 기존 salt 패턴)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `_meteorRng` 필드 + ClockOut config 주입 시 seed + `DrainMeteorBarrageRequests` + drain 시퀀스 호출

## 구현

1. **`MatchSeed.DeriveMeteorSeed`**: `MeteorSalt` 추가(Pickup/Gimmick salt 미러). 순수 함수.
2. **`_meteorRng`**(`Unity.Mathematics.Random`): `CreateGimmickConfigIfActive` 의 ClockOut 분기에서 `DeriveMeteorSeed(_matchSeed)` 로 seed(map build, 매치당 1회 → **같은 matchSeed = 같은 셀 시퀀스**). 요청은 config 주입 이후에만 발생하므로 seed 선행 보장.
3. **`DrainMeteorBarrageRequests`**(매 프레임 drain 시퀀스, `DrainHazardSpawnRequests` 뒤): 큐 비면 early-return. ClockOut/`meteorProjectile`/맵 미준비면 비우고 드롭(미지정 시 warning). 요청마다 Walk 셀 수집 → `_meteorRng` 로 미중복 `meteorCount` 곳 선택 → 각 셀에 `SpawnProjectile(new ProjectileSpawnRequest{ SkyFall×TileAoe, impact=셀중심, damage/tileRange=cd, flightTime=warning+i·stagger(순차), dataIndex=cd.meteorProjectile, arcHeight=dropHeight, targetFaction=Enemy }, Entity.Null)`.
4. content-1 OnDeath 폭발 cast 와 동형(`SpawnProjectile(...,Entity.Null)` 셀 타겟 SkyFall×TileAoe). 리워크는 가법적이라 이 경로 불변.

## 완료 기준

- compile 0 에러(Unity 재컴파일).
- (통합 — unit 5 asset 배선 후 Play): 사직서 5장 소모 → Walk 타일 3곳에 메테오 순차 낙하 → 적만 피해. 결정론(같은 matchSeed → 같은 착탄 셀 시퀀스). Play 실측은 unit 5.

확인 2026-07-20 — Unity 재컴파일 후 read_console CS 에러 0(Burst JIT 캐시 경고만, 환경성). Play 실측은 unit 5 asset 배선 후.
