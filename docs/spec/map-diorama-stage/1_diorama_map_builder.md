# 1 — DioramaMapBuilder: 프랍 스캔 → 셀 양자화 → GeneratedMap 조립

## 목적

스테이지 프리팹의 저작 컴포넌트들을 읽어 `GeneratedMap` 을 조립한다. 계산 전부를 **plain 값 입출력 순수 static** 으로 두고(README 계약 8), Mono 는 컴포넌트 → plain 구조체 변환만 한다.

## 변경 대상

- 신규 `Assets/_Project/Scripts/Data/MapStage/DioramaMapBuilder.cs` — 순수 코어 + 얇은 스캔 진입점
- 신규 `Assets/_Project/Tests/EditMode/DioramaMapBuilderTests.cs`
- 참조(무수정): `GeneratedMap.cs` · `MapConnectivity.cs` · `PlacementLayer.cs`

## 구현

**스캔 (Mono 레이어, 얇게)**: `MapStage.GetComponentsInChildren<T>()` 로 수집 → 각 프랍의 스테이지-로컬 위치를 plain 입력으로 변환. 월드가 아니라 **스테이지 로컬 기준** — 스테이지 프리팹이 씬 어디에 있든 같은 맵이 나온다.

**양자화 (순수)**: `cell = floor((localPos - gridOriginLocal) / tileSize)`. footprint 차지 셀 = 앵커 셀 + offset 부터 size 만큼. playArea 밖 셀은 무시(부분 걸침은 안쪽만 반영).

**조립 (순수)** — 입력: gridSize, footprint rect 목록, blockZone rect 목록, 스폰(cell+laneIndex) 목록, 골 cell 목록, 루트 체인 목록. 출력: `GeneratedMap` 의 각 배열.

1. `blocked[]` = footprint OR (순서 무관 — README 계약 5)
2. `tiles[]` 합성: `!blocked → Walk`, `blocked → Deco` (README 계약 2)
3. `placeMask[]`: 열린 셀 = `Ground|Path|Air`, 차단 셀 = 0, BlockZone 차감 (README 계약 3). 스폰/골 셀 닫기는 여기서 하지 않는다 — 기존 `BattleBridge.CloseCellLayers` 런타임 담당(무수정).
4. `spawns[]` = laneIndex 오름차순 정렬 (중복/공백 인덱스는 실패). `goals[]` = 저작 순서가 아니라 셀 사전순 정렬(결정론).
5. `waypointCells/waypointRanges/spawnRoutes` = 루트 체인을 기존 flatten 형식으로 (`MapDocumentBuilder.ToGeneratedMap` 의 출력 형식을 그대로 재현 — 그 코드를 참조하되 복사 뒤 문서화).
6. `structures` = 빈 배열 (StructureMarker 는 범위 밖 — README 후속 후보).
7. `seed`/`generatorVersion` = 수동 저작 규약값 (`-1`/`0` 관례 승계).

**검증은 조립기 밖**: 연결성은 기존 `MapConnectivity.AllSpawnsReachGoal` 을 호출자가(unit 2 / 에디터 린트) 돌린다. 조립기는 형식 오류(스폰 0개, laneIndex 중복, 골이 차단 셀 위, playArea 밖 마커)만 실패로 보고한다 — 실패 형식은 예외가 아니라 결과 구조체(에디터 린트가 목록으로 소비).

## 완료 기준

- [ ] EditMode 테스트 그린: 양자화 경계(셀 경계선 위 프랍·음수 로컬 좌표) · footprint 부분 걸침 · blocked OR 순서 비의존 · placeMask 조립 · laneIndex 정렬/중복 검출 · 루트 flatten 왕복 · 골 차단 셀 검출
- [ ] 조립 결과가 `MapConnectivity` 를 통과하는 픽스처 1개 + 통과 못 하는 픽스처 1개 (음성 대조군)
- [ ] `GeneratedMap` 필드 중 미기입이 없는지 전수 대조 (필드 추가 회귀 방지 — 테스트에 명시)
