# 1. Dormant 3벌 은퇴 — 구 3중1 + SkillBar 완전 삭제

## 목적

awakening-hand 가 dormant 로 남겨둔 구 사용 방식 코드 3벌을 삭제한다. 새 각성 손패가
실플레이 검증("플레이 감각 좋음", 2026-07-10)을 통과했고 unit-dreamcatcher-icons 까지
그 위에 얹힌 지금이 삭제 적기다.

## 변경 대상

**클래스 삭제 (+.meta)**
- `Assets/_Project/Scripts/Core/Dreamcatcher/DreamcatcherController.cs`
- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherSelectionView.cs`
- `Assets/_Project/Scripts/UI/SkillBar.cs`

**BattleScene 오브젝트 삭제 (UnityMCP, 전용 GO 3개 — Transform+스크립트뿐)**
- `Dreamcatcher` (GO active, 컴포넌트 disabled) · `DreamcatcherSelectionView` (GO inactive) ·
  `SkillBar` (GO inactive; skillRuntime/draftController 참조는 GO 와 함께 소멸)

**BattleBridge.Dreamcatcher.cs — 구독자 0 이 되는 이벤트 삭제**
- `FirstDefenderPlaced` · `WaveMilestoneReached` · `_firstDefenderPlacedFired` ·
  `FireFirstDefenderPlacedOnce` + 본체의 호출부 3곳(웨이브 마일스톤 invoke 1 + 배치 훅 2)

**테스트 이관/정리**
- `DreamcatcherDeckCarryInTest` — `DreamcatcherController.ResolveDeck` 대상 →
  `DreamcatcherHandController.ResolveAttachDeck` 로 이관(동일 reflection 패턴, 검증 가치 유지:
  저장덱 해석 + 폴백)
- `DreamcatcherEffectTest` — ① bridge 직접 구동 테스트는 유지, `NeutralizeSceneController`
  헬퍼/호출만 제거 ② 3중1 auto-pick 테스트(`EnteringPlacement_TriggersController_AutoPicksAndApplies`)
  는 은퇴 플로우 전용 → 삭제
- `PlacementAuraTest` / `ActionLockTest` / `DreamcatcherCombatDamageTest` /
  `DreamstoneCarryInSmokeTest` — 씬 컨트롤러 중화 블록 제거(참조 대상 소멸)

**주석 정리(선택)**: `DreamcatcherHandController` 의 "dormant controller" 참조 주석 2곳 현행화.

## 삭제하지 않는 것

- `SkillRuntime` 클래스/씬 컴포넌트 — Active 캐스트 API 의 `skillRuntime?.` 가드와 얽힘.
  후속 후보로 이관.
- `SkillLoadoutController` — Active 2장 롤 소스로 계속 사용 중.
- `BattleLogger.RecordDreamcatcherOffer/Pick` + 스키마 — 계약 5.

## 완료 기준

- [ ] compile 클린 + 삭제 클래스 참조 grep 0건 (주석 제외)
- [ ] BattleScene 저장 후 missing script 0 (UnityMCP 검사)
- [ ] EditMode 그린 + 이관/수정된 PlayMode 5종 그린
- [ ] Play smoke: 배치→전투 진입, 손패 열기/카드 사용 정상 (기존 경로 무회귀)
