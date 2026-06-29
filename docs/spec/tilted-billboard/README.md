# Tilted Billboard (2.5D) — 메인 Tilemap 씬

> 상태: 진행 중 (2026-06-24 착수 · 2026-06-25 **퍼스펙티브+XZ 모델로 방향 전환**)
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

## 작업 단위

| # | 문서 | 작업 구분 | 목적 |
|---|---|---|---|
| 0 | `0_billboard_component.md` | 컴포넌트 통합 | 단일 `Billboard`(Mode+주입 틸트각). SpineUnitView 인라인 틸트 이관 |
| 1 | `1_camera_tilt_framing.md` | 카메라 | `BoardCameraPreset` 틸트 + framing 틸트 보정 |
| 2 | `2_character_tilt.md` | 튜닝(캐릭터) | 캐릭터 레이어 틸트 핀 해제 + θ 정합 φ |
| 3 | `3_blob_shadow.md` | 신규 | `BlobShadow` 컴포넌트 + 스폰 배선 + 정렬 |
| 4 | `4_prop_layer_unify.md` | 통합/레이어 | Quad·Prop 를 `Billboard` 로 수렴 + 프랍 per-data 틸트각 |
| 5 | `5_handoff_summary.md` | 인계 | 구현 종료 요약 (구현 후 작성) |
| 6 | `6_prop_distance_tilt.md` | 신규 | 배경 프랍 거리(elevation) 기반 틸트 — 퍼스펙티브 근/원 부조화 보정 (캐릭터 제외) |

## 튜닝 시작값 (실측 조정 전제)

- 카메라 pitch **θ ≈ 50°**, 캐릭터 tilt **φ ≈ −35°** (≈ θ×0.7, XY보드라 음수).
- 프랍 틸트각은 per-data: 풀/낮은 것 작게, 나무/구조물 크게 (문서 참조: 38~52 범위 감각).

## 후속 후보 (현 범위 밖)

- **검정 아웃라인(DST/CotL)**: URP 호환 셰이더 별도 검증 필요. 별도 spec.
- **sim-height → 화면 offset**: 호버 유닛/투사체 아크의 높이 표현. 현재 평면뷰 유지.
- **TiltedDynamic**: elevation 기반 동적 틸트. 퍼스펙티브 전환으로 도입 정당화됨 → **배경 프랍은 unit 6 에서 스폰 bake 로 실현**(정적 카메라라 per-frame 불필요). 이동 캐릭터는 휘청 회피 위해 제외(고정 유지).
- **GroundWander 풍 앰비언트 크리터**: 경로 이동 디펜스라 게임플레이엔 부적합, 데코 한정 검토.
