# 2 — 보스 어그로 면역 + 직접 행동정지·넉백 면역

## 목적

**이 작업 단위 없이는 짱쎈놈이 성립하지 않는다.** 코스트 2 가디언 1기가 붙으면 `Aggroed` 가 타겟 수를
1로 강제해 cleave 3 이 소멸하고, `Chasing` 조기 return 이 사냥 분기보다 앞이라 보스가 가디언만 쫓는다.
`boss-defender-field` 가 파킹해둔 "보스 어그로 면역" 후속 후보를 실행한다.

적용 범위는 `BossTag` 전체 — **나이트메어도 함께 바뀐다.**

## 변경 대상

- `Assets/_Project/Scripts/Battle/Effects/AggroStateSystem.cs` — 부착 차단 1곳
- `Assets/_Project/Scripts/Battle/Effects/EnemyCcEvents.cs` — `EnemyCcEvent` 에 출처 필드
- `Assets/_Project/Scripts/Battle/Effects/Modifiers/StackModifierTickSystem.cs` — enqueue 2곳에서 출처 표시
- `Assets/_Project/Scripts/Battle/Effects/CcApplySystem.cs` — 부여 거절
- `Assets/_Project/Scripts/Battle/Effects/EffectSpawner.cs` — `ApplyCc` 부여 거절
- `Assets/_Project/Scripts/Battle/Effects/CcActionLock.cs` — lock-set 조회 재사용(수정 없을 수도)

## 구현

### 어그로 — 부착 1곳 차단

`Aggroed` 의 **유일한 writer** 는 `AggroStateSystem` 의 `ecb.AddComponent(ev.enemy, new Aggroed{...})` 다.
소비 지점은 6곳(`AttackSystem` sticky override·`desiredCount` 강제, `EnemyAiStateSystem` 2곳,
`MovementSystem` chase flow, `TauntAttackGrantSystem` 2곳, 브리지 어그로 아이콘)이므로 **부착 차단이
압도적으로 싸다.** 선점 가드 옆에 `BossTag` 보유 시 `continue` 1줄.

`BossTag` 는 Combat 소유이고 Effects 가 RO 로 읽는다 — 같은 시스템이 이미 `AttackState`·
`AggroAttackProfile` 을 RO 로 읽는 선례가 있다.

**`AggroCapacity` 회계는 건드리지 않는다.** `held` 는 매 프레임 `Aggroed` 보유 적으로 full recompute
하므로, 보스가 부착을 못 받으면 카운트에 아예 들어오지 않는다. `AggroPolicy.CanAcquire` 무변경.

**FSM 전이도 건드리지 않는다.** `EnemyAiState.Evaluate` 는 순수 함수이고 `aggroed` 가 `Chasing`/`Standoff`
진입의 유일한 조건이므로, 부착이 없으면 자동으로 `Marching`(사냥 flow-follow) ↔ `Engaging` 만 쓴다.

### CC — 출처 필드 + 부여 2곳 거절

`CcKind` 의 `DoT` 가 CC 와 같은 버퍼를 쓰고, 스택 임계가 만드는 DoT·스턴도 **같은
`EnemyCcEventsSingleton` 큐**로 들어온다 — kind 만으로는 직접 CC 와 구별할 수 없다(README 계약 6).

- `EnemyCcEvent` 에 출처 필드 1개 추가. **기본값 = 직접** → 기존 생산자 전부 무회귀.
- `StackModifierTickSystem` 의 `ApplyDot`/`ApplyStun` enqueue 2곳에서만 "스택 출처" 로 켠다.
- **면역 술어**: `직접 출처 && (CcActionLock.IsLock(kind) || kind == Impulse)`.
  → 스택 유발 CC · `DoT` · `Slow` 는 통과. lock-set 을 단일 소스에서 조회하므로 새 lock 종류가 추가되면
  면역이 자동으로 동행한다.
- 부여 거절 지점 **2곳뿐**: `CcApplySystem` 의 `EnemyCcEventsSingleton` 드레인(`AttackSystem` 3곳 ·
  `ProjectileHitSystem` · `ZoneApplySystem` · `StackModifierTickSystem` 이 전부 이 큐로 수렴) +
  `EffectSpawner.ApplyCc`(호출처 3곳).
- **`Impulse` 에 추가 작업 없다** — `MovementSystem` 이 같은 `CcEffect` 버퍼를 순회해 변위를 더하므로
  부여를 막으면 넉백이 자동 차단된다.
- **`CcClearRequestsSingleton` 은 손대지 않는다** — 제거 전용이라 면역 하에선 자연 no-op.
- 술어를 `IsLocked` **판정** 쪽에 넣지 않는다 — 그쪽은 무시 지점이 6곳 이상(이동·공격 락·변위·상태FX·
  wake-on-hit·`DreamCocoon`)이라 회귀 표면이 훨씬 크다.

### 넉업 연출 신호도 함께 거절한다 (부여 거절 원칙의 유일한 예외)

`AttackSystem` 은 `knockupOnHitSec` 블록의 **같은 루프에서** `EnemyCcEvent{Stun}` 과 `KnockupVisualEvent` 를
함께 enqueue 한다. CC 만 거절하면 **보스가 시각적으로 떠오르는데 스턴은 걸리지 않는다** —
`KnockupVisualEvents.cs` 의 "durationSec = 스턴 시간(같은 값이어야 착지와 해제가 맞는다)" 계약이 깨지고
버그로 보인다.

- `AttackSystem` 의 그 지점에서 대상이 `BossTag` 이면 **CC 와 연출 신호를 둘 다 skip** 한다.
- 이것이 "부여 시점 거절 2곳" 원칙의 **유일한 예외**다. 이유는 연출 채널이 CC 큐와 분리돼 있어서
  `CcApplySystem` 거절로는 도달할 수 없기 때문이다. 다른 CC 는 전부 큐로 수렴하므로 예외가 늘지 않는다.
- `BossTag` 조회를 위해 이 지점에 lookup 이 하나 필요하다(Combat 내부 RO 읽기).

## 완료 기준

- **EditMode**: 면역 술어를 순수 함수로 분리하고(`직접 출처`, `kind`) → 스택 출처는 전부 통과 /
  직접 `Stun`·`Sleep`·`Impulse` 는 거절 / 직접 `DoT`·`Slow` 는 통과. `CcKind` 에 값이 추가될 때의 회귀 가드.
- **PlayMode 회귀 1개**: 가디언 1기 + 잡몹 1기 + 보스 →
  ① 가디언이 보스를 때려도 보스에 `Aggroed` **미부착**, 잡몹에는 부착(면역이 전역이 아님을 증명)
  ② `AggroCapacity.held` 가 잡몹 1만 카운트
  ③ 직접 Sleep 이벤트 → 보스 `CcEffect` 버퍼 슬롯 0
  ④ **Bleed 스택 → 보스 HP 실제 감소**(스택 통과 회귀 가드)
  ⑤ 보스 `EnemyAiState` 가 `Chasing` 에 진입하지 않음
- **Play 육안**: 가디언을 깐 상태에서 방어유닛 3기 인접 배치 → 보스가 가디언에 묶이지 않고 **3기를
  동시에** 갈아낸다(unit 1 에서 확인 못 했던 것이 여기서 확인된다).
- **PlayMode 추가**: 말파이트가 보스를 때려도 **보스가 떠오르지 않는다**(연출 신호 거절 회귀 가드).
- **영향 범위(검증 완료)**: CC-on-hit 필드를 가진 방어유닛은 `Defender_Archer`(`knockbackDistance: 2`) ·
  `Defender_Malphite`(`knockupOnHitSec: 0.8`) · `Defender_TooMuchTalker`(`sleepOnHitSec: 3.5`) **3기뿐**이고
  `Defender_IceCaster` 는 해당 필드가 없다. 이 3기가 보스전에서 해당 효과를 잃는 것을 Play 로 확인한다 —
  **의도된 동작이며 조용히 무효가 되는 것을 수용한다**(사용자 확정 2026-07-29).
- 나이트메어 무회귀 확인 — 어그로에 안 끌리는 것 외 기존 능력 3종이 그대로 동작.
