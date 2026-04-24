# 13. Prop Distribution Rewrite (Poisson + cluster + jitter)

## 목적

`BackgroundPropPlacer` 를 `3_decor_placement_rules.md` 의 Pass 0~5 로 전면 재작성. 가중치 랜덤을 버리고 Poisson-disk + cluster + jitter 로 유기 분포.

## 전제 (모두 완료 필수)

- `8` (Placer → Plan 전환): placer 가 plan 단일 입력을 받음.
- `12` (Decor anchor 5종 확장): anchor 5종 모두 생성.
- `9` (cell `decorBudgetBias` 채움): 가중치 재료.

## 변경 대상

- `Assets/_Project/Scripts/Data/BackgroundPropPlacer.cs` (전면 재작성)
- `Assets/_Project/Scripts/Data/PropData.cs` (필드 추가)
- `Assets/_Project/Scripts/Data/PropPlacement.cs` (rotation/scale/clusterId)
- `Assets/_Project/Scripts/Core/MapView.cs::InstantiateBackgroundProps`
- `Assets/_Project/Tests/EditMode/BackgroundPropPlacerTests.cs`
- forest theme `PropData` assets

## 구현 가이드

1. `PropData` 필드 추가 (3 번과 동일):
   - `preferredAnchorTypes`, `preferredRegionSizeMin`
   - `clusterRadius`, `clusterCount`, `clusterProbability`
   - `rotationJitterDegrees`, `scaleJitter`
   - `pathProximityRange`, `borderProximityRange`
2. `PropPlacement` 확장: `rotationYaw`, `scale`, `clusterId`.
3. 알고리즘 (Pass 0~5):
   - Pass 0: plan/rng/occupancy 준비. `plan.goal`, `plan.spawns` 활용.
   - Pass 1: 각 Env region 에 Poisson-disk 후보 분포. `d_min` 은 region 크기 + theme density 로 결정.
   - Pass 2: `plan.DecorAnchors` 에서 seed 선정. 우선순위 `RegionCenter → OuterBorder → NearWalkButSafe → Filler`. anchor type 별 weight 는 theme 관리.
   - Pass 3: 각 seed 에 대해
     - `PropData` 후보 필터 (`allowedZoneTypes`, `preferredAnchorTypes`, `preferredRegionSizeMin`, `pathProximityRange`, `borderProximityRange`)
     - weight = `placementWeight × decorBudgetBias × theme multiplier × (spawn/goal 인접 시 density 감소)`
     - weighted random 선정
     - `clusterProbability` 로 cluster 모드 → `clusterRadius` 안의 k 개 Poisson 후보에 같은 prop
     - footprint / occupancy 검증 후 반영
   - Pass 4: rotation/scale jitter
     - `rotationYaw = rng.NextFloat(-rotationJitterDegrees, +rotationJitterDegrees)` (FullCamera 프랍은 0)
     - `scale = 1 + rng.NextFloat(-scaleJitter, +scaleJitter)`
   - Pass 5: decor scatter (visual only, `Env` 만, `Filler`/`RegionEdge` anchor, occupancy 미생성)
4. Poisson: rejection sampling 으로 시작. 성능 이슈 시 grid 기반 Bridson.
5. `MapView.InstantiateBackgroundProps`: `placement.rotationYaw` / `scale` 반영.
6. 테스트:
   - deterministic: 동일 plan × 동일 seed → placement 비트 동일
   - `Walk` / `Place` 침범 0
   - cluster 모드 활성 시 같은 prop family 가 `clusterRadius` 이내 복수 배치
   - rotation/scale 이 jitter 범위 내
   - spawn/goal 인접 density 감소

## 완료 기준

- Forest theme Play smoke screenshot 에서 cluster / scatter 분포 육안 구분됨
- 복붙 인상 없음 (같은 prop 의 yaw/scale 서로 다름)
- `BackgroundPropPlacerTests` 전원 통과
- Walk / Place footprint 침범 0
- spawn/goal 주변 density 감소
- deterministic 보장

## 주의

- jitter 는 `PropBillboardMode.FullCamera` 프랍에 rotation 적용 금지.
- cluster 는 같은 prop family 만 확장. 다른 prop 금지.
- `System.Random` 금지, `Unity.Mathematics.Random` 사용.
- Poisson rng 와 prop 선정 rng 는 같은 stream 허용. 단 **Pass 간 호출 순서 고정** — 바뀌면 deterministic 깨짐.
- cluster 수 상한 (theme 전역 또는 region cellCount 의 일정 비율) 으로 같은 prop 이 지나치게 몰리는 것을 방지.

확인 일자: 2026-04-24 / 커밋 해시: 95d3879
