# Handoff — dreamcatcher-placement-aura

## Commit
- (이 커밋) feat(dreamcatcher): 느린 각성 = host 스폰 오라(PlacementAura)

## Implemented
- `DcPayloadKind.PlacementAura`(kind 6, append). SelfWarmupBuff(5)는 핸들러 없는 reserved 로 잔존(H4).
- `BattleBridge.RegisterPlacementAura(axis, asPct, warmupSec)`: `_activeDcEffects`/`_activeWarmups` 에
  **등록만**(현재 유닛/host 미적용, future-only) + revocable handle 반환.
- `ApplyDreamcatcherCardToUnit` 반환 bool→int(**<0 실패 / 0 무회수 / >0 회수핸들**) + PlacementAura 분기.
- `DreamcatcherHandController.CommitUnit`: int handle 을 `_attachedTo` 에 저장 → 기존 `OnDefenderDied`
  → `RevokeDreamcatcherEffects(handle)` 경로가 host 사망 시 오라 회수(재사용).
- Card_SlowAwakening: payload kind 5→6, description 갱신. axis=All 유지.
- PlayMode `PlacementAuraTest`(2): 신규배치 부여+warmup / host·기존 미부여 / host 회수 원복 / axis 게이팅.

## Key Files
- `Assets/_Project/Scripts/Data/Dreamcatcher/DcMechanic.cs` — PlacementAura enum
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — RegisterPlacementAura, ApplyDreamcatcherCardToUnit(int)
- `Assets/_Project/Scripts/Core/Dreamcatcher/DreamcatcherHandController.cs` — CommitUnit handle 배선
- `Assets/_Project/Data/Dreamcatcher/Card_SlowAwakening.asset`
- `Assets/_Project/Tests/PlayMode/PlacementAuraTest.cs`

## Verified
- compile 클린. PlayMode PlacementAura 2/2 · 기존 Dreamcatcher/Dreamstone 8/8 · EditMode 15/15.
- 신규 파일은 assets refresh(scope=all) 후에야 테스트 러너가 인식(scripts refresh 만으론 누락) — 재발 시 주의.

## Notes
- **H4 정정**: 직전 커밋 `fe4ba372` 의 SelfWarmupBuff 핸들러가 유실돼 느린 각성이 no-op 이었음(플래키
  파일쓰기 클로버). 본 spec 이 PlacementAura 로 교체하며 실동작 확보. BattleBridge 편집마다 grep 검증함.
- 회수(H3): magnitude 1.0 중화는 mult>1 버프만 검증(느린 각성 1.5 안전). <1 디버프 오라는 후속에서 별도 검증 전 금지.
- future-only 는 `RegisterPlacementAura` 가 `_defenderByTile` 루프를 안 도는 것으로 보장(H1).

## Two-Track Review (2026-07-10)
- code-reviewer + ecs-reviewer 병렬. **양측 APPROVE** (CRITICAL/HIGH 0).
- ECS PASS: 맥락 경계(RegisterPlacementAura=Mono List, 효과는 EnqueueStatModifier 큐 경유, BattleBridge 유일 게이트웨이) /
  레지스트리 lifecycle(BeginPlacement clear) / 회수 정확성(additive 0.5→0.0 동일 merge key) / Burst 무관.
- **반영된 수정**: M1(다중 PlacementAura 핸들 누수 → 2번째 오라 스킵 가드) · L1(`_dcHandleCounter` monotonic 주석) ·
  LOW1(테스트 warmup 을 자연 배치 쿨다운 baseline 과 분리) · test-gap(회수 후 신규 배치 미부여 assert 추가).

## Follow-up
- **M2(미해결)**: host **실제 사망** → 컨트롤러 `OnDefenderDied` → revoke 경로의 아우라 PlayMode 통합 테스트.
  현재는 브릿지 `RevokeDreamcatcherEffects` 직접 호출로 계약 검증(컨트롤러 배선은 Squad unit 9 와 공유·기존). 딜 데미지·죽음 이벤트 구동 하네스 필요.
- 다른 스폰-오라 카드 일반화(디버프는 H3 선행).
- SelfWarmupBuff(5) reserved 값 정리 여부.
- 무의식 프레임 인게임 손패 확대.
