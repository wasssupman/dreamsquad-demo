# 4 — 리티클 세션 핸드오프 (재주장 신호 확장)

> rev 2 (2026-07-29 critic 반영): 재주장 트리거를 `NotifyInteractionEnded` 단독 → **세션 강제
> 종료 신호 전체**로 확장(H2) · `Close()` 내부 순서 교정 · 재주장 조건을 코드 표현으로 명시
> (`!TapGated()` 오독 차단) · Confirm 서술 교정(M5는 unit 3 이 소유).

## 목적

`DreamcatcherFocusPresenter` 는 단일 세션이다(계약 6). 선택 리티클(`AimKind.Selected`)이 떠
있는 상태에서 남(카드 조준, 손패 닫힘)이 세션을 대체/종료하면 — 선택이 살아 있는 한 —
리티클을 **재주장**한다.

**깔때기 하나로는 부족하다(critic H2)**: `Focus.End()` 호출처는 슬롯 종료(커밋/취소) 외에도
`DreamcatcherHandView.Close()`(항아리 토글·0장 자동 닫힘), `ForceClose()`(페이즈·Reset),
슬롯 `OnDisable`(침강 완료 시 패널 비활성화마다) 이 있고, 뒤의 셋은 `NotifyInteractionEnded`
를 지나지 않는다. 계약 7 이 "선택 유지"로 규정한 경로에서 리티클만 죽고 되살릴 트리거가 없다.

## 변경 대상

- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherHandView.cs` — `FocusCleared` 이벤트 + `Close()` 순서 교정
- `Assets/_Project/Scripts/UI/Dreamcatcher/DcInspectController.cs` — 재주장 수신부

## 구현

### A. 뷰 — 세션 종료 신호 단일화

- `public event Action FocusCleared` — 뷰가 `_focus.End()` 를 호출하는 모든 지점
  (`Close()`, `ForceClose()`) 에서 **End 직후** 발화.
- **`Close()` 내부 순서 교정**: `_focus?.End()` 를 `CancelAllCardInteraction()` **앞**으로
  옮긴다. 현행 순서(취소→End)는 취소 깔때기가 발화시킬 재주장을 같은 함수가 곧바로 지운다.
- 슬롯 `OnDisable → EndInteraction` 경로: 패널 비활성(침강 완료/teardown)마다 실행되는데
  `NotifyInteractionEnded` 를 부르지 않는다. 이 경로는 항상 `Close()/ForceClose()` 가 선행하므로
  **A 의 `FocusCleared` 가 이미 커버** — 슬롯 코드는 만지지 않는다(변경 최소).
- `NotifyInteractionEnded()` 에서 `public event Action InteractionEnded` 발화(기존 깔때기 —
  커밋/취소/ESC).

### B. 컨트롤러 — 재주장 수신부

`InteractionEnded` + `FocusCleared` 둘 다 같은 수신부로 구독한다. 재주장 조건은 **코드 표현
그대로**(critic Ambiguity — `!TapGated()` 로 구현 금지: `State == Hand` 가 포함돼 손패 열린
동안 재주장이 영영 안 돈다):

```
void OnFocusSessionReleased()
{
    if (_selected == Entity.Null) return;
    var drag = defenderSelector != null ? defenderSelector.DragController : null;
    if (GameManager.Instance != null && GameManager.Instance.IsAiming) return; // 포탈 2탭 대기 중
    if (drag != null && drag.IsAiming) return;
    if (!bridge.TryGetDefenderCell(_selected, out var cell)) return; // 사망 — unit 1 liveness 가 정리
    ShowSelectionReticle(_selected, cell); // Select 의 리티클 블록과 공유(중복 제거 추출)
}
```

- 포탈 2탭 대기(`IsAiming` 유지) 중 커밋 깔때기가 오는 경우는 위 가드가 거르고, 출구 커밋
  완료 시 `EndInteraction → NotifyInteractionEnded` 가 다시 와서 그때 재주장된다.
- **폴링 재주장 금지** — `BeginSelection` 은 `Begin` 경유라 매 프레임 부르면 리티클 pop 이
  매 프레임 재생돼 깨진다. 이벤트 1회 재주장만.
- `_reticleShown` 은 `FocusCleared` 수신 시 false 로 리셋 — 남이 끝낸 세션에 컨트롤러
  `Close()` 가 뒤늦게 `End()` 를 쏘는 stale 상태를 없앤다.
- 확정 펄스는 재주장과 공존한다(독립 타이머, `End()` 후 완주 — 기존 계약). 펄스 중심 캡처
  시점 문제는 unit 3 B 가 소유.

## 완료 기준

- [ ] compile 클린
- [ ] Play: 선택 → 카드 D&D 로 **다른** 유닛에 부착 → 커밋 후 선택 유닛 위 리티클 복귀
      (콜아웃 이름 포함, 가로질러 날아오지 않고 pop)
- [ ] Play: 선택 → 카드 드래그 취소(손패로 복귀/ESC) → 리티클 복귀
- [ ] Play: 선택 → 카드 탭 즉발(unit 3) → 리티클 복귀
- [ ] Play: **선택 중 항아리 탭으로 손패만 닫기** → 리티클이 살아난다(FocusCleared 경로 — H2 핵심)
- [ ] Play: **마지막 카드 커밋(0장 자동 닫힘)** → 침강 완료 후에도 리티클 유지(슬롯 OnDisable 경로)
- [ ] Play: 선택 → 포탈 입구+출구 커밋 → 리티클 복귀(2탭 대기 중에는 재주장 안 함)
