# 3 — 손패 조준 중 퇴근 잠금 · **홀드 (2026-08-19 사용자 결정)**

> **상태: 홀드. 지시 없이 착수하지 않는다.**
> 근거(사용자): **각성 버튼으로 손패를 여는 경로와 D&D 부착 방식이 현재 비활성**이다.
> 아래 도달 경로는 그 두 진입구가 살아 있을 때만 성립하므로, 지금 잠금을 넣으면 **닫혀 있는
> 문에 자물쇠를 다는 것**이다. 그 경로를 되살리는 작업이 생기면 **이 문서를 먼저 읽는다.**

## 무엇을 발견했나 (격리 코드리뷰 High 1건)

README 계약 12 rev 1 은 앞 삽입의 안전을 "퇴근은 탭·조준은 드래그라 단일 터치에서 안 겹친다"는
**말**로 보장했다. 그 전제는 코드에 근거가 없다:

- 부착 조준(`AimMode.Defender`)은 **선택과 패널을 유지하는 것이 계약**이다
  (`DreamcatcherCardDragSlot` — 선택 해제는 `_mode != Defender` 일 때만).
- 패널은 그동안 레이캐스트를 받고, 퇴근 버튼 판정(`DcInspectController.CanRetire`)에 조준 축이 없다.
- 기존 `AimingNow()` 로도 안 잡힌다 — `GameManager.IsAiming` 은 **Active 카드에만** 선다
  (`DreamcatcherCardDragSlot`: `slot.card.type == CardType.Active` 분기).

그래서 두 진입구가 살아 있으면 이 순서가 성립한다:

```
카드 드래그 시작(손패 창 안) → 퇴근 → 앞에 N장 삽입 → 그 카드가 창 밖으로 밀림
→ 드롭 → CommitAttach: 브리지 적용 **완료** → UseUnit(창 밖) false → return false
```

결과: **효과는 걸렸는데 각성 미차감 · 카드가 손패에 남아 반복 가능 · 회수 핸들 유실**
(handle>0 이면 host 사망에도 revoke 불가) · `leakAllowanceCost` 는 이미 지불됨.

## 재개할 때 할 일 (준비된 안)

1. `DreamcatcherHandView.AnyInteractionActive()`(이미 있다 — 드래그/조준이 슬롯을 소유하는가)를
   `public bool InteractionActive` 로 읽기 전용 공개. **새 상태를 만들지 않는다.**
2. `DcInspectController.CanRetire` 첫머리에서 그것이 참이면 false.
   매 프레임 피드(`TickSelectionAnchor`)에 실려 버튼 흐림이 따라오고, `OnRetirePressed` 도
   같은 함수를 지나므로 눌림 시점에도 막힌다.

## 왜 근본 수정(`CommitAttach` 롤백)이 아닌가

롤백은 "적용 → 되돌림"이 아니라 **"큐에서 먼저 확보 → 적용 → 실패 시 원래 인덱스 복원"** 으로
순서를 뒤집어야 한다(`Recover` 는 맨 뒤라 복원이 아니다). 모든 카드의 부착 경로를 건드리므로
이 spec 밖이다. 그 공백은 README 후속 후보에 **사망 트리거 손패 op 의 선행 조건**으로 남아 있다.

## 완료 기준 (재개 시)

- 손패에서 카드를 집어 든 상태에서 선택 패널의 「퇴근」이 흐려지고 눌리지 않는다.
  드롭/취소하면 즉시 활성화된다.
- `DefenderRetireTest` 전체 초록 — 이 테스트는 브리지 API 를 직접 부르므로 이 판정을 타지 않는다.
