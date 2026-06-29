# movement-lane-centering — 코너 lane-centering (적 이동 품질)

> 상태: 초안 (2026-06-29 · 미착수). `enemy-spawn-positioning` 에서 분리.

## 배경 / 문제

적은 cardinal flow field 로 이동한다(`FlowFieldBuilder`: 셀마다 ±X/±Z 단위벡터, **중심 복원 성분 없음**).
코너에서 유닛이 새 복도에 **셀 경계로 진입한 직후** 새 방향으로 진행하면서 수직 위치가 셀 안쪽 엣지(±0.49·tile)에
"얼어붙고", flow 엔 복원력이 없어 그 엣지를 goal 까지 탄다 → 유닛이 이동타일을 벗어난 것처럼 보인다.

실측(running Play, `execute_code`): 적 13마리 중 3마리가 코너 직후 `|perp|` 0.29~0.49. spawn spread(상한 0.1~0.2)·
넉백(CC 버퍼 empty) **무관**으로 확인됨. `enemy-spawn-positioning` README "코너·출구 거동 (수용)" 에서 받아들였던 항목.

## 검증 질문

적이 코너를 지나도 복도 **중앙 근처**를 타고(안쪽 엣지에 얼어붙지 않고), **재겹침 없이** 자연스럽게 이동하는가?

## 선결 설계 결정 (착수 전 반드시 — 2026-06-29 리뷰 도출)

1. **복원 목표: `target=0` + dead-band vs 유닛별 target.**
   - 유닛별 target(스폰 spread 값) 안: **부호 스칼라의 기하학적 방향이 턴마다 회전**(`p2` 뒤집힘) →
     serpentine 에서 서로 다른 target 이 같은 오프셋으로 수렴해 **재겹침 가능**(설계비평 C1). spread band 가 작아 target 들이 뭉침.
   - `target=0` + dead-band(`|perp|<deadband` 면 복원 안 함) 안: C1 회피·단순. 단 **동시 버스트**(같은 프레임·같은 점 스폰)는
     X 동일 → 0 수렴 시 재겹침 우려. 동시성 정도에 의존.
   - → **wave 가 동시 버스트인지 시간차 스폰인지 먼저 확인** 후 어느 쪽인지 결정.

## 구현 요구사항 (리뷰 반영 — 결정 후 상세화)

- **rate 는 속도비례**: moveSpeed 1.5~7.2(Runner 7.2). 상수 rate 금지 → `rate = k·follow.speed` (k≈0.3~0.5). (설계비평 M3)
- **cell-trim 순서 명시 + 재조립 perp 를 축별 `±(0.5·tile−ε)` clamp**(recompose 후). 벽 침투/clamp 상쇄 방지. (M1, m2)
- **임펄스 측면 성분 보존**: `flowStep` 만 분해(임펄스 제외)하거나 측면 감쇠를 명시 문서화. (M2)
- **recovDir(zero-flow) 분기선 centering 스킵.** (m1)
- **ECS**(ecs-reviewer): 유닛별 target 채택 시 `RefRO<LaneOffset>` 를 **쿼리에**(ComponentLookup 금지). 셀중심=
  `GridMath.CellToWorldCenter`, 수직=`SpawnSpread.Perpendicular` 재사용. `LaneCenteringSingleton` teardown 을
  `DestroyEcsInfrastructureEntities` 에 + 생성 전 idempotent destroy(`TeardownFlowField` 패턴). helper `[BurstCompile]`.
- **순수 헬퍼 + EditMode**: relax 수학 + projection→relax→reconstruct 전체 사이클(직진/코너/zero-flow) + 셀 불변식 clamp.
  PlayMode 통합 1개(코너 통과 시 perp 수렴) 권장.

## 맥락 / 경계

- **Movement 맥락만 수정**(`LocalTransform` 쓰기). 스폰 시 컴포넌트 세팅은 `BattleBridge` gateway.
- aggro/tornado/portal/past-goal 은 flow branch 밖이라 **자동 제외**(centering 미적용).

## 작업 단위 (잠정 — 설계결정 후 확정)

| # | 구분 | 목적 |
|---|---|---|
| 0 | design | 복원목표(0+deadband vs 유닛별) + rate 모델 결정. wave 동시성 확인 |
| 1 | helper+test | `LaneCentering` 순수 헬퍼 + EditMode(전체 사이클·clamp) |
| 2 | movement | `MovementSystem` 통합(cell-trim 순서·임펄스·recovDir) + (유닛별 시) `LaneOffset`/싱글톤 + teardown |
| 3 | verify | Play 코너 통과 수렴 확인 |

## 참조

- 진단/측정: `docs/spec/enemy-spawn-positioning/4_continuous_spread.md` 완료 라인.
- spawn spread 헬퍼/불변식: `Assets/_Project/Scripts/Battle/Movement/SpawnSpread.cs`.
- 리뷰 원문: 2026-06-29 ecs-reviewer + 설계 비평(세션 기록). 핵심: C1(부호 방향 비일관)·C2(스코프)·M1~M3.
