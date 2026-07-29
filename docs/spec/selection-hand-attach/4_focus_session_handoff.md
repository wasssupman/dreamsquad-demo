# 4 — 리티클 세션 핸드오프 (조준 ↔ 선택 재주장)

## 목적

`DreamcatcherFocusPresenter` 는 단일 세션이다(계약 6). 선택 리티클(`AimKind.Selected`,
inspect unit 6)이 떠 있는 상태에서 카드 드래그가 `BeginFocus`(AttachAim 등)로 세션을
대체하고, 종료 시 `EndInteraction → Focus.End()` 가 리티클을 통째로 끈다 — 선택은 살아
있는데 리티클만 사라진다. 인터랙션이 끝나면 선택 리티클을 **재주장**한다.

## 변경 대상

- `Assets/_Project/Scripts/UI/Dreamcatcher/DcInspectController.cs`

## 구현

- 재주장 트리거 = `DreamcatcherHandView` 의 인터랙션 종료 시점. 슬롯의 모든 종료 경로
  (커밋/취소/ESC/OnDisable)는 `_view.NotifyInteractionEnded()` 깔때기를 지난다 — 여기서
  `public event Action InteractionEnded` 를 발화하고 `DcInspectController` 가 구독한다.
- 수신부: `_selected != Entity.Null && !TapGated-드래그류` 이면 `Select` 의 리티클 블록과
  동일 로직(`TryGetDefenderCell → BeginSelection`, `_reticleShown = true`)을 재실행.
  전용 private 메서드로 추출해 `Select` 와 공유(호출처 2 — 제약 10 (b) 충족).
- **폴링 재주장 금지** — `BeginSelection` 은 `Begin` 경유라 매 프레임 부르면 리티클 pop
  (`_reticleInit` 리셋)이 매 프레임 재생돼 깨진다. 이벤트 1회 재주장만.
- 커밋 성공 직후의 순서: `CommitNow` → `Focus.Confirm()`(펄스 캡처) → `End()` →
  `NotifyInteractionEnded` → (이벤트) → `BeginSelection` 재주장. 펄스는 독립 타이머라
  재주장과 공존(기존 계약 "End 후에도 완주").
- 선택 유닛이 그 인터랙션으로 죽었으면(`TryGetDefenderCell` 실패) 재주장 생략 — 기존
  사망 닫힘 경로(`OnAttachmentsChanged`/앵커 소실)가 정리한다.

## 완료 기준

- [ ] compile 클린
- [ ] Play: 선택 → 카드 D&D 로 **다른** 유닛에 부착 → 커밋 후 선택 유닛 위 리티클 복귀
      (콜아웃 이름 포함, 화면 가로질러 날아오지 않고 pop)
- [ ] Play: 선택 → 카드 드래그 취소(손패로 복귀/ESC) → 리티클 복귀
- [ ] Play: 선택 → 카드 탭 즉발(unit 3) → 확정 펄스 후 리티클 복귀
- [ ] Play: 선택 → 포탈 입구+출구 커밋 → 리티클 복귀(IsAiming 해제 후)
