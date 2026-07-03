# 3. BoardViewMode.Legacy3D 모드 값 + 분기 + Legacy 씬 제거

## 목적

`BoardViewMode.Legacy3D` enum 값과 그것으로 분기하는 presentation/bridge 코드, Legacy 씬을 제거한다. 선행: unit 2 (MapView 삭제로 Legacy3D 분기 대부분이 이미 축소된 상태).

## 변경 대상

- `Core/BoardViewMode.cs` — `Legacy3D = 0` 삭제. **`TilemapRect = 1` / `TilemapIso = 2` 명시값 유지** (직렬화 안정 — 씬의 boardViewMode 는 int 로 저장됨)
- `Core/BoardSpace.cs` — 기본 `_mode`(12행) → `TilemapRect`, grid==null 시 Legacy3D 폴백(23~26) → LogError 만 남김(폴백 모드 없음), identity 숏컷 분기(36/51/63/72) 제거
- `Bridge/BattleBridge.cs` — 필드 기본값(85) → `TilemapRect`, `UseTilemapView`(217) 삭제 후 분기 상수화 인라인(681~695 ternary·gating, 697, **739, 763, 770, 778**, 2453, 2800~2818, **3071**, 1719, 1944 — critic m3 보완), `legacyCharacterScale`(91)·`characterBillboardTilt`(76) 필드 삭제 + Awake(246)/OnValidate(254) 의 static 미러 세팅을 `tilemapBillboardTilt` 로 전환
- `Presentation/SpineUnitView.cs:71`, `Presentation/QuadUnitView.cs:24,56,104~105` — Legacy3D 분기 제거 (tilemap 경로만 유지)
- **테스트 (critic B2)**: `Tests/EditMode/BoardSpaceTests.cs` — `Legacy3D_AllConversions_AreIdentity` 테스트 **삭제**(검증 대상 동작 자체가 제거됨) + TearDown 의 Legacy 리셋(17행) 제거. `Tests/EditMode/TilemapMapViewTests.cs:22` — 동일 리셋 제거. 근거: "유효 grid 없는 안전 idle 모드"는 더 이상 존재하지 않는다 — BoardSpace 는 사용 전 Configure 가 계약이고, 각 테스트는 자체 Configure 로 시작한다
- 주석 전량 정리: `rg -i legacy3d Assets --type cs` 히트 전체(~20곳 — BattleBridge 12곳, QuadUnitView 3곳, SpineUnitView/TilemapMapView/DamageNumberView/SkillBar/BoardCameraPreset 각 1곳 등). 4곳만이 아님 (critic M1)
- **씬/설정**: `Assets/_Project/Scenes/BattleScene_Legacy3D.unity`(+.meta) 삭제, `ProjectSettings/EditorBuildSettings.asset` 의 해당 씬 항목 제거

## 구현

1. enum 값 제거 → compile 에러를 지도 삼아 분기 순회 제거. 각 분기에서 **Tilemap 경로만 남긴다** (동작 선택이 아니라 상수 접기).
2. `ApplyEnvironmentGating(bool)` — 인자 상수 true 이므로 무인자 "항상 숨김" 으로 단순화 (빈 목록 no-op 유지).
3. `CharacterBillboardTilt` static 미러: 빌드 전(Awake/OnValidate)에도 SpineUnitView 가 유효값을 읽어야 함 → `tilemapBillboardTilt` 로 대체. 빌드 시(682)와 값 소스 일원화.
4. 씬 삭제 + Build Settings 항목 제거는 마지막에. **주의**: `EditorBuildSettings.asset` 이 이미 dirty — 커밋 전 diff 확인해서 legacy 씬 항목 제거분만 포함되게 스테이징.

**주의**:
- `BoardSpace.Configure` null-grid 가드: 폴백 모드가 사라지므로 **LogError + 상태 미변경(return)** 으로 — 잘못된 구성을 조용히 받지 않고, 마지막 유효 구성을 유지한다. 이후 변환 호출은 명시 예외로 실패 (조용히 잘못 그리는 것보다 낫다). 신규 가드 테스트 `Configure_NullGrid_LogsErrorAndKeepsLastValidConfig` 추가.
- 테스트 TearDown 리셋 제거로 stale grid 가 정적 상태에 남을 수 있으나, BoardSpace 를 쓰는 모든 테스트는 자체 Configure 로 시작하므로 누수 없음.
- **headless sim 빌드 계약** (구현 중 발견): `BattleBridgeDraftMapTests`/`DraftControllerMapRebuildTests` 는 view 없는 BattleBridge 로 맵 빌드를 검증한다 — 종전엔 Legacy3D 기본값이라 view-init 를 안 탔음. 따라서 view-init/`BoardSpace.Configure` 는 view 부재 시 **조용히 skip** (다른 view 가드들과 동일), 씬 오배선 감지는 **Awake 의 tilemapMapView null 체크**(런타임 전용, EditMode 테스트에서 안 불림)가 담당.

## 완료 기준

- [x] compile 통과 (에러 0)
- [x] `rg -i "legacy3d" Assets --type cs` → 0건 (코드 + 주석 전량, ProjectSettings/씬 제외)
- [x] EditMode 테스트 스위트 PASS (BoardSpaceTests 는 identity 테스트 삭제 후 기준)
- [x] Tilemap Play 무회귀: 유닛 스폰/빌보드 틸트/데미지 숫자/스킬 캐스트 방향(1944 관련)/배치 피드백
- [x] Build Settings 에 BattleScene_Legacy3D 부재 확인

확인 2026-07-03 — compile 0 · sweep 0건 · EditMode 434개 기존실패 1건(ObstaclePlacer) 외 PASS(헤드리스 7건은 view-init silent-skip 계약으로 해소) · Play 전투 풀루프(배치→웨이브→교전 VFX→드림캐쳐 prep→결과창) 무회귀 스크린샷 3장(`legacy_removal_u3_*.png`) · 콘솔에 legacy-removal 계열 에러 0 (Particle Velocity 플러딩은 GA 히트 VFX 이슈 — 별도). 데미지 숫자/캐스트 방향은 코드 무변경(주석/상수접기)이라 공유 변환(ToView) 정상성으로 갈음.
