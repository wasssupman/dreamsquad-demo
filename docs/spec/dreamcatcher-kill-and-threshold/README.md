# dreamcatcher-kill-and-threshold

> 상태: 드래프트 (대기) — Spec A `dreamcatcher-new-abilities` 완료 후 착수. 2026-07-12

## 상위 목표

인프라 투자가 필요한 드림캐쳐 **2종**. 사용자 결정으로 원안 플레이버를 살린다. 코어 전투 파이프(`IncomingDamage`)와 실행 시스템(HealthThreshold)에 손대므로 저위험 3종(Spec A)과 **분리**해 게이팅을 끊는다.

## 신규 능력 2종

### 🟨 개인 타겟 (Unit)
1. **`last_stand` / 최후의 발악** — `HealthThreshold(0.3)` × `SelfStatBuff(stat=DamageMul, +30%)`
2. **`devouring_craving` / 포식의 갈망** — `OnKill` × `SelfStatBuff(stat=AttackSpeedMul, +8%, TTL 4s, refresh)`

## 왜 별도 spec 인가 (critic 근거)

두 능력은 BLOCKER/HIGH 결함 대부분이 집중된 인프라를 연다:
- `IncomingDamage`(`{ float amount; }`)에 **source 추가** = 데미지 생산자 다수 영향(코어 struct).
- `EnemyKilledEventsSingleton` 는 BattleBridge 가 이미 consume-once drain → **두 번째 소비자 금지**. OnKill 발동을 다른 seam 에서.
- `BossHealthThresholdSystem` 이 `RequireForUpdate<ThreatEntry>`(보스 게이팅) + SelfBlink payload 만 처리.

## 작업 단위 (초안 — A 완료 후 확정)

| # | 구분 | 목적 |
|---|---|---|
| 0 | contract | append `DcTriggerKind.OnKill` + `DcPayloadKind.SelfStatBuff` + 선택자 `StatKind buffStat`(DcPayloadSpec+DcTriggerSlot). (`ccKind`/`stackKind` 는 Spec A 에서 이미 추가됨) |
| 1 | infra+feature | 디펜더 HealthThreshold(last_stand): `BossHealthThresholdSystem` → `HealthThresholdSystem` 개명, threat-drain 독립 가드 + `RequireForUpdate<ThreatEntry>` 제거, 디펜더 bake(`fraction`/`maxHpRef`/`nextBoundaryIndex=1`), SelfStatBuff eval 을 blink 조기-return 이전 배치 |
| 2 | infra+feature | OnKill(devouring): `IncomingDamage.source` 추가 + 생산자 채움(**투사체 owner 포함**, DoT/on-place=Null) + `EnemyKilledEvent.killer` + `DamageApplicationSystem` 에서 killer 의 `DcTriggerSlot` RO 읽어 SelfStatBuff → StatModifier 채널(신규 채널·drain 재소비 금지) |
| 3 | assets | DreamcatcherCard SO 2종 + 값 + 통합 + 테스트(궁수 킬로 devouring 발동 assertion 포함) |
| 4 | docs | handoff |

## feature-wide 계약 (critic 반영, A 완료 후 확정)

1. **OnKill 발동 (BLOCKER)**: `EnemyKilledEventsSingleton` **재소비 금지**. `DamageApplicationSystem`(Units)이 killing blow 의 `IncomingDamage.source`(killer)를 알고, killer 의 `DcTriggerSlot`(Combat) **RO 읽기**로 OnKill 슬롯 확인 → self 에 StatModifier 채널(Effects) enqueue. 맥락 간 읽기만·쓰기는 채널.
2. **킬 귀속 범위 (HIGH)**: **투사체 킬 포함**(투사체는 `owner` 보유 → source=owner). DoT(`DotApplySystem`)·on-place·환경 = source=Null → OnKill 미발동. killing-entry 규칙: source 非Null 인 마지막 처리 entry.
3. **SelfStatBuff (BLOCKER)**: `buffStat` 선택자로 last_stand=DamageMul / devouring=AttackSpeedMul 구분. self 에 TTL + 고유 stackId, refresh=max remaining(기존 `ModifierApplySystem` merge 재사용, 신규 코드 0). devouring 은 비스택 단일 슬롯.
4. **HealthThreshold 게이팅 (MEDIUM)**: 시스템 개명 + threat-drain 독립 가드(제거해도 위협 드레인 무손상 — `ThreatHitEvents` HasBuffer 가드 독립). query faction-neutral 이라 디펜더 자동 포함.
5. **새 플레이 오브젝트 0**: 카드 아트만. 신규 ProjectileData 불필요.
6. **테스트**: `IncomingDamage.source` 추가로 인한 기존 데미지 테스트 회귀 확인. 궁수 투사체 킬 → devouring 발동, HP 임계 → last_stand 발동 PlayMode assertion.

## 파이프라인 커버리지

**N/A** — 신규 플레이 오브젝트 없음.

## 착수 전제

Spec A 완료(특히 `ccKind`/`stackKind` 선택자 + `ApplyCcToTarget`/`ApplyStackToTarget` 패턴 확립) 후, 그 위에 `SelfStatBuff`/`OnKill` 를 append 한다. A 의 RESOLVE arm 패턴을 참고 구현.
