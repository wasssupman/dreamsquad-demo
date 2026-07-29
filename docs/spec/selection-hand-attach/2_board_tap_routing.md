# 2 — 손패 오픈 중 보드 탭 라우팅 (dismissCatcher → 선택 전환/동시 해제)

## 목적

손패 오픈 중 보드 탭의 소유자는 `HandDismissCatcher`(전화면 투명 Button, canvas order 5)다.
현행 동작(무조건 `Close()`)을 라우팅으로 확장한다: **유닛 픽 = 선택 전환 + 손패 유지**
(사용자 결정 2), **빈 보드 = 손패+선택 동시 해제**(사용자 결정 3). 계약 11 은 "동시 경쟁"
금지이므로 순차 핸드오프(손패 열림/닫힘으로 소유자 교대)로 지킨다(계약 3).

## 변경 대상

- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherHandView.cs` — catcher 클릭 핸들러
- `Assets/_Project/Scripts/UI/Dreamcatcher/DcInspectController.cs` — 전환/해제 수신부

## 구현

### A. catcher 클릭 → 이벤트로 위임

- `dcBtn.onClick` 핸들러를 교체: 클릭 좌표와 함께 `public event Action<Vector2> BoardTapped`
  를 발화. 좌표는 `Pointer.current.position.ReadValue()` (Button onClick 은 좌표를 안 주므로
  릴리즈 프레임의 포인터 위치 — press/release 가 같은 오브젝트여야 클릭이 성립하니 충분).
- **가드(계약 3)**: `AnyInteractionActive()`(드래그/포탈 조준) 이면 클릭 무시 — 포탈 출구
  탭의 릴리즈가 catcher 클릭으로 손패를 닫던 잠복 엣지를 함께 차단한다.
- 구독자 없으면(미배선/테스트) 기존 `Close()` 폴백 — 항아리 단독 오픈 사용성 보존.

### B. DcInspectController — 라우팅 수신

- `OnEnable/OnDisable` 에서 `handView.BoardTapped` 구독/해제.
- 수신: 기존 `TryPick(screenPos, out entity)` (2단 픽킹 재사용 — 계약: 픽킹 사본 금지):
  - 유닛 && `entity != _selected` → `Select(entity)` (선택 전환 — 손패는 이미 열려 있고
    `SetSelectionTarget` 만 갱신된다, unit 1)
  - 유닛 && `entity == _selected` → `Close()` (재탭 토글 문법 유지 — 손패도 닫힘, 계약 7)
  - 빈 보드 → `Close()` (손패+선택 동시 해제)
- **선택이 없는 상태**(항아리 단독 오픈)에서의 수신: 유닛 픽 = `Select`(선택 시작 + 손패
  유지 — 결정 2 와 일관), 빈 보드 = 손패만 닫기(`CloseFromSelection` 아닌 기존 `Close()`
  경로, 선택이 없으니 해제할 것도 없다).

### C. IsOverUi 는 그대로

`DcInspectController.Update` 의 raw 탭 경로는 catcher 가 활성인 동안 `IsOverUi` 히트로
자연 차단된다(unit 0 의 tap-gate 와 이중 방어) — 두 소비자가 같은 press 를 못 노린다.

## 완료 기준

- [ ] compile 클린
- [ ] Play: 손패 오픈 중 다른 유닛 탭 → 선택 전환(줌·리티클·패널·플립북·SelectionTarget 이동),
      손패 유지·재딜 없음
- [ ] Play: 손패 오픈 중 선택 유닛 재탭 → 전부 해제(토글 문법 유지)
- [ ] Play: 손패 오픈 중 빈 보드 탭 → 손패+선택 동시 해제
- [ ] Play: 항아리 단독 오픈(선택 없음) → 빈 보드 탭 = 손패만 닫힘(기존 동작), 유닛 탭 = 선택 시작
- [ ] Play: 포탈 입구 지정 → 출구 탭 → 커밋 정상 + 손패 유지(잠복 엣지 해소 확인)
