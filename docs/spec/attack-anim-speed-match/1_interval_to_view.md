# 1 — 간격을 뷰까지 배선 + 공격 애니 압축

## 목적

AttackSystem 이 공격 간격을 계산해 이벤트에 싣고, Bridge/Pool 을 거쳐 PlayAttack 이 공격 트랙 TimeScale 을 간격에 맞춰 압축한다. 별도 데이터 없이 공격속도 필드에서 직접.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (DrainUnitAttackVisualEvents)
- `Assets/_Project/Scripts/Presentation/SpineUnitPool.cs` (NotifyAttack)
- `Assets/_Project/Scripts/Presentation/SpineUnitView.cs` (PlayAttack)

## 구현

### AttackSystem — 간격 계산 후 enqueue

attackSpeedMul/effectiveCooldownMul 을 enqueue 앞으로 옮겨 계산하고, `attackInterval = cooldownDuration × effectiveCooldownMul` 을 이벤트에 싣는다. double-fire(2연발) 로 cooldownRemaining 을 0 화하기 **전**의 값이라 애니는 정상 간격 유지.

### BattleBridge / SpineUnitPool

`NotifyAttack(evt.attacker, targetWorld, evt.attackInterval)` → `view.PlayAttack(attackInterval)`.

AttackSystem 이 실제 발사 주기 `attackAnimPeriod = max(cooldownDuration/attackSpeedMul, hitDelaySec)` 를 이벤트에 싣는다(critic MEDIUM #1).

### SpineUnitView.PlayAttack (핵심 산식)

```csharp
var entry = state.SetAnimation(0, attack, false);
if (attackAnimPeriod > 0f && entry?.Animation != null && entry.Animation.Duration > 0f)
    entry.TimeScale = Mathf.Max(1f, entry.Animation.Duration / attackAnimPeriod);
```

- SoT: 배율의 논리는 `attackAnimPeriod`(=SO cooldownDuration/attackSpeedMul, hitDelaySec)에서만 파생. 별도 튜닝 데이터 0.
- `TrackEntry.TimeScale` 은 공격 애니만 스케일 → skeleton.timeScale(걷기/battleScale)과 독립 곱.
- 하한 1.0 = 구조 상수(저작속도보다 느리게 안 늘림). 상한 없음.
- `attackAnimPeriod<=0` 폴백 → TimeScale=1(현행).

> **critic 반영 완료**: 하한 `max(1,…)` 은 compress 구간(period<animDuration)에서 animDuration 이 소거돼 SoT 가 유일 저작자 → 불변 준수. 느린 공격 구간의 animDuration 종속은 "자연+대기"를 위한 의도된 구조 상수(critic LOW #3, 유지). 발사 주기에 hitDelay 포함(MEDIUM #1), 유한성 주석 정정(LOW #4).

## 완료 기준

- compile 성공, `read_console` 에러 0.
- (Play) 공속↑ 유닛 공격 모션 압축 완주(빠른 스윙), 시뮬 rate/데미지 불변.
- 초고속에서 콘솔 에러/파괴 없음.
