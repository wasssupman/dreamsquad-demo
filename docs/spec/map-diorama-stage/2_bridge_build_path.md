# 2 — BattleBridge 빌드 경로 교체 + MapStagePool

## 목적

`BuildMapForBattle` 의 문서 경로(MapDocumentPool → MapGridBattleAdapter)를 스테이지 경로(MapStagePool → 프리팹 인스턴스화 → DioramaMapBuilder)로 교체한다. **이후 단계(필드 설치·BoardSpace·카메라·placeMask 닫기)는 무변경.**

## 변경 대상

- 신규 `Assets/_Project/Scripts/Data/MapStage/MapStagePool.cs` — SO: `entries[] = (MapStage 프리팹, AttackDeck, WavePlanAsset)` + `devEntries[]` (기존 `MapDocumentPool` 구조 승계 — 인덱스 선정 의미 유지. `WarnOnSiegeCoreHpMismatch` 는 미승계 — 계약 11)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 맵 소스 구간 전수 (critic M-3·M-10): `BuildMapForBattle` 문서 해석부(~L989–1290) + **커빙 블록 삭제**(:1091–1108 `hasAuthoredDeco`/`hasAuthoredMaskIntent`/`DesignateDeco`/`RederivePlaceMask` — 발동 시 placeMask 를 `Derive` 로 되써 Ground 유닛 전원 배치 불가) + `mapPool` 필드(:35)와 **`BuildMapForBattle` 밖 게이트 3곳**(:1288, :1357, :1738)
- `Assets/_Project/Scripts/UI/DevMapOverridePanel.cs` — `MapDocumentPool` SerializeField 타입 결합 → 스테이지 풀로 교체 (critic M-10)
- 씬 재배선 2개 (critic M-10 · «씬 wiring 수작업 금지» 계약): `BattleScene.unity` mapPool 참조 + `OutgameScene.unity:593` DevMapOverridePanel
- `MapPoolSelect.cs` — 무수정 확인 완료(critic): `SelectIndex(seed, count)` 는 풀 타입 비결합
- 신규 픽스처: 테스트/개발용 미니 스테이지 프리팹 1개 (`Assets/_Project/Prefabs/Maps/MapStage_Fixture.prefab` — KayKit 최소 조립: 바닥판 + 스폰 2 + 골 1. 정식 파일럿 맵은 unit 5)
- `MapStage` 인스펙터에 **"Dev 엔트리 등록" 버튼** — 스테이지를 풀 `devEntries` 에 기본 덱/플랜과 짝지어 등록 (MapPainter 의 dev 슬롯 자동 등록 선례 승계). 지향점 «스크립트 붙이면 그 자체로 게임 진행 가능»의 마지막 마일 — 풀 수동 편집 없이 바로 Play

## 구현

1. 풀에서 entry 선정 — 기존 우선순위 그대로: DevMapOverride > fixedMapSeed > 토너먼트 시드 > 0.
2. 스테이지 프리팹 인스턴스화. **인스턴스가 곧 비주얼이다.** 파괴는 **맵 teardown 5경로 전수**에 편입 (critic 지적 — `BattleBridge.cs:976` 주석: 매치 종료·재빌드 선행·fallback 교체·StopBattle·draft 정리): 전부 `TeardownGeneratedMap` 을 지나는지 확인하고, 지나지 않는 경로가 있으면 그 경로에도 스테이지 파괴를 명시. 누락 = 판마다 스테이지 누적.
3. `DioramaMapBuilder` 스캔+조립 → 형식 오류 시 `MapGenerationFailedException` 동형 하드 실패.
4. `MapConnectivity.AllSpawnsReachGoal` 실패도 **하드 실패** (README 계약 9 개정 — critic M-1: unit 3 이후 폴백 맵은 렌더러가 없다). `BuildFallbackLinear` 호출부는 이 unit 에서 제거.
5. **`CenterBoardAtWorldOrigin` 제거 + 스테이지-격자 정렬이 유일한 `grid.transform` writer 가 된다** (critic C-1): `Grid` 트랜스폼을 스테이지 인스턴스의 `gridOriginLocal` 에 맞춰 배치. `TilemapMapView.Initialize` 는 존치하되(합성 tiles 페인팅 scaffolding — 은퇴는 unit 3) **내부의 `CenterBoardAtWorldOrigin` 호출은 이 unit 에서 삭제** — «Initialize 무변경»이 아님에 주의. sim origin 은 `float3.zero` 유지(README 계약 7).
6. **배경/링/구조물 프랍 인스턴스화 3종은 이 unit 에서 즉시 차단** (critic M-8): 합성 tiles 에서 `Deco→Env zone` 이라 `BackgroundPropPlacer` 가 아티스트 프랍 위에 절차 프랍을 범람시킨다. 골 구조물 프랍만 unit 4 이관까지 유지.
7. 이후 무변경: `CloseCellLayers` → `BoardSpace.Configure` → 카메라 → `BuildFlowField`.
8. 골 HP 는 기존대로 `AttackDeck.goalStabilityMax`(덱 소유) — 마커는 셀만 준다. 이중 저작 없음(2026-08-18 확인: 덱에 골 셀 필드 없음).

**laneCount 결정론 주의**: 웨이브 브리핑과 런타임이 같은 laneCount 를 받는 기존 계약(`docs/reference/map-wave-balancing.md`)이 스테이지 경로에서도 성립해야 한다 — 스폰 수 산출을 빌더 결과 한 곳에서만 읽는다.

## 완료 기준

- [ ] compile + EditMode **두 lane** (`Wassup.Tests.EditMode` + `Wassup.Tests.EditMode.Assets`) 무회귀 — 맵/에셋 spec 은 Assets lane 필수 (critic M-4). 구 문서 경로 테스트의 처분은 unit 7 소관이므로 이 unit 에서는 컴파일 유지가 기준
- [ ] **PlayMode 스모크 신설** (critic M-12 — unit 5 에서 이동): `Assets/_Project/Tests/PlayMode/DioramaStagePlayTests.cs`, 픽스처 스테이지 기준 — ① 적 스폰 후 **셀이 N프레임 안에 바뀐다** → 골 도달 이벤트 ② 열린 셀 배치 성공 + 차단 셀 거부 + BlockZone 거부 ③ 차단 footprint 통과 0
- [ ] 픽스처 스테이지로 에디터 Play: 위 스모크와 동일 시나리오 육안 + **프랍-격자 정렬 확인**(gridOriginLocal≠0 인 픽스처로 — C-1 회귀 축)
- [ ] DevMapOverride 스테퍼로 entry 전환 + "Dev 엔트리 등록" 버튼 동작
- [ ] 풀 선정 결정론 EditMode 테스트 (같은 시드 = 같은 entry)
