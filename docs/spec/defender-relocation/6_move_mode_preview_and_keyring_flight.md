# 6 — 이동모드 프리뷰 강화 + 키링 비행

## 목적

이동모드의 목적지 선택 피드백을 강화하고 비행을 키링 룩으로 바꾼다:
1. 이동모드 진입 → **배치 가능 타일 하이라이트**
2. 타일 press 동안 → 그 타일의 **공격범위 프리뷰**
3. release 확정 → 슬로모 종료(기존) → **키링(고리+줄) 비행**으로 해당 위치에 드랍

확정 제스처(press→범위 프리뷰, release→커밋)는 unit 2 의 현행 모델과 이미 일치.

## 변경 대상

- `Assets/_Project/Scripts/UI/DefenderRelocationController.cs` (하이라이트/범위/키링 비행)
- `Assets/_Project/Scripts/Data/RelocationSettings.cs` (키링 비행 노브)

## 구현

1. **진입 하이라이트**: `EnterMoveMode` 에서 `bridge.ShowPlacementHighlight()`(빈 Place 타일 = 재배치 유효
   타깃; 소스는 점유라 자동 제외). `CancelMoveMode` 에서 `HidePlacementHighlight()` + `ClearPlacementRange()`.
2. **범위 프리뷰**: `UpdateScout` 에서 셀 변경 시 유효하면 `bridge.SetPlacementRange(cell, _unit)`,
   무효면 `ClearPlacementRange()`. `ClearScout` 에서도 `ClearPlacementRange()`. (드래그 배치 스카우트 미러)
3. **키링 비행(자체완결)**: 기존 `RunRelocationFlight` 의 실뷰 베지어 비행을 유지하되, 비행 동안 유닛
   위에 **고리(ring) + 줄(cord) LineRenderer** 를 띄워 키링 룩을 준다. 고리는 유닛 위 `flightRopeLength`
   (카메라 up) 지점을 부드럽게 추종(줄 각도가 흔들려 sway 느낌). 착지/중단 시 제거. 드래그 컨트롤러의
   세션/슬로모/커밋에 손대지 않는다(공유 핫파일 무수술). Shader.Find("Sprites/Default") null 가드 —
   실패 시 비주얼만 생략(유닛 비행은 유지).

## 완료 기준

- [x] 컴파일 클린
- [x] 진입 하이라이트(`ShowPlacementHighlight`)·종료 소거(`HidePlacementHighlight`) — 코드 경로
- [x] press 중 공격범위 프리뷰(`SetPlacementRange`)·무효/해제 소거(`ClearPlacementRange`) — 코드 경로
- [x] release 확정 → 슬로모 종료(기존) → 키링(고리+줄 LineRenderer) 비행 → 착지 드랍 — 코드 경로
- [x] PlayMode relocation 5/5 회귀 없음(뷰 계층 추가라 커밋 로직 불변)
- [ ] **사용자 Play 시각 확인** — 하이라이트·범위·키링 룩·드랍. 뷰 비주얼이라 자동 재현 불가(원격).

2026-07-24 자동 검증 통과 (PlayMode relocation 5/5). 사용자 시각 확인만 남음.
**주의**: 키링은 `Shader.Find("Sprites/Default"→URP/Unlit→Unlit/Color)` 폴백. 다 null 시 비주얼 생략
(아치 비행은 유지). 실기 빌드에서 셰이더 포함 여부 확인 필요.

## 개정 (2026-07-24) — 사용자 피드백 반영

1. **비행이 "평면 이동"으로 보임** → 아치를 `Vector3.up`(world) → **카메라 up** 으로 변경. Low-TopDown
   뷰에서 world-up 아치는 시선과 겹쳐 foreshorten 되어 평면처럼 보였다. 카메라-up 은 화면 세로라
   각도 무관하게 던지는 아치가 보인다(tap-to-place 키링 기준과 동일). `flightArcHeight` 기본값 1.2→1.8.
2. **이동모드 카메라 통합** → 진입 경로 무관 **줌아웃 고정 오버뷰**. 기존엔 `SetInspectFocus`(소스 줌인)
   였으나, 목적지 선택엔 보드 전체가 보여야 하므로 `SetMoveOverview()`(줌아웃) 로 교체. CameraDirector 에
   좌표 없는 config 구동 채널 신설(`SetMoveOverview`, 헤드룸 채널 미러 — dolly 음수=후퇴=줌아웃 + 스프링
   가중치). `CameraDirectionConfig.moveOverview*` 필드(코드 기본값이라 에셋 재배선 불요).

## 키링 안 보임 디버깅 (2026-07-24)

진단 테스트로 실측: 키링 LineRenderer 는 정상 생성·설정(sortingOrder 20000, Sprites/Default,
width, 좌표)·`isVisible=True` 인데도 화면에 안 보였다 → **depth 가림**. 드래그 키링 고리는 카메라
레이 위(모든 지오메트리 앞)라 안 가리지만, 이 키링은 유닛 근처(보드 깊이)라 불투명 보드/유닛 뒤로
가려진다(sortingOrder 는 투명 정렬만, 불투명 depth 는 별개). 유닛 아치(불투명)는 보이고 키링만 안 보인 것과 일치.

**수정**: 키링 머티리얼을 `Hidden/Internal-Colored`(_ZTest/_ZWrite/블렌드 프로퍼티 노출)로 +
`ZTest Always`·오버레이 큐 = **항상 위**. (Sprites/Default 는 _ZTest 프로퍼티가 없어 override 불가.)
잘못 짚은 이력: ① 셰이더 null(아님) ② sortingOrder 50→20000(정렬 문제 아님, depth 문제였음).

**빌드 주의**: `Hidden/Internal-Colored` 는 Hidden 셰이더라 빌드 스트립 가능 → 폴백 Sprites/Default 는
다시 가려질 수 있다. 실기 배포 전 전용 on-top 셰이더(ZTest Always 노출) 승격 또는 Always Included 등록 필요.

## 후속 후보

- 키링 sway 물리 고도화(현재 고리 스무스 추종 근사) / 드래그 배치 키링과 완전 통일
- 배치 스킬/어그로 반경 등 다른 색 채널 범위 표시(range-preview 백로그와 합류)
