# Desert Theme

> 상태: 완료 2026-06-30 (재활용 기반 arid 사막 + 폴리시: teal 엣지 해결·sand 색조·moss 완화). 선인장 아트만 차단(후속). 사용자 육안 통과.
> 전제: 테마 = `MapThemeData`. 런타임 선택 = `SeasonRuntime.Active.mapTheme` ← `SeasonData.mapTheme` ← `SeasonRegistry.defaultSeason`.
> 대상: `Map/Theme/desert/`, `Art/Theme/desert/`, `Data/Theme/desert/`, `Data/TileSets/`, `Data/Season/`. forest 불변.

## 목표 / 검증 질문

> **기존 맵 구성 구조를 그대로 써서, 기존 에셋 재활용 중심으로 사막 룩(사막 바닥 + 사막 프랍)의 desert 테마가 Play 에서 렌더되는가?**

## 핵심 발견 (구현 방향을 바꾼 사실)

- **Tilemap 모드 바닥은 테마가 아니라 `BattleBridge.tileSet`(`TileSetData`, scene 필드)이 결정.** `MapThemeData`/`SeasonData` 는 TileSetData 를 안 가졌음 → 바닥이 테마/시즌 무관(전 테마 공용)이었다.
- 그래서 desert 바닥을 위해 **테마-구동 tileSet 훅**을 추가: `MapThemeData.tileSet` 필드 + `BattleBridge` 가 `theme.tileSet ?? scene tileSet`. forest 는 `tileSet=null` → scene fallback(무영향).
- 프랍은 테마(`tileProps`)가 이미 구동 → desert 프랍은 추가 코드 없이 동작.

## feature-wide 계약

- **테마-구동 tileSet (신규 코드, ~5줄).** `MapThemeData.tileSet` 지정 시 그 TileSetData 로 Tilemap 바닥 렌더, 비면 scene fallback. 향후 모든 테마가 바닥 교체 가능.
- **desert 바닥 = `TileSet_Desert`** (AutoTileTest 복제): env/deco/terrain → `Tile_Sand`(sand 스프라이트, PPU 1024=1셀, Bilinear/Uncompressed), `surroundFarColor` 샌디. walk(스톤)·place(더트) 오토타일은 사막에도 무난해 공유.
- **desert 프랍 = 바이옴-중립 13종**(바위·boulder·dead_tree·skull·ruin·crates·log·stump). `propGlobalTint` 웜.
- **forest 와 분리(테마 데이터)**: desert 전용 PropData 13종을 `Data/Theme/desert/` 에 복제, `tileProps` 재연결. `desert.asset` 의 forest 텍스처 레거시 참조는 null. **중립 프리팹/스프라이트/오토타일은 공유**(돌·통나무는 바이옴 무관 — 사용자 합의).
- **테마 선택 = 시즌.** `season_S2_desert`(SeasonData→desert) + `SeasonRegistry.allSeasons=[forest,desert]`. **defaultSeason=forest 유지**(커밋) — desert 는 선택형. 기본 전환은 `defaultSeason` 한 줄.
- **타일 import 규칙**: Bilinear + Uncompressed. [[project_tilemap_grid_lines_cause]]
- **씬/`mode=force` reimport 금지.** [[feedback-scene-save-bakes-wip]] · [[feedback-unitymcp-force-reimport-breaks-bridge]]

## 작업 단위

| # | 문서 | 작업 | 상태 |
|---|---|---|---|
| 0 | `0_sand_tile_import.md` | sand 텍스처 → `Art/Theme/desert/`(Sprite, Bilinear/Uncompressed) | ✅ |
| 1 | `1_desert_theme_asset.md` | `desert.asset`(MapThemeData): sand env/deco·틴트·13 프랍 | ✅ |
| 2 | `2_tileset_hook_and_ground.md` | 테마-구동 tileSet 훅(코드) + `TileSet_Desert`+`Tile_Sand` | ✅ |
| 3 | `3_season_separation_verify.md` | season_S2_desert + 레지스트리 + forest 분리(PropData 복제) + Play 검증 | ✅ |

## 폴리시 (unit 5, 2026-06-30 후속)

- **teal 오토타일 엣지 ✅ 해결**: Test/ 의 107 오토타일 스프라이트를 PIL 로 teal→sand recolor → `Generated/Tiles/Desert/`, `AutoTile_PlaceDirt_Desert`/`_Stone_Desert` 생성(스프라이트 guid 재매핑), `TileSet_Desert` 의 place/walk 재연결. 씬 무변경.
- **sand 색조 ✅**: `Tile_Sand.color` salmon→골든탄.
- **프랍 green moss ⚠️ 완화**: `propGlobalTint` 강화(웜)로 녹색→khaki. 큰 통나무/보울더 잔여 녹색은 "마른 이끼"로 수용(사용자 결정).
- **선인장 아트 ⛔ 차단**: 프로젝트에 사막 식생 스프라이트 0, 직접 작성 불가. 아트 확보 시 전용 PropData/프리팹.

## 후속 후보

- moss 잔여가 거슬리면 mossy 프랍(fallen_log/boulder_cluster) 큐레이션 제외.
- desert 를 기본 시즌으로(현재 forest 기본 — `defaultSeason` 한 줄).
- 선인장/야자 식생 아트 + 사막 전용 path(스톤이 쿨톤) 워밍.
