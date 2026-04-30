# 2. ModifierApplySystem

## 목적

두 채널을 드레인하여 대상 entity 의 `StatModifierSlot` / `StackModifierSlot` buffer 를 갱신하는 단일 시스템. **merge 정책 (refresh / cap / 새 슬롯)** 의 단독 책임자. 갱신 발생 시 `BuffStatsDirty` enable.

scope: ApplySystem 만. tick / aggregate / threshold dispatch 는 후속.

## 변경 대상

| 파일 | 변경 |
|---|---|
| `Assets/_Project/Scripts/Battle/Effects/Modifiers/ModifierApplySystem.cs` | 신규 — `ISystem`, OnCreate / OnUpdate (not Burst, structural change 포함) |

## 구현

```csharp
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(StatModifierTickSystem))]    // 3번에서 정의될 system. forward declare OK.
public partial struct ModifierApplySystem : ISystem {
    public void OnCreate(ref SystemState state) {
        state.RequireForUpdate<StatModifierApplyEventsSingleton>();
        state.RequireForUpdate<StackModifierApplyEventsSingleton>();
    }

    // OnUpdate not Burst — EntityManager.AddBuffer / SetComponentEnabled / AddComponent 가 structural change.
    public void OnUpdate(ref SystemState state) {
        var statQ = SystemAPI.GetSingleton<StatModifierApplyEventsSingleton>().queue;
        var stackQ = SystemAPI.GetSingleton<StackModifierApplyEventsSingleton>().queue;
        var em = state.EntityManager;
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        while (statQ.TryDequeue(out var ev)) ApplyStat(em, ecb, ev);
        while (stackQ.TryDequeue(out var ev)) ApplyStack(em, ecb, ev);

        ecb.Playback(em);
        ecb.Dispose();
    }
}
```

**`ApplyStat`** — merge key `(source, stat, op, stackId)`:
```
1. target 에 StatModifierSlot buffer 없으면 ecb.AddBuffer<StatModifierSlot>(target).
2. 같은 (source, stat, op, stackId) 슬롯 검색.
   - 있으면: header.remaining = max(old.remaining, ev.duration); magnitude = ev.magnitude. (기존 stackId 유지)
   - 없으면: 새 슬롯 추가 { header={remaining=ev.duration, source=ev.source, stackId=ev.stackId}, stat, op, magnitude }.
3. BuffStatsDirty 가 없으면 ecb.AddComponent<BuffStatsDirty>(target). (Add 시 기본 disabled)
4. ecb.SetComponentEnabled<BuffStatsDirty>(target, true).
```

**`ApplyStack`** — merge key `(source, kind)`:
```
1. target 에 StackModifierSlot buffer 없으면 add.
2. 같은 (source, kind) 슬롯 검색.
   - 있으면: stackCount = min(maxStack, stackCount + ev.countDelta); header.remaining = ev.perAppDuration (S1 refresh).
     ※ maxStack 은 buffer 슬롯 자체의 maxStack 필드 사용. 새 슬롯 생성 시점에만 SO 의 maxStack 에서 복사.
   - 없으면: 새 슬롯 { header={remaining=ev.perAppDuration, source=ev.source, stackId=0}, kind, stackCount=min(maxStack_default, ev.countDelta), maxStack=maxStack_default, lastTriggeredStack=0 }.
     ※ `maxStack_default` 결정 — 4번 단위에서 SO 가 producer 측에 maxStack 을 알려주는 메커니즘 (payload 에 maxStack 포함 또는 producer 가 SO 에서 lookup) 도입. 1번 시점에는 payload 에 maxStack 없음 → 4번 단위 작성 시 `StackModifierApplyEvent` 에 byte maxStack 추가하거나, ApplySystem 이 SO registry 를 lookup. **구현 시 결정**.
3. BuffStatsDirty 갱신은 Stack 은 BuffStats 직접 영향 없으므로 불요. (Stack 의 임계값 파생만이 BuffStats 영향) — 따라서 SetComponentEnabled 호출 안 함.
```

**구현 결정 (4번 단위 작성 시 확정)**: `StackModifierApplyEvent` 에 `byte maxStack` 필드 추가가 가장 단순. payload 가 1바이트 늘어남. 또는 ApplySystem 이 `StackModifierSO` registry 를 read — 추가 인프라 필요. 권장: payload 확장.

## 완료 기준

- [ ] `ModifierApplySystem.cs` 신규 작성. 컴파일 통과.
- [ ] EditMode 테스트 (`ModifierApplySystemTests`):
  - [ ] 같은 (source, stat, op) refresh: 슬롯 1개, magnitude=new, remaining=max.
  - [ ] 다른 source 같은 stat: 슬롯 2개 공존.
  - [ ] 다른 stackId 같은 source/stat: 슬롯 2개 공존.
  - [ ] Stack countDelta 누적 + maxStack cap 동작.
  - [ ] BuffStatsDirty 가 Stat 부착 후 enabled, Stack 부착 후 변동 없음 (또는 disabled 유지).
- [ ] PlayMode smoke: BattleScene 에서 enqueue 1회 수동 트리거 (debug 코드 OR 5번 단위 진입까지 지연) — 회귀 0.
- [ ] 본 문서 하단에 확인 일자 + 커밋 해시 기재 후 commit.

## 후속 단위 의존

- 3번이 dirty 를 처리 (read + disable).
- 4번이 StackModifierSO + maxStack payload 결정.
- 5번이 AttackSystem 에서 enqueue.

---

확인 일자 + 커밋 해시: _(작업 완료 시 기재)_
