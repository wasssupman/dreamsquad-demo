# 12 — Handoff Summary (units 7~9)

> units 0~6 은 2026-06 에 끝났고 handoff 를 안 남겼다(README 표가 `5_handoff_summary.md` 를
> 가리키지만 그 파일은 존재한 적이 없다). 이 문서는 **units 7~9 구간**의 인계 지도다.

## Commit

- `06ab754a` feat(tilted-billboard): units 7~9 — 블롭 접지를 스테이지 평면 기준으로
- 관련: `e694f546` docs(claude-md): 제약 12 — BattleBridge 진입은 최후 수단 (이 작업에서 나온 규칙)
- **미푸시** (push 승인제)

## Implemented

- 블롭 높이가 씬 전역 절대 Y 가 아니라 **스테이지가 선언한 보드 평면**에서 온다. `blobShadowGroundY`(0.216) → `blobShadowLift`(평면 상대 0.026).
- 평면은 `BoardSpace.RaycastPlane()` 에서 읽는다. **BattleBridge 진입점 신설 없음** — `BoardSpace.IsConfigured` 접근자만 소유자 쪽에 추가.
- 유닛 정렬 스윕이 매 프레임 덮어쓰던 블롭 `ShadowOrder(-5)` 를 되찾았다(궤적 리그와 같은 형태의 캐시 참조 가드).
- 블롭 지름 = `FootprintWidthCells × BlobShadowSize`. `ISpineUnitVisualData` 에 멤버 신설(Defender = `Footprint.x` / 적 = `1`).
- 폭을 `QuadUnitViewPool.TrySpawn` 까지 관통. **기본값 없음** — 호출처 3곳이 의도를 명시한다.
- 프랍 authored 블롭 경로는 불변(`shadow-polish unit 6` 계약 유지).

## Key Files

- `Assets/_Project/Scripts/Presentation/BlobShadow.cs` — 자리 결정의 단일 지점(`ApplyTransform`)
- `Assets/_Project/Scripts/Core/BoardSpace.cs` — 평면 소유자. `RaycastPlane()` / `IsConfigured`
- `Assets/_Project/Scripts/Presentation/SpineUnitView.cs` · `QuadUnitView.cs` — Attach + 정렬 스윕 가드
- `Assets/_Project/Scripts/Presentation/QuadUnitViewPool.cs` — 폭 관통
- `Assets/_Project/Scripts/Data/ISpineUnitVisualData.cs` — `FootprintWidthCells`
- `Assets/_Project/Scenes/BattleScene.unity` — `blobShadowLift: 0.026`

## Verified

- EditMode 코어 **2494 통과 / 실패 0 / 스킵 3**(기존 Ignore). Assets lane 은 선행 실패 2건(`boomerang`·`bomb_man` 문안)만.
- 전 어셈블리 `dotnet build` 오류 0.
- Play 실측: StreetDay 평면 0.870 → 블롭 0.896 / **회귀 가드** Duel 평면 0.190 → 0.216(옛 값과 동일).
- 라이브 전투 실유닛 9기: order 전부 −5, y 전부 평면+lift.
- 2026-08-30 사용자 Play 육안 확인 통과.

## Notes (되돌리면 안 되는 것)

- **`plane.normal` 로 리프트하지 말 것.** grid 가 `Euler(90,0,0)`(`TilemapMapView.cs:218`)이라 법선이 **아래**를 향한다. 리프트는 월드 +Y. 구현 중 실제로 밟은 함정이다.
- **정렬 가드는 캐시 참조 비교여야 한다.** `GetComponentInParent<BlobShadow>()` 는 비활성 오브젝트를 건너뛰는데 스윕은 비활성까지 열거한다 — 블롭이 꺼지면 가드가 조용히 뚫리고, `Attach` 가 order 를 1회만 쓰므로 영구히 복원되지 않는다.
- **폭 파라미터에 기본값을 다시 넣지 말 것.** 기본값 1 이 디펜더 fallback(quad) 경로를 조용히 삼킨 것이 리뷰에서 잡혔다.
- `BlobShadow.ApplyTransform` 의 매 프레임 `RaycastPlane()` 은 **의도적**이다. `AlignGridTo` 가 맵 빌드마다 grid 를 옮기므로 Attach 시점 캐싱은 스테이지 교체에서 stale 해진다.
- unit 9 는 `eb2386ad`(전 유닛 footprint 1×1 철회) 이후 **화면 변화가 0** 이다. 고장이 아니라 저작값이 1 인 것 — 값을 올리면 코드 0 줄로 따라온다.

## Follow-up

- **unit 10 폐기** — 계측이 XZ 어긋남을 찾지 못했고, `bounds` 가 «시각 발끝» 대용이 못 된다는 것도 실측으로 드러났다(틸트가 +0.57Z, 무기가 −0.38X). 되살리려면 발끝 추정을 스켈레톤 본 등 다른 근거로 다시 세워야 한다.
- **unit 11 보류** — 유닛별 XZ 노브. 요구사항은 유효하나(사용자 2026-08-28) 육안에서 남는 어긋남이 보고되면 착수.
- README «후속 후보» 3건: `BlobShadowStyle` SO 이관 · 큰 보스의 1타일 그림자 · 블롭 지름의 `tileSize` 환산.
- **`docs/spec/billboard-camera-follow/`** — 빌보드가 카메라 pitch 를 따라가게 하는 원래 요청. README 만 작성됨, unit 0(모드 저작면) 미착수. 이 spec 의 «캐릭터 φ = 45° 고정» 계약을 그쪽이 승계·개정한다.
