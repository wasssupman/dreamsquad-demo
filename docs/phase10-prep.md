# Phase 10 이관 스펙 — Procedural 맵 + 테마 시스템

> Phase 9 브레인스토밍 (2026-04-19) 에서 Q1~Q5 진행 중 도출된 "맵 시스템 재설계" 전체 스펙이 Codex 2차 리뷰에서 **scope 팽창** 판정. Phase 9 = 축 A(flow field) only 로 축소, 축 B(procedural + 테마 + multi-cell + enum 재분류) 는 본 문서로 전량 이관. Phase 9 완료 후 본 문서 기반 Phase 10 브레인스토밍 재개.

---

## 1. Phase 10 주제

**"매 판 달라지는 procedural 맵 + 테마별 배경 오브젝트 + TileType enum 재분류"**

### 1.1 검증 질문

매 플레이 달라지는 맵 위에서도 Phase 9 에서 검증된 flow field 가 안정 동작하고, procedural 로 생성된 맵이 플레이어에게 유의미한 변이(경로 길이, 배치 전략 차이)를 주는가.

### 1.2 Phase 9 와의 경계

- Phase 9 종료 시점: flow field 엔진이 고정 PrototypeMap (single-goal/single-spawn) 위에서 검증 완료, Portal/Tornado 자율 복귀 확인
- Phase 10 추가: (1) 맵 procedural 생성, (2) 테마 + multi-cell obstacle 자산 시스템, (3) TileType enum 재분류, (4) multi-spawn 확장

### 1.3 비범위 (Phase 11+ 이관)

- 환경효과 (화산, 바람 영역 등 동적 타일)
- 가변 그리드 크기 (20×10 외)
- Multi-goal (goal 복수)
- 레벨 디자이너 authoring UI (custom inspector 등)
- Addressables / 풀링

---

## 2. Phase 9 브레인스토밍에서 확정 이관된 결정

### 2.1 타일 데이터 모델 — Enum 재분류 (Q2 결정 A)

```csharp
public enum TileType : byte
{
    Empty     = 0,
    Walkable  = 1,
    Placeable = 2,
    Blocked   = 3,
}
```

- 한 타일 = 한 역할 (mutually exclusive)
- `Walkable ≠ Placeable` 전제로 defender 배치가 flow field 에 영향 없음 → Phase 9 static flow field 전제 유지
- 환경효과(화산/바람)는 별도 NativeArray layer 로 Phase 11+ 추가 예정 (enum 확장 아님)

**Codex H-3 필수 대응**: 현재 `Buildable=0 / Path=1 / Obstacle=2` 와 숫자 충돌 → `PrototypeMap.asset` silent corruption.

**Migration 전략 (P10-01)**:
1. 기존 `TileType` 은 Phase 9 까지 그대로 유지
2. Phase 10-01 에서 새 enum `MapTileType` (이름 변경으로 enum 충돌 회피) 도입
3. PrototypeMap migration script 가 기존 타입 → 새 타입 매핑:
   - `Buildable (0)` → `Placeable`
   - `Path (1)` → `Walkable`
   - `Obstacle (2)` → `Blocked`
4. 모든 참조 사이트(`MapView.cs:47-49`, `BattleBridge.cs:1009` 등) 일괄 업데이트 후 구 enum 삭제

### 2.2 Goal 모델 — Single goal + Multi spawn (Q4 결정 B)

- goal 타일 1개, spawn 타일 N개 (theme 파라미터로 구성)
- flow field 1벌 (모든 spawn 이 같은 goal 로 수렴)
- Phase 9 는 single goal + **single** spawn 이었던 것을 Phase 10 에서 multi spawn 으로 확장
- 장래 multi-goal 은 `goalId` 필드 + field N벌로 확장 가능 (Phase 11+ 후보)

### 2.3 AttackDeck.SpawnEntry.pathId 의미 전환 (Codex C-4)

- 현재 `string pathId` (값 "A" / "B") → MapData.Paths 의 id 매칭
- Phase 10 후 `int spawnTileIndex` 또는 `string spawnGroupId` — **명시적 필드 교체 + asset migration script**
- 현재 deck asset (`WaveA.asset`) 수정 또는 `[ContextMenu]` migration 제공
- 하위 호환 없음 (Phase 10 개시와 함께 신규 필드로 단절)

### 2.4 Procedural 생성 전략 — Path-first (Q5 사용자 원안 반려 → path-first 채택)

```
① 그리드 Empty 초기화 (20×10 고정, Phase 11+ 에서 가변)
② spawn(N) + goal(1) 타일 결정 (theme.spawnRule / theme.goalRule 제약 내)
③ spawn → goal 경로 carve → Walkable 마킹
④ Placeable 확보: path 인근 buffer + theme.minPlaceableTiles 쿼터
⑤ Blocked 배치: multi-cell footprint-aware greedy, theme.densityTarget 까지
```

**사용자 원안(배경-first) 반려 사유**:
- rejection sampling 함정: 배경 배치가 경로 존재성 무고려 → 도달 불가 발생 시 재시도
- 경로 품질 통제 불가: 경로가 obstacle 사이로 강제 삽입 → 지그재그 / 너무 짧거나 긴 형태
- Placeable 공간 비결정적: 사후에 "배치 가능 공간이 충분한가" 검증 필요

**Path-first 근거**:
- 연결성이 구조적으로 보장 (단 §3 H-4 의 불변조건 필요)
- 경로 품질 선제 통제 (경로 알고리즘이 길이/커브 수 제어)
- Placeable 이 "path 인근 고지" 로 자연스럽게 정렬 → TD 타워 배치 의미 명확
- Blocked 이 decorative 역할로 축소 → 의도한 역할(장식+시각적 차단)에 맞춤

### 2.5 테마 + Multi-cell Obstacle 구조

**파일 규약**:
```
Assets/_Project/Map/Theme/
├── forest/
│   ├── forest_1x1_tree.prefab
│   ├── forest_1x1_rock.prefab
│   ├── forest_2x1_log.prefab
│   ├── forest_2x2_bush.prefab
│   └── forest.asset    ← MapThemeData SO (obstacles 수동 할당)
└── desert/ ...
```

**데이터 구조**:
```csharp
public class ObstacleView : MonoBehaviour
{
    public int2 footprint;       // 파일명과 일치
    public bool canRotate;       // 2×1 only true 권장
    public int  weight;
    // 자식: SpriteRenderer / MeshRenderer
}

public class MapThemeData : ScriptableObject
{
    public string themeId;
    public ObstacleView[] obstacles;
    public int minPlaceableTiles;       // Placeable 쿼터 하한
    public int obstacleDensityPct;      // Blocked 목표 밀도
    public int2 gridSize;                // v1 은 (20,10) 고정
    public SpawnGoalConstraint spawnRule;
    public SpawnGoalConstraint goalRule;
    public PathConstraints pathRule;     // 최소/최대 길이, 커브 수 등
}

public struct PlacedObstacle
{
    public int2 anchorCell;             // top-left
    public int  obstacleAssetIdx;
    public bool rotated;
}

public struct GeneratedMap
{
    public NativeArray<MapTileType> tiles;
    public UnsafeList<PlacedObstacle> obstacles;
    public int2 goal;
    public int2[] spawns;
    public int   seed;
    public int   attemptIndex;
    public int   generatorVersion;
}
```

**Prefab pivot 규약**: footprint 중심. 인스턴스화 시 `center = (anchorCell + (footprint-1)*0.5f) * tileSize`.

**회전**: `canRotate=true` obstacle 은 2×1 ↔ 1×2 단순 90° 교체. Quaternion.Euler(0, 90, 0).

**Validation**: 전체 footprint 가 그리드 경계 안 + 전부 Empty. 한 셀이라도 Walkable/Placeable/Blocked/경계밖 이면 skip.

**Authoring 검증 (Editor script)**:
```
[InitializeOnLoad] ThemeValidator:
    Assets/_Project/Map/Theme/**/*.prefab scan
    regex: (\w+)_(\d+)x(\d+)(?:_(\w+))? → themeId, W, H, variant
    verify: prefab.ObstacleView.footprint == (W, H)
    warn mismatches at console
```

### 2.6 시각 자산 배치 전략 (runtime)

```
MapView.RenderGeneratedMap(GeneratedMap map, MapThemeData theme):
    1. obstaclesRoot 기존 자식 전체 Destroy (Codex L-3)
    2. tile cube primitive 생성 (4 TileType 에 맞게 Material 구분)
    3. foreach o in map.obstacles:
         prefab = theme.obstacles[o.obstacleAssetIdx]
         center = (o.anchorCell + (footprint-1)*0.5) * tileSize
         rot = o.rotated ? Quaternion.Euler(0,90,0) : identity
         Instantiate(prefab, center, rot, obstaclesRoot)
    4. Collider 없음 (flow field 가 차단 담당)
```

- 풀링 없음 (Phase 10 맵 static per-play, 판 시작 시 1회 생성)
- Batching 없음 (50 draw call 수준 무시)

### 2.7 GeneratedMap 주입 경로 (Codex C-3 확대)

**현재 문제**: BattleBridge, MapView, PlacementInput 각자 `[SerializeField] MapData map` + `float tileSize` 독립 보관.

**Phase 10 해결**:
```
BattleBridge (runtime map owner):
    - ProceduralMapGenerator.Generate(theme, seed) → GeneratedMap
    - MapView.Initialize(generatedMap, theme, tileSize)      // 시각 자산
    - PlacementInput.Initialize(generatedMap, tileSize)       // 배치 판정 소스
    - FlowFieldSingleton allocate + BFS build                 // ECS 주입
    - spawn 타일 world pos 로 적 인스턴스화
```

- GeneratedMap 은 runtime-only struct, 디스크 저장 없음
- MapData SO 는 Phase 10 시점 deprecated (또는 단위 테스트 fixture 용도로만 남김)

### 2.8 Phase 10 v1 scope 제약 (명시적)

- 테마 1개 (`forest` 가칭)
- Obstacle 4~6종 (1×1 ×3, 2×1 ×2, 2×2 ×1)
- Variant 1~2 per footprint
- Spawn 1~3, Goal 1
- Map 크기 20×10 고정 (Phase 11+ 에서 가변)
- 풀링/Addressables 없음
- Runtime theme switch 없음 (GameManager 에 단일 `MapThemeData` ref)

---

## 3. Codex 리뷰 — Phase 10 해결 필요 공백

(축 A 는 Phase 9 에서 해결. 이하 축 B 관련)

### CRITICAL

#### C-3 (확대) — GeneratedMap → ECS/MapView/PlacementInput 주입

**현재 의존**: `BattleBridge.cs:27-30` `[SerializeField] MapData map` + `float tileSize`, `MapView.cs:9-10` 자체 `map`/`tileSize`, `PlacementInput.cs:19-22` 자체 `tileSize`.

**Phase 10 해결 경로**: §2.7 참조. BattleBridge 가 단일 owner, Init API 로 전달.

#### C-4 (확대) — SpawnEntry.pathId → spawnTileIndex migration

**현재 구조**:
- `AttackDeck.cs:17-22`: `public string pathId;` "A" / "B"
- `BattleBridge.cs:1149-1158`: `map.Paths` 에서 pathId 매칭

**Phase 10 해결**:
1. `SpawnEntry.pathId` → `SpawnEntry.spawnTileIndex` (int) 또는 `spawnGroupId` (string new)
2. 기존 deck asset 전수 수정 (현재 `WaveA.asset` 만 확인)
3. Migration script: `[ContextMenu("Migrate SpawnEntry")]` 또는 `IPreprocessBuild` hook

### HIGH

#### H-3 — TileType enum 숫자 충돌 migration

§2.1 참조. `MapTileType` 로 이름 변경 + asset migration script 동반.

#### H-4 — Path carve 알고리즘 불변조건

Codex 지적: "Path-first 는 연결성 구조 보장" 주장은 **알고리즘 불변조건 없이는 거짓**.

**Phase 10 에서 결정/증명해야 할 불변조건**:
- 모든 path cell 이 그리드 경계 안
- path 가 4-neighbor 연결된 연속 셀
- ④⑤ 이후 어떤 단계도 Walkable 셀을 덮어쓰지 않음
- BFS 검증: 모든 spawn → goal 도달성 생성 후 명시적 재확인

**알고리즘 후보 (Phase 10 브레인스토밍 Q6)**:
- A) Deterministic randomized Manhattan walk with bounded detours (+ BFS post-validation) ← 추천 v1
- B) A* from spawn to goal with random edge weights
- C) Anchor-based bend (N anchor 점 지정 후 anchor 간 A*)
- D) Cellular automata / BSP (복잡도 과잉, Phase 11+)

### MEDIUM

#### M-5 — Retry seed 유도식

```
attemptSeed_i = hash(baseSeed, attemptIndex=i, generatorVersion)
generatorVersion = 알고리즘 / 상수 변경 시 수동 증가 int
```

**로그 필수**: `baseSeed`, `attemptIndex`, `finalSeed`, `generatorVersion`.

#### M-6 — RNG 출처

- **금지**: `UnityEngine.Random` (global mutable state)
- 관리형 경로 (ProceduralMapGenerator 본체): `System.Random(seed)`
- Burst/Job 경로 (있을 경우): `Unity.Mathematics.Random(seed)`
- 현재 코드 중 `BattleBridge.cs:997` 에 `UnityEngine.Random.Range` 잔재 — Phase 10 generation 에서 사용 금지 (별개로 현 위치는 Phase 9 에서 터치하지 않음)

#### M-7 — Greedy obstacle placement 종료 조건

- `maxAttempts = width * height * K` (v1: K=3)
- 밀도 미달 시 옵션:
  - (a) 현재 상태 수용 + 실제 밀도 로그 경고 ← **추천 v1**
  - (b) 전체 맵 재시도 (retry seed 공식)
- 선택 시 명시 로그: `achievedDensity`, `targetDensity`, `attemptsUsed`.

#### M-8 — Placeable 쿼터 부족 시 buffer 확장 규칙

결정적 우선순위:
1. 1차: path 의 4-neighbor cell (좌표 정렬 순)
2. 2차 (1차 부족): path 의 8-neighbor cell
3. 3차 (2차 부족): path 거리 2 cell (Chebyshev)
4. 여전히 부족: generation 실패 → retry

각 단계 내 순서는 **좌표 정렬 또는 seeded shuffle** (결정적).

#### M-9 — Path carve 알고리즘 v1 선택

Phase 10 브레인스토밍 Q6 에서 결정. 기본 추천: **deterministic randomized Manhattan walk with bounded detours, BFS post-validation**. 실패 시 A* fallback.

### LOW

#### L-1 — TileType 이름 변경 영향 전수 조사

파급 사이트:
- `Assets/_Project/Scripts/Data/MapData.cs:6-11` (enum 정의)
- `Assets/_Project/Scripts/Core/MapView.cs:47-49` (material dict 키)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs:1007-1010` (placement 판정)
- 그 외 Grep `TileType` 로 전수

#### L-2 — MapThemeData.gridSize SO 필드

Phase 10 v1 은 20×10 고정. `gridSize` SO 필드는 존재하되 MapView/generator 가 읽고 (20,10) 이외 값이면 `Debug.LogWarning` 후 기본값 사용. Phase 11+ 에서 가변 지원.

#### L-3 — MapView obstacle root teardown

```
MapView.Initialize:
    if (obstaclesRoot != null) Destroy(obstaclesRoot)
    obstaclesRoot = new GameObject("Obstacles")
    obstaclesRoot.transform.SetParent(transform, false)

MapView.OnDestroy:
    if (obstaclesRoot != null) Destroy(obstaclesRoot)
```

Phase 9 에서는 obstacle prefab 없음 → Phase 10 에서 처음 필요.

---

## 4. 작업 분해 초안 (P10-XX)

- [ ] **P10-01** — `MapTileType` enum 신설 (Empty/Walkable/Placeable/Blocked). PrototypeMap.asset migration script (`[ContextMenu]`)
- [ ] **P10-02** — `MapThemeData` SO + `ObstacleView` MonoBehaviour 정의
- [ ] **P10-03** — Theme authoring 규약 검증 Editor script (파일명 regex ↔ ObstacleView.footprint)
- [ ] **P10-04** — `GeneratedMap` struct + BattleBridge 가 단일 owner. MapView/PlacementInput Init API 변경
- [ ] **P10-05** — `ProceduralMapGenerator.Generate(theme, seed)`: path carve 알고리즘 v1 (Q6 결정 후)
- [ ] **P10-06** — Path carve EditMode 테스트 (직선/장애물/연결성/길이 범위/불변조건 검증)
- [ ] **P10-07** — Placeable 쿼터 확보 + 확장 규칙 (M-8)
- [ ] **P10-08** — Obstacle multi-cell greedy placement + footprint-aware (M-7)
- [ ] **P10-09** — Obstacle 생성 EditMode 테스트 (밀도 수렴, footprint, 겹침)
- [ ] **P10-10** — MapView 재작성: GeneratedMap 수신 + 4 TileType 별 Material + obstacle prefab Instantiate (L-3)
- [ ] **P10-11** — PlacementInput: GeneratedMap 수신 + tileSize 단일 소스
- [ ] **P10-12** — AttackDeck.SpawnEntry.pathId → spawnTileIndex migration + 기존 deck asset 수정 (C-4)
- [ ] **P10-13** — Flow field rebuild on generated map (Phase 9 BFS 재사용, FlowFieldSingleton 를 generated map 에 대해 재계산)
- [ ] **P10-14** — Seed 로그 (baseSeed / attemptIndex / finalSeed / generatorVersion)
- [ ] **P10-15** — Forest theme 자산 (4~6 obstacle prefab + MapThemeData.asset)
- [ ] **P10-16** — PlayMode 회귀: 매 판 다른 맵 5회 + 각 판 flow field 자율 복귀 + defender 배치 가능성 + 시각 확인

---

## 5. 브레인스토밍 Q6+ (Phase 10 착수 시 재개)

Phase 9 브레인스토밍 종료 시점의 미결 Q 목록:

- **Q6** — Path carve 알고리즘 v1 선택 (§3 H-4 후보 중)
- **Q7** — Seed 관리 전략 (per-play vs per-run vs per-attempt)
- **Q8** — Generation 실패 fallback 정책 (max 5회 재시도 후 PrototypeMap fallback? theme 기본 패턴? 조건 완화?)
- **Q9** — Theme 추가 시점 (Phase 10 v1 = forest 1개, Phase 11+ desert/dungeon)
- **Q10** — PrototypeMap.asset 을 Phase 10 시점 삭제할지, deprecated 로 남길지 (테스트 fixture / fallback 용도)

---

## 6. 선행 조건

- Phase 9 완료 (flow field on fixed PrototypeMap 검증)
- `P7-15 / P8-10 / P9-11~P9-12` PlayMode 회귀 통과
- Unity Editor + Entities 패키지 버전 확정 (Phase 9 설계 §5)

---

## 7. Codex 리뷰 근거 링크

- Phase 9 설계 문서: `docs/plans/2026-04-19-phase9-flow-field-design.md`
- 원 Phase 9 prep (scope 축소 전): 현재 파일과 같이 갱신됨 — `docs/phase9-prep.md`
- 현재 waypoint 기반 구현 (Phase 9 제거 대상): `Assets/_Project/Scripts/Battle/Movement/MovementSystem.cs`, `BattleBridge.cs:581~602 (ResolveExitWaypointIndex)`, `BattleBridge.cs:1146~1204 (적 스폰 경로 lookup)`

---

**작성**: 2026-04-19  
**근거**: Phase 9 브레인스토밍 Q1~Q5 + Codex 2차 리뷰 (C-3/C-4 확대, H-3/H-4, M-5~M-9, L-1~L-3)  
**상태**: Phase 9 완료 후 Phase 10 브레인스토밍에서 본 문서 기반으로 Q6+ 해결 → `docs/PHASE10.md` 작성 흐름.
