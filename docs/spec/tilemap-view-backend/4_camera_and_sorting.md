# 4. 모드별 카메라 + 레이어 Sorting

## 목적

Tilemap 모드(XY 평면)에서 보드 전체가 보이는 orthographic 카메라와 레이어 단위 정렬을 구성한다. per-unit 정렬은 unit 3 의 sim 좌표 보존으로 이미 해결 — 본 unit 은 카메라와 "보드 < 유닛" 레이어 규칙만 다룬다. Legacy3D 카메라는 건드리지 않는다.

## 변경 대상

- 신규: `Assets/_Project/Scripts/Data/BoardCameraPreset.cs` (ScriptableObject)
- 신규 에셋: `Assets/_Project/Data/Camera/CameraPreset_TilemapRect.asset`, `CameraPreset_TilemapIso.asset`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 맵 빌드 시 프리셋 적용 (unit 2 분기 지점에 추가)
- `Assets/_Project/Scripts/Core/TilemapMapView.cs` — TilemapRenderer sortingOrder 설정 (필요 시)

## 구현

- `BoardCameraPreset`: `orthographic`, `orthoSizePadding`(보드 크기에서 size 산출용 여유), `position offset`, `rotation`, `transparencySortMode`/`sortAxis`. 하드코딩 금지 — 수치는 전부 에셋.
- 적용: Tilemap 모드에서 `Camera.main` 에 프리셋 적용 + `orthographicSize` 를 `GeneratedMap.gridSize` 와 셀 크기에서 계산. **매 맵 빌드마다 idempotent 재적용** (`RebuildDraftMap` 재진입에도 누적 없음). Legacy3D 는 프리셋 미적용 = 현행 씬 카메라 그대로. 모드 전환은 Play 재시작 전제 (README 계약) — 같은 Play 세션 안에서 Legacy3D ↔ Tilemap 전환으로 카메라 원복이 필요해지면 정지하고 질문.
- sorting:
  - `transparencySortMode = CustomAxis`, sortAxis = Y 를 프리셋에서 카메라에 적용.
  - TilemapRenderer (ground/overlay) 의 sortingOrder 를 유닛/VFX SpriteRenderer 보다 항상 아래로 — "보드 레이어 < 유닛 레이어" 1규칙. 기존 `BoardSortOrder` 규칙/코드는 무변경 (per-unit 정렬은 unit 3 에서 sim 좌표 기반으로 동작). 대규모 sorting 재설계는 범위 밖 (board-visualization V-010 회귀 금지).
- Spine(SkeletonAnimation) 은 2D 렌더러라 XY 평면에서 그대로 동작 — 회전 보정이 필요한 경우만 `ToView` 적용 지점에서 함께 처리.

## 완료 기준

> ✅ 검증 2026-06-14 — `Data/BoardCameraPreset.cs` SO + Rect/Iso 프리셋 2에셋(`Data/Camera/`),
> `BattleBridge.ApplyTilemapCameraPreset`(gridSize+aspect→orthographicSize, 보드중심 프레이밍, 맵빌드마다 idempotent),
> `TilemapMapView` ground/overlay sortingOrder −20/−10(유닛 BoardSortOrder 양수 아래). compile 0.
> **TilemapRect Play(메모리 배선)**: Camera.main ortho size=7.125 centered(10,5,−20) sortMode=CustomAxis, 보드 전체
> 프레이밍 + 유닛 타일 위 렌더(스크린샷), RebuildDraftMap 2회 카메라 동일(idempotent), 에러 0. 커밋: 6b44972.

- Unity compile 0 errors.
- `Legacy3D` Play smoke: 카메라/sorting 무변화 (회귀 기준).
- `TilemapRect` Play: 보드 전체가 화면에 들어오고, 유닛이 타일 위에 그려지며(아래 깔리지 않음), 유닛 간 상하 겹침이 행 기준 자연 정렬 (unit 3 산출 확인 포함). 스크린샷 1장 확보.
- `RebuildDraftMap` 2회 후에도 카메라 상태 동일 (idempotent 확인).
- 콘솔 error/warning 0.
