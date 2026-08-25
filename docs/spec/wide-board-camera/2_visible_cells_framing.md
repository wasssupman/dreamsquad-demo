# unit 2 — 가시 칸수 저작 (판이 화면보다 커진다)

## 목적

전투·배치 레시피가 **판 전체**를 잡던 것을 그만두고, **「가로 N 칸이 보인다」**를 저작한다.
이 unit 이 이 spec 의 진짜 전환점이다 — 여기서 처음으로 판이 화면 밖으로 나간다.

**거리로 저작하지 않는다.** 고정 거리는 화면이 넓은 기기가 판을 더 보게 만든다(가시 가로는
`tanV × aspect` 에 정비례). 랭킹을 제출하는 비동기 토너먼트에서 **실력의 일부가 기기가 된다.**
이 프로젝트는 절대 거리 저작이 화면비마다 무너진 사고를 이미 겪었고, 그 실측이
`CameraFramingMath.DofRange` 주석에 남아 있다(*"16:9 게임뷰 27.7 → 19.5:9 폰 25.3"*).

## 변경 대상

- `Assets/_Project/Scripts/Data/CameraDirectionConfig.cs` (`CameraStateFraming`)
- `Assets/_Project/Scripts/Presentation/CameraFramingMath.cs`
- `Assets/_Project/Scripts/Presentation/CameraDirector.cs`
- `Assets/_Project/Data/Camera/CameraDirectionConfig.asset`
- `Assets/_Project/Tests/EditMode/CameraStatePoseTests.cs` → `Assets/_Project/Tests/EditModeAssets/CameraStatePoseTests.cs` (**이동** — 에셋을 읽으므로 Assets lane. asmdef 참조도 그쪽으로 바뀐다)

## 구현

**`fitToBoard`(bool) → `distanceMode`(enum 3값)**: `FitBoard`(오버뷰) / `VisibleCells`(전투·배치) /
`FixedDistance`. `CameraStateFraming` 에 `visibleBoardWidthCells` 를 더하고, 거리는 unit 0 의 역산으로
**매 프레임 파생**된다. 저작값은 거리가 아니라 칸수다. `FitDistance`(코드)는 `FitBoard` 모드가 그대로
쓰므로 **삭제 대상이 아니다.**

⚠ **직렬화 마이그레이션.** bool → enum 은 자동 승격이 없다. `distanceMode` 를 **새 필드로 신설**하고
에셋의 각 레시피에 값을 직접 저작한다. 커밋 후 `CameraDirectionConfig.asset` 을 열어 배치·전투
레시피의 거리 관련 필드가 기본값으로 리셋되지 않았는지 **육안 확인**한다(직렬화 타입 변경의 전형적
사고). 구 `fitToBoard` 필드는 마이그레이션 확인 뒤 같은 커밋에서 제거한다.

**작은 맵 자동 폴백**: 실효 칸수 = `min(저작 칸수, 보드 칸수)`. 라이브 맵 대부분이 15×12 이하라
이 폴백이 없으면 **판보다 화면이 커서 보드 밖 여백이 화면에 들어온다**. 폴백이 있으면 작은 맵은
오늘의 fit 과 같은 그림이 되고, 맵별 예외 코드가 0 이다.

**이 커밋의 저작값은 오늘의 그림이다.** `visibleBoardWidthCells` 를 라이브 최대 보드 폭 이상으로
저작해 **폴백이 걸리게** 둔다 — 기계는 들어오지만 화면은 안 바뀐다. **실제 칸수 인하(전투 줌이 판보다
좁아지는 값)는 팬이 열리는 unit 4 와 같은 커밋에서 저작한다.** 이유는 도달성이다: 팬 없이 판이 화면
밖으로 나가면 화면 밖 셀에 배치할 수단이 없고 `DragPlacementReachTest` 가 즉시 빨개진다.

**⚠ 착수 전 확정할 것**: 현재 `fovMin 31` 인데 전투 레시피가 `fov 25` 를 저작한다. 거리는 25 로
뽑히고 최종 FOV 는 31 로 클램프돼 **의도치 않은 여백이 이미 들어가 있다.** 이게 의도인지 사고인지
정하지 않으면 어떤 칸수도 올바르게 저작할 수 없다(README 「먼저 재야 할 것」 1).

**기존 테스트의 계약 교체.** `AuthoredPlacementLead_KeepsBoardOnScreen_AtFullPan` 은 「판이 화면에
다 들어온다」를 단언하므로 여기서 깨진다. **지우지 않고 계약을 바꾼다** — 새 단언은
「클램프가 관심점을 보드 안으로 가두고, 그 결과 보드 밖 여백이 화면에 들어오지 않는다」다.
그리고 이 테스트는 에셋을 로드하면서 코어 lane 에 있는 기존 오배치이므로 **`Tests/EditModeAssets/`
로 옮긴다**(lane 판별: 에셋을 읽으면 Assets lane).

## 완료 기준

- **화면비를 바꿔도 역산된 가시 칸수가 같다** — EditMode 단언 + 16:9 / 19.5:9 / 4:3 게임뷰 3종
  스크린샷. 이게 이 unit 의 핵심 단언이다.
- **화면이 오늘과 같다**(폴백 저작값). 기존 카메라 테스트 + `DragPlacementReachTest` 전건 초록.
- 작은 맵(Tutorial 12×7, Serpent 15×11)에서 폴백이 걸리는 것을 로그/스크린샷으로 확인.
- `CameraDirectionConfig.asset` 의 배치·전투 레시피 값이 마이그레이션으로 리셋되지 않았다(육안).
- 코어/에셋 lane 초록. 옮긴 테스트가 Assets lane 에서 돈다.
- **unit 3 과 같은 세션에서 확인한다** — 흐림 기준이 같은 전환에 걸려 있다.
