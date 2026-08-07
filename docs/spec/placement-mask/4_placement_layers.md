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
5. **판정 단일 지점 유지**: 층 교집합 연산은 `GeneratedMap.PlaceableAt` 하나이고, 배치 판정은 전부 `SpatialPlacementCheck` 를 지난다(D&D·탭·재배치·하이라이트 공유). 효과 타일만 `PlaceableAt` 을 직접 부른다(배치 판정이 아니라 셀 선정).
6. **하이라이트는 유닛 종속**: 드는 유닛의 층으로 스캔한다(Ground 유닛을 들면 배치지면이, Path 유닛을 들면 경로가 빛난다). 유닛 미상이면 `Ground` 폴백. **파생 게이트는 유닛과 표시 상태까지 래치해야 한다** — bool 하나만 래치하면 "arm 갈아타기"처럼 유닛만 바뀌는 전이에서 이전 유닛의 층이 남아 판정과 갈린다(리뷰 C-1).
7. **효과 타일은 `Ground` 층 고정** — 경로 위로 번지지 않는다.
8. **스폰·골 칸은 어느 층으로도 배치 불가** — 라이브 맵 빌드 마지막에 층을 닫는 런타임 불변식. 페인터는 이 칸의 마스크가 파생과 다르면 경고만 한다(문서는 오염시키지 않는다).

## 알려진 파급 (리뷰 반영)

- **Path 층의 blast radius = 맵 전체**: 파생이 `Walk→Path` 라, 유닛 SO 하나에 `Path` 를 적는 순간 **모든 맵의 모든 도로 칸**이 그 유닛에게 열린다. 이는 의도된 의미다("경로 유닛은 길에 선다" — 맵마다 저작하는 게 아니다). 특정 도로 칸을 **닫으려면** 페인터로 그 칸의 Path 비트를 지운다(라이브 6종은 authored Deco 라 커빙이 이미 skip 상태이므로, 마스크 저작이 커빙을 추가로 끄는 부작용은 없다).
- **스폰·골 칸은 런타임이 닫는다**(계약 8). 파생만 두면 스폰·골(정의상 Walk 셀)까지 Path 유닛에게 열리는데, 적이 튀어나오는 칸·유출 지점 위 배치는 어느 층 저작에도 없던 의미다. 문서/커빙 의미는 그대로 두고 **라이브 맵에만** 마지막에 덮는다.
- **구 0/1 문서 호환**: units 0~3 시기 Bake 문서(`MapDocument_Test`)의 값 `1` 은 그대로 `Ground` 비트라 **지면 저작은 전부 살아 있다**. 없는 건 Path 비트뿐이다. 따라서 `Mask=파생 리셋` 을 누르지 말 것 — 그 문서의 저작(unit 3 B-1 검증 픽스처)이 통째로 날아간다. 경로 층이 필요하면 그 칸만 Mask 브러시로 덧칠한다.

## 완료 기준

- EditMode: `Derive`/`Sanitize` 순수 함수, `(셀 & 유닛)` 교집합 판정(Ground 유닛×경로 셀 = 거부, Path 유닛×경로 셀 = 허용, All 유닛 = 둘 다, None 유닛 = Ground 폴백), 커빙 intent/재파생의 새 파생 준수, 효과 타일 Ground 고정.
- 기존 EditMode 전량 그린(유닛 SO 무변경 = 무회귀).
- compile 클린. 하이라이트 유닛 종속은 Play 육안(unit 3 육안 축과 함께).
