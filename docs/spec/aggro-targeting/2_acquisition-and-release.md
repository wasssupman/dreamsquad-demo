# Unit 2 — 획득·해제 시스템 (AggroAssignmentSystem)

## 목적

어그로 상태의 단일 권한 시스템. 매 틱 해제 → count 집계 → 근접 획득 순서로 `Aggroed`/`AggroProvider` 를 관리한다. Effects 맥락 소유.

## 변경 대상

- (신규) `Assets/_Project/Scripts/Battle/Effects/AggroAssignmentSystem.cs`

## 구현

ISystem, `[UpdateInGroup(typeof(SimulationSystemGroup))]`, `[UpdateBefore(typeof(MovementSystem))]` (이동·공격이 같은 틱에 갱신된 어그로 상태를 읽도록). 구조 변경은 EntityCommandBuffer.

매 틱 3패스:

### 1) 해제 패스
모든 `Aggroed` 적 순회:
- `guardian == Entity.Null` 또는 `!EntityManager.Exists(guardian)` 또는 가디언 `Health.value <= 0` (또는 `DeadTag`) → `ecb.RemoveComponent<Aggroed>(enemy)`. (계약 6)
- 적 자신이 죽었으면(`DeadTag`) 무시(곧 정리됨).

### 2) count 집계 패스
살아남은 `Aggroed` 를 guardian 별로 카운트 → `NativeHashMap<Entity,int> countByGuardian`. (파생 count, 계약 8)

### 3) 획득 패스
`AggroProvider` + `LocalTransform` 가진 가디언 순회:
- `free = capacity - countByGuardian[guardian]`. `free <= 0` 이면 skip.
- 가디언 `range` 내(`GridMath` 타일 거리, AttackSystem 의 사거리 판정과 동일 방식) 적 후보 중:
  - 이미 `Aggroed` 면 skip (**선점 고정**, 계약 5).
  - `FactionTag == Enemy`, 살아있음.
  - 가까운 순으로 `free` 개까지 `ecb.AddComponent(enemy, new Aggroed { guardian })`, 임시 카운트 증가.

> 후보 적 스냅샷은 AttackSystem 과 동일하게 `ToEntityArray`/`ToComponentDataArray` 로 Temp 수집.

> **단순화(계약/후속)**: 본 단위는 capacity 상한 안에서 근접 적을 즉시 배정한다. "시간당 어그로 수량"(공격당 대상수×공격속도 rate) 은 후속 단위. 따라서 AttackSystem 이 어그로를 발생시키지 않는다 — 획득은 이 시스템이 전담.

## 완료 기준

- [ ] 컴파일 + Burst 호환(ISystem, NativeHashMap/Temp).
- [ ] EditMode: 가디언 1 + 적 N, capacity=K → 정확히 min(K, 사거리내 적) 마리만 `Aggroed`.
- [ ] EditMode: 이미 어그로된 적은 두 번째 가디언이 가져가지 않음(선점).
- [ ] EditMode: 가디언 Health=0 → 링크된 적 전부 `Aggroed` 제거.
- [ ] 맥락 경계: 이 시스템만 `Aggroed`/`AggroProvider` 쓰기.
