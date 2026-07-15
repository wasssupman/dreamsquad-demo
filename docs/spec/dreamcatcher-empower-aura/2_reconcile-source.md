# 2 — ReconcileStatusFx origin 소스 훅 + ActiveDcEffect 상속 수정 + fallbackDeck 제거

## 목적

unit 1 판정을 부착/해제에 연결하고, 상속 경로의 출처 오태깅 버그를 수정하며, 미의도 기본 덱을 제거한다.

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `_modifierSlotQuery`(StatModifierSlot) + `ReconcileStatusFx` 훅
- `Assets/_Project/Scripts/Bridge/BattleBridge.Dreamcatcher.cs` — `ActiveDcEffect.origin` + 상속/revoke 경로
- `Assets/_Project/Scripts/Core/Dreamcatcher/DreamcatcherHandController.cs` — `fallbackDeck` 제거
- `Assets/_Project/Data/Config/StatusFxRegistry.asset` — `Empowered`(kind 2) 임시 항목(금빛 폴백, unit 3에서 프리팹)

## 구현

1. **reconcile**: `_modifierSlotQuery = CreateEntityQuery(ReadOnly<StatModifierSlot>())`(생성/해제 기존 쿼리 관용구).
   매 프레임 각 유닛 버퍼 → `HasActiveDreamcatcherModifier` 면 `Ensure(e, Empowered, anchor)`. 버퍼 읽기만(맥락 준수).
   해제·revoke·사망 시 기존 `EndFrame` 자동 회수.
2. **ActiveDcEffect.origin 상속 수정(핵심 버그)**: `_activeDcEffects` 는 드림캐쳐 카드 + 드림스톤을 함께 담는다.
   `ApplyActiveDcEffectsTo`(신규 배치 유닛 상속)가 origin 을 Dreamcatcher 로 **하드코딩**해 드림스톤 버프가
   Dreamcatcher 로 오태깅 → 드림스톤만 껴도 오라가 뜸. → `ActiveDcEffect.origin` 필드 추가, Add 3곳
   (Internal=Dreamcatcher · RegisterPlacementAura=Dreamcatcher · ApplyPendingDreamstones=Dreamstone) 태깅,
   상속·revoke 가 `e.origin` 재적용.
3. **fallbackDeck 제거**: `ResolveAttachDeck` 의 fallbackDeck 폴백 + 필드 삭제. 저장 덱 없으면 부착 덱 빈 목록.
   (사용자 결정 — 이번 오라 버그의 원인은 아니었으나 미의도 동작이라 함께 제거.)
4. **revoke op-aware 중립화(리뷰 HIGH)**: revoke 가 `1f` 를 보내면 `FromMultiplier`→Additive+0 이라, 감소형
   (Multiplicative, 예 DmgTakenMul 0.87) 버프는 머지 키 `op` 불일치로 중립화 실패 → 버프+오라 잔존. →
   `EnqueueStatModifierRaw`(명시적 op+magnitude) 추가, revoke 가 원본 op 를 재도출해 그 op 의 identity
   (Multiplicative→1.0 / Additive→0) emit. PlayMode `RevokeNeutralizesReductionShapedBuff` 로 가드.

## 완료 기준

- [x] 컴파일 클린, 전체 EditMode 회귀 무손실
- [x] Play: 드림스톤-only 유닛 → 오라 미표시 (origin 분리 실증)
- [ ] Play: 드림캐쳐 카드 실제 사용 → 그 유닛만 오라 (unit 3 저작 프리팹으로 최종 시각 확인)
- [x] revoke/사망 시 자동 회수(상태 구동)

사용자 확인 2026-07-15(드림스톤-only 미표시).
