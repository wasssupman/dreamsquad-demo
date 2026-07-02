# unit 6 (rev) — 프랍 블롭 프리팹 authoring 전환

## 목적

멀티셀 sim footprint 프랍(2x1 통나무, 2x2 통 — 2026-07-02 최초 도입)에서 그림자 위치가 프랍마다 어긋남.
근본 원인: 이미지마다 피벗/캔버스 여백이 달라 **런타임 계산(피벗 기준·bounds 기준 모두)으로는 위치가 정규화되지 않음**.
프랍 그림자는 런타임에 움직일 일이 없으므로, 블롭을 **프리팹에 굽고 프랍별로 미세조정**하는 구조로 전환 (사용자 결정).

## 변경 대상

- `Assets/_Project/Scripts/Presentation/BlobShadow.cs` — `authoredInPrefab` 직렬화 플래그 + Awake 전역값 정규화
- `Assets/_Project/Editor/PropDataEditor.cs` — `GeneratePrefab` 이 BlobShadow 자식을 자동 생성
- `Assets/_Project/Scripts/Core/TilemapMapView.cs` — `AttachPropBlob` 은 authored 블롭 내장 시 스킵 (레거시 폴백만 유지)

## 구현

- **소유권 분리**: 위치 XZ·크기(footprint 종횡비)·회전 = **프리팹 소유** (프랍별 확정값, 에디터에서 WYSIWYG 조정).
  sprite·color·sortingOrder·바닥 Y = **전역 소유** — authored 블롭의 `Awake()` 가 `BattleBridge.BlobShadow*` 전역값으로 덮어 기존 "모든 수치는 데이터에서" 계약 유지.
- **생성기 기본값**: 크기 = `1타일 × visualScale × footprint 종횡비(fx/max, fy/max)` (긴축 정규화), 위치 z = Tilted 프랍의 몸체 중심 지면 투영(`0.5 × 월드높이 × sin(tilt)`), 회전 = Euler(90,0,0).
  기본값은 출발점 — 캔버스 여백이 큰 아트는 프리팹에서 손튜닝 (통나무: z 0.45, scale 1.4×0.7).
- **재생성 보존**: `GeneratePrefab` 재실행 시 기존 프리팹의 블롭 transform 을 복사해 수동 튜닝을 날리지 않는다.
- **레거시 폴백**: 블롭 미내장 프리팹(기존 로스터)은 종전 런타임 원형 블롭(`BlobShadowSize × visualScale`, 피벗 XZ) 유지. 회귀 없음.
- 유닛(QuadUnitView/SpineUnitView)은 런타임 `Attach`(live) 경로 그대로 — 동작 불변.

## 주의 (소스 캔버스 크기)

PPU 256 고정이므로 **캔버스 px 가 월드 크기를 결정**한다: 월드 크기 = 캔버스px/256 × visualScale.
소스 이미지를 다른 해상도로 교체하면 visualScale 과 블롭 튜닝을 재점검할 것 (512px 통나무 교체 사고: visualScale 1.6→0.8 재조정).

## 완료 기준

- compile 클린.
- Play → 게임뷰 스크린샷: 통(2x2)·통나무(2x1) 그림자가 몸체 아래 정위치, 1x1 프랍(꽃/돌)은 기존과 동일.
- 확인 2026-07-02 (Play 스크린샷 육안 통과).
