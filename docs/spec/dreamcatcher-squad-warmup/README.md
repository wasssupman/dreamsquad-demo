# Dreamcatcher Squad Warmup — 배치 워밍업 스쿼드 카드 1종

> 상태: **완료 2026-07-09** — unit 0 landed(`81733a78`, Play 실증 cooldown 2s→0 · attackSpeedMul 1.5). 배치 워밍업 인프라(`placementWarmupSec` · `_activeWarmups` · `ApplyPlacementWarmup` · `BeginPlacement` clear) 구축.
>
> **계보 주의**: 이 spec 이 만든 "느린 각성" 카드의 *형태*는 이후 재설계됨 — `dreamcatcher-subconscious-unit`(Unit 부착) → `dreamcatcher-placement-aura`(스폰 오라, kind 6). 그러나 여기서 만든 **워밍업 인프라는 그대로 살아있고 placement-aura 가 재사용**(`RegisterPlacementAura` → `_activeWarmups.Add`). spec 작업 자체는 완료, 카드 최종 형태만 후속에서 진화.

## 목표

무의식 카테고리 · 스쿼드 타입 카드 1종 추가(가칭 "느린 각성"):
**배치 시 2초간 대기(공격 X), 이후 공격속도 +50%** (매치 영구).

## 설계 (단순)

기존 인프라 재사용, 신규 시스템 0:

- **2초 대기** = 배치 시 유닛 `AttackState.cooldownRemaining = 2`(이미 `deployDelaySec` 가 쓰는 배치 idle 메커니즘).
- **공속 +50%** = 기존 `CardEffect{AttackSpeed, 50}` → StatModifier(매치 영구).
- **트릭**: +50% 를 즉시 적용해도 2초 동안 공격 불가(대기)라, 관측상 "2초 후 +50%" 와 **완전 동일**. 별도 지연 타이머 불필요.

카드 필드 `placementWarmupSec`(스쿼드 카드용, 배치 시 cooldown 세팅). 축(axis)=All(전체 스쿼드).

## 작업 단위

| # | 문서 | 작업 |
|---|---|---|
| 0 | `0_warmup_card.md` | CardCategory 에 `Subconscious` + `placementWarmupSec` 필드 + BattleBridge 스쿼드 워밍업 적용(현재/미래 유닛) + 카드 에셋 + 검증 |

## 계약

1. **스쿼드 경로 재사용**: `ApplyDreamcatcherCard`(현재 매칭 유닛) + `ApplyActiveDcEffectsTo`(미래 배치 유닛)에 워밍업 적용. `_activeWarmups`(axis, sec) 레지스트리로 미래 유닛 상속(_activeDcEffects 패턴). `BeginPlacement` 에서 clear.
2. **cooldown = max(현재, warmupSec)** — 기존 deployDelaySec 을 단축하지 않음.
3. **하드코딩 금지**: 2초/50% 는 카드 SO(`placementWarmupSec`, `CardEffect.percent`)에서.
4. **category=Subconscious** append(기존 Normal/Unique 뒤). category 는 여전히 라벨 dormant(덱 규칙 무관).

## 후속 후보

- **각성 중 전용 VFX**(선택 폴리시) — "대기상태"는 이미 idle 애니로 표현됨(SpineUnitView 가 비공격 시 idle 루프). 별도 연출 불필요. 단, "곧 빨라진다"를 알리는 충전/zzz VFX 를 원하면 그때만 추가.
- 무의식 카테고리를 deck-builder 라벨/필터로 노출.
