# 8. 맵 크기에 맞춘 홈 포즈 (board fit)

## 목적

맵 풀의 맵마다 크기가 다른데(12×10 ~ 20×12) 카메라는 씬에 고정 배치돼 있어, 작은 맵은 여백이 남고 큰 맵은 가장자리가 잘릴 수 있다. **맵 빌드 시점에 보드 전체가 화면에 들어오도록 홈 포즈의 거리를 자동 계산**한다.

## 변경 대상

- `Assets/_Project/Scripts/Presentation/CameraFramingMath.cs` — **신규**. fit 거리 순수 함수
- `Assets/_Project/Scripts/Presentation/CameraDirector.cs` — `FrameBoard(Bounds)` 공개 API
- `Assets/_Project/Scripts/Data/CameraDirectionConfig.cs` — `boardFitMargin`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `BuildMapForBattle` 에서 호출
- `Assets/_Project/Tests/EditMode/CameraFramingMathTests.cs` — **신규**

## 구현

**왜 홈 포즈인가**: `CameraPhasePose`(페이즈 델타)·드래그 포커스·킥·브리딩이 전부 **홈 기준 델타**로 얹힌다(`CameraComposeMath.Compose`). 홈만 갈아끼우면 나머지 연출은 그대로 따라온다. 반대로 카메라 transform 을 직접 쓰면 다음 `LateUpdate` 에 홈으로 되돌려져 무효다 — 그래서 옛 `ApplyTilemapCameraPreset` 이 은퇴했다.

**fit 산식** (`CameraFramingMath.FitDistance`): 회전은 유지하고 거리만 구한다. 보드 코너를 카메라 회전의 역으로 돌려 `local = R⁻¹(corner − center)` 를 얻으면, 카메라를 `center − forward·t` 에 둘 때 그 코너의 view 공간 z 는 `local.z + t` 가 된다. 프러스텀 안에 들어올 조건은

```
|local.x| ≤ (local.z + t)·tanH      |local.y| ≤ (local.z + t)·tanV
```

이므로 코너마다 `t ≥ |x|/tanH − z`, `t ≥ |y|/tanV − z` 이고, 전체 최댓값이 답이다. 바운딩 **구** 근사(`radius / sin(fov/2)`, 옛 프리셋 방식)와 달리 pitch 로 납작해진 보드에서 과한 여백이 생기지 않는다.

`margin` 은 결과 거리에 곱한다(1 = 딱 맞음). 하드코딩 금지라 `CameraDirectionConfig.boardFitMargin` 에서 온다.

**적용**: `BuildMapForBattle` 이 `BoardSpace.Configure` 직후, 그리드가 확정된 시점에 `EnsureCameraDirector()?.FrameBoard(bounds)` 를 부른다. 보드 중심이 화면 중앙에 오고 회전·FOV 는 씬 값 그대로다.

⚠ **bounds 는 플레이 그리드여야 한다.** `TryGetBoardWorldBounds` 는 ground 타일맵 **렌더러 실측**이라 주변 데코 지대(나무 숲)까지 포함한다 — 15×12 맵이 35×32 로 잡혀 카메라가 거리 54 까지 물러나고 플레이 영역이 화면 중앙의 작은 조각이 됐다. 그래서 `TilemapMapView.TryGetPlayfieldWorldBounds(gridSize)` 를 신설해 grid 셀 좌표(0..gridSize)로 플레이 범위만 만든다. iso 마름모는 대각선 양 끝만으로 좌우 극단을 놓치므로 4코너를 모두 감싼다.

**경계**:
- view 나 director 가 없으면(headless EditMode) 조용히 skip — 기존 view-init skip 계약과 같다.
- 홈이 바뀌어도 진행 중 연출 가중치는 건드리지 않는다. 맵 빌드는 Draft 진입 시점이라 연출이 없다.

## 완료 기준

- [x] 컴파일 · EditMode 통과 (2026-07-24: 1273개 실패 0, 신규 `CameraFramingMathTests` 7개 포함)
- [x] Coil(15×12)·Hook(13×12) 맵에서 각각 플레이 영역 전체가 화면 안에 들어온다 (Play 스크린샷 확인)
- [x] 데코 지대가 아니라 플레이 그리드 기준으로 fit — bounds 35×32 → 15×12, 거리 54.6 → 20.5
- [x] `boardFitMargin = 1.12` — 스폰 마커가 화면 가장자리에 붙지 않을 여유 (사용자 확인)
- [ ] 페이즈 전환·드래그 포커스·킥·펄스가 새 홈 기준으로 정상 동작 (실전 Play 확인 남음)

확인: 2026-07-24 · pitch 60° 라 세로(깊이) 제약이 지배 → 폭이 달라도 거리 동일(20.5). 깊이가 다른 맵에서만 거리 변동.

## 후속 후보

- 화면 중심 오프셋(보드를 살짝 위/아래로) 노브 — 현재는 보드 중심이 정중앙.
- 세로/가로 aspect 별 margin 분리 — 실기기 aspect 편차가 크면.
