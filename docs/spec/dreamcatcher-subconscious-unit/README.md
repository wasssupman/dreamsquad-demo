# dreamcatcher-subconscious-unit

> 상태: 구현 완료 2026-07-10 (compile 클린 · EditMode 15/15 · PlayMode 8/8; UI 육안 검증 후속)
>
> ⚠️ 후속: 느린 각성의 **메커니즘 자체가 재설계**된다(별도 신규 spec) — host 는 효과 미부여,
> host 생존 중 **신규 배치 유닛에만** 느린 각성 효과 부여. 본 spec 의 Unit/SelfWarmupBuff 는
> 그 토대(부착 모델·프레임)로 유지되되, 적용 대상 로직은 신규 spec 에서 교체된다.

## 목표

1. **느린 각성을 Unit(개별 부착) 카드로 전환** — 스쿼드 전체 버프에서, 부착한 유닛 1기가
   "2초 대기 → 공속 +50%(매치 영구)" 를 얻는 개별 메커니즘으로. Unit 경로가 stat 버프+warmup 을
   다루도록 새 payload `SelfWarmupBuff` 추가(사용자 확정 방식 A).
2. **무의식(Subconscious) 등급 전용 프레임** — 덱빌더 카드 그리드 + 상세 팝업에서 무의식 등급이
   타입색(Unit=금/Squad=청)과 구분되는 고유 프레임색을 갖는다(category 우선 > 타입색).

## 검증 질문

- 느린 각성을 유닛에 부착하면 그 유닛만 2초 멈췄다가 공속 +50% 가 되는가? (스쿼드 전체 아님)
- 덱빌더에서 무의식 카드가 다른 카드와 프레임색으로 구분되는가?

## 배경

- Unit 부착 경로 `ApplyDreamcatcherCardToUnit` 은 `mechanics[]`/`attackMods[]` 만 소비하고
  `effects[]`·`placementWarmupSec` 는 읽지 않는다(SO 주석: "ApplyDreamcatcherCard stays
  Axis-only"). 따라서 type/binding 만 플립하면 효과·워밍업이 전부 inert → 새 payload 필요.
- 가장 가까운 기존 것 = `SelfBuffLethal`(마지막 불꽃): `EnqueueAttackSpeedMul(1+mag/100, dur)`
  + LethalTimer(자폭). 여기서 자폭을 빼고 버프를 **영구(DcDuration=1e9)** 로, dur 을 **warmup 초**로.
- 프레임: `DreamcatcherDeckBuilderView` 가 현재 `card.type` 으로 프레임/아트폴백 색을 정하고
  `category` 는 dormant. 무의식 프레임을 위해 category 를 프레임 채색용으로만 재활성.

## 결정 (2026-07-10 사용자 확정)

1. **방식 A**: `mechanics[]` 에 새 `DcPayloadKind.SelfWarmupBuff` 추가. 다른 Unit 카드와 동일 모델.
   `effects[]` 는 Squad 전용으로 유지(계약 불변).
2. **프레임 범위**: 덱빌더 카드 그리드 + 상세 팝업 (인게임 손패는 범위 밖).

## 작업 단위

| # | 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | code | `0_self_warmup_payload.md` | `DcPayloadKind.SelfWarmupBuff`(append) + Unit-path 적용 |
| 1 | data | `1_slow_awakening_asset.md` | Card_SlowAwakening → Unit/mechanics 전환 + description 갱신 |
| 2 | code | `2_subconscious_frame.md` | 덱빌더 카드+팝업 무의식 프레임색(category 우선) |
| 3 | code | `3_retire_hostless_persist.md` | 레거시 hostless 영속 apply(`ApplyDreamcatcherCard`) 은퇴 → Hosted 이관 |

## Feature-wide 계약

1. **append-only**: `DcPayloadKind.SelfWarmupBuff` 는 enum 끝에 추가(기존 카드 payload int 보존).
2. **effects[] = Squad 전용 유지**: Unit 경로는 여전히 effects[]/placementWarmupSec 를 안 읽는다.
   느린 각성의 버프/워밍업은 mechanics[] payload 로 이관한다.
3. **SelfWarmupBuff 의미**: trigger=None(즉발). `magnitude`=공속 %(→ ×(1+mag/100), 만료 없음
   DcDuration=1e9), `duration`=warmup idle 초(`ApplyPlacementWarmup`, 0=무). 자폭 없음.
   **엔티티 단위 StatModifier** — 유닛 사망 시 함께 소멸(전역 `_activeDcEffects` 아님).
   awakening-hand §5 에 따라 부착 유닛 사망 시 카드 엔트리는 큐로 재순환.
4. **프레임 우선순위**: `category==Subconscious` → 무의식색, else 타입색(Unit 금 / Squad 청).
   category 는 프레임 채색으로만 재활성(덱 규칙/라벨은 여전히 무관).
5. **하드코딩 금지**: 50%/2초 는 카드 SO(mechanics payload)에서.

## 후속 후보 (범위 밖)

- 무의식 프레임을 인게임 손패(`DreamcatcherHandView.BindCard`)까지 확대.
- squad `placementWarmupSec` 인프라는 유지(다른 스쿼드 카드용). 현재 이 필드를 쓰는 에셋 0개가 됨.
- 무의식 프레임에 색 외 추가 연출(액센트 링/글로우).
