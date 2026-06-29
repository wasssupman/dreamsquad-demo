# enemy-tile-movement-integrity — 적 타일 이동 무결성 (이동 결함 픽스)

> 상태: 진행 중 (2026-06-29 착수). `movement-lane-centering` 초안에서 리프레임 — **"레인 시스템"은 명시적으로 폐기**, 결함 픽스만.

## 배경 / 문제

적은 cardinal flow field(`FlowFieldBuilder`: walk 셀 BFS, ±X/±Z 단위벡터, **중심 복원 성분 없음**)로 이동한다. 정상 flow 이동은 `MovementCellTrim` 으로 walk 타일 위에 제약되지만, 세 결함이 있다:

1. **코너 엣지-허깅** — flow 축이 90° 꺾일 때 직전 진행축 위치가 새 측면축 오프셋이 되어, 적이 턴 셀 안쪽 모서리(측정 |perp| 0.29~0.49)에 고정되고 복원력이 없어 그 엣지를 goal 까지 탄다. (근본원인: 하단 "코너 메커니즘")
2. **aggro 타일 이탈** — aggroed 적이 `MovementSystem` 에서 guardian 으로 직선 self-walk + `continue` 로 cell-trim 을 스킵 → 프랍/Place 타일 위로 이동, guardian 의 Place 타일에 적층.
3. **비결정론 스폰 분산** — 스폰 측면 오프셋이 `_spawnSpreadRng`(RNG)에 의존. 시뮬레이션은 구조적으로 결정론이어야 한다.

## 검증 질문

적이 **항상 walk 타일 위에서** 이동하고(aggro 중에도 프랍/Place 진입 0), 코너에서 안쪽 엣지에 얼어붙지 않으며, 같은 매치 입력에 **결정론적으로** 동일하게 움직이는가?

## 공통 원칙 (착수 전 확정 — 2026-06-29 설계 논의)

- **레인 시스템 안 만든다.** `laneIndex`/동적 재배정/lane 회귀/goal 레인 = 전부 폐기(후속 후보 II). 이 spec 은 **결함 3개 픽스**만.
- **① tile-invariant**: 모든 이동모드가 walk 셀 위. aggro 는 guardian 의 Place 타일 진입 대신 **인접 walk 타일에서 사거리로 정지**해 공격. (넉백 impulse 는 이미 cell-trim 제약 → 무변경. 토네이도/portal 은 의도된 오버라이드 → 제외.)
- **② 코너 복원 = `target=0 + dead-band`**: `dead-band ≈ 스폰 분산폭(≈0.2·tile)`. 직진 분산은 밴드 안이라 **불변(보존)**, 코너 드리프트(밴드 밖, 0.29~0.49)만 밴드로 복원. **유닛별 target 아님**(레인 없음 → 중앙 0 이 유일 target).
- **rate 속도비례**: `rate = k·follow.speed` (k≈0.4). 상수 rate 금지.
- **③ 결정론 스폰 분산**: RNG → 저불일치 결정론 수열(golden-ratio Weyl). `|offset| < 0.5·tile` 불변식 유지.
- **맥락 경계**: Movement 맥락만 `LocalTransform` 쓰기. 스폰 오프셋 세팅은 `BattleBridge` gateway.

## 작업 단위

| # | 작업 | 문서 | 목적 |
|---|---|---|---|
| 0 | 결정론 스폰 분산 (③) | `0_deterministic_spawn_spread.md` | `SpawnSpread` RNG → 결정론 수열. EditMode |
| 1 | 코너 복원 (②) | `1_corner_recenter.md` | 순수 헬퍼 + `MovementSystem` flow branch target=0+deadband. EditMode |
| 2 | aggro 타일 제약 (①) | `2_aggro_tile_constrain.md` | aggro self-walk cell-trim + 사거리 정지 + stuck 폴백. EditMode+Play |
| 3 | 통합 검증 | `3_verify.md` | Play: 타일 이탈 0 / 코너 비-엣지 / 결정론 재현 |

순서는 격리도 낮은→높은(안전→위험). 단위 간 강한 의존 없음.

## 맥락 / 경계

- **Movement 맥락만 수정** (`MovementSystem`, `MovementCellTrim`, `SpawnSpread`). 스폰 세팅은 `BattleBridge`.
- aggro/tornado/portal/past-goal 분기 중 **tornado·portal 제외**(의도된 오버라이드). **aggro 만** 타일 제약(unit 2).
- ③은 완료 spec `enemy-spawn-positioning` 의 `ComputeSpawnLateralOffset` 동작을 수정 → 교차참조.

## 후속 후보 (현 스코프 밖)

- **(II) 결정론 레인 대형 시스템** [L] · `laneIndex` + 동적 nearest-lane 재배정(코너 축변화·복도 재획득 시 snap) + 질서있는 lane 전진/분기(가디언별)/재집결. 2026-06-29 설계 논의에서 "버그 픽스 ≠ 기능"으로 분리. 동적 재배정 안은 코너까지 우아하게 처리함이 검증됨(축 바뀔 때 nearest-lane snap = 엣지-허깅 자동 해소). product 가치 확인 후 별도 spec.
- **aggro 타일 경로탐색** [M] · unit 2 는 greedy+clamp. guardian 이 벽 뒤면 stuck → 사거리 공격 폴백. 정식 경로탐색은 별도.

## 참조: 코너 엣지-허깅 메커니즘

동쪽 복도(forward=X, lateral=Z)를 타던 적이 턴 셀에서 북(forward=Z, lateral=X)으로 꺾이면, 턴 셀에 서쪽 경계로 진입한 X 위치가 새 lateral(X) 오프셋이 된다 → 안쪽 모서리(−0.5 근처) 고정. cardinal flow 에 복원력이 없어 그 엣지를 goal 까지 탄다. 측정 0.29~0.49 가 이와 일치. 진단 원본: `docs/spec/enemy-spawn-positioning/4_continuous_spread.md`.
