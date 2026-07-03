# 4. MapThemeData LEGACY 필드 43개 삭제

## 목적

Legacy 렌더만 읽던 `MapThemeData` 필드 43개 + nested `TerrainSurfaceVariant` 클래스를 삭제하고 테마 asset 을 재직렬화한다. 선행: unit 2 (유일 독자였던 MapView/TerrainSurfaceSelector/TerrainTileRuleResolver 삭제 — grep 검증 2026-07-03: 그 외 독자 0건).

## 변경 대상

**`Assets/_Project/Scripts/Data/MapThemeData.cs`** — 삭제 그룹 (필드 수):

| 그룹 (Header) | 필드 | 수 |
|---|---|---|
| Deprecated Tile Surface | place/walk/env/decoTileTexture + 4종 Variants[] | 8 |
| Surface Variation Settings | tileVariantNoiseScale/Jitter/SeedOffset | 3 |
| Env Base (+Deprecated Deco) | place/walk/env/decoSurfaceRules + path/edgeSurfaceInfluence | 6 |
| Walk Shape Set | walkSingle/StraightNS/StraightEW/Corner/End/TJunction/Cross | 7 |
| Env Detail | terrainDetailTextures/Density/Scale | 3 |
| Place Transition | 4 텍스처 + inner/outer scale·opacity, edgeOpacity/Thickness + tileThickness/tileTopScale/tileSideColor | 13 |
| Zone Tints | place/walk/envBaseTint | 3 |
| nested class | `TerrainSurfaceVariant` 통삭제 | — |

계: 43. **`propGlobalTint` 는 ACTIVE(PropInstanceUtil 경유) — 유지.** `WeightedProp`/tileSet/프랍풀/poisson/obstacle 계열 전부 유지 (README ACTIVE 목록).

**Asset 재직렬화**: `forest.asset` / `desert.asset` — 필드 삭제 후 SetDirty + SaveAssets 로 stale YAML 키 제거 (UnityMCP execute_code).

## 구현

1. 필드/Header/nested class 삭제. attribute(`[Tooltip]` 등) 포함 블록 단위로.
2. compile 후 `rg "placeTileTexture|walkSurfaceRules|walkCornerTexture|placeEdgeTexture|placeBaseTint|terrainDetailTextures|tileVariantNoiseScale|TerrainSurfaceVariant" Assets --type cs` → 0건.
3. 테마 asset 2종 재직렬화 → `rg "placeBaseTint" Assets/_Project/Data --type-add 'asset:*.asset' -t asset` 로 YAML 잔존 키 0건 확인.
4. 인스펙터에서 forest.asset 열어 LEGACY Header 그룹 소멸 + ACTIVE 필드 값 보존 육안 확인.

**주의**: asset 재직렬화는 값 손실 없는 키 제거만이어야 함 — tileSet/프랍풀 참조가 유지되는지 diff 로 확인 후 커밋.

## 완료 기준

- [ ] compile 통과 (에러 0)
- [ ] 삭제 필드명 grep 0건 (코드 + asset YAML)
- [ ] Tilemap Play 스크린샷 무회귀 (바닥 tileSet/프랍/tint)
- [ ] forest/desert 인스펙터: LEGACY 그룹 소멸, tileSet·프랍풀·propGlobalTint 값 보존
