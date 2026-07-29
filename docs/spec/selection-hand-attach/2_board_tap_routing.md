# 2 — 손패 오픈 중 보드 탭 라우팅 (탭 캐처 재설계 + 선택 전환/동시 해제)

> rev 2 (2026-07-29 critic 반영): catcher 를 Button+`Pointer.current` → **press-스냅샷 탭 캐처**로
> 재설계(H1+M2 동시 해소) · 무선택 dismiss 규칙 재정의(H3) · 수신부 게이트(M6).

## 목적

손패 오픈 중 보드 탭의 소유자는 `HandDismissCatcher`(전화면 투명, canvas order 5)다. 현행
동작(무조건 `Close()`)을 라우팅으로 확장한다: **유닛 픽 = 선택 전환 + 손패 유지**(결정 2),
**빈 보드 = 손패+선택 동시 해제**(결정 3). 계약 11 은 순차 핸드오프로 지킨다(계약 3).

## 변경 대상

- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherHandView.cs` — catcher 재설계 + `BoardTapped`
- `Assets/_Project/Scripts/UI/Dreamcatcher/DcInspectController.cs` — 라우팅 수신부
- `Assets/_Project/Scripts/UI/DefenderRelocationController.cs` — `IsOverUi` 의 catcher 단독 히트 예외(M6)

## 구현

### A. catcher 재설계 — press-스냅샷 탭 캐처 (계약 3)

기존 `Button` + `onClick` 을 버리고 경량 컴포넌트(`GiftPhaseView.cs:264` TapCatcher 선례)로
교체한다. GO/레이어/sibling/활성화 시점은 기존 그대로(런타임 생성 — 씬 배선 0 유지).

```
HandDismissTapCatcher : MonoBehaviour, IPointerDownHandler, IPointerClickHandler
  OnPointerDown:  _pressBlocked = view.AnyInteractionActive() || GameManager.IsAiming;
                  (pressPosition 은 eventData 가 준다)
  OnPointerClick: if (_pressBlocked) return;                              // H1 — press 프레임 스냅샷
                  if (Distance(eventData.pressPosition, eventData.position)
                      > moveThreshold) return;                            // M2 — 스와이프 ≠ 탭
                  view.RaiseBoardTapped(eventData.position);              // eventData 좌표(계약 3)
```

- **릴리즈 시점 상태로 판정 금지**: 포탈 출구 탭은 press 프레임에 커밋되어 `IsPortalAiming/
  IsAiming` 이 릴리즈 프레임엔 이미 false 다 — 릴리즈 가드는 무효(critic H1 프레임 추적).
  press 스냅샷만이 그 릴리즈를 걸러낸다.
- `moveThreshold` 는 SerializeField(제약 6) — raw 경로의 `tapMoveThreshold 24px` 와 같은 계열.
- `AnyInteractionActive()` 는 뷰 private → 뷰가 catcher 에 자기 참조를 주입(생성 시점).

### B. `BoardTapped` 라우팅

- 뷰: `public event Action<Vector2> BoardTapped`. **구독자가 없으면 기존 `Close()` 폴백**
  (미배선/테스트 — 항아리 단독 사용성 보존).
- 컨트롤러 수신부(`OnEnable/OnDisable` 구독/해제):
  1. **첫 줄 게이트**: `if (MustClose()) return;` (critic M6 — 이동모드 중 항아리 오픈 조합에서
     1프레임 선택 플래시/lease churn 차단)
  2. `TryPick(screenPos, out entity)` (기존 2단 픽킹 재사용 — 사본 금지):
     - 유닛 && `entity != _selected` → `Select(entity)` — 선택 전환(무선택 상태 포함: 항아리
       단독 오픈에서 유닛 탭 = 선택 시작, 결정 2 와 일관)
     - 유닛 && `entity == _selected` → `Close()` (재탭 토글 — 손패도 닫힘)
     - 빈 보드 → **선택 유무 무관** `Close()` 경유 정리 + `handView.CloseFromSelection()`
       — 계약 7 "닫기 의도 탭" 규칙. 무선택이면 선택 쪽은 no-op 이고 손패만 닫힌다
       (orb-dock 바깥 탭 dismiss 보존 — critic H3 해소).

### C. 이동모드 목적지 지정 보호 (critic M6)

이동모드 중 항아리를 열면(항아리 order 7 > catcher 5) 전화면 catcher 가
`DefenderRelocationController.IsOverUi` 에 걸려 **목적지 press 가 무장되지 않는다**.
`IsOverUi` 의 RaycastAll 결과가 **catcher 단독 히트면 UI 로 치지 않는다** —
catcher 는 "보드 위 빈 공간" 의 대리이지 UI 위젯이 아니다. (수신부 게이트 B-1 이 catcher
클릭의 선택 오염을 이미 막으므로 두 소비자 경쟁은 없다.)

### D. IsOverUi(인스펙트) 는 그대로

`DcInspectController` 의 raw 탭 경로는 catcher 활성 동안 `IsOverUi` 히트 + unit 0 tap-gate 로
이중 차단 — 두 소비자가 같은 press 를 노리지 않는다(검증 완료).

## 완료 기준

- [ ] compile 클린
- [ ] Play: 손패 오픈 중 다른 유닛 탭 → 선택 전환(줌·리티클·패널·플립북·SelectionTarget 이동),
      손패 유지·재딜 없음
- [ ] Play: 손패 오픈 중 선택 유닛 재탭 → 전부 해제 / 빈 보드 탭 → 전부 해제
- [ ] Play: **항아리 단독 오픈(선택 없음)** → 빈 보드 탭 = 손패만 닫힘 / 유닛 탭 = 선택 시작
- [ ] Play: **포탈 입구 지정 → 출구 탭 → 커밋 정상 + 손패 유지 + 선택 불변**(H1 재설계 검증 —
      출구 릴리즈가 선택을 전환/해제하지 않는다)
- [ ] Play: 손패 오픈 중 보드를 길게 **스와이프**(>임계) → 아무 일도 없음(탭 오인 없음)
- [ ] Play: 이동모드 중 항아리 오픈 → 목적지 지정이 여전히 동작(C 검증), 선택 플래시 없음
