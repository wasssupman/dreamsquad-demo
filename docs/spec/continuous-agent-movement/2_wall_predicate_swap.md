# unit 2 — 벽 술어를 zero-flow → 정적 walk 마스크로 교체

## 목적

"이 칸을 걸을 수 없는가"의 판단 근거를 **경로 계산 결과(flow)**에서 **지형 데이터(walk 마스크)**로 바꾼다.

현행 `IsWallCell` 은 `flow == 0` 을 벽으로 읽는다. 이는 **경로의 결과에 벽의 정의를 얹은 형태**라 두 가지를 못 한다:

1. **D1-b(장애물이 경로를 바꾼다)를 켤 수 없다.** 봉쇄로 필드가 끊기면 차단된 구역의 모든 셀이 `dist=MaxValue / flow=0` 이 되어 **구역 전체가 벽**이 된다. 적이 벽 위에 서 있는 상태가 되고 clamp 거동이 무너진다.
2. **평활화 레이캐스트(unit 7)가 쓸 수 없다.** 가시선을 그으려면 "지형이 막혔나"를 물어야 하는데 flow 는 "거기서 어디로 가나"만 안다.

**이 unit 은 동작을 바꾼다.** unit 1 과 달리 테스트 기대값이 바뀌는 것이 정상이다.

## 변경 대상

- 수정: `Assets/_Project/Scripts/Battle/Movement/NavGrid.cs` — 우선순위 반전, `flow`/`goals`/`goalCell`/`IsGoalCell` 제거
- 수정: `Assets/_Project/Scripts/Battle/Movement/MovementCellTrim.cs` — `IsWallCell`·`DefaultObstacles` 은퇴, `BuildNavGrid` 인자 축소
- 수정 (테스트): `NavGridTests` · `MovementCellTrimTests` · `MovementCellTrimApplyTests` · `FillWalkMaskTests` + 실행 결과가 바뀌는 픽스처

## 구현

### 술어

```
IsBlocked(cell) = !InBounds(cell)
               || staticWalk[cell] == 0
               || (hasObstacles && blockedCells.Contains(cell))
```

`flow` 를 보지 않으므로 골 예외도 사라진다 — 골은 `tiles == Walk` 라 마스크에서 이미 통행 가능이다. (골이 Walk 가 아닌 맵이 생기면 그건 맵 저작 결함이지 술어가 감쌀 일이 아니다.)

### 마스크 미생성 시

**평지로 본다** — 정적 벽 없음, 장애물만 판정. 프로덕션에선 `SimFieldInstaller` 가 항상 채우므로 해당 없고, 이 규약은 마스크를 안 쓰는 EditMode 픽스처를 보호한다(`goals` 를 `IsCreated` 에서 뺀 것과 같은 전략).

그 대가로 **벽 거동을 검증하는 테스트는 마스크를 명시해야 한다.** 그게 이 unit 의 테스트 작업이다.

### 의미 변화 (의도된 것)

| 셀 | 이전 (flow) | 이후 (마스크) |
|---|---|---|
| 비-Walk 타일 | 벽 | 벽 (동일) |
| 골에서 도달 가능한 Walk | 통행 | 통행 (동일) |
| 골 셀 | 통행 (명시 예외) | 통행 (마스크가 이미 1) |
| **골에서 도달 불가한 Walk** | **벽** | **통행** ← 유일한 차이 |

마지막 줄이 이 unit 의 전부다. 이동 자체는 안 깨진다 — `MovementSystem` 은 zero-flow 셀에서 이미 `FlowRecovery.RecoveryDir` 로 복구하고, 고립이면 정지한다(외력은 유지). 그 경로가 이제 **clamp 이 아니라 정지**로 처리된다.

## 테스트 갱신 원칙

**일괄 갱신 금지.** 기대값이 바뀌는 테스트마다 위 표의 어느 행에 해당하는지 적고 넘어간다. 표의 4행("도달 불가 Walk")이 아닌 이유로 깨지는 테스트가 있으면 **술어가 의도보다 넓게 바뀐 것**이므로 멈추고 원인을 본다.

`NavGridTests.Flow_Wins_Over_Mask_When_Both_Present` 는 unit 1 이 "unit 2 에서 반전시킬 것"이라 주석해 둔 케이스다 — 반전이 아니라 **의미가 사라지므로** 마스크 우선을 검증하는 케이스로 교체한다.

## 완료 기준

- [ ] compile 통과 (콘솔 에러 0)
- [ ] `MovementCellTrim.IsWallCell` 소멸 — grep 결과 0
- [ ] `NavGrid` 에 `flow`/`goals`/`goalCell` 없음
- [ ] EditMode 실패 0. **갱신한 테스트마다 위 표의 해당 행을 근거로 기록**
- [ ] Play 스모크: 적 이동·보스 사냥·순찰병·차단 해저드 정상
- [ ] **봉쇄 시나리오** — 차단 해저드로 경로를 완전히 막았을 때 차단 구역의 적이 벽에 갇히지 않고 해저드를 때리는가. (필드 재빌드는 unit 5 라 *경로가 바뀌지는 않는다*. 여기서 볼 것은 "구역 전체가 벽이 되는 사고가 사라졌는가"뿐)
- [ ] `ecs-reviewer` 통과

---

**완료 기준 확인**: (미확인)
