# Terrain Surface Rules Spec

**보존 상태**: legacy archive. 현재 활성 source of truth 는 `docs/spec/board-visualization/` 이다. 이 문서는 이전 terrain surface rule 설계 이력을 보존하기 위한 문서로 동결한다.

**작성일**: 2026-04-23  
**상태**: v1 surface rule 구현 완료. Walk shape 판별, shape texture slot 연결, forest/volcano walk shape texture 연결 완료. edge mask 전용 타일은 다음 구현 대상.

## 목표

맵의 게임플레이 타일 타입(`Place`, `Walk`, `Env`, `Deco`)은 유지하면서, 시각적으로는 배경 지형이 하나의 연속된 표면처럼 보이게 한다.

초기 목표는 단순 variant random 을 넘어서 다음을 지원하는 것이다.

- Forest: grass, small grass, big grass 가 군집과 외곽/경로 영향에 따라 자연스럽게 분포한다.
- Volcano: burn land, burn grass 가 노이즈/경로/외곽 기준으로 분포한다.
- 같은 seed, 같은 map, 같은 theme 에서는 동일한 표면 결과가 나온다.
- `MapView` 는 배경 지형/경로/배치석을 서로 다른 렌더 레이어로 분리한다.

## 현재 구조

```text
GeneratedMap
  -> MapView.BuildBoardBase
  -> MapView.BuildContinuousTerrainTop
  -> MapView.BuildTiles
      -> Walk: shape texture overlay
      -> Place: individual buildable tile top
      -> Env/Deco: no per-cell top; continuous terrain surface handles the visual
```

`GeneratedMap.tiles` 는 게임플레이 로직용 타일 타입만 가진다.  
배경 지형은 개별 셀마다 완성 텍스처를 붙이지 않는다. `Env/Deco` 는 하나의 연속 지형면으로 렌더링하고, 경로와 배치석만 그 위에 얹는다.

## 데이터 계약

`MapThemeData` 는 기존 단일 텍스처/variant 배열 외에 다음 rule 배열을 가진다.

- `placeSurfaceRules`
- `walkSurfaceRules`
- `envSurfaceRules`
- `decoSurfaceRules`

각 `TerrainSurfaceVariant` 는 다음 값을 가진다.

| 필드 | 의미 |
|---|---|
| `texture` | 이 rule 이 선택되면 사용할 타일 top 텍스처 |
| `weight` | 기본 선택 가중치 |
| `noiseRange` | low-frequency 지형 노이즈 선호 범위 |
| `moistureRange` | secondary detail/moisture 노이즈 선호 범위 |
| `nearPathMultiplier` | 경로 근처 셀에서의 가중치 배율 |
| `edgeMultiplier` | 맵 외곽 셀에서의 가중치 배율 |

테마 전역 파라미터:

- `tileVariantNoiseScale`: 지형 노이즈 스케일
- `tileVariantJitter`: legacy variant fallback 의 셀별 jitter
- `tileVariantSeedOffset`: 테마별 deterministic offset
- `pathSurfaceInfluence`: 경로 인접도가 surface rule 에 영향을 주는 강도
- `edgeSurfaceInfluence`: 맵 외곽 거리가 surface rule 에 영향을 주는 강도

## Rule 평가 방식

각 후보 rule 은 아래 점수를 계산한다.

```text
score =
  weight
  * RangeScore(macroNoise, noiseRange)
  * RangeScore(moistureNoise, moistureRange)
  * Lerp(1, nearPathMultiplier, pathInfluence)
  * Lerp(1, edgeMultiplier, edgeInfluence)
  * deterministicJitter
```

가장 높은 score 의 texture 를 선택한다.

`macroNoise` 와 `moistureNoise` 는 같은 셀에서 항상 동일하게 계산되는 coherent noise 이다.  
`pathInfluence` 는 주변 `Walk` 타일의 맨해튼 거리 기반으로 계산한다.  
`edgeInfluence` 는 맵 외곽에 가까울수록 커진다.

## Critic Review 반영

2026-04-23 critic review 에서 다음 리스크가 확인됐다.

- `Env/Deco` 를 continuous terrain top 으로 처리하면서 기존 `env/decoSurfaceRules` 가 실제 렌더 경로에서 우회된다.
- 셀별 `SelectRuleTexture()` 만으로는 이웃 연속성/경계 완화가 부족하다.
- `MapThemeData` 에 단일 텍스처, legacy variants, surface rules, walk shape texture 가 공존해 어떤 필드가 실제로 사용되는지 불명확하다.
- selector 단위 테스트만 있고 `MapView` 통합 렌더 경로를 검증하지 못한다.

이번 구현에서 반영할 결정:

- `MapView` 가 tile type 을 직접 해석하지 않고 `TerrainTileRuleResolver` 를 통하도록 한다.
- `Env/Deco` surface rule 은 현재 continuous terrain 의 base texture 후보로만 사용한다. 셀별 군집 분포 표현은 추후 procedural terrain texture 또는 overlay pass 로 분리한다.
- 이번 패스의 목표는 full biome generation 이 아니라 `Walk`/`Place`/background 관계를 resolver 결과로 명시하는 것이다.
- 테스트는 resolver 산출물까지 확장한다.

반영 결과:

- `TerrainTileRenderInfo` 추가.
- `TerrainTileRuleResolver` 추가.
- `MapView` 의 `Walk`/`Place` 렌더 결정과 continuous terrain texture 선택을 resolver 경유로 변경.
- `TerrainTileRuleResolverTests` 추가.

## Fallback

surface rule 이 없거나 usable rule 이 없으면 기존 구조를 유지한다.

1. `{tileType}SurfaceRules`
2. `{tileType}TileVariants`
3. `{tileType}TileTexture`
4. fallback color material

이 순서로 사용한다.

## Rendering Layer Contract

자연스러운 지형감을 위해 렌더링 책임을 분리한다.

| 레이어 | 대상 | 렌더 방식 |
|---|---|---|
| Board Base | 맵 전체 하단 두께/측면 | 맵 크기 하나의 cube |
| Continuous Terrain Top | `Env/Deco` 배경 지형 | 맵 크기 하나의 quad, theme grass/ground texture 를 tiling |
| Walk Overlay | `Walk` 경로 | 셀별 shape texture overlay, top scale 은 1 이상으로 맞닿게 배치 |
| Buildable Tile Top | `Place` 배치 가능 타일 | 개별 stone/slab tile, top scale 을 줄여 풀밭 사이에 얹힌 느낌 유지 |
| Terrain Detail Overlay | `Env/Deco` 배경 위 작은 풀/잔디 디테일 | seeded scatter quad, 투명 texture |
| Place Edge Overlay | `Place` 와 background 경계 | edge mask 기반 투명 grass fringe overlay |

이 구조에서는 배경 풀밭이 셀 단위로 끊기지 않고, 레퍼런스처럼 길과 배치석만 지형 위에 놓인 오브젝트처럼 보인다.

## Forest v1 Rule 방향

Forest 는 `Env/Deco` background tiles 에 다음 표면을 사용한다.

- `grass1`: 중간 노이즈 범위의 기본 풀밭
- `grass2`: 살짝 어두운/다른 질감의 기본 풀밭
- `smallgrass1`: 경로 근처 또는 detail noise 가 높은 구역에 작은 풀
- `biggrass2`: 맵 외곽/높은 macro noise 구역에 큰 풀 군집

아직 `Place/Walk` 전용 surface rule 은 비워져 있고, 기존 texture fallback 을 사용한다.

## Volcano v1 Rule 방향

Volcano 는 `Env/Deco` background tiles 에 다음 표면을 사용한다.

- `burn_land1`: 갈라진 탄 지면. 외곽/높은 macro noise 구역에서 더 강함.
- `burn_grass1`: 그을린 풀. 경로 근처 또는 detail noise 가 높은 구역에서 더 강함.

아직 volcano 전용 prop/obstacle set 은 없다.

## 다음 구현: 경로/경계 전환

현재 rule 은 셀 하나의 texture 를 선택하거나, Walk shape 정도만 판별한다. 다음 단계는 RuleTile-like resolver 를 추가해 이웃 타일과의 관계를 렌더 결과로 명확히 분리한다.

### RuleTile-like Resolver

Unity `RuleTile` 을 직접 쓰지는 않는다. 현재 프로젝트는 `GeneratedMap` + 3D `MapView` 구조이므로, Unity TilemapRenderer 로 갈아타지 않고 같은 개념만 가져온다.

새 resolver 의 책임:

```text
GeneratedMap + MapThemeData + cell
  -> neighbor mask 계산
  -> tile type / shape / edge relation 판별
  -> TerrainTileRenderInfo 반환
  -> MapView 가 base/overlay quad 를 생성
```

초기 산출물:

```csharp
public readonly struct TerrainTileRenderInfo
{
    public readonly Texture2D baseTexture;
    public readonly Texture2D overlayTexture;
    public readonly float yaw;
    public readonly bool drawBase;
    public readonly bool drawOverlay;
}
```

초기 resolver 범위:

- `Walk`: 기존 shape texture 를 overlay 로 반환한다.
- `Place`: 배치석 base texture 를 반환한다.
- `Env/Deco`: 기본적으로 셀별 base 를 그리지 않는다. continuous terrain top 이 담당한다.
- `Place` 주변 `Env/Deco` 경계는 edge overlay 후보로만 다룬다.

이번 패스에서 하지 않을 것:

- Unity `RuleTile`/`TilemapRenderer` 직접 도입.
- 47-tile full autotile set.
- diagonal/inner corner full coverage.
- `GeneratedMap` 데이터 구조 변경.
- pathfinding/placement 로직 변경.

### Neighbor Mask

기본 방향 mask 는 N/E/S/W 순서로 저장한다.

| bit | 방향 |
|---|---|
| 1 | N |
| 2 | E |
| 4 | S |
| 8 | W |

resolver 는 필요에 따라 다음 관계를 계산한다.

- same-type mask: 같은 gameplay tile type 과 연결되는지.
- walk mask: 주변 경로와 맞닿는지.
- background mask: 주변이 `Env/Deco` 인지.
- place mask: 주변이 `Place` 인지.

### Render Layer Result

렌더 레이어는 다음 순서를 유지한다.

1. `BoardBase`
2. `ContinuousTerrainTop`
3. `Walk Overlay`
4. `Place Base`
5. future edge/transition overlay

`MapView` 는 tile type 을 직접 많이 분기하지 않고, resolver 결과를 받아 필요한 quad 를 만든다. 다만 `Env/Deco` 는 continuous terrain 으로 처리되므로 기본적으로 셀별 quad 를 만들지 않는다.

### Walk 형태 판별

각 `Walk` 셀은 4방향 이웃의 `Walk` 여부로 형태를 판별한다.

- `single`
- `end_n/e/s/w`
- `straight_ns/ew`
- `corner_ne/nw/se/sw`
- `t_n/e/s/w`
- `cross`

이 결과를 기반으로 `walkSingleTexture`, `walkStraightNSTexture`, `walkStraightEWTexture`, `walkCornerTexture`, `walkEndTexture`, `walkTJunctionTexture`, `walkCrossTexture` 를 우선 선택한다.  
전용 texture 가 비어 있으면 기존 `walkSurfaceRules`, `walkTileVariants`, `walkTileTexture` fallback 을 사용한다.

corner/end/T-junction 텍스처는 기준 방향 1장을 사용하고, `MapView` 가 shape 에 맞춰 tile top quad 를 회전한다.

### Background/Place 경계

`Place` 와 `Env/Deco` 가 만나는 셀은 갑작스러운 색/질감 단절이 생긴다.  
다음 단계에서는 background cell 또는 place cell 에 edge mask 를 계산한다.

- N/E/S/W 방향 인접 타일 타입
- diagonal corner 여부
- path proximity
- edge transition texture 또는 overlay 선택

### Theme Transition Texture

Forest 예시:

- `tile_forest_walk_straight`
- `tile_forest_walk_corner`
- `tile_forest_path_grass_edge`
- `tile_forest_place_grass_edge`

Volcano 예시:

- `tile_volcano_walk_cracked_straight`
- `tile_volcano_walk_cracked_corner`
- `tile_volcano_burn_edge`
- `tile_volcano_lava_scorch_edge`

## 완료 기준

- [x] surface rule 데이터 구조 추가
- [x] selector 로 MapView variant 선택 책임 분리
- [x] forest env/deco surface rule 구성
- [x] volcano env/deco surface rule 구성
- [x] 새 타일 텍스처 mipmap/trilinear/aniso import 설정
- [x] surface rule unit test
- [x] Walk shape 판별
- [x] Walk straight/corner/end texture slot/fallback 연결
- [x] Forest/Volcano walk shape 전용 이미지 생성 및 theme 연결
- [x] `Env/Deco` per-cell top 제거, continuous terrain top 구조 적용
- [x] `Walk` overlay 와 `Place` individual tile 렌더 레이어 분리
- [x] RuleTile-like resolver 추가
- [x] resolver 산출물 unit test
- [x] Place/background edge mask 와 grass fringe overlay prototype
- [x] Terrain detail seeded scatter prototype
- [ ] 전용 고품질 detail/edge decal asset 제작
- [ ] Play mode 시각 smoke
