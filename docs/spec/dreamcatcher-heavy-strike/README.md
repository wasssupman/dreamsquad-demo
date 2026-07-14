# Dreamcatcher Heavy Strike — N회마다 강공(피해 ×2)

> 상태: **완료 2026-07-14** — units 0~3 구현·커밋(`b2dc9edb`·`ae0384ff`·`35e443cc`+에셋), Play/로그 검증(근접 exact ×2.00). 인계 `4_handoff_summary.md`. 카드명 `응축된 일격`. 컨셉: "5회 공격마다 피해 2배, 크리티컬 명칭 없이 강공 느낌". 남은 후속: 실아트 교체 + 특수 데미지 폰트 sibling 스펙.
> 방식 확정: **단일 강타(진짜 2배)**. 5번째 기본 공격의 데미지 자체를 ×2 → 데미지 숫자 하나가 크게 뜬다. 2연발 근사는 채택하지 않는다.
> 이 spec 은 신규 Unit 드림캐쳐 1종 + 이를 표현하기 위한 새 공격-출력 배율 payload 를 추가한다.

## 목표

- 부착된 유닛이 기본 공격을 `N`회(기본 5) 할 때마다, 그 회차의 공격 하나가 **피해 ×`M`(기본 2.0)** 인 강공이 된다.
- 강공은 그 공격의 **모든 피해 대상**(근접 다중타격 / 투사체 splash / bounce 포함)에 동일하게 적용된다 — "한 방을 통째로 크게".
- "크리티컬"이라는 이름/랜덤성은 없다. 결정론적 주기 강공이다.
- 신규 이동/CC/물리 효과, 신규 플레이 오브젝트, 신규 VFX 시스템은 만들지 않는다.

## 검증 질문

> 강공 카드를 부착한 유닛은 정확히 `N`번째 기본 공격마다(그 사이 공격은 평타로) 피해가 `M`배가 되며, 그 배율이 그 공격의 근접 다중타격·투사체 splash·bounce 피해 전부에 적용되고 Threat(어그로) 귀속도 동일 배율로 동기화되는가? 카드가 없는 유닛의 기존 공격/투사체/데미지·어그로는 무회귀인가?

## 카드 스펙 (가제)

| 필드 | 값 |
|---|---|
| asset | `Card_HeavyStrike.asset` |
| id | `heavy_strike` |
| displayName | `응축된 일격` |
| type / category | `Unit` / `Unique` |
| axis | `All` |
| effects | 빈 배열 |
| mechanics | `AttackN(period=5) × <신규 payload>` 1슬롯, `magnitude = 2.0` |
| attackMods | 빈 배열 |
| 전용 아트 | `dreamcatcher_card_23.png` (다음 번호, 없으면 placeholder) |
| 시트 행 | 없음 (catalog-only — content-2 / kill-and-threshold 선례) |
| 기본 덱 | 변경하지 않음 |

### authoring (예정)

```text
mechanics[0]
  trigger.kind   = AttackN
  trigger.period = 5            // 5회마다
  payload.kind   = <신규: 강공>  // unit 0 에서 enum 확정
  payload.magnitude = 2.0        // ×2 (데미지 배율)
```

카드 문안(가안): "다섯 번째 공격마다 짓누르는 강공 — 그 일격의 피해가 2배가 된다."

## Feature-wide 계약 (load-bearing)

1. **카운터는 기존 것을 재사용한다.** `AttackN` 트리거 + `DcTriggerSlot.counter` + `DcTrigger.Tick(ref counter, period)` 가 이미 매 N회째 발동을 제공한다. 카드별 복사본은 독립 `DcTriggerSlot`(독립 카운터)을 갖는다. 새 카운터/새 시스템/새 NativeQueue 없음.
2. **강공은 "그 공격의 출력 데미지 배율"이다 — 별도 이펙트 발사가 아니다.** 기존 AttackN payload(ProjectileToTarget/CC/Stack)는 *추가* 캐리어를 발사하지만, 강공 payload 는 **그 공격 자신의 피해를 ×M** 한다. `magnitude` = 배율(2.0 = ×2), attacker 의 일반 damageMul 과는 곱으로 합성.
3. **배율은 hit-site 에서 실제 victim 피해에 곱한다** — `끝을 보는 눈`의 배관(`priorityDamageMul`) 선례 계승. 저장된 base output 에 미리 곱하지 않는다(splash/bounce 과증폭 + Threat desync 방지). `ProjectileHitSystem` 이 victim 의 Damage 를 enqueue 할 때 곱하고, **`IncomingDamage` 와 `ThreatTable.TryCredit` 에 동일 배율**을 넣는다(HIGH — content-2 계약 4 동일).
4. **강공은 primary 한정이 아니라 그 공격의 전 victim 에 적용된다.** eye 는 primary victim 만 ×1.2 였지만, 강공은 근접 cleave·splash·bounce 를 포함한 그 attack 의 모든 Damage victim 을 ×M ("한 방 통째"). 캐리어 필드는 per-attack 스칼라 하나.
5. **발동 회차 = 그 공격 자신.** 5·10·15… 번째 공격이 강공(그 사이는 평타). 발동 판정은 `DcTrigger.Tick` 이 fire 하는 RESOLVE 시점과 정렬한다. off-by-one(다음 공격 arm) 방식은 쓰지 않는다.
6. **부착 자격.** Bridge 의 기존 per-kind validation 에 강공 payload 케이스를 추가. `magnitude > 0`, `period > 0` 요구(비양수면 bake skip + 경고, 기존 AttackN None-guard 동일). host 는 양수 Damage output 을 내는 defender 여야 함(eye 선례 — 힐러/output 없는 caster 거절).
7. **캐리어 전달.** `ProjectileSpawnRequest` → Bridge drain → `ProjectileState` 에 `heavyDamageMul` inert 필드(zero-init = 1배 = 비활성, 실제 `mul > 0 ? mul : 1`). 근접 공격은 melee 출력 경로에서 직접 곱. 기존 request 생산자 전수 수정 없이 모든 기존 투사체 inert.
8. **콘텐츠·UI (catalog-only, 시트 N/A).** SO 를 `DreamcatcherCardCatalog.asset` 에 등록하면 런타임 리프레셔가 자동 열거 → 덱빌더 COLLECTION·손패·부착아이콘 자동 노출. UI 코드 0, 기본 덱·씬 미변경. 시트 roundtrip 없음(값은 Unity-authored). Unit 카드는 자동 수치 렌더가 없으므로 `5회`·`2배`는 카드 문안과 실제 데이터로 함께 육안 검증.

## 작업 단위 목록

| # | 예정 문서 | 작업 | 핵심 완료 기준 |
|---|---|---|---|
| 0 | `0_definition_and_carrier.md` | 신규 `DcPayloadKind`(강공) append + `ProjectileSpawnRequest`/`ProjectileState` 에 `heavyDamageMul` inert 필드 + Bridge drain 전달 | 미사용 기본값 inert(=1배), compile green |
| 1 | `1_bake_and_trigger.md` | Bridge per-kind validation/bake(magnitude=배율, period 요구), AttackSystem: AttackN 강공 fire 시 그 공격 출력에 배율 마킹(melee + projectile 캐리어) | EditMode: 카운터 주기(5·10…만 강공), bake validation(비양수/비defender 거절), 무카드 무회귀 |
| 2 | `2_heavy_damage_apply.md` | `ProjectileHitSystem` victim finalDamage 1회 계산 → 전 victim ×M + `IncomingDamage`+`TryCredit` 동일 배율, melee 경로 동일 | melee cleave/ splash/ bounce 전부 ×M, Threat 미desync, 비강공 공격 ×1 |
| 3 | `3_card_asset.md` | 아트 23 import → SO 저작(AttackN×강공, magnitude 2.0) → catalog 등록 → Play e2e | COLLECTION/손패 노출, 5회째 강공 육안·로그 확인, art!=null |
| 4 | `4_handoff_summary.md` | 인계 | 범위·테스트·잔여 리스크 기록 |

> README 승인 후 `0_*` 부터 한 개씩 작성·승인·구현·검증·커밋.

## 파이프라인 커버리지

신규 플레이 오브젝트 없음 → 오브젝트 스폰→View/Pool 대조 **N/A**. 기존 기본 공격/투사체의 **피해 배율 파라미터만** 확장.

| 경계 | 이번 spec | 신규 시스템/큐 |
|---|---|---|
| 정의 SO | 기존 `mechanics[]` + `DcPayloadKind` kind 1개 | 없음 |
| Mono→ECS | 기존 `BattleBridge` bake + 투사체 drain 확장 | 없음 |
| Combat 상태 | 기존 `DcTriggerSlot`(카운터 재사용), 캐리어 스칼라 필드 | 시스템 없음 |
| 투사체 | Request/State inert 필드 1개 + Hit arm 배율 | 없음 |
| Presentation/UI | 기존 art/catalog/description 자동 소비 | 없음 |
| 씬 | 변경 없음 | 없음 |

## 범위 밖 / 후속 후보

- **특수 데미지 숫자 비주얼 (별도 sibling spec — 사용자 확정 방향)**: 드림캐쳐로 파생되는 데미지(강공·드림캐쳐 투사체·CC/스택 등)를 기존 magnitude 팔레트가 아니라 **사이버펑크풍 강렬 스타일**(UV 그라데이션/네온 글로우/아웃라인 — 단색 아님)의 데미지 폰트로 렌더. 공유 배관(`IncomingDamage`→`DamageNumberEvent`→`DamageApplicationSystem`)에 **source-kind 태그** 추가 + `DamageNumberStyle` 특수 팔레트 + 커스텀 TMP 머티리얼/셰이더(선례 `CardCrumple_UI.shader`) 저작. 강공은 이 spec 의 첫 소비자. **heavy-strike 완료 후 착수** (횡단 기능이라 heavy-strike 스코프에 넣지 않음 — 제약 9). 일반 데미지도 리스타일할지는 그 spec 에서 결정.
- 강공 전용 타격 VFX/SFX/화면 흔들림 등 연출 — 이번엔 데미지 배율만.
- Squad(축 전체) 주기 강공 — 카운터가 유닛별이라 Squad 스코프는 대규모 재설계. 범위 밖.
- 랜덤 확률 크리티컬 시스템(스탯 기반) — 결정론적 주기 강공과 다른 축. 범위 밖.
- 시트 미러링(강공 payload 컬럼) — `dreamcatcher-sheet-sync` 확장(후속).
- 배율/주기 밸런스 재조정, 기본 덱 자동 편입, 보유/해금 경제.

## 착수 리스크

- 최대 회귀면은 `AttackSystem` 의 기존 데미지 출력·투사체 spawn 경로 + `ProjectileHitSystem` 의 victim 피해/Threat 귀속. 카드 mod 분기 바깥은 건드리지 않고 무카드 통합 테스트를 먼저 고정한다.
- 배율을 저장 output 에 미리 곱하면 splash/bounce 과증폭 + Threat desync — content-2 가 겪은 함정. 반드시 hit-site 에서 victim 별로 곱하고 IncomingDamage+TryCredit 동시 적용.
- 현재 무관 dirty(ProBuilder 등)는 exact-path staging 으로 격리.
