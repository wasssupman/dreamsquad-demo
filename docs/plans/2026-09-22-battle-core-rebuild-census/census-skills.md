# 스킬·드림캐쳐·기믹 — 키워드 census

> 조사 범위: `Assets/_Project/Scripts/Skills/`(도메인 전량) · `Battle/Skills/`(seam·어댑터) ·
> `Data/Dreamcatcher/` · `Core/Dreamcatcher/` · `Bridge/BattleBridge.Dreamcatcher.cs` ·
> `Battle/Effects/`(기믹) + 관련 spec 12개 · `docs/reference/battle-core-architecture.md` §1.10~1.14 · §5.2~5.3 · §8.
> 모든 포인터는 grep 으로 존재 확인함.

## ⚠ 지시문 수치 정정 2건

- **concrete 는 58개가 아니라 34개**다. `Skills/Concrete/` 29파일에 `sealed class … : ISkill` 34개 + abstract base 2개(`SelfStatBuffSkillBase`, `StatAuraSkill`). `Id` 는 1~34 연속이고 `SkillRegistry` 가 중복 등록을 던진다.
- **`SimIntent` 는 14종이 아니라 24종**이다. 별도로 `MetaIntentKind` 2종(`GainCost`·`ReduceSkillCooldown`)이 있고 이쪽은 **큐를 안 탄다**(Mono 자원이라 즉시 반영 — 큐에 실으면 코스트 획득이 한 프레임 늦는다).
- `SkillSeam` 은 지시문대로 **7 + `None`(=0)** 이 맞다. 다만 이름이 `Attack, Periodic, Threshold, Death, Lifecycle, Cast, Immediate` 이고 **번호 순서는 `Periodic`=1, `Attack`=2, `Threshold`=3, `Death`=4, `Lifecycle`=5, `Immediate`=6, `Cast`=7** 이다.

---

## 1. 키워드 표 (62행)

| 키워드 | 한 줄 정의 (게임 언어) | 현행 구현 포인터 | 판정 | 애매하면: 결정 이력 + 미결 요지 |
|---|---|---|---|---|
| 「트리거 → 발동」 레일 | 무슨 일이 일어나면 무엇이 터지나. 카드·적 악몽·배치 스킬·가디언 캐스트·액티브·퇴근이 **같은 레일** | `DcMechanic` → `DcTriggerSlot` → `SkillFiredEvent` → `ISkill` | 확정 | §1.11 「한 어휘, 다섯 사용자」 |
| `ISkill` | 스킬 하나 = concrete 하나. 진영도 host 종류도 안 갖는다 | `Skills/ISkill.cs` | 확정 | — |
| 호출자 = 소유자 | `Execute` 를 부른 쪽이 그 스킬의 주인. 보스 스킬을 방어유닛이 부르면 코드 0줄로 반대편을 겨눈다 | `Skills/CasterRef.cs` `OfUnit`/`Player` | 확정 | foundation 계약 4. 검증 질문이 「방어유닛이 BossLeap 을 장착하면 상대 진영 밀집 셀로 도약하는가」였다 |
| 무상태 concrete | 스킬은 **개시와 수치**까지. 진행형(도약 비행·수면 완주·시한부·궤도탄)은 개체의 상태 | foundation 계약 5 | 확정 | 08-11 「ISkill 기각」의 실근거 3종 중 하나를 이 계약이 무력화 |
| 도메인은 ECS 를 모른다 | `Wassup.Skills` asmdef 가 Entities 미참조 → **컴파일러가 강제** | `Skills/Wassup.Skills.asmdef` | 확정 | 불변식 3. 허용되는 유일한 외부 타입은 `Unity.Mathematics` 값 타입 |
| `SkillRegistry` | skillId → concrete. 미등록은 조용한 no-op 이 아니라 loud 거절 | `Skills/SkillRegistry.cs` | 확정 | `RegisteredIds` 로 완전성 테스트 |
| `NotRouted = 0` | 「스킬이 아니다」 | `SkillRegistry.NotRouted` | 확정 | ⚠ **이전 도중 뜻이 뒤집혔다**(구 `LegacyArmId` = 「아직 arm 이 처리」 → 「아무도 처리 안 함」). 그래서 `OnPlace × NextAttackDoubleFire` 가 조용히 죽어 있었고 EditMode 는 전부 초록이었다 — migration unit 8 |
| `SkillPayloadPolicy` | 「이 페이로드는 스킬인가」의 단일 정본. 어휘 밖 **7종** | `Data/Dreamcatcher/SkillPayloadPolicy.cs` | 확정 | 이유가 일곱 다 다르다: 센티넬 · 발동규칙 · 자기참조 · 다른배선 · 손패UI · 이관됨 · 죽은값 |
| 어휘 밖 2종 (발동 규칙 ↔ 자기참조) | `PlacementAura` = **시제**가 다름(지금 실행이 아니라 미래 배치에 적용될 규칙 등록) · `HeavyStrike` = **자기참조**(자기를 부른 공격 사건 자체를 바꿈, 스킬 발화점은 정의상 그 뒤) | 같은 파일 | 확정 | 나머지 5종과 **뭉뚱그리면 다음 후보를 잘못 분류한다** — migration README 가 반복 경고 |
| `DcSkillRouting.SkillIdFor` | (트리거, 페이로드) → skillId. **bake 와 범위 프리뷰가 같은 함수** | `Core/Dreamcatcher/DcSkillRouting.cs` | 확정 | 불변식 15 — 미러 두 벌이면 「붙는데 무효」가 돌아온다 |
| 트리거가 라우팅을 가르는 경우 | 죽음·처치·퇴근 계열 광역 = 「실려 온 자리」(`DeathSiteBlastSkill`) / 산 계열 = 「내 발밑」(`SelfAreaBlastSkill`) / 경계 자기버프 = 출처가 다름(`ThresholdSelfBuffSkill`) | 같은 파일 상단 분기 | 확정 | `ForPayload` = 트리거 무관 표. **위치를 잘못 두면 그 트리거 밖 조합이 조용히 죽는다** |
| `SkillSeam` | 발화 지점의 이름. `None(0)` + 7종 | `Battle/Skills/SkillDispatchSeams.cs` | 확정 | **정본은 enum 이지 문서 숫자가 아니다.** 토대가 「3」이라 적은 것은 그때 센 값이고 상한이 아니었다 |
| seam 생성 규칙 | 감지자가 다른 프레임 창을 가지면 seam 이 따로 난다 → 단일 드레인은 **산술적으로 불가능** | foundation 계약 7 | 확정 | 계약에 개수를 적지 않는다 — 개수는 코드가 소유 |
| 이벤트가 자기 seam 을 말한다 | 큐는 1개, 남의 seam 것은 꼬리로 되돌림. `budget = queue.Count` 스냅샷이 종료 보장 | `SkillDispatchSystem.cs:138,199,216` | 확정 | 예전엔 소유가 시스템 순서에서 **창발**해 ① 자기 죽음 이벤트를 경계 seam 이 집어가고 ② 시뮬 밖 생산자(퇴근)는 seam 을 고를 방법이 없었다 |
| `SkillFiredEvent` 값 스냅샷 | 발화한 쪽이 그 순간의 값을 싣는다 — 드레인 시점 재질의 가능 건수 **0**(unit 0 전수 실측) | `Battle/Skills/SkillFiredEvents.cs` | 확정 | 불변식 5. `bestTarget` 은 9단계 오버라이드 합성물이라 재현 불가 |
| 자리↔몸 **짝** | `FiredPosition↔CasterBodyRadius` · `TargetPosition↔EventBodyRadius`. **0 = 그 자리는 칸** | 같은 struct | 확정 | 단일 필드면 시체폭발이 **킬러의 몸**으로 적 시체 위에서 터진다 — `distance-based-range` unit 23b |
| `CasterFaction` | 시전자 진영도 값이다. 파괴 뒤 seam 은 엔티티에서 못 읽는다 | 같은 struct | 확정 | 종전 폴백이 「플레이어 = 방어유닛 편」이라 **적의 작별 선물이 적을 때렸다**. unit 8 이 `OnDeath` 를 적에게 열기 **전에** 이 축을 먼저 실었다 |
| `TargetTraversalLayers` | killer 의 통행 층 스냅샷. 0 으로 새면 **무제한 통과** | `SkillParams` · `Concrete/DeathSiteHazardSkill.cs` | 확정 | fail-closed — 「0 = 안 깐다」 |
| `RequiresLiveCaster` | 드레인 때 시전자가 살아 있어야 하나. **Lifecycle seam 에서만 false** | `SkillDispatchSeams.cs` ⑤ | 확정 | 기본 가드를 두면 작별 선물이 **매번** 버려진다(실제로 그랬다) |
| `ISkillContext` | 질의(자리·정체·후보·격자 판단·조준 필요) + `Emit` 2개. `UnitPredicate` 8종 | `Skills/ISkillContext.cs` | 확정 | 술어: Alive · PendingDeployment · InUltimateLeap · HasShieldBuffer · HasAggroCapacity · IsPathFollowing · CanReceiveDamage · HasPosition |
| `ctx.Emit` 만 | concrete 는 상태를 안 바꾼다. 적용은 어댑터가 소유 맥락 채널로 | `Battle/Skills/EcsSkillContext.cs` | 확정 | 계약 3 |
| 직접 쓰기 폐쇄 목록 | ECB 가 구조적으로 표현 못 하는 4건: `DelaySelfAttack`(max 로 읽고-고쳐-쓰기) · `ScaleKillReward`(처치 이벤트가 enqueue 시점에 값 복사) · `BeginDreamCocoon` 감시자 부착(잠과 **원자**) · `EmitPattern` 의 `PatternSlot` 요소 대입(durable 버퍼 덮어쓰기) | `EcsSkillContext.cs:646,806,836,1015` | **애매** | foundation README 본문은 아직 「**예외 3건**」인데 같은 문서의 표는 **4행**이고 `battle-core-architecture` §8-3 은 「**4건**」 — 문서 두 곳이 갈려 있다 |
| ECB 스테이징 + seam 재생 | 원자 동시 부착(궁극기 = 잠금+무적)이 필요해 열린 둘째 갈래. 어댑터는 담기만, 재생은 디스패처가 자기 seam 안에서 | 계약 3 | 확정 | 개정 전 문면은 「enqueue/append **만**」이었고 코드가 건전한데 계약이 못 따라온 경우라 계약을 고쳤다 |
| 캐리어 생성 셋 | `SpawnTornadoField`·`SpawnPortal`·`SpawnAllyBuffField` 는 즉시 `CreateEntity`(뷰 등록부가 매 프레임 맞춰 ECB 지연 불가) | `Battle/Effects/EffectSpawner` | 확정 | 「직접 쓰기」가 아니라 **구조 변경** — 두 축을 섞지 말 것(디스패처의 프레임당 lookup 스냅샷을 건드리는 쪽은 이쪽) |
| `SimIntentKind` 24 + `MetaIntentKind` 2 | 스킬이 방출하는 의도 어휘 | `Skills/SkillIntent.cs` | 확정 | 아래 §3 전량 |
| `SkillFieldKind` | `SpawnFieldCarrier` 안의 3갈래(AllyBuff·Pull·Portal) — **읽는 필드가 종류마다 다름** | 같은 파일 | 확정 | 한 의도로 묶은 근거 = 셋이 같은 문장(「저기에 얼마 동안 장을 둔다」) |
| `SkillTarget` | 대상 축(유닛 / CellA / CellB / **발사 시점 방향**) | `Skills/ISkill.cs` | 확정 | `DirectionXZ` 재계산 금지 — 드레인 시점엔 둘 다 움직였다. 「입구==출구 거절」은 arm 이 아니라 **창구 규칙** |
| `SkillParams` | 슬롯 스칼라 위에 씌우는 **이름 붙은 뷰**. `-1 = 없음` 축 3개 | `Skills/SkillParams.cs` | 확정 | 원 슬롯의 `tileRange` 겸직 **13가지 의미**를 의도적으로 물려받지 않는다 |
| `SkillEntityId` / `SimEntityId` | 도메인 핸들 ↔ 스폰 순번 ID(프로세스 밖으로 나가는 유일한 축) | `Skills/SkillEntityId.cs` · 불변식 17 | **애매** | ⚠ `SimEntityId` 없는 시전자 = **스킬 레이어 전면 침묵**(감지도 되고 concrete 도 불리는데 모든 질의가 빈손 → `ExecutedCount` 로도 안 보임). **스폰 지점 발급 범위가 스킬 보유 아키타입을 전부 덮는지 미확인** — migration README 잔여 리스크 |
| 풀 밖 엔티티 | 어댑터는 `AttackUnitTag`/`DefenderUnitTag` 두 풀에서만 핸들 역변환 | migration README | **애매** | 태그 없으면 핸들은 만들어지는데 역변환이 실패해 **효과가 조용히 사라진다**(unit 3a 에서 실제로 밟음). `BuildCaster` 엔 경고가 있지만 `BuildTarget` 엔 **없다** |
| 후보 상한 64 | 「가까운 64」가 아니라 **풀 순서 선착 64**(legacy `AuraPulse` 는 무상한이었다) | migration README | **애매** | 반경 안 후보가 64를 넘는 판이 실제로 생기면 그때 잘림 규칙을 계약으로 — 미결 |
| 감지자 8곳 | AttackSystem · BossPeriodicTrigger · HealthThreshold · DamageApplication · UnitLifecycle · HazardCast · 브리지(퇴근) · 브리지(부착·액티브) | §5.2 발화 경로 · `grep "Seam = SkillSeam"` 13개소 | 확정 | 「감지는 분산, 실행은 단일」 — 불변식 4 |
| `DcTrigger` 순수 함수 5 | `Tick`(N번째) · `WouldFire`(비파괴 peek) · `PeriodicTick`(잔여 이월) · `HealthThresholdEval`(단조 래치 k) · `GatePass` | `Battle/Combat/DcTrigger.cs` | 확정 | 경계 래치는 회복으로 되감기지 않는다(핑퐁 익스플로잇 차단). 한 틱 다중 경계 관통 = **1회 보고** |
| `HasDetector` | 「이 조합을 잡는 감지자가 있나」. 진영 화이트리스트 2술어가 여기로 접혔다 | `DcTrigger.cs` 하단 | 확정 | `OnPlace`/`OnRetire` 만 방어유닛 전용 — **적에겐 사건 자체가 없다**(열고 말고의 문제가 아님). fail-closed 유지 |
| 카드 화이트리스트 은퇴 | 두 벌로 두는 것 자체가 위험 | migration unit 8 | 확정 | 걷힌 것: 자기진영 타격 방지 술어 2 · 「방어유닛 전용」 하드코딩 3(강공 pre-scan · 자기 죽음 루프 · 투사체 splash/bounce 풀) |
| `DcTriggerKind` (10) | §2 전량. **append-only**(시트가 enum **값**으로 왕복) | `Data/Dreamcatcher/DcMechanic.cs` | 확정 | migration 계약 4 |
| `DcPayloadKind` (33) | §2 전량. append-only | 같은 파일 | 확정 | — |
| 트리거 게이트 축 | 사건(edge) × 동적 술어(level)의 직교 조합. `HpBelow` × (Self / EventTarget). **조합은 데이터, 어휘만 코드** | `DcTriggerSpec.gate/gateSubject/gateValue` · `DcTrigger.GatePass` | **애매** | `GateComboSupported` 가 **2조합만** 개방(궁지폭발 = OnDamagedN×Self · 처형타 = AttackN×EventTarget), 나머지 gate≠None 은 bake loud 거절. 미개방 조합 5종 + 복수 게이트 ∧ + Mono 상태 게이트가 backlog — `dreamcatcher-trigger-gates` |
| 카운트 게이트 | `if(Pass){ if(Tick()) fire; }` — 게이트 실패 사건은 counter 무변화. 회복 시 counter 유지 | 같은 spec 계약 | 확정 | `HeavyStrike` 합성 불변식: pre-scan(`WouldFire ∧ Pass`)과 counter 루프가 **같은 프레임·같은 subject·같은 pre-damage HP** |
| `DcTriggerSlot` | 부착된 규칙 하나의 unmanaged 형태. `instanceId` 로 같은 카드 2장이 독립 카운터 | `Battle/Combat/DcTriggerSlot.cs` | **애매** | `tileRange` 가 **7~13가지 뜻을 겸직**(AoE 반경·궤도 반경·maxStack·피해감소%·폴백 반경·착지 링 상한·실드 0=자기만…). 저작 툴팁·주석은 아직 "Chebyshev" 라 **저작자가 믿고 값을 정하면 어긋난다**(backlog [중]) |
| 슬롯 쓰기 소유권 | `counter` = AttackSystem 전용 · `elapsed/fireCount` = BossPeriodicTrigger · `nextBoundaryIndex` = HealthThreshold | 같은 파일 주석 | 확정 | 맥락 경계(제약 2)의 슬롯 내부판 |
| `BakeUnitMechanics` | **진영 중립** 단일 bake. 적 `nightmareMechanics` · 방어유닛 `UnitSkillAbility.mechanics` · 가디언 해저드/실드 캐스트 · 액티브 · 퇴근 페이로드가 전부 같은 `DcTriggerSlot` 을 굽고 같은 라우팅 표를 쓴다 | `BattleBridge.cs:10139`(호출부 :8686 방어유닛 · :10126 적) | 확정 | §5.2 「같은 레일 위의 비-카드 사용자」 |
| bake 침묵 금지 게이트 3 | ① 감지자 없음 ② 부착 전용 payload 를 트리거에 매닮 ③ 스킬인데 라우팅 없음 → 전부 skip + 경고 | `BattleBridge.cs:10203~10236` | 확정 | 「침묵보다 거절이 낫다」. ②는 **면제가 아니라 거절 사유**(unit 8 리뷰 H-2) |
| `DamagedCounter` | 피격 N회는 **Units 소유 버퍼**에서 완결 — `DcTriggerSlot` 로 통합하지 않는다 | `Battle/Units/DamagedCounter.cs` | 확정 | critic CRITICAL 반영(피격 쓰기 = Units 맥락). payload 필드만 위드닝 |
| `DcAttackModSlot` / `DcAttackModKind` | 항시 켜진 공격 수식자 3종(`ProjectileBounce`·`FrontmostTarget`·`DamageVsSleeping`) | `Battle/Combat/DcAttackModSlot.cs` · `DcMechanic.cs:426` | 확정 | **스킬이 아니다** — 이번 공격의 출력 조립에 곱·합으로 참여 |
| `FrontmostAttackLock` | 「맨 앞을 계속 문다」 지속 락 + `damageMulSnapshot` | `Battle/Combat/FrontmostAttackLock.cs` · 부착은 `BattleBridge.Dreamcatcher.cs:1080` | 확정 | 같은 이유로 스킬 어휘 밖 |
| 판별 기준(스킬인가) | 이번 공격의 **출력 숫자/타이밍 조립에 참여** = 밖 / **별도 대상·캐리어·채널로 나감** = 안 | foundation 결정 기록 | 확정 | ⚠ 경계 정정: `OnDamagedN × NextAttackDoubleFire` 는 **charge 부여까지 안 / charge 의 소비는 `AttackSystem` 내부라 밖** |
| `DreamcatcherCard` | type + `mechanics[]` + `attackMods[]` + `effects[]` + `skill`(Active) + 부착 제한 + 비용 + 유출 허용치 + `visible` | `Data/Dreamcatcher/DreamcatcherCard.cs` | 확정 | 필드 상세 정본은 `docs/reference/dreamcatcher-card-schema.md` |
| `CardType` (Squad/Unit/Active) | Squad = 매치 지속 스탯 배율 / Unit = host 규칙 슬롯 / Active = 타일 조준 스킬(쿨다운) | 같은 파일 | 확정 | 덱 상한이 여기 키잉(Squad ≤2). 구 `CardBinding` 은 제거됨 |
| `CardCategory` | Normal / Unique / Subconscious — **순수 시각 라벨**(보라 프레임·"무의식" 칩) | 같은 파일 | 확정 | 덱 **규칙** 소비처는 0(림의 선물 폐지로 마지막 규칙 소비처 소멸). dormant 는 아니다 |
| `CardTargetAxis` / `CardBuffKind` | Squad 카드의 수혜 축(ClassRanger·ClassGuardian·Cost1·All) / 버프 종류(AttackDamage·AttackSpeed·EffectiveHealth·MoveSpeed·CostRate·DamageVsCc) | 같은 파일 | 확정 | `CostRate` 는 StatModifier 매핑 없이 `CostRuntime` 이 통째 소비. `DamageVsCc` 에 **둔화(Slow)는 해당 없음**(CcEffect 가 아니라 StatModifier) |
| Squad 카드는 sim 에 없다 | ECS 엔티티·슬롯 0. 브리지 `_activeDcEffects` + `StatModifierApplyEvent`(지속 1e9). 신규 배치 유닛에 `ApplyActiveDcEffectsTo` 로 상속, 철회 = 배율 1.0 재적용(중화) | §5.2 표 | 확정 | — |
| `Active` = `SkillData` 래퍼 | 저작 효과 **6갈래 → concrete 5종**(스탯 버스트 · 당김장 · 메테오 · **아군 장판[효과 2갈래가 공유]** · 포탈 2셀) | `BattleBridge.CastSkillAtTile` / `CastActiveSkillAtTile`(`:3021~`) | 확정 | `Caster = Entity.Null`, 진영은 디스패처가 `CasterRef.Player` 로 접는다 |
| Immediate seam | 부착·액티브는 **동기 트랜잭션** — 브리지가 자기 콜스택에서 `Update()` 를 직접 부른다. **자기 순서를 갖지 않는다** | `SkillDispatchSeams.cs` ⑥ | 확정 | 큐에 넣고 프레임을 기다리면 **소모(차감·쿨다운) 뒤에 실행이 도착한다** |
| `trigger == None` 카드 3 | 마지막 불꽃(`SelfBuffLethal`) · 호접몽(`DreamCocoon`) · 살찌운 제물(`BountyMark`) — 슬롯 없이 부착 즉발 | `SkillPayloadPolicy.OnlyValidWithNoTrigger` | 확정 | 트리거에 매달면 bake 가 거절 |
| `DcApplicability` | 「이 host 에 붙을 수 있나」. UI preflight(`DreamcatcherAttachEval`)와 커밋 bake 가 **같은 함수** | `Core/Dreamcatcher/DcApplicability.cs` | 확정 | host **종속** 조건만 — 「magnitude ≤ 0」처럼 어느 host 에서나 답이 같은 것은 이 층 밖 |
| `DcHostArchetype` / `DcProjectileRoute` / `DcRejectReason` | Standard·FacingVolley·BombThrow·HazardCast / None·Homing·Ballistic·Directional·Grenade / 거절 사유 8종(`Unclassified` 포함) | 같은 파일 | 확정 | ⚠ 경로는 탄 SO 선언이 아니라 **그 host 가 실제로 타는 길**(Projectile_Bomb 은 flightMode 0 인데 폭탄맨은 `GrenadeToCell`) |
| `DcRangeCatalog.Resolve` | skillId × 저작 반경 × **트리거** → 도형·반경. 판정과 표기가 **같은 함수**(`SkillMath.TryOriginRadius`)를 부른다 | `Core/Dreamcatcher/DcRangeCatalog.cs` | 확정 | fail-closed — 모르는 concrete 는 None. **트리거가 인자인 이유**: 같은 concrete 가 트리거에 따라 다른 자리에서 터진다. 새 concrete 가 조용히 None 되는 감지 테스트가 backlog |
| `DreamcatcherCycleDeck` | 판 덱 12 = 저장 덱 10 + **공용 액티브 2**(전원 동일). 매치 시드 **raw** 로 Fisher-Yates **1회**(생성자가 전담) | `Core/Dreamcatcher/DreamcatcherCycleDeck.cs` · `DreamcatcherHandController.cs:127` | 확정 | 손패 = 큐 앞 N 의 뷰 |
| 부착 상한 | 유닛당 `MaxAttachPerUnit`(설정 SO, 기본 3). 판정 = `CountAttachedTo(host)` 전수. **시뮬은 모른다** | `DreamcatcherHandController.cs:522` | 확정 | 판 규칙이 아니라 손패 규칙(§5.2) |
| 카드 순환 | host 소멸 → 큐 **뒤** / 액티브 사용 → 즉시 뒤 / 인수인계 있으면 그 유닛의 **다른** 카드는 큐 **앞** / **실패한 부착은 차감·순환 없음** | §5.2 자원표 | 확정 | — |
| 인수인계(`RecallAttachedToFront`) | 퇴근할 때 같이 붙어 있던 **다른** 카드를 부착 순서대로 큐 앞으로(자기 자신은 맨 뒤) | `DcPayloadKind 25` · `DcPayloadKinds.IsHandOp` | 확정 | ★ **실행자가 sim 도 브리지도 아닌 Mono 컨트롤러** — `DcTriggerSlot` 을 안 굽는다. `magnitude` **소비자 0**(주석은 「상한」이라 적었지만 아무도 안 읽는다) |
| 표식 카드 회수(`BountyMark`) | 적을 겨냥하는 최초의 드림캐쳐. 처치 = 배율 보상 + 회수 / 유출 = 무보상 회수 | `Skills/Concrete/BountyMarkSkill.cs`(Id 27) | 확정 | 각성 배율은 baked 값 덮어쓰기, **마음 회복은 SO 원값**(두 축 겸직 금지) |
| 각성 게이지 소스 | 적 처치(`EnemyKilledEvent.awakeningReward`, 표식 배율 baked) · 아군 사망(`DefenderUnitData.awakeningReward`) · **퇴근 0** · 액티브 사용 = 비용 차감 | §5.2 자원표 | 확정 | 초과 소멸, 시간 충전 없음. 퇴근에 주면 배치↔퇴근 파밍 |
| 퇴근은 sim 사건이 아니다 | `DeadTag` 없이 `DestroyEntity` — 사직서·작별 선물·각성 지급을 **배제 코드 0줄**로 막는다 | `BattleBridge.RetireDefender`(seam enqueue `:4387`) | 확정 | 불변식 11. 되돌릴 수 없는 sim 변경을 뷰 처리보다 **먼저** 끝낸다 |
| `OnPlace` 배치 스킬 | 브리지가 배치 확정 시 `JustDeployed` 태그 → **Periodic seam** 에서 같은 프레임 발화·태그 제거 | `BossPeriodicTriggerSystem.cs:33,106,121,178` | 확정 | ⚠ 배치 트리거가 **주기 seam 을 탄다**(시스템 이름과 어긋남) |
| 캐논 1:1 융단폭격 | 미사일 1발 = 적 1기 | `on-place-skill-rework` units 9~11 (`8995140e`) | 확정 | 「한 칸에 몇 발」은 발사가 아니라 **착탄의 성질**. 원인은 상수가 아니라 구조(탄 하나에 조준이 둘) |
| 배스티온 집단 도발 | `AreaTaunt` — 반경 내 적 전원을 duration 초 host 에게. host 는 `AggroCapacity` 보유 필수 | `Skills/Concrete/AreaTauntSkill.cs`(Id 8) | 확정 | 게이트 판정은 `AggroStateSystem`(Effects)이 전부 소유 — 복제하면 둘이 갈린다. **통행 층 게이트만은 concrete 에서** 걸어야 근접 가디언이 하늘의 적을 안 끈다 |
| 배치 스킬의 방향 축 | 실드셔틀·샷건맨이 「방향으로 쏘는」 배치 스킬을 개통 | `on-place-shuttle-shotgun`(완료 2026-08-19) | 확정 | 폭탄맨·전방관통 4종이 이 위에 올라탄다 |
| `PlacementAura` | 앞으로 배치될 유닛에 적용될 **규칙 등록**. 등록·조회·해지 3시점이라 영수증 필요 | `BattleBridge.RegisterPlacementAura` → `RevokeDreamcatcherEffects` | 확정 | 스킬 레이어로 **못 옮긴 이유 = 시제**(포트의 결함이 아니라 범주가 다르다). 후속 후보: 「출처 사망 시 모디파이어 회수」가 생기면 영수증이 불필요해진다 |
| 시즌 기믹 4 | 과로(레드불) · 번아웃 · 사직서 · 온천. 판 전체에 얹히는 규칙 | `Data/Gimmick/{RedBull,Burnout,ClockOut,Onsen}GimmickData.cs` + 전용 시스템 7 | 확정 | **스킬 정의에서 정의 수준으로 제외**(foundation 결정 기록). ⚠ critic 이 앞 둘을 「과로」 하나로 묶었으나 `BattleConfig` 활성화 단위로는 **독립 2개** |
| 기믹 self-gate | `BattleConfig.gimmickPool` → `BattleBridge.CreateGimmickConfigIfActive` → 각 시스템 `RequireForUpdate<…Config>` | `Battle/Effects/*GimmickConfig.cs` | 확정 | config 부재 = 완전 무변화(클린 플레이) |
| 기믹 배정 | `MatchSeed.DeriveGimmickSeed(matchSeed)` → `GimmickSelection.PickIndex(poolCount, seed)` 순수 함수 | `Core/MatchSeed.cs:29` · `Core/GimmickSelection.cs` | 확정 | 매치당 1회. `poolCount<=0 → -1`, seed 0 방어 |
| 사직서 → 메테오 barrage | 방어유닛 **자연 사망** 시 배치 타일에 드랍 → 5장 모이면 소모 → Walk 타일 10곳 순차 낙하(**적 전용**) | `ResignationDropSystem` · `ResignationThresholdSystem` · `MeteorBarrageRequestsSingleton` | 확정 | rev 1 의 **강제 퇴근(10초 타이머)·퇴근 코스트 환급은 폐기**(unit 8, 2026-07-21 사용자 평가) |
| 기믹 리빌 페이즈 | 배치 **앞**에 진입 리빌 1회(아이콘+룰 라벨+한 줄+색조+움직임+효과음). 전투 중 상시 배지는 만들지 않는다 | `gimmick-recognition-upgrade`(완료 2026-08-01) · `Data/GimmickRevealConfig.cs` · `UI/GimmickPhaseView.cs` | 확정 | 2026-07-31 사용자 결정. 배정 로직은 안 건드린다 |
| `BossPeriodicTriggerSystem` | 주기·배치 감지자. 채찍질·자장가·가호·발사 명세·배치 스킬 | `Battle/Combat/BossPeriodicTriggerSystem.cs` | 확정 | 하류 `ProjectileEmitter` **같은 프레임** 제약이 이 seam 에만 있다 |
| 보스 도약(`SelfBlink`) | 즉시 텔레포트 + 뷰 아치 + **피격 가능** | `Concrete/BlinkToClusterSkill.cs`(Id 5) · `BossLeapVisualEventsSingleton` | 확정 | 목적지 해석 실패 시 **그냥 skip**(임계는 이미 소모, 재발동 없음) |
| 궁극기 도약(`UltimateLeap`) | 판 밖 이탈 + 예고 + **무적**. 개시가 두 컴포넌트 원자 부착 | `Concrete/UltimateLeapSkill.cs`(Id 6) · `UltimateLeapSystem` · `UltimateLeapVisualEventsSingleton` | 확정 | `SelfBlink` 와 한 kind 로 겸직 금지(DotEffect 가 겪은 형태). 「생존당 1회」는 코드에 없다 — `fraction ≥ 0.5` 면 둘째 경계가 음수 |
| 마메모 · 자는 캐스터 | 웨이브 회전을 멈추는 보스 / 캐스터가 CC 를 안 봐서 자장가에 걸려도 계속 시전 | `HazardCastSystem` · `ShieldCastSystem`(`shield-guardian-defender` 계약 7 의 **의도**) | **애매** | 스펙은 「캐스터 편성이 자장가의 답」으로 프레이밍했는데 **사용자는 버그로 읽었다**(Play 관측 2026-08-11). 고치면 가디언·캐스터 **전원** 동작이 바뀌어 별 spec. 2026-08-26 재확인 「후속」 — 미결 |
| 트리거가 트리거를 낳을 때의 순서 | 스킬이 만든 사건이 다시 트리거를 켤 때의 순서·깊이 계약 | — | **애매** | ⚠ **이력 미발견.** `budget = queue.Count` 는 그 프레임 드레인의 **종료만** 보장하고, 같은 프레임 재진입 이벤트가 어느 seam/프레임에 실행되는지는 **어디에도 선언돼 있지 않다**. 시체폭발 → OnKill → 잿불 같은 연쇄가 이미 라이브 |

---

## 2. `DcTriggerKind` 전량 (10) / `DcPayloadKind` 전량 (33)

### `DcTriggerKind` — append-only(시트가 enum **값**으로 왕복)

| # | 값 | 뜻 | 감지자 · 특이사항 |
|---|---|---|---|
| 0 | `None` | 부착 즉발 — **슬롯을 굽지 않는다** | 브리지 부착 지점(Immediate seam). `HasDetector` 는 false(=닫힘) |
| 1 | `AttackN` | N번째 공격 RESOLVE | `AttackSystem`. 진영 무관 |
| 2 | `OnDamagedN` | N회 피격 | `DamageApplicationSystem`(단일 피해 루프). counter 는 **Units 소유 `DamagedCounter`** |
| 3 | `OnDeath` | **내가 죽는다**(작별 선물) | `UnitLifecycleSystem` — 모든 사망 경로(피해·치명 타이머·순찰 수명)가 합류하는 유일 지점. **2026-08-26 적에게 개방** |
| 4 | `PeriodicTimer` | 주기 초마다 | `BossPeriodicTriggerSystem`. 잔여 이월(드리프트 없음), 틱당 최대 1회 |
| 5 | `HealthThreshold` | 체력 경계 통과 | `HealthThresholdSystem`. 단조 래치 k(회복으로 안 되감김), 스폰 시점 maxHp 스냅샷 기준 |
| 6 | `OnKill` | **내가 죽였다** | `DamageApplicationSystem` 킬 처리 — AttackSystem RESOLVE 를 **안 탄다** |
| 7 | `OnShieldBreak` | 부여된 실드가 피격으로 완전 소진 | `DamageApplicationSystem` Absorb. **시간 만료 경로는 없음/명시적 배제** |
| 8 | `OnRetire` | 퇴근할 때 | 브리지 퇴근 경로. ⚠ **`OnDeath` 와 교차 발동하지 않는 것이 존재 이유**(퇴근은 `DeadTag` 를 안 단다). 방어유닛 전용 |
| 9 | `OnPlace` | 배치 확정 | 브리지 → `JustDeployed` → **Periodic seam**. 방어유닛 전용(적은 배치되지 않는다) |

### `DcPayloadKind` — append-only

| # | 값 | 뜻 | 비고 |
|---|---|---|---|
| 0 | `None` | 센티넬 | **어휘 밖** |
| 1 | `ProjectileToTarget` | 그 공격의 대상에게 탄 1발 | 피해는 flat(공격자 배율 안 탐) |
| 2 | `SelfTileAoe` | 자기(또는 실려 온) 자리 폭발 | **라우팅이 트리거로 갈리는 대표 사례** — concrete 2개로 분기 |
| 3 | `NextAttackDoubleFire` | 다음 공격 충전 부여 | 부여 = 스킬 / **소비는 `AttackSystem` 내부**(밖) |
| 4 | `SelfBuffLethal` | 즉발 공속 버프 + 자폭(시한부) | `trigger == None` 전용 |
| 5 | `AreaBarrage` | 원격 진앙 폭격 | **arm 철거됨** — 발사 명세로 이관. **어휘 밖** |
| 6 | `SelfBlink` | 자기 순간이동(밀집 셀) | 뷰 아치 + 피격 가능 |
| 7 | `SelfWarmupBuff` | (예약, 핸들러 미구현) | **죽은 값** — warmup 이 Sleep 으로 승격되며 은퇴. **어휘 밖** |
| 8 | `PlacementAura` | host 생존 중 **신규 배치** 유닛에 공속+warmup | **어휘 밖 — 발동 규칙(시제)** |
| 9 | `AllyMoveSpeedAura` | 펄스 오라(보스 "채찍질") — 같은 진영 이속 | host 자신 제외. `duration > periodSeconds` 가 저작 계약 |
| 10 | `ApplyCcToTarget` | 맞은 적 **1기**에 CC | 밀쳐냄은 방향 필수(같은 칸이면 무의미) |
| 11 | `ApplyStackToTarget` | 맞은 적에 원소 스택/DoT | `tileRange` 를 상한으로 겸직(레거시) |
| 12 | `SelfStatBuff` | 시전 유닛 자신에게 StatModifier | last_stand(경계) / devouring(처치) |
| 13 | `HeavyStrike` | 그 발동 공격 자신의 출력을 magnitude 배 | **어휘 밖 — 자기참조**. 전 victim 에 적용(cleave/splash/bounce 포함) |
| 14 | `DreamCocoon` | 호접몽 — 부착 즉시 Sleep + 완주 감시 | 무피격 완주 = 영구 버프 / 피격 wake = 파탄. `trigger == None` 전용 |
| 15 | `BountyMark` | 살찌운 제물 — 적에게 각성 배율 + 받는 피해 감소 | 적을 겨냥하는 **최초의** 드림캐쳐. `trigger == None` 전용 |
| 16 | `AreaSleep` | N타일 내 가장 가까운 M명 L초 수면 | concrete 가 「재우자마자 내가 깨울 자리」를 뺀다(레거시엔 없던 규칙) |
| 17 | `EmitProjectilePattern` | 발사 명세(`ProjectilePatternData`) 트리거 | 반복 주기는 **트리거 소유**, 전개는 패턴 소유 |
| 18 | `UltimateLeap` | 이탈 → 예고 → 강습 | 이탈 중 공격·이동 불가 + **피격 불가** |
| 19 | `GrantShield` | 실드 부여. `tileRange 0` = 자신만 | **`duration` 안 씀**(이 엔진 실드엔 TTL 없음). bake 가 `duration>0` 경고 |
| 20 | `SplitOnDeath` | 분열(자식 N기) | **어휘 밖 — 다른 배선**. `DcTriggerSlot` 엔트리를 만들지 않는다(브리지 킬 드레인이 SO 직독). `SplitChain.Validate` 가 순환 차단 |
| 21 | `AreaBreath` | 대상 방향 **부채꼴** 즉발 피해 | 반각 정의역 **(0°, 90°)**. 저작 120° 는 조용히 60° 가 되므로 `>= 90` loud 거절. 초기값 50°(45° 는 대각 경계) |
| 22 | `SelfOrbitProjectile` | 궤도 화염구 — 중심은 **발사 시점 고정점** | 재타격 쿨타임은 탄 SO 소유. host 가 죽어도 자기 수명을 산다 |
| 23 | `AreaTaunt` | 범위 도발 | 기존 어휘로 표현 불가라 신설(10은 1기, 16은 수면 전용, **도발은 CC 가 아니라 타게팅 상태**) |
| 24 | `SpawnHazard` | 장판 설치(잿불) | **신규 스칼라 0** — 카드는 「어떤 불씨를」만. 지속·반경·모양·틱·뷰 전부 `HazardSO` 소유(심·뷰가 갈리지 않게) |
| 25 | `RecallAttachedToFront` | 인수인계 — 부착분을 큐 앞으로 | **어휘 밖 — 손패 UI**. 실행자가 Mono 컨트롤러. **어느 칸도 안 읽는다** |
| 26 | `AllyStatAura` | 반경 내 **아군**에 TTL 스탯 모디파이어 | 레거시 `BoostNearbyDefenders` 수렴. 스탯은 `buffStat`, **진영은 payload 가 정한다** |
| 27 | `OpponentStatAura` | 반경 내 **상대**에 TTL 스탯 모디파이어 | 레거시 `BindNearby` 수렴 |
| 28 | `GainCost` | 코스트 획득 | **판 밖 런타임** — sim 상태 무변경, 큐 미경유(즉시) |
| 29 | `ReduceSkillCooldown` | 액티브 쿨다운 단축 | 〃 |
| 30 | `AreaApplyStack` | 반경 내 상대 전원에 스택 | 11과 달리 **반경과 상한이 둘 다 필요**해 겸직 불가. 상한은 스택 종류가 소유 |
| 31 | `AreaCc` | 반경 내 상대 전원에 CC(+부수 피해) | **CC 가 있어야 성립**(지속 0 + 피해만은 조용히 소모) |
| 32 | `AreaDot` | 반경 내 상대 전원에 지속 피해 + **자기 공격 대기** | 조사 중 기본 공격 정지가 사양 |

---

## 3. `SimIntentKind` 전량 (24) + `MetaIntentKind` (2)

| 군 | 의도 | 도착지 · 계약 |
|---|---|---|
| 센티넬 | `None` | — |
| 피해·회복 | `DealDamage` | `IncomingDamage` 인박스 |
| | `Heal` | `IncomingHeal` 인박스 |
| 상태 | `ApplyStatModifier` | `StatModifierApplyEvents` ⚠ 병합 키 계약 |
| | `ApplyStack` | `StackModifierApplyEvents` |
| | `ApplyCc` | `EnemyCcEvents` (이름과 달리 **진영 중립 채널**) |
| | `ApplyDot` | `DotApplyEvents` |
| | `ClearCc` | `CcClearRequests` |
| | `GrantShield` | `IncomingShield` 인박스 ⚠ **다음 프레임 드레인이 의도** |
| 표적 | `Taunt` | `AggroAcquireEvents` |
| | `CreditThreat` | `ThreatHitEvents` |
| | `ScaleKillReward` | 표식(적 대상) — **가진 값을 배로**. `Amount` 가 양이 아니라 배율 |
| 이동 | `Blink` | `BlinkRequestEvents` |
| | `BeginUltimateLeap` | 진행형 개시 — **두 컴포넌트 원자 동시 부착** |
| 생성 | `SpawnProjectile` | `ProjectileSpawnRequest` 캐리어 |
| | `EmitPattern` | `PatternSlot` 전진 + `EmitterInstance` ⚠ **성사와 원자** |
| | `SpawnOrbitProjectile` | 한 점을 도는 탄 — `SpawnProjectile` 에 겸직 금지 |
| | `SpawnZoneCarrier` | `EffectSpawner`(장판·링크·오라 캐리어) |
| | `SpawnFieldCarrier` | `SkillFieldKind`(AllyBuff / Pull / Portal) — **종류마다 읽는 필드가 다름** |
| 진행형 개시 | `BeginDreamCocoon` | 잠 + 완주 감시 원자 부착 |
| | `StartLethalTimer` | 「이 시간 뒤에 죽는다」 — 버프 만료와 죽음은 **다른 사건** |
| | `GrantCharge` | 다음 공격 예약. 이름이 DoubleFire 가 아닌 이유 = 소비 규칙은 RESOLVE 소유 |
| | `DelaySelfAttack` | **자기 자신을 묶는다**. 새 컴포넌트 없이 `AttackState` 대기시간을 `max` 로 민다 |
| 관측 | `Report` | 실패 보고(조용한 no-op 방지). 문자열은 어댑터가 만든다 |
| | `PlayVisual` | 연출 신호 — **시뮬 상태 무변경**. 「언제 트나」가 스킬의 판단 |
| **메타**(판 밖) | `GainCost` | `CostRuntime` — 큐 미경유(즉시) |
| | `ReduceSkillCooldown` | `SkillRuntime` — 〃 |

---

## 4. `SkillSeam` 전량 (None + 7) 과 same-frame 하류 계약

| seam | 감지자 | 드레인 위치 계약 |
|---|---|---|
| `None` = 0 | — | 「생산자가 안 채웠다」 → loud 폐기. ⚠ **0 에 진짜 seam 을 두면 fail-open**(안 채운 이벤트가 그리로 조용히 흘러간다) |
| `Periodic` = 1 | `BossPeriodicTriggerSystem`(주기 + `OnPlace`) | `[UpdateAfter]` BossPeriodicTrigger · `[UpdateBefore]` `ModifierApply` · `AggroState` · **`ProjectileEmitter`**(발사 명세 같은 프레임) |
| `Attack` = 2 | `AttackSystem` RESOLVE | `[UpdateBefore]` `DamageApplication` · `ProjectileEmitter` |
| `Threshold` = 3 | `HealthThresholdSystem` | `[UpdateBefore]` `UltimateLeapSystem`. ⚠ emitter 제약 **없음**(ECS 리뷰 H-1 — 걸면 emitter 가 `UnitLifecycle` 뒤로 밀려 그 프레임 사망 host 의 잔여 버스트가 엔티티째 사라진다) |
| `Death` = 4 | `DamageApplicationSystem`(`OnKill` · `OnDamagedN` · `OnShieldBreak`) | `[UpdateAfter]` DamageApplication · `[UpdateBefore]` **`UnitLifecycleSystem`**(대상이 아직 있다). ⚠ emitter 제약 없음 — `OnKill × 발사 명세` 첫 저작이 생기는 날 처리 필요 |
| `Lifecycle` = 5 | `UnitLifecycleSystem`(파괴 **뒤**) + 브리지 퇴근 | 시전자가 **없다** → `RequiresLiveCaster = false`. 값만으로 완결돼야 한다. `[UpdateBefore]` `HealthThresholdSystem` 은 이제 **지연만** 정한다 |
| `Immediate` = 6 | 브리지(부착 · 액티브) | **자기 순서를 갖지 않는다** — 브리지가 `Update()` 를 직접 불러 자기 콜스택에서 끝낸다(동기 트랜잭션) |
| `Cast` = 7 | `HazardCastSystem` → `CastEventsSingleton` | 캐스터는 `attackRange` 0 이라 RESOLVE 에 못 간다. `AttackSystem` 이 **같은 프레임**에 소비 |

`SkillFiredEvent` 생산 지점 13개소(grep `Seam = SkillSeam`): `AttackSystem:1865` · `BossPeriodicTriggerSystem:121` · `HealthThresholdSystem:114` · `DamageApplicationSystem:318,398,488` · `UnitLifecycleSystem:261` · `HazardCastSystem:168` · `BattleBridge.Dreamcatcher:356,433,1191` · `BattleBridge:3032`(액티브) · `BattleBridge:4389`(퇴근).

---

## 5. concrete 34 분류표 (shape group → concrete)

| 형 | concrete (Id) — 한 줄 |
|---|---|
| **대상 투사체 / 탄** | `TargetProjectileSkill`**19** 그 공격의 대상에게 탄(대상은 스킬이 고르지 않는다, 피해 flat) · `OrbitProjectileSkill`**24** 궤도 화염구(중심 = 발사 시점 고정점, 월드속도÷반경 = 각속도) · `EmitPatternSkill`**7** 발사 명세 전진 + 인스턴스(원자, 이미 조준된 명세는 안 건드림) |
| **자리 폭발** | `SelfAreaBlastSkill`**4** 산 시전자 발밑(owner = 자기 자신이 계약) · `DeathSiteBlastSkill`**20** 실려 온 자리(죽은/죽인/비워진 칸 — **누구의 자리인가는 감지자가 정한다**) · `TileMeteorSkill`**33** 지정 칸 낙하(예고 시간 = 비행 시간) |
| **해저드 / 장판** | `DeathSiteHazardSkill`**21** 실려 온 자리에 장판(통행층 fail-closed) · `CastHazardSkill`**28** 캐스트 성사 → 실려 온 칸에(종류가 실려 옴, 등록부가 다름) |
| **필드 캐리어** | `AllyBuffFieldSkill`**30** 아군 장판(**빈 칸에도 놓인다** — 0기 거절 폐기) · `PullFieldSkill`**31** 당김장(재시전은 겹친다) · `PortalSkill`**32** 입구/출구 2셀 |
| **오라 (TTL 모디파이어)** | `AllySpeedAuraSkill`**2** · `AllyStatAuraSkill`**9** · `OpponentStatAuraSkill`**10** — 공용 base(`StatAuraSkill`). ⚠ 해제는 **TTL 만료 하나뿐**(반경 이탈도 host 사망도 회수 안 함) |
| **스탯 버스트** | `TileStatBurstSkill`**29** — **장판이 아니라 스냅샷**(그 순간 반경 안 적에게만, 나중에 들어온 적엔 안 걸림) |
| **자기 버프 / 자기 상태** | `SelfStatBuffSkill`**18** · `ThresholdSelfBuffSkill`**22**(출처가 「빈사에서 켜졌다」 — 공용 구현 + 얇은 파생 둘) · `SelfBuffLethalSkill`**25** 버프+시한부 · `GrantSelfChargeSkill`**23** 충전 부여 · `DreamCocoonSkill`**26** 잠+완주 감시(**의도 하나인 이유가 원자성**) |
| **광역 상태** | `AreaSleepSkill`**1**(재우자마자 깨울 자리 제외, 수는 유지) · `AreaCcSkill`**14**(띄움 길이 = `min` — 잡는 시간과 다르다) · `AreaDotSkill`**15**(지속 채널이라 그동안 공격 정지가 사양) · `AreaStackSkill`**13**(상한은 스택 종류 소유 — concrete 는 모른다) |
| **단일 대상 상태** | `TargetCcSkill`**16**(밀쳐냄은 방향 필수 — 같은 칸이면 CC 슬롯만 먹는다) · `TargetStackSkill`**17**(상한 겸직 제거) |
| **실드** | `GrantShieldSkill`**3**(만충이면 skip — 같은 출처 병합이 max 라) |
| **표적 / 귀속** | `AreaTauntSkill`**8**(통행 층 게이트만 concrete 에서) · `BountyMarkSkill`**27**(두 효과가 **원자** — 하나만 걸리면 버그) |
| **이동** | `BlinkToClusterSkill`**5**(목적지 실패 시 skip — 임계는 소모됨) · `UltimateLeapSkill`**6**(개시만, 카운트다운·착지·슬램은 시스템) |
| **부채꼴** | `ConeBreathSkill`**34**(cos² 자기 축 — `HitThreshold` 에 겸직 금지) |
| **메타 (판 밖 자원)** | `GainCostSkill`**11** · `ReduceSkillCooldownSkill`**12** — 「ECS 를 안 만지니 레이어 밖에 두자」는 유혹이 경계를 무너뜨리는 자리 |

합계 3+3+2+3+3+1+5+4+2+1+2+2+1+2 = **34**.

---

## 6. ECS 고유라 새 설계에서 개념 자체가 사라지는 것

1. **`EcsSkillContext` 어댑터 통째.** foundation README 가 이미 「버려지는 것은 이것뿐이고 그것이 포트 패턴의 비용」이라 선언했다. `ISkillContext`·concrete·seam 규칙은 그대로 산다.
2. **`skillId` 를 unmanaged 로 굽는 이유**(계약 12). Burst ISystem 이 managed 레지스트리를 못 읽어 「이 슬롯은 새 경로인가」를 숫자 하나로 갈랐다. 엔진-프리 sim 에선 직접 참조가 가능해 이 간접층의 **근거가 소멸**한다. ⚠ 단 시트 왕복·골든·저작이 `skillId` 에 매여 있는지는 별도 확인 필요.
3. **ECB 스테이징 vs 직접 쓰기 2갈래.** 「재생이 한 박자 뒤」「읽고-고쳐-쓰기를 못 한다」는 ECB 의 성질에서 나온 구분이라 개념째 사라진다. 다만 **원자성 요구는 남는다**(잠+감시, 잠금+무적).
4. **`DcTriggerSlot` 이 unmanaged 버퍼여야 하는 제약.** `tileRange` 한 칸이 13가지 뜻을 겸직한 근본 원인이 「Burst 가 읽을 수 있는 평평한 형태」였다. 새 설계에선 payload 별 타입 분리가 가능해진다.
5. **`SkillFiredEvent` 값 스냅샷의 **일부**.** 「드레인 시점에 엔티티가 파괴됐다」는 ECS 수명 규칙의 산물이다. ⚠ 그러나 **`bestTarget` 재현 불가**(9단계 오버라이드 합성)와 **「감지 시점과 실행 시점이 다르다」는 구조**는 아키텍처와 무관하게 남는다.
6. **`SkillDispatch{Seam}System` 7 인스턴스 + `[UpdateBefore/After]` 그래프.** 시스템 순서가 곧 seam 계약이던 형태. 명시적 phase 호출로 바뀌며, 그때 **순서 제약이 코드에 안 보이게 되는 위험**이 새로 생긴다.
7. **`Entity.Null` 시전자 / `CasterRef.Player` 접기.** 「핸들이 없다」가 ECS 표현이다. 개념(플레이어 시전 = 시전자 없음)은 남는다.
8. **`SystemBase` vs `ISystem` 선택**(디스패처가 managed 여야 했던 이유 = 레지스트리가 managed). 무의미해진다.
9. **`_aliveAttackersQuery` 류 쿼리 공유 제약**(불변식 16)과 **`OnUpdate` 의 `GetComponentLookup` 을 지우면 Burst 가 조용히 깨진다**(불변식 14) — 둘 다 Entities 고유 함정이라 사라진다.

---

## 7. 코드에만 박혀 있고 문서에 없는 규칙 (rebuild 가 놓치기 쉬운 것)

1. **`Periodic` seam 이 `OnPlace` 도 받는다.** 이름은 「주기」인데 배치 트리거가 같은 드레인을 탄다(`BossPeriodicTriggerSystem` 이 `JustDeployed` 를 본다). 문서 표의 「주기·배치」 한 단어가 전부다.
2. **경계·죽음 seam 에 emitter 순서 제약이 «의도적으로» 없다.** 오늘 그 조합 저작이 0건이라 무해하지만, 첫 저작이 생기는 순간 **0/1 프레임 지연이 빌드마다 갈린다.** 주석에만 있고 계약 문서엔 없다.
3. **`DcSkillRouting` 의 「트리거 무관 표」 위치 자체가 계약이다.** `NextAttackDoubleFire`·`SpawnHazard` 를 트리거별 블록에 두면 그 트리거 밖 조합이 라우팅 0 을 받아 **조용히 죽는다** — 실제로 `OnPlace × 충전`이 그랬다.
4. **`-1 = 없음` 이 세 축에 따로 있다**(`DataIndex`·`PatternIndex`·`HazardDataIndex`). struct default 0 은 **유효 index** 라 미배선 슬롯이 0번 탄/0번 패턴/0번 장판을 쏜다. 명시 `-1` 초기화가 계약.
5. **`GrantShield` 에 시간 만료가 없다.** 실드는 깎여야만 사라진다(`ShieldMath` 에 TTL 축 없음). bake 가 `duration > 0` 저작을 경고한다.
6. **`GrantShield` 의 `tileRange 0 = 자신만`은 병합 키 때문이다.** host 를 포함시키면 「경계마다 자기 실드」와 「주기마다 아군 실드」가 한 슬롯(`source` 키)을 공유해 **벽이 상시 실드로 붕괴**한다.
7. **오라 해제는 TTL 만료 하나뿐.** 회수는 「제거」가 아니라 같은 병합 키로 항등 재발행(중립화)이라 그 축을 열면 별개 설계가 필요하다.
8. **`SplitOnDeath` 는 버퍼는 붙이되 엔트리를 건너뛴다.** bake 가 「mechanics 가 비지 않으면 무조건 `AddBuffer`」 + 「이 조합만 엔트리 skip」. 초판 설계는 전용 큐·레지스트리·슬롯 필드·이벤트 필드·스탬프 다섯을 다 만들려 했고 리뷰(H2)가 걷어냈다.
9. **`AreaSleepSkill` 이 「재우자마자 내가 깨울 자리」를 뺀다** — 레거시 실드 파열엔 없던 규칙. 재우는 **수**는 그대로(뺄 만큼 더 뽑는다)고 **누가** 자느냐만 다르다. 라우팅 표 주석에만 있다.
10. **`ConeBreath` 반각 45° 금지.** 셀 대각선 경계에 정확히 걸려 부동소수 비교가 동전 던지기가 된다(결정론 요건). 저작 초기값 50°, `>= 90` loud 거절(cos²θ = cos²(180−θ) 라 120° 가 조용히 60° 가 된다).
11. **폭발 킬 귀속 원칙 3갈래**: 시체폭발 = killer · 진동갑주 = self · 궁지폭발/실드폭발 = host(2026-07-25 사용자 통일). `SelfAreaBlastSkill` 의 `owner = 자기 자신`이 계약인 이유가 **OnKill 연쇄·위협 귀속**이다(점수엔 무영향).
12. **`RecallAttachedToFront` 의 `magnitude` 소비자가 0.** 컨트롤러도 덱도 안 읽고 카드 문안은 「저작 손잡이 없음」을 단언한다.
13. **실드 파열 브리지 arm 이 죽은 코드로 남아 있다**(`if (!routedToSkillLayer)` — migration unit 8 잔여물). 남아 있으면 「파열은 브리지가 실행한다」로 오해된다(backlog [낮음]).
14. **`SkillCone` 부채꼴 거리 게이트가 양쪽 몸 0** — 제약 13 미이행분. 프리필터만 고치면 **변화 0**이라 콘 자체를 고쳐야 한다(backlog [중]).
15. **`AttackSystem.PickFallbackTarget` 이 아직 체비셰프 사각**이고 **이름과 달리 폴백이 아니라 유일 경로**다(폭탄맨 + 캐스터 4종은 RESOLVE 를 안 타 `AttackReach` 를 한 번도 안 지난다). 사용자 지시로 뒤로 미뤄져 있다(2026-09-06).
16. **`ScaleKillReward` 는 이 레이어에서 유일하게 «적을 이롭게 하지 않으면서 적에게 거는 표식»** 이고, 그 값은 그 적이 죽을 때 소비된다. `Amount` 가 양이 아니라 **배율**이다.
17. **`SelfWarmupBuff`(7) 는 유령 enum 이다.** 핸들러 미구현 + 사용 카드 0. append-only 규율 때문에 번호만 잔존한다 — 같은 실수를 막으려고 게이트 축이 「미사용 라이브 경로 금지」를 계약으로 세웠다.

---

## 8. 이 영역에서 rebuild 가 결정해야 할 열린 질문 (7)

1. **트리거가 트리거를 낳을 때의 순서 계약.** 큐 스냅샷 / 세대(generation) / 깊이 예산 중 무엇인가. **이력 미발견**이고, `budget = queue.Count` 는 그 프레임 드레인의 종료만 보장한다. 시체폭발 → `OnKill` → 잿불 같은 연쇄가 이미 라이브다.
2. **seam 7 을 그대로 옮기나, phase 로 재정의하나.** seam 은 「감지자의 프레임 창」에서 도출된 값이라 sim 구조가 바뀌면 **7 이라는 수가 근거를 잃는다.** 그러나 규칙(「감지자가 다른 창을 가지면 따로 난다」)은 아키텍처 중립이고 foundation 계약 7 이 **개수를 계약에 적지 말라**고 못박았다.
3. **`ISkill` 과 `DcMechanic` 두 어휘를 계속 둘 것인가.** 오늘은 저작(`DcMechanic`) → 라우팅(`SkillIdFor`) → 실행(`ISkill`) 3단이고, `skillId` 의 존재 이유가 Burst 제약이었다. 그 제약이 사라지면 **저작이 concrete 를 직접 가리킬 수 있는지** 판정해야 한다. ⚠ 단 트리거별 분기(죽은 자리 ↔ 내 발밑 ↔ 비워진 칸)는 남는다 — 「누구의 자리인가는 감지자가 정한다」가 그 이유다.
4. **어휘 밖 7종을 레일 안으로 들일 것인가.** 특히 `PlacementAura`(시제)와 `HeavyStrike`(자기참조)는 「포트의 결함이 아니라 범주가 다르다」로 정리돼 있다. 새 설계에서 **「미래 규칙 등록」과 「자기를 부른 사건 수정」을 1급 어휘로 만들지** 결정해야 한다. `SplitOnDeath` 는 「시제상 스킬인데 배선만 다른 길」이라 형태 점검이 이미 후속 후보다.
5. **직접 쓰기 폐쇄 목록 4건을 무엇으로 대체하나.** 넷 다 ECS 고유 제약에서 나왔고 엔진-프리 sim 에선 평범한 쓰기가 된다. 그러면 **「쓰기는 Emit 만」이라는 규율을 무엇이 강제하는가** — 오늘은 asmdef(`noEngineReferences`)가 컴파일러로 강제한다(불변식 3). 이 강제 수단을 잃으면 규율이 관습으로 퇴화한다.
6. **미개방 게이트 조합을 열 것인가.** `GateComboSupported` 가 2조합만 허용한다. 특히 `EventTarget × OnDamagedN` 은 **한 프레임 다중 source 의 subject 선정 규칙**(KillAttribution 전례)을 먼저 정해야 열린다. 복수 게이트 ∧, Mono 상태 게이트(코스트·각성)는 브리지 주입 결정이 선행.
7. **자는 캐스터.** 해저드/실드 캐스터가 CC 를 안 보는 것이 사양인가 버그인가 — 스펙은 사양으로 썼고(`shield-guardian-defender` 계약 7) **사용자는 버그로 읽었다**(Play 관측 2026-08-11). 고치면 가디언·캐스터 **전원**의 동작이 바뀐다. rebuild 는 이 판정을 상속하거나 뒤집어야 하고 중간은 없다.

### 덤으로 확인된 문서 드리프트 2건 (rebuild 가 잣대로 삼기 전에 고쳐야)

- `docs/spec/skill-layer-foundation/README.md` 계약 3 본문이 「**직접 쓰기(예외 3건, 폐쇄 목록)**」인데 바로 아래 표는 **4행**이고 `battle-core-architecture.md` §8-3 은 「**4건**」이다.
- `docs/spec/battle-sim-extraction/order-capture.md` 에 **디스패처 7계가 미등재**다(README 가 「48 시스템」이라 적은 것은 57이 맞다 — §9 드리프트 표). 「생산자 위치」 박제가 실제와 어긋나 있다.
