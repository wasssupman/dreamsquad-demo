# 효과·스탯 — 키워드 census

> 도메인: status effects · modifiers · hazards
> 조사 범위: `Assets/_Project/Scripts/Battle/Effects/**` · `Battle/Units/{Shield*,Health,DamagedCounter,MaxHealthScale*}` ·
> `Data/{HazardSO,StackModifierSO,EffectTileData}` · `Bridge/BattleBridge.{Dreamcatcher,UnitStats}.cs` ·
> `Skills/SkillModifierKinds.cs` · 관련 spec 14종 · `docs/spec/README.md` Follow-up Backlog ·
> `docs/reference/battle-core-architecture.md` §1.5~1.6 · §8 불변식 · `CLAUDE.md`
> 모든 포인터는 grep 으로 존재를 확인했다. 결정 이력을 못 찾은 항목은 「이력 미발견」으로 적는다.

---

## 키워드 표 (58행)

| 키워드 | 한 줄 정의 (게임 언어로) | 현행 구현 포인터 | 판정 | 애매하면: 결정 이력 + 미결 요지 |
|---|---|---|---|---|
| 실효 스탯 캐시 | 「이 유닛이 지금 실제로 얼마나 세나」를 매 프레임 한 번 접어 둔 값 | `ModifierStats` · `ModifierStatsDirty`(enableable) · `ModifierStatsAggregateSystem`(유일한 writer) | 확정 | |
| 스탯 7종 | 바꿀 수 있는 것: 공격력·공속·받는피해·재생·이동속도·CC 대상 피해·최대체력 배율 | `StatKind` | 확정 | |
| 결합 3종 | 곱하기 / 더하기 / 강제 고정 | `CombineOp{Multiplicative, Additive, Override}` | 확정 | |
| 저작 분류 규칙(Policy B) | 올리는 버프는 더하기, 깎는 디버프는 곱하기 | `ModifierAuthoring.FromMultiplier` | 확정 | 사용자 결정 2026-07-03 (`docs/spec/modifier-additive-authoring/`) |
| 결합식 + 바닥/천장 | 버프가 무한히 달아나거나 디버프가 0으로 소멸하지 않게 자른다 | `ModifierMath.CombineMul` · `ModifierStatsAggregateSystem` 상수 `[0.2,5]` · 이동 `[0.15,3]` · 최대체력 바닥 `0.05` | 확정 | `modifier-stacking-policy`(실측: Guardian 데미지 15→0.24 곱누적). 경계값 SO 저작은 백로그 [S] |
| **비율 합성 규약** | 10%+10%가 21%인가 20%인가 | 현행 `(1+Σadd)×Πmul`, float | **애매** | `docs/spec/unit-stats-and-modifiers/2-stat-modifier-system.md` §3.1 + `3-migration-notes.md` §3 이 **정반대**를 요구한다(고정소수점 scale 1000 · 「가산 후 1회 승산」). 그 문서가 스스로 「가장 큰 변경」이라 못박음 |
| **Override 항등** | 강제 고정을 걸 수는 있는데 **풀 수가 없다** | `ModifierStatsAggregateSystem`(`hasOver` 래치 + `math.max`) · 회수는 `BattleBridge.RevokeDreamcatcherEffects` | **애매** | 회수 = 항등값 재발행인데 Override 에는 항등이 없다(0 을 보내도 래치가 남는다). 현재 Override 생산자 0 이라 라이브 영향 없음 |
| `regenPerSec` 예외 | 자원값이라 배율 클램프 밖 | `ModifierStatsAggregateSystem` `math.max(0f, …)` | **애매** | 결합식이 `(0+Σadd)×Πmul` 이라 **곱셈 슬롯만 있으면 0**. 백로그 「regenPerSec Override 음수 처리」[S] (ecs-review M2) |
| `ModifierOrigin` 14 | 이 버프를 누가 걸었나 (머지 키가 아니라 꼬리표) | `Effects/Modifiers/ModifierTypes.cs` · `ModifierHeader.origin` | 확정 | 생산자 있는 12 + 기본값 + 은퇴한 시너지. **번호 보존 append-only** |
| 시너지 은퇴 슬롯 | 인접 동족 버프는 철거됐고 번호만 남았다 | `ModifierOrigin.Synergy`(서수 3) · stackId 1 | 확정 | `docs/spec/synergy-toggle/README.md` — 은퇴 2026-09-03 (커밋 `6e9acfbb`, 사용자 결정) |
| 도메인 미러 enum | 스킬 레이어가 엔진 없이 부르는 같은 어휘 | `Wassup.Skills.SkillModifierOrigin`/`SkillStatKind`/`SkillCombineOp` → `Battle/Skills/EcsSkillContext.cs:688` 숫자 캐스트 · `SkillModifierKindPinTests` | 확정 | 번호 보존 제약의 **실제 이유**가 이 캐스트다. `SkillCombineOp.FromAuthoredMultiplier`(=3) 는 미러에만 있는 넷째 값 |
| `stackId` 네임스페이스 | 같은 출처·같은 스탯이라도 슬롯을 따로 쓰고 싶을 때의 칸 번호 | on-place 0 · ~~시너지 1~~ · 효과타일 2(`EffectTileStackId`) · 아군장판 3(`AllyBuffField.StackId`) · 드림캐쳐 100+(`_dcStackCounter`) · 스택 파생 100+kind(`StackModifierTickSystem.StackDerivedStackIdBase`) | **애매** | **손으로 분양하는 전역 번호판**이다. 재설계의 대안(소스 ID 정렬)은 Mono 스펙 §3.2 |
| 병합 키 4축 | 누가·어느 스탯·어느 결합·몇 번 칸 — 넷이 다 같아야 한 슬롯 | `ModifierApplySystem.ApplyStat` | 확정 | 불변식: 출처가 다르면 같은 종류의 버프도 덮지 않고 각자 산다 |
| 갱신 규칙 | 같은 칸에 다시 걸면 크기는 새 값, 시간은 긴 쪽 | 동상 (`remaining = max(old,new)`, `magnitude = new`) | 확정 | |
| 누적 상한 | 상한을 실으면 갱신이 **쌓기**로 바뀐다 (광란) | `StatModifierApplyEvent.magnitudeCap` · `ModifierAuthoring.StackCap` · `FrenzyStackingTests` | 확정 | `dreamcatcher-berserker` unit 0~1. 상한은 magnitude 만 막고 **remaining 은 안 막는다**(가장 뜨거운 지점에서 꺼지는 함정 회피) |
| 철회 = 항등 덮어쓰기 | 버프를 「지우는」 게 아니라 ×1.0 으로 중화한다 | `RevokeDreamcatcherEffects`(원본 op 재도출 후 그 op 의 항등) · `EnqueueStatModifierRaw` | **애매** | ⚠ 철회에 상한을 실으면 `min(cap, 기존+항등) = 기존` 이라 **조용히 실패**. 백로그 「Dispel/Cleanse 채널」[S] · 「출처 사망 시 모디파이어 회수」(skill-layer-migration 후속) |
| 부여 채널 + 1프레임 지연 | 이번 프레임에 건 버프는 대개 **다음** 프레임에 듣는다 | `StatModifierApplyEventsSingleton` · `ModifierApplySystem`(`[UpdateBefore(MovementSystem)]`) | **애매** | `docs/spec/battle-sim-extraction/0_system_order_capture.md`: 생산자 11 중 **8이 소비자 뒤**(AttackSystem·DamageApplication·ProjectileHit·HealthThreshold·StackModifierTick·DreamCocoon·PickupConsume·FatigueAccrual), 같은 프레임은 3(AllyBuffField·ZoneApply·BossPeriodicTrigger). 문서가 명시적으로 「**재배치 판단은 M1 의 몫**」 |
| 만료 | 슬롯마다 자기 시계, 0 이면 사라지고 재계산을 깨운다 | `StatModifierTickSystem` | 확정 | 과거 버그: dirty 로 쿼리해서 만료가 영원히 안 돌았다(주석에 기록) |
| 드림캐쳐 오라 판정 | 「드림캐쳐 버프를 받는 중인가」를 몸에 빛으로 표시 | `ModifierAuraClassifier`(순수, `ModifierAuraClassifierTests`) | 확정 | origin 필터 + net 편차를 함께 본다(중화된 슬롯은 비활성). `DamageVsCcMul`/`MaxHealthMul` 은 판정 제외 |
| 실효 스탯 표시 | 선택 패널의 체력·공격력·공속 + 기본값 대비 델타 | `Bridge/BattleBridge.UnitStats.cs` `TryGetUnitStatReadout` · `UnitStatMath.CooldownToRate` | 확정 | 조건부 배율(CC 대상·최전방·바운스)은 **의도적으로 제외**(대상·시점 의존이라 접으면 거짓 표시). `Health.max` 에 maxHealthMul 이 이미 반영돼 있어 재곱 금지 |
| 투사체 귀속 결함 | 같은 적이 쏠 때마다 새 슬롯이 생겨 디버프가 곱누적된다 | `ProjectileHitSystem` 의 `ApplyStat` 이 `source` 로 **투사체 엔티티**를 보냄 | **애매(라이브 결함)** | 백로그 `enemy-fire-stack-shooter` 후속 [S]. `ApplyStack` 은 unit 0 에서 고쳤고 이쪽만 남음. 고치면 곱누적→상시 ×0.6 이라 **수치 재조정과 한 묶음** |
| 스택 6종 | 불·얼음·출혈·독 + 시즌 기믹 피로 | `StackKind{None,Fire,Ice,Bleed,Poison,Fatigue}` | 확정 | |
| 스택 병합 키 2축 | 누가 걸었나 × 무슨 스택 (칸 번호 없음) | `ModifierApplySystem.ApplyStack` | 확정 | Stat(4축)과 **비대칭**이고 `origin` 도 안 싣는다(주석에 의도 명시) |
| 스택 저작 | 최대 중첩 · 1회 지속 · 갱신 정책 | `Data/StackModifierSO.cs`(`DefaultMaxStack = 5`, `perAppDuration`, `StackPolicy`) | 확정 | `StackPolicy.PerStackInline`/`DecayTick` 은 **소비처 0**(죽은 확장 포인트) |
| 임계 규칙 | N중첩에 도달하면 도트/스턴/스탯이 터진다 | `ThresholdRule`(`atStack` × `Edge`/`Consume` × `ApplyDot`/`ApplyStun`/`ApplyStat`) · `StackModifierTickSystem.DispatchThresholds` | 확정 | 배열은 `atStack` 오름차순 **가정**(검증 코드 없음) |
| Edge 는 올라가는 길에만 | 최대 중첩에 닿는 순간 규칙이 멈춘다 | 동상 (`stackCount > lastTriggeredStack`) | 확정 | `dreamcatcher-berserker` README — 광란이 스택 시스템을 못 쓴 이유. 「가장 뜨거워야 할 지점에서 버프가 꺼진다」 |
| 임계 규칙 레지스트리 | 「불 스택의 규칙」은 판 전체에 한 벌뿐 | `BattleBridge._stackThresholds`(static Dictionary, `:9820`) · `GetStackThresholds`(`:9900`) | **애매** | 백로그 「화염 스택을 출처별로 가르기」[M] (elite-enemy-tier) — 드래곤과 킨들러가 `StackModifier_Fire` 를 물리적으로 공유. 드래곤을 4→10 올렸더니 킨들러도 같이 올라갔다 |
| CC 5종 | 감속 · 넉백 · (도트 토큰) · 기절 · 수면 | `CcKind{Slow,Impulse,DoT,Stun,Sleep}` | 확정 | Root 없음 — 전면 정지는 이동 배율 0 이 아니라 **전용 플래그**로 만들라는 규정(`ModifierStatsAggregateSystem` 주석) |
| CC 병합 | 같은 종류는 슬롯 하나, 시간은 긴 쪽 | `CcEffectMerge.Apply`(키 = kind, vector/scalar/tickInterval 은 incoming 으로 갱신) | 확정 | `tickTimer` 는 보존하되 주기가 바뀌면 **비례 환산** |
| 행동 잠금 | 잠·기절은 공격도 이동도 못 «시작» 한다 | `CcActionLock.IsLock`/`IsLocked`(Combat·Movement 는 읽기만) · `CcActionLockTests` | 확정 | `combat-action-lock` 계약 1~2 (2026-07-10 사용자 확정). 넉백은 잠금이 아니다 — 밀리는 중에도 때린다 |
| 피격 기상 | 잠든 유닛은 맞으면 즉시 깬다 | `CcClearRequestsSingleton` → `CcClearSystem`(`[UpdateAfter(DamageApplicationSystem)]`) | 확정 | 계약 3 — Units 는 Effects 소유 `CcEffect` 를 직접 못 지운다. Stun 은 wake 대상 아님 |
| CC 감쇠 | 시간은 이동 **후**에 깎는다 | `CcDecaySystem` `[UpdateAfter(MovementSystem)]` | 확정 | 무한 = `+∞`(자연 통과) |
| 보스 면역 | 잠금 + 넉백을 막는다, 출처 불문 | `CcActionLock.IsBossImmune` · 거절점 2곳(`CcApplySystem` · `EffectSpawner.ApplyCc`) · `BossCcImmunityTests` | 확정 | `boss-jjangssen` unit 8 이 「직접 출처」 예외 축(`CcSource`)을 은퇴시킴 — 근거였던 「스택 DoT 가 CC 큐를 공유」가 `dot-effect-extraction` 으로 소멸 |
| 넉백의 실체 | 밀리는 동안 계속 미는 속도 (순간 이동 아님) | `MovementSystem.cs:190` `vector * dt` 누적, **상태 분기 앞에서 1회 합성** | 확정 | 합성 지점이 하나여야 하는 이유: 복사본이 늘면 일부 상태가 조용히 넉백 면역이 된다(실제로 4상태가 면역이었다) |
| 감속은 CC 가 아니다 | 저작은 `Slow` 인데 실제로는 이동속도 모디파이어 | `ZoneApplySystem` → `StatKind.MoveSpeedMul` | 확정 | `modifier-legacy-migration` unit 2 |
| `CcKind.DoT` | 저작 토큰으로만 남은 껍데기 | `HazardEffect.kind` 는 저작 · 런타임은 `DotApplyEvent` 로 분기 · 로그 태그로만 잔존 | 확정 | `dot-effect-extraction` 계약 1 |
| 지속 피해 전용 버퍼 | 타는 것과 묶이는 것은 다른 계층 | `DotEffect` · `DotApplyEventsSingleton` · `DotApplySystem`(부여→틱→감쇠 전부) | 확정 | 분리 전 실측 결함: 출혈 중인 적이 화염 장판을 밟으면 총 ~194(의도 50)를 맞고 **장판을 나가도 계속 탔다** |
| 도트 병합 키 = 출처 × 원소 | 장판 불과 중첩 불은 서로 덮지 않는다 | `DotEffectMerge`(`DotOrigin{Stack,Zone,OnPlace}` × `DotElement{Bleed,Fire,Ice,Poison}`) | 확정 | **설계 불변식 13** — 두 축 겸직 금지. source(Entity) 를 축으로 쓸 수 없는 이유도 주석에 |
| 이산 틱 | 초당 수십 번 대신 주기마다 한 덩어리 | `DotTick.Advance`(`MaxTicksPerFrame = 1024`) · 신규 슬롯 첫 틱 즉발 · `DotTickTests` | 확정 | `tickInterval > 0` 이면 scalar 가 **틱당 피해**로 의미가 바뀐다 |
| 다중 공격자 도트 | 난도질꾼 2기가 물어도 출혈은 안 합산된다 | `DotEffectMerge`(같은 키는 한 슬롯, `remainingTime = max`) | 확정 | 사용자 결정 2026-07-30 「그대로 두기」. 뒤집으려면 `enemy-fire-stack-shooter` README 계약 2·6-1 인용 후 재승인 |
| 실드 | 피해보다 먼저 깎이고, **시간으로는 안 사라진다** | `Units/ShieldSlot.cs` · `ShieldMath.Merge/Absorb/Sum/ValueFromSource` (쓰기는 `DamageApplicationSystem` 단독) | 확정 | 같은 출처 = max(중첩 불가) · 다른 출처 = 합산 · 소모는 오래된 슬롯부터(삽입 순, 결정론). 사용자 결정 2026-07-21 |
| 피해 순서 | 받는피해 배율 → 실드 흡수 → 체력 | `DamageApplicationSystem.cs:168 → :192` | 확정 | `shield-guardian-defender` 계약 2 — 표시 데미지 = 흡수량 |
| 완전 흡수 = 피격 아님 | 실드로 다 막으면 「맞았다」로 세지 않는다 | 동상 (`totalDamage` 0 분기가 wake-on-hit·가시갑옷·데미지넘버·킬귀속을 전부 가른다) | 확정 | 사용자 결정 2026-07-21 — 가시갑옷류와 상성 나쁨이 **인지된 트레이드오프** |
| 실드 파열 | 실드 합이 양수에서 0 이 되는 그 순간 = 사건 | `Units/ShieldBreakEvent.cs`(`preSum>0 && post==0`) · `Effects/ShieldGrantedEvents.cs` | 확정 | 시간만료 경로가 **구조적으로 없어** 배제됨(`dreamcatcher-shield-break` 계약). 같은 채널을 `OnDamagedN` 피격 폭발이 공유(`fromDamagedTrigger`) |
| 최대체력 배율 | 배율은 Effects 가 정하고 체력은 Units 만 쓴다 | `StatKind.MaxHealthMul` → `MaxHealthScaleSystem` → `Health.ScaleMax` · `MaxHealthScaleState.baseMax` | 확정 | 바닥 1 HP · 축소 시 현재값 클램프 · 복원 시 **무료 힐 없음**. lazy-attach(배율이 1 에서 벗어난 첫 프레임에 baseMax 캡처) |
| 피격 카운터 | 「N번 맞을 때마다」 카드의 셈판 | `Units/DamagedCounter.cs`(버퍼, Units 소유) + `DcGateKind` 게이트 + `payload`/`aoeVisualScale` | 확정 | `DcTriggerSlot`(Combat)과 **다른 버퍼**인 이유 = 카운터 쓰기가 Units 소유라서 |
| 열기 / 피로 | 시즌 기믹이 시간으로 쌓는 것 | `HeatAccrual`+`HeatMath.Delta`(`HeatMathTests`) · `FatigueAccrual` → `StackKind.Fatigue` → `ModifierOrigin.Burnout` | 확정 | 둘 다 lazy-attach(스폰 경로 무수정). 열기는 HP 1 바닥이라 **열기로는 죽지 않는다** |
| 장판(존) | 밟고 있는 동안만 듣는 땅 | `Data/HazardSO.cs`(shape × `HazardEffect[]` × lifetime) · `Hazard{originCell, radiusTiles}` · `ZoneApplySystem`(매 프레임 재발행) · `HazardLifetimeSystem`(수명만) | 확정 | ⚠ 셀 해시(`HazardSingleton.cellToEffects`)는 **은퇴** — `distance-based-range` unit 19 가 연속 원(해저드 스냅샷 × 피해자 몸)으로 교체. grep 결과 0건 |
| 존은 적 전용 | 아군 회복 장판을 만들 수 없다 | `ZoneApplySystem` 의 `Faction.EnemyUnit` 하드 게이트 | **애매** | 백로그 `summon-patrol-defender` 후속 [S] — 「실제로 생기면 그때 `HazardEffect` 에 진영 축을 연다」(제약 8). 드래곤 브레스 옵션 A 가 막힌 자리이기도 함 |
| 길막 설치물 | 체력이 있어 부숴야 사라지는 장애물 | `BlockingHazardSO`/`BlockingHazard`(`maxHp`·`decayPerSec`·`explode*`·`overheadHeight`) · `HazardDestroyedEventsSingleton` · `EffectSpawner.SpawnBlockingHazard` | 확정 | 시한(unit 1) 은퇴 — **문은 「부서짐」 하나**(unit 7·9). 폭발 계기도 그것뿐 |
| 해저드 시전 | 공격과 별개 쿨다운으로 땅을 깐다 | `HazardCastState`/`HazardCastSystem` → `Combat.CastEventsSingleton`(`[UpdateBefore(AttackSystem)]` 로 같은 프레임 소비 보장) | **애매** | ⚠ 「**자는 캐스터가 계속 시전한다**」 — 캐스트가 CC 를 안 본다(`shield-guardian-defender` 계약 7 의 의도). **사용자는 버그로 읽었고 판정 대기**(Play 관측 2026-08-11 · 2026-08-26 재확인, 「후속」) |
| 캐리어 장판 | 장판이 엔티티고, 수명 끝나면 통째로 사라진다 | `TornadoField` · `PortalLink` · `AllyBuffField` + `EffectTickSystem` | 확정 | 멤버십은 스냅샷이 아니라 매 프레임 재판정 — 들어온 적도 걸리고 나간 적은 풀린다 |
| 아군 버프 장판 | 안에 서 있는 아군만, 나가면 곧 풀린다 | `AllyBuffFieldSystem` · `EffectSpawner.AllyBuffApplySec = 0.5` | 확정 | 겹치면 **가장 강한 값으로 못박음**(만료 swap-back 이 순서를 바꿔 승자가 무작위가 되는 것을 차단). duration 은 여기 한 곳에서만 정해진다 |
| 감속장 스냅샷 | 회오리·아군장판과 달리 시전 순간에만 잡는다 | `BattleBridge.ApplySlowField` | **애매** | 백로그 `active-ally-zone` 후속 [M] — 「안에 있는 대상이 영향을 받는다」 원칙의 **마지막 예외** |
| 효과 타일 | 배치 칸 일부가 유닛을 강하게/약하게 만든다 | `Data/EffectTileData.cs` · `BattleBridge.AddEffectTile`/`ApplyEffectTileIfAny`/`ApplyEffectTileOnce`(`EffectTileStackId = 2`, `duration = ∞`, `origin = Tile`) | **애매** | **회수 경로가 없어 엔티티당 1회로 봉인**됨(`defender-relocation` unit 8 — 재배치 시 새 칸 효과는 붙는데 옛 칸이 안 풀린다). 같은 stat 중복 저작은 마지막만 남음(저작 규칙, `EffectTileData` 주석). `EffectTileData.op` 존중을 위해 중앙 헬퍼를 **우회**한다 |
| Squad 카드 버프 | 판이 끝날 때까지 가는 전원 버프 + 신규 배치 상속 | `BattleBridge.Dreamcatcher.cs` `_activeDcEffects` · `DcDuration = 1e9f` · `ApplyActiveDcEffectsTo` · `_dcStackCounter++`(카드 효과마다 새 칸) | 확정 | 회수는 host 사망 시 항등 재발행 + 레지스트리 제거(미래 상속 중단) |
| 배치 오라 | 앞으로 놓을 유닛만 받는다 (host 자신 제외) | `RegisterPlacementAura` · `_activePlacementSleeps` → `ApplyPlacementSleep`(Sleep) | 확정 | `dreamcatcher-placement-aura`(2026-07-10 사용자 확정). 스킬 레이어로 **못 옮긴 둘 중 하나** — 시제가 다르다(지금 실행이 아니라 미래 규칙 등록) |
| 호접몽 | 끝까지 자면 영구 버프, 중간에 맞으면 파탄 | `DreamCocoon`/`DreamCocoonSystem`(`Epsilon = 0.05`) | 확정 | `subconscious-curse-expansion` unit 0 |

---

## ECS 고유라 새 설계에서 개념 자체가 사라지는 것

각 항목은 「사라지는 기계」와 「다른 형태로 살아남아야 하는 규칙」을 짝으로 적는다.

- **`ModifierStatsDirty`(IEnableableComponent)** — 살아남을 규칙: 「슬롯이 바뀐 프레임에만 재계산」. ⚠ **만료도 dirty 를 다시 켜야 한다** — 과거에 dirty 로 쿼리해서 만료가 영원히 안 돌고 모디파이어가 무한 지속된 버그가 있었다(`StatModifierTickSystem` 주석).
- **부여 3채널 + `ModifierApplySystem`**(`StatModifierApplyEventsSingleton` · `StackModifierApplyEventsSingleton` · `DotApplyEventsSingleton`) — 직접 메서드 호출로 바뀌면 큐가 통째로 사라진다. 살아남을 사실: **「부여 시점 ≠ 반영 시점」의 1프레임 비대칭이 현재 밸런스에 구워져 있다**(생산자 11 중 8). 없애는 것도 유지하는 것도 결정이어야지 부작용이면 안 된다.
- **ECB 회피(버퍼 최초 생성은 즉시 `EntityManager`)** — 살아남을 규칙: **같은 드레인 안의 두 번째 이벤트가 첫 슬롯을 덮어쓰면 안 된다.** 순수 C# 리스트에서는 공짜지만, 그 함정이 있었다는 사실이 테스트로 남아야 한다. `MarkDirty` 가 ECB 를 안 쓰는 이유도 같다.
- **`DynamicBuffer.RemoveAtSwapBack`** — 살아남을 규칙: 만료가 슬롯 **순서를 런타임에 뒤섞는다.** 그래서 겹친 장판의 승자를 순회 순서에 맡기면 안 되고 `AllyBuffFieldSystem` 이 「가장 강한 값」으로 못박았다. 리스트로 바뀌어도 같은 규율이 필요하다(`ShieldMath.Absorb` 가 `RemoveAt` 으로 삽입 순서를 지키는 것과 짝).
- **`BattleBridge` static `_stackThresholds` 레지스트리** — Burst 가 managed Dictionary 를 못 읽어 `StackModifierTickSystem` 이 통째로 비-Burst 가 된 우회. 살아남을 규칙: 스택 임계 규칙은 **데이터가 소유**하고 시뮬은 키로만 조회한다. (백로그의 `IStackThresholdRegistry` 테스트 주입 안이 이 자리)
- **`Entity`(version 포함)를 실드/모디파이어 출처 키로 쓰는 것** — 살아남을 규칙: 부여자가 죽어도 실드는 남아야 하고, **출처 키가 재활용된 id 와 충돌하면 안 된다**(`ShieldSlot` 주석이 명시). 순수 C# 에서는 안정 ID 를 따로 발급해야 한다.
- **`HazardSingleton.cellToEffects` 멀티해시** — 이미 은퇴했다(연속 원 판정). 남은 규칙: 겹친 동일 효과 존의 적용 순서에 결과를 맡기지 않는다.
- **`EffectSpawner.AllyBuffApplySec = 0.5`** — 기준이 Unity ProjectSettings 의 `Maximum Allowed Timestep`(0.3333) 이다. 살아남을 규칙: **재발행 지속시간 > 최대 프레임(틱) 델타**. 고정 틱으로 가면 값의 근거 자체가 바뀌므로 상수를 그대로 베끼면 안 된다.
- **Burst 함정 3종**(`OnUpdate` 안 지역 `GetComponentLookup` 금지 / 소비처 0 이어도 필드 존치 / 에디터 캐시 BC1055) — 개념째 소멸. `HazardCastSystem`·`ZoneApplySystem`·`AllyBuffFieldSystem` 세 파일의 긴 경고 주석이 전부 이 축이다.
- **맥락 경계 강제 채널**(`CcClearRequestsSingleton` = Units→Effects) — 단일 프로세스 순수 C# 에서는 직접 호출로 접힌다. 살아남을 규칙: **피격 기상은 피해 적용 직후 같은 틱**이어야 한다(다음 프레임 지연 금지).

---

## 코드에만 박혀 있고 문서에 없는 규칙 (rebuild 가 놓치기 쉬운 것)

- `_dcStackCounter` 는 `BeginPlacement` 에서 100 으로 **리셋**(`BattleBridge.cs:1557`)되지만 `_dcHandleCounter` 는 **앱 수명 monotonic**(의도적 비대칭 — stale handle alias 방지, 주석에 근거).
- 스택 슬롯은 `origin` 을 **안 싣는다**(항상 `Unspecified`). Stat 슬롯(4축 + origin)과의 의도된 비대칭이고, 그래서 스택은 오라 판정 대상이 아니다.
- `StructureTag`(거점)는 `ApplyStat` · `ApplyStack` · `ApplyCc` **셋 다에서 거절**된다 = 거점은 상태이상·모디파이어 전면 면역. 어느 spec 에도 계약으로 안 적혀 있다.
- `magnitudeCap` 클램프는 **기존 슬롯 갱신 경로에만** 있다. 신규 슬롯 생성 2경로에는 없다(주석은 「상한이 항상 1회분 이상이라 무의미」로 설명하지만, 그 전제가 깨지면 조용히 새는 자리).
- `regenPerSec` 결합식이 `(0 + Σadd) × Πmul` — **곱셈 슬롯만 있으면 결과가 0**. 사실상 Additive 전용 스탯이고 `EffectTileData` 툴팁만 이걸 귀띔한다.
- `CcEffectMerge`/`DotEffectMerge` 의 **tickTimer 비례 환산** — 주기가 바뀌면 「다음 틱까지 진행률」을 새 주기로 환산한다(큰 주기에서 쌓인 타이머가 작은 주기로 넘어가 조기 발동하는 것 방지).
- **첫 틱 즉발 규약** — 신규 슬롯은 `tickTimer = tickInterval` 로 시작해 진입 즉시 1회 준다. 생산자 3곳이 전부 이 규약에 기댄다.
- `DotApplySystem` 은 **지급을 정방향, 만료 제거를 역방향**으로 돈다 — 역순 지급이면 여러 도트가 걸린 대상의 데미지 숫자 표시 순서가 조용히 뒤집힌다.
- `DotApplySystem` 이 job 두 벌을 일부러 안 합친다 — 기본값 `NativeQueue.ParallelWriter` 가 스케줄 안전성 검사에 걸리기 때문(ECS 고유 이유, 순수 C# 에선 즉시 접힌다).
- `HeatMath.Delta`: 회복은 **오버힐을 잘라내고**(만피 유닛 VFX 스팸 방지), 과열은 **HP 1 바닥**(열기는 사망 원인이 될 수 없다).
- `BlockingHazardSO`: `maxHp / healthDecayPerSec` = **아무도 안 때렸을 때의 수명(초)** 이 저작 감각이다. 주석에만 있다.
- `explodeDamage > 0` 인데 `explodeProjectile` 미배선이면 **탄 0번의 비주얼이 한 프레임 번쩍인다**(경고 로그만, 차단 안 함).
- `ThresholdRule[]` 은 `atStack` **오름차순 가정**이고 검증 코드가 없다(저작자 책임). `Consume` 모드는 발화 후 `lastTriggeredStack` 을 **차감된 최종 stackCount** 로 맞춘다.
- `StackModifierSO.DefaultMaxStack = 5` 는 미등록 `StackKind` 의 폴백이고, 여러 producer 가 이 기본값을 복사해 쓰던 것을 SO 로 수렴시킨 것이다.
- `HazardEffect.targetTraversalLayers` 는 `[NonSerialized]` **런타임 스냅샷**이다 — 저작은 항상 0 이고 `EffectSpawner.SpawnHazard` 가 덮어쓴다. 0 = 필터 없음(레거시).
- `HazardEffect.element` 는 저작하지만 `origin` 은 **저작하지 않는다** — 해저드가 만들면 언제나 `DotOrigin.Zone`.
- 존의 지속시간(`restDuration`)은 장판 위에서 **매 프레임 갱신**된다. 그래서 저작값 0.2s 는 「나가면 0.2초 뒤 꺼진다」는 뜻이지 총 지속이 아니다.
- `Hazard.radiusTiles < 0` = 존 효과 없음. 모양→반경 매핑은 `SingleCell→0 · Square3x3→1 · RadiusSquare→max(1,radius)`.
- `ApplyEffectTileOnce` 의 가드(`_effectTileAppliedEntities`)는 on-place 가드(`_onPlaceTriggeredEntities`)와 **공유하면 안 된다** — 재배치가 on-place 를 재무장하기 때문. 효과 타일이 없는 칸에 배치돼도 마킹한다.
- `ShieldMath.ValueFromSource` 는 「기존값 ≥ amount 면 Merge 가 max 로 no-op」이라 **재부여/VFX 를 스킵**하는 용도다(헛발동 방지).

---

## 이 영역에서 rebuild 가 결정해야 할 열린 질문 (5)

1. **비율 합성 규약을 갈아엎나.** 현행은 float `(1+Σadd)×Πmul`, 엔진 비의존 스펙(`docs/spec/unit-stats-and-modifiers/`)은 고정소수점 scale 1000 + 「가산 후 1회 승산」 + `FixedMath.Div` 단일 관문 + 소스 ID 정렬을 요구하며 스스로 「가장 큰 변경」이라 못박았다. 바꾸면 **모든 저작 수치가 재조정 대상**이고, 안 바꾸면 그 스펙 두 편이 죽은 문서가 된다. 그 문서는 `Override` 도 **도입하지 않는다**(YAGNI)고 명시했는데 현행에는 있다.
2. **회수 모델: 항등 덮어쓰기인가 슬롯 삭제인가.** 지금은 중화(identity re-emit)라서 ① Override 에 항등이 없고 ② 상한을 실으면 지우기가 조용히 실패하며 ③ 효과 타일은 회수가 없어 「엔티티당 1회」로 봉인돼 있다. 슬롯 삭제(dispel 채널)로 가면 셋이 한 번에 풀리지만 「어느 슬롯을 지우나」 키 설계와 `CombineOp` 별 면역 정책이 선행한다.
3. **1프레임 지연 비대칭을 계승하나.** 생산자 11 중 8 이 다음 프레임에 반영된다. `battle-sim-extraction` 이 「M1 의 몫」으로 명시 보류했고, 고정 틱 순수 C# 에서는 「즉시 반영」이 자연스러운 기본값이라 **가만히 두면 바뀐다**. 골든 코퍼스 A/B 가 이 축을 관측하는지도 함께 확인해야 한다.
4. **슬롯 식별자 체계.** 손으로 분양하는 전역 `stackId` 번호판(0 / 은퇴 1 / 2 / 3 / 100+ / 100+kind)을 유지할지, Mono 스펙의 「소스 ID 로 정렬 후 합산」으로 갈지. 후자로 가면 결정성의 근거가 **삽입 순서에서 정렬로** 옮겨가고, 은퇴 슬롯 번호 보존 제약(`SkillModifierOrigin` 숫자 캐스트)도 재검토 대상이 된다.
5. **스택 임계 규칙의 소유 단위.** 지금은 `StackKind` 당 전역 한 벌이라 드래곤과 킨들러가 불 스택 규칙을 물리적으로 공유한다(한쪽 튜닝이 다른 쪽을 끌고 간다 — 실측 사례 있음). `DotOrigin` 이 2축으로 푼 것과 **같은 결의 문제**이고, 출처별 오버라이드를 열지 말지를 지금 정해야 데이터 스키마가 굳는다.

---

## 요청서 대비 정정 2건

- **`HazardSingleton` cell→effects 멀티해시(겹칠 때 비결정 순회)는 이미 은퇴했다.** `distance-based-range` unit 19 가 존 판정을 셀 해시에서 **연속 원**(해저드 스냅샷 × 피해자 몸)으로 옮겼고, `HazardLifetimeSystem` 에는 수명 틱만 남았다. 리포 전체 grep 결과 `HazardSingleton`·`cellToEffects` **0건**. `HazardCellsBuffer` 는 검사/뷰용으로만 존치.
- **`CcKind` 에 Knockback/Root 라는 이름은 없다.** 넉백은 `Impulse`, Root 는 **존재하지 않는다** — 전면 정지가 필요해지면 `moveSpeedMul → 0` 이 아니라 전용 이동 플래그로 만들라는 규정이 `ModifierStatsAggregateSystem` 에 박혀 있다(그래야 바닥 클램프 0.15 에 안 걸린다).
