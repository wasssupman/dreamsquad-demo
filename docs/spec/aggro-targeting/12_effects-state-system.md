# Unit 12 — Effects arm: AggroStateSystem (해제 · held 재계산 · 히트 적용)

> 구 `AggroAssignmentSystem` 개명(더 이상 근접 "배정" 아님). Effects 소유 — `Aggroed`/`AggroCapacity` 유일 writer.

## 목적

히트 이벤트를 소비해 capacity+선점 게이트 아래 `Aggroed` 를 부착하고, 사망 가디언 링크를 해제하며, held 를 재계산한다. **근접 후보 스캔/거리 선정 전량 삭제.**

## 변경 대상

- `Assets/_Project/Scripts/Battle/Effects/AggroAssignmentSystem.cs` → `AggroStateSystem.cs` (개명 + 재작성)

## 구현

`[UpdateInGroup(BattleSimGroup)]`, `[UpdateBefore(MovementSystem)]`. ISystem. ECB.

**Pass 1 — 해제**: 모든 `Aggroed` 순회. `AggroPolicy.ShouldRelease(guardianAlive)` 로 판정, `guardianAlive = Exists(g) && !HasComponent<DeadTag>(g) && Health[g].value > 0`(critic M1 — ECB 파괴분 + death-프레임 + HP 3중). 해제 시 `ecb.RemoveComponent<Aggroed>`.

**Pass 2 — held 재계산**: 살아있는 `Aggroed` 를 guardian 별 카운트 → `NativeHashMap<Entity,int> countByGuardian`. 각 가디언 `AggroCapacity.held = count`(full recompute).

**Pass 3 — 히트 드레인 (critic H1/잔여4 프로토콜)**: ECB.AddComponent 는 playback 전 `HasComponent` 로 안 보이므로 로컬 상태로 틱 내 정합성 유지.
```
claimed = NativeHashSet<Entity>          // 이번 틱 부착분
runningHeld = clone(countByGuardian)
while queue.TryDequeue(out var ev):
  if !HasComponent<AggroCapacity>(ev.guardian): continue          // 사망/비가디언 방어
  if claimed.Contains(ev.enemy) || HasComponent<Aggroed>(ev.enemy): continue  // 선점(기존+틱내)
  runningHeld.TryGetValue(ev.guardian, out int h)
  if !AggroPolicy.CanAcquire(h, capacity[ev.guardian], false): continue       // capacity
  ecb.AddComponent(ev.enemy, new Aggroed{ guardian = ev.guardian })
  claimed.Add(ev.enemy); runningHeld[ev.guardian] = h + 1
```

OnCreate: `RequireAnyForUpdate(AggroCapacity, Aggroed)` — 마지막 가디언 소멸 후에도 orphan 해제 유지(기존 HIGH1 보존).

## 완료 기준

- [ ] 컴파일 + Burst. NativeHashSet/HashMap/Temp Dispose.
- [ ] 히트 이벤트 → `Aggroed` 부착(Play). 근접만으로는 어그로 안 걸림(공격 전 walk-past).
- [ ] 여유 1슬롯에 같은 틱 2히트 → 1개만 부착(H1). 2 가디언이 같은 적 히트 → 1개만(선점).
- [ ] 가디언 사망 → 링크 적 전원 해제 → 출구 복귀.

완료: 2026-07-09 (AggroStateSystemTests 8 통과, critic M4 Exists 방어 / 커밋 `b84b6887`)
