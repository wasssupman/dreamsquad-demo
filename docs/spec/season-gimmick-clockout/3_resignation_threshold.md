# 3. 사직서 임계 → 메테오 barrage 요청

## 목적

룰 2 전반: 살아있는 사직서가 `resignationThreshold`(5) 도달 시 **그 수만큼 소모(destroy)** + **메테오 barrage 요청 enqueue**. 실제 메테오 cast 는 unit 4(BattleBridge drain). 번아웃 Consume 전례(임계 소모 후 재누적).

## 변경 대상

- `Assets/_Project/Scripts/Battle/Effects/MeteorBarrageRequest.cs` — 신규 큐 원소 struct
- `Assets/_Project/Scripts/Battle/Effects/MeteorBarrageRequestsSingleton.cs` — 신규 NativeQueue 채널(Effects→Bridge)
- `Assets/_Project/Scripts/Battle/Effects/ResignationThresholdSystem.cs` — 신규 임계 소모 + 요청 시스템
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 큐 lifecycle(생성/dispose/singleton 파괴)
- `CLAUDE.md` — NativeQueue 채널 목록 18→19

## 구현

1. **`MeteorBarrageRequest { int meteorCount }`**: 큐 원소(plain struct, `DefenderDeathEvent` 전례). `meteorCount` = 트리거 시 config baked. 나머지 cast 파라미터(damage/range/warning/stagger/projectile)는 BattleBridge 가 배정 기믹 SO 에서 읽음(unit 4).
2. **`MeteorBarrageRequestsSingleton { NativeQueue<MeteorBarrageRequest> queue }`**: BattleBridge 소유 lifecycle(기존 `RequestsSingleton` 전례 — 생성/dispose/singleton 파괴 3곳).
3. **`ResignationThresholdSystem`**(Effects, Burst): `RequireForUpdate<ClockOutGimmickConfig>` + `<MeteorBarrageRequestsSingleton>`. 살아있는 `Resignation` count ≥ threshold 면 `barrages = count/threshold` → `threshold*barrages` 개 destroy(ecb) + `barrages` 회 enqueue. 잔여(<threshold)는 다음 사이클 재누적.
4. **BattleBridge**: 큐 생성(dispose-first) + teardown dispose + singleton 파괴 — 기존 큐 전례 동형.

## 완료 기준

- compile 0 에러(Unity 재컴파일).
- (로직) 사직서 threshold 개 도달 시 그만큼 소멸 + 큐에 요청 enqueue. 실 메테오 착탄은 unit 4. Play 실측은 unit 5 asset 배선 후.

확인 2026-07-16 — Unity 재컴파일 후 read_console 에러 0(authoritative). CLAUDE.md 채널 18→19 반영.
