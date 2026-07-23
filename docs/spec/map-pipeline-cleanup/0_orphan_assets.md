# 0. 참조 0 orphan 에셋 삭제

## 목적

전 프로젝트 GUID grep 으로 참조 0 이 확인된 맵/타일셋 에셋을 삭제한다. 코드·씬 무관, 무위험. 가장 먼저 처리해 이후 유닛의 노이즈를 줄인다.

> **리뷰 정정(2026-07-23)**: 최초 초안은 `desert.asset`(MapThemeData) + `TileSet_Desert` 도 orphan 으로 봤으나 **오류**였다. `SeasonRegistry.asset.allSeasons[1] = season_S2_desert.asset`(등록된 라이브 시즌)의 `mapTheme` 가 `desert.asset`(guid febf3efe…)을 가리키고, `desert.asset` 이 `TileSet_Desert`(466c1d82…)를 참조한다. 둘 다 **live** → 삭제 목록에서 제외. `season_S2_desert` 자체의 폐기 여부는 별도 product 결정(이 스펙 범위 밖, 후속 후보로 이관).

## 변경 대상 (삭제)

| 에셋 | GUID | 근거 |
|---|---|---|
| `Assets/_Project/Data/Maps/MapDocument_TwinLane.asset` | 51855b55… | 풀에 없음, 자기 meta 외 참조 0 |
| `Assets/_Project/Data/Maps/MapDocument_hello.asset` | c2b667df… | 풀에 없음, 참조 0 |
| `Assets/_Project/Data/TileSets/TileSet_Placeholder.asset` | 47e86843… | 참조 0 |
| `Assets/_Project/Data/TileSets/TileSet_PlaceholderIso.asset` | d818ceae… | 참조 0 |

각 `.asset` + `.asset.meta` 함께 삭제. **`desert.asset`/`TileSet_Desert`/`desert/` 폴더는 유지**(위 정정).

## 구현

1. 삭제 직전 재확인: 각 GUID 를 전 레포에서 grep(`.asset`/`.unity`/`.prefab`, 자기 `.meta` 제외) → 참조 0 재확인. **1건이라도 나오면 정지하고 보고**(desert 처럼 숨은 season 참조가 더 있을 수 있음).
2. `git rm` (또는 파일 삭제 후 스테이징). AssetDatabase.DeleteAsset 은 MCP `safety_checks=false` 필요할 수 있음.
3. 삭제 후 Unity refresh → 콘솔에 missing-reference 경고 0 확인.

## 완료 기준

- [x] 4개 asset + meta 삭제 (desert 관련 2종은 **유지**)
- [x] 삭제 후 각 GUID grep 참조 0 (자기참조 없음 — 삭제 전 전수 재확인 완료)
- [x] Unity refresh 후 콘솔 missing-reference/broken-guid 경고 0
- [x] EditMode green (변화 없어야 정상 — 이 유닛은 코드 무관)

확인 2026-07-23 — 삭제 전 GUID 4종 전수 grep 참조 0 재확인 → UnityMCP manage_asset delete(meta 동반) → refresh 후 콘솔 error/warning 0 → EditMode 1301 중 1299 green(2 skip=기존 Ignored).
