# 4 — 드림캐쳐 카드 (즉발 · hand-op)

## 목적

카드 32행 중 **실행 클래스가 다른 두 종류**를 처리한다.
`3_cards_slot_arm.md` 와 나눈 이유는 이 둘이 슬롯을 안 타기 때문이다.

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.Dreamcatcher.cs` — 즉발 5행 경로
- `Assets/_Project/Scripts/Core/Dreamcatcher/DreamcatcherHandController.cs` — hand-op 실행자
- `Assets/_Project/Scripts/Skills/Concrete/`

## 구현

1. **즉발 5행**(`trigger = None`) — 슬롯 없이 **부착 즉시** 실행한다.
   예: 호접몽(`DreamCocoon`) · `BountyMark`. 감지자를 안 거치므로 디스패처 3지점이 아니라
   **부착 지점에서 직접 `Execute`** 한다. 「호출자 = 소유자」의 가장 단순한 형태다.
   ⚠ 부착 경로는 **요청-응답**이다 — `ApplyDreamcatcherCardToUnit` 이 부착 코드(-1 = 무차감
   거절)를 반환해 **코스트 환불**을 결정한다(`BattleBridge.Dreamcatcher.cs:310~441`).
   `Execute` 는 void 이므로 **가부 판정은 스킬 밖**(기존 `DcApplicability` preflight — 이미 순수)에
   유지하고 `Execute` 는 발동만 담당한다. 토대 unit 0 의 판정을 따른다.
2. **`RecallAttachedToFront`(hand-op)** — 실행자가 sim 도 브리지도 아니라
   `DreamcatcherHandController`(Mono)다. `DcMechanic.cs` kind 25 주석이 그것을 명시한다.
   **판정 필요**: Mono 계열 intent 로 표현할지, 구조적 예외로 명문화할지.
   예외로 두면 §「범위 밖」에 근거와 함께 적는다 — 「아직 안 옮김」은 예외가 아니다.
3. **`RegisterPlacementAura` 의 revoke 핸들** — host 사망 시 `RevokeDreamcatcherEffects(handle)`
   가 회수한다. fire-and-forget `Emit` 으로 표현 불가. 별도 포트 메서드 또는 예외로 판정
   (토대 unit 0).

## 완료 기준

- [ ] 즉발 5행이 concrete 로 존재하고 부착 지점에서 직접 `Execute` 된다
- [ ] 부착 가부/코스트 환불이 **스킬 밖**에 남아 있고 동작이 바뀌지 않았다
- [ ] hand-op 이 이전됐거나, 예외로 **근거와 함께** 명문화됐다
- [ ] PlacementAura 회수가 동작한다 (host 사망 → 효과 소멸 PlayMode 단언)
- [ ] 그물 초록
