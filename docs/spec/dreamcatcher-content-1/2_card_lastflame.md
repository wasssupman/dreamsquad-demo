# 2 — ③ 마지막 불꽃 (카미카제: 공속버프 + 자폭)

## 목적

부착 즉시 5초간 공속 +90%, 종료 시 유닛 자폭(보드에서 제거). "트리거×페이로드"가 아닌 **즉발-타이머 효과** 부류.

## 변경 대상

- 신규: `Assets/_Project/Scripts/Battle/Effects/LethalTimerSystem.cs`
- 수정: `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `ApplyDreamcatcherCardToUnit`(가드 재구조화 + SelfBuffLethal 즉발 처리)
- 신규 에셋: `Assets/_Project/Data/Dreamcatcher/Card_LastFlame.asset`

## 구현

**부착 가드 재구조화 (critic C2)**: 기존 가드는 `trigger.kind==None` 을 무조건 skip → ③(trigger=None)이 거절됨. 가드를 **`trigger==None && payload==None` 일 때만 거절**로 바꾼다(즉발 payload 는 trigger=None 이 정상).

**부착(즉발)**: `trigger.kind==None && payload.kind==SelfBuffLethal` 이면 슬롯 미저장, 부착 시점 즉시:
- `EnqueueAttackSpeedMul(entity, 1f + magnitude/100f, duration)` (기존 StatModifier, magnitude=90 → 1.9x, duration=5s)
- `_em.AddComponent(entity, new LethalTimer{ remaining = duration })`
- **`attached++` (critic M2)** — 즉발 branch 도 성공 시 카운트해야 API 가 true 반환.

**자폭**: `LethalTimerSystem`(ISystem, Effects, `[UpdateInGroup(BattleSimGroup)]`) — `SystemAPI.Query<RefRW<LethalTimer>>().WithNone<DeadTag>()`(critic H5 — 데미지 사망과 이중 DeadTag 방지) 순회: `remaining -= SystemAPI.Time.DeltaTime`(StatModifier 와 동일 dt 소스), `<=0` 이면 `ecb.AddComponent<DeadTag>` + `RemoveComponent<LethalTimer>`. 이후는 기존 사망 경로(UnitLifecycleSystem → DefenderDeathEvent → bridge 제거)가 처리. **③이 만든 사망이 ②(OnDeath)를 달면 콤보** — UnitLifecycleSystem 이 죽는 엔티티 슬롯을 읽으므로 성립.

**주의**: LethalTimer=Effects 소유. DeadTag/사망은 기존 경로 재사용(신규 death 채널 금지). 공속 버프 5s 만료 ≈ 자폭 5s 는 같은 dt 라 동프레임 수렴.

## 완료 기준

- [ ] 컴파일 + 무회귀 (EditMode green, 가드 재구조화가 기존 트리거 카드 무영향)
- [ ] Play: 부착 즉시 공속 급증 → ~5초 후 유닛 사망(보드 제거). ②카드와 함께 부착 시 사망 폭발 콤보.
- [ ] LethalTimer 유닛이 데미지로 먼저 죽어도 예외 없음(WithNone<DeadTag>)
- [ ] 사용자 확인
