# 17. Prop Distribution Proper Pass (Poisson + debug marker)

## 목적

rev3 13 구현에서 `BackgroundPropPlacer` 가 Poisson-disk 를 생략하고 anchor-only + cluster offset 으로 축약됐다. audit V-001 / V-002 에서 두 개의 High 결함이 확증:

- **V-001**: 같은 prop family 가 cluster 로 읽히지 않고 anchor 하나당 단일 개체로 배치됨. 빈 Env region 이 남음.
- **V-002**: prop 아래 **흰 사각 footprint marker** 가 debug marker 처럼 보이는 artifact.

본 spec 은 두 문제를 한 묶음으로 해결한다. 알고리즘(V-001) 보다 marker 버그(V-002) 가 단순하고 임팩트가 즉시 크므로 V-002 먼저 제거 후 V-001 로 이동.

## 전제

- `8` (Placer → Plan), `12` (anchor 5종), `13` (prop rewrite 1차), `16` (audit) 완료.
- audit screenshot `seed12345_game_full.png` 이 prop marker 가 시각적으로 남아 있음을 확증.

## 변경 대상

### V-002 조사 및 제거
- `Assets/_Project/Scripts/Presentation/PropBillboard.cs`
- `Assets/_Project/Editor/PropDataEditor.cs` (Generate Billboard 로직)
- `Assets/_Project/Prefabs/Props/forest/**` (prefab 내 marker child)
- `Assets/_Project/Scripts/Core/MapView.cs::InstantiateBackgroundProps` (marker quad 생성 여부)

### V-001 Poisson 정식 도입
- `Assets/_Project/Scripts/Data/BackgroundPropPlacer.cs` (Pass 1/2/3 재작성)
- `Assets/_Project/Scripts/Data/MapThemeData.cs` (density/min-spacing 파라미터 노출)
- `Assets/_Project/Tests/EditMode/BackgroundPropPlacerTests.cs`

## 구현 가이드

### Step 1. V-002 root-cause

1. prefab 한 개(예: `prop_style_round_tree_1_1`) 를 열어 child GameObject 목록 확인. Quad/Plane 이 있으면 footprint marker.
2. 없다면 `PropBillboard` runtime 생성 경로 확인. MeshRenderer + 흰 material 이 기본값으로 활성화된 case 의심.
3. `MapView.InstantiateBackgroundProps` 가 footprint 표시용 quad 를 runtime 에 추가하는지 grep.
4. 원인 확인 후:
   - prefab 문제면 prefab 의 marker child 비활성화 (SetActive false) 또는 삭제
   - runtime 생성이면 코드 제거
   - Editor-only 표기면 `#if UNITY_EDITOR` 로 감싸 build 에서 제외

완료 기준: audit screenshot 조건에서 prop 아래 흰 사각형이 보이지 않음.

### Step 2. Poisson-disk sampling

1. `BackgroundPropPlacer.Generate` 내부에 `GenerateRegionCandidates(region, d_min, rng)` 신규 메서드 추가.
2. Bridson 변형:
   - region cellCount 에 비례해 cell grid 생성 (여기서는 1셀 = 1 샘플 단위)
   - active list 에서 후보 pop → k=30 회 반경 `[d_min, 2*d_min)` 내 후보 생성 → 거리 조건 통과하면 active list 에 추가
   - 결과 = candidate cell list (정수 좌표)
3. 단순 구현(rejection sampling) 도 허용:
   - region cell 중 min distance `d_min` 만족하는 점을 무작위로 고름 (attempts 상한)
4. `d_min` 산출:
   - `d_min = clamp(1, round(sqrt(region.cellCount / (theme.density * theme.densityPerRegionArea))), region_diameter/2)`
5. candidate 상한 = `region.cellCount * min(theme.density, 1.0)`.

### Step 3. Cluster 확장

1. seed 선정은 `plan.DecorAnchors` 유지. anchor type 별 weight 로 우선순위.
2. seed 마다:
   - `PropData` 후보 필터 + weighted random
   - 선정 prop 이 `clusterProbability > 0` 이면 cluster 모드 활성
   - cluster 모드: 방금 뽑은 Poisson candidate 중 **seed 와 가까운 k = clusterCount 개** 선택, 같은 prop 으로 채움
   - non-cluster: candidate 1 개만
3. Poisson candidate 가 0 개로 반환되면 fallback 으로 anchor cell 자체 사용 (현 동작).

### Step 4. 테스트

- deterministic: 동일 seed × 2 회 → placement 비트 동일 (Poisson 순서 고정 필수)
- cluster 활성 prop 2 개 이상이 `clusterRadius` 이내 배치되는 케이스 assert
- region 큰 경우 candidate 수가 cellCount × density 범위 내
- V-002 회귀 방지: runtime instance 의 child 에 MeshRenderer + 흰 material 조합이 없음 assert (가능한 범위에서)

## 완료 기준

- V-002 marker 모든 forest prop 에서 시각 제거 (audit 재캡처 비교)
- V-001 재감사에서 cluster 모드 prop 이 군집으로 읽힘 (`Dispatch` 에서 V-001 status → Mid/Low 로 내려감)
- deterministic 유지, 기존 tests 통과
- Poisson candidate 수가 region 크기에 선형 비례
- Unity console error 0

## 주의

- V-002 는 단순 버그일 가능성이 높음. Step 1 완료 전 Step 2 착수 금지 (같은 screenshot 으로 후속 튜닝 판단이 왜곡됨).
- Poisson 의 rng 와 prop 선정 rng 는 같은 stream 사용 허용. **Pass 간 호출 순서 고정** 필수 (deterministic).
- cluster 수 상한을 `region.cellCount / 8` 또는 theme param 으로 캡. 한 region 이 같은 prop 으로 도배되는 것 방지.
- `System.Random` 금지, `Unity.Mathematics.Random` 사용.

확인 일자: 2026-04-24 / 커밋 해시: 818712b
