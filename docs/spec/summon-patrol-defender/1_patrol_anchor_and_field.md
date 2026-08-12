# unit 1 — 거점 컴포넌트 + 박스 한정 이동 필드

## 목적

순찰병이 "거점 박스 안에서만 움직이며 적을 마중 나간다"를 성립시키는 계산을 만든다. **신규 이동 알고리즘을 쓰지 않는다** — 박스 제약을 walkMask 마스킹으로 표현하면 기존 BFS·하강을 그대로 재사용할 수 있다.

## 변경 대상

- 신규 `Assets/_Project/Scripts/Battle/Movement/PatrolAnchor.cs`
- 신규 `Assets/_Project/Scripts/Battle/Effects/PatrolStep.cs`
- 신규 `Assets/_Project/Scripts/Battle/Effects/PatrolAreaMath.cs` (순수)
- 신규 `Assets/_Project/Scripts/Battle/Effects/PatrolFieldSystem.cs`
- 신규 `Assets/_Project/Tests/EditMode/PatrolAreaMathTests.cs`

## 구현

**컴포넌트 2개 — 소유 맥락이 다르다.**

```csharp
// Movement 소유 — 이동 제약. writer = Bridge(스폰·재배치). Effects/Movement 는 RO.
struct PatrolAnchor : IComponentData { int2 cell; int tileRadius; }

// Effects 소유 — 이번 틱의 이동 방향. writer = PatrolFieldSystem 단독. Movement 는 RO.
struct PatrolStep : IComponentData { float2 dir; }
```

두 컴포넌트를 나누는 근거는 **오늘의 맥락 소유권**이다(미래 확장이 아니다). anchor 는 Bridge 가 쓰고 이동이 소비하는 제약이고, step 은 Effects 가 매 틱 굽는 결과다.

**`PatrolAreaMath` (순수, EditMode 대상)**

- `IsInArea(cell, anchorCell, tileRadius)` — Chebyshev 구역 멤버십.
- `FillAreaMask(walkMask, gridSize, anchorCell, tileRadius, outMask)` — 구역 밖 셀을 0 으로 지운 walkMask 사본. **버퍼를 스스로 0 으로 지운다**(호출자 계약으로 두면 버퍼 재사용 최적화가 들어오는 순간 앞 엔티티의 구역이 뒤 엔티티에 새어 순찰병이 거점을 벗어난다).
- `StepDir(...)` — 단일 진입점. 이번 틱 이동 방향(cardinal 또는 zero).

**목적지 선정은 "최근접 적 1체"가 아니라 N-소스 BFS 다.** 최근접 1체를 먼저 고르면 벽으로 갈린 구역에서 **도달 불가한 최근접 적** 때문에 같은 구역의 도달 가능한 적을 통째로 포기한다(코앞의 적을 두고 뒷걸음질). 구역 안 적 **전원**의 사격 위치를 소스로 한 번에 굽으면 "갈 수 있는 사격 위치 중 가장 가까운 곳"이 자동으로 나오고, BFS 횟수는 1회로 같다.

**`PatrolFieldSystem` (Effects, `[UpdateBefore(MovementSystem)]`)**

매 틱 `PatrolAnchor` 보유 엔티티마다:

1. `MovementCellTrim.FillWalkMask` 로 벽/장애물 마스크 생성(프레임당 1회, `AggroStateSystem` 과 공유하는 단일 술어) → `FillAreaMask` 로 구역 마스킹
2. `StepDir` 호출 → `PatrolStep.dir` 기록

`StepDir` 내부 분기:

- 자기 셀이 구역 **밖**(외력에 밀림) → 마스킹 **없는** walkMask 로 거점 복귀 경로
- 구역 안 적 있음 → `FlowFieldBuilder.CollectDefenderSources`(적 전원, `tileRange` = `AttackState.range` 타일 환산) + `BuildFromSources` → `FlowRecovery.RecoveryDir` 하강
- 소스 0 또는 자기 셀 도달 불가 → 적 포기, 거점 복귀
- 이미 거점 → `zero`

**거점 복귀는 `AggroChaseMath.BuildChaseField` 를 쓰지 않는다.** 그쪽이 부르는 `CollectDefenderSources` 는 **중심 셀을 제외**한다(방어유닛 자기 셀 = `Place` = 벽 전제). 거점은 walk 셀이고 순찰병이 실제로 서야 하는 칸이라 소스에서 빠지면 안 된다 → `FlowFieldBuilder.BuildFromSources` 를 직접 부른다.

**거점 복귀에 `dist == MaxValue` 가드를 두지 않는다.** 그 값은 ⑴ 진짜 고립(`RecoveryDir` 이 알아서 zero) ⑵ **자기 셀이 마스크 0**(발밑 차단 해저드) 두 상황에서 나오는데, ⑵ 에서 가드를 두면 탈출 자체를 막아 순찰병이 장애물 안에 영구히 박힌다.

**박스 밖 시작을 반드시 다룬다** (README 계약 6). 포털/토네이도/임펄스가 순찰병을 박스 밖으로 밀 수 있다. 마스킹된 walkMask 로는 박스 밖 셀의 `dist` 가 `int.MaxValue` 라 하강이 zero 가 되어 **영구 정지**한다. 그래서 현재 셀이 박스 밖이면 **anchor 로의 복귀 경로를 마스킹 없는 walkMask 로** 계산한다.

**그리디 스텝 금지** (README 계약 3). 8-이웃 대각 이동도 금지 — 백로그 "대각 코너 슬립 차단"이 미수리이고 현행 이동이 cardinal 인 것은 의도다.

## 완료 기준

- [ ] EditMode `PatrolAreaMathTests`:
  - 박스 안 적 1체 → 그 적 방향으로 cardinal dir
  - 박스 밖 적만 존재 → anchor 복귀 dir (적을 향하지 않음)
  - 적 없음 + 이미 anchor → `dir == zero`
  - **박스 밖 시작** → anchor 쪽 복귀 dir (zero 아님)
  - **U자 벽으로 막힌 박스** → 고착 없이 우회 경로 dir, 도달 불가면 zero
  - 박스 안에 walk 셀이 하나도 없음 → zero (크래시 없음)
- [ ] 기존 EditMode 스위트 전량 통과
- [ ] 이 unit 단독으로는 화면 변화 없음 (`PatrolStep` 소비는 unit 2)
- [ ] 콘솔 에러/경고 0

---

**완료 기준 확인**: 2026-08-03 · 커밋 `68d2f35c` · 신규 `PatrolAreaMathTests`(213줄)가 박스 안/밖 적·복귀 dir·U자 벽 우회·walk 셀 없음을 EditMode 로 고정한다.
**이후 두 번 바뀌었다** — `traversal-layers` unit 3(`5df1b930`)이 이 필드를 통행 층 인지로 바꿨고, unit 9(`515e5f00`)가 반경 출처를 소환사 `attackRange` 로 옮겨 `leashTileRadius` 를 은퇴시켰다. **현행 계약은 `9_coverage_from_attack_range.md` 가 정본**이다.
(체크박스 소급 기록 — 위 주석 참조.)
