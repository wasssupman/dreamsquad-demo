# Tilted Billboard (2.5D) — 메인 Tilemap 씬

> **상태: 완료 2026-08-30** — units 0~9 구현·검증 완료 (10 폐기 · 11 보류). 인계 = `12_handoff_summary.md`
>
> 이력: 2026-06-24 착수 · 2026-06-25 **퍼스펙티브+XZ 모델로 방향 전환** · 2026-08-28 **unit 7~ 재개(디오라마 스테이지 정합)**
> 대상 씬: `Assets/_Project/Scenes/BattleScene.unity` (Tilemap 모드, URP). Legacy3D 는 불변.

## 상위 목표

메인 Tilemap 배틀 씬을 평면 탑다운에서 **CotL/DST 풍 2.5D 빌보드 룩**으로 업그레이드한다.

## 방향 전환 이력 (중요)

- **초기(폐기)**: ortho 카메라 + XY 평면 보드를 틸트. → XY 보드는 사실상 "세워진 벽"이라
  ortho 틸트가 어색하고 여전히 탑다운으로 보임. 폐기.
- **현재**: **퍼스펙티브 카메라 + XZ 평면 바닥 + 레이어별 빌보드 각도.** 참조 문서 원본 모델이자
  Legacy3D 좌표계와 동일 → 빌보드 틸트 수학이 자연스럽다(`Euler(tilt,0,0)`).
- 이는 `tilemap-view-backend` 의 "XY 평면 정면뷰" 결정을 의도적으로 뒤집은 것. `BoardSpace` 가
  전부 `grid.transform` 기준이라, **그리드를 90° 회전해 XZ 바닥에 눕히면** ToView/ToSim/RaycastPlane 이 자동 추종한다.

## 핵심 기하 (왜 동작하는가)

- **그리드 90° 회전** → 타일맵이 XZ 수평 바닥(진짜 지면). 카메라는 **퍼스펙티브**로 위·뒤에서 내려다봄.
- **캐릭터/프랍은 빌보드**: XY 수직면으로 서고, 발 기준 월드 X 틸트 `Euler(φ,0,0)` 로 카메라를 향해 기울임.
- **레이어별 φ**: 바닥=틸트 없음(카메라가 비스듬히 봄), 캐릭터·프랍은 각자 φ.
- 값 감각: 카메라 pitch ≈ 55°, 캐릭터 φ ≈ pitch×0.8 ≈ 45° (참조 문서 Characters 45 와 동일 계열).

## 2026-08-28 드리프트 — 전제가 깨진 두 곳

unit 0~6 은 «바닥 = 그리드 평면이 월드 Y≈0» + «카메라 pitch 55° 고정» 시절에 저작됐다.
그 뒤 `map-diorama-stage` 가 들어오면서 두 전제가 깨졌는데 이 spec 이 따라가지 않았다.

**(a) 블롭이 스테이지 평면을 모른다.** 블롭 Y = `BattleBridge.BlobShadowGroundY` = 씬 전역 상수
`0.216`(`BlobShadow.cs:125`). 그런데 MapStage 는 스테이지마다 발바닥 평면을 선언한다(`gridOriginLocal.y`).

| 스테이지 | `gridOriginLocal.y` | 블롭 Y | 어긋남 |
|---|---|---|---|
| `MapStage_StreetDay` | 0.87 | 0.216 | **−0.654 (바닥 아래로 파묻힘)** |
| `MapStage_Duel` / `_Street` / `_Subway` | 0.19 | 0.216 | +0.026 ✓ |
| `MapStage_Hello` | 0 | 0.216 | +0.216 (공중) |

`0.216` 은 0.19 스테이지에 맞춰 손으로 튜닝된 값(0.19 + 판독 리프트 0.026)이다.
바닥 타일 페인팅이 은퇴해(`TilemapMapView.cs:140`) 지금 바닥은 디오라마 메쉬라,
평면 아래로 내려간 블롭은 깊이 테스트에 잘리거나 자글거린다. → unit 7

**(b) 모드가 데이터가 아니다.** 아래 계약은 "틸트 각은 데이터에서 온다"고 적었는데, 실제로 데이터인 건
**각도뿐**이고 **모드는 코드 4곳에 하드코딩**돼 있다 — `SpineUnitView.cs:106` · `QuadUnitView.cs:67` ·
`DefenderDragPlacementController.cs:1621·1721` 전부 `BillboardMode.Tilted`. `Billboard` 의 `mode`
SerializeField 는 런타임 `AddComponent` 직후 `Setup` 이 덮어써 스폰된 유닛에선 죽은 필드다.
그래서 카메라 pitch 를 90 으로 올려도 캐릭터·프랍은 45° 로 서 있다.
→ 별도 spec **`docs/spec/billboard-camera-follow/`** 가 소유한다(이 spec 은 결함을 기록만 한다).

## feature-wide 계약

- **구조적 레이어 분리**: 바닥(틸트 없음) / 캐릭터 / 배경 프랍. 빌보드 레이어는 **독립 φ**.
- **틸트 각은 데이터에서 온다** (하드코딩 금지): 캐릭터=`tilemapBillboardTilt`(serialized), 프랍=`PropData.tiltAngle`(per-SO).
- **단일 `Billboard` 컴포넌트**가 틸트/페이싱 공식의 유일 소유자. 틸트 각도는 호출측/데이터에서 주입.
- **좌표 권위는 `grid.transform`**: 보드 평면(XZ)·중앙정렬·입력 평면 모두 grid 기준 live. Tilemap sim origin=0 불변.
- **회전 vs 위치/스케일/페이싱 채널 분리**: 틸트=`transform.rotation`(X), 좌우반전=Spine `ScaleX`, 위치=`BoardSpace.ToView`.
- **피벗 = 발(feet)**: 틸트는 view transform 원점(셀 위치) 기준 회전. 위치·정렬 불변.
- **정렬**: cell 기반 `sortingOrder` 유지(행=깊이). 퍼스펙티브 전환 후 정렬·헬스바 회귀 검증.
- **범위**: Tilemap(주로 Rect)만. Legacy3D 는 건드리지 않는다.
- **파이프라인**: URP. 블롭은 `Sprites/Default`. 검정 아웃라인은 범위 밖.

### 2026-08-28 추가 계약 (unit 7~)

feature-wide 인 것만 둔다 — 구현 상세(발끝 샘플 방식·크기 산식·offset 축)는 각 unit 문서가 소유한다.

- **보드 평면의 소유자는 스테이지다.** 블롭은 `BoardSpace.RaycastPlane()` 로 읽고 **절대 월드 Y 상수를 갖지 않는다.** `blobShadowLift` 는 평면 **상대** 값이다.
- **블롭은 캐릭터 아래 대역이다** (`ShadowOrder = -5`, unit 3). 유닛 정렬 스윕이 이를 덮지 않는다 — unit 8 이 되찾는다.
- **차폐는 깊이 테스트에 맡긴다.** 가림 판정 코드·레이캐스트를 추가하지 않는다 (사용자 결정 2026-08-28).
- **프랍 authored 블롭 경로는 건드리지 않는다** (`shadow-polish unit 6` — 프리팹이 위치/회전/크기 정본).
- **1×1 유닛과 적은 수치가 변하지 않아야 한다** (0.19 스테이지 기준). 회귀 가드.
- **증거 없는 수정은 하지 않는다.** XZ 계열(unit 10·11)은 Play 계측이 어긋남을 보여준 뒤에만 착수한다 (CLAUDE.md 버그 절차 1번).

## 작업 단위

| # | 문서 | 작업 구분 | 목적 |
|---|---|---|---|
| 0 | `0_billboard_component.md` | 컴포넌트 통합 | 단일 `Billboard`(Mode+주입 틸트각). SpineUnitView 인라인 틸트 이관 |
| 1 | `1_camera_tilt_framing.md` | 카메라 | `BoardCameraPreset` 틸트 + framing 틸트 보정 |
| 2 | `2_character_tilt.md` | 튜닝(캐릭터) | 캐릭터 레이어 틸트 핀 해제 + θ 정합 φ |
| 3 | `3_blob_shadow.md` | 신규 | `BlobShadow` 컴포넌트 + 스폰 배선 + 정렬 |
| 4 | `4_prop_layer_unify.md` | 통합/레이어 | Quad·Prop 를 `Billboard` 로 수렴 + 프랍 per-data 틸트각 |
| 5 | ~~`5_handoff_summary.md`~~ | 인계 | **작성된 적 없음**(units 0~6 구간). units 7~9 인계는 12번 |
| 6 | `6_prop_distance_tilt.md` | 신규 | 배경 프랍 거리(elevation) 기반 틸트 — 퍼스펙티브 근/원 부조화 보정 (캐릭터 제외) |
| 7 | `7_blob_ground_plane.md` | 결함 수정 | 블롭 접지 — 보드 평면 소유권 (드리프트 a). **측정된 결함** |
| 8 | `8_blob_sorting_order.md` | 결함 수정 | 정렬 스윕이 덮어쓴 `ShadowOrder(-5)` 되찾기. **정적 확인됨** |
| 9 | `9_blob_footprint_size.md` | 저작면 | 블롭 지름 = footprint 가로 타일 수 |
| — | — | **계측 게이트** | ✅ 2026-08-28 실측 — `dXZ(origin, blob) = 0`, 발끝은 평면 위(−0.03). **어긋남 없음** |
| 10 | `10_blob_camera_projection.md` | **폐기** | 계측이 요구하지 않았다. `bounds` 가 발끝 대용이 못 된다는 것도 실측으로 드러남(문서 하단) |
| 11 | `11_blob_unit_offset.md` | 보류 | 유닛별 XZ 노브 — 육안에서 남는 어긋남이 보고되면 착수 |
| 12 | `12_handoff_summary.md` | 인계 | units 7~9 구간 인계 지도 |

드리프트 (b)(모드 하드코딩 → pitch 추종)는 **이 spec 의 units 로 넣지 않는다.**
이 spec 의 결론이 「캐릭터 φ = 45° 고정」이라 그 결론을 뒤집는 작업을 같은 폴더에 넣으면
README 계약이 자기모순이 된다. → **`docs/spec/billboard-camera-follow/`** 가 소유한다(계약 승계·개정 포함).
선행: 이 spec 의 7~9 가 먼저다 — 블롭이 −0.65 어긋난 상태에서는 새 빌보드 룩을 판정할 수 없고,
7~9 의 «0.19 스테이지 전후 동일» 회귀 가드도 기준선을 잃는다.

## 튜닝 시작값 (실측 조정 전제)

- 카메라 pitch **θ ≈ 50°**, 캐릭터 tilt **φ ≈ −35°** (≈ θ×0.7, XY보드라 음수).
- 프랍 틸트각은 per-data: 풀/낮은 것 작게, 나무/구조물 크게 (문서 참조: 38~52 범위 감각).

## 후속 후보 (현 범위 밖)

- **검정 아웃라인(DST/CotL)**: URP 호환 셰이더 별도 검증 필요. 별도 spec.
- **sim-height → 화면 offset**: 호버 유닛/투사체 아크의 높이 표현. 현재 평면뷰 유지.
- ~~**TiltedDynamic**~~ → **`billboard-camera-follow` 로 이관**(2026-08-28). 단 원래 항목이 배제한 것은 **elevation(위치) 기반** 동적 틸트이고, 그 배제 사유("이동 캐릭터 휘청")는 유닛마다 각이 달라지는 데서 온다. 그 spec 이 검토하는 것은 **카메라 pitch 파생** — 전 유닛이 같은 각을 공유하고 카메라가 천천히 움직이므로 그 사유가 걸리지 않는다. 배경 프랍의 elevation bake(unit 6)는 그대로 둔다.
- **`BlobShadowStyle` SO 이관**: `BattleBridge` 의 blob 튜닝 serialized 4개 + static 4개를 SO 로. 제약 6 정합. 제약 12 판정으로 unit 7 범위에서 **이관됨** — 이 spec 의 검증 질문에 답하는 데 불필요.
- **프랍 authored 블롭을 unit 7 계약으로 편입**: 지금은 프리팹이 정본이라 스테이지 평면을 모른다. 프랍은 안 움직이므로 급하지 않다.
- **footprint 전체를 덮는 타원/rect 블롭**: unit 9 는 «가로 폭 지름의 원». 2×3 유닛에서 세로가 남는 게 거슬리면 그때.
- **큰 보스의 1타일 그림자** (리뷰 2026-08-30): 적은 sim 이 1칸 점유라 `FootprintWidthCells => 1` 이 참값이지만, `spineVisualScale` 이 3.2 까지 저작된 보스가 폭3 디펜더 옆에 서면 그림자가 «몸집» 과 어긋난다. unit 9 이전엔 전부 1타일이라 그림자가 아무 주장도 안 했는데, 이제 «몇 칸을 쓰는지» 를 말한다고 선언해서 생긴 불일치다. 유닛별 크기 노브 금지는 사용자 결정이므로, 손대려면 «시각 크기» 축을 따로 세워야 한다.
- **블롭 지름의 셀→월드 환산에 `tileSize` 부재** (리뷰 F3): `9_blob_footprint_size.md` 의 «단위에 대한 주의» 참조. `tileSize = 1` 인 동안 무해하고 전역 노브로 균일 보정 가능하지만, 그 노브가 예술적 배율과 환산을 겸직하게 된다.
- **GroundWander 풍 앰비언트 크리터**: 경로 이동 디펜스라 게임플레이엔 부적합, 데코 한정 검토.
