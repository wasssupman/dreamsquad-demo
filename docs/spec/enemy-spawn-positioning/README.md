# enemy-spawn-positioning — 적 유닛 스폰 위치 개선

> 상태: 완료 2026-06-29 (units 0~4 · 커밋 `2487bb0`,`010a32e`,`06cc883`). 코너 lane-centering 은 별도 스펙 `movement-lane-centering` 으로 분리.

## 목표 (검증 질문)

적 유닛이 (1) 이동타일 중심에 비주얼 피봇이 정렬되고(유닛 타입별 미세조정),
(2) 스폰 시 한 점에 겹치지 않고 **중앙 기준 ± 작은 변주**로 자연스럽게 분산되어 행진하는가?

이전: `BattleBridge` 가 spawn 셀 중심 한 점(`GridToWorldCenter`)에 스폰 → 같은 lane 적들이 완벽히 포개진 채 행진.

## 접근 (왜 이렇게 — 정합성)

`FlowFieldBuilder` 는 **cardinal 단위벡터만** 낸다(중심 당김 성분 없음, `FlowFieldBuilder.cs:58~78`).
유닛을 스폰 셀 안의 **sub-cell 위치**(셀 중심 ± 측면 오프셋)에 두면 flow 가 그 오프셋을 보존한 채 전방으로 옮긴다. 이동 시스템 무수정.

**핵심 불변식 — `|오프셋| < 0.5·tileSize` ⇒ 유닛이 스폰 셀에 머문다.**
`WorldToCell`·goal·cell-trim·`blockedCells` 등 셀 단위 시스템이 유닛을 동일 셀로 보므로 예외 없이 그대로 동작.
측면 분산은 기존 셀 단위 계약의 sub-cell 확장이지 특례가 아니다.

## 공통 원칙 (feature-wide 계약)

- **측면 분산 = sim 스폰 위치.** ECS `LocalTransform` 시작점을 셀 중심 → 셀 내 sub-cell. flow 가 전방 보존. Movement 무수정.
- **분산 = 중앙 기준 연속 랜덤 오프셋.** 스폰마다 `[−fraction, +fraction·topScale]` 에서 뽑는다(상/중/하 이산 슬롯 폐기 — unit 4).
- **lateral axis = `flow[spawnCell]` 의 XZ 수직.** 토폴로지 유도(고정 월드축 하드코딩 금지). flow 0 셀은 폴백 축.
- **`|오프셋| < 0.5·tileSize` 강제**(`SpawnSpread.MaxHalfFraction` + `LateralOffset` clamp) → 셀 단위 시스템 불변. 기본 fraction `±0.2`.
- **`topScale`(상단 압축)**: 키 큰 캐릭터가 화면 위로 솟지 않게 상단(+) 범위만 좁힘. 1=대칭, 기본 0.5.
- **비주얼 피봇 오프셋은 유닛 타입별** — `AttackUnitData.visualOffset`. 기본 `(0,0,0)` 무회귀. 목표2(sim)와 직교.
- **RNG 결정론**: `_spawnSpreadRng` 는 map seed 로 빌드마다 리셋.
- **순수 계산**(분율 범위/수직/오프셋)은 EditMode 회귀 고정. **ECS 경계**: `BattleBridge` 만 스폰 시 오프셋 계산.

## 작업 단위

| # | 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | data+view | `0_visual_pivot_offset.md` | `AttackUnitData.visualOffset` + 적 View 적용 (목표 1) |
| 1 | spawn+test | `1_spawn_lateral_offset.md` | 셀 내 sub-cell 측면 오프셋 토대 (목표 2) |
| 2 | handoff | `2_handoff_summary.md` | 인계 요약 (1차) |
| 3 | spawn(rev) | `3_asymmetric_spread.md` | 상단 슬롯 압축(`topScale`) — unit 4 에 흡수 |
| 4 | spawn(rev) | `4_continuous_spread.md` | 상/중/하 슬롯 → 중앙 ± 연속 랜덤 교체 (**현행 분산 방식**) |

## 코너·출구 거동 (수용)

- **직진**: 변주 보존. **90° 코너**: 측면 변주가 진행축 앞뒤로 회전(겹침 아님). **goal**: 출구 깔때기로 모임(정상).
- TD 직진 위주 맵이면 자연스러움. 코너 엣지-허깅(±0.49) 복원은 별도 스펙 `movement-lane-centering` 으로 분리(2026-06-29 리뷰).

## 후속 후보 (현 스코프 밖)

- **코너 lane-centering** → `docs/spec/movement-lane-centering/` 초안(2026-06-29 리뷰로 분리). 코너 엣지-허깅 복원 — 본 spec 스코프 밖.
- 유닛 간 separation / boid 회피.
- 블록 시 우회 재라우팅(`BuildFlowField` rebuild 트리거) — 이동 아키텍처 별도 스펙(flow field 유지 결론).
- Quad 폴백 경로 `visualOffset` 미배선(적=Spine 라 무영향).
