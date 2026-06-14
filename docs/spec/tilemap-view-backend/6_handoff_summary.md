# 6 — Handoff Summary (tilemap-view-backend)

## Commit

- `4bd8cff` 0 BoardSpace 변환 헬퍼 + BoardViewMode enum + EditMode 7 테스트
- `371130b` 1a TileSetData SO + TilemapMapView 페인터 + 정합 테스트 3
- `f4bfa8e` 1b placeholder 타일셋 14에셋 (rect square / iso diamond / 마커)
- `cc62a71` 2 BattleBridge 뷰 모드 분기 + 헬스바/backdrop/prop 게이팅
- `f8105ba` 3 프레젠테이션 경계 (위치 ToView / 방향 view공간 / sorting sim보존 / 입력 ToSim / hover facade)
- `6b44972` 4 BoardCameraPreset SO + 2프리셋 + 모드별 ortho 카메라 + 보드<유닛 sorting
- `42311d4` 폴더 meta fix (Data/Camera.meta, Data/TileSets.meta)
- 5 검증 전용(코드 변경 없음) — 본 문서 + 번호 문서 검증줄

## Implemented

- `BoardViewMode {Legacy3D, TilemapRect, TilemapIso}` 1개 인스펙터 값으로 보드 뷰 백엔드 전환.
- `BoardSpace` 정적 헬퍼가 sim(rect XZ 월드) ↔ view 변환의 유일 지점. Legacy3D = identity. 정합 권위는 주입된 Grid.
- `TilemapMapView` write-only 페인터 — `SetTilesBlock` 일괄, goal/spawn overlay 마커, hover/reject/flash, Clear 재진입 안전.
- `TileSetData` SO 로 타일 교체 단위화 (rect/iso placeholder 2종).
- BattleBridge: Tilemap 모드 sim origin=zero, `BoardSpace.Configure` BuildFlowField 직전, 헬스바 Entities Graphics 렌더 게이팅(HealthBarSystem 불변), backdrop/prop Legacy 전용, teardown Clear.
- 프레젠테이션 write 전수 ToView, 방향 view 공간 일원화, sorting 은 `_simWorld` 보존으로 계산, 입력 RaycastPlane+ToSim, hover/reject 는 BattleBridge facade 로 활성 뷰 분기.
- 모드별 ortho 카메라 프리셋(gridSize+aspect→orthoSize, idempotent), TilemapRenderer sortingOrder 음수.

## Key Files

- `Assets/_Project/Scripts/Core/BoardSpace.cs`, `BoardViewMode.cs`, `TilemapMapView.cs`
- `Assets/_Project/Scripts/Data/TileSetData.cs`, `BoardCameraPreset.cs`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (뷰 분기 / 게이팅 / facade / ApplyTilemapCameraPreset)
- `Assets/_Project/Scripts/Presentation/{SpineUnitView,QuadUnitView(+Pool),ProjectileViewPool,DamageNumberView,VfxSpawner}.cs`
- `Assets/_Project/Scripts/Core/PlacementInput.cs`, `Scripts/UI/DefenderDragPlacementController.cs`
- `Assets/_Project/Data/TileSets/`, `Assets/_Project/Data/Camera/`
- 테스트: `Tests/EditMode/{BoardSpaceTests,TilemapMapViewTests}.cs`

## Verified

- EditMode **325 / 323 pass / 0 fail / 2 skipped**(기존 Ignored). 컴파일 0 에러.
- TilemapRect Play(메모리 배선): 보드 200셀 페인트, 유닛 13뷰 `view==ToView(sim)` d=0.000 z=0, 헬스바 `MaterialMeshInfo=0` 게이팅, RebuildDraftMap 2회 잔상0, 카메라 size7.125 centered idempotent.
- TilemapIso Play: 마름모 보드 + isoCellSize 적용 + 유닛 정렬.
- **sim 결정론**: matchSeed=9999, 3모드 `_generatedMap` byte-identical (tilesFNV 동일).
- Legacy3D = BoardSpace identity → 회귀 무변경(사용자 Play 확인 + 구조적 보장).

## Notes

- **모든 Play 검증은 메모리상 `_TilemapBoard` 배선(씬 미저장)** 으로 수행 — 디스크 `BattleScene.unity` 는 무관한 미커밋 변경 827줄로 dirty 라 커밋 오염 방지 위해 손대지 않음.
- `SpineUnitView.cs` 커밋(`f8105ba`)에 세션 이전부터 있던 billboard tilt LateUpdate(+11, 본 스펙 외)가 동반됨 — 사용자 승인 후 rider 로 포함.
- BoardSpace 는 정적 상태 — Configure 가 맵 빌드마다 1회. 모드 전환은 Play 재시작 전제.

## Follow-up

- **영속 씬 저장 (필수 잔여)**: dirty `BattleScene.unity` 정리 후 `_TilemapBoard`(Grid+ground/overlay Tilemap) + `BattleBridge` 필드/프리셋을 씬에 저장 → 빌드/실기기에서 인스펙터 값으로 모드 전환 가능.
- 해저드/장애물 비주얼 Tilemap 정렬, Mono 헬스바 오버레이, RuleTile/시즌 타일, 2D 외곽 연출 — README 후속 후보 참조.
