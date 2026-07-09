# Unit 14 — 테스트 + PlayMode smoke + handoff + 이식 가이드 갱신

## 목적

히트 구동 재설계의 회귀를 고정하고, 행동 변화(근접→히트)가 의도임을 확인하며, 인계 문서를 남긴다.

## 변경 대상

- (신규) `Assets/_Project/Tests/EditMode/AggroPolicyTests.cs` (Unit 9 에서 착수, 여기서 시스템 테스트 보강)
- (신규) `Assets/_Project/Tests/EditMode/AggroStateSystemTests.cs`
- (신규) `docs/spec/aggro-targeting/15_hit_driven_handoff.md`
- `docs/reference/` (아그로 이식 가이드 — 선택)

## 구현

**EditMode 순수함수 테스트**(Unit 9): `CanAcquire`/`SelectTargets` 경계.

**EditMode 시스템 테스트**(critic M5/잔여1 — 핵심):
- 여유 1슬롯 가디언 + 같은 틱 2 `AggroHitEvent` → `Aggroed` 정확히 1개, `held == capacity`.
- 2 가디언이 같은 적 히트 → 1개만(선점).
- 가디언 `DeadTag` → 링크 적 `Aggroed` 제거.

**PlayMode / 육안 smoke**(critic 잔여2): 근접→히트 전환의 행동 변화 확인 —
- 가디언 사거리 안으로 적이 들어와도 **가디언이 공격해 명중하기 전엔 안 끌림**(회귀 아님, 의도).
- 명중 후 적이 가디언 타일로 걸어와 겹침 → 가디언만 공격 → 가디언 죽으면 흩어짐.
- capacity 초과분은 명중해도 데미지만(어그로 X).
- 아이콘 머리 위 표시/해제.

**Handoff**(`15_hit_driven_handoff.md`): 커밋 해시 · Implemented · Key Files · Verified(compile/test/Play) · Notes(근접→히트 전환은 되돌리면 안 됨, held full-recompute, 드레인 프로토콜) · Follow-up(투사체 가디언, 도발 에픽 가디언).

## 완료 기준

- [ ] EditMode 전부 green(H1 시스템 테스트 포함).
- [ ] PlayMode smoke 시나리오 사용자 육안 통과(스크린샷).
- [ ] handoff 작성. reference 이식 가이드에 아그로 항목 반영(선택).
- [ ] `docs/reference/object-pipeline-map.md` 에 아이콘(오버헤드 View) 정거장 반영 필요 여부 확인.
