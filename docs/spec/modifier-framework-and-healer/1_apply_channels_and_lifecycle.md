# 1. Apply Channels & Lifecycle

## 목적

두 신규 `NativeQueue` singleton 채널 정의 + `BattleBridge` 의 lifecycle (생성·dispose) 까지. 기존 8개 채널 (`EnemyCcEvents`, `UnitAttackVisualEvents`, `GoalReachedEvents` 등) 과 **완전히 동일한 패턴 답습** — `Allocator.Persistent` + `StartBattle` 에서 create + `EndBattle`/`CleanupBattle` 에서 entity destroy + queue dispose.

scope: 채널 struct 정의 + BattleBridge 손댐. `ModifierApplySystem` 자체는 2번에서 작성하므로 0번에 더해 *큐 기반 인프라만* 깔린 상태가 1번의 끝.

## 변경 대상

| 파일 | 변경 종류 | 내용 |
|---|---|---|
| `Assets/_Project/Scripts/Battle/Effects/Modifiers/StatModifierApplyEvents.cs` | 신규 | payload struct `StatModifierApplyEvent` + `StatModifierApplyEventsSingleton` (queue 보유) |
| `Assets/_Project/Scripts/Battle/Effects/Modifiers/StackModifierApplyEvents.cs` | 신규 | payload struct `StackModifierApplyEvent` + `StackModifierApplyEventsSingleton` |
| `Assets/_Project/Scripts/Bridge/BattleBridge.cs` | 수정 | NativeQueue field 2개 + create / dispose / singleton entity 생성 — 기존 채널 (`_enemyCcQueue` 등) 와 같은 위치에 추가. spawn 함수 내부 위치는 기존 패턴 그대로. |

## 구현

**`StatModifierApplyEvents.cs`** (기존 `EnemyCcEvents.cs` 패턴 답습)
```csharp
namespace Wassup.Battle.Effects {
    public struct StatModifierApplyEvent {
        public Entity target;
        public StatKind stat;
        public CombineOp op;
        public float magnitude;
        public float duration;
        public Entity source;
        public ushort stackId;       // producer 가 부여, 디폴트 0
    }
    public struct StatModifierApplyEventsSingleton : IComponentData {
        public NativeQueue<StatModifierApplyEvent> queue;
    }
}
```

**`StackModifierApplyEvents.cs`**
```csharp
namespace Wassup.Battle.Effects {
    public struct StackModifierApplyEvent {
        public Entity target;
        public StackKind kind;
        public byte countDelta;       // 부착당 누적량 (cap 은 Apply 시점 적용)
        public float perAppDuration;  // refresh 정책 (S1) — remaining = perAppDuration
        public Entity source;
    }
    public struct StackModifierApplyEventsSingleton : IComponentData {
        public NativeQueue<StackModifierApplyEvent> queue;
    }
}
```

**`BattleBridge.cs`** 수정 — 기존 8개 채널과 동일 패턴:

1. **field 추가** (`_enemyCcQueue` 와 같은 줄 근처):
   ```csharp
   private NativeQueue<StatModifierApplyEvent> _statModifierQueue;
   private NativeQueue<StackModifierApplyEvent> _stackModifierQueue;
   ```

2. **`StartBattle()` 에서 create** (기존 EnemyCcEvents create 코드 직후):
   ```csharp
   _statModifierQueue = new NativeQueue<StatModifierApplyEvent>(Allocator.Persistent);
   var statSingleton = em.CreateEntity(ComponentType.ReadWrite<StatModifierApplyEventsSingleton>());
   em.SetComponentData(statSingleton, new StatModifierApplyEventsSingleton { queue = _statModifierQueue });

   _stackModifierQueue = new NativeQueue<StackModifierApplyEvent>(Allocator.Persistent);
   var stackSingleton = em.CreateEntity(ComponentType.ReadWrite<StackModifierApplyEventsSingleton>());
   em.SetComponentData(stackSingleton, new StackModifierApplyEventsSingleton { queue = _stackModifierQueue });
   ```

3. **`EndBattle()` / `CleanupBattle()` 에서 dispose** (기존 dispose 블록에 합류):
   ```csharp
   if (_statModifierQueue.IsCreated) { _statModifierQueue.Dispose(); }
   if (_stackModifierQueue.IsCreated) { _stackModifierQueue.Dispose(); }
   ```
   singleton entity 자체는 World destroy 시 자동 정리되지만, 기존 채널이 명시적으로 entity destroy 하면 같은 패턴 따라가기.

## 완료 기준

- [ ] 2개 신규 파일 + BattleBridge 수정.
- [ ] Unity Editor 컴파일 통과 (Console error 0).
- [ ] PlayMode: BattleScene 진입 → 두 singleton entity 가 World 에 존재 (`em.CreateEntityQuery(ComponentType.ReadOnly<StatModifierApplyEventsSingleton>()).CalculateEntityCount() == 1` 임시 로그로 검증).
- [ ] PlayMode: BattleScene 종료 (또는 도메인 reload) 후 Allocator leak 경고 없음 (Unity Console).
- [ ] 본 문서 하단에 확인 일자 + 커밋 해시 기재 후 commit.

## 후속 단위 의존

- 2번 (`ModifierApplySystem`) 이 두 singleton 을 `RequireForUpdate` + drain.
- 5번 (`AttackSystem` outputs 분기) 가 두 채널의 `AsParallelWriter()` 로 enqueue.
- 8번 (legacy producer 마이그레이션) 가 동일 채널 사용.

---

확인 일자 + 커밋 해시: 2026-04-30, `3c10cb7` (Unity Editor 컴파일 통과 + console clean + 도메인 reload leak 0). commit 에 prior uncommitted refactor (SpineDefenderPool→SpineUnitPool, DefenderAttackEvent→UnitAttackVisualEvent rename) 가 함께 흡수됨 — 별도 회귀 없음.
