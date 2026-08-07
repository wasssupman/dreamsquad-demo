# 4. 배치 층 비트필드 (셀 마스크 × 유닛 레이어)

rev 2026-08-07 — units 0~3 의 0/1 마스크를 **층 비트필드**로 확장 (사용자 결정).

## 목적

"이 유닛은 어떤 종류의 칸에 설 수 있나"를 데이터로 만든다. 셀은 자기가 여는 **층 비트**를 갖고, 유닛은 자기가 설 수 있는 **층 비트**를 갖는다. 판정은 교집합 하나다:

```
배치 가능  ⇔  (셀 층 비트 & 유닛 층 비트) != 0
```

**구현은 클래스에 종속되지 않는다** — 코드는 `DefenderClass`/role 을 일절 보지 않는다. "레인저는 배치지면, 가디언은 경로" 같은 배정은 각 유닛 SO 에 비트를 적는 **저작 선택**일 뿐이고, 런타임은 비트만 본다.

## 변경 대상

- `Assets/_Project/Scripts/Data/PlacementLayer.cs` (신규) — `PlacementLayer` [Flags] enum + `PlacementLayers` 파생/정규화 순수 함수
- `Assets/_Project/Scripts/Data/GeneratedMap.cs` — `LayersAt` / `PlaceableAt(cell, layers)`
- `Assets/_Project/Scripts/Data/DefenderUnitData.cs` — `placementLayers` 필드 + `EffectivePlacementLayers`
- `Assets/_Project/Scripts/Data/MapGrid/MapDocumentBuilder.cs` · `BattleMapBuilder.cs` · `ObstaclePlacer.cs` — 파생/정규화를 단일 함수로 교체
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (+`.Relocation.cs`) — 판정 4번째 인자, 하이라이트 유닛 종속
- `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs` · `DefenderRelocationController.cs` — 하이라이트 호출에 유닛 전달
- `Assets/_Project/Scripts/Data/EffectTilePlacer.cs` — Ground 층 고정

## 계약

1. **층 정의**: `None=0`, `Ground=1<<0`(배치지면), `Path=1<<1`(경로), `All=0xFF`(유닛 전용 — 셀이 여는 어느 층이든). 층 이름은 **공간** 기준이지 유닛 클래스 기준이 아니다.
2. **비트 ↔ 타일 종류 파생은 단일 정의**(`PlacementLayers.Derive`): `Place→Ground`, `Walk→Path`, `Deco/Env→None`. 빌더·커빙 재파생·폴백·페인터가 전부 이 함수를 쓴다.
3. **셀 비트는 정의된 층만**(`Sanitize` = `& (Ground|Path)`). 미정의 비트는 저장/로드 시 떨어진다 — `All` 유닛이 의미 없는 비트로 배치되는 걸 막는다.
4. **유닛 기본값 = 미지정 폴백**: SO 필드가 `None`(기존 asset 의 역직렬화 기본값)이면 `Ground` 로 읽는다. 즉 **SO 를 안 건드리면 units 0~3 과 완전히 동일**하게 동작한다(옵트인).
5. **판정 단일 지점 유지**: 층 교집합은 `SpatialPlacementCheck` 안에서만 계산한다. D&D·탭·재배치·하이라이트가 그 술어를 공유한다.
6. **하이라이트는 유닛 종속**: 드는 유닛의 층으로 스캔한다(Ground 유닛을 들면 배치지면이, Path 유닛을 들면 경로가 빛난다). 유닛 미상이면 `Ground` 폴백.
7. **효과 타일은 `Ground` 층 고정** — 경로 위로 번지지 않는다.

## 알려진 파급

- 파생이 바뀌어(Walk→Path 비트) **`Walk` 셀의 마스크 값이 0 → 2 로 변한다**. 라이브 맵 6종은 `placeMask` 필드 자체가 없어 항상 파생 경로라 무영향. units 0~3 시기에 Bake 한 문서(`MapDocument_Test`)는 Walk 비트가 0 으로 굳어 있어 Path 유닛이 못 선다 — 페인터의 `Mask=파생 리셋` 후 재저작하면 된다.

## 완료 기준

- EditMode: `Derive`/`Sanitize` 순수 함수, `(셀 & 유닛)` 교집합 판정(Ground 유닛×경로 셀 = 거부, Path 유닛×경로 셀 = 허용, All 유닛 = 둘 다, None 유닛 = Ground 폴백), 커빙 intent/재파생의 새 파생 준수, 효과 타일 Ground 고정.
- 기존 EditMode 전량 그린(유닛 SO 무변경 = 무회귀).
- compile 클린. 하이라이트 유닛 종속은 Play 육안(unit 3 육안 축과 함께).
