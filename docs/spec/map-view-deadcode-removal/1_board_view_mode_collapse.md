# 1. `BoardViewMode` 접기 (iso 경로 제거)

## 목적

isometric 은 폐기됐다(2026-08-12 사용자 결정). `BoardViewMode` 는 값이 `TilemapRect` 하나만 남으므로 enum 자체가 의미를 잃는다. 남은 iso 분기는 **하나뿐**이며(`ConfigureGrid`), 그 외 모드별 분기는 unit 0 에서 이미 사라졌다.

값이 하나인 enum 을 시그니처 3개에 계속 실어 나르면 "모드가 여럿인 척"하는 거짓 신호가 남는다.

## 변경 대상

- `Assets/_Project/Scripts/Core/BoardViewMode.cs` — **파일 삭제**
- `Assets/_Project/Scripts/Core/BoardSpace.cs` — `_mode` 필드, `Mode` 프로퍼티, `Configure` 의 `mode` 파라미터
- `Assets/_Project/Scripts/Core/TilemapMapView.cs` — `Initialize` 의 `mode` 파라미터, `ConfigureGrid` 의 `mode` 파라미터 + iso 분기(`:229-238`)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `[SerializeField] boardViewMode` (`:133`), 호출부 2곳(`:1155`, `:1165`)
- `Assets/_Project/Scripts/Data/TileSetData.cs` — `isoCellSize` (`:99-101`)
- `Assets/_Project/Data/TileSets/PH_Iso_{Walk,Place,Env,Deco}.asset` (+ `.meta`) — **삭제** (참조 0)
- `Assets/_Project/Tests/EditMode/BoardSpaceTests.cs`, `TilemapMapViewTests.cs` — 아래 참조

## 구현

- `ConfigureGrid` 는 rect 경로만 남긴다: `cellLayout = Rectangle`, `cellSize = (tileSize, tileSize, 1)`. 90°X 회전·anchor·sorting·그림자 설정은 **전부 그대로**.
- `BoardSpace.Configure(simOrigin, tileSize, grid)` 로 시그니처 축소. `grid == null` 명시 실패 계약은 유지한다.
- `TilemapMapView.Initialize(map, tileSize, tileSet, realShadows = false)` 로 축소.

**테스트 처리 — 그냥 지우지 않는다.**

`BoardSpaceTests` 의 iso 케이스(`:72`, `:110-111`, `:132`)와 `TilemapMapViewTests.Iso_PaintAlignsWithBoardSpace`(`:90`)가 지키던 계약은 *"`BoardSpace` 는 iso 수식을 하드코딩하지 않고 주입된 `GridLayout` 에 위임한다"* 이다. **이 계약은 iso 폐기 후에도 유효하다** — 위임이 깨지면 grid 회전·셀 크기 변경이 조용히 어긋난다.

그래서 iso 레이아웃 대신 **비자명한 rect grid**(예: 비균일 `cellSize` + `transform` 회전/오프셋)로 같은 성질을 다시 못 박는다. "`Grid` 가 권위"라는 문장이 테스트로 남아야 한다.

- `TilemapMapViewTests.cs:97` 의 `tileSet.isoCellSize = ...` 도 함께 제거.
- `boardViewMode` SerializeField 제거는 씬을 편집하지 않는다 — `BattleScene.unity` 의 `boardViewMode: 1` 은 orphan 키로 남는다.

## 완료 기준

> ✅ 검증 2026-08-12 — compile 0 errors. EditMode **2193 중 실패 0**(2192→2193: iso 3개 제거, 대체 4개 추가).
> **변이 검증 통과**: `BoardSpace.ToView` 를 grid 위임 없이 rect 수식으로 하드코딩하자 **7개**가 빨개졌고
> 신규 회전 테스트 3개가 전부 포함됐다(`RotatedNonUniformGrid_RoundTrip` / `_SimCellCenter` /
> `RotatedGrid_AxisDirections`). 되돌린 뒤 재실행 green — 대체 테스트가 헛돌지 않음을 확인.
> 셀 중심 비교는 **grid 로컬 공간**으로 바꿨다(월드 XY 비교는 그리드가 회전하면 거짓 실패한다).
> 페인트 정합은 **3축 전부**로 강화 — `tileAnchor.z=0` 계약이 깨져도 XY 비교는 통과해 버린다.
> Play(BattleScene) 정상, 보드/프레이밍 unit 0 스크린샷과 동일, 콘솔 error/warning 0.
> `BattleScene.unity` 무변경(`boardViewMode: 1` 은 orphan 키로 잔존).

- Unity compile 0 errors.
- EditMode 전체 green. **iso 테스트를 대체한 신규 테스트가 실제로 존재하고 통과**한다 (삭제만 하고 끝내지 않았음을 확인).
- 대체 테스트가 유효한지 확인: `BoardSpace` 에 rect 수식을 하드코딩하도록 일부러 고치면 그 테스트가 **빨개져야** 한다. 확인 후 되돌린다.
- Play 1판: 보드 렌더·유닛 정렬·드래그 배치·hover/reject 가 정리 전과 동일. 스크린샷 1장.
- 콘솔 신규 error/warning 0.
- `.meta` 짝 삭제 확인. `BattleScene.unity` 미커밋 확인.
