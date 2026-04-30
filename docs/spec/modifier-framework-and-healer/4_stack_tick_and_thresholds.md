# 4. StackModifier Tick & Thresholds

## 목적

`StackModifierSlot` 의 시간 만료 + 임계값 도달 시 파생 효과를 *기존 채널* 로 dispatch (1프레임 지연). `StackModifierSO` + `ThresholdRule` 데이터 모델 도입. `StackKind` enum 멤버 채움.

scope: Stack 사이드 lifetime + threshold dispatch. ApplySystem(2번) 와 BuffStats(3번) 는 무관.

## 변경 대상

| 파일 | 변경 |
|---|---|
| `Assets/_Project/Scripts/Battle/Effects/Modifiers/ModifierTypes.cs` | 수정 — `StackKind` 멤버 추가: `None, Fire, Ice, Bleed, Poison`. (실제 사용 producer 는 후속 spec, 여기선 enum 정의만) |
| `Assets/_Project/Scripts/Battle/Effects/Modifiers/StackModifierApplyEvents.cs` | 수정 — payload 에 `byte maxStack` 추가 (2번 단위에서 미결로 남긴 결정 확정) |
| `Assets/_Project/Scripts/Data/StackModifierSO.cs` | 신규 — `ScriptableObject` + `ThresholdRule[]` + `StackPolicy` enum |
| `Assets/_Project/Scripts/Battle/Effects/Modifiers/StackModifierTickSystem.cs` | 신규 — `ISystem`, tick + 만료 + 임계값 dispatch |

## 구현

**`StackKind` 멤버 추가**:
```csharp
public enum StackKind : byte { None, Fire, Ice, Bleed, Poison }
```

**`StackModifierApplyEvent` payload 확장**:
```csharp
public struct StackModifierApplyEvent {
    public Entity target;
    public StackKind kind;
    public byte countDelta;
    public byte maxStack;          // ★ 추가 — producer 가 SO 에서 복사해서 보냄
    public float perAppDuration;
    public Entity source;
}
```
ApplySystem 의 새 슬롯 생성 시 `maxStack = ev.maxStack`. 같은 슬롯 refresh 시 maxStack 은 *기존 슬롯 값 유지* (다른 SO 가 같은 source/kind 를 쓰지 않는다는 가정 — 그게 깨지면 후속 spec).

**`StackModifierSO.cs`**
```csharp
namespace Wassup.Data {
    public enum StackPolicy : byte { RefreshAll, PerStackInline, DecayTick }   // 디폴트 RefreshAll(S1)
    public enum ThresholdMode : byte { Edge, Consume }
    public enum DerivedEffectKind : byte { ApplyDot, ApplyStun, ApplyStat }     // 1차 셋

    [Serializable] public struct ThresholdRule {
        public byte atStack;
        public ThresholdMode mode;
        public DerivedEffectKind derivedKind;
        public float magnitude;     // DOT dps / Stun duration / Stat magnitude
        public float duration;      // DOT/Stun/Stat 지속
        public StatKind stat;       // ApplyStat 만 의미
        public CombineOp op;        // ApplyStat 만 의미
    }

    [CreateAssetMenu(fileName = "StackModifier", menuName = "Wassup/StackModifier", order = 30)]
    public class StackModifierSO : ScriptableObject {
        public StackKind kind;
        public byte maxStack = 5;
        public float perAppDuration = 5f;
        public StackPolicy policy = StackPolicy.RefreshAll;
        public ThresholdRule[] thresholds;
    }
}
```

**`StackModifierTickSystem.cs`** (not Burst — ECB structural change + queue enqueue):
```csharp
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(BuffStatsAggregateSystem))]
public partial struct StackModifierTickSystem : ISystem {
    public void OnUpdate(ref SystemState state) {
        float dt = SystemAPI.Time.DeltaTime;
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        var ccQueue = SystemAPI.GetSingleton<EnemyCcEventsSingleton>().queue.AsParallelWriter();
        var statQueue = SystemAPI.GetSingleton<StatModifierApplyEventsSingleton>().queue.AsParallelWriter();
        // ※ 이 system 은 single-threaded 이므로 ParallelWriter 대신 일반 Enqueue 도 OK.

        foreach (var (slots, entity) in SystemAPI.Query<DynamicBuffer<StackModifierSlot>>().WithEntityAccess()) {
            for (int i = slots.Length - 1; i >= 0; i--) {
                var s = slots[i];

                // 1. tick
                s.header.remaining -= dt;

                // 2. threshold edge 검출 — lastTriggeredStack < at <= stackCount 인 모든 threshold 발화
                //    (multi-threshold 통과: 4→7 점프 시 5/6/7 모두 발화)
                if (s.stackCount > s.lastTriggeredStack) {
                    DispatchThresholds(s.kind, s.lastTriggeredStack, s.stackCount, entity, ref ccQueue, ref statQueue);
                    s.lastTriggeredStack = s.stackCount;
                }

                // 3. 만료
                if (s.header.remaining <= 0f) {
                    slots.RemoveAtSwapBack(i);
                } else {
                    slots[i] = s;
                }
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }

    // DispatchThresholds — kind 별로 SO registry lookup → ThresholdRule[] 순회
    //   ※ SO registry 접근 — Burst 불호환이라 system 자체가 not Burst. 또는 Bridge 가 Blob 으로
    //     ThresholdRule[] 을 변환해서 ECS singleton 으로 노출하는 방안도 있으나 이 단위에서는
    //     단순히 static dictionary <StackKind, ThresholdRule[]> 를 BattleBridge 가 채워두고
    //     읽는 형태로 구현. (성능 critical 경로 아님 — 임계 도달은 드물다.)
}
```

**SO registry 접근 결정**:
- 옵션 A: `BattleBridge` 가 모든 `StackModifierSO` asset 을 모아 정적 `Dictionary<StackKind, ThresholdRule[]>` 캐시 구성 (Editor StartBattle 시점 1회).
- 옵션 B: `StackModifierSO` 를 BlobAssetReference 로 변환하여 ECS singleton 컴포넌트 (`StackThresholdsSingleton`) 에 저장. Burst 친화.
- 권장: **A 디폴트**, 향후 발화 빈도 높아지면 B 로 전환.

**Consume 모드** (`ThresholdMode.Consume`): 발화 후 `s.stackCount -= rule.atStack` (또는 0). `lastTriggeredStack = stackCount`.

## 완료 기준

- [ ] `StackKind` 5 멤버. `StackModifierApplyEvent.maxStack` 추가.
- [ ] `StackModifierSO` ScriptableObject 정의 + 1개 테스트 asset 작성 가능 (Fire 5스택 → DOT 5dps 5초 ApplyDot threshold).
- [ ] `StackModifierTickSystem` 컴파일 통과.
- [ ] EditMode 테스트:
  - [ ] 4→7 점프: 5/6/7 모두 정의된 SO 면 3건 dispatch (mock channel writer 검증).
  - [ ] 만료 후 reset: lastTriggeredStack=0 → 다시 5 도달 시 재발화.
  - [ ] Consume 모드: stack 5 도달 → 1회 발화 + stackCount 0.
  - [ ] **1프레임 지연 검증**: stack 5 도달 프레임에 ApplySystem/CcApplySystem 은 아직 처리 안 됨, 다음 프레임에 처리됨.
- [ ] 본 문서 하단에 확인 일자 + 커밋 해시 기재 후 commit.

## 후속 단위 의존

- 8번 producer 마이그레이션 시 동일 채널 사용.
- 11번 테스트 단위가 본 단위의 EditMode 테스트를 포함.

---

확인 일자 + 커밋 해시: _(작업 완료 시 기재)_
