# Spec B 세션 핸드오프 — dreamcatcher-kill-and-threshold

> 다른 세션이 Spec B 를 이어받기 위한 인계. **계약의 source of truth 는 `README.md`** (plan-review 반영본). 이 문서는 그 위에 "왜 이렇게 됐나 + 검증해둔 seam + 함정 + 현재 블로커"를 얹은 지도다.

## 현재 상태 한 줄

Spec B 는 **계획 확정·리뷰 완료, 구현 미착수**. 코어 `IncomingDamage` 수술이라 **런타임 검증(Unity) 없이는 착수 위험** — 현재 Unity 다운이 최상위 블로커.

## 어떻게 여기까지 왔나 (결정 이력)

1. 사용자가 신규 드림캐쳐를 원함 → "4 개인타겟 + 1 전체타겟", 회복(힐/흡혈) 제외, **기존 기반 위 신규 능력**(기존 조합 재탕 금지).
2. 능력 5종 설계 → seam 검증 결과 **비용이 갈림**: 3종은 AttackSystem RESOLVE 저비용, 2종(last_stand·devouring)은 인프라 필요. 사용자 결정으로 **원안 플레이버 유지 + 인프라 투자**.
3. critic plan-review 권고로 **Spec A(저위험 3종) / Spec B(인프라 2종) 분리**. Spec A 완료·커밋(`2ab01723`), two-track APPROVE.
4. Spec B 계획을 plan-review(6건) 후 확정(`54f6e7bb`). 격리 worktree 위임은 **비추**(csproj/Library gitignore → 컴파일 불가, 더 위험) 판단.

## 구현 대상 2종 (README 계약 요약)

- **last_stand** = `HealthThreshold` × `SelfStatBuff(CardBuffKind.AttackDamage, +30%)`. "HP 30% 이하 1회" = `fraction=0.7`.
- **devouring_craving** = `OnKill`(매 킬) × `SelfStatBuff(CardBuffKind.AttackSpeed, +8%, TTL 4s, 비스택 refresh)`.

## 검증해둔 Seam (file:line — 재탐색 불필요)

- `IncomingDamage`(`Battle/Units/IncomingDamage.cs`) = `{ float amount; }` — **source 없음**. unit 2 가 여기에 `Entity source` 추가.
- `EnemyKilledEvent`(`Battle/Units/EnemyKilledEvent.cs`) = `{ float3 position; int awakeningReward; }` — killer 없음. enqueue = `DamageApplicationSystem.cs:188`(합산만, per-entry source 추적 없음).
- `EnemyKilledEventsSingleton` = BattleBridge 가 이미 consume-once drain(점수/각성) → **재소비 금지**. OnKill 은 DamageApplicationSystem 에서 killer 의 `DcTriggerSlot` RO 읽어 발동.
- `DcTrigger.HealthThresholdEval`(`Battle/Combat/DcTrigger.cs:46`) = `hp < maxHp*(1−k*fraction)` 반복 경계·단조 래치. "임계 이하 1회" = fraction=(1−비율).
- `BossHealthThresholdSystem`(`Battle/Combat/BossHealthThresholdSystem.cs`) — query faction-neutral(:74)이나 `OnCreate` 에 `RequireForUpdate<ThreatEntry>`(보스 게이팅, :35) + SelfBlink payload 만 처리(:89). 개명+게이팅 완화+SelfStatBuff arm 대상. threat-drain 은 `ThreatHitEvents` HasBuffer 독립 가드(:47-54)라 게이팅 제거해도 무손상.
- `ModifierStats`(`Battle/Effects/Modifiers/ModifierStats.cs`) + `ModifierStatsAggregateSystem` — SelfStatBuff 는 `StatModifierApplyEventsSingleton` 채널로 self enqueue. **base-1 함정 주의**(Spec A 교훈): `ModifierStatsDirty` 가 disabled 로 추가돼 무-모디파이어 유닛은 집계가 안 돎.
- 데이터 계층 순수성: `Wassup.Data` 는 Battle 타입 참조 금지 → `buffStat` 는 `StatKind` 가 아니라 **`CardBuffKind`**(기존 `MapDcEffect` 번역 재사용). Spec A 의 `DcCcKind`/`DcStackKind` 미러 패턴 참고.
- `IncomingDamage.source` 채울 생산자 전수: `AttackSystem.cs`(:523 직접, :331 투사체 bake owner), `ProjectileHitSystem.cs`(:134/176/209/307 = projectile.owner), `DotApplySystem.cs`(:41/59 = Null), `BattleBridge.cs` on-place(:2408/2475 = Null), + Meteor/hazard 확인. 누락 시 devouring 조용히 무발동.

## plan-review 에서 이미 닫은 것 (다시 하지 말 것)

buffStat=CardBuffKind / HealthThreshold fraction=0.7 의미 / 킬 귀속=프레임 내 source非Null 최대amount entry / OnKill 매킬 비스택 refresh / source 생산자 전수 / blink 셋업 지연. 상세는 README 계약 1~8.

## 함정 / 주의

- **base-1 무적 트랩**(Spec A): 새 stat 필드는 add-site 에서 명시 초기화 안 하면 미집계 유닛에서 0 → 곱연산 무적. (Spec B 는 SelfStatBuff 라 self 대상이지만, 새 필드 추가 시 항상 이 패턴 점검.)
- **컴파일 검증 레시피**(Unity 다운 시): `dotnet build Wassup.Runtime.csproj -clp:ErrorsOnly`. 단 `git pull` 후 새 소스가 들어오면 Unity 생성 csproj 가 stale → Layout/BattleHudTrayConfig 류 가짜 CS0234/0246. 그 파일들을 csproj `<Compile Include>` 에 임시 주입해야 clean 신호(메모리 `verify-compile-without-unity` 참조). **worktree 는 csproj/Library gitignore 라 이 방법도 불가**.
- 맥락 경계: OnKill 발동은 Units(DamageApplicationSystem)가 Combat(DcTriggerSlot) **RO 읽기** + Effects(StatModifier) **채널 쓰기**. 직접 컴포넌트 쓰기 금지.

## 착수 순서 (README 작업 단위)

0(enum/선택자 선언, compile 검증 가능) → 1(HealthThreshold 디펜더) → 2(OnKill+IncomingDamage.source, 최고 침습) → 3(카드 에셋+테스트) → handoff. unit 2 는 Unity 실행 검증과 반드시 묶어라(기존 데미지·위협·투사체 회귀).

## 관련 커밋
- `2ab01723` Spec A(정의 계층·payload 패턴 — Spec B 가 append 하는 토대)
- `54f6e7bb` Spec B 계획(이 폴더 README)
- `b8bb1157` dreamcatcher-taxonomy-cleanup(택소노미 단일화 — CardType/CardBinding 정리 배경)
