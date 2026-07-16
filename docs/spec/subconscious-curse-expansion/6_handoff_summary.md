# 6. Handoff Summary

## Commit

- `78436ea7` docs — spec 신설 + critic 리뷰(REVISE: M2/m5) 반영
- `f5ef3b2d` feat unit 0 — 호접몽(DreamCocoon 잠 완주 저주)
- `318e09cb` feat unit 1 — 몽마의 계약(유출 허용치 선불)
- `18ddc39f` feat unit 2 — 살찌운 제물(적 표식 현상금 메커니즘)
- `9bb2ca2d` feat unit 3 — 적 타겟 드래그 + Marked 인디케이터
- `05163533` test unit 4 — 림 풀 통합 검증 + stale 스모크 실측 이관

## Implemented

- 무의식 풀 3→**6장** (호접몽·몽마의 계약·살찌운 제물). §6 규율(리스크 선불·리턴 후불) 카드 구조로 강제.
- `DcPayloadKind.DreamCocoon(14)`/`BountyMark(15)` append. 신규 NativeQueue 채널 **0**.
- 호접몽: Sleep(4s) + Effects 소유 `DreamCocoon`+`DreamCocoonSystem`(CcClear 후·CcDecay 전 핀) — 완주 시 영구 공격력 +35%, 피격 wake = 파탄(기존 wake-on-hit 재사용).
- 몽마의 계약: `DreamcatcherCard.leakAllowanceCost` + BattleBridge 런타임 오프셋 `_leakAllowancePenalty`(SO 불변, 패배 판정식 보정, 매치 리셋) + CommitAttach 선불 게이트.
- 살찌운 제물: `EnemyKilledEvent.entity` append, `ApplyBountyMark`(AwakeningReward ×3 베이크 + DmgTakenMul ×0.7), 처치/유출 드레인 → `EnemyGone` → 카드 큐 복귀. 드래그 `AimMode.EnemyMark` + `TryPickNearestEnemy`(반경 SO 노브) + `StatusFxKind.Marked` 골드 "!" 인디케이터.

## Key Files

- `Scripts/Battle/Effects/DreamCocoon(.cs|System.cs)` — 신규 ECS 쌍
- `Scripts/Bridge/BattleBridge.Dreamcatcher.cs` — preflight 2종 + DreamCocoon bake + ApplyBountyMark + EnemyGone/_bountyMarked
- `Scripts/Bridge/BattleBridge.cs` — 유출 오프셋/판정식·드레인 훅·Marked reconcile·TryPickNearestEnemy
- `Scripts/Core/Dreamcatcher/DreamcatcherHandController.cs` — 선불 게이트 + CommitMarkEnemy + OnEnemyGone
- `Scripts/UI/Dreamcatcher/DreamcatcherCardDragSlot.cs` — EnemyMark 조준/커밋
- `Data/Dreamcatcher/Card_{ButterflyDream,IncubusPact,FattenedOffering}.asset` + 카탈로그
- Tests: `DreamCocoonTest` `IncubusPactTest` `BountyMarkTest`(PlayMode) + `DreamcatcherCatalogSyncTests`(로스터 6장·에셋 계약 3종·림 통합)

## Verified

- compile 0 에러(dotnet + Unity) · EditMode 856/856(skip 2 = 기존 Testability) · 신규 PlayMode 7종 + Sleep/킬 경로 회귀 green (MCP run_tests)
- PlayMode 기존 실패 4건은 `ea155e65`(내 커밋 이전) detached 재실행으로 **main 기존 실패 확정** → backlog "PlayMode 스모크 위생" 참조
- **미검증(사용자 Play)**: 드래그→적 표식 e2e 육안, Gift 리빌에서 신규 카드 표시, 몽마 유출→패배 체감

## Notes (되돌리면 안 되는 의도)

- `DreamCocoonSystem` 의 `remaining>0` 가드 + 순서 핀(CcClear 후·CcDecay 전) = 파탄/완주 결정론의 본체. epsilon 은 보조 안전핀일 뿐.
- 유출 허용치 지불은 **SO 불변** — `deck.defeatGoalReachedCount` 직접 감소 금지(에디터 자산 오염 + 매치 간 누적).
- 표식의 DmgTakenMul 은 revoke 레지스트리 **비등록**(엔티티 수명 = 모디파이어 수명)이 의도. 적-Dreamcatcher-origin 최초 사례 — origin 기반 판정 추가 시 진영 게이트 필수(`BattleBridge.cs` empower 쿼리 주석).
- BountyMark 카드의 CommitAttach 유입은 trigger=None 가드가 무차감 거절(계약) — 이 가드에 기대는 라우팅.

## Follow-up

- unit 5 카드 아트 3종(외부 이미지 저작 필요 — cursed-relics 4_card_art 규격: 1024×1536 tarot) → 완료 시 `CompletedCardArt_HasExpectedSpriteImportContract` 확장
- 표식 전용 프리팹 연출·코쿤 잠 연출·시트 재export·유출 허용치 HUD → 본 README 후속 후보
- stale PlayMode 스모크 4건 → `docs/spec/README.md` Follow-up Backlog "PlayMode 스모크 위생"
