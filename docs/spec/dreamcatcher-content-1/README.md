# Dreamcatcher Content 1 — 신규 드림캐쳐 3종 (트리거·수명 어휘 확장)

> 상태: **작성 2026-07-09, 구현 대기**
>
> 배경: unit-trigger / attack-mod-bounce 로 세운 2계층·확장비용 패턴을 신규 콘텐츠 3장으로 실증. 3장 모두 순수 데이터가 아니라 **enum+arm+훅** 확장이 필요하지만 각 확장은 국소적. "확장 비용 지도"의 여러 축(신규 트리거 3종, 신규 페이로드 2종, 신규 즉발-타이머 부류)을 실제 카드로 검증.

## 목표

개별유닛 바인딩 드림캐쳐 3종 추가:

- **② 작별 선물** — 사망 시 주변 2타일에 폭발 데미지 100.
- **③ 마지막 불꽃** — 부착 즉시 5초간 공속 +90%, 종료 시 자폭(카미카제).
- **① 가시 갑옷** — 5회 피격 시 다음 공격 1회가 2연발.

구현 순서 = ② → ③ → ① (쌈→비쌈, 각 독립 검증). ③의 자폭이 ②를 트리거하는 콤보가 어휘 설계의 보너스 실증.

## 검증 질문

> 세 카드가 각자의 트리거(사망/타이머만료/피격5회)에서 의도한 효과(AOE폭발/자폭/더블파이어)를 내는가? 신규 트리거·페이로드가 **enum+arm+훅**만으로 붙고 기존 카드/투사체/전투는 **무회귀**인가? 크로스맥락 트리거(①: Units 카운트→Combat 소비)가 맥락 경계를 지키는가?

## 작업 단위

| # | 문서 | 카드 | 작업 |
|---|---|---|---|
| 0 | `0_vocabulary.md` | 공통 | 트리거 2(`OnDamagedN`/`OnDeath`) + 페이로드 3(`SelfTileAoe`/`NextAttackDoubleFire`/`SelfBuffLethal`) enum append + `DcPayloadSpec` 필드(`tileRange`/`duration`) + 컴포넌트 2종(`NextAttackDoubleFire`/`LethalTimer`) — 컴파일만 |
| 1 | `1_card_farewell.md` | ② | OnDeath seam 훅(`DrainDefenderDeathEvents`) + SelfTileAoe 스폰(TileAoe 투사체 재사용) + 카드 에셋 + 검증 |
| 2 | `2_card_lastflame.md` | ③ | on-bind StatModifier(공속+90% 5s) + `LethalTimer` tick 시스템(만료→DeadTag) + 카드 에셋 + 검증 |
| 3 | `3_card_thornmail.md` | ① | `OnDamagedN` 카운트(DamageApplicationSystem, Units) + `NextAttackDoubleFire` 부여 + AttackSystem 소비(더블파이어) + 카드 에셋 + 검증 |
| 4 | `4_handoff_summary.md` | — | 인계 |

## Feature-wide 계약 (load-bearing)

1. **2계층 유지**: enum/수치는 정의계층(`DcMechanic.cs`, ECS 무참조). 카드는 기존 `mechanics[]` 재사용 — ②①은 `DcMechanic{trigger,payload}`(트리거형), ③은 `trigger=None`+`payload=SelfBuffLethal`(즉발). 별 배열 신설 금지.
2. **트리거별 카운트/발동 맥락 = 그 이벤트를 소유한 맥락**. AttackN=Combat(기존), OnDamagedN=Units(DamageApplicationSystem), OnDeath=Bridge death seam. **같은 슬롯을 두 맥락이 쓰지 않는다**(트리거 kind 별로 쓰는 맥락이 하나) → 경계 유지.
3. **크로스맥락 핸드오프(①)**: Units 가 트리거 감지 후 Combat 효과가 필요하면 **컴포넌트로 넘긴다**(`NextAttackDoubleFire` Units write / Combat read+clear). 신규 NativeQueue 채널 없이. 트리거 소스가 Combat 밖인 첫 사례의 seam.
4. **페이로드는 기존 프리미티브 재사용**: SelfTileAoe = 기존 `PayloadKind.TileAoe` 투사체 스폰(사망 셀 impact 락, damage 100, impactTileRange 2). NextAttackDoubleFire = AttackSystem RESOLVE 가 output/투사체를 2회 발행. 공속 버프 = 기존 `StatKind.AttackSpeedMul` StatModifier(duration).
5. **신규 컴포넌트 소유**: `NextAttackDoubleFire`(Combat, charge), `LethalTimer`(Effects). DeadTag 부여는 기존 사망 경로 재사용(신규 death 채널 금지).
6. **바인딩/부착은 기존 API**: `ApplyDreamcatcherCardToUnit` 확장(가드 재사용). ②①은 mechanics 트리거 슬롯, ③은 부착 시점에 즉시 StatModifier+LethalTimer 부여(슬롯 카운트 없음).
7. **무회귀**: 신규 enum 케이스는 기존 switch 의 새 arm — default/미지원은 기존대로. 신규 컴포넌트 없는 유닛은 무영향.
8. **직렬화 append-only**: enum 케이스는 끝에 추가(기존 카드 에셋 값 보존).

## 파이프라인 커버리지 (투사체·이벤트)

| 정거장 | 이번 spec | 비고 |
|---|---|---|
| 데이터 SO | `DreamcatcherCard.mechanics[]` (기존) | 신규 SO 타입 없음 |
| 사망 이벤트 | 기존 `DefenderDeathEventsSingleton`(cell) — bridge 드레인에서 OnDeath 슬롯 검사 | 신규 채널 0 |
| AOE 스폰 | 기존 `ProjectileSpawnRequest{TileAoe}` → drain → ImpactSystem | ②: 사망 셀 impact 락 |
| 피격 카운트 | 기존 `DamageApplicationSystem`(Units) IncomingDamage drain 지점 | ①: defender 피격 시 슬롯 tick |
| 공속 버프 | 기존 `StatModifierApplyEventsSingleton` | ③ |
| 자폭 | 기존 DeadTag 사망 경로 | ③: LethalTimer 만료 |

## 후속 후보

- **개별유닛 바인딩·회수 UX** (여전히 미구현 — 카드가 API 로만 부착). 3장 다 이 seam 위에 얹힘.
- OnDamagedN 의 "피격" 정의 세분화(현재 프레임당 IncomingDamage>0 = 1카운트; DoT 틱 개별 카운트는 후속).
- NextAttackDoubleFire 를 charges>1 / 지속시간형으로 일반화.
- SelfTileAoe 에 non-Damage(슬로우 폭발 등) — 현재 Damage-only.
- LethalTimer 를 "만료 시 임의 효과"로 일반화(자폭 외).
