# 2. Handoff Summary — dreamcatcher-bridge-partial-cleanup

## Commit

- unit 0 `cc4f26b9` refactor(dreamcatcher): BattleBridge 드림캐쳐 번역자 partial 분리
- unit 1 `bed30057` cleanup(dreamcatcher): 구 3중1/SkillBar dormant 3벌 은퇴 (−1,063줄)

## Implemented

- `BattleBridge.Dreamcatcher.cs` (partial) 신설 — 드림캐쳐 카드 번역자 전체
  (ActiveDcEffect 레지스트리 · Hosted/ToUnit apply · Revoke · PlacementAura ·
  MapDcEffect/MatchesDcAxis). 순수 이동, 시그니처/경계 불변.
- 픽킹 유틸·드림스톤 블록·BakeNightmareMechanics 는 본체 잔류 (경계 근거는 `0_bridge_partial.md`).
- 구 3중1(DreamcatcherController + SelectionView)·SkillBar 클래스/씬 GO 완전 삭제.
- 구독자 0 이 된 `FirstDefenderPlaced`/`WaveMilestoneReached` 이벤트 체인 제거.
- 덱 반입 회귀 테스트를 `DreamcatcherHandController.ResolveAttachDeck` 로 이관.
- 삭제 코드를 현재형으로 서술하던 주석 5곳 현행화.

## Key Files

- `Assets/_Project/Scripts/Bridge/BattleBridge.Dreamcatcher.cs` — 새 카드 부류/payload 작업은 여기
- `Assets/_Project/Scripts/Core/Dreamcatcher/DreamcatcherHandController.cs` — 유일한 드캐 사용 진입점
- `Assets/_Project/Tests/PlayMode/DreamcatcherDeckCarryInTest.cs` — 이관된 테스트

## Verified

- compile 클린 · EditMode 678/678 (스킵 2 = 기존 의도적 ignore)
- PlayMode 대상 6클래스 그린. DreamstoneCarryInSmokeTest 2건이 배치 실행에서 PrimeTween
  "OnComplete ignored" 로 실패 → 단독 재실행 4/4 통과 = **기존 순서의존 플레이크**(씬 전환을
  넘는 트윈 누수), 이번 변경 무관.
- BattleScene 저장 후 validate: missing script 2건(MapView/DraftView)은 **기존부터 존재**
  (DreamcatcherEffectTest 주석에 pre-existing 으로 기록돼 있던 것) — 이번 삭제 유발 아님.
- 삭제 클래스 참조 grep: 코드 0건 (역사 서술 주석만 잔존).

## Notes

- `BattleBridge` 는 이제 partial 2파일 — 본체는 `partial class` 선언. 새 파일 추가 시 .meta 짝 커밋.
- 비활성 GameObject 는 UnityMCP manage_gameobject 로 못 지운다(by_id/by_name/by_path 전부) —
  일회용 MenuItem 스크립트(transform.Find)로 우회하고 실행 후 스크립트 삭제.
- `skillRuntime` SerializeField(BattleBridge)는 여전히 미배선 유지 — Active 캐스트의 쿨다운
  게이트 무력화 계약(awakening-hand C1). 되돌리지 말 것.

## Follow-up

- `SkillRuntime` 클래스/씬 컴포넌트 은퇴 검토 (소비자 0, 단 `skillRuntime?.` 가드 확인 선행)
- `BattleLogger.RecordDreamcatcherOffer/Pick` + 스키마 정리 (순환/사용 이력 로깅 대체 시)
- payload kind 디스패치 테이블화 임계점 (~12종) / Effects stackId-remove 프리미티브 (리뷰 권고 2·3)
- DreamstoneCarryInSmokeTest 순서의존 트윈 플레이크의 구조적 마감 (suite-level tween teardown)
