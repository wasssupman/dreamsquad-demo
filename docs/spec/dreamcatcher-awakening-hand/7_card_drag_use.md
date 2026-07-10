# 7 — Unit/Squad 카드 스와이프 사용

> rev 4 (2026-07-10): **확정 지연(pending) 제거** — touchup 즉시 커밋. 아래 §4~5 의 pending 규칙은 은퇴, "Recovered 재렌더 deferral" 만 드래그/2탭 기준으로 존치. 유닛 호버 하이라이트는 `SetPlacementHighlightAboveUnits(true)` 동반 필수(유닛 스프라이트에 가림).

## 목적

손패 카드를 스와이프해 사용하는 인터랙션의 본체: Unit 타입 = 유닛 위 touchup 부착(하이라이트), Squad 타입 = 아무 영역 touchup, 손패 영역 복귀 = 취소, touchup 즉시 커밋. Active 타입 사용은 unit 8.

## 변경 대상

- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherCardDragSlot.cs` (신규 — `DefenderDragSlot` 패턴)
- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherHandView.cs` (드래그 연동 + pending 표시)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (defender 셀 조회 공개 API 1개 — 필요 시)

## 구현

1. **드래그 세션**: 카드 아이템에 `IBeginDrag/IDrag/IEndDrag`. 시작 조건 = `CanUse(entryId)`(dim 카드 차단). 드래그 중 카드가 포인터를 따라감(원본 슬롯 반투명). Active 타입 카드는 이 unit 에서는 드래그 비활성(unit 8 에서 활성).
2. **Unit 타입 타겟팅**: 포인터 스크린 → 월드 → 셀 변환(기존 D&D 배치 변환 경로 재사용) → bridge `TryGetDefenderAt(cell, out Entity)`(`_defenderByTile` 래핑, 신규 공개 메서드) → 유닛 존재 + 부착 상한 미만이면 **하이라이트**(기존 tile hover highlight 재사용). touchup:
   - 유닛 위 → pending 진입(아래 4).
   - 유닛 없는 곳 → 취소(원위치 트윈).
3. **Squad 타입**: 하이라이트 없음. touchup 이 **손패 뷰 영역 밖**이면 pending 진입, 영역 안이면 취소.
4. **확정 지연 (pending, 뷰 전용)**: touchup 시 카드가 대상 위치에 떠서 `confirmDelaySec` 카운트다운(링/게이지 표시, **실시간 기준 — 슬로모 무영향**, critic L1). 이 동안 카드 탭 = 취소(손패 복귀, 무차감). 만료 시 **커밋**: `CommitUnit(entryId, entity)` / `CommitSquad(entryId)` 호출 → 성공 시 자동 복귀(unit 6), 실패(대상 소멸 등) 시 손패 복귀·무차감. `confirmDelaySec=0` 이면 즉시 커밋. **pending 중 손패는 열린 채(슬로모 유지), 시뮬 반영은 커밋 시점만**(기획 문서 §11). **pending 규칙 3건**: ① pending 활성 entryId 는 손패 스트립에서 잠금(소진 표시 + 드래그 차단 — `HandChanged(Recovered)` 재렌더에도 유지, 이중 커밋 방지, critic M4) ② pending 중 손패 토글 = pending 취소 후 닫기(critic H1, unit 6 §8) ③ pending 중 phase 이탈 = 드롭·무차감(critic H2, unit 6 §9). 동시 pending 은 1건만(드래그 세션이 단일이므로 자연 보장).
5. **pending 중 대상 사망(Unit)**: 커밋 시점 `CommitUnit` 이 entity 유효성 검증에서 실패 → 취소 처리와 동일(무차감).
6. **취소 규칙(공통)**: 손패 뷰 rect 안 touchup = 취소. ESC/포커스 손실 = 취소. 취소·무효 드롭은 차감·순환·로그 없음.
7. **워밍업 카드 확인**: Squad `placementWarmupSec` 카드(느린 각성)가 실시간 사용에서도 기존 `ApplyDreamcatcherCard` 경로로 동작하는지 확인만(코드 무변경 기대).

## 완료 기준

- [ ] Unit 카드: 드래그 → 유닛 하이라이트 → touchup → pending 카운트다운 → 커밋 → 부착 + 게이지 -15 + 다음 카드 슬라이드 + 자동 복귀.
- [ ] pending 중 탭 취소 → 손패 복귀, 무차감. pending 중 대상 사망 → 무차감 복귀.
- [ ] Squad 카드: 필드 아무 곳 touchup → pending → 커밋 → 축 버프 + 게이지 -30 + 큐 맨 뒤.
- [ ] 손패 영역 되돌림/유닛 없는 곳(Unit) → 취소.
- [ ] 콘솔 에러/워닝 0.

> 확인 2026-07-10 — 커밋 ddaf08f6 → rev 4 계열 7a9aed09(확정지연 제거)·c9d02e2e/784faf23/5a6146c9(호버 검출·붉은 틴트)·4ddb21d9(네임 밴드)·05e78af6(StS 화살표) — 사용자 Play 확인
