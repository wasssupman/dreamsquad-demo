# enemy-spawn-positioning — 적 유닛 스폰 위치 개선

> 상태: 완료 2026-06-26 (sim 스폰 오프셋 · 커밋 `2487bb0`, `010a32e`)

## 목표 (검증 질문)

적 유닛이 (1) 이동타일 중심에 비주얼 피봇이 정렬되고(유닛 타입별 미세조정),
(2) 스폰 시 한 점에 겹치지 않고 타일 폭 안 상/중/하로 분산되어 **그 간격을 유지한 채 전방으로 행진**하는가?

현재: `BattleBridge:3496` 이 spawn 셀 **중심 한 점**(`GridToWorldCenter`)에 스폰 → 같은 lane 적들이
동일 속도·동일 flow 로 완벽히 포개진 채 행진한다. (수렴이 아니라 애초에 안 퍼뜨림.)

## 접근 (왜 이렇게 — 정합성)

`FlowFieldBuilder` 는 **cardinal 단위벡터만** 낸다(중심으로 당기는 성분 없음, `FlowFieldBuilder.cs:58~78`).
따라서 유닛을 스폰 셀 안의 **sub-cell 위치**(셀 중심 ± 측면 오프셋)에 두면 flow 가 그 오프셋을
**보존한 채** 전방으로 옮긴다. 이동 시스템은 손대지 않는다.

**핵심 불변식 — `|오프셋| < 0.5·tileSize` ⇒ 유닛이 스폰 셀 안에 머문다.**
`WorldToCell`(round-half)·goal 판정·cell-trim·`blockedCells` 등 **모든 셀 단위 시스템이 유닛을
동일 셀로 보므로 예외 없이 그대로 동작**한다. 측면 분산은 기존 셀 단위 계약의 sub-cell 확장이지 특례가 아니다.

## 공통 원칙 (feature-wide 계약)

- **측면 분산 = sim 스폰 위치.** ECS `LocalTransform` 시작점을 셀 중심 → 셀 내 sub-cell. flow 가 전방 보존. Movement 무수정.
- **lateral axis = `flow[spawnCell]` 의 XZ 수직.** 스폰이 좌측 가장자리가 아니어도 맵 토폴로지에서 유도(고정 월드축 하드코딩 금지). flow 0 인 셀은 폴백 축.
- **`|오프셋| < 0.5·tileSize` 강제** → 셀 불변 → 셀 단위 시스템 전부 무영향. 기본 spread = 타일폭의 1/3(±0.33).
- **비주얼 피봇 오프셋은 유닛 타입별** — `AttackUnitData.visualOffset`(프랍 `PropData.visualOffset` 동형). 기본 `(0,0,0)`=현재 동작 보존. 목표2(sim 위치)와 직교.
- **슬롯 수 / spread 비율 / 배정 모드(순차·랜덤)는 `BattleBridge` serialized 노브.** 하드코딩 수치 금지. 기본 3슬롯(상/중/하)·순차(lane별 round-robin). 랜덤은 map seed 결정론.
- **순수 계산**(슬롯→부호화 fraction)은 EditMode 단위 테스트로 회귀 고정.
- **ECS 경계**: `BattleBridge` 만 스폰 시 슬롯 배정. Movement/Units 맥락 무수정.

## 작업 단위

| # | 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | data+view | `0_visual_pivot_offset.md` | `AttackUnitData.visualOffset` + 적 View 적용 (목표 1) |
| 1 | spawn+test | `1_spawn_lateral_offset.md` | 셀 내 sub-cell 측면 오프셋 — `SpawnSpread` 순수헬퍼+EditMode, `BattleBridge` 슬롯배정·config (목표 2) |
| 2 | handoff | `2_handoff_summary.md` | 인계 요약 (구현 종료 시) |

## 코너·출구 거동 (수용)

- **직진 구간**: 간격 완벽 보존.
- **90° 코너**: 측면 간격이 진행축 앞뒤(lead-lag)로 회전(겹침 아님).
- **goal 부근**: 출구 깔때기로 모임(정상, 어차피 퇴장).

TD 직진 위주 맵이면 코너 회전은 거의 안 보임 → 수용. 항상-수직 스프레드가 필요해지면 후속(비주얼 수직추적).

## 후속 후보 (현 스코프 밖)

- **비주얼 수직추적**: View 가 진행방향 수직으로 렌더 → 코너에서도 항상 측면 유지. sim 은 중심선. 코너 회전이 거슬리면.
- 유닛 간 separation / boid 회피.
- 블록 시 우회 재라우팅(`BuildFlowField` rebuild 트리거) — 이동 아키텍처 별도 스펙.
- spawn 슬롯을 wave/유닛 단위 지정(현재 전역 config + 자동 배정).
