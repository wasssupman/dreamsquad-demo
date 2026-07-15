# 설계 검토 — 효과 트리거 통합 (드림캐쳐 ↔ 기믹)

**상태**: 📌 파킹(논의 기록). **당장 작업 안 함** — 다음에 이 방향을 착수할 때 참고하는 문서. 방향 합의 = "트리거 엔진 추출 + 도메인 태그" (2026-07-15 사용자 결정), 단 실착수는 보류.
**질문**: 지금 드림캐쳐 맥락에 갇힌 "트리거→효과" 기계를 도메인 중립 엔진으로 올리고, 기믹·드림캐쳐를 **구분 가능한 소비처(domain)** 로 둘 수 있는가?

## 한 줄 결론

**가능하고, 8할은 이미 되어 있다.** 드림캐쳐는 이미 *데이터주도 `trigger→payload` 중립 계약*(`DcMechanic`, "architecture-agnostic, ECS-free")을 갖고 있다. 통합의 본질은 새 프레임 발명이 아니라 **① `Dc*` 네이밍/위치 중립화 ② `domain` 태그 추가 ③ 기믹이 필요로 하는 트리거 종류(co-location·지연) 확장 ④ 기믹 bespoke 시스템 3개를 그 위 rule 로 이관**이다. 단, 트리거 *감지*가 5개 시스템에 분산돼 있고 payload *디스패치*가 큰 switch 라 "한 시스템으로 합치는" 통합은 아니다 — **공용 계약 + 분산 감지자** 형태가 유지된다.

## 현재 구조 실측 (3계층)

| 계층 | 내용 | 중립성 |
|---|---|---|
| **A. 효과 적용 배관** | `StatModifierApplyEvent → ModifierApplySystem → StatModifierSlot → ModifierStatsAggregateSystem → ModifierStats`; `IncomingDamage`; `CcEffect`; `StackModifier` | **이미 완전 중립** (드림캐쳐·기믹·스킬·시너지 공용). `ModifierOrigin` 로 출처 태그 이미 존재 |
| **B. 트리거→효과 정의/발동** | 정의: `DcMechanic{DcTriggerSpec, DcPayloadSpec}` (순수 데이터, ECS-free). 런타임 slot: `DcTriggerSlot`(Combat). 트리거종류 `DcTriggerKind{AttackN, OnDamagedN, OnDeath, PeriodicTimer, HealthThreshold, OnKill}`, payload `DcPayloadKind{... SelfStatBuff, ApplyCc/StackToTarget, ProjectileToTarget, AreaBarrage, SelfBlink ...}` | **반쯤 중립** — 계약은 도메인 무관인데 `Dc` 명칭·`Data/Dreamcatcher/` 위치·활성화(카드 로드아웃)에 묶임 |
| **C. 도메인 bespoke** | 드림캐쳐: `_activeDcEffects`+`ApplyActiveDcEffectsTo`(배치 상속), 드림스톤, empower 오라, `AwakeningReward`. 기믹: `OverworkGimmickConfig`, `FatigueAccrualSystem`, `PickupSpawn/ConsumeSystem`, `LastRunSystem`, 시즌 SO 게이팅 | **도메인 고유** (통합 대상 아님 또는 부분) |

**트리거 감지는 분산돼 있다** (각 조건이 발생하는 곳에서 감지, `slot.trigger==X` 필터):
- `AttackN` → `AttackSystem` · `OnKill`/`OnDamagedN` → `DamageApplicationSystem` · `PeriodicTimer` → `BossPeriodicTriggerSystem` · `HealthThreshold` → `HealthThresholdSystem` · `OnDeath` → `UnitLifecycleSystem`.
→ 통합해도 이 분산은 유지된다(이벤트가 원래 그 시스템에서 난다). 공용화되는 건 **slot 계약 + 정의 데이터 + payload 디스패치 규약**이지 "단일 트리거 시스템"이 아니다.

## 제안 설계 (개념)

B계층을 `Data/Dreamcatcher/Dc*` → 중립 `Data/Effects/` 로 승격:

- `DcMechanic` → `TriggerEffectRule { TriggerSpec trigger; PayloadSpec payload; EffectDomain domain }`
- `DcTriggerSlot`(Combat) → `TriggerEffectSlot` (필드 동일 + `EffectDomain domain`)
- `DcTriggerKind` → `TriggerKind` (append: `PickupOverlap`, `TimerDelay`(one-shot) — 기믹이 요구)
- `DcPayloadKind` → `PayloadKind` (기믹용 payload 추가: `SelfDamageFraction`(라스트런 crash), 기존 `SelfStatBuff`/`ApplyStackToSelf` 재사용)
- **소비처 구분** = `EffectDomain{ Dreamcatcher, Gimmick, Boss, ... }` 태그를 slot·rule 에 부착. 하류(empower 오라·기믹 아이콘·dispel·밸런스·로깅)가 이 태그로 필터. 방출되는 StatModifier 의 `ModifierOrigin` 과 1:1 매핑(중복 아님 — origin=modifier 출처, domain=rule 출처).

드림캐쳐 카드·기믹 룰 둘 다 `TriggerEffectRule[]` 데이터로 선언 → 같은 slot 에 bake → 같은 분산 감지자가 처리. "기믹이 드림캐쳐가 되고 드림캐쳐가 기믹이 되는" 것은 `domain` + 활성화 경로 차이일 뿐.

## 기믹 3시스템 → rule 매핑

| 기믹 현재 | rule 로 표현 | 판정 |
|---|---|---|
| `FatigueAccrualSystem` (10s 주기 자기 스택) | `TriggerKind.PeriodicTimer` + `ApplyStackToSelf` payload | ✅ 매끈 (PeriodicTimer 감지자 이미 존재, 일반화만) |
| 번아웃 (Fatigue 5스택 임계) | 기존 `StackModifier` 임계 경로 (A계층, 이미 공용) | ✅ 변경 거의 없음 |
| `PickupConsumeSystem` (co-location) | `TriggerKind.PickupOverlap`(신규) + `SelfStatBuff`(공속) payload | ⚠️ 신규 트리거종류+감지자 필요 |
| `LastRunSystem` (5s 지연 데미지) | `TriggerKind.TimerDelay`(신규 one-shot) + `SelfDamageFraction`(신규) payload | ⚠️ 신규 트리거+payload |
| `PickupSpawnSystem` (월드에 레드불 스폰) | **매핑 안 됨** — "유닛 트리거→효과"가 아니라 "월드 오브젝트 주기 스폰". 별도 축(스포너)으로 잔존 | ❌ 범위 밖 |
| 시즌 SO 게이팅 (`OverworkGimmickConfig` 주입) | 활성화 계층 — rule 세트를 "언제 켜나"의 문제. domain 별 활성화 소스(시즌 vs 카드 로드아웃)로 잔존 | ❌ 범위 밖(의도) |

→ **유닛 대상 trigger→effect 는 대부분 이관 가능**, 단 pickup 월드 스폰·시즌 게이팅은 통합 대상이 아니다(다른 축).

## 마이그레이션 단계 (저위험 순서, 각 단계 컴파일·Play 검증)

0. `EffectDomain` enum 신설 + `DcTriggerSlot`/`DcMechanic` 에 domain 필드 append(기본 Dreamcatcher) — **동작 불변, 순수 추가**.
1. `Dc*` → 중립 `TriggerEffect*` **rename/re-home** (Data/Effects). 동작 불변, 참조만 갱신. (가장 큰 diff, 무위험)
2. `TriggerKind.PeriodicTimer` 감지자를 boss 전용에서 일반화 → 기믹 `FatigueAccrual` 을 rule 로 이관, 기존 시스템 제거.
3. `PickupOverlap` 트리거종류 + 감지자 신설 → `PickupConsume` 이관.
4. `TimerDelay`+`SelfDamageFraction` → `LastRun` 이관.
5. 소비처(오라/UI/dispel)가 `domain` 을 읽도록 배선.

각 단계가 독립 커밋·검증 가능. 1단계(rename)는 다른 세션과 공유되는 `DcTriggerSlot`/`DcMechanic` 을 건드리므로 **조율 필요**(대규모 참조 갱신).

## 리스크 / 비용

- **payload 디스패치 switch 분산**: `AttackSystem`(984줄 "unhandled payload") 등에 큰 switch. 신규 payload 추가는 여러 감지자를 건드림 — 통합의 진짜 비용은 여기.
- **공유 파일 대량 변경**: `DcTriggerSlot`/`DcMechanic`/5개 감지 시스템/BattleBridge.Dreamcatcher 는 다른 세션이 활발히 편집 중(camera/dreamcatcher-empower). rename 은 충돌 대량 유발 가능 → 세션 조율 필수. [[battlebridge-shared-multi-session]]
- **과잉 추상화 위험** (제약 8): 지금 기믹은 룰 3개뿐. "2번째 기믹 생길 때 추출" 원칙과 균형 필요 — rename+domain 태그(0~1단계)는 저비용 고효용, 2~4단계(신규 트리거종류)는 실제 재사용이 보일 때.
- **하위호환**: `DcTriggerKind`/`DcPayloadKind` 는 SO/시트에 int 직렬화(append-only 계약). rename 은 타입명만, enum 값 순서 보존해야 기존 카드 에셋 안 깨짐.

## 권고

- **0~1단계(domain 태그 + rename/re-home)만 먼저** 하는 게 ROI 최고: 위험 낮고, "드림캐쳐 맥락 탈피 + 소비처 구분"이라는 사용자 목표의 핵심을 즉시 달성. 기믹 시스템 이관(2~4)은 **2번째 기믹**이 생겨 실제 반복이 드러날 때 추출(제약 8 준수).
- 단 rename 은 공유 파일 광범위 변경이라 **다른 세션과 타이밍 조율** 후 단독으로 진행.
- 이 문서 승인 시 `docs/spec/effect-trigger-unification/` 로 승격해 단계별 작업 단위화.

## 미해결 (승격 전 확정 필요)

1. `EffectDomain` 과 `ModifierOrigin` 를 하나로 합칠까, 별개 유지할까(rule 출처 vs modifier 출처 — 개념상 별개지만 값이 겹침).
2. rename 범위: `DcAttackMod`(trigger-less)까지 중립화할지.
3. pickup 월드 스포너를 "EffectRule 의 payload=SpawnWorldPickup"로 억지로 넣을지, 별도 스포너 축으로 명확히 분리할지(후자 권장).
