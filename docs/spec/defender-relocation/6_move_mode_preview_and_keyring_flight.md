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
3. **키링 비행(배치 키링 재사용)**: 실뷰 베지어 비행(`SetRelocationViewOverride`)은 유지하되, 비행 동안
   **배치(D&D/탭)와 동일한 고리+줄**을 유닛 머리 위에 얹는다. 룩(빌보드 고리 + 월드 줄, 스타일/머티리얼/
   색/폭)은 `DefenderDragPlacementController.CreateKeyringHardware(unit)` 팩토리가 **단일 소유**(배치
   프리뷰의 고리/줄 구성과 같은 소스 = `DragSwaySettings`·`Sprites/Default`·`Billboard`). 실루엣은 재배치의
   실제 유닛이 담당하므로 팩토리는 고리+줄만 만든다. 위치 앵커 = 실제 렌더 유닛 뷰의 `transform.position`
   (= `SpineUnitView` 가 `BoardSpace.ToView` 적용을 마친 좌표) → 고리는 그 위 `head + ropeWorld` 를 스무스
   추종(sway 근사). 착지/중단 시 root 만 파괴(머티리얼은 드래그 컨트롤러 공유 — 파괴 금지).

## 완료 기준

- [x] 컴파일 클린
- [x] 진입 하이라이트(`ShowPlacementHighlight`)·종료 소거(`HidePlacementHighlight`) — 코드 경로
- [x] press 중 공격범위 프리뷰(`SetPlacementRange`)·무효/해제 소거(`ClearPlacementRange`) — 코드 경로
- [x] release 확정 → 슬로모 종료(기존) → 키링(고리+줄 LineRenderer) 비행 → 착지 드랍 — 코드 경로
- [x] PlayMode relocation 5/5 회귀 없음(뷰 계층 추가라 커밋 로직 불변)
- [ ] **사용자 Play 시각 확인** — 하이라이트·범위·키링 룩·드랍. 뷰 비주얼이라 자동 재현 불가(원격).

2026-07-24 자동 검증 통과 (PlayMode relocation 5/5). 사용자 시각 확인만 남음.
**주의**: 키링 룩은 배치 키링과 단일 소스(`CreateKeyringHardware`) — 별도 셰이더/빌드 리스크 없음
(배치 프리뷰가 이미 쓰는 `Sprites/Default`). `DragController` 미해석(트레이 미준비) 시 키링만 생략,
유닛 비행은 유지. 개정 2 참조.

## 개정 (2026-07-24) — 사용자 피드백 반영

1. **비행이 "평면 이동"으로 보임** → 아치를 `Vector3.up`(world) → **카메라 up** 으로 변경 시도.
   (**개정 3 에서 폐기** — camera-up 만으론 안 됐다. 진짜 원인은 sim 공간 아치를 `BoardSpace.ToView`
   가 통째로 버린 것. sim 이 아니라 **view 공간**에서 곡선을 태워야 한다. 개정 3 참조.)
2. **이동모드 카메라 통합** → 진입 경로 무관 **줌아웃 고정 오버뷰**. 기존엔 `SetInspectFocus`(소스 줌인)
   였으나, 목적지 선택엔 보드 전체가 보여야 하므로 `SetMoveOverview()`(줌아웃) 로 교체. CameraDirector 에
   좌표 없는 config 구동 채널 신설(`SetMoveOverview`, 헤드룸 채널 미러 — dolly 음수=후퇴=줌아웃 + 스프링
   가중치). `CameraDirectionConfig.moveOverview*` 필드(코드 기본값이라 에셋 재배선 불요).

## 개정 2 (2026-07-24) — 키링 재설계: 배치 키링 재사용

사용자 피드백: 자작 키링이 "배치/탭 키링과 전혀 다르게 생겼고 위치도 엉뚱한 데서 움직인다".
근본 원인 2개:
1. **룩 불일치**: 자작 = raw 월드 LineRenderer 2개 + `Hidden/Internal-Colored`+`ZTest Always` 해킹.
   기존 배치 키링 = 빌보드 고리(로컬 원 14세그) + 월드 줄, `Sprites/Default`, `DragSwaySettings` 색/반경/폭,
   머리 위로 부양해 depth 자연 통과. → 완전히 다른 물건이었다.
2. **위치 오류**: `UpdateKeyring(p)` 가 sim 좌표 `p` 를 world 로 직접 사용(`BoardSpace.ToView` 누락).
   실제 렌더 유닛은 `ToView(p)+offset` 에 있는데 키링은 sim 공간에 그려져 어긋났다.

**재설계**: 키링 룩을 `DefenderDragPlacementController.CreateKeyringHardware(unit)` 팩토리로 이관 —
배치 프리뷰의 고리/줄 구성과 **단일 소스**. 재배치는 팩토리 산출물을 받아 **실제 유닛 뷰의
`transform.position`**(ToView 완료) 위에 매 프레임 얹는다. ZTest 해킹 제거(배치 키링과 같은
`Sprites/Default` — **빌드 안전**, Hidden 셰이더 스트립 우려 소멸). 색/반경/폭은 `DragSwaySettings` 공유,
재배치 전용 knob 은 `flightRingFollow`(sway) 만 남김(`RelocationSettings` 의 flightRing*/flightCord*/
flightKeyring*/flightRopeLength 제거 — orphan YAML 키는 무해).

## 개정 3 (2026-07-24) — 비행 곡선을 탭 배치와 동일 로직 공유

사용자 피드백: "이동이 아직도 평면이동이다. 탭투플레이스 비행 시뮬 로직 살펴보고 동일 로직 공유하는 형태로".

**진짜 근본 원인**: `BoardSpace.ToView(sim)` 는 **sim 높이(y)를 완전히 버린다**(보드 = 평면 정면뷰 —
`cx=simX, cy=simZ` 만 사용). 재배치 비행은 `SetRelocationViewOverride(sim)` → SyncMonoUnitViews 가
`ToView` 적용 → **아치의 수직 성분이 소실** → 평면. camera-up/world-up 은 무관했다(둘 다 sim 에 넣으면
ToView 가 죽임). 반면 tap 은 프리뷰 transform 을 **view 공간에 직접** 배치(ToView 우회)라 아치가 산다.

**수정**:
- 비행 곡선 계산을 순수 함수 `KeyringSim.ThrowArcControls`(arcHeight 배수·좌우 변주·상승/하강 접선)로
  추출 — tap(`RunSimulatedDrag`)과 재배치가 **공유**(EditMode 테스트로 고정). tap 은 이 함수로 리팩터(동작 불변).
- 튜닝은 `DragSwaySettings`(tapArc*/tapThrow*) 단일 소스 — `DefenderDragPlacementController.ComputeThrowArc`
  가 공급. `RelocationSettings.flightArcHeight` 제거.
- 비행을 **view 공간**으로 이관: `TryGetRelocationAnchors` 가 view 좌표(ToView 적용) 반환, 컨트롤러가
  view 공간에서 곡선 샘플, override 를 **view 좌표**로 저장. SyncMonoUnitViews 는 override 시 `ToView`
  재적용 없이 `SpineUnitView.SetFlightView`(직배치 + 전경 소팅)로 그리고 게이지/오버헤드는 생략(비전투 이동).

검증: 컴파일 클린, EditMode 12/12(ThrowArcControls 2 신규), PlayMode relocation 5/5.

폐기 이력: ZTest Always(Hidden/Internal-Colored)·sortingPriority 튜닝은 depth 우회 시도였으나, 애초에
"자작 키링"이라는 방향이 틀렸다 — 기존 것을 재사용하니 depth·룩·빌드안전이 한 번에 해소.

## 후속 후보

- 키링 sway 물리 고도화(현재 고리 스무스 추종 근사)
- 배치 스킬/어그로 반경 등 다른 색 채널 범위 표시(range-preview 백로그와 합류)
