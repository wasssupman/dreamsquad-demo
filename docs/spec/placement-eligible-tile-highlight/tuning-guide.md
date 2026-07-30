# 배치 하이라이트 · 사거리 커스터마이징 가이드

배치 가능 하이라이트(시안 슬랩)와 공격 사거리(라임 그리드)의 겉모습을 **코드 수정 없이** 조정하는 법.
값은 대부분 `TileSetData` 에셋 인스펙터에 있고, 모양(슬랩/아웃라인)만 스프라이트 교체다.

> ⚠️ **어느 TileSet 을 바꾸나** (2026-07-31 정정 — 실제 존재하는 `TileSetData` 는 **2개**다.
> 이전 판이 적었던 `TileSet_Placeholder` / `TileSet_PlaceholderIso` 는 없다):
>
> | 에셋 | guid | 언제 쓰이나 |
> |---|---|---|
> | `Generated/Tiles/AutoTileTest/TileSet_AutoTileTest` | `d780c834…` | **BattleScene 의 `BattleBridge.tileSet`(씬 fallback)** — forest 테마는 `tileSet` 미할당이라 여기로 떨어진다 = **현재 시즌(overwork/forest)의 라이브** |
> | `Data/TileSets/TileSet_Desert` | `466c1d82…` | `Map/Theme/desert/desert.asset` 의 `tileSet` — **사막 시즌에서만** |
>
> **이름이 직관과 반대다.** "Desert" 가 씬 기본값처럼 보이지만 씬이 물고 있는 건 AutoTileTest 쪽이고,
> `TileSet_Desert` 를 고치면 지금 시즌에서는 **아무 변화가 없다**(2026-07-31 실제로 여기서 한 번 헛짚었다).
> 규칙은 `BattleBridge.cs:1041-1043` — 테마가 지정하면 그 tileSet, 아니면 씬 fallback. 전 시즌 통일하려면 2개 다 바꾼다.
> 인스펙터에서 값 바꾼 뒤 **Ctrl+S(SaveAssets)** 로 저장. 파일을 에디터 **밖에서** 고쳤다면 그 에셋을
> Reimport 해야 반영된다(에디터는 메모리의 옛 값을 계속 쓰고, 저장 시 되레 덮어쓴다).

---

## 한눈에 — 무엇을 / 어디서 / 어떻게

| 바꾸고 싶은 것 | 어디 | 현재값 |
|---|---|---|
| 배치영역 **색·투명도** | TileSet 인스펙터 `placeableColor` | 시안 `(0.5, 0.88, 1, 0.5)` |
| 배치영역 **슬랩 모양**(림 두께/내부) | 스프라이트 `placeable_slab.png` 교체 | 64px, 림 5px+베벨 |
| 배치영역 **등장 페이드 시간** | TileSet 인스펙터 `placeableFadeInDuration` | `0.2` 초 |
| 사거리 **색** | TileSet 인스펙터 `rangeColor` | 라임 `(0.55, 1, 0.25)` |
| 사거리 **밝기(투명도)** | TileSet 인스펙터 `rangePulseMaxAlpha` | `0.85` |
| 사거리 **아웃라인 두께** | 스프라이트 `tile_grid_outline.png` 교체 | 3px solid + 1px soft |
| 겹침 순서 / z-오프셋 | 코드(고급) | 아래 참조 |

---

## 1. 배치 가능 영역 (시안 슬랩)

- **색·투명도** — `placeableColor` (인스펙터 색 필드). 알파(A)가 전체 진하기. 낮추면 은은, 높이면 진함.
  - 초록 금지(hover valid 와 충돌), 노랑 금지(사거리와 충돌). 차가운 계열(시안/블루/보라) 권장.
- **등장 페이드** — `placeableFadeInDuration`. `0` = 즉시(연출 없음), 크게 = 천천히 차오름.
- **슬랩 모양** — `placeable_slab.png` (64px). 흰색 베이스에 **알파로 형태**(색은 위 placeableColor 가 입힘).
  - 현재 프로파일: 가장자리 5px = 밝은 림(불투명), 안쪽으로 베벨 falloff, 중앙 = 옅은 채움(0.35).
  - 이미지 에디터로 직접 그려도 되고, 아래 스니펫으로 재생성해도 된다.

## 2. 공격 사거리 (라임 그리드)

- **색** — `rangeColor`. ⚠️ **스킬 조준 사거리도 같은 색을 쓴다**(공유). 바꾸면 둘 다 바뀜.
  드롭 **후** 방향 지정(조준 페이즈)은 별도 knob `aimRangeColor`(주황) 라 따라오지 않는다.
- **밝기** — `rangePulseMaxAlpha` (0~1). 현재 펄스가 꺼져 있어 이 값이 **정적 투명도**. (`rangePulseMinAlpha`/`rangePulseSpeed` 는 펄스 꺼진 상태라 무시됨.)
- **아웃라인 두께** — `tile_grid_outline.png` (64px, 흰색 테두리 스프라이트). 테두리 px 를 바꾸면 두께가 바뀐다.

## 3. 스프라이트 재생성 스니펫 (선택 — 모양·두께 조정)

이미지 에디터 대신 Unity 에서 절차적으로 다시 굽고 싶으면, `execute_code`(Unity MCP)로 아래 실행.
숫자만 바꾸면 된다.

**사거리 아웃라인 두께** — `tile_grid_outline.png`:
```csharp
string p = "Assets/_Project/Data/TileSets/tile_grid_outline.png";
int S=64, SOLID=3, SOFT=1;   // ← SOLID: 꽉 찬 테두리 px, SOFT: 바깥쪽 부드럽게 번지는 px
var tex=new UnityEngine.Texture2D(S,S,UnityEngine.TextureFormat.RGBA32,false);
for(int y=0;y<S;y++)for(int x=0;x<S;x++){
  int d=System.Math.Min(System.Math.Min(x,y),System.Math.Min(S-1-x,S-1-y));
  float a = d<SOLID?1f : d<SOLID+SOFT?UnityEngine.Mathf.Lerp(1f,0f,(float)(d-SOLID)/SOFT):0f;
  tex.SetPixel(x,y,new UnityEngine.Color(1,1,1,a));
}
tex.Apply();
System.IO.File.WriteAllBytes(p,UnityEngine.ImageConversion.EncodeToPNG(tex));
UnityEngine.Object.DestroyImmediate(tex);
UnityEditor.AssetDatabase.ImportAsset(p,UnityEditor.ImportAssetOptions.ForceUpdate);
```

**배치 슬랩 모양** — `placeable_slab.png` (RIM=림 두께, INNER=내부 진하기):
```csharp
string p="Assets/_Project/Data/TileSets/placeable_slab.png";
int S=64, RIM=5; float INNER=0.35f;   // ← RIM: 밝은 테두리 px, INNER: 중앙 채움 알파(0~1)
var tex=new UnityEngine.Texture2D(S,S,UnityEngine.TextureFormat.RGBA32,false);
for(int y=0;y<S;y++)for(int x=0;x<S;x++){
  int d=System.Math.Min(System.Math.Min(x,y),System.Math.Min(S-1-x,S-1-y));
  float a = d<1?0f : d<RIM?1f : d<RIM+4?UnityEngine.Mathf.Lerp(1f,INNER,(float)(d-RIM)/4f):INNER;
  tex.SetPixel(x,y,new UnityEngine.Color(1,1,1,a));
}
tex.Apply();
System.IO.File.WriteAllBytes(p,UnityEngine.ImageConversion.EncodeToPNG(tex));
UnityEngine.Object.DestroyImmediate(tex);
UnityEditor.AssetDatabase.ImportAsset(p,UnityEditor.ImportAssetOptions.ForceUpdate);
```

## 4. 고급 — 코드에서만 바꾸는 것 (`Assets/_Project/Scripts/Core/TilemapMapView.cs`)

- **사거리 펄스 다시 켜기**: `Update()` 의 사거리 알파 블록을 정적 → sin 펄스로 되돌린다(git 히스토리 `7e9ed7eb` 역).
- **겹침 z-오프셋**: `EnsurePlaceableTilemap`(−0.04) / `EnsureRangeTilemap`(−0.05) 의 `localPosition.z`.
  ⚠️ **이 값을 0 으로 되돌리면 바닥과 겹쳐 카메라 이동 중 z-fight(자글거림) 재발**. 건드리지 말 것.
  (원인: 바닥 머티리얼 `TileShadowReceive` 가 depth write → 투명 오버레이가 같은 평면이면 깊이 다툼.)
- **sorting 순서**: ground −20 · effect −15 · placeable −13 · range −12 · hover −10 (드래그 중 range/placeable 는 유닛 위로 상승).

## 주의

- 값 바꾸면 **SaveAssets(Ctrl+S)** 필수. 안 하면 Play 종료 시 되돌아감.
- `rangeColor`/펄스는 `placement-attack-range-preview`(스킬 조준 사거리) 와 **공유** — 바꾸면 그쪽도 바뀐다.
- 색 조합 규칙: 배치=시안, 사거리=라임(2026-07-31 이전엔 주황이라 웜/쿨 대비였다), hover=초록/빨강.
  라임은 hover 초록·시안 슬랩과 이웃한 색이라, 톤을 더 만질 땐 **셋이 서로 안 뭉개지는지**를 기준으로 본다.
