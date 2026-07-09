# 2 — ③ 마지막 불꽃 (카미카제: 공속버프 + 자폭)

## 목적

부착 즉시 5초간 공속 +90%, 종료 시 유닛 자폭(보드에서 제거). "트리거×페이로드"가 아닌 **즉발-타이머 효과** 부류.

## 변경 대상

- 신규: `Assets/_Project/Scripts/Battle/Effects/LethalTimerSystem.cs`
- 수정: `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `ApplyDreamcatcherCardToUnit`(SelfBuffLethal 처리)
- 신규 에셋: `Assets/_Project/Data/Dreamcatcher/Card_LastFlame.asset`

## 구현

**부착(즉발)**: ③은 슬롯 카운트가 아니라 부착 시점 1회 효과. mechanic 을 `trigger.kind=None`(즉발) + `payload.kind=SelfBuffLethal`(0_vocabulary) 로 표현. `ApplyDreamcatcherCardToUnit` 이 이 페이로드를 만나면 슬롯을 저장하지 않고 **즉시**:
  - `EnqueueAttackSpeedMul(entity, 1f + magnitude/100f, duration)` (기존 StatModifier 채널, magnitude=공속% 예:90, duration=5초)
  - `_em.AddComponent(entity, new LethalTimer{ remaining = duration })`

**자폭**: `LethalTimerSystem`(ISystem, Effects, BattleSimGroup) — 매 프레임 `remaining -= dt`, `<=0` 이면 `ecb.AddComponent<DeadTag>(entity)` + `RemoveComponent<LethalTimer>`. DeadTag 이후는 기존 사망 경로(DefenderDeathEvent enqueue → bridge 제거)가 처리. ③이 만든 사망이 ②(OnDeath)를 달면 콤보 성립.

**주의**: LethalTimer 는 Effects 소유. DeadTag 부여는 기존 사망 경로 재사용(신규 death 채널 금지). 공속 버프는 기존 StatModifier duration 이라 5초 후 자동 만료 — 자폭과 독립(버프 만료 ≈ 자폭 동시).

## 완료 기준

- [ ] 컴파일 + 무회귀
- [ ] Play: 부착 즉시 공속 급증(발사 빨라짐) → ~5초 후 유닛 사망(보드에서 제거). ②카드와 함께 부착 시 사망 폭발 콤보.
- [ ] 사용자 확인
