# 5 — Handoff Summary (C 완료)

C(`ingame-dreamcatcher`) MVP 종료. 최신 계약은 README + 번호 문서 우선.

## Commit

- spec `1b07c06`
- 0 `e6a4f8f` — 유닛 class 라벨
- 1 `7feae1a` — 카드 데이터 + 기본 덱
- 2 `823b7ea` — 효과 적용(현재·미래) + modifier-framework 버그 수정
- 3 `7f60bc7` — 선택 흐름 트리거 + 컨트롤러
- 4 `abec3fb` — 선택 UI 모달 + BattleScene 배치

## Implemented

- `DefenderClass`(Ranger/Guardian/Bruiser/Caster/Support) + `DefenderUnitData.role` 15유닛 백필.
- `DreamcatcherCard`(axis + CardEffect[]) / `DreamcatcherDeck` + 6 카드 + 고정 10장 덱(`DreamcatcherDeck_Default`).
- BattleBridge `_activeDcEffects` registry: `ApplyDreamcatcherCard` → 현재 매칭 유닛 즉시, `CreateDefenderEntity` 훅으로 미래 유닛 상속. 매치 영구(1e9), stackId 100~ 중복 스택, `BeginPlacement` 리셋.
- 효과 4채널: ATK→DamageMul, AS→AttackSpeedMul, **HP→DmgTakenMul 프록시**, Move→MoveSpeedMul. `EnqueueDmgTakenMul` 추가.
- 트리거: `FirstDefenderPlaced`(1회) + `WaveMilestoneReached`((wave+1)%5==0). `DreamcatcherController` 구독 → Draw3 → 모달/폴백.
- `DreamcatcherSelectionView` 런타임 3카드 모달, 선택 중 timeScale=0 → 선택 후 1.

## Key Files

- `Assets/_Project/Scripts/Data/DefenderClass.cs`, `Data/Dreamcatcher/{DreamcatcherCard,DreamcatcherDeck}.cs`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (registry/apply/trigger; `ApplyDreamcatcherCard`, `FireFirstDefenderPlacedOnce`, QueueWave)
- `Assets/_Project/Scripts/Battle/Effects/Modifiers/ModifierApplySystem.cs` (bufferless AddBuffer 수정)
- `Assets/_Project/Scripts/Core/Dreamcatcher/DreamcatcherController.cs`, `UI/Dreamcatcher/DreamcatcherSelectionView.cs`
- 에셋: `Data/Dreamcatcher/*` (6 카드 + 덱), BattleScene `Dreamcatcher` GameObject
- 테스트: `Tests/PlayMode/DreamcatcherEffectTest.cs` (효과/트리거 2개)

## Verified

- PlayMode 6/6 (효과 현재 스택1.21·HP0.87·축필터·미래상속1.21 / 첫배치 트리거·자동선택·once-guard / 모달 Play 검증).
- EditMode 294(292+2skip) — modifier-framework 수정 회귀 없음.
- Play(MCP): 첫 배치 → 모달(3장, timeScale=0) → 탭 → 적용 + 재개.

## Notes (되돌리지 말 것)

- **modifier-framework 수정 필수**: `ModifierApplySystem` bufferless 경로 `ecb.AddBuffer`→`em.AddBuffer`. 한 프레임에 갓 배치된 유닛에 여러 StatModifier(synergy+카드들)가 올 때 마지막만 남던 버그. 미래 유닛 다중효과 상속에 필수.
- HP는 max-HP 가 아니라 **받는 피해 감소(DmgTakenMul)** 프록시. 정확한 max-HP 는 후속.
- synergy 가 DamageMul 을 건드리므로, 스택 검증 테스트는 AttackSpeed 축 사용.
- 유닛 배치하는 PlayMode 테스트는 씬 `DreamcatcherController` 를 Destroy + `Time.timeScale=1` 리셋(모달/일시정지 오염 방지).
- UI 라벨 영문(한글 폰트 후속). 컨트롤러/뷰 미배치 시 비파괴.

## Follow-up

- **D `dreamcatcher-deck-builder`**: Outgame 드림캐쳐 패널에 10장 세이브덱 빌더 → `PlayerProfile.dreamcatcherDecks`. 현재 덱은 코드 고정(Default).
- 복합 효과: row-only/crit/pierce/splash/lowcost-summon/guardian-taunt/match-start-cost (+신규 메커닉/채널), 무의식 2장.
- 진짜 MaxHealthMul 채널(HP 프록시 → 정확 max-HP).
- 5웨이브 실주행 통합검증(현재는 첫배치로 메커니즘만 검증).
- 일시정지를 timeScale 대신 MovementPauseRequest 채널로.
- 카드 효과 요약/아이콘 비주얼, 선택 카드 누적 표시.
