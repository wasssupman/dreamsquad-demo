# 10 — StructureMarker: 본능(Instinct) 거점의 스테이지 저작

## 목적

계약 11 은 스테이지에서 거점(본능·마음)을 비가용으로 뒀다. 그러나 브리지의 거점 스폰 경로
(`SpawnStructureEntities` / `SpawnStructureViews`)는 문서 은퇴 후에도 **통째로 살아 있고** 입력 목록만
`null` 로 막혀 있다. 사용자 요청(2026-08-26, Duel 재저작): «본능은 기존 것을 쓸 수 있는지 확인» →
쓸 수 있다. 스테이지 마커 하나로 입력을 다시 채운다. **마음(Core)은 이 unit 범위 밖** — 적 마음은
공성 모드·유출 판정·시드 스폰 파생까지 끌고 오므로 계약 11 의 «공성 비가용»은 그대로 둔다.

## 변경 대상

- 신규 `Assets/_Project/Scripts/Core/MapStage/StructureMarker.cs` — `side`(StructureSide) · `data`(StructureData). 기즈모 = footprint(본능 3×3) 채움, 방어=파랑/적=빨강, 라벨 `I`
- `MapStageScanner.cs` — 마커 수집 → `StageScan.structures`(`List<StructureEntry>`, 관리 참조 포함 — StageScan 은 이미 관리 클래스)
- `DioramaMapBuilder.cs` — `Validate`: data null 거부 · `kind == Core` 거부(계약 11) · 중심 셀 playArea 안·차단 위 금지 · footprint(3×3) 전체가 playArea 안 · 셀 중복 거부. `Assemble`: `structures = NativeArray<StructurePlacement>` (cell + `DeriveFaction(side, kind)`), (y, x) 사전순
- `BattleBridge.cs` — `_stageStructures : List<StructureEntry>`. 스캔 직후 (y, x) 사전순으로 채우고 `TeardownGeneratedMap` 에서 비운다. `SpawnStructureEntities` / `SpawnStructureViews` 의 `docStructures = null` 자리에 이 목록을 꽂는다 — 그 아래 로직은 한 줄도 안 바꾼다(SO 스탯·OccupiedCells·AttackState 베이크·프리젠터 등록 전부 그대로)
- `MapStageEditors.cs` — `StructureMarkerEditor`(HelpBox + 스냅 버튼)
- 테스트 `DioramaMapBuilderTests` — 본능 1기 조립(faction 파생 확인) · Core 거부 · data null 거부 · footprint 경계 밖 거부 · 중복 거부
- `docs/reference/map-stage-authoring.md` — 구성 요소 표 행 + 제약 8

## 구현

1. 마커는 `BonusSpawnMarker` 와 같은 꼴(위치→셀 양자화) + 필드 2개. 비주얼 자식은 두지 않는다 — 거점 프랍은
   맵 빌드 시 브리지가 `StructureData.viewPrefab` 으로 세운다(battle-structures 후속 2 계약, 맵 수명).
2. footprint 는 `StructurePlacements.FootprintOf(faction)` 로 파생(본능 3, 마음 1) — 별도 크기 필드 없음.
   footprint 는 **점유**(배치 배제 + OccupiedCells)이고 통행 차단이 아니다(instinct-content unit 1) —
   따라서 마커 셀에 `PropFootprint` 를 겹치지 않는다.
3. 빌더는 `GeneratedMap.structures` 에 (cell, faction)만 싣는다(구 `MapDocumentBuilder` 와 동형). 소비처는
   `BattleBridge` 배치 폐쇄(`CloseCellLayers` 3×3)와 진단 로그뿐이며 둘 다 `IsCreated` 가드가 이미 있다.
4. 브리지가 든 관리 목록의 순서 = 빌더 정렬과 동일한 (y, x) 사전순 — 엔티티 생성 순서가 저작 계층 순서에
   의존하지 않게 한다(계약 5 결정론).

## 완료 기준

- [x] compile 0 · EditMode 코어 lane green(신규 5 케이스 포함)
- [x] Duel 스테이지(unit 11)에서 전투 시작 시 `StructureTag` 본능 엔티티 4 + 뷰 4 (PlayMode `DioramaStagePlayTests`)
- [x] `StructureMarker` 에 Core SO 를 물리면 배틀 진입 하드 실패 + 오류 문구에 «계약 11» 명시

체크박스 마감 2026-08-27 — 확인 2026-08-26 — EditMode 6 케이스 · DioramaStagePlayTests 본능 4기 · Structures_Core_Rejected_WithContract11Message.
