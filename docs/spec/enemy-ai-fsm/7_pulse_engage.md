# 7 — Pulse engageMovement (진동형 교전 이동)

## 목적

`Engaging` 상태에 **진동형 이동(Pulse)** 을 추가한다. 적이 공격마다 잠깐 멈춰 휘두르고 다시 전진하기를 반복하며 라인을 밀고 들어온다. 어그로 유무로 동작이 자동 분기:

- **비어그로 `Engaging` + Pulse** → 치고-전진 반복(B).
- **어그로 `Standoff`** → 무조건 정지라 자동으로 캠프(A). (변경 없음)

option (a): 별도 타이밍 필드 없이 기존 `AttackState.hitDelayRemaining`(타격 진행 신호)에 연동한다.

## 변경 대상

- `Assets/_Project/Scripts/Data/EnemyBehaviorEnums.cs` — `EngageMovement` 에 `Pulse` 추가.
- `Assets/_Project/Scripts/Battle/Movement/MovementSystem.cs` — `Engaging` 분기에 Pulse 처리 + `AttackState` RO 룩업.
- `Assets/_Project/Tests/EditMode/MovementSystemTests.cs` — Pulse 정지/전진 EditMode 테스트.

## 구현

`EngageMovement { Halt = 0, Advance = 1, Pulse = 2 }` (additive — 기존 직렬화값 보존).

MovementSystem `Engaging` 분기:
- `Halt` → 정지 (현행)
- `Advance` → flow 전진 (현행)
- `Pulse` → `AttackState.hitDelayRemaining > 0` 이면 정지(스윙 진행 중), `== 0` 이면 flow 전진. `AttackState` 없으면 전진(타격 진행 없음).

`var attackStateLookup = SystemAPI.GetComponentLookup<AttackState>(isReadOnly: true);` 추가. `AttackState` 는 Combat 소유·타 맥락 RO 허용(컴포넌트 주석 명시) → Movement 가 RO 로 읽는 것은 경계 준수.

타임라인(쿨 0.8 / hitDelay 0.3 예):
```
START → hitDelayRemaining=0.3 → 정지 0.3s(스윙) → RESOLVE → 전진 0.5s → 쿨0 → 다시 START
```

**결합/순서 가정 (ecs-review M3)**: Pulse 정지 구간 길이 = `hitDelaySec`(타격지연과 동일 값에 묶임 — option a). `hitDelayRemaining` 은 AttackSystem(Combat)이 같은 SimulationSystemGroup 패스에서 set/감소시킨다. README 계약 3 은 `EnemyAiStateSystem UpdateBefore(MovementSystem)` 만 고정하고 AttackSystem↔MovementSystem 순서는 고정하지 않으므로 MovementSystem 은 이번/직전 프레임 `hitDelayRemaining` 을 읽을 수 있다(스윙 윈도우 동안 값이 단조라 1프레임 지연은 동작·결정성에 무해). 멈춤시간과 텔레그래프를 따로 튜닝하거나 가시적 위상 문제가 보이면 `attackMotionSec` 분리 + AttackSystem 순서 고정(후속 후보 "Pulse 멈춤시간 분리").

## 완료 기준

- compile 통과, 콘솔 에러 0.
- EditMode: `Engaging`+Pulse+`hitDelayRemaining>0` → 정지(x 불변), `==0` → flow 전진. 기존 Halt/Advance/Standoff/Marching 테스트 회귀 없음.
- (Pulse 가시 동작 검증은 unit 8 Vanguard SO + Play 에서.)

---

✅ **완료 2026-06-30** — `EngageMovement.Pulse`(additive, Halt0/Advance1/Pulse2) + MovementSystem Engaging 분기(AttackState RO). TDD: RED(회복→전진 실패) 확인 후 구현, MovementSystemTests **15/15 PASS**(Pulse 4: 스윙정지/회복전진/Standoff캠프/AttackState없음전진). ecs-reviewer APPROVE(맥락경계·Burst·로직정합·enum 마이그레이션 PASS). M1(Standoff+Pulse 캠프)·M2(degenerate fallback) 테스트 추가, M3(hitDelaySec 결합/순서 가정) 위 문서화 반영.
