# 2. Legacy 렌더 서브시스템 삭제

## 목적

`MapView` 렌더 서브시스템(절차적 region mesh + surface rule 해석기)과 그 전용 테스트를 삭제하고, `BattleBridge` 의 MapView 코드 경로를 제거한다. 선행: unit 0(헬퍼 추출)·unit 1(입력 의존 해소) 완료 필수.

## 변경 대상

**파일 삭제** (+.meta):
- `Assets/_Project/Scripts/Core/MapView.cs`
- `Assets/_Project/Scripts/Data/TerrainSurfaceSelector.cs`
- `Assets/_Project/Scripts/Data/TerrainTileRuleResolver.cs`
- `Assets/_Project/Tests/EditMode/TerrainSurfaceSelectorTests.cs`
- **백드롭 전체** (사용자 결정 2026-07-03 — Legacy3D 전용 기능이라 통삭제): `Presentation/Backdrop/BackdropMounter.cs` + `BackdropAnchorTable.cs`, `Data/Season/SeasonBackdropData.cs`, **`Editor/SeasonBackdropDataEditor.cs`** (CustomEditor — 누락 시 Editor 어셈블리 compile 붕괴, critic B1), `Tests/EditMode/BackdropAnchorTableTests.cs`, asset `Assets/_Project/Data/Season/backdrop_S1_forest.asset`

**`Assets/_Project/Scripts/Bridge/BattleBridge.cs`** — mapView 필드 + 그것을 읽는 모든 경로:
- `mapView` 필드(82) + Awake null 체크(233~234)
- 683~688: `mapView.Initialize` + `_boardOrigin` 캡처의 legacy else-분기 (Tilemap 경로는 origin=zero 고정 — README 계약 유지)
- backdrop 제거: `enableSeasonBackdrop` 필드(45), `_backdropRoot`(121), `Unmount` 호출 5곳(356/718/1065/1107/3493), mount 사이트(719~720), `backdrop` 로컬(587)
- 750~759: 배경 프랍/장애물 legacy 경로 (`mapView.InstantiateBackgroundProps/InstantiateObstacles`)
- 1115: `mapView.ResetVisualRoots()`
- 2801/2807/2813/2819: hover/reject 의 `else if (mapView != null)` 분기 4개

**`Assets/_Project/Scripts/Data/Season/SeasonData.cs:12`** — `backdrop` 필드 삭제 (SeasonData/SeasonRuntime/mapTheme 채널 자체는 유지 — 시즌 시스템은 ACTIVE)

## 구현

1. bridge 경로 제거를 먼저 (compile 유지한 채 mapView 참조 0건으로).
2. 파일 4종 삭제. `rg "\bMapView\b" Assets --type cs` → 주석 외 0건 확인.
3. `UseTilemapView` 프로퍼티/분기 조건 자체는 이 unit 에서 유지 — Legacy3D **모드 값** 제거는 unit 3.
4. 주석-only 언급(PaletteSanityProbe:8, FlowFieldSingleton:17, GridMath:9, MovementCellTrim:25, BackdropMounter:17)은 표현만 정리(선택, sim 코드 무변경).

**주의**:
- `_boardOrigin` 의미 불변 — Tilemap 모드는 이전부터 zero. legacy 캡처 코드만 사라진다.
- `ResultScreen.cs`/`PaletteSanityProbe.cs` 의 "backdrop" 은 무관한 동명 UI/프로브 — 건드리지 않는다.
- 백드롭 삭제로 `docs/spec/README.md` Follow-up Backlog 의 seasonal-backdrop 그룹에서 backdrop 의존 항목 3개(tint/exposure 튜닝·미세 시차·라이팅 매칭) 무효화 — 같은 커밋에서 정리. 시즌 시스템 항목(시즌별 MapThemeData/메타 hook/배지 UI)은 유지.
- `SeasonData.backdrop` 필드 삭제로 **forest·desert 두 시즌 asset 모두**(`season_S1_forest.asset:18`, `season_S2_desert.asset:18`) stale `backdrop:` 키 잔존(무해) — 재직렬화 시 둘 다 대상 (critic m2).
- `BattleScene.unity` 에 MapView GameObject(+컴포넌트)와 `BattleBridge.mapView` serialized 참조 잔존 — 코드 삭제 후 missing-script 로 남지만 compile/런타임 무해. 씬이 사용자 WIP 로 dirty 라 이 spec 에서는 씬 정리 안 함. **unit 5 handoff Follow-up 에 씬 청소 항목으로 기록** (critic m1).

## 완료 기준

- [x] compile 통과 (에러 0)
- [x] `rg "\bMapView\b" Assets --type cs` 주석 외 0건
- [x] EditMode 테스트 스위트 PASS (TilemapMapViewTests 포함, TerrainSurfaceSelectorTests 는 삭제됨; ObstaclePlacerTests 1건은 기존 실패 — 회귀 아님)
- [x] Tilemap Play 스크린샷 무회귀 (바닥/프랍/hover 피드백)

확인 2026-07-03 — compile 0 · 주석 외 참조 0건 · EditMode 434개 중 기존 실패 1건(ObstaclePlacer, 회귀 아님) 외 PASS · Play 스크린샷(`legacy_removal_u2_noregress.png`, 프랍 45/링 118/구조물 3 + hover(3,3) 하이라이트 정상) · 콘솔 에러 0. 백드롭 통삭제 포함(파일 10종+meta), Follow-up Backlog seasonal 그룹 정리 동반. 커밋 `73a2efd` (병행 GA 작업분 히스토리 분리 후 최종 해시).
