# 2 — BattleBridge 빌드 경로 교체 + MapStagePool

## 목적

`BuildMapForBattle` 의 문서 경로(MapDocumentPool → MapGridBattleAdapter)를 스테이지 경로(MapStagePool → 프리팹 인스턴스화 → DioramaMapBuilder)로 교체한다. **이후 단계(필드 설치·BoardSpace·카메라·placeMask 닫기)는 무변경.**

## 변경 대상

- 신규 `Assets/_Project/Scripts/Data/MapStage/MapStagePool.cs` — SO: `entries[] = (MapStage 프리팹, AttackDeck, WavePlanAsset)` + `devEntries[]` (기존 `MapDocumentPool` 구조 승계 — 인덱스 선정 의미 유지)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `BuildMapForBattle` 의 맵 소스 구간 (~L989–1290 중 문서 해석부)
- 참조(무수정 확인): `MapPoolSelect.cs` · `DevMapOverride.cs` — 인덱스 선정 로직이 풀 타입에 결합돼 있으면 최소 수정
- 신규 픽스처: 테스트/개발용 미니 스테이지 프리팹 1개 (`Assets/_Project/Prefabs/Maps/MapStage_Fixture.prefab` — 정식 파일럿 맵은 unit 5)

## 구현

1. 풀에서 entry 선정 — 기존 우선순위 그대로: DevMapOverride > fixedMapSeed > 토너먼트 시드 > 0.
2. 스테이지 프리팹 인스턴스화 (씬 루트, teardown 시 파괴 — `TeardownGeneratedMap` 짝에 스테이지 인스턴스 파괴 추가). **인스턴스가 곧 비주얼이다.**
3. `DioramaMapBuilder` 스캔+조립 → 형식 오류 시 기존 `MapGenerationFailedException` 경로와 동형으로 하드 실패.
4. `MapConnectivity.AllSpawnsReachGoal` → 실패 시 기존 `BuildFallbackLinear` 안전망 유지 (README 계약 9). 폴백 시 스테이지 인스턴스는 파괴(비주얼-논리 불일치 방지).
5. 이후 무변경: `CloseCellLayers` → `TilemapMapView.Initialize`(**이 unit 에서는 존치** — 합성 tiles 를 그대로 페인팅하는 scaffolding, 시각은 어긋나도 기능 검증용. 은퇴는 unit 3) → `BoardSpace.Configure` → 카메라 → `BuildFlowField`.
6. **스테이지-격자 정렬**: `Grid` 트랜스폼을 스테이지의 `gridOriginLocal` 에 맞춰 배치 — 기존 `CenterBoardAtWorldOrigin` 의 후계. 스테이지 인스턴스를 원점 기준으로 세우고 격자를 그에 맞추는 쪽이 단순(뷰 오프셋은 grid transform 에만, sim origin 은 `float3.zero` 유지 — README 계약 7).
7. 골 HP 는 기존대로 `AttackDeck.goalStabilityMax`(덱 소유) — 마커는 셀만 준다. 이중 저작 없음(2026-08-18 확인: 덱에 골 셀 필드 없음).

**laneCount 결정론 주의**: 웨이브 브리핑과 런타임이 같은 laneCount 를 받는 기존 계약(`docs/reference/map-wave-balancing.md`)이 스테이지 경로에서도 성립해야 한다 — 스폰 수 산출을 빌더 결과 한 곳에서만 읽는다.

## 완료 기준

- [ ] compile + 기존 EditMode 스위트 무회귀 (문서 경로 테스트는 이 unit 에서 삭제/치환 대상 명시)
- [ ] 픽스처 스테이지로 에디터 Play: 적이 스폰 → 열린 마당 횡단 → 골 도달, 방어 유닛 열린 셀 배치 성공
- [ ] DevMapOverride 스테퍼로 entry 전환 동작
- [ ] 풀 선정 결정론 EditMode 테스트 (같은 시드 = 같은 entry)
