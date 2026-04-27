# 29. Final Handoff — board-visualization spec 종료

**작성일**: 2026-04-27
**상태**: **wrap (중단 종료)**. baseline 코드는 유지, 추가 시각 튜닝은 본 spec 에서 진행하지 않는다.

## 종료 사유

- rev2/rev3 가 잡았던 "Enter the Gungeon 수준 연속 바닥" 은 grid cell-by-cell 맵 생성 구조와 충돌해 27 / 28 까지 끌어도 해결 못 함이 확증.
- rev4 에서 **보드게임 타일** (Warhammer Underworlds / Gloomhaven 계열) 로 시각 참조를 낮추고 22 palette pass 까지 코드 반영했으나, Play 결과 틴트가 시각적으로 반영되지 않고 Place edge / inner corner / outer corner overlay 가 화면에 보이지 않음.
- 추가 트레이스 결과 root cause 가 board-visualization spec 의 zone 규칙이 아니라 **렌더링 파이프라인** (URP transparent surface mode 누락, DOTS_INSTANCING 경로, overlay sortingOrder 누락) 쪽으로 좁혀짐 → 본 spec scope 안에서 더 다듬는 것은 ROI 낮음.

## Commit (rev4 final)

| 커밋 | 제목 |
|---|---|
| `4d0fca3` | docs(board-visualization): record 22 commit hash in spec |
| `6c88007` | tune(board-visualization): unify theme palette for board tile concept |
| `1e0c2fd` | refactor(board-visualization): render place as region meshes (28) |
| `09ce521` | tune(board-visualization): reduce place slab seams (27) |
| `39ce856` | record sort order unification verification (26) |
| `4462819` | unify board character sort order (26) |
| `4f8684f` | migrate unit quad views to mono (24) |
| `6dfa019` | fix board prop scale sorting (17b) |
| `a1c7c98` | feat(board-visualization): finish poisson and place edge pass (17~19) |

## Implemented (유지되는 baseline)

- `BoardVisualPlan` / `BoardVisualPlanBuilder` (cells, regions, anchors, decor anchors, masks, shapeClass, proximity).
- `MapView` 가 plan 기반으로 Walk yaw, Place edge mask, Env region surface, decor anchor 를 소비.
- `BackgroundPropPlacer` 가 plan + theme + seed 로 Poisson + cluster + jitter 분포 생성 (V-001 부분 달성).
- Enemy / fallback Defender Mono quad view 이관 + Spine Defender 와 함께 `BoardSortOrder.Compute` 공식 공통 사용. 캐릭터-프랍 sorting 회귀 0.
- `Place` 영역을 region 단위 row-run tiled mesh (`BuildPlaceRegionSurface`) 로 렌더해 cardinal 연결된 region 의 내부 seam 제거. hover/flash 는 셀 단위 투명 overlay 로 분리.
- `MapThemeData` 에 `placeBaseTint` / `walkBaseTint` / `envBaseTint` / `propGlobalTint` 필드 + `forest.asset` 보드게임 톤 값.

## Key Files

- `Assets/_Project/Scripts/Core/MapView.cs` — `BuildTiles`, `BuildPlaceRegionSurface`, `BuildPlaceEdgeOverlays`, `BuildPlaceHoverOverlays`, `BuildSharedMaterials`, `CreateTileTopMaterials`, `InstantiateBackgroundProps`, `ApplyPropGlobalTint`
- `Assets/_Project/Scripts/Data/BoardVisualPlanBuilder.cs` / `BoardVisualPlan.cs` / `BoardVisualCell.cs` / `BoardVisualRegion.cs`
- `Assets/_Project/Scripts/Data/MapThemeData.cs` — zone tint 필드
- `Assets/_Project/Scripts/Rendering/RuntimeMaterialFactory.cs` — `CreateOpaqueTexture` / `CreateTransparentTexture` / `ApplyColor`
- `Assets/_Project/Shaders/Tile_Unlit.shader` — `_BaseColor` MainColor + DOTS instancing 경로
- `Assets/_Project/Map/Theme/forest/forest.asset` — placeBaseTint (0.9, 0.85, 0.72, 1) 등
- `Assets/_Project/Scripts/Battle/Common/BoardSortOrder.cs`

## Verified

- compile error 0 (Unity console).
- `BoardVisualPlanBuilderTests`, `TerrainTileShapeUtilityTests`, `TerrainSurfaceSelectorTests` 통과.
- 26까지 캐릭터-프랍 sorting Play 회귀 확인 완료.
- 28 region mesh 전환 후 cardinal 연결 Place region 의 내부 seam 제거 육안 확인.
- **22 palette pass 의 Play 검증은 실패**. 사용자 보고 기준 틴트 / 엣지 / 코너 overlay 가 화면에 반영되지 않음. Unity MCP unavailable 로 자동 캡처 보강 불가.
- **2026-04-27 사후 코드 트레이스 결과 root cause 가 다른 곳에 있음을 확인** (Follow-up 섹션 갱신본 참조). 처음 추측한 "URP transparent surface mode 누락" / "DOTS_INSTANCING 우회" 는 코드 레벨로는 무관 (이미 정상). 실 의심 대상은 (A) `MapView._tileTextureMaterials` 캐시 키 설계 결함, (B) Place edge mask 가 `envNeighborMask` 한정이라 Place ↔ Walk 경계 fringe 누락, (C) `placeEdgeOpacity` / `placeOuterCornerOpacity` 가 0.25~0.42 로 시각적 인지 한계. red-tint 결정 실험으로 A 발화 여부 확정 필요.

## Notes (되돌리면 안 되는 의도)

- `Place` 는 셀 단위 quad 로 다시 돌아가지 않는다. `BuildPlaceRegionSurface` 의 region mesh 가 hover/flash overlay 와 분리된 구조를 유지.
- `BoardSortOrder.Compute(gridSize, x, y) + offset` 공식이 Enemy / Defender / fallback Defender / Prop 공통 sorting 표준. ECS RenderMesh 경로로 회귀 금지.
- prop root scale 은 `placement.scale` 만, visualScale 은 `PropBillboard.ApplyData` 에서만 적용 (V-009 회귀 금지).
- `MapView` 는 `BattleBridge` 외 ECS API 직접 호출 없음. plan 만 소비.
- forest.asset 의 zone tint 값은 컨셉 의도이므로 다음 spec 에서 렌더 파이프라인 수정 후 재평가하기 전까지 그대로 둔다.

## Follow-up (별도 spec 으로 분리)

본 spec 안에서는 더 진행하지 않는다. 시작하려면 새 feature-slug 폴더로 옮긴다.

1. **palette-and-overlay-fix** (가장 가능성 높은 후속, 진단 갱신본)
   - **사전 결정 실험 (필수)**: `forest.asset → placeBaseTint = (1, 0, 0, 1)` 빨강으로 두고 Play. Place 면이 빨개지면 tint path 정상 → Bug A 가시 영향 없음 → 시각 문제는 Bug B + Bug C. 빨개지지 않으면 Bug A 가 실제로 발화 중. 이 한 실험이 후속 작업 분기를 결정.
   - **Bug A — 타일 머티리얼 캐시 키 결함**: `MapView._tileTextureMaterials: Dictionary<Texture2D, Material>` (line 27). `CreateTileTopMaterials` 가 Place → Walk → Env 순서로 호출되며 `ContainsKey(textures[i])` 로 skip → 같은 텍스처가 여러 zone 에 있으면 첫 zone tint 만 반영. forest.asset 에서는 zone primary texture 가 분리돼 silent 하지만 다음 테마에서 터짐. 캐시 키를 `(BoardZoneType, Texture2D)` 로 교체 + line 527 / 670 의 lookup 도 zone 인자 받도록.
   - **Bug B — Place edge mask 가 Env 이웃 한정**: `MapView.cs` line 242 `int edgeMask = visualCell.envNeighborMask;`. Place ↔ Walk 경계 cells 의 edge fringe 가 절대 안 그려짐. `transitionMask` (4-bit, 모든 이웃 zone 차이) 로 교체하거나 mask 를 `envNeighborMask | walkNeighborMask` 로 합쳐야 결정. 디자인 결정 + 코드 1줄.
   - **Bug C — overlay alpha 가 너무 낮음**: `placeEdgeOpacity = 0.25` (27 단계에서 0.36 → 0.25 로 낮춤), `placeOuterCornerOpacity = 0.42`, `placeInnerCornerOpacity ≈ 동급`. 베이지/녹색 텍스처 위 알파 0.25 흰 fringe 는 거의 안 보임. **Inspector 튜닝**으로 0.5~0.7 범위 시도. 단 너무 올리면 다시 V-004 (edge fringe 너무 강조) 회귀.
   - **(원래 의심선이었으나 검증 결과 무관)**: `RuntimeMaterialFactory.CreateTransparentTexture` 의 URP transparent surface mode 는 line 72-83 에 이미 다 설정됨. `Tile_Unlit.shader` 의 `DOTS_INSTANCING_ON` 은 ECS RenderMesh entity 렌더 시에만 활성, Mono `MeshRenderer.sharedMaterial` 경로의 타일에서는 안 탐. 두 항목 다시 파지 말 것.
   - **(선택) overlay sortingOrder**: 본 코드 트레이스로는 occlusion 위험 없음 확인 (region mesh y=0.002 / hover y=0.006 / edge y=0.022 / 모두 transparent + ZWrite off). 그러나 future regression 보험으로 BoardSortOrder 부여 자체는 무해. 우선순위 낮음.
2. **17r prop-distribution-retry** (선택) — V-001 잔존. Poisson 정공법 재구현 원하면.
3. **23 volcano-theme-fill** (선택) — 두 번째 테마 자산 채움.
4. **leak: BattleBridge.StartBattle 반복 시 Persistent allocates 경고** — board-visualization 과 분리 추적. ECS 컨텍스트 정리 경로 점검.

## 다음 spec 시작 가이드

- 새 폴더 이름 후보: `docs/spec/palette-and-overlay-fix/`.
- 첫 작업 단위 (`0_red_tint_decision_test.md`): forest.asset 의 `placeBaseTint = (1,0,0,1)` 로 두고 Play. 결과로 후속 분기 확정.
  - Place 빨개짐 → Bug A 영향 없음, Bug B (edge mask) + Bug C (alpha) 만 다룸.
  - Place 빨개지지 않음 → Bug A 가 실제로 발화. `MapView._tileTextureMaterials` 캐시 키 (zone, texture) 로 교체.
- 후속 작업 단위 (Bug A/B/C 각각 최대 1 파일).
- 본 spec 의 28 region mesh 구조 / 22 tint 필드는 그대로 활용. 추가 컨셉 변경 없음.
- `RuntimeMaterialFactory.CreateTransparentTexture` 와 `Tile_Unlit.shader` 의 DOTS_INSTANCING 은 **건드리지 말 것** — 검증 결과 정상 동작.

## 주의

- `docs/spec/background-props/` 는 legacy. 종료된 본 spec 도 더 이상 source of truth 아님. 다음 spec 시작 시 본 handoff 만 참조.
- 워크트리에 본 작업과 무관한 dirty 파일이 섞여 있다 (`Assets/PixPlays/ElementalAOE/...WindAoeSmokeMat.mat`, `Assets/Screenshots/...` audit 산출물 등). 다음 작업자는 이 부분을 건드리지 말 것.
- 사용자가 "추가 진행 무의미" 로 본 spec 종료를 승인했다 (2026-04-27). 같은 컨셉으로 재시도 금지. 후속은 렌더 파이프라인 root cause 또는 컨셉 자체 재설계 둘 중 하나에서 출발.

---

## 트러블슈팅 이력 (다시 만들 때 반복 금지)

본 섹션은 **나중에 board-visualization 을 처음부터 다시 만들 경우 같은 막다른 길을 다시 파지 않기 위한 기록**이다. 시간순 + 시도/결과/교훈 형식.

### 시도한 접근들과 결과

| # | 시도 | 결과 | 한 줄 교훈 |
|---|---|---|---|
| 1 | "벽 없는 Enter the Gungeon 류 절차 생성 맵" 컨셉 위에 시각 레이어 튜닝 (rev2/rev3) | grid cell-by-cell 맵 생성 구조와 본질적으로 불일치. 어떤 시각 튜닝으로도 "연결된 룸" 처럼 안 읽힘 | **시각 컨셉을 잡기 전에 맵 생성 구조부터 확인**. 셀 단위 생성기 위에 룸 기반 시각 참조를 얹으면 영영 못 따라간다 |
| 2 | 16 visual audit — V-001~V-010 카탈로그 작성 | 카탈로그는 유효했음. 그러나 finding 별로 spec 을 쪼개니 (17/18/19) 같은 컨셉 미스를 finding 마다 따로 때리는 모양 | finding 이 패턴으로 묶이면 (예: "전체가 보드로 안 읽힘") 개별 spec 으로 쪼개지 말고 **컨셉을 먼저 점검** |
| 3 | 17 Poisson 정공법 — V-001 (프랍 분포) 해소 시도 | 부분 달성. clusterProbability/clusterCount 가 asset 에 있어도 후보 밀도와 family grouping 이 약해 anchor 단발 인상 잔존 | Poisson + cluster 는 **anchor 후보 밀도 + radius 가 충분한 영역을 전제**. 작은 region 에서는 Poisson 자체가 무력 |
| 4 | 17b prop visualScale 이중 적용 hotfix | 해소 (커밋 6dfa019) | root scale 과 visualRoot scale 분리는 명시적 계약으로 둬야 한다 |
| 5 | 17c 캐릭터 sort align — 처음 드래프트 | DEPRECATED. 전제 오류 — Enemy/fallback Defender 가 **ECS RenderMesh 경로** 라 `Renderer.sortingOrder` 체계 밖이라는 걸 놓쳤다. Codex 리뷰가 잡음 | sorting 통일 작업은 **모든 캐릭터/프랍의 렌더 경로를 먼저 확인**. ECS 와 Mono 가 섞여 있으면 한 쪽 통일부터 |
| 6 | 24 Enemy ECS→Mono quad view 이관 | 완료 (커밋 4f8684f) | hybrid ECS 에서 시뮬과 렌더 분리 원칙대로면 캐릭터 렌더는 처음부터 Mono 였어야 |
| 7 | 25 fallback Defender Mono 수렴 | 완료 (커밋 4462819) | 위와 동일 |
| 8 | 26 sort order unification — `BoardSortOrder.Compute` 공식 도입 | V-010 해소 | sort order 는 일찍 통일 공식을 만들었어야. spec 후반에 닦으니 이미 쌓인 시각 평가가 노이즈로 묶임 |
| 9 | 27 Place seam 경량 조정 (`tileTopScale` 0.86→0.95, `placeEdgeOpacity` 0.36→0.25, variants 4→3) | 시각 개선 미체감. 셀 단위 quad 구조 위에서는 경량 조정으로 seam 못 없앰 | **셀 quad 가 본질이면 quad scale 조정은 timing waste**. region mesh 로 가야 함을 더 빨리 인정했어야 |
| 10 | 28 Place region mesh refactor — `BuildPlaceRegionSurface` 도입 | cardinal 연결된 region 은 묶인 plate 로 보이나, **파편화된 작은 Place 조각은 여전히 seam 노출**. 원인은 맵 생성기가 Place 를 큰 덩어리로 안 내놓는 grid cell-by-cell 구조 | region mesh 는 **region 자체의 모양을 못 바꾼다**. mesh 로 묶어도 원본이 흩어져있으면 효과 제한적. 이 시점에서 컨셉 자체를 점검했어야 |
| 11 | rev4 컨셉 재정의 — "Enter the Gungeon" → "보드게임 타일" (Warhammer Underworlds/Gloomhaven). 격자감 수용 | 컨셉상 합리적 결정. V-007 무력화 가능 | **이 결정은 17 시점 (또는 그 이전) 에 이뤄졌어야**. 28 까지 끌고 와서 컨셉 항복 = 큰 시간 낭비 |
| 12 | 22 palette pass — `MapThemeData` 에 zone tint 4 필드, `forest.asset` 보드게임 톤 입력, `MapView` 가 `RuntimeMaterialFactory` 로 tint 전달 | 코드 트레이스 통과. 그러나 사용자 Play 보고 — **틴트 안 보임 + 엣지/코너 overlay 안 보임**. Unity MCP unavailable 로 자동 캡처 검증 불가 → 코드 레벨 판단만으로 종료 처리했다가 사용자 보고로 미해결 확인 | **코드 트레이스 ≠ 화면 검증**. URP / DOTS_INSTANCING / sortingOrder 같은 렌더 파이프라인 단의 함정이 코드 레벨에서는 안 보인다. Play 검증 없이 spec 종료 금지 |

### 잘못된 가정들 (컨셉/구조 차원)

1. **"Enter the Gungeon 류 시각은 시각 레이어 작업으로 만들 수 있다"** — 거짓. 룸 기반 시각은 룸 기반 맵 생성기가 전제. 셀 단위 생성기 위에서는 어떻게 해도 못 만든다.
2. **"region mesh 로 묶으면 Place 가 plate 로 보인다"** — 절반만 사실. region 자체가 파편화돼있으면 mesh 로 묶어도 plate 안 됨.
3. **"variant 갯수 줄이거나 opacity 낮추면 톤이 통일된다"** — 거짓. tone harmony 는 **base tint 자체** 가 같은 계열이어야 하는 문제이지 variant 갯수 문제가 아님.
4. **"코드 트레이스 + 컴파일 통과 + Console 무에러 = 시각 작업 완료"** — 거짓. URP transparent surface mode, DOTS_INSTANCING 키워드, runtime material 의 셰이더 프로퍼티 키 매칭 — 이 셋 중 하나만 틀어져도 코드는 멀쩡한데 화면이 안 바뀐다. Play 캡처가 진짜 검증.

### 잘못된 가정들 (렌더 파이프라인 차원)

> **2026-04-27 추기**: rev4 종료 직후 처음 적은 의심선 3개 중 2개 (URP transparent surface mode 누락, DOTS_INSTANCING 우회) 는 **사후 코드 트레이스에서 거짓으로 확인**. `RuntimeMaterialFactory.CreateTransparentTexture` 는 이미 surface mode 를 다 설정하고, `DOTS_INSTANCING_ON` 은 ECS RenderMesh 경로에서만 활성이라 Mono 타일에는 무관. 본 사례에서 진짜 교훈은 아래로 갱신.

1. **"`new Material(Shader.Find(...))` 으로 만든 머티리얼은 transparent 로 동작한다"** — 일반론은 거짓이지만, **본 프로젝트의 `RuntimeMaterialFactory.CreateTransparentTexture` 는 이미 `_Surface=1`, `_SrcBlend`, `_DstBlend`, `_ZWrite=0`, `_SURFACE_TYPE_TRANSPARENT` 키워드, render queue Transparent 를 모두 설정**. 새 spec 에서 이 함수를 또 의심하지 말 것. 다른 곳 (직접 `new Material(...)` 호출하는 코드) 이 있다면 그건 따로 점검.
2. **"DOTS_INSTANCING 키워드가 Mono 타일에서도 SetColor 를 무력화한다"** — 거짓. `DOTS_INSTANCING_ON` 은 Entities Graphics 가 ECS RenderMesh entity 를 그릴 때만 셰이더 variant 로 활성. Mono `MeshRenderer.sharedMaterial` 경로 (현재 모든 타일/프랍/캐릭터) 에서는 비활성 → `_BaseColor` 가 CBUFFER 에서 정상 읽힘. ECS 측 (projectile / healthBar) 에서 색을 동적으로 바꿀 때는 다르게 다뤄야 하지만 그건 본 spec 무관.
3. **"코드 트레이스만으로 root cause 를 단정할 수 있다"** — **이번에 가장 비싸게 배운 것**. handoff 작성 직후 코드 트레이스로 의심선 2개를 적었는데 사후 정밀 트레이스에서 둘 다 무관 판명. 진짜 root cause 는 (A) `_tileTextureMaterials` 캐시 키 설계, (B) Place edge mask 가 Env 한정, (C) overlay alpha 가 너무 낮음. **추측 의심선을 spec 에 박아두면 다음 작업자가 그것부터 따라가 시간 낭비**. 의심선은 "확정 전 가설" 로 명시하고, 결정 실험 (예: red-tint) 으로 좁힌 뒤에 작업 단위로 확정할 것.
4. **"sortingOrder 가 overlay 가시성 문제일 수 있다"** — 가능성은 있지만 본 사례에서는 거짓. region mesh y=0.002, hover y=0.006, edge overlay y=0.022, 모두 transparent + ZWrite off → URP 의 back-to-front 정렬로 자연스럽게 edge 가 위에 그려짐. occlusion 위험 없음. 새 spec 에서 sortingOrder 부여는 보험으로만 다루고 우선순위 낮춤.

### 다시 만들 때 우선순위 (제안)

1. **결정 실험으로 root cause 좁히기 (코드 트레이스 + 추측 금지)**:
   - 한 줄 변경 + Play 캡처로 가설을 좁힌다. 본 사례에서는 `forest.asset → placeBaseTint = (1,0,0,1)` 로 Place 가 빨개지는지가 결정 실험. 빨개지면 tint path 정상이니 이후 작업은 alpha/edge 마스크 쪽, 안 빨개지면 캐시/캐릭터 lookup 경로 쪽으로 분기.
   - **추측한 의심선을 spec 에 박지 말 것**. 의심선은 "결정 실험으로 확정 전 가설" 로만 표기하고, 실험 후에 작업 단위 spec 으로 승격.
   - 빈 씬 sanity check 도 옵션이지만 본 프로젝트의 `RuntimeMaterialFactory` 는 이미 URP transparent surface mode 를 다 설정하므로 우선순위 낮음.
2. **맵 생성 구조 결정 먼저**: 룸 기반인가 셀 기반인가. 시각 컨셉은 그 결정에 종속. **셀 기반이면 Enter the Gungeon 시각 포기, 보드게임 타일 으로 시작**.
3. **sortingOrder 공식을 첫 작업 단위 (`BoardSortOrder` 등) 로 못박기**. 모든 렌더 객체 (캐릭터/프랍/타일/오버레이) 가 같은 공식 위에서 정렬되도록.
4. **모든 캐릭터 렌더를 Mono 로 통일하고 시작**. ECS RenderMesh 와 Mono 섞으면 sorting 디버깅 비용 폭발.
5. **시각 작업의 완료 기준 = Play 캡처**. 코드 레벨 판단 금지. Unity MCP unavailable 이면 사용자에게 캡처 의뢰하고 그게 올 때까지 spec 종료 보류.
6. **palette/tint pass 는 마지막**. 그 전에 (a) tint 가 화면에 반영되는 path 가 한 가지 색 (예: red) 으로 가시 확인되고, (b) overlay alpha / mask 조건이 실제로 그릴 픽셀을 만들고 있는지 검증돼야 의미 있음. 본 사례는 (a) 와 (b) 둘 다 미확인 상태에서 palette 를 입혔다가 결과가 안 보였다.

### 다시 만들 때 절대 안 할 것

- "Enter the Gungeon 같은 자연스러운 바닥" 을 셀 단위 맵 생성기 위에서 시도하지 말 것.
- variant 갯수, tileTopScale, edgeOpacity 같은 경량 파라미터 튜닝으로 컨셉 미스를 덮으려 하지 말 것.
- ECS RenderMesh 경로의 캐릭터 sortingOrder 를 `Renderer.sortingOrder` 로 통일하려 하지 말 것 (애초에 다른 체계).
- 직접 `new Material(Shader.Find(...))` 호출하는 새 코드를 짜지 말 것 — `RuntimeMaterialFactory` 의 `CreateOpaque*` / `CreateTransparent*` 만 사용. (factory 가 이미 URP surface mode 를 명시 설정함. 이를 우회하면 transparent 가 opaque 로 그려지는 문제 재발 가능.)
- 의심선/가설을 검증 없이 spec / handoff 에 root cause 로 박지 말 것. 본 사례에서 "URP transparent surface mode 누락" / "DOTS_INSTANCING 우회" 두 의심선이 정밀 트레이스에서 둘 다 거짓으로 판명. 결정 실험 (예: 한 색 빨강으로 토글) 후에만 spec 화.
- Unity MCP unavailable 상태에서 시각 spec 을 코드 레벨 판단으로 완료 선언하지 말 것.
- 17c 처럼 전제 점검 안 한 채 spec 드래프트 시작하지 말 것 (렌더 경로/소속 맥락 먼저 확인).

### 살릴 것

baseline 코드 (`6c88007` 시점) 자체는 다시 만들 때도 재활용할 가치가 있음:

- `BoardVisualPlan` 타입 체계 + builder
- `BoardSortOrder.Compute` 공식
- Enemy/fallback Defender Mono quad view
- `BuildPlaceRegionSurface` region mesh 패턴
- `BackgroundPropPlacer` 의 Poisson + cluster + jitter 골격
- forest 테마 자산 세트 (atlas v2 / surface variants / edge / corner texture)

이것들은 컨셉 미스와 무관하게 구조적으로 맞는 결과물이다. 다시 만들 때 0 부터 다시 짤 필요 없음.
