# 3. 프레젠테이션 경계 매핑 적용

## 목적

ECS sim 좌표가 MonoBehaviour 뷰로 넘어가는 모든 지점에 `BoardSpace.ToView`(위치)/`ToViewVector`(방향), 입력이 sim 으로 들어오는 지점에 `ToSim` 을 적용한다. sorting 은 sim 좌표를 보존해 계산한다. Legacy3D 에서는 identity 라 무변화.

## 변경 대상 — 위치 write (`ToView`)

- `Assets/_Project/Scripts/Presentation/SpineUnitView.cs` (L26, L56)
- `Assets/_Project/Scripts/Presentation/QuadUnitView.cs` (L31) + `QuadUnitViewPool.cs` (L28)
- `Assets/_Project/Scripts/Presentation/ProjectileViewPool.cs` (L104, L144, L153)
- `Assets/_Project/Scripts/Presentation/DamageNumberView.cs` (L51. L70 driftUp 은 view-up(Y) 연출 — XY 뷰에서도 Y 가 화면 위라 무변환. 이 가정을 주석으로 명시)
- `Assets/_Project/Scripts/Presentation/MeteorFall.cs` (L29, L38 — start/target 을 ToView 후 보간)
- `Assets/_Project/Scripts/Presentation/VfxSpawner.cs` — `transform.position` 대입(L96, L97 portal)뿐 아니라 **`Instantiate(prefab, pos, …)` 오버로드로 위치가 들어가는 5곳**: `SpawnPlacementRing`(L33), `SpawnMeteorFall`(L48), `SpawnMeteorBurst`(L63), `SpawnTornado`(L77), `SpawnHealApplied`(L129). 변환은 **VfxSpawner 각 메서드 진입부 1회** — BattleBridge 호출부는 sim 좌표 유지 (이중 변환 금지).
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `SpawnMeteorWarningVisual`(L1500~) 의 경고 Quad 위치 + 평면 회전 (Tilemap 모드에서 XY 평면을 보도록).
- `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs` (L64 — preview 위치)

## 변경 대상 — 방향/회전 (`ToViewVector` 또는 view 공간 일원화)

- `SpineUnitView.FaceToward`(L137~145): 비교 대상인 `worldPoint`(sim, `NotifyAttack` 경유)를 진입부에서 `ToView` 후 view `transform.position` 과 비교.
- `SpineUnitPool`/`BattleBridge.TrySpawnCastVfx`(L1720~1722): `dir = targetWorld - anchor` 가 sim−view 혼합 — `targetWorld` 를 `ToView` 한 뒤 빼기. `ResolveCastAnchor` 결과는 view 공간임을 주석으로 명시.
- `ProjectileViewPool.SyncTransforms`(L109~115): 속도/`LookRotation` 계산을 **ToView 적용 후의 view 좌표끼리** 수행 (lastPosition 도 view 좌표로 보존) — 위치만 변환하고 회전을 sim delta 로 계산하는 실수 금지.

## 변경 대상 — sorting (sim 좌표 보존)

- `SpineUnitView`(L72~74) / `QuadUnitView`(L43~45) 의 `UpdateSortingOrder` 가 현재 view `transform.position` 에서 셀을 역산 (`BoardSortOrder.ComputeFromWorld` 는 `world.z` 사용) — ToView 적용 후 z 가 소실되어 붕괴한다. **`UpdatePosition(simWorld)` 에서 sim 좌표를 필드로 보존**하고 `UpdateSortingOrder` 는 보존된 sim 좌표로 `ComputeFromWorld` 호출. `BoardSortOrder.cs` 자체는 무변경.

## 변경 대상 — 입력 read (`ToSim` + 입력 평면)

- `Assets/_Project/Scripts/Core/PlacementInput.cs` (L65~69)
- `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs` (L126~130)
- `plane` 생성을 `BoardSpace.RaycastPlane()` 으로 교체, 히트 지점을 `ToSim` 후 기존 `GridMath.WorldToCell` 흐름 유지. 셀 판정/Place 로직 무변경.
- hover/reject 피드백 (`mapView.SetPlacementHover`/`FlashTileReject`/`ClearPlacementHover` 호출 — PlacementInput L86/L97, DragController L95/L235/L241): 활성 뷰로 분기 (Tilemap 모드 → `TilemapMapView` 의 unit 1 대응 메서드).

## 구현 원칙

- 변환식을 뷰 클래스 안에 인라인으로 풀어 쓰지 않는다 (단일 변환 지점 계약). 방향은 `ToViewVector`, 위치는 `ToView` 만 사용.
- 누락 스캔 (완료 전 필수): `grep -rnE "transform\.position\s*=|\.position\s*=|Instantiate\([^)]*new Vector3|Instantiate\([^)]*[Ww]orld" Assets/_Project/Scripts/Presentation Assets/_Project/Scripts/UI Assets/_Project/Scripts/Bridge` — 위 열거 외 신규 발견 시 spec 에 추가 후 처리 (조용히 넘어가지 않는다).
- ECS 쪽 스냅샷/이벤트 생산 코드는 sim 좌표 그대로 유지 — 변환은 소비측에서만.

## 누락 스캔 추가 발견 (2026-06-14)

scan 이 spec 열거 밖 spots 를 찾음. 분류:

- **범위 안 (처리)**: `BattleBridge.PlayDeploymentPresentation`(placementVfxPrefab L2554), `PlayDeploymentRingPulse`(L2604), `PlayFallbackDeploymentPulse`(L2579) — 배치 연출 VFX(완료기준 "placement ring VFX"). sim `world` 직접 사용분만 `ToView`. `vfxSpawner.SpawnPlacementRing(world)` 은 VfxSpawner 내부 변환이라 sim 유지(이중변환 금지).
- **범위 밖 (후속 이관)**: `DebugObstacle` 큐브(L2799, 디버그 전용), 해저드 비주얼(`SpawnHazardWithVisual` L2831 / `SpawnBlockingHazardWithVisual` L2872 / destruction VFX L3043, hazard-caster 별도 feature). → README 후속 후보.
- **N/A**: `BackdropMounter`(Tilemap 모드 게이팅 OFF — Legacy 전용).

## 완료 기준

> ✅ 검증 2026-06-14 — 9파일 편집(SpineUnitView/QuadUnitView(+Pool)/ProjectileViewPool/DamageNumberView/
> VfxSpawner/PlacementInput/DefenderDragPlacementController/BattleBridge; MeteorFall 무변경). 위치 ToView,
> 방향 view 공간 일원화, sorting `_simWorld` 보존, 입력 RaycastPlane+ToSim, hover/reject BattleBridge facade 분기.
> compile 0, 전체 EditMode **325/323 pass**(회귀 0). **TilemapRect Play(메모리 배선)**: 적 13뷰 중 샘플 5개
> `view == ToView(sim)` d=0.000, 전부 z=0(보드 평면) — 셀 정렬 확정. Legacy3D=identity 무변경. 커밋: f8105ba.
> NOTE: SpineUnitView.cs 동반 커밋에 본 스펙 외 billboard tilt(미커밋이던 것) 포함.

- Unity compile 0 errors.
- `Legacy3D` Play smoke: 유닛/투사체/데미지 숫자/메테오(낙하+경고링)/포탈·토네이도·힐 VFX/배치 프리뷰/hover·reject/sorting 이 본 spec 이전과 동일 (identity 회귀).
- `TilemapRect` Play: 유닛·투사체가 보드 셀 위에 정렬되어 이동/전투. 투사체가 진행 방향을 향함. 유닛 상하 겹침이 행 기준 정렬. 드래그 배치가 의도한 셀에 떨어지고 hover/reject 피드백 표시 (경계 셀 포함).
- 메테오 경고 링·낙하·버스트, 토네이도, 힐, placement ring VFX 가 두 모드 모두에서 해당 셀 위에 표시.
