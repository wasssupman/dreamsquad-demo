# dreamcatcher-new-abilities

> 상태: 코드 완료 2026-07-13 (dotnet build 4어셈블리 0 error). 카드 에셋 authoring·테스트 실행·PlayMode 검증은 Unity 복구 대기. critic plan-review 반영본.

## 상위 목표

기존 sim 기반(트리거 디스패처 · StatModifier · CC · Stack/DoT) 위에 **저위험 신규 능력 3종**을 얹는다. 전부 `AttackSystem` RESOLVE seam 과 기존 이벤트 채널만 사용 — 코어 구조체 변경 없음. (킬 귀속·디펜더 HealthThreshold 인프라가 필요한 `last_stand`·`devouring_craving` 는 **Spec B `dreamcatcher-kill-and-threshold`** 로 분리.)

### 검증 질문

> "새 payload 를 append 하고 AttackSystem RESOLVE 에 분기 1개씩 추가하는 것만으로, 3종이 실제 전투에서 의도대로 발동하는가? 특히 **투사체 궁수**에서 frost×shatter 콤보가 보너스를 받는가?"

## 신규 능력 3종

### 🟦 전체 타겟 (Squad · axis=All)
- **`shatter_hymn` / 산산이 부수는 성가** — 모든 아군이 **CC(둔화·기절·수면) 걸린 적에게 +25% 피해**. `frost_arrow` 와 콤보.

### 🟨 개인 타겟 (Unit)
1. **`frost_arrow` / 서리의 화살** — `AttackN(3)` × `ApplyCcToTarget(cc=Slow, 40%, 2s)`
2. **`ember_bite` / 불씨 물기** — `AttackN(3)` × `ApplyStackToTarget(stack=Bleed, n)` (DoT)

## 재사용 seam (critic 검증 완료)

- **AttackSystem RESOLVE**: `bestTarget` + `EnemyCcEventsSingleton`(:114-118) + `StackModifierApplyEventsSingleton`(:81) + `DcTriggerSlot` RW. AttackN payload arm 은 현재 `ProjectileToTarget` 만 처리(:687, 그 외 warn) → 신규 arm 추가.
- **CC/Stack**: `EnemyCcEvents` / `StackModifierApplyEvents`(+`StackModifierTickSystem`·`DotApplySystem`) — Combat→Effects 채널. `StackKind` 에 `Bleed` 이미 존재(enum append 불필요).
- **Damage/Modifier**: `ModifierStats`(damageMul…) + `ModifierStatsAggregateSystem`. 데미지 write 6곳(critic): `AttackSystem.cs:331`(투사체 bake)·`:523`(직접 output) + `ProjectileHitSystem.cs:134/176/209/307`.

## 작업 단위

| # | 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | contract | `0_definition_layer.md` | append `DcPayloadKind.{ApplyCcToTarget,ApplyStackToTarget}` + **선택자 필드** `ccKind`/`stackKind`(DcPayloadSpec+DcTriggerSlot) + `StatKind.DamageVsCcMul` + `CardBuffKind.DamageVsCc` + `ModifierStats.damageVsCcMul`(base 1). 무동작 |
| 1 | feature | `1_on_hit_payloads.md` | frost_arrow·ember_bite: bake + AttackSystem RESOLVE arm 발동(EnemyCc·StackModifier 채널) |
| 2 | feature | `2_damage_vs_cc.md` | shatter_hymn: aggregate 6번째 stat(base 1) + `MapDcEffect` 매핑 + RESOLVE 타겟 CC 조건 배율 **(투사체 bake 경로 포함)** |
| 3 | assets | `3_card_assets.md` | DreamcatcherCard SO 3종 + 값 + 덱/카탈로그/시트 통합 + 테스트 |
| 4 | docs | `4_handoff_summary.md` | 인계 요약 |

## feature-wide 계약 (critic BLOCKER/HIGH 반영)

1. **append-only**: 신규 enum 케이스는 끝에 추가(기존 카드/스톤 에셋 int 안정). `StackKind` 는 손대지 않음(Bleed 존재).
2. **선택자 필드 (BLOCKER 해소)**: `DcPayloadSpec`(authoring)·`DcTriggerSlot`(baked) 양쪽에 `CcKind ccKind`(ApplyCcToTarget), `StackKind stackKind`(ApplyStackToTarget) 추가. 기존 "kind별 struct 분리 YAGNI·신규필드 0" 주석과 충돌함을 unit 0 에 적시(payload 다형성이 필드 다중화를 강제).
3. **맥락 경계**: CC/Stack 는 AttackSystem(Combat)이 **채널로만** enqueue. DamageVsCc 는 공격자 `ModifierStats`(Effects) RO + 타겟 `CcEffect` RO **읽기만** → RESOLVE 데미지 배율. Effects 쓰기 없음. 신규 채널 0.
4. **DamageVsCcMul base 1 (HIGH 해소)**: `ModifierStatsAggregateSystem` 이 6번째 stat 을 **1.0 로 초기화**(moveSpeedMul 선례, CombineMul). RESOLVE 적용부도 `HasComponent ? … : 1f` 부재-가드. 0-기본값이면 CC 대상 무적 = 절대 금지.
5. **DamageVsCc 적용 지점 (HIGH 해소)**: 투사체 궁수에서 콤보가 죽지 않도록, **RESOLVE 의 투사체 bake 경로**(`AttackSystem.cs:331`)에서 `bestTarget` 의 CC 상태(`ccActionLookup` 이미 hoist:78)로 배율 적용 + 직접 output(:523)에도 적용. 판정 시점 = 발사 시점 의도 대상. (homing 이 다른 적을 맞히는 타이밍 불일치는 허용 — LOW.)
6. **CC/스택 대상**: frost/ember 는 RESOLVE 시점 `bestTarget` 에 즉시 적용(발사 시점 의도 대상). homing 명중 대상 불일치 허용.
7. **새 플레이 오브젝트 0**: 3종 모두 기존 CC/스택/데미지 파이프. 신규 `ProjectileData` 불필요. 카드 아트만 신규(placeholder 허용).
8. **테스트**: 카드당 ≥1 assertion — 특히 **shatter 투사체 경로**(궁수+CC 대상 보너스)·frost CC 부여·ember 스택 부여. 기존 드림캐쳐 테스트 회귀 green. dotnet build 4어셈블리 0 error(Unity 미가동 시).

## 파이프라인 커버리지

**N/A** — 신규 플레이 오브젝트/렌더 정거장 없음. CC=기존 EnemyCc 연출, Stack=기존 DoT, Damage=기존 IncomingDamage. `object-pipeline-map` 대조 대상 아님.

## 후속 후보

- **Spec B `dreamcatcher-kill-and-threshold`**: last_stand(디펜더 HealthThreshold) + devouring_craving(OnKill 킬 귀속). 별도 draft.
- `ApplyCcToTarget` 변형(Impulse 넉백 / Stun) — payload 재사용, 데이터만.
- lifesteal/heal 계열 — 사용자 지시로 보류.
