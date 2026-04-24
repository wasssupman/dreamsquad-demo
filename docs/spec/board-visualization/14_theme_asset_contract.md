# 14. Theme Asset Contract

## 목적

테마 자산 카테고리를 고정된 계약으로 정리한다. `MapThemeData` 의 legacy 필드와 rev3 신규 필드를 분리하고 forest / volcano 양쪽이 같은 카테고리 세트를 채우도록 한다.

## 전제

- `7` (Deco resolution: Env folding 유지) 완료.
- `10` (place rendering + inner corner 슬롯 추가) 완료.
- `11` (env variation) 완료.

## 변경 대상

- `Assets/_Project/Scripts/Data/MapThemeData.cs`
- `Assets/_Project/Map/Theme/forest/forest.asset`
- `Assets/_Project/Map/Theme/volcano/volcano.asset`
- 필요 시 신규 asset 생성 / 누락분 식별

## 카테고리 계약 (v1)

| 카테고리 | 필드 | 최소 세트 | null 시 동작 |
|---|---|---|---|
| env base | `envSurfaceRules[]` | 2 종 이상 | fallback color material |
| env detail | `terrainDetailTextures[]`, `terrainDetailDensity`, `terrainDetailScale` | 1 종 이상 | detail skip |
| place base | `placeSurfaceRules[]` 또는 `placeTileVariants[]` | base + variants 3 종 | fallback color |
| place transition | `placeEdgeTexture`, `placeOuterCornerTexture`, `placeInnerCornerTexture` | outer/inner/edge 각 1 종 권장 | 각 null → 해당 overlay skip. inner corner 가 null 이면 outer corner 로 **fallback 하지 않음** |
| walk shape set | `walkSingleTexture`, `walkStraightNSTexture`, `walkStraightEWTexture`, `walkCornerTexture`, `walkEndTexture`, `walkTJunctionTexture`, `walkCrossTexture` | 7 종 필수 | 누락 시 base walk texture fallback |
| walk transition | `walkShoulderTexture` (optional) | 0~1 | null 이면 shoulder 없음 |
| prop family | `tileProps[]`, `decorProps[]` | background ≥5, decor ≥3 | placer 에서 그냥 skip |

## Deprecated 격리

아래 필드는 `[Header("Deprecated")]` 아래로 이동하고 런타임 경로에서 호출되지 않도록 한다:

- `placeTileTexture`
- `walkTileTexture`
- `envTileTexture`
- `decoTileTexture`
- `placeTileVariants` (신규 surface rule 이 대체)
- `walkTileVariants`
- `envTileVariants`
- `decoTileVariants`
- `decoSurfaceRules` (Deco folding 결정 `7` 에 의해 render 에 미사용)

Deprecated 필드는 제거하지 않고 보존한다 (기존 asset 과의 호환). `18_` 이후 cleanup spec 에서 제거 결정.

## null 처리 정책

- renderer 는 null asset 을 만나도 예외 없이 skip.
- placer 는 null prop prefab 을 skip.
- inner corner texture null 이면 inner corner 표현이 시각적으로 없어진다 (outer corner fallback 금지, degrade 의도).

## 구현 가이드

1. `MapThemeData` 내부를 위 카테고리 헤더로 그룹핑. Inspector 에서 카테고리 구분이 보이게 `[Header]` 사용.
2. 누락 카테고리가 있으면 runtime `Debug.Assert` 로 경고 (build 차단까지는 가지 않음).
3. forest:
   - 이미 채워진 slab variants, walk shape texture 유지
   - `placeInnerCornerTexture` 신규 asset 필요 (프로토 품질로 1 장)
   - env variation 2 종 이상 확인
4. volcano:
   - 누락된 카테고리 식별 후 프로토타입 asset 채움
   - 디자인 완성도 목표 아님, 구조 점검용
5. 새 테마 추가 가이드 문서 (optional): `docs/spec/board-visualization/theme_template.md`. rev3 스프린트에서는 필수 아님.

## 완료 기준

- `MapThemeData` 가 카테고리별 헤더로 그룹핑됨
- forest, volcano 양쪽이 "필수" 세트 채움 (최소 세트)
- Deprecated 섹션이 격리되어 있음
- 누락 asset 이 있어도 렌더 오류 없이 skip
- inner corner null fallback 이 `Debug.Log` 없이 정상 동작

## 주의

- 본 단계에서 inner corner 용 새 asset 은 최소 프로토타입. 고품질은 별도 asset pass.
- `7` (Deco folding) 결정에 의해 `decoSurfaceRules` 는 렌더 경로에서 호출되지 않음. 자산은 보존하되 runtime 에서 참조 0.
- 필드 이동은 Unity Inspector 에서 reference 가 깨지지 않도록 FormerlySerializedAs 적절히 사용.

확인 일자: 2026-04-24 / 커밋 해시: 7efc54d
