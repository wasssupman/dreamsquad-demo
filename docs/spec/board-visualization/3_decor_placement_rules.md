# Decor Placement Rules (rev3)

## 역할

프랍 배치는 생태계 시뮬이 아니라 **보드 장식 규칙**이다. rev3 에서는 Poisson-disk + cluster + jitter 기반 분포로 Enter-the-Gungeon 수준의 유기성을 낸다.

## 유지할 것

- `PropData`
- footprint 기반 배치 (occupancy 충돌 방지)
- deterministic seed
- placement 는 `BoardVisualPlan` 만 입력으로 받는다 (`8`)

## 바꿀 것

- best-fit center 기반 단일 배치 → **Poisson-disk 후보 분포**
- repeat avoidance → **cluster seed 로 치환** (같은 prop family 가 가까이 모일 수 있게)
- 회전/스케일 고정 → **rotation/scale jitter**
- `GeneratedMap` 단독 입력 → **`BoardVisualPlan` 입력** (spawn/goal 포함, `8`)

## 배치 원칙

1. `Walk` 직접 점유 금지
2. `Place` 기본 점유 금지
3. `Env` 주 배치 대상
4. 큰 프랍은 넓은 region + 외곽 우선
5. 경로 인접 셀은 작은 프랍 위주
6. spawn/goal 부근 밀도 낮춤 (`plan.goal`, `plan.spawns` 사용)
7. cluster 는 같은 prop family 내에서만
8. jitter 로 복붙 인상 제거

## 배치 패스

### Pass 0. 입력 준비

- 입력: `BoardVisualPlan`
- rng = deterministic(`visualSeed XOR placementSeedOffset`)
- occupancy grid 초기화

### Pass 1. Poisson-disk 후보 분포

각 Env region 에 대해:
- region 크기 + theme density 로 `d_min` 계산
- Bridson 변형 또는 rejection sampling 으로 region 내부 Poisson-disk 샘플 집합 생성
- candidate 상한 = region cellCount × density

### Pass 2. Cluster seed 선정

- `plan.DecorAnchors` 의 5종 중 `RegionCenter` → `OuterBorder` → `NearWalkButSafe` → `Filler` 순서로 seed 후보 방문
- anchor type 별 weight 는 theme 에서 관리
- seed 별 `PropData.clusterProbability` 로 cluster 모드 활성화

전제: anchor 5종 구현이 완료되어 있어야 함 (`12` 선행). 그렇지 않으면 seed 후보가 RegionCenter/RegionEdge 2종뿐이라 cluster 전략이 작동하지 않는다.

### Pass 3. Candidate 채우기

각 seed 에 대해:
1. `PropData` 후보 중 `allowedZoneTypes`, `preferredAnchorTypes`, `preferredRegionSizeMin`, `pathProximityRange`, `borderProximityRange` 를 만족하는 것만 필터
2. `decorBudgetBias` (셀에서 읽음) + theme 가중치로 weight 조정
3. spawn/goal 인접 셀은 theme `spawnGoalPropDensityMultiplier` 로 다운
4. weighted random 으로 prop 선정
5. cluster 모드면 Poisson 후보 중 seed 근처 k 개를 같은 prop 으로 채움
6. 각 placement 는 footprint + occupancy 검증 후 반영

### Pass 4. Rotation / Scale jitter

각 placement 마다:
- `rotationYaw = rng.NextFloat(-prop.rotationJitterDegrees, +prop.rotationJitterDegrees)`
- `scale = 1 + rng.NextFloat(-prop.scaleJitter, +prop.scaleJitter)`
- `PropBillboardMode == FullCamera` 프랍은 rotation 적용 안 함 (카메라 회전으로 의미 없음)

### Pass 5. Decor scatter (visual-only)

- `Env` 만 대상
- anchor `Filler`, `RegionEdge` 기반 scatter
- occupancy 미생성 (gameplay 영향 없음)
- `Walk` 인접 밀도 낮춤

## PropData 필드 확장 (13 에서 구현)

| 필드 | 의미 |
|---|---|
| `footprintX`, `footprintY` | 유지 |
| `placementWeight` | 유지 |
| `minDistanceCells` / `minSpacing` | 유지 |
| `allowedZoneTypes` | `Env` 단일 (v0) |
| `preferredAnchorTypes` | 허용 anchor 집합 |
| `preferredRegionSizeMin` | 최소 region 크기 |
| `clusterRadius` | cluster 반경 (셀) |
| `clusterCount` | cluster 당 최대 배치 수 |
| `clusterProbability` | 0~1. seed → cluster 모드 확률 |
| `rotationJitterDegrees` | 회전 지터 (0~180) |
| `scaleJitter` | 스케일 지터 (0~0.5) |
| `pathProximityRange` | 경로 인접 허용 범위 |
| `borderProximityRange` | 외곽 인접 선호 범위 |

## PropPlacement 필드 확장 (13 에서 구현)

| 필드 | 의미 |
|---|---|
| `propIndex`, `x`, `y`, `width`, `height` | 유지 |
| `rng` | 유지 |
| `rotationYaw` | Pass 4 결과 |
| `scale` | Pass 4 결과 |
| `clusterId` | cluster 식별자 |

## Theme Data 방향

theme 에서 관리:
- `anchorWeights[anchorType]`
- `clusterSeedBias` (anchor type 별)
- `densityPerRegionArea`
- `forestPropFamily[]`, `decorScatterPropFamily[]`

## 결정론

- Poisson sampling 은 seed 기반 고정
- cluster 선택은 seed 기반
- jitter 는 seed + placementIndex 기반
- 동일 plan + 동일 seed 면 placement 시퀀스 비트 동일
- `System.Random` 금지, `Unity.Mathematics.Random` 사용

## v0 목표

- cluster 또는 scatter 분포가 육안 구분됨
- 같은 prop family 반복 인상 없음
- 큰 프랍은 큰 빈 영역을 읽게 함
- `Walk` / `Place` 침범 없음
- spawn/goal 주변 밀도 낮음
