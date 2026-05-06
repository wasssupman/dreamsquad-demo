# 2. Hazard Cast System

## 목적

범위 내 공격 유닛 target 을 찾고, 쿨타임마다 target cell 에 hazard spawn request 를 enqueue 하는 ECS system 을 추가한다.

## 변경 대상

- Add: `Assets/_Project/Scripts/Battle/Effects/HazardCastSystem.cs`
- Use: `Assets/_Project/Scripts/Battle/Units/Faction.cs`
- Use: `Assets/_Project/Scripts/Battle/Movement/GridMath.cs`
- Use: `Assets/_Project/Scripts/Battle/Effects/FlowFieldSingleton.cs`

## 구현

`HazardCastSystem` 은 `ISystem` 으로 작성한다.

대상 선택:

- caster: `HazardCastState`, `LocalTransform`, `FactionTag`, `DefenderUnitTag`
- target: `LocalTransform`, `FactionTag`, `PathFollowState`
- default mask: `Faction.Enemy`
- self target 금지
- `GridMath.RangeToTiles(range)` + Chebyshev tile distance 사용
- Burst system 안에서 `GridMath.ChebyshevDistance(int2, int2)` 를 직접 호출하지 않는다. `GridMath.ChebyshevDistance` 는 현재 Burst BC1064/BC1067 오류가 있으므로, **MVP 에서는 항상 inline 으로 계산한다**:
  ```csharp
  int dist = math.max(math.abs(tgtCell.x - casterCell.x), math.abs(tgtCell.y - casterCell.y));
  if (dist > tileRange) continue;
  ```
  Burst-safe helper 수정은 별도 후속 작업이다. 이 오류가 수정되기 전까지는 inline 을 유지한다.
- 같은 range 안에서는 가장 가까운 target (world-space `distancesq`) 을 선택한다.

쿨타임:

- 매 update `cooldownRemaining -= DeltaTime`
- target 이 없으면 cooldown 은 감소만 한다.
- target 이 있고 `cooldownRemaining <= 0` 이면 request enqueue 후 `cooldownRemaining = cooldownDuration`

cell 계산:

- target 의 `LocalTransform.Position` 을 `GridMath.WorldToCell` 로 변환한다.
- request 는 `centerCell` 로 저장한다.
- zone hazard 는 기존 `SpawnHazardWithVisual` 의 nearest walk-cell 보정을 허용한다. 공격 유닛은 path 위에 있으므로 보정은 보통 no-op 이다.
- blocking hazard 는 spawn API 가 유효성 검사를 수행한다. 실패하면 아무 hazard 도 생성되지 않으며 cooldown 은 소모된다.
- `width = 1; height = 1;` 을 `HazardCastSystem` 에서 리터럴로 고정한다. `HazardCastState.footprintWidth/Height` 값은 MVP 에서 읽지 않는다. BattleBridge drain 쪽은 전달된 값을 그대로 쓴다.

OnCreate requirements:

```csharp
state.RequireForUpdate<HazardCastState>();
state.RequireForUpdate<FlowFieldSingleton>();
state.RequireForUpdate<HazardSpawnRequestsSingleton>();
```

update order:

```csharp
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(MovementSystem))]    // 계약: 공격 유닛의 이동 후 위치를 기준으로 cast
```

`AttackSystem` 과의 순서는 gameplay 효과에 의존하지 않는다. hazard 효과는 다음 tick 부터 적용되며, 본 spec 의 4종 caster 는 `outputs[]` 없이 hazard cast action 만 수행한다.

## 완료 기준

- defender caster 1개가 범위 안 공격 유닛 위치에 request 를 생성한다.
- 범위 밖 공격 유닛에는 request 를 생성하지 않는다.
- target 이 request 후 파괴되어도 target cell snapshot 기준으로 spawn 된다.
- caster 가 request 후 파괴되면 drain 에서 drop 된다.
- cooldown 중에는 추가 request 를 생성하지 않는다.
- EditMode 테스트가 target selection / cooldown / dead caster / dead target 을 포함한다.
