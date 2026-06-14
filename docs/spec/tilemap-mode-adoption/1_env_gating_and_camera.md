# 1. 환경 게이팅 + 카메라 보드 프레이밍

## 목적

Tilemap 모드에서 ① 카메라가 보드 실측 bounds 에 맞게 프레이밍 + 하늘(skybox) 제거(solid 배경), ② Legacy 3D 환경 오브젝트를 비활성(목록 기반)해 보드가 단독으로 깔끔히 보이게 한다. Legacy3D 진입 시 완전 원복.

**범위 주의**: 카메라 부분은 씬 독립 — 지금 코드화. 환경 숨김은 **SerializeField 목록 scaffold 만** 만들고 실제 대상 배선은 dirty `BattleScene.unity` 정리(unit 2 선행) 후. 빈 목록이면 no-op.

## 변경 대상

- `Assets/_Project/Scripts/Data/BoardCameraPreset.cs` — `solidColorBackground` + `backgroundColor` 추가.
- `Assets/_Project/Scripts/Core/TilemapMapView.cs` — `TryGetBoardWorldBounds(out Bounds)` (ground 타일맵 렌더 bounds).
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `ApplyTilemapCameraPreset` 를 bounds 기반 프레이밍 + clearFlags 로 개선, `tilemapHiddenEnvironment` SerializeField + `ApplyEnvironmentGating(bool)` 호출.
- 에셋: 기존 `CameraPreset_TilemapRect/Iso.asset` 새 필드 값 세팅.

## 구현

- `BoardCameraPreset`: `bool solidColorBackground = true`, `Color backgroundColor = (0.09,0.10,0.13,1)`.
- `TilemapMapView.TryGetBoardWorldBounds(out Bounds b)`: groundTilemap `TilemapRenderer.bounds` (페인트된 셀 영역) 반환. 비었으면 false.
- `ApplyTilemapCameraPreset` 개선: 보드 bounds 있으면 `orthographicSize = max(extents.y, extents.x/aspect)*패딩계수`, center = bounds.center (iso 마름모도 정확히 맞춤). 없으면 기존 gridSize 추정 폴백. `solidColorBackground` 면 `clearFlags=SolidColor`+`backgroundColor`. idempotent 유지.
- `ApplyEnvironmentGating(bool tilemap)`: `tilemapHiddenEnvironment[]` 의 각 GameObject `SetActive(!tilemap)`. 맵 빌드에서 호출(tilemap→숨김, Legacy→복원). 빈 배열 = no-op.
- 하드코딩 금지: 패딩계수/배경색/숨김목록 전부 에셋·SerializeField.

## 완료 기준

> ✅ 검증 2026-06-14 — `BoardCameraPreset` 배경 필드, `TilemapMapView.TryGetBoardWorldBounds`, `ApplyTilemapCameraPreset`
> bounds 기반 프레이밍 + clearFlags=Solid, `ApplyEnvironmentGating`(목록 SetActive). compile 0, EditMode **325/323
> pass**(도메인 리로드 후, 회귀 0). TilemapIso Play(메모리 배선): **빌드 코드만으로**(수동 조작 0) clearFlags=Solid +
> orthographicSize=6.22 bounds-fit(마름모 정확) + 환경 4개 게이팅 → 깔끔한 iso 보드 스크린샷. 커밋: 4a3fc91.
> 주의: `tilemapHiddenEnvironment` 는 빈 배열로 커밋(no-op). 실제 환경 대상 배선은 unit 2(dirty 씬 정리) 후.
> ※ 검증 메모: 다회 Play+execute_code 후 EditMode 풀스위트가 edit-mode Destroy 로그로 6건 실패하는 잔류 오염 발생 →
> `EditorUtility.RequestScriptReload()` 도메인 리로드로 해소(회귀 아님).

- Unity compile 0 errors. 전체 EditMode green(회귀 0).
- Legacy3D Play: 카메라/환경 본 spec 이전과 동일(ApplyTilemapCameraPreset/Gating 미호출, skybox 유지).
- TilemapIso/Rect Play(메모리 배선): 카메라가 보드 bounds 에 꽉 맞고(마름모 포함) skybox 제거됨. 스크린샷 1장.
- `tilemapHiddenEnvironment` 빈 상태에서 no-op(에러 없음). 실제 환경 배선은 unit 2(씬 정리) 후.
