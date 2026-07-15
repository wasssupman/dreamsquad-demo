# 4. 레드불 픽업 아키타입 — 엔티티 정의 + 주기 스폰

## 목적

야근 룰 2 의 절반. 기믹 활성 시 `redbullSpawnInterval`(5s)마다 **이동/배치 타일영역(Walk∪Place)** 의 임의 셀에 레드불 픽업 엔티티가 생성되고, 미소비 시 `redbullLifetime` 후 만료된다. 소비 판정과 라스트런 효과는 unit 5.

## 핵심 발견 (조사 결과)

- `MapTileType`: **Walk(이동)** 과 **Place(배치)** 는 별개 셀 타입. "이동/배치 타일영역" = Walk∪Place.
- `FlowFieldSingleton.dist` 는 **Walk 셀만** 반영 (Place 미포함). 어떤 기존 ECS 싱글턴에도 Place 셀 집합이 없다.
- → 후보 셀 배열(Walk∪Place)을 BattleBridge 가 `_generatedMap` 에서 만들어 싱글턴으로 실어준다 (FlowFieldSingleton 과 동형: Persistent NativeArray, BattleBridge 생성/dispose).

## 변경 대상

- `Assets/_Project/Scripts/Battle/Effects/Pickup.cs` — `PickupKind` enum + `Pickup` 컴포넌트
- `Assets/_Project/Scripts/Battle/Effects/PickupSpawnState.cs` — 싱글턴 (후보 셀 + cadence + rng)
- `Assets/_Project/Scripts/Battle/Effects/PickupSpawnSystem.cs` — 스폰 + 만료 시스템
- `Assets/_Project/Scripts/Core/MatchSeed.cs` — `PickupSalt` + `DerivePickupSeed`
- `Assets/_Project/Scripts/Data/Gimmick/OverworkGimmickData.cs` — `redbullLifetime`
- `Assets/_Project/Scripts/Battle/Effects/OverworkGimmickConfig.cs` — `redbullLifetime`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `BuildPickupSpawnState` + teardown + config 복사 + 디버그 로그
- `Assets/_Project/Data/Gimmick/Gimmick_Overwork.asset` — redbullLifetime 값
- `FatigueDebugMenu.cs` — "Log Redbull Pickups" 메뉴 (검증용)

## 구현

1. **Pickup** (Effects 소유): `{ int2 cell; PickupKind kind; float remainingLife }`. `PickupKind : byte { Redbull }` (append-only, StackKind/StatusFxKind 전례).
2. **PickupSpawnState** (싱글턴, Effects): `{ NativeArray<int2> candidateCells; float elapsed; Unity.Mathematics.Random rng }`.
   - candidateCells = Walk∪Place 셀. **BattleBridge 소유** — Persistent 할당, `BuildPickupSpawnState`(BuildFlowField 직후) 생성, `TeardownFlowField` dispose (맵 재빌드 lifecycle 공유).
   - elapsed/rng 는 Effects(PickupSpawnSystem)가 mutate. rng seed = `MatchSeed.DerivePickupSeed(matchSeed)` (재현 가능, 판마다 고정).
3. **PickupSpawnSystem** (Effects, BattleSimGroup): `RequireForUpdate<PickupSpawnState>` + `<OverworkGimmickConfig>` self-gate.
   - **만료**: 각 Pickup `remainingLife -= dt`, 0 이하 ECB.DestroyEntity.
   - **스폰**: `elapsed += dt`; `elapsed >= interval` 마다 후보 중 rng 로 셀 선택. 이미 픽업이 있는 셀은 회피(NativeHashSet, 최대 8회 재시도 — 포화 시 skip). 프레임당 스폰 수 상한(안전).
   - 구조 변경은 `EntityCommandBuffer(Temp)` 일괄 playback.
4. **BattleBridge**: `_pickupSpawnStateSingleton` 필드. `BuildPickupSpawnState()` 가 gimmick 활성 시 `_generatedMap.tiles` 순회해 Walk∪Place 셀 수집 → 싱글턴 생성. `DestroyBattleEntities`/`CleanupDraftMapBeforeRebuild` 에 `DestroyEntitiesByType<Pickup>()` 추가 (엔티티 정리). `CreateGimmickConfigIfActive` 가 redbullLifetime 복사.

## 완료 기준

- compile 통과 + 콘솔 클린.
- 활성 시즌 기믹 연결 상태로 Play → 전투 진입 후 5초마다 레드불 스폰 (디버그 메뉴 "Log Redbull Pickups" 로 개수/셀 확인, 셀이 보드 범위 내 Walk∪Place). 만료(개수 상한 유지) 확인.
- gimmick=null → 픽업 0개 (self-gate).
- (시각 표현은 unit 6, 소비/라스트런은 unit 5.)

확인 2026-07-15 · 커밋 `a5d89682` — Play 실측(로그): 후보 123셀, 5초 주기 스폰(남은수명 16.5/11.5/6.5/1.5s = 5s 간격), 정상상태 4개(=lifetime20/interval5) 만료, 매치 경계 teardown+재주입.
