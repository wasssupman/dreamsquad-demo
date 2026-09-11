# 2 — 다중 타격 선정 두 경로에 게이트

## 목적

`AttackSystem` 의 다중 타격 대상 선정이 **주 대상 방향 도형** 안에서만 일어나게 한다. 주 대상 선정
(`:615` 최근접 · focus/frontmost/sticky 락 · RESOLVE 재판정)은 **건드리지 않는다** — 결정 1.

## 변경 대상

- `Combat/AttackSystem.cs` — Outputs 경로 `:1411~1548`
  - 일반: pass 루프 `:1502` (`hitMaskO`)
  - 가디언: `AggroTargeting.SelectTargets` 호출 `:1449`
- `Combat/AggroTargeting.cs` — `SelectTargets`/`FillNearest` 시그니처에 도형 인자
- `Tests/EditMode/AggroAoeWidthTests.cs`(기존) 무변 확인 · `AttackShapeSelectionTests.cs` 신규

## 구현

**선정 경로는 둘뿐이다**(「legacy melee path」 주석은 stale — outputs 경로만 산다).

### 일반 경로 (`:1492~`)

`bestTarget` 이 이미 정해져 있다. pass 루프 **앞에서 한 번**:

```
u = (bestTargetPos − atkPos).xz / |·|        ← lengthsq < SameSpotEpsSq 면 게이트 off (계약 7)
```

루프 안 `AttackReach.InReach(...)` 통과 **직후**에 `shapeKind` 로 분기해 `AttackReach.InSector/InLane`
(**Rect 분기도 구현한다** — 결정 8: bake 되는데 게이트가 무시하는 kind 는 조용한 함정)
(tileSize 나눗셈은 래퍼가). Omni 면 분기 자체를 건너뛴다 — 오늘 경로와 명령 수가 같다.
`rankByHealth`(힐러) 분기도 같은 필터를 지난다 — 후보에 못 들면 순위에 못 든다.

### 가디언 경로 (`AggroTargeting`)

`SelectTargets` 가 **primary 를 스스로 고른다**(`outIdx[0]`). 그래서 방향은 선정 도중에 생긴다:

- `FillNearest` 에서 `count == 0` 이면 게이트 없음(원 — 주 대상 획득), `count ≥ 1` 부터
  `u = cands[outIdx[0]].pos − gPos` 로 게이트.
- Pass A(비-어그로 우선)·Pass B(잔여) 둘 다 같은 규칙 — 방향은 outIdx[0] 하나다.
- 시그니처에 `shapeKind/sinHalf/cosHalf/halfWidth` 를 더한다. Omni 호출은 오늘과 같은 답이라
  `AggroAoeWidthTests` 가 그대로 초록이어야 한다.
- ⚠ 그 뒤 `keepFrontmostPrimary` 가 primary 를 frontmost 로 swap 하면 게이트 방향(swap 전 primary)과
  어긋난다. README 후속 후보 — 이 unit 은 기록만.

### 손대지 않는 것

- `committedTarget` RESOLVE 재판정 — 주 대상 단독.
- `EnemyAiStateSystem`·`HazardCastSystem`·`DetectionSystem`·`PatrolAreaMath` — «멈춰도 되나/발견했나»
  는 획득 질문이라 도형과 무관.
- `AreaSleepSkill` 등 스킬 layer 의 `attackTargetCount` 소비 — 후속 후보.

## 완료 기준

- [ ] `AttackShapeSelectionTests`: 공격자 1 + 후보 4(전방·측면 θ 안·측면 θ 밖·등 뒤) 배치.
      `Sector(90°) · N=3` → 전방·θ 안 만 히트(2체, N 미달은 정상) · `Omni · N=3` → 최근접 3.
      `Rect(1.0) · N=3` → 일렬 3체 히트, 측면 1.0 밖 out. 가디언(AggroCapacity) 버전 동일 단언.
- [ ] **주 대상은 언제나 `hitTargets[0]`** — 도형이 있어도 primary 가 빠지지 않는다(계약 2 의 sim 증명).
- [ ] `AggroAoeWidthTests` · `AttackReachTests` · `RangePredicateInvariantsTests` 무변 초록.
- [ ] 골든 코퍼스 전건 초록(저작 0 이므로 무변이어야 한다 — 빨가면 게이트가 Omni 를 못 지나는 것).
- [ ] Burst: `AttackSystem` OnUpdate 의 lookup 호출을 지우지 않는다(4번째 재발 함정, `AttackSystem.cs:32`).
