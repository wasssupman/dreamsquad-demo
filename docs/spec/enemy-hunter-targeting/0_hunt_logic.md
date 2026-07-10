# 0 — 헌트 로직 (컴포넌트 + 순수함수 + Evaluate 확장)

## 목적

헌터 추격의 어휘와 순수 판정을 정의한다. 컴파일 + EditMode 가 완료 기준.

## 변경 대상

- 신규: `Assets/_Project/Scripts/Battle/Combat/HuntTarget.cs`
- `Assets/_Project/Scripts/Battle/Combat/EnemyAiStateSystem.cs` — `Evaluate` 시그니처 확장 + 최근접 선정 순수함수
- 테스트: `Assets/_Project/Tests/EditMode/EnemyAiStateEvaluateTests.cs` (기존 있으면 append) + 최근접 선정 테스트

## 구현

### HuntTarget 컴포넌트 (Combat 소유)

```csharp
// 헌터가 추격 중인 방어유닛. EnemyAiStateSystem(Combat)만 write, MovementSystem(Movement) RO.
// 스폰 베이크로 BossTag 엔티티에 사전 부착(핫패스 구조변경 금지). Null = 추격 대상 없음.
public struct HuntTarget : IComponentData { public Entity value; }
```

### Evaluate 확장 (계약 5)

기존:
```csharp
Evaluate(bool aggroed, bool guardianInRange, bool hasFireTarget)
  aggroed ? (guardianInRange ? Standoff : Chasing) : (hasFireTarget ? Engaging : Marching)
```
확장 — hunter 축 추가 (기본 false = 비-헌터 무회귀):
```csharp
Evaluate(bool aggroed, bool guardianInRange, bool hasFireTarget, bool isHunter, bool hasHuntTarget)
  if (aggroed) return guardianInRange ? Standoff : Chasing;      // 기존
  if (hasFireTarget) return Engaging;                            // 기존 (사거리 내)
  if (isHunter && hasHuntTarget) return Chasing;                 // 신규 (추격)
  return Marching;                                               // 기존 (goal)
```

### 최근접 선정 (순수함수, EditMode)

```csharp
// 후보 중 atkCell 에서 Chebyshev 최소거리 방어유닛 index. 동점 = entity index 오름차순.
// 없으면 -1. FSM 이 이미 뜨는 후보 스냅샷(faction mask 로 방어유닛 필터)에서 호출.
static int SelectNearestTarget(int2 atkCell, mask, cand..., out ...)
```
- mask 는 AttackState.targetMask (헌터 보스 = Defender). aggro 스냅샷과 동일 후보 풀 재사용.
- 결정론: 거리 동점이면 `entity.Index` 낮은 쪽(스냅샷 순서 무관).

## 완료 기준

- [ ] `HuntTarget.cs` 컴파일.
- [ ] `Evaluate` 5인자 확장 — 비-헌터(isHunter=false) 결과가 3인자 시절과 동일(무회귀 테스트).
- [ ] hunter 전이 테스트: 사거리 타겟 → Engaging / 없고 HuntTarget 있음 → Chasing / 없음 → Marching / aggro 우선.
- [ ] 최근접 선정 결정론 EditMode(거리·동점 index·빈 풀 -1).
