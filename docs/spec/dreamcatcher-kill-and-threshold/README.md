# dreamcatcher-kill-and-threshold

> 상태: **완료 2026-07-13** — 코드 3유닛 + 카드 2종 + 테스트 + 투트랙 리뷰(양 트랙 APPROVE) 반영. EditMode 716 green(신규 KillAttribution 5), PlayMode Spec B 2/2 green(last_stand·devouring), 기존 dreamcatcher/combat PlayMode 회귀 0.
>
> **시트 연동(확인됨, 별도 스코프):** DcSheetExporter/Applier 왕복은 신규 카드를 자동 열거하지만 `DcMechanicDto` 스키마에 `triggerFraction`·`buffStat`(Spec B)·`ccKind`·`stackKind`(Spec A) 컬럼이 없어 정의값을 담지 못함(import 는 partial-update 라 손실 없음, 편집만 불가). 완전 연동 = `dreamcatcher-sheet-sync` 확장(Spec A 도 해당) 별도 스펙.
>
> **구현 노트 (드래프트 계획 대비 실제):**
> - `HealthThreshold` 트리거·`fraction` 필드·`HealthThresholdEval` 는 nightmare-catcher(보스)가 **이미 구현** → unit 0 는 `OnKill`·`SelfStatBuff`·`buffStat` 만 신규.
> - `BossHealthThresholdSystem` → **`HealthThresholdSystem` 개명** + ThreatEntry 게이팅 제거 + SelfStatBuff arm 추가(디펜더 last_stand 수용). 신규 시스템 없음.
> - **buffStat 번역은 bake 시점**(`MapDcBuff` 추출) → slot 에 Battle `StatKind` + 배율(magnitude) baked. ccKind/stackKind 선례.
> - **영구버프 = `duration<=0 → float.PositiveInfinity`**(기존 무한 컨벤션). last_stand=duration0(영구), devouring=4s TTL refresh.
> - **킬 귀속**은 `KillAttribution.Consider` 순수 fold 로 분리(EditMode 결정성 고정). DamageApplicationSystem 이 프레임 내 source非Null 최대 amount = killer.
> - **시트 통합 = N/A**: DcSheetApplier 는 id-match 부분갱신이라 시트에 없는 카드는 미터치. 능력 카드는 Unity-authored(값이 mechanics 에 baked). 시트 row 불요.
> - **덱 통합 = 카탈로그 등록만**: 기본 덱(10장 preset)은 캡 유지 위해 미변경. 두 카드는 `DreamcatcherCardCatalog`(가용 카드 풀)에 등록 → 덱빌더에서 선택 가능.

## 상위 목표

인프라 투자가 필요한 드림캐쳐 **2종**. 코어 전투 파이프(`IncomingDamage`)와 실행 시스템(HealthThreshold)에 손대므로 저위험 3종(Spec A)과 **분리**해 게이팅을 끊는다.

## 신규 능력 2종

### 🟨 개인 타겟 (Unit)
1. **`last_stand` / 최후의 발악** — `HealthThreshold` × `SelfStatBuff(buffStat=AttackDamage, +30%)`. **1회성 "HP 30% 이하"** → `fraction=0.7` (경계 = maxHp×(1−1×0.7)=30%, 다음 경계 음수라 재발동 없음). ※ `HealthThresholdEval` 은 `1−k*fraction` 반복 경계라 "임계비율 이하 1회" = `fraction=(1−비율)`.
2. **`devouring_craving` / 포식의 갈망** — `OnKill`(매 킬) × `SelfStatBuff(buffStat=AttackSpeed, +8%, TTL 4s)`. **비스택 refresh**(중첩 아님 — 킬마다 고정 stackId 로 +8% 버프의 지속을 갱신). 카피는 "스택" 금지, "처치 시마다 공속 버프 갱신"으로.

## 왜 별도 spec 인가

- `IncomingDamage`(`{ float amount; }`)에 **source 추가** = 데미지 생산자 다수 영향(코어 struct).
- `EnemyKilledEventsSingleton` 는 BattleBridge 가 이미 consume-once drain → **재소비 금지**. OnKill 은 다른 seam.
- `BossHealthThresholdSystem` 이 `RequireForUpdate<ThreatEntry>`(보스 게이팅) + SelfBlink payload 만 처리.

## 작업 단위 (초안)

| # | 구분 | 목적 |
|---|---|---|
| 0 | contract | append `DcTriggerKind.OnKill` + `DcPayloadKind.SelfStatBuff` + 선택자 **`CardBuffKind buffStat`**(DcPayloadSpec+DcTriggerSlot). ⚠ StatKind 아님 — 데이터 계층 순수성(기존 `MapDcEffect` 가 CardBuffKind→StatKind 번역 재사용) |
| 1 | infra+feature | 디펜더 HealthThreshold(last_stand): `BossHealthThresholdSystem`→`HealthThresholdSystem` 개명, `RequireForUpdate<ThreatEntry>` 제거+threat-drain 독립 가드, **blink 셋업은 blink 슬롯 존재 시에만 지연**, 디펜더 bake(`fraction`/`maxHpRef`/`nextBoundaryIndex=1`), SelfStatBuff payload arm(StatModifier 채널) — blink 조기-return 이전 배치. (분리 시스템 추출도 검토) |
| 2 | infra+feature | OnKill(devouring): `IncomingDamage.source` 추가 + **전 생산자 채움**(아래 목록) + `EnemyKilledEvent.killer` + `DamageApplicationSystem` 에 **per-entry source 추적** + killer 의 `DcTriggerSlot` RO 읽어 SelfStatBuff→StatModifier 채널(재소비 금지) |
| 3 | assets | DreamcatcherCard SO 2종 + 값 + 통합 + 테스트 |
| 4 | docs | handoff |

## feature-wide 계약 (plan-review 반영)

1. **SelfStatBuff 선택자 = `CardBuffKind`** (BLOCKER 해소): `StatKind` 는 Wassup.Data 에서 참조 불가. `MapDcEffect(CardBuffKind)→(StatKind,mult)` 재사용해 self 에 StatModifier enqueue. last_stand=AttackDamage / devouring=AttackSpeed 로 구분.
2. **HealthThreshold 의미** (HIGH 해소): 반복 경계 eval 이므로 "임계 이하 1회"는 `fraction=1−임계`. last_stand=0.7. 디펜더 bake 가 `maxHpRef`(스폰 maxHp)·`fraction`·`nextBoundaryIndex=1` 설정. 값은 SO/시트 유래.
3. **OnKill 발동** (BLOCKER): `EnemyKilled` 큐 **재소비 금지**. `DamageApplicationSystem`(Units)이 killing entry 의 `source`(killer)로 killer 의 `DcTriggerSlot`(Combat) **RO 읽기** → OnKill 슬롯이면 self 에 StatModifier 채널(Effects) enqueue. 매 킬 발동(period 무시). 맥락 간 읽기만·쓰기는 채널.
4. **킬 귀속 규칙** (HIGH 해소, 명시 결정): killing entry = **그 프레임 IncomingDamage 중 `source` 非Null 최대 amount** entry. DoT/on-place/환경(source=Null)은 미귀속(OnKill 미발동) — 의도. 오귀속 한계(비치명 기여가 최대일 때)는 수용(크레딧=공속버프, 비치명적).
5. **`IncomingDamage.source` 생산자 전수** (MEDIUM 해소): 채워야 할 write 사이트 — `AttackSystem.cs`(직접 :523, 투사체 bake :331 owner), `ProjectileHitSystem.cs`(:134/176/209/307 = projectile.owner), `DotApplySystem.cs`(:41/59 = Null), `BattleBridge.cs` on-place(:2408/2475 = Null), + Meteor/hazard 확인. 미설정=Null=미귀속(의도). **핵심 공격 경로 누락 시 devouring 무발동** → 전수 체크 필수.
6. **HealthThreshold 게이팅** (MEDIUM): 개명 + threat-drain 독립 가드(제거해도 `ThreatHitEvents` HasBuffer 가드 독립 → 무손상). query faction-neutral 이라 디펜더 자동 포함.
7. **새 플레이 오브젝트 0**: 카드 아트만. 신규 ProjectileData 불필요.
8. **테스트**: `IncomingDamage.source` 추가로 인한 기존 데미지/위협 테스트 회귀 확인. HP 임계→last_stand·궁수 킬→devouring PlayMode assertion. 킬 귀속 규칙(다중 source)의 결정성은 테스트로 고정. (Unity 실행 필요.)

## 파이프라인 커버리지

**N/A** — 신규 플레이 오브젝트 없음.

## 착수 전제 / 리스크

- Spec A(선택자·payload 패턴) 위에 append. `SelfStatBuff` self-enqueue 는 HealthThresholdSystem·DamageApplicationSystem 두 곳이 `StatModifierApplyEventsSingleton` writer 확보 필요.
- **최상위 리스크**: 코어 `IncomingDamage` 변경의 정합성(기존 데미지·위협·투사체 무손상, 킬 귀속)은 **런타임 검증 의존** → Unity 다운 중 compile-only 커밋은 회귀 위험 큼. **Unity 복구 후 in-place 착수 권장**(격리 worktree 는 csproj/Library gitignore 로 컴파일조차 불가 → 더 위험).
