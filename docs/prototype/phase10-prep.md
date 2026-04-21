# Phase 10 이관 스펙 — 맵 시스템 재설계 (seed 기반 procedural + 타일 4종)

> Phase 9 브레인스토밍 (2026-04-19) 에서 축 B 로 이관된 스펙 + 사용자 요구사항 재정의 (2026-04-21) + Codex 축소 리뷰 (2026-04-21) 반영. **Phase 10 = 맵 시스템 재설계 전체**. Phase 9 flow field 엔진 위에 얹는 procedural 생성 레이어.

---

## 1. 사용자 원 요구사항 (2026-04-21)

1. 매 판 맵은 **seed 기반 랜덤 procedural 생성**
2. 맵 크기는 **X × Y 유동**, 기본 **20 × 20**
3. **Multi-line + single-goal** 디폴트
4. 그리드 위에 **사용자가 이동타일 수동 지정** (추후 맵툴) **OR 랜덤 생성**
5. 이동타일 기반으로 맵 테마 **디자인 오브젝트 배치**
6. 타일 4종 구분: **이동 / 방어배치 / 환경 / 배경 오브젝트**

---

## 2. Phase 10 sub-phase 분할 (Codex 권고)

Phase 9 Task 8 3분할 성공 경험을 Phase 단위로 확장. 두 sub-phase 로 분리.

### Phase 10A — Data 모델 교체 + Infra

**검증 질문**: 기존 고정 맵(PrototypeMap) 위에서 새 4-타입 enum + GeneratedMap 주입 경로 + 20×20 가변 그리드 + multi-spawn 가 Phase 9 flow field 엔진과 정상 연동되는가?

**Bullets**: B2 (X×Y 유동) / B3 (multi-line single-goal) / B6 (타일 4종) + 공통 인프라

### Phase 10B — Procedural 생성 + 테마 오브젝트 배치

**검증 질문**: seed 기반 랜덤 / 수동 입력 맵이 실제 플레이어에게 유의미한 변이를 주며, 테마 오브젝트가 이동·배치 타일을 침범하지 않고 배치되는가?

**Bullets**: B1 (seed) / B4 (수동/랜덤) / B5 (테마 오브젝트)

---

## 3. 타일 4종 매핑 (사용자 bullet 6)

```csharp
public enum MapTileType : byte
{
    Walk  = 0,   // 이동 (적 이동 가능, flow field Walkable)
    Place = 1,   // 방어배치 (defender 배치 가능)
    Env   = 2,   // 환경 (Phase 10 = 시각 구분만, Phase 11 에서 효과 동작)
    Deco  = 3,   // 배경 오브젝트 (시각 장식)
}
```

- **mutually exclusive** (한 타일 = 한 역할)
- Walk 만 flow field 의 walkable mask 에 포함
- Place 만 PlacementInput 이 defender 배치 허용
- Env / Deco 는 Phase 10 에서 **시각 구분만** — 효과 동작은 Phase 11 이관 (화산/바람 등)
- 기존 `TileType { Buildable=0 / Path=1 / Obstacle=2 }` 와 **숫자 충돌** → `MapTileType` 로 이름 변경하여 enum 충돌 회피

---

## 4. 체크리스트 (Codex 축소 + 재추가 반영, 약 20 항목)

### Phase 10A — Data 모델 + Infra

- [x] **P10A-01** — `MapTileType` enum 신설 (Walk/Place/Env/Deco). 기존 `TileType` 과 분리
- [x] **P10A-02** — `MapGenerationSettings` SO 신설 (gridWidth, gridHeight, defaultSeed — 기본 20×20)
- [x] **P10A-03** — `GeneratedMap` runtime struct: `NativeArray<MapTileType> tiles, int2[] spawns, int2 goal, int2 gridSize, int seed, int generatorVersion`
- [x] **P10A-04** — `BattleBridge` 가 GeneratedMap 단일 owner. `MapView.Initialize(GeneratedMap)`, `PlacementInput.Initialize(GeneratedMap)` 주입
- [x] **P10A-05** — `FlowFieldBuilder` walkMask = `MapTileType == Walk` 로 변경 (Phase 9 Path-only 규칙 이어감). 20×20 크기 회귀 검증
- [x] **P10A-06** — `PlacementInput` 배치 판정을 `MapTileType == Place` 로 교체
- [x] **P10A-07** — `MapView` 가 4 타일 타입별 cube Material 4종 (임시 색상). 시각 구분만
- [x] **P10A-08** — `MapData` legacy asset: fixture 용으로 유지 + `MapTileType` 매핑 규칙 문서화 (코드 주석 1단락)
- [x] **P10A-09** — Tests: 20×20 gridSize / multi-spawn 전부 goal 연결성 BFS / 4-enum migration EditMode

### Phase 10B — Procedural 생성 + 테마

- [x] **P10B-01** — `ProceduralMapGenerator.Generate(seed, gridSize, theme) → GeneratedMap`: branch/trunk/root path carve v1
- [x] **P10B-02** — 수동 입력 경로: `GeneratedMap` 을 직접 받는 API (맵툴 예약용 data shape 고정)
- [x] **P10B-03** — Fallback 맵: 생성 실패 시 하드코딩 **직선 multi-spawn-goal** 맵 사용. freeze 방지
- [x] **P10B-04** — `MapThemeData` SO (최소 필드): `obstaclePrefabs[]`, `minPlaceableRatio` (2필드)
- [x] **P10B-05** — `ObstaclePlacer`: Walk + Place 비침범 셀에 **단일 셀** obstacle prefab 배치 (multi-cell 은 Phase 11 이관)
- [x] **P10B-06** — Forest 테마 v1: obstacle prefab 3~4종 (1×1), `forest.asset` MapThemeData
- [x] **P10B-07** — `AttackDeck.SpawnEntry.pathId` → `spawnIndex` (int) migration + `WaveA.asset` 수정
- [x] **P10B-08** — Seed 로그: BattleLogger 에 `seed` + `generatorVersion` 필드
- [x] **P10B-09** — Tests: seed 결정성 (same seed → same map) / Path carve 연결성 / Play smoke 다른 spawn lane 비교

### 문서

- [x] **P10-DOC-01** — `docs/spec/map-system/` 완료 처리 + `20_claude_handoff_summary.md` 작성
- [x] **P10-DOC-02** — CLAUDE.md Phase 갱신

---

## 5. 브레인스토밍 Q (Phase 10 착수 전 결정)

Phase 10A 착수 전 결정:

- **Q-B**: ECS 맥락 — `GeneratedMap` 소유 맥락. `FlowFieldSingleton` (Effects) 와 병합 vs 별도 `MapDataSingleton` (Units) 신설
- **Q-I**: RNG 출처 — `System.Random(seed)` (managed) vs `Unity.Mathematics.Random` (Burst-safe, seed-based Xorshift). 결정성 신뢰도 비교

Phase 10B 착수 전 결정:

- **Q-6 (원 Q6)**: Path carve 알고리즘 v1 — multi-spawn × single-goal 에서 각 spawn 별 독립 carve vs 공유 trunk vs A* 가중치
- **Q-C**: AttackDeck multi-spawn 할당 — deck 내 `spawnIndex` 명시 vs round-robin vs random-per-spawn
- **Q-F**: Multi-spawn 적 분배 전략 — 동시 스폰 vs rotating vs timed 분산
- **Q-K (축소)**: 수동 입력 data shape 만 — `int2[] walkTiles, int2[] spawns, int2 goal` struct. 외부 I/O (JSON/etc) 는 맵툴 Phase

---

## 6. Phase 11+ 이관 확정 (Phase 10 범위 밖)

- **환경효과 실제 동작** (화산/바람 타일 effect) — Phase 10 은 `Env` 타일 타입 정의만
- **Multi-cell obstacle 시스템** (footprint / canRotate / weight / ObstacleView)
- **테마 시스템 확장** (파일명 regex validator, 다중 테마 pool, runtime theme switch, Addressables)
- **Defender 경제 리밸런스** (20×20 기반 cost/skill 재튜닝)
- **맵툴 실제 구현** (scene 에디터 확장, JSON 직렬화, UI)
- **Multi-goal 지원** (goalId + flow field N벌)

---

## 7. Codex 축소 권고 (2026-04-21) — 이 문서 반영 내역

**CRITICAL 과잉 삭제** (8건):
- ObstacleView / PlacedObstacle / footprint / canRotate / weight (multi-cell 시스템) → Phase 11
- Obstacle prefab 4~6종 고정 수량 → v1 3~4종으로 축소
- Editor 파일명 regex 검증 → Phase 11 (테마 시스템 확장)
- MapThemeData 의 densityPct/gridSize/spawnRule/goalRule/pathRule → 2필드로 축소
- BattleMapSpec adapter (미래 훅) → 삭제. GeneratedMap 직접 주입
- phase11-prep.md 선작성 → 본 문서에 §6 으로 통합
- 20×10 고정 → 20×20 기본 + X×Y 유동

**HIGH 과잉 축소** (4건):
- Blocked multi-cell greedy → 단일 셀 배치
- Placeable 쿼터 + buffer 확장 정책 → 최소 밀도 1필드
- Generation 실패 fallback 5회 재시도 + fallback 맵 → 3회 재시도 + 하드코딩 직선 맵
- generatorVersion 로그 → 유지 (재추가, seed 재현성 필수)

**재추가** (4건 — Codex 가 과도 삭제):
- `generatorVersion` 로그 — 알고리즘 변경 시 동일 seed 가 다른 맵 생성. 버그 재현 필수
- Fallback 하드코딩 맵 — 재시도 전부 실패 시 UX freeze 방지
- Path carve 알고리즘 방향 — multi-line 복잡도로 브레인스토밍 Q 로 결정
- `MapData` legacy 처리 규칙 — 1줄 주석 명시 (P10A-08)

---

## 8. 선행 조건

- Phase 9 완료 ✅ (flow field 엔진 검증 완료, 2026-04-21)
- P7-15 / P8-10 사용자 PlayMode 회귀 — Phase 10 착수 전 무관, 종료 시 함께 검증 가능
- Unity Editor + Entities 패키지 버전: **Phase 9→10 사이 재논의 진행 중** — 2026-04-21 "Unity Editor 일단 패스" 결정. Phase 10 은 **현재 환경 (Unity 6000.3.5f2 + Entities 1.4.5)** 에서 진행. 설계는 1.4 / 6.x 공통 API 만 사용

---

## 9. 참조 문서

- Phase 9 설계: `docs/plans/2026-04-19-phase9-flow-field-design.md`
- Phase 9 종료 스펙: `docs/PHASE9.md`
- 잔여 이슈: `docs/residual-issues.md`
- ECS / TRD 제약: `docs/TRD.md`

---

**작성**: 2026-04-19 (초안) / 2026-04-21 (rev2 — 사용자 요구 재정의 + Codex 축소 반영)  
**근거**: Phase 9 브레인스토밍 이관 + 사용자 6 bullet + Codex 과잉 검토  
**상태**: Phase 10 map-system 구현 완료. 세부 종료 상태는 `docs/spec/map-system/20_claude_handoff_summary.md` 를 기준으로 인계.
