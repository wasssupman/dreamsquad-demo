# 0. JSON 스키마 계약 — 탭 6종 (docs only)

## 목적

시트 탭/컬럼 스키마와 배열 싱크 시맨틱을 확정한다. 구현 없음 — unit 2~3 의 계약 원본.

## 공통 컨벤션 (unit-stat-spreadsheet-schema 승계)

- 응답: `GET {base}/{탭명}` → `{success, data:[행], errorDetail}`. 빈 문자열 셀 = 키 생략 = 기존값 유지.
- enum = C# 멤버명 문자열(case-insensitive). 미지 멤버명은 해당 행 바인딩 실패.
- `_` 접두 컬럼 = 계약 밖 (export 가 정보용으로 기록, import 는 무시).
- 매칭 실패(미지 id, 범위 밖 slot)는 스킵 + 로그 리포트. 절대 추측하지 않는다.
- **미인식 헤더 리포트** (review 3b): DTO 에 없는 컬럼(`_` 제외)은 탭별로 로그에 명시된다 — 컬럼 rename/삭제가 "빈 셀=유지" 에 삼켜져 무음 실패하는 것을 방지.

## 탭 스키마

### `DcCards` — 카드 본체 (행 = DreamcatcherCard 1장, 키 = `id`)

| 컬럼 | 타입 | 비고 |
|---|---|---|
| id | string | 매칭키 |
| displayName / description | string | 기획 텍스트 |
| type | enum CardType | Squad/Unit/Active |
| binding | enum CardBinding | Axis/Unit — type 과 정합 검증(경고) |
| axis | enum CardTargetAxis | ClassRanger/ClassGuardian/Cost1/All |
| placementWarmupSec | float | Squad 워밍업 |
| _skillId | (정보) | Active 카드가 감싼 SkillData.id |

### `DcCardEffects` — 스탯 버프 (행 = effects[] 1건, 키 = `cardId`+`slot`)

| 컬럼 | 타입 |
|---|---|
| cardId / slot | string / int (배열 index) |
| kind | enum CardBuffKind |
| percent | float (+10 = +10%) |

### `DcMechanics` — 트리거 메커니즘 (행 = mechanics[] 1건, 키 = `cardId`+`slot`)

| 컬럼 | 타입 | 원본 필드 |
|---|---|---|
| cardId / slot | string / int | |
| triggerKind | enum DcTriggerKind | trigger.kind |
| triggerPeriod | int | trigger.period (AttackN N타) |
| triggerPeriodSeconds | float | trigger.periodSeconds (PeriodicTimer 주기 초) · unit 7 |
| triggerFraction | float | trigger.fraction (HealthThreshold 경계비율) · unit 7 |
| payloadKind | enum DcPayloadKind | payload.kind |
| magnitude / tileRange / duration | float / int / float | payload.* |
| ccKind | enum DcCcKind | payload.ccKind (ApplyCcToTarget) · unit 7 |
| stackKind | enum DcStackKind | payload.stackKind (ApplyStackToTarget) · unit 7 |
| buffStat | enum CardBuffKind | payload.buffStat (SelfStatBuff 대상 스탯) · unit 7 |
| _projectileId | (정보) | payload.projectile → ProjectileData.id |

### `DcAttackMods` — 상시 공격 변조 (행 = attackMods[] 1건, 키 = `cardId`+`slot`)

| 컬럼 | 타입 |
|---|---|
| cardId / slot | string / int |
| kind | enum DcAttackModKind |
| count / tileRange | int |
| damageMul | float |

### `DcSkills` — Active 카드가 감싼 SkillData (행 = SkillData 1개, 키 = `id`)

| 컬럼 | 타입 | 비고 |
|---|---|---|
| id / displayName / description | string | |
| range / magnitude / durationSec / cooldownSec / warningSec | float | 밸런스 스칼라 |
| cost | int | CostRuntime 스킬바 비용 (awakening 비용은 DcConfig) |
| _effect | (정보) | 구조 enum — 시트에서 변경 금지. (`_target` 열은 대상축 폐기로 제거 — active-dreamcatcher-tile-aim unit 0) |

### `DcConfig` — 싱글턴 설정 union 탭 (행 = config SO 1개, 키 = `id`)

| id 행 | 컬럼 |
|---|---|
| `awakening_default` | gaugeMax, gaugeStart, costSquad, costUnit, costActive, handSize, maxAttachPerUnit, slomoTimeScale |
| `deck_rule_default` | deckSize, maxSquad, maxUnit |

행마다 자기 컬럼만 채우고 나머지는 빈 셀(= 유지). union 이라 컬럼 합집합이어도 부분 갱신 계약으로 안전.

## 배열 싱크 시맨틱 (핵심 결정, 2026-07-11 확장성 검증 반영)

탭을 두 등급으로 나눈다 — 에셋 참조 유무가 기준.

**시트 SoT 탭** (`DcCardEffects`, `DcAttackMods` — 순수 스칼라, 에셋 참조 없음):
- 탭에 `cardId` 가 **등장하면** 그 카드의 배열을 해당 행들(slot 오름차순)로 **전체 재구성** — 행 추가/삭제/순서 변경이 곧 효과 추가/삭제/재배열. 효과가 계속 늘어나는 운영 전제.
- 탭에 미등장한 카드의 배열은 유지 (실수 삭제 방어). 배열 길이가 변하면 명시 리포트 (`[cardId] effects 2→1`).
- 탭 fetch 실패 시 해당 탭 전체 미적용 (기존 탭별 독립 실패 규칙).
- `slot` 은 정렬 키. 같은 `(cardId, slot)` 중복 → 그 카드 전체 스킵+리포트. 음수/빈 slot 도 그 카드 전체 스킵+리포트 (review H1).
- **재배열 규칙** (review M1): 빈 셀은 "그 **slot 번호**의 기존 항목" 값을 상속한다 (위치가 아님). slot 번호를 바꿔 재배열할 때는 셀을 전부 채워라 — 빈 셀 상속과 재배열을 섞으면 값이 slot 라벨을 따라 이동한다.

**Unity SoT 탭** (`DcMechanics` — `projectile` 에셋 참조 포함):
- 기존 배열의 `slot`(index) 항목에 값만 덮어쓴다. slot ≥ 배열 길이 → 스킵+리포트.
- 항목 신설/삭제/에셋 참조 변경은 Unity 에서 하고 Export 로 시트를 재시드한다.
- payloadKind 가 ProjectileToTarget 인데 SO 의 projectile 이 비어 있으면 경고 리포트 (적용은 수행).

## payloadKind → 사용 컬럼 매트릭스 (기획 입력 가이드)

| payloadKind | magnitude | tileRange | duration | 그 외 컬럼 |
|---|---|---|---|---|
| ProjectileToTarget | 투사체 flat 데미지 | — | — | projectile 필수 (Unity 관리) |
| SelfTileAoe | 폭발 데미지 | AOE 반경(타일) | — | — |
| NextAttackDoubleFire | — | — | — | — |
| SelfBuffLethal | 공속 +% | — | 지속/자폭 초 | — |
| PlacementAura | 공속 +% (매치영구) | — | warmup idle 초 | — |
| ApplyCcToTarget | (Impulse 넉백 세기) | — | CC 지속 초 | **ccKind** (Stun/Impulse) |
| ApplyStackToTarget | 부여 스택 수 | (효과 반경) | 스택 지속 초 | **stackKind** (Fire/Ice/Bleed/Poison) |
| SelfStatBuff | 버프 **퍼센트**(30 = +30%) | — | 지속 초(≤0=매치영구) | **buffStat** (AttackDamage/AttackSpeed…) |

trigger 스칼라(종류별 배타적): AttackN/OnDamagedN → `triggerPeriod`(N타). PeriodicTimer → `triggerPeriodSeconds`(주기 초, ≤0 inert). HealthThreshold → `triggerFraction`(경계비율, "임계 이하 1회" = 1−임계). OnKill/None/OnDeath 는 트리거 스칼라 0.

## 완료 기준

- [ ] 본 문서 검토 승인 → unit 1 시드 JSON 이 이 스키마와 일치
- [ ] 컬럼명이 SO 필드명(중첩은 접두 평탄화 규칙)과 1:1 대응함을 unit 2 DTO 가 그대로 구현
