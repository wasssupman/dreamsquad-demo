# 4 — handoff summary (Spec B 구현)

Spec B(last_stand·devouring_craving) 구현 인계. 최신 계약은 `README.md`(구현 노트 포함) 우선. 계획 배경/seam 지도는 `SESSION_HANDOFF.md`.

## Commit
- `f67c2885` feat(dreamcatcher): kill-and-threshold — last_stand·devouring + IncomingDamage.source

## Implemented (compile + EditMode/PlayMode green)
- **unit 0**: `DcTriggerKind.OnKill`·`DcPayloadKind.SelfStatBuff(12)` append + `DcPayloadSpec.buffStat`(CardBuffKind) + `DcTriggerSlot.buffStat`(Battle StatKind, bake-translated). `HealthThreshold`/`fraction`/`HealthThresholdEval` 는 기존재(nightmare-catcher) 재사용.
- **unit 1 (last_stand)**: `BossHealthThresholdSystem`→`HealthThresholdSystem` 개명. `RequireForUpdate<ThreatEntry>` 제거(threat-drain 은 TryGet/HasBuffer 독립가드). SelfStatBuff arm(self 에 StatModifierApplyEvent, `duration<=0→∞`) — blink 조기-return 제거하고 payload 3-way 디스패치. bake: `MapDcBuff` 추출 + fraction/maxHpRef(스폰 maxHp)/nextBoundaryIndex=1.
- **unit 2 (devouring + 코어)**: `IncomingDamage.source`(Entity) 추가. 생산자 채움 — AttackSystem(=attackerEntity), ProjectileHitSystem×4(=threatOwner); DoT·on-place 는 Null(미귀속). `DamageApplicationSystem`: 프레임 내 source非Null 최대amount = killer(`KillAttribution.Consider` 순수 fold) → 킬 시 killer 의 OnKill×SelfStatBuff 슬롯 RO 읽어 self 에 StatModifier 채널 enqueue.
- **unit 3 (에셋+테스트)**: `Card_LastStand`(HealthThreshold f=0.7 × 공격력+30% 영구) · `Card_DevouringCraving`(OnKill × 공속+8% 4s) 카드 SO 2종 + `DreamcatcherCardCatalog` 등록. EditMode `KillAttributionTests`(5), PlayMode `DreamcatcherKillThresholdTest`(last_stand·devouring 2).

## Key Files
- `Assets/_Project/Scripts/Battle/Units/IncomingDamage.cs` — source 필드(킬 귀속)
- `Assets/_Project/Scripts/Battle/Units/KillAttribution.cs` — 킬 귀속 순수 fold(신규)
- `Assets/_Project/Scripts/Battle/Units/DamageApplicationSystem.cs` — killer 추적 + OnKill 발동
- `Assets/_Project/Scripts/Battle/Combat/HealthThresholdSystem.cs` — 개명 + SelfStatBuff arm(last_stand)
- `Assets/_Project/Scripts/Bridge/BattleBridge.Dreamcatcher.cs` — SelfStatBuff bake + `MapDcBuff`
- `Assets/_Project/Data/Dreamcatcher/Card_{LastStand,DevouringCraving}.asset`

## Verified
- EditMode: 716 pass / 0 fail (2 pre-existing Ignore) — 신규 KillAttribution 5 포함.
- PlayMode: Spec B 2/2 green(리뷰 반영 후 재확인). 기존 dreamcatcher/combat(DreamcatcherCombatDamage·DreamcatcherEffect·PlacementAura) green → `IncomingDamage.source` 회귀 0.
- **투트랙 리뷰(2026-07-13): 양 트랙 APPROVE** (code-reviewer + ecs-reviewer, CRITICAL/HIGH 0). 반영한 MEDIUM 3건 아래 Notes.
- **비-Spec-B 실패 4건**(회귀 아님): AuthE2E(HTTP timeout), Dreamstone/Squad CarryIn 2건(Placement→**Gift**, gift-phase 흐름 drift), MovementIntegrity(unfocused aggro 5s 타이밍). 앞 2건은 gift-phase 테스트 갱신 필요(별도 스코프), MovementIntegrity 는 포커스 재실행으로 재확인 권장.

## Review 반영 (MEDIUM 3건)
- **additive parity**(code MEDIUM-1): SelfStatBuff 를 `op=Multiplicative` 하드코딩 → `ModifierAuthoring.FromMultiplier` 경유로 변경. +% 버프가 squad/on-place 와 동일 Additive 버킷에 합산(관례 일치). 두 fire arm 모두.
- **stackId 네임스페이스**(code MEDIUM-2): `(ushort)(instanceId & 0xFFFF)` 잘라쓰기 → `DcTriggerSlot.statBuffStackId`(bake 시 `_dcStackCounter++`, squad 이펙트와 동일 단일 할당자)로 교체. 충돌 원천 차단, "instanceId≠stackId 네임스페이스" 불변식 유지.
- **per-frame 할당**(ecs MEDIUM-1): HealthThresholdSystem 이 매 프레임 디펜더 쿼리+2배열 할당하던 것을 **첫 SelfBlink 발동 때 지연 생성**. last_stand-only(blink 없는) 판에서 할당 0.

## Notes (되돌리면 안 됨)
- **base-1 무적 트랩**: buffStat 은 `damageVsCcMul` 같은 새 stat 이 아니라 기존 damageMul/attackSpeedMul(둘 다 base 1) 재사용이라 무해. 하지만 새 stat 추가 시 항상 add-site 1f 초기화 점검.
- **영구=∞ 컨벤션**: last_stand 카드 duration=0 → arm 이 `float.PositiveInfinity` 로 해석. 카드에 큰 수 하드코딩 금지.
- **킬 귀속 = strict `>`**(동점은 먼저 접힌 엔트리). KillAttributionTests 가 고정 — 완화 금지.
- **HealthThresholdSystem 이 이제 매 프레임 실행**(flowfield gated). 보스 없는 판에서도 threat-drain 돎(빈 ThreatEntry = no-op). 되돌려 ThreatEntry 게이팅 넣으면 디펜더 last_stand 죽음.
- **OnKill 은 EnemyKilled 큐 재소비 아님** — DamageApplicationSystem 킬 지점 직결. `EnemyKilledEvent.killer` 필드 추가 안 함(불필요).

## Follow-up
- MovementIntegrity + gift 2건 포커스 재실행 확인(사용자).
- gift-phase 흐름 변경으로 drift 난 Squad/Dreamstone CarryIn 테스트 갱신(별도 스코프).
- **테스트 갭(review LOW)**: devouring PlayMode 는 melee(직접 데미지)만 커버 — **투사체(궁수) 킬 귀속**(source=projectile.owner) 경로는 미검증. ranger 변형 테스트 추가 후보.
- `ApplyCcToTarget(Impulse)` 넉백·Slow-stat 페이로드 등 후속(Spec A follow-up).
- **시트 스키마 확장(별도 스펙)**: `DcMechanicDto`+`DcSheetExporter`+시트 탭에 `triggerFraction`·`buffStat`(Spec B)·`ccKind`·`stackKind`(Spec A) 컬럼 추가. 없으면 export 시 정의값 누락(import 는 partial 이라 무손실). Spec A/B 공통 — `dreamcatcher-sheet-sync` 확장으로.
