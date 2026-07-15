# 4 — Handoff Summary (dreamcatcher-empower-aura)

## Commit
- `ca20eedb` unit 0 초기(구 buff/debuff — StatusFxKind + classifier)
- `34c99cf3` ModifierOrigin 프레임워크(생산자 19곳) + 오라 판정/reconcile + 버그 2건 + fallbackDeck 제거
- `10370f6e` unit 3 — EmpowerAura 스켈레톤 저작 + registry 배선
- `b7d58720` 폴리싱 — 5요소 파랑/주황 파워업 오라
- `33d3b90f` 정식화 — 전용 텍스처 2종 + `_SKELETON`→`EmpowerAura.prefab` 승격 + 뭉툭화·scale 0.7

## Implemented
- 모디파이어 출처 1급 태깅(`ModifierOrigin`) — 생산자 19곳, 같은 크기 버프도 출처 구분(슬롯 단위).
- 드림캐쳐 강화 유닛에만 `Empowered` 온-바디 오라 자동 부착/해제(상태 구동 reconcile, origin==Dreamcatcher net≠identity).
- 드림스톤/시너지/on-place/슬로우 등 타 출처 제외. revoke 시 오라 해제.
- 버그 2건 수정: `ApplyActiveDcEffectsTo` 드림스톤 오태깅(ActiveDcEffect.origin) · revoke 감소형 미중립화(op-aware).
- `DreamcatcherHandController.fallbackDeck` 제거(미의도 기본 덱).
- `EmpowerAura_SKELETON.prefab` + `EmpowerAura_Mat.mat`(금빛 상승, URP/Particles/Unlit 가산) 저작·배선.

## Key Files
- `Battle/Effects/Modifiers/ModifierTypes.cs`(enum+header)·`StatModifierApplyEvents.cs`·`ModifierApplySystem.cs`(전파)
- `Battle/Effects/Modifiers/ModifierAuraClassifier.cs`(순수 판정) + `Tests/EditMode/ModifierAuraClassifierTests.cs`(14)
- `Bridge/BattleBridge.cs`(EnqueueStatModifier[Raw]·ReconcileStatusFx·생산자) · `BattleBridge.Dreamcatcher.cs`(ActiveDcEffect.origin·revoke)
- `Data/StatusFxKind.cs`(Empowered) · `Data/Config/StatusFxRegistry.asset`(kind 2 배선)
- `VFX/EmpowerAura.prefab`(정식, 5요소) + `VFX/Materials/EmpowerAura_{Glow,Streak}_Mat.mat` + `VFX/Textures/EmpowerAura_{Glow,Streak}.png`
- `Tests/PlayMode/DreamcatcherEffectTest.cs`(revoke 회귀 추가)

## Verified
- EditMode 780 pass / 2 기존 skip. PlayMode 2 pass(신규 `RevokeNeutralizesReductionShapedBuff` 포함).
- 투트랙 리뷰(ecs+code) 반영: origin 누락 0·머지 키 청정 확인, HIGH revoke 결함 수정.
- Play 프리뷰: 드림스톤-only 무오라 / 드림캐쳐 강화 유닛 금빛 오라(사용자 "느낌 OK").

## Notes (되돌리면 안 되는 의도)
- origin 은 **메타데이터** — 머지 키 `(source,stat,op,stackId)` 에 넣지 말 것.
- 오라 판정은 **슬롯 버퍼** 기준(집계 ModifierStats 는 출처 소실). reconcile 은 defender 한정 쿼리.
- revoke 중립화는 **원본 op** 로 identity emit(1f→Additive 고정 금지 — 감소형 버프 잔존).
- `ActiveDcEffect.origin` 항목별 보존 필수(드림스톤/드림캐쳐 공유 리스트).

## Follow-up
- 오라 아트 폴리싱(텍스처/강도/색) → `_SKELETON` 정식화. + README 후속 후보(강도별 단계·출처 태그 재사용·음수 Override 잠재결함·fallbackDeck 씬 잔재 재저장) 참조.
