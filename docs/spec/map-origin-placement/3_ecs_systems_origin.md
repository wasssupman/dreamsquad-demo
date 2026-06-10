# 3 — ECS 시스템에 field.origin 전파

## 목적

Burst 시스템들이 grid↔world 변환 시 `FlowFieldSingleton.origin`(작업 1)을 GridMath 에 넘기도록 한다. 이로써 이동·타겟팅·AoE·해저드가 옮겨진 좌표계에서 정확히 동작한다.

## 변경 대상 (모두 `field`/`flowField` 싱글턴을 이미 보유)

- `Battle/Movement/MovementSystem.cs` (72/84/85/142/149)
- `Battle/Combat/AttackSystem.cs` (106/116/228/372/427)
- `Battle/Combat/MeteorResolutionSystem.cs` (67/72)
- `Battle/Effects/ZoneApplySystem.cs` (39)
- `Battle/Effects/HazardCastSystem.cs` (57/70/88)
- `Battle/Effects/EffectSpawner.cs` (143)

## 구현

각 호출에 `origin: field.origin`(또는 해당 싱글턴 변수의 `.origin`)을 named arg 로 추가. 예:

```csharp
// MovementSystem
int2 cell = GridMath.WorldToCell(current, field.tileSize, field.gridSize, origin: field.origin);
...
desired = MovementCellTrim.ClampToBoundary(desired, cell, field.tileSize, origin: field.origin);

// AttackSystem (tileSize/gridSize 를 지역변수로 캐시한 경우 origin 도 같이 캐시)
int2 atkCell = GridMath.WorldToCell(atkPos, tileSize, gridSize, origin: ffOrigin);

// CellToWorldCenter (y 인자 있는 호출은 origin 을 named 로)
float3 cellWorld = GridMath.CellToWorldCenter(cells[i].cell, flowField.tileSize, fallbackTargetPos.y, origin: flowField.origin);
```

작업 분할(메모리 피드백: 대규모 refactor 는 A/B/C 분할). 6개 파일을 한 커밋에 묶되, 컴파일 단위로 위험하면:
- 3A: Movement (MovementSystem + ClampToBoundary 경로)
- 3B: Combat (AttackSystem + MeteorResolutionSystem)
- 3C: Effects (ZoneApply + HazardCast + EffectSpawner)
각 서브커밋이 독립 컴파일 green. (origin 기본값 zero 라 부분 마이그레이션 중에도 빌드는 깨지지 않음 — 단지 해당 시스템만 아직 origin 미반영.)

## 완료 기준

- [ ] compile green (서브커밋 각각).
- [ ] MapView 이동 상태에서 Play: 적이 옮겨진 경로를 정확히 따라가고, 디펜더 공격 사거리/타겟팅이 시각적으로 맞고, 해저드/AoE 가 올바른 셀에 적용.
- [ ] 핵심 계산(이동/타겟팅/데미지)의 기존 EditMode 단위 테스트가 origin=0 에서 그대로 통과(회귀 없음).
- [ ] origin≠0 케이스로 WorldToCell round-trip 통합 시나리오 1개(가능하면 PlayMode smoke).

## 주의

- `CellToWorldCenter` 호출 중 `y` 인자를 쓰는 곳은 origin 을 반드시 named arg(`origin:`)로 — 위치 인자로 넣으면 y 자리에 들어감.
- 6개 시스템 외에 GridMath 를 호출하는 신규 지점이 없는지 작업 직전 재grep 으로 확인.
