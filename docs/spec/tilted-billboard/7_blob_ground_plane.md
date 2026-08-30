# 7 — 블롭 접지: 보드 평면 소유권

> unit 3(블롭 신설)의 후계. 그 unit 이 정한 «절대 월드 Y 상수» 를 «스테이지 평면 상대» 로 바꾼다.
> 디오라마 스테이지 도입으로 전제가 깨진 부분만 고치고, 나머지 계약은 unit 3 그대로 둔다.

## 목적

블롭의 Y 를 씬 전역 상수에서 **스테이지가 선언한 보드 평면**으로 옮긴다.
README «2026-08-28 드리프트» 표(StreetDay −0.654 / Hello +0.216)가 이 unit 하나로 사라진다.

**XZ 는 건드리지 않는다.** 발 위치 어긋남은 아직 재현되지 않았고, 평면을 고치면 발끝이 평면에
붙으므로 그 뒤에 남는 어긋남을 계측해야 원인을 안다(unit 10 선행 조건). CLAUDE.md 버그 절차 1번.

## 변경 대상

- `Assets/_Project/Scripts/Presentation/BlobShadow.cs`
- `Assets/_Project/Scripts/Core/BoardSpace.cs` (`IsConfigured` 접근자)
- `Assets/_Project/Editor/PropDataEditor.cs` (주석만 — authored 프랍 값은 불변)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs`
- `Assets/_Project/Scripts/Presentation/SpineUnitView.cs` · `QuadUnitView.cs` · `IngameCharacterTest.cs` (인자명)
- `Assets/_Project/Scenes/BattleScene.unity`

## 구현

### 평면 소비 (제약 12 — 브리지 진입 없음)

`BlobShadow.ApplyTransform` 이 `Wassup.Core.BoardSpace.RaycastPlane()` 를 읽는다.
`BoardPlaneY` 같은 BattleBridge static 을 **신설하지 않는다** — `BoardSpace` 가 이미 `grid.transform`
을 소유하고 그 평면을 노출한다(`SpawnAlertPresenter` 가 이미 소비).

```
p       = 앵커 (기존대로 _groundAnchor ?? _target.position) — XZ 는 그대로 쓴다
groundY = BoardSpace.IsConfigured
            ? BoardSpace.RaycastPlane().ClosestPointOnPlane(p).y   // 스테이지 발바닥 높이
            : p.y                                                   // 맵 미빌드 하네스 폴백
pos     = (p.x, groundY + lift, p.z)
```

**⚠ `plane.normal` 로 띄우면 안 된다** (구현 중 발견). grid 가 `Euler(90,0,0)`
(`TilemapMapView.cs:218`)이라 `forward = (0,−1,0)` — 법선이 **아래**를 향해 블롭이 바닥 밑으로
내려간다. 같은 이유로 그리드 자식들은 local −Z 를 «카메라 쪽»으로 쓴다(`TilemapMapView.cs:678`).
리프트는 월드 +Y 다.

`BoardSpace.IsConfigured` 는 소유자 쪽에 새로 둔 얇은 접근자다(제약 12 (b)) — `RaycastPlane()`
이 `_grid` 를 역참조하므로 맵 미빌드 하네스(`IngameCharacterTest`)에서 부르기 전에 묻는다.

회전은 `Euler(90,0,0)` 유지 — 평면 법선 유도는 이 unit 범위 밖(README 후속 후보).

### lift 필드 의미 교체 (진입점 신설 아님)

- `blobShadowGroundY`(절대 Y) → `blobShadowLift`(평면 상대). static 미러도 `BlobShadowLift`.
- 씬 값 `0.216` → `0.026`. 근거: 0.216 − 0.19(0.19 스테이지 발바닥) = 0.026 = 원 주석의 "발 평면에서 ~5px(@1080)".
- ⚠ serialized 이름이 바뀌므로 **씬 YAML 의 키도 같이 바꾼다.** 에디터가 BattleScene 을 연 채
  저장하면 이 편집이 날아간다 — 편집 전후로 사용자에게 씬 상태를 확인한다.

### 순수 함수 없음

`ClosestPointOnPlane + normal*lift` 는 자명한 2줄이고 호출처가 1곳이다.
제약 10 하위조항 (a)(b)(c) 어느 것도 성립하지 않으므로 **인라인이 맞다.**
투영 산식(분기·폴백 있음)은 unit 10 에서 생기면 그때 추출한다.

## 완료 기준

- [x] 컴파일 통과 — 전 어셈블리 오류 0 (Runtime·Editor·Tests.EditMode·Tests.Assets·Skills·Assembly-CSharp)
- [x] 코어 lane 초록 — 2494 통과 / 실패 0 / 스킵 3(기존 Ignore). Assets lane 은 선행 실패 2건(`boomerang`·`bomb_man` 문안)만
- [x] Play 실측: StreetDay 평면 0.870 → 블롭 0.896 (= 평면+lift). 옛 코드는 0.216 = 0.654 아래
- [x] 회귀 가드: Duel 평면 0.190 → 블롭 0.216 = **옛 값과 정확히 동일**
- [x] 콘솔 에러 0
