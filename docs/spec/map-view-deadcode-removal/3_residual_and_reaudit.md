# 3. 잔여 사장 심볼 정리 + 재감사

## 목적

앞선 세 단위가 지나간 뒤 남는 소품을 치우고, **삭제로 인해 새로 orphan 이 된 것이 없는지 재감사**한다. 사장 코드 정리는 연쇄한다 — A 를 지우면 A 만 쓰던 B 가 새로 죽는다. 한 번 훑고 끝내면 반쯤 정리된 상태가 남는다.

## 변경 대상

- `Assets/_Project/Scripts/Data/MapGrid/MapDocument.cs` — `goal`(단수) SerializeField(`:20`) + `Goal` 프로퍼티의 폴백 분기(`:44`)
- `Assets/_Project/Editor/MapPainterWindow.cs` — 레거시 단일골 로드 폴백(`:112-113`)
- `Assets/_Project/Data/TileSets/PH_Rect_Env.asset` (+ `.meta`) — **삭제** (참조 0)
- 재감사 결과에 따라 추가 (사전에 특정하지 않는다)

## 구현

**단수 `goal` 폴백 제거 — 조용한 폴백을 시끄러운 계약으로 바꾼다.**

맵 9장 전부 `goals` 배열을 채우고 있고, `BattleMapBuilder.BuildFallbackLinear` 도 `goals` 를 명시 세팅한다(`:49-51`). 따라서 `goals` 가 빈 경우의 폴백 분기는 전부 도달 불가다.

단순 삭제 대신 **`OnValidate` 에 "goals 는 1개 이상" 에러를 추가**하고 `Goal => goals[0]` 로 만든다. 폴백을 지우면서 검증을 안 넣으면 "빈 goals 로 저장된 문서"가 조용히 인덱스 예외로 바뀐다 — `MapDocument` 가 이미 다른 축(`StructureAuthoringRules`/`WaypointAuthoringRules`)에서 쓰는 loud-fail 방식과 같은 형태로 맞춘다.

`GeneratedMap.goal`(int2)은 **건드리지 않는다.** 이름이 같지만 별개이며, 소비처가 여럿인 살아있는 필드다. `TilemapMapView.PaintMarkers` / `InstantiateStructureProps` 의 `map.goals` 빈 경우 `else` 가지도 지금은 도달 불가지만, `GeneratedMap` 은 페인터 밖에서도 조립되므로 **이 spec 범위에서는 남긴다**.

**재감사.**

unit 0~3 삭제 후 다음을 다시 센다. 참조 0 이 새로 나오면 이 단위에서 함께 정리하거나, 판단이 필요하면 README 후속 후보로 이관한다.

- `Data/TileSets/` 의 모든 `.asset` — guid 로 전 프로젝트 역참조
- `Data/Camera/`, `Data/Maps/` 동일
- `TileSetData` 의 각 `TileBase` 슬롯 — 읽는 코드가 있는가
- `Core/`, `Data/MapGrid/` 의 public 심볼 중 참조 0

검색 범위는 `Assets/_Project` 이며 `il2cppOutput` 백업은 제외한다.

## 완료 기준

> ✅ 검증 2026-08-12 — compile 0 errors, EditMode **2193 중 실패 0**.
> **guard 유효성**: 임시 테스트 2개로 확인 후 제거 — ① `OnValidate` 가 빈 goals 에 지정 문구로
> 에러를 뱉는다 ② `ToGeneratedMap` 이 `MapGenerationFailedException` 으로 명확히 실패한다.
> 둘 다 통과(2195 중 실패 0).
> Play(BattleScene) 정상, 콘솔 error/warning 0. 맵 9장·`BattleScene.unity` 무변경.
>
> **재감사 수확 — 삭제 연쇄 확인** (`Data/TileSets`·`Data/Camera` 전 에셋 guid 역참조):
> `PH_Rect_Walk` / `PH_Rect_Place` / `PH_Rect_Deco` **3건이 참조 0** 으로 새로 드러나 함께 제거.
> tilemap-view-backend 시절 placeholder 로, 현행 `TileSet_Desert` 는 전부 다른 타일
> (`Tile_Sand`·`AutoTile_*` 등)을 쓴다. 나머지 에셋은 전부 참조 ≥1 로 확인.
>
> ⚠ **함정 기록**: `MapDocument.cs` 에 `goal` 참조가 한 곳 남아 `Wassup.Runtime` 이 컴파일에
> 실패했는데, **테스트는 stale 어셈블리로 계속 green 을 냈다.** 콘솔에도 안 잡혔다(반복 refresh 가
> 소거). `editor_state.compilation.last_domain_reload_after` 가 `last_compile_finished` 보다
> 과거면 그 테스트 결과는 **믿으면 안 된다**. 진단은 `Editor.log` 의 `error CS` grep 이 유일하게
> 확실했다. 이 방식을 다음 세션이 재사용할 것.

- Unity compile 0 errors, EditMode 전체 green.
- `goals` 를 비운 `MapDocument` 를 만들면 `OnValidate` 가 **에러를 뱉는다** (수동 1회 확인 후 되돌림).
- Map Painter Load → Bake → Load 왕복에서 goals 가 보존된다.
- Play 1판 정상, 콘솔 신규 error/warning 0.
- **재감사 결과를 `4_handoff_summary.md` 에 표로 남긴다** — 무엇을 셌고, 무엇이 남았고, 무엇을 후속으로 미뤘는지. 다음 작업자가 같은 조사를 반복하지 않게 하는 것이 이 단위의 절반이다.
- `.meta` 짝 삭제 확인.
