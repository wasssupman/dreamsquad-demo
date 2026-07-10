# Enemy Hunter Targeting — 보스가 방어유닛을 추격 (누수 방지)

> 상태: **폐기 2026-07-11 (과설계 판단, 코드 전량 revert)** — units 0~2 구현·리뷰·실플레이까지 갔으나, 실측 결과 **단일 레인 맵에서 추격이 순수 마칭 대비 얻는 게 0**(둘 다 레인 옆 방어유닛 사거리 진입 → FSM Engaging, 경로 밖 방어유닛은 둘 다 미도달). 사용자 결정으로 전량 버림. 이동 로직은 헌터 이전으로 완전 원복(MovementSystem/EnemyAiStateSystem diff 0).
>
> 배경: `docs/spec/nightmare-catcher/` (보스 콘텐츠 출처), 실플레이 피드백 2026-07-10~11.

## ⚠ 폐기 사유 (2026-07-11) — 되살릴 때 읽어라

**과설계였다.** 원래 "보스 누수" 문제에 대해 근본 원인(누수가 사거리 밖 배치 때문인지, engageMovement 때문인지)을 **확인하지 않고** 바로 "최근접 직선 추격"을 설계한 게 점프였다. 실측으로 드러난 것:

- 이 게임 맵은 **단일 walk 레인**(예: y=1), 방어유닛은 레인 옆 **`Place` 셀(=walkMask 0=벽)** 에 배치된다.
- 보스가 **그냥 flow 로 마칭**만 해도 레인을 타고 방어유닛 옆을 지난다 → 사거리(2타일) 진입 → **기존 FSM 이 Engaging → (engageMovement=Halt) 정지·공격**. 추격 코드 불필요.
- 추격+wall-slide 로 고친 동작도 결국 "레인 타고 접근 → 교전" 으로 **마칭과 동일 결과**. 경로 밖 방어유닛(벽 셀)은 추격이든 마칭이든 **둘 다 미도달**.
- 즉 추격은 **멀티 레인/개방형 맵에서 보스가 flow-경로를 이탈해 다른 레인 방어유닛을 사냥**해야 할 때만 값을 한다. 단일 레인에선 잉여 복잡도(직선추격→벽고착→wall-slide→softlock 가드로 이어진 땜빵 사슬).

**되살리는 조건**: 개방형/멀티 레인 맵이 도입되고, 보스가 flow-경로 밖 방어유닛을 능동 사냥해야 할 때. 그 경우 **wall-slide 같은 greedy 슬라이드가 아니라** 방어유닛의 최근접 walkable 이웃을 목표로 한 **target-directed field(multi-source BFS)** 가 옳은 해법(당시 wall-slide 는 flow-BFS 재사용이 구조적으로 막혀서 나온 근사였다 — Place 셀=non-walkable 이라 goal-BFS builder 가 early-return). 아래 units 0~3 의 계약/함정 기록을 참고하되, 이동은 직선추격이 아닌 필드 기반으로 재설계할 것.

**보존된 것(헌터와 무관)**: `enemy-walk-anim-speed` unit 4(이동=Walk/정지=Idle + 슬로모 수정)는 프레젠테이션이라 유지. 보스 `engageMovement=Halt`(마칭 중 방어유닛 만나면 정지·교전)도 유지 — 이게 단일 레인 맵의 "누수 방지" 실질 해법이다.

---

_아래는 폐기된 원래 스펙(역사 기록)._

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
