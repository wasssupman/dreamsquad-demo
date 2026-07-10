# dreamcatcher-placement-aura

> 상태: 구현 완료 2026-07-10 (spec-review 반영 · compile 클린 · PlayMode PlacementAura 2/2 · 회귀 8/8 · EditMode 15/15)

## 목표

"느린 각성"을 **스폰-오라(placement aura)** 메커니즘으로 재설계한다:

- Unit 카드처럼 **host 유닛에 부착**한다. **host 자신은 효과를 받지 않는다.**
- host 가 살아있는 동안, **axis 매칭 신규 배치 유닛**에게 "느린 각성 효과"(2초 warmup + 공속 +50%)를
  **배치 시점에 부여**한다.
- **host 사망 → 그동안 부여한 효과 전원 회수 + 미래 부여 중단 + 카드 재순환.**

`dreamcatcher-subconscious-unit` 의 SelfWarmupBuff(부착 유닛 자신에 즉발)를 이 오라 모델로 교체한다.
프레임(무의식 보랏빛)·Unit 부착 타겟팅·description 필드는 그대로 재사용.

## 검증 질문

- host 부착 후 새로 배치한 axis-매칭 유닛이 2초 대기 후 공속 ×1.5 가 되는가?
- host 부착 전부터 있던 유닛과 host 자신은 효과가 없는가?
- host 사망 시 그 오라로 버프받은 유닛 전원이 원복되고, 이후 신규 배치는 버프 안 받는가?

## 결정 (2026-07-10 사용자 확정)

1. **host 사망 시 회수**: 그동안 부여한 전원 효과 revoke (grant-and-keep 아님).
2. **axis 매칭 신규 배치만** 부여 (card.axis 를 다시 의미있게 — 향후 스폰-오라 카드 확장 여지).
3. 효과값 = 기존 그대로(2초 warmup + 공속 +50%), 카드 SO 에서.

## 기존 인프라 재사용 (신규 시스템 최소)

라이브 machinery 가 이 메커니즘의 대부분을 이미 제공:

- **미래 배치 부여** = `ApplyActiveDcEffectsTo(entity)` (신규 defender 생성 시 호출, BattleBridge:3590)
  가 `_activeDcEffects`/`_activeWarmups` 를 순회해 axis-매칭 신규 유닛에 적용.
- **회수** = `RevokeDreamcatcherEffects(handle)` 가 stackId 로 전 유닛 buff 를 magnitude 1.0 중화 +
  레지스트리 제거(미래 부여 중단). host 사망 훅은 컨트롤러에 이미 존재(`OnDefenderDied` → handle>0 revoke).

**차이(신규)**: 일반 Squad hosted 는 "커밋 시 현재 유닛에도 즉시 적용"하지만, 오라는 **future-only**
(커밋 시 현재 유닛·host 미적용) + Unit 부착 타겟팅 + host-bound handle.

## 작업 단위

| # | 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | code | `0_placement_aura_payload.md` | `DcPayloadKind.PlacementAura`(append) 정의·계약 |
| 1 | code | `1_bridge_register_aura.md` | future-only 오라 등록(handle) + `ApplyDreamcatcherCardToUnit` 분기(handle 반환) |
| 2 | code | `2_controller_host_revoke.md` | `CommitUnit` handle 배선 → `_attachedTo` → host 사망 revoke(기존 경로 재사용) |
| 3 | data | `3_slow_awakening_asset.md` | Card_SlowAwakening: mechanics=[PlacementAura(mag50,dur2)] 로 재작성 |
| 4 | test | `4_playmode_verify.md` | 신규배치 부여 / host·기존유닛 미부여 / host 사망 회수 PlayMode |

## Feature-wide 계약

1. **append-only**: `DcPayloadKind.PlacementAura` enum 끝에 추가.
2. **future-only**: 오라는 커밋 시점 현재 유닛/​host 에 적용하지 않는다. 신규 배치(`ApplyActiveDcEffectsTo`)
   에서만 부여. 구현은 **신규 전용 등록 메서드**(레지스트리 add 만) — `ApplyDreamcatcherCardInternal`
   의 현재-유닛 루프를 타면 안 됨(H1).
3. **host-bound handle**: 오라는 revocable handle(≥1) 을 받아 `_attachedTo[entry]=(host,handle)` 에 저장.
   host 사망 → `RevokeDreamcatcherEffects(handle)` (전 유닛 중화 + 레지스트리 제거).
4. **axis 매칭**: 부여 대상 = `MatchesDcAxis(newUnit, card.axis)` 인 신규 배치 유닛만.
5. **효과값 = SO**: AS%(payload.magnitude), warmup 초(payload.duration). 하드코딩 금지.
6. **재사용 유지**: 프레임(무의식)·Unit 부착 타겟팅·description(subconscious-unit spec)은 변경 없음.
   느린 각성만 PlacementAura 로 교체. SelfWarmupBuff(5)는 **핸들러 없는 reserved enum** 으로 잔존
   (append-only; 어떤 카드도 사용 안 함 — H4).

## 파이프라인 커버리지

**N/A** — 신규 플레이 오브젝트(유닛/투사체/해저드/VFX) 없음. 기존 defender spawn 경로
(`ApplyActiveDcEffectsTo`)와 StatModifier/cooldown 재사용만. 생성→렌더 정거장 변경 없음.

## 크리틱 반영 (2026-07-10 spec review)

- **H4(확인됨)**: 직전 커밋 `fe4ba372` 의 `SelfWarmupBuff`(kind 5)는 **BattleBridge 핸들러가
  유실**돼 느린 각성이 현재 no-op(트리거 가드에서 스킵). 이 spec 이 PlacementAura 로 교체하며 수정.
  SelfWarmupBuff(5)는 **핸들러 없는 reserved 값으로 잔존**(append-only) — 정리는 후속.
- **enum**: PlacementAura 는 **kind 6 으로 append**(5=SelfWarmupBuff reserved 유지). Card_SlowAwakening
  payload kind 5→6.
- **H1/H2 계약은 unit 1/2 문서가 SoT**: future-only 등록은 **신규 전용 메서드**로(기존
  `ApplyDreamcatcherCardInternal` 재사용/수정 아님 — 현재 유닛 루프 오적용 방지). int 반환 규약도 unit 1/2.
- **M6(정정)**: warmup 은 배치 시 1회 cooldown. host 사망 <duration초 전 배치된 유닛은 잔여 idle 이
  남을 수 있음(수용 — 쿨다운 경과 시 자연 소멸). "이미 만료" 가정은 삭제.
- **H3(제한)**: revoke=magnitude 1.0 중화는 **mult>1 버프에만 검증됨**(기존 Squad unit 9 선례).
  느린 각성=+50%(1.5)라 안전. **<1 디버프 오라는 이 revoke 로 중화 보장 안 됨** → 후속에서 별도 검증 전 금지.
- **M5(계약)**: axis=All 이면 **다른 오라 host 도 신규 배치로서 부여 대상**(host 자신 오라만 제외).
  중첩(여러 host)은 stackId 독립 → 곱연산 중첩. 의도된 동작으로 수용.
- **M7(테스트)**: axis=All 은 "axis 게이팅"을 반증 불가 → unit 4 는 **특정 axis 테스트 오라**를 하나 더
  등록해 비매칭 유닛 제외를 증명.

## 후속 후보

- 다른 스폰-오라 카드(axis별 버프/디버프)로 일반화 — <1 디버프는 revoke 중화 검증 선행(H3).
- SelfWarmupBuff(kind 5) reserved 값 정리 여부 결정.
- 무의식 프레임 인게임 손패 확대(subconscious-unit 후속과 병합).
