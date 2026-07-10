# Enemy Hunter Targeting — 보스가 방어유닛을 추격 (누수 방지)

> 상태: **스펙 작성 중** — 2026-07-10 착수. nightmare-catcher 실플레이에서 발견된 범위 밖 이슈를 별도 spec 으로 분리(사용자 확정).
>
> 배경: `docs/spec/nightmare-catcher/` (보스 콘텐츠 출처), 실플레이 피드백 2026-07-10.

## 목표

보스가 **타겟이 없으면 goal 로 걸어가 누수**되는 문제를 고친다. 사용자 요구:

- 맵에 배치된 방어유닛이 **하나도 없으면** → goal 로 이동 (기존 동작 유지).
- 방어유닛이 **하나라도 있으면** → 가장 가까운 방어유닛을 추격해 **공격 상태 전환이 계속 이어진다** (누수 안 함).

즉 보스는 "goal 로 새는 적"이 아니라 "방어유닛을 사냥하는 적"이다. 전멸시켜야만 goal 에 도달한다.

## 검증 질문

> 보스가 사거리 밖 방어유닛을 향해 **스스로 추격**하다가, 사거리에 들면 멈춰 공격하고, 그 유닛이 죽으면 다음 최근접으로 이어 추격하는가? 맵에 방어유닛이 0마리일 때만 goal 로 이동하는가? 일반 적(비-헌터)의 기존 march/aggro 동작은 **무회귀**인가?

## 사용자 확정 결정 (2026-07-10)

1. **적용 = 보스 전용** (`BossTag` 게이트). 일반 적은 지금처럼 goal 로 진행(누수 = 정상 TD 압박). 데이터 플래그 일반화는 후속(YAGNI).
2. **컨테이너 = 별도 mini-spec** (공유 FSM/Movement 를 건드리므로 렌즈 B 대상).
3. **타겟 = 최근접 방어유닛** (위협 리더 아님 — 교전 목적. 텔레포트만 위협 리더).

## 작업 단위

| # | 문서 | 작업 | 목적 |
|---|---|---|---|
| 0 | `0_hunt_logic.md` | 계약+테스트 | `HuntTarget` 컴포넌트 + 최근접 선정 순수함수 + `Evaluate` 확장(hunter→Chasing) + EditMode |
| 1 | `1_fsm_and_bake.md` | 배선 | `EnemyAiStateSystem` 헌터 판정(BossTag 게이트, 최근접 계산, HuntTarget+Chasing set) + BattleBridge 보스 HuntTarget 베이크 |
| 2 | `2_movement_chase.md` | 배선 | `MovementSystem` chasing 분기 확장 — aggro 없으면 HuntTarget anchor 로 self-walk |
| 3 | `3_play_validation.md` | 검증 | Play e2e(추격·교전전환·연쇄·0마리 goal·무회귀) + 렌즈 B |

## Feature-wide 계약 (load-bearing)

1. **보스 전용, BossTag 게이트.** 헌터 판정은 `BossTag` 보유 적만. 비-보스는 `EnemyAiStateSystem.Evaluate` 결과 불변(무회귀).
2. **새 AiState 없음 — `Chasing` 재사용.** 헌터 추격은 기존 `Chasing` 상태(이동목표 goal→anchor). aggro Chasing 은 guardian anchor, 헌터 Chasing 은 HuntTarget anchor. FSM 상태 폭발 금지(nightmare-catcher 계약 4 원칙 계승).
3. **맥락 경계.** `HuntTarget`(Combat 소유)은 `EnemyAiStateSystem`(Combat)만 쓴다. `MovementSystem`(Movement)은 RO 로 읽어 이동만. 위치 쓰기는 Movement.
4. **타겟 = 최근접 방어유닛**, 결정론(cell 거리 → 동점 entity index). FSM 이 이미 뜨는 후보 스냅샷(AttackSystem 동일 풀) 재사용.
5. **전이 규칙**: 비-aggro 헌터가 (a) 사거리 내 타겟 있음 → `Engaging`(기존, 멈춰 공격) (b) 없지만 방어유닛 존재 → `Chasing`(HuntTarget=최근접) (c) 방어유닛 0 → `Marching`(goal). 사거리 진입 시 자연히 Chasing→Engaging.
6. **HuntTarget 은 스폰 베이크로 사전 부착**(BossTag 경로). FSM 은 값만 write(핫패스 구조변경 금지). 비-보스는 미부착.
7. **aggro 우선.** 보스가 `Aggroed`면 기존 aggro Chasing(guardian) 유지 — 헌터 로직은 비-aggro 일 때만. (보스 어그로 면역은 별도 후속.)

## 파이프라인 커버리지

신규 플레이 오브젝트 없음 — AI/이동 행동 확장. 생성→렌더 경로 변경 0. `HuntTarget` 컴포넌트 1개 추가(신규 채널 0, teardown 은 AttackUnitTag 상속).

## 후속 후보 (스코프 밖)

- **일반 헌터 아키타입** — `AttackUnitData` 데이터 플래그로 임의 적을 헌터로. 두 번째 수요 생기면.
- **보스 어그로 면역** — 헌터 보스가 가디언 자석에 저항(현재는 aggro 우선). `BossTag` 게이트 1줄. nightmare-catcher 후속과 동일 항목.
- **추격 타겟 정책** — 최근접 외(최저 HP·최고 위협 등) 선택 정책. 지금은 최근접 고정.
- **추격 중 경로 스마트** — 현재 HuntTarget 로 직선 self-walk(cell-trim). 장애물 우회는 flow-field 재활용 검토.
