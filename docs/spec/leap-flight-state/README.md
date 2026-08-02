# leap-flight-state — 도약 비행 중 행동 상태 정의

> 상태: **작성 2026-08-02 (구현 전)**. `ultimate-leap` spec 의 선행 의존.

## 목표

일반 보스 도약(SelfBlink)의 비행 0.83초 동안 **sim 보스가 착지 셀에서 공격·이동하는 것**을 막는다.
sim 은 발동 프레임에 이미 텔레포트를 끝내는데 뷰만 아치로 날아가므로, 공중에 떠 있는 그림과
착지 셀에서 싸우는 실체가 어긋난다 — 이 정합을 상태 하나로 잡는다.

## 검증 질문

> 보스가 도약 비행 중 공격하지 않고 이동하지 않으며, **피격은 그대로 가능한가**?
> 착지 후에는 즉시 정상 행동으로 복귀하고, 비행 취소(사망·teardown)에도 상태가 남지 않는가?

## 설계 원칙 (ultimate-leap 과 공유)

**저장하는 것은 사실(무엇을 하고 있나), 능력(무엇이 허용되나)은 소비 지점에서 파생한다.**
`CcActionLock` 선례 — 어디에도 "ActionLocked" 는 저장되지 않고, `CcEffect` 버퍼(사실)에서
`IsLocked`(술어)로 파생된다. 허구(도약/석화/잠수)는 프레젠테이션이 소유하고 sim 은 능력만 안다.

이 spec 의 사실은 `LeapFlight` 태그 하나다. 파생이 자명(태그 존재 = 공격·자기주도 이동 불가)해서
술어 함수를 만들지 않는다 — 쿼리 필터와 lookup 체크가 곧 파생이다(제약 10 의 과잉 추출 금지).

## 작업 단위

| 파일 | 작업 | 문서 | 목적 |
|---|---|---|---|
| 0 | 시뮬 | `0_leap_flight_component.md` | `LeapFlight` 태그(Combat) + AttackSystem·MovementSystem 게이트 |
| 1 | 브리지 | `1_bridge_lifecycle.md` | 비행 창에 붙이고 착지·취소에 뗀다 (`PendingDeployment` 선례) |
| 2 | 인계 | `2_handoff_summary.md` | 종료 시 작성 |

## Feature-wide 계약

1. **`LeapFlight` 는 Combat 소유 태그다.** 의미: "도약 비행 중 — 공격·자기주도 이동 불가, **피격 가능**".
   Combat 에 두는 이유: 행동 잠금은 Combat 의 축(`AttackState`·`EnemyAiState`)이고, MovementSystem 은
   Combat 컴포넌트를 이미 RO 로 읽는 선례가 있다(`AttackState`·`EnemyAiState`). 궁극기(후속 spec)에서
   Combat 시스템이 소유 맥락 내 쓰기로 같은 태그를 재사용한다.
2. **anti-계약: `DamageApplicationSystem`·타겟 후보 수집에 절대 넣지 않는다.** 바로 옆의
   `PendingDeployment` 는 피격까지 막지만 이건 아니다 — 넣는 순간 보스가 비행 중 무적이 된다.
   비대칭이 의도임을 코드 주석과 이 문서 양쪽에 남긴다.
3. **fail-open.** 태그 부재 = 전부 허용. 소비는 `WithNone<LeapFlight>`(쿼리) 또는
   `HasComponent`(lookup) — 붙이는 쪽이 누락돼도 유닛이 조용히 마비되는 일이 없다.
4. **쓰기 주체 2원화.** 일반 도약 = **브리지**(비행 창의 길이 0.83s 는 뷰 시계라 브리지만 안다.
   브리지는 ECS 창구라 경계 위반 아님). 궁극기 = Combat 시스템(sim 시계 소유). 같은 태그, 다른 시계 —
   창(window)의 소유가 다를 뿐 의미는 동일하다.
5. **구조 변경(add/remove) 수용.** 판당 보스 도약 ~2회 수준의 빈도라 태그 + 쿼리 필터가 플래그 값
   쓰기보다 낫다 — 소비 지점에 분기 0.
6. **외력은 살아 있다.** 잠그는 것은 자기주도 이동뿐 — impulse/tornado/portal 은 `CcActionLock` 의
   Sleep/Stun 과 같은 규약으로 유지(combat-action-lock 선례). 단 보스는 넉백 면역이라 실효는 낮다.
7. **이식성.** 신규 규칙 함수 0 — 이 spec 의 이식 대상은 "사실 + 소비 필터" 패턴과 이 계약 문서다.
   Mono 이식 시 태그는 bool 필드, 필터는 if 문이 된다(게임 의미는 계약 2·6 에 있다).

## 부수 효과

- **flight-lift-feel 후속 후보 "보스 착지점 드리프트" 해소** — 비행 중 이동이 잠기므로 뷰가 내려앉는
  `toWorld` 와 sim 위치가 어긋날 거리 자체가 0 이 된다.

## 파이프라인 커버리지

플레이 오브젝트 신설·생성→렌더 경로 변경 없음 — 기존 보스의 행동 게이트만 추가. **N/A**.

## 후속 후보 (범위 밖)

- **`PendingDeployment` 와의 개념 통합** — 그것도 "공격 ✗ + 피격 ✗" 조합의 특수형이다. 배치
  파이프라인 전체가 걸려 있어 별건. 세 번째 유사 메커니즘이 생기면 통합 검토.
- **능력 술어 집약(`UnitCapability`)** — 지금은 소비 지점 조합(쿼리 + lookup)이 관용구다. 사실이
  3개 이상 겹치는 유닛이 실제로 생기면 그때 집약.
