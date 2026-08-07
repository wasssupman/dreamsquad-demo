# 0. traverseMask 데이터 레이어 (행동 변화 0)

## 목적

셀의 **통행 층**과 적의 **통행 층**을 데이터로 만든다. 이 유닛은 길찾기를 바꾸지 않는다 — 값이 흐르고 폴백이 서는 것까지. 파생 기본값이 현행(`tiles==Walk`)을 정확히 재현하므로 판은 그대로다.

## 변경 대상

- `Assets/_Project/Scripts/Data/TraversalLayer.cs` (신규) — `TraversalLayer` [Flags] enum + `TraversalLayers.Derive/Sanitize`
- `Assets/_Project/Scripts/Data/GeneratedMap.cs` — `traverseMask` + `TraverseLayersAt`
- `Assets/_Project/Scripts/Data/MapGrid/MapDocument.cs` · `MapDocumentBuilder.cs` · `BattleMapBuilder.cs` — 직렬화·왕복·파생(placeMask 와 동형)
- `Assets/_Project/Scripts/Data/AttackUnitData.cs` — `traversalLayers` + `EffectiveTraversalLayers`
- `Assets/_Project/Tests/EditMode/` — 파생·왕복·폴백·교집합

## 구현

1. `TraversalLayer` : `None=0`, `Ground=1<<0`(지상), `All=0xFF`(적 전용 표현). `TraversalLayers.Derive(MapTileType)` = `Walk→Ground`, 그 외 0. `Sanitize` = 정의된 비트만. **placement 의 `PlacementLayers` 를 재사용하지 않는다**(계약 1 — 축이 다르다).
2. `GeneratedMap.traverseMask` + `TraverseLayersAt(cell)`(미생성 시 `Derive(tiles[cell])` 폴백). `Dispose` 추가. `IsCreated` 불변식에는 넣지 않는다(픽스처 보호 — placeMask 선례).
3. `MapDocument.traverseMask` 직렬화 + `OnValidate` 는 `goalMaxStability` 패턴(length-0 = 부재 = 유효). 빌더 양방향 + 0/1 아닌 값 Sanitize. 빌더 산출물 불변식: 항상 생성.
4. `AttackUnitData.traversalLayers`(기본 `Ground`) + `EffectiveTraversalLayers`(`None → Ground` 폴백). 코드가 적 종류/enum 을 분기하지 않는다.
5. **아직 소비하지 않는다** — `BuildFlowField` 의 `walk[i] = tiles[i]==Walk` 는 unit 1 에서 교체한다. 이 유닛에서 바꾸면 행동 변화 0 이 깨진다.

## 완료 기준

- compile 클린, 기존 EditMode 전량 그린(행동 변화 0).
- EditMode 신규: `Derive` 매핑 명시 고정(`Walk→Ground`, Place/Deco/Env→0), `Sanitize` 미정의 비트 제거, 왕복 보존, 부재/길이 불일치 파생 폴백, `TraverseLayersAt` 폴백, 적 SO `None→Ground` 폴백, `(셀 & 적)` 교집합 진리표.
- 기존 맵 6종·기존 적 SO 무변경으로 판 동일(`walkMask` 산출이 이 유닛에서 그대로임을 테스트로 고정).
