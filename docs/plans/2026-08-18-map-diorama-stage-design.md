# 맵 구조 개편 — 디오라마 스테이지 (탐색 설계)

> 2026-08-18 브레인스토밍 결과물. 구현 상세는 추후 `docs/spec/map-diorama-stage/` 로 분산한다.
> **별도 브랜치 작업 · 기존 파이프라인 전면 교체 방향.**

## 목표

맵 저작을 "에디터 창에서 셀 칠하기(MapPainter → MapDocument)"에서 **"씬에서 Ground + 프랍을 자유 배치하는 디오라마 방식"** 으로 전환한다. 비주얼은 Ground 메쉬/터레인 + 프랍 그 자체가 되고(자동 생성 타일맵 페인팅 은퇴), 논리는 지금처럼 **격자 위에서** 돈다 — 프랍 위치를 셀로 양자화해 논리 격자를 자동 파생한다. 레퍼런스: 로스트캐슬2, 컬트오브램.

## 확정된 방향 결정 (2026-08-18 사용자 결정)

| # | 결정 | 내용 |
|---|---|---|
| D1 | 저작 매체 | 맵 = 프리팹 1개 (Ground + 역할 스크립트 붙은 프랍). 프랍 위치에서 논리 자동 파생 |
| D2 | 이동 모델 | **열린 마당** — walkable = 막히지 않은 모든 셀. 복도 저작 폐기 |
| D3 | 유닛 블로킹 | **없음** — B-1 계약 유지(유닛·적 상호 통과). 막는 것은 지형 프랍만 |
| D4 | 높이 | **논리 불반영** — 평면(Y=0) 전제 유지. `BoardSpace` Y-폐기 계약 무변경 |
| D5 | 이전 전략 | 별도 브랜치에서 **전면 교체** (MapDocument/MapPainter 은퇴). 파일럿 맵으로 검증 후 확산 |
| D6 | footprint | **명시 선언이 정본** + 바운즈에서 초안 제안 버튼. 사각형만(shape mask 는 후속). 역할 프랍은 셀 스냅, 순수 장식은 자유 배치 |

## 아키텍처 — 접근 A: 프리팹 정본 + 로드타임 파생, `GeneratedMap` 계약 유지

```
MapStage 프리팹 (Ground 메쉬 + 프랍들 + 저작 컴포넌트)     ← 정본 (아티스트가 만짐)
        │ 전투 빌드 시 인스턴스화 = 그게 곧 비주얼
        ▼
DioramaMapBuilder (프랍 스캔 → 셀 양자화 → 마스크 조립)     ← 순수 코어는 static 함수
        ▼
GeneratedMap (구조체 무변경)                                ← 심 계약 유지
        ├─→ 심: FlowField / NavGrid / 배치 술어 / traversal-layers   전부 무변경
        └─→ 뷰: OverlayView (기존 오버레이 타일맵 채널만 존치)
```

**핵심 트릭 — `tiles` 합성**: 열린 셀 → `Walk`, 막힌 셀 → `Deco` 로 합성해서 `GeneratedMap` 에 넣는다. 그러면 `walkMask = tiles==Walk`, `cellLayers = Derive(tiles)`, 픽업 후보(Walk∪Place=열린 셀) 등 **기존 파생식이 한 줄도 안 바뀌고** 열린 마당이 성립한다. `MapTileType` 은퇴(풀 footprint 모델, 접근 C)는 검증 후 후속 spec.

## 저작 컴포넌트 (Authoring 레이어 — MonoBehaviour, 런타임 상태 없음)

| 컴포넌트 | 역할 | 파생 결과 |
|---|---|---|
| `MapStage` (루트) | 격자 원점 + playArea rect(셀 단위) 선언. Ground 사이즈에서 자동 제안 버튼, 수동 트림 가능 | `gridSize`. **격자 = playArea 만** — Ground 가 그보다 크면 나머지는 셀 없는 순수 배경(현 서라운드 링의 후계) |
| `PropFootprint` | 점유 footprint(w×h) + 앵커 오프셋 선언. 기즈모로 차단 셀 실시간 표시 | 해당 셀 차단 → `tiles=Deco`, placeMask=0 |
| `SpawnMarker` | **명시적 `laneIndex`** (씬 계층 순서 금지 — 결정론) | `spawns[]` (laneCount = 웨이브 결정론 키) |
| `GoalMarker` | 골 위치 (안정도 필드 없음 — HP 는 `AttackDeck.goalStabilityMax` 단독 소유, critic 정정) | `goals[]`. 열린 셀 위 필수(검증) |
| `RouteMarker` | 웨이포인트 체인(경로 index + 순번). 선택적 | `waypointCells/Ranges`, `spawnRoutes` |
| `PlacementBlockZone` | 배치 금지 영역(rect). 선택적 | 해당 셀 placeMask 차감 |

- 컴포넌트 없는 프랍 = 순수 장식. 논리에 안 들어감, 자유 배치.
- `GoalMarker`/`SpawnMarker` 는 **뷰 앵커 + 골 피해 연출 훅도 소유**한다 — 현재 `TilemapMapView` 가 가진 `TryGetGoal/SpawnVisualAnchor`(튜토리얼 포커스), `SetGoalCrack`(균열 단계), `MarkGoalCollapsed`(붕괴 틴트)의 후계. `BattleBridge` 호출 경로(:6058, :6171)를 마커의 뷰로 재배선한다. 골 HP 는 심 소유, 마커는 연출만.
- placeMask 기본 규칙: **열린 셀 전부 `Ground\|Path\|Air`**, 막힌 셀 0, BlockZone 차감. 스폰/골 셀은 기존 `CloseCellLayers` 가 런타임에 닫음(무변경).
- 포탈 프랍(사용자 예시): `GeneratedMap` 에 필드가 없어 신규 배선(빌더 수집 → 전투 시작 시 `PortalLink` 엔티티 생성) 필요 — **별도 작업 단위**로 분리, v1 필수 아님.
- 공성 구조물(`StructureMarker`): SiegeTest 전용 기능 — 필요 시 후속.

## 파생 파이프라인 (전투 빌드 시 1회)

`BattleBridge.BuildMapForBattle` 의 문서 경로를 교체한다:

1. `MapStagePool` (프리팹 + AttackDeck + WavePlanAsset 짝 — 기존 `MapDocumentPool` 구조 승계, 인덱스 선정/DevMapOverride 의미 유지)
2. 스테이지 프리팹 인스턴스화 (비주얼 완성)
3. `DioramaMapBuilder.Build(stage)` → 프랍 스캔 → 셀 양자화(floor 규칙) → `GeneratedMap` 조립
4. `MapConnectivity.AllSpawnsReachGoal` 재사용 — 실패 시 기존 fallback linear 안전망 유지
5. 이후 단계(필드 설치, BoardSpace.Configure, 카메라 프레이밍) 무변경

순수 코어(양자화·마스크 조립·마커 수집)는 plain 값 입출력 static 함수로 분리(제약 10) → EditMode 테스트 대상. Mono 스캔 레이어는 얇게.

## 뷰 계층

- **은퇴**: `TilemapMapView` 의 PaintGround/PaintSurroundRing, `BoardVisualPlan*`, `BackgroundPropPlacer`, `TilemapPropScatter`(유일한 타일맵 역참조), `TileSetData` 의 바닥 타일 절반.
- **존치 — `OverlayView` 로 분리**: 오버레이 7채널(호버/사거리/배치가능/아군장판/텔레그래프/효과타일/마커)은 타일맵 그대로 유지. `Grid` 는 좌표 권위(`BoardSpace.Configure`)+오버레이 캔버스로 존치. 논리가 평면이므로 코플레이너 전제 성립, 87개 `ToView` 호출처 무변경.
- Ground 메쉬는 논리 평면(Y=0) 근방에서 평평하게. 시각적 미세 굴곡은 오버레이 리프트로 흡수.
- 카메라: `TryGetPlayfieldWorldBounds` 가 격자(=playArea) 기준이므로 무변경 — Ground 배경이 아무리 커도 프레이밍 불변.
- `BoardSortOrder` 행 간격 10 버그(폭>10 맵에서 앞뒤 정렬 붕괴)는 새 맵이 넓어질 수 있어 **이번에 수정** (뷰 전용, 심 무관).

## 전투 로직 접점 감사 — 코드 무변경, 행동은 달라지는 곳 (2026-08-18 코드 확인)

`tiles` 합성 덕에 아래는 **전부 무수정으로 동작**하지만, 열린 마당에서 행동이 달라지거나 전제가 뒤집히므로 파일럿 검증 축에 넣는다.

| 접점 | 현재 전제 | 열린 마당에서 | 판정 |
|---|---|---|---|
| **«Place=벽» 전제 3곳** — `CollectDefenderSources`(중심 셀 제외, `FlowFieldBuilder:172`), `TryGetNearestWalkCell`(`BattleBridge:2599`), `PatrolAreaMath:206` | 방어유닛 발밑 = Place = 벽. traversal-layers §5 가 "방어유닛 이동 spec 몫"으로 미룸 | 발밑이 Walk 가 되어 전제가 **지금 뒤집힘**. 앵커 스냅은 자기 셀 반환(개선), 순찰 영역에 소환사 셀이 포함됨 | 코드 무변경 · **순찰 소환물 행동 육안 검증 필수** |
| **어그로 추격** (`AggroChaseMath` — 사거리 디스크 내 Walk 셀 BFS) | 복도만 추격 경로 | 마당 전체가 경로 — 추격 소스 셀 급증, 포위 형태로 접근 | 무변경 · 성능/그림 확인 |
| **cellLayers 파생** — Place 소멸로 `Ground` 통행 비트 공집합, 차단 셀(Deco)→`Air` | `Ground\|Path` 순찰병은 Place+Walk 를 돎 | 순찰병은 Path 만으로 전 마당. **공중(Air) 적은 차단 프랍 위를 넘는다** (바위 위 비행 — 의도로 수용) | 무변경 · "공중도 못 넘는 프랍"(절벽 등)은 표현 불가 → 후속(접근 C 에서 footprint 별 차단 층) |
| **웨이브 컨셉 게이팅** — ~~`MapConceptRules` 가 tiles 직독~~ (critic 정정 2026-08-18: `MapConceptRules` 소비처는 페인터 경고·테스트뿐, `WavePatternGenerator` 는 tiles 를 안 읽음) | 컨셉 게이트는 **laneCount 축**이 전부 | 스폰 수가 같으면 웨이브 불변 — 열린 마당 자체는 게이트에 무영향 | 밸런스 트랙은 «내용 변화»가 아니라 «난이도 체감 변화»(경로 단축·분산) 대응 |
| **효과 타일 분포** — `EffectTilePlacer` 가 `PlaceableAt(Ground)` 로 후보 선정 (critic Minor 2) | Place 셀(배치지) 위주 | 전 마당이 후보 — 경로 위까지 퍼짐 (placement-mask 계약 6 과는 정합) | 무변경 · 파일럿 육안으로 분포 확인 |
| **사직서 메테오 barrage** — Walk 셀 전수 수집 후 타격 (`BattleBridge:4531`) | 복도 셀만 후보 | 마당 전체가 후보 — 타격 분산 급증 | 무변경 · 밸런스 트랙에서 재조정 |
| **공격/투사체 LOS 없음** — Battle 에 지형 차폐 개념 자체가 없음 (이동 LOS 만 존재, `PathSmoothing`) | Deco 벽 너머 사격이 평면 타일이라 안 보였음 | 3D 프랍(바위/집)을 **관통 사격이 눈에 띔** | v1 수용(캐주얼) · 후속 후보 |
| **연결성/구조물 검증** — `MapConnectivity` 가 스폰·골 모두 Walk 요구, `StructurePlacement:155` 동일 | 저작 규칙으로 보장 | 마커 린트가 보장해야 — "스폰/골이 차단 셀 위" 검출 | 마커 린트 항목에 포함 (아래 검증 갱신) |

**무영향 확인**: 공격 사거리·타겟팅(`NearestTargeting`, `DefenderDensity`)은 연속 sim 좌표 기반 — 셀 종류 무관. 배치 점유(1셀 1유닛)·해저드 장판·포탈 텔레포트·CC/DoT 전부 셀 좌표만 쓰고 tiles 종류를 읽지 않는다.

## 은퇴 목록 (브랜치에서)

`MapPainterWindow` · `MapDocument`(+Builder/Adapter/Pool) · `ObstaclePlacer` 커빙 · 바닥 페인팅 계열(위 뷰 섹션). 문서(`docs/reference/map-wave-balancing.md` 의 지형 규칙 등)는 교체 시점에 갱신.

## 밸런스 영향 (별도 트랙)

열린 마당은 스폰→골 거리·경로 다양성·배치 지형을 근본적으로 바꾼다. 웨이브/덱 밸런스는 새 맵과 짝으로 재저작한다(풀 엔트리 구조가 이미 맵·덱·플랜을 짝으로 묶음). **이 개편 spec 의 완료 기준에 밸런스 품질은 넣지 않는다** — 파일럿 맵의 기능 검증까지만.

## 검증

- EditMode: 양자화(footprint→셀)·마스크 조립·마커 수집 순수 함수 테스트 + `MapConnectivity` 재사용.
- 에디터: `MapStage` 인스펙터 검증 버튼 — 연결성 + 마커 린트(스폰 0개, laneIndex 중복, **스폰/골이 차단 셀 위**, playArea 밖 마커 등).
- PlayMode: 픽스처 스테이지 프리팹 1개 — 적이 열린 마당을 가로질러 골 도달 + 열린 셀 배치 성공 스모크.
- 파일럿 육안 검증 축(전투 접점 감사에서 도출): ① 순찰 소환물이 소환사 셀 침범/이탈 없이 도는가 ② 공중 적이 차단 프랍을 넘는 그림이 수용 가능한가 ③ 어그로 추격이 포위 형태로 자연스러운가 ④ 골 균열/붕괴 연출이 마커 뷰에서 재현되는가.

## 작업 단위 초안 (spec 분산 시)

| # | 단위 | 크기 |
|---|---|---|
| 0 | 저작 컴포넌트 + 기즈모 (`MapStage`/`PropFootprint`/마커들) | M |
| 1 | `DioramaMapBuilder` 순수 코어 + EditMode 테스트 | M |
| 2 | `BattleBridge` 빌드 경로 교체 + `MapStagePool` | M |
| 3 | `OverlayView` 분리 (바닥 페인팅 은퇴) + `BoardSortOrder` 수정 | M |
| 4 | 골/스폰 마커 뷰 재귀속 (앵커·균열·붕괴 — `TilemapMapView` 구조물 경로 후계) | S~M |
| 5 | 파일럿 맵 1개 제작 + PlayMode 스모크 + 육안 검증 축 4종 | M |
| 6 | 포탈 프랍 배선 (선택) | S~M |

## 후속 후보 (범위 밖)

- 접근 C: `MapTileType` 은퇴 — 마스크 묶음 정본화 (tiles 합성 제거)
- shape mask footprint (L자 대형 구조물)
- 높이 tier (이번에 명시적으로 배제)
- 유닛 블로킹/미로 쌓기 (명시적으로 배제)
- 웨이브 생성기 열린 마당 재밸런스 + `enemy-wave-integration` 스킬 갱신 (`MapConceptRules` 의 tiles 직독 게이트 포함)
- 공격/투사체 지형 LOS (3D 프랍 관통 사격 — v1 수용, 눈에 거슬리면 착수)
- footprint 별 차단 층 선언 ("공중도 못 넘는 프랍" — 절벽/성벽. 접근 C 와 함께)
