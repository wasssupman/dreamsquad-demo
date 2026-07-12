# Nightmare Whip Aura — 보스 "채찍질" 아군 이속 오라

> 상태: **완료 2026-07-12** (units 0~3 구현·커밋·검증. 인계: `4_handoff_summary.md`)
> 설계 배경: `docs/plans/2026-07-11-nightmare-whip-aura-design.md`
> 선행: `docs/spec/nightmare-catcher/`(트리거 프레임워크·보스 콘텐츠), `docs/spec/modifier-framework-and-healer/`(modifier 채널), `docs/spec/dreamcatcher-placement-aura/`(오라 선례)

## 목표

nightmare-catcher 첫 지시 4종 스킬 중 마지막 미구현 항목 — **"채찍질": 자신 기준 3타일 이내의 아군(같은 진영) 유닛들의 이동속도 20% 증가** — 를 드림캐쳐 `trigger × payload` 프레임워크에 편입한다.

채찍질 = `PeriodicTimer(1s)` × **`AllyMoveSpeedAura`(신규 payload append=9)** (tileRange=3, magnitude=20, duration=1.5s)

매 펄스마다 host 기준 Chebyshev tileRange 내 같은 진영 유닛(host 제외)에게 `MoveSpeedMul ×(1+magnitude/100)` 모디파이어(TTL=duration)를 enqueue 한다. duration > period 라 범위 내에선 merge-refresh 로 유지, 이탈/보스 사망 시 ≤duration 내 자연 만료.

## 검증 질문

> 보스 주변 3타일 내 아군 적 유닛들이 이동속도 +20% 를 받는가? 범위 밖 유닛은 받지 않고, 범위를 벗어나면 ≤1.5초 내 원복되는가? 이 오라가 융단폭격·텔레포트·기본공격과 **직교**로 굴러가는가? 기존 슬로우(존/CC)와 Πmul 로 자연 합성되며 방어유닛 경로는 **무회귀**인가?

## 작업 단위

| # | 문서 | 작업 | 목적 |
|---|---|---|---|
| 0 | `0_payload_contract.md` | 계약 | `DcPayloadKind.AllyMoveSpeedAura` append + 펄스 타겟 순수함수(EditMode). 동작 무변경 |
| 1 | `1_aura_pulse_arm.md` | 로직/배선 | `BossPeriodicTriggerSystem` 페이로드 분기 + `BakeNightmareMechanics` 베이크(같은 커밋) |
| 2 | `2_boss_authoring_play.md` | 검증 | `Enemy_Boss_Nightmare` mechanics 추가 + 테스트 웨이브 미니언 동행 + Play e2e |
| 3 | `3_whip_pulse_visual.md` | 연출 (rev 1 스코프 추가) | 펄스 hit-VFX — `Projectile_WhipPulse`(Cylinder04) + arm enqueue, blink 퍼프 선례 |

## Feature-wide 계약 (load-bearing)

1. **신규 표면 최소**: 신규 payload enum 1개(append=9) + arm 분기 1개 + 순수함수 1개. 신규 트리거/시스템/채널/SO 타입/슬롯 필드 **0**. `DcTriggerSlot` 은 기존 필드(`periodSeconds`/`elapsed`/`magnitude`/`tileRange`/`duration`) 재사용.
2. **정의 계층은 진영을 모른다** (nightmare-catcher 계약 2 상속): 페이로드 의미 = "host 와 **같은 진영** 유닛 버프". arm 이 host 태그(AttackUnitTag/DefenderUnitTag)로 풀 선택 — 태생적 진영 중립(슬롯 존재 게이트, 진영 게이트 없음).
3. **host 자신 제외** — entity 비교(셀 비교 아님: host 와 같은 셀의 아군은 대상).
4. **modifier 프레임워크 통과, 새 데미지/이속 경로 0**: enqueue = `StatModifierApplyEventsSingleton`(기존 Combat→Effects 채널, producer-agnostic). `op=Mul`, `magnitude=1+slot.magnitude/100`, `source=host`, `stackId=0`. 유지 = merge-refresh(`remaining=max(old,new)`), 만료 = `StatModifierTickSystem`, 합성 = `ModifierStatsAggregateSystem` Πmul(floor/ceil 클램프 포함), 소비 = `MovementSystem`. **이 spec 은 modifier 계층 코드를 1줄도 수정하지 않는다.**
5. **자연 만료가 곧 해제 정책**: 범위 이탈·보스 사망 시 revoke 없음 — TTL(duration) 만료로 원복. duration(1.5s) > period(1s) 가 authoring 계약(위반 시 범위 내 점멸). 다중 whip 소스는 source 별 슬롯이라 곱연산(1.2×1.2) — 정의된 동작.
6. **degenerate 가드** (nightmare-catcher 계약 9 상속): `periodSeconds<=0` 은 기존 `PeriodicTick` 가드가 이미 차단. `magnitude==0`(mul 1.0) 또는 `duration<=0` 슬롯은 arm 이 enqueue skip(무의미 이벤트 스팸 방지). 음수 magnitude(아군 슬로우)는 허용 — aggregator floor 가 클램프.
7. **결정론**: 펄스 대상 선택은 전수(범위 내 전원) — 선택 분산이 없어 RNG/round-robin 불요. 순수함수 `SelectTargets`(타겟팅, sim-critical)만 EditMode.
8. **직교성** (nightmare-catcher 계약 4 상속): 채찍질은 독립 슬롯 accumulator 로 틱, `AttackState`/`AiState`/폭격·텔레포트 슬롯 무접촉. FSM 무변경.
9. **슬로모 일관**: 펄스 accumulator 와 modifier `remaining` 이 모두 Battle 도메인 dt 로 감속 — 손패 열림 0.3x 에서도 refresh 여유(duration−period) 비율 불변. 별도 처리 금지.

## 파이프라인 커버리지

**N/A** — 플레이 오브젝트 신설·생성→렌더 경로 변경 없음(`docs/reference/object-pipeline-map.md` 대조 불요). 신규 채널 0(기존 `StatModifierApplyEventsSingleton` 재사용), 신규 SO 타입 0, teardown 신규 0(모디파이어는 대상 유닛 버퍼에 실려 유닛 teardown 에 동반, 큐는 기존 lifecycle).

## 후속 후보 (스코프 밖)

- ~~채찍질 VFX/연출~~ → **unit 3 로 편입 구현** (rev 1, 사용자 요청 2026-07-12). 잔여: 전용 채찍 스윙 저작·버프 링 등 고도화는 보스 전용 연출 백로그(nightmare-catcher 후속)와 합류.
- **defender-side 오라 카드** — 계약 2 로 이미 중립이라 카드 데이터만으로 성립. 카드 taxonomy/밸런스는 별도 product 결정.
- **버프 아이콘/상태 표시** — 미니언 머리 위 버프 표시. `unit-status-fx` 계열 후속.
- **기본공격 100 원거리화** — nightmare-catcher 후속 후보 잔여분(이 spec 범위 아님).
