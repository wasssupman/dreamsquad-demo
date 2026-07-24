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
**주의**: 키링은 `Shader.Find("Sprites/Default")` null 시 비주얼 생략(비행은 유지). 실기 빌드에서
셰이더 포함 여부 확인 필요(미포함 시 Always Included Shaders 등록 or SerializeField Material 로 승격).

## 후속 후보

- 키링 sway 물리 고도화(현재 고리 스무스 추종 근사) / 드래그 배치 키링과 완전 통일
- 배치 스킬/어그로 반경 등 다른 색 채널 범위 표시(range-preview 백로그와 합류)
