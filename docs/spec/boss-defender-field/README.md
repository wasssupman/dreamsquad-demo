# Boss Defender Field — 보스 방어유닛-지향 flow field 이동

> 상태: **완료 2026-07-11** (units 0~5, 커밋 `dc298ceb`·`7aa2277e` — EditMode 654/652 pass + Play e2e 트레이스 + 사용자 실플레이 확인. unit 5 = 소스 규칙 버그픽스(4-이웃 → 공격가능 반경). handoff: `4_handoff_summary.md`)
>
> 배경: `docs/spec/enemy-hunter-targeting/` (폐기 2026-07-11 — 직선추격+wall-slide 땜빵 사슬로 전량 revert. **그 README 의 폐기 사유를 먼저 읽을 것**). 이 spec 은 그 backlog 승격분 — 같은 목표를 필드 기반으로 재설계.

## 목표 (사용자 확정 2026-07-11)

보스의 행동 규칙은 두 줄이다:

1. 맵에 방어유닛이 **없으면** → goal 로 걸어간다 (기존 마칭, 코드 변경 0).
2. 방어유닛이 **하나라도 있으면** → 가장 가까운 방어유닛에게 걸어가서, 사거리에 들면 멈춰 공격한다 (**전멸까지 사냥, leak-proof**). 지나쳤거나 뒤·옆에 새로 배치된 유닛에게도 되돌아가 재교전한다.

"걸어가서"가 유일한 비자명 지점이다 — 방어유닛은 **Place 셀(=walkMask 0, 벽)** 위에 있어서 직선 이동은 벽에 박힌다(폐기 spec 의 사인). 해법: 방어유닛들의 **walkable 이웃 셀을 소스로 multi-source BFS** 를 돌려 "방어유닛-지향 flow field" 를 만들고, 보스는 goal 마칭과 **똑같은 flow-follow 코드**로 그 필드만 따라간다. 신규 이동 코드 0, 타겟 선정 코드 0 (BFS 가 자동으로 최근접 소스를 가리킨다).

## 검증 질문

> 보스 뒤/옆에 방어유닛을 배치하면 보스가 **되돌아가** 교전하는가? 죽이면 다음 최근접으로 이어가는가? 방어유닛 0 이면 goal 로 가는가? 일반 적(비-보스) 마칭/aggro 는 무회귀인가?

## 작업 단위

| # | 문서 | 작업 | 목적 |
|---|---|---|---|
| 0 | `0_multi_source_bfs.md` | 순수함수+테스트 | `FlowFieldBuilder.BuildFromSources` + 소스 수집 헬퍼 + EditMode |
| 1 | `1_field_singleton_system.md` | 배선 | `DefenderFieldSingleton` + BattleBridge 생성/teardown + `DefenderFieldSystem`(Effects, 매 프레임 재빌드) |
| 2 | `2_movement_branch.md` | 배선 | `MovementSystem` 보스 분기 — hunt-dist 유한하면 defender field 를 따름 |
| 3 | `3_play_validation.md` | 검증 | Play e2e (재교전·연쇄·0마리 goal·무회귀) |

## Feature-wide 계약 (load-bearing)

1. **보스 전용, `BossTag` 게이트** (RO lookup). 비-보스 이동은 diff 0.
2. **새 FSM 상태 0, FSM 코드 변경 0.** 사냥은 `Marching` 상태 그대로 — "어느 field 를 따르나"는 Movement 내부 상세. 사거리 진입 → 기존 `Engaging`(`engageMovement=Halt`) 이 멈춤·공격을 담당하고, 타겟 사망 → `Marching` 복귀 → 필드가 다음 최근접을 가리킨다.
3. **`DefenderFieldSingleton` 은 Effects 소유.** 유일 writer = `DefenderFieldSystem`(Effects). Movement 는 RO 소비. BattleBridge 는 생성/teardown 만 (기존 `FlowFieldSingleton` 라이프사이클 미러, 멱등).
4. **매 프레임 재빌드, 이벤트 훅 없음.** 그리드가 작아 Burst BFS 1회는 무시 가능 — 배치/사망 추적 상태·재빌드 트리거·신규 NativeQueue 채널 전부 0.
5. **fallback = per-frame, 무상태**: 보스 현재 셀의 hunt-dist 가 `int.MaxValue` 면 그 프레임은 goal flow 를 따른다. "방어유닛 0"과 "도달불가 방어유닛만 존재"가 같은 규칙으로 처리 → 정지 softlock 구조적 불가.
6. **소스 = 방어유닛을 공격 가능한 walkable 셀 — Chebyshev ≤ R, R = 동시 헌터 사거리(타일)의 min(클램프 ≥1)** (unit 5 rev — 초기 "4-이웃" 규칙은 레인 비인접 배치를 전부 놓침). FSM `HasFireTarget` 과 같은 메트릭이라 소스 도달 = Engaging 전이 보장, min fold 라 짧은 사거리 헌터의 dist-0 스톨 구조적 불가. 사거리 밖 초심층 배치는 소스 미기여 → 자연히 fallback(보스가 물리적으로 공격 불가한 대상). Place 셀 자체는 벽이라 직접 seed 불가.
7. **벽 판정은 goal field 유지.** `MovementCellTrim`/`IsWallCell` 은 계속 기존 `FlowFieldSingleton` 사용 — defender field 의 zero-flow 는 소스 셀(dist 0)도 포함하므로 벽 프록시로 쓰면 오판.
8. **직선추격·wall-slide 재도입 금지** (폐기 spec 함정). 이동은 오직 필드 flow-follow.
9. **결정론**: multi-source BFS 의 dist 는 소스 삽입 순서와 무관, flow 채움은 dist 기반 별도 패스(타이는 `Dirs` 순서 고정) — seeded RNG 없음.

## 파이프라인 커버리지

N/A — 신규 플레이 오브젝트 0, 생성→렌더 경로 변경 0. 시뮬 전용 싱글톤 1 + ISystem 1 + 이동 분기.

## 후속 후보 (스코프 밖)

- **일반 헌터 아키타입** — `EnemyBehavior`/SO 데이터 플래그로 임의 적을 헌터로. 두 번째 수요 생기면 (BossTag 게이트 → 플래그 1줄 교체).
- **보스 어그로 면역** — 현재는 aggro 우선(가디언 자석이 사냥을 중단시킴, 기존 동작 유지). 면역은 별도 결정.
- **필드 dirty-skip 최적화** — 방어유닛 셀 집합 불변이면 재빌드 skip. 프로파일에서 문제 될 때만. (보스 부재 skip 은 unit 5 에서 반영됨)
- **R-별 필드 분리** — 이질 사거리 다중 보스가 동시에 사냥할 때, "긴 사거리 보스만 공격 가능한 심층 배치"까지 사냥하려면 사거리별 필드 필요. 현 콘텐츠(보스 1종)에선 불필요.
- **추격 타겟 정책** — 최근접 외(최저 HP 등). BFS 소스 가중치로 표현 가능.
