# aggro-standoff — aggro 사거리 정지 (이동 종료 조건)

> 상태: 완료 2026-06-29 (units 0~1, Play 검증 PASS). `enemy-tile-movement-integrity` 후속(거기 후속 후보에서 승격).

## 배경 / 문제

aggro 이동(`MovementSystem` aggro 분기)이 guardian **중심까지**(stackThreshold 0.05) 밀어붙여 cell-trim 이 walk 셀 가장자리에서 막음 → 스프라이트가 디펜더 위로 겹쳐 보임. 한편 공격은 `AttackSystem` 이 `dist ≤ AttackState.range` 에서 이미 발사 → **이동이 사거리를 지나쳐 계속 미는 불일치.**

## 검증 질문

aggro 적이 **공격범위에 들어온 순간 이동을 멈추고**(도달 완료) 그 자리에서 공격하는가? 엣지 겹침 없이?

## 통합 모델

"적은 target 으로 이동, target 도달 조건 충족 시 정지."
- goal target → 도달 = goal 셀 진입.
- **aggro target → 도달 = 공격범위 안** (`tileDist ≤ RangeToTiles(range)`, AttackSystem 발사와 동일 tile-Chebyshev metric — unit 1) → 이동 종료.

## 공통 원칙 / 결정 (2026-06-29 확정)

- **standoff 거리 = `AttackState.range`** (쏘는 거리와 동일). 별도 파라미터·min-clamp **없음**.
- range 출처: 일반 적 native `AttackState`, outputs 없는 적은 `TauntAttackGrantSystem` 이 `AggroAttackProfile`→`AttackState` 부여.
- `MovementSystem` 이 `AttackState`(Combat) **RO 읽기**(맥락 간 읽기 허용). 부여 range 가 같은 프레임 보이도록 `TauntAttackGrantSystem` 을 `UpdateBefore(MovementSystem)`.
- range 없으면 0 → 기존처럼 경계까지(폴백).

## 작업 단위

| # | 작업 | 문서 | 목적 |
|---|---|---|---|
| 0 | standoff | `0_standoff.md` | MovementSystem 도달조건=range + TauntGrant 순서. Play 검증 |
| 1 | metric 통일 (M1) | `1_metric_unify.md` | 정지 판정을 AttackSystem 발사와 동일한 tile-Chebyshev 로(soft stall 제거). EditMode 26 |

## 데이터 주의 (로직 아님)

aggro 공격 사거리 = **1 고정**(현 계획), taunt acquisition(`AggroProvider.range`) ≥2 는 **별개**(끌어오는 거리). range=1 + 직교 인접이면 적이 **인접 walk 셀 중심(dist 1.0)** 에 정지 → 겹침 0. **드문 대각-전용 배치**(직교 walk 이웃 없음)면 코너에서 사거리 듦(약간 겹침) — 로직 아닌 배치 케이스.

## 후속 후보

- **aggro 정식 경로탐색** [M] · greedy+cell-trim 근사 대체(guardian 벽 뒤). `enemy-tile-movement-integrity` 에서 이관.
- **aggro 공격 사거리 통일** [S] · 현재 standoff 는 `AttackState.range`(native, 측정 4~8) 사용 → 원거리 적은 2~3타일 떨어져 정지. aggro 시 고정 range(예 1)로 모으려면 `AggroAttackProfile`/override 데이터 작업. 디자인 결정(standoff 로직과 별개).
- ~~**standoff/발사 metric 통일** [S] (M1)~~ → **완료 (unit 1)**: 정지 판정을 tile-Chebyshev `≤RangeToTiles(range)`(AttackSystem 동일)로 통일 → 정지⟺발사가능 일관, soft stall 소멸.
- ~~**aggro/코너 PlayMode smoke** [S] (M2)~~ → **완료**: `Tests/PlayMode/MovementIntegritySmokeTest.cs` — 실 전투 동안 active 적 전원 walk 타일 유지(offWalk 0, aggro chase + flow recenter cell-trim 가드) + 더미 guardian aggro 데미지(standoff 도달 + RESOLVE). enemy-tile-movement/aggro-standoff/attack-hit-delay 합성 회귀 가드. PASS(7.6s).
