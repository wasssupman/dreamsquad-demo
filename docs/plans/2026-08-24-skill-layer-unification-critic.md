# 스킬 레이어 통합 — spec critic 5트랙 수렴 (2026-08-24)

리뷰 대상: `skill-layer-unification-draft.md` (초안, 미승인)
리뷰어 5명 전원 **REQUEST CHANGES**. CRITICAL 7건.

| 트랙 | 담당 쟁점 | 판정 |
|---|---|---|
| critic-scope | 끝점 완결성 | REQUEST CHANGES — C1·C2·C3 CRITICAL |
| critic-protocol | 프로토콜 표면 | REQUEST CHANGES — 제약 1 **성립**, 공백 4 |
| critic-determinism | 결정론·순서·성능 | REQUEST CHANGES — D1·D2 CRITICAL |
| critic-sequencing | 순서·의존·완주 | REQUEST CHANGES — S1 CRITICAL |
| critic-ecs | ECS 규율 정합 | REQUEST CHANGES — E1·E2 CRITICAL |

---

## A. 방향은 승인됐다

- **사용자 하드 제약(도메인 ECS 참조 0)은 실측으로 성립.** 질의 **12동사** · 의도 **14종**.
  경로장(`FlowFieldSingleton`) 소비 arm 은 **2개뿐**(blink/leap)이고 소비 형태가 이미 순수 함수
  (`BlinkMath`·`DefenderDensity` — 격자를 인자로 받음)라 질의 2개로 봉합. **감쌀 수 없는 것 없음.**
- **감지 분산 유지**는 파킹 설계 실측과 일치 — 옳다.
- **맥락 경계 붕괴 아님** — enqueue/append 는 CLAUDE.md 가 정한 맥락 간 통신의 사전 승인 동사.
  쓰기 소유권은 드레인하는 소유 맥락에 남는다.
- **`Unity.Mathematics` 는 도메인에 남긴다** — 순수 managed 라 M1 Burst-off·CoreCLR 과 양립.
  자체 벡터 신설은 제약 8 충돌.
- **성능 우려 기각(§9 ③ 종결)** — 최악 정렬 프레임 ~50발, 발당 가상호출+큐왕복+풀스캔 **<0.1ms**.
  공격 사건 실측 2.4~5.6/s. 조건: 어댑터가 후보 풀을 **프레임 공유 lazy 캐시**로.
- **결정론 안전** — `Assets/_Project/Scripts/Battle/` 전체 `Schedule`/`ScheduleParallel` **0건**.
  plain `Enqueue` 로 충분, 순서 = 시스템 순서 = 결정적.
- **`ISkill` 부활은 08-11 기각과 정합** — 실근거 3종(에셋 SerializeReference / 보스별 클래스 /
  스킬의 상태 소유) 셋 다 안 밟는다. 단 spec 에 1문단 명시 필요.
- **`TestSkillContext` 는 실이득** — 페이크가 답할 무거운 질의가 전부 이미 순수 코어로 존재
  (`DefenderDensity`·`BlinkMath`·`AuraPulse`·`AoeTargetCap`·`TileAoe.IsInCone`·`OnPlaceFireAim`).
  오늘 테스트 불가인 로직(AreaSleep skip-rank 선별·EmitPattern 조준)이 처음 단위 테스트 표면에 올라옴.

---

## B. CRITICAL — 구조를 바꿔야 하는 것

### 1. 드레인 지점은 **3개**다 (E1 + D1 독립 수렴)

감지 시스템들이 각자 명시적 same-frame 하류 계약을 갖는다. 필요한 구간:

| 구간 | arm |
|---|---|
| #4 → #8 | BossPeriodic (어그로·모디파이어·CC 같은 틱) |
| #35 → #36 | AttackN (피해정산 #36, 발사 #40) |
| #45 → #46 | HealthThreshold (blink #47, 궁극기 카운트다운 #46) |

`#8 < #45` → **한 지점으로 셋 만족 불가(산술적)**.
`BattleBridge.Update` 는 **원리적 탈락** — 라이브 루프가 `Mono Update → SimulationSystemGroup`
이라 그룹 산출 이벤트는 다음 틱 브리지 페이즈에 드레인(하네스 스텝 순서 Bridge→ECS 가 박제).
인용했던 `HazardCastSystem` 선례는 **Burst ISystem ↔ Burst ISystem 한정**이라 부적용.

→ **디스패처 = `BattleSimGroup` 안의 managed `SystemBase`, 단일 클래스 · 인스턴스 3개**를
어트리뷰트로 핀. Battle 폴더 현재 `SystemBase` 0개 — 제약 3 의 "managed 참조가 진짜 필요할 때"
정당한 첫 사례(MonoBehaviour 아니므로 제약 1 과 무충돌).

**«통합»의 재정의: 단일 레지스트리 · 단일 어휘 · 드레인 지점 3.**
통합은 concrete 와 어휘에 있지 호출 자리에 있지 않다.

단일 지점 가정 시 실제 발생하는 변화(3지점 채택 시 전부 소멸):
자장가 1틱 지연→피해자 공격 1회 추가 · 도발 +1틱 · whip 오라 위치 드리프트 ·
SelfBlink/UltimateLeap +1틱 · **AttackN×패턴이 골든 ch6 ProjectileSpawn 직격** · AreaBreath 정산 +1틱.

또한 **읽기 표본점 이동**(D4): BossPeriodic arm 은 이번 프레임 **이동 전**(#18 앞) 위치를,
AttackSystem arm 은 **이동 후** 위치를 읽는다. 3지점이면 자동 해소.

### 2. 이벤트는 **값 스냅샷**이어야 한다 (E4 + D2 수렴)

`SkillFiredEvent{caster, skillId, slotIndex}` 3필드로 부족.
- AttackN arm 전부가 `bestTarget`/`bestTargetPos` 소비 — 재도출하면 타겟팅 규칙 복제
- 죽음 계열은 드레인 시점에 host 부재: `UnitLifecycleSystem.cs:108~137`(파괴 **전에** 굽는다),
  `DamageApplicationSystem.cs:389~405`("killer 가 **살아 있는 지금** 읽는다")
- TOCTOU 전력 있음: `BossPeriodicTriggerSystem.cs:134` "죽음 큐가 끼면 시체가 한 번 더 스킬을 쓴다"

→ 이벤트가 **SkillParams 값 스냅샷 + 대상 SimEntityId + 발화 위치**를 싣는 것을 계약으로.
선례 3개 존재: `ShieldBreakEvent`·`EnemyKilledEvent`·`DefenderDeathEvent`.
드레인 시 캐스터 생존·슬롯 유효 재검증(무효 = drop + loud log) 추가.

### 3. **골든은 증인이 아니다** (D3 + S1)

- 골든 코퍼스 7종 전체에서 스킬 발화 기록 **0회**: `ch13(DcTriggerFired)=0` · `ch10(ShieldGranted)=0`
  · `ch11(ShieldBreak)=0` · `ch12(Knockup)=0`
- StatModifier · Cc · Aggro · Blink 는 **채널 자체가 없다**(`LegacyTraceV0.cs:24~44`)
- `4_legacy_trace_golden.md:114~122` 가 «드림캐쳐 다용 판» 부재를 명시(카드 사용이 UI 경유라 재현 불가)
- 게다가 코퍼스가 **stale**(configHash 항목 3개 추가) + 워킹트리 dirty 75건에
  `MapDocument_Test.asset`(configHash 반응 축) 포함

→ "동작 무변경, 골든이 증인"은 **거짓**. 이전 전에 (a) 하네스 입력에 카드 부착·메커닉 발화
시나리오를 넣어 코퍼스 확보(placement 가 `PlaceDefenderAs` 로 UI 우회한 선례) 또는
(b) 특성화 범위를 **arm 전수**로 넓히고 완료 기준에서 «골든 단독 증인» 문구 제거.

### 4. `SimEntityId` 싱글턴 승격은 **M1 로 반환** (E2)

발급이 단일 카운터·스폰 순서인데 장판 캐리어는 매치 **중간** 스폰 → 이후 모든 유닛 ID 가 밀림 →
타겟팅 동률 승자·발사 RNG 열 변경 → **골든 전건 발산**.
승격도 불필요 — 캐리어 생성처가 전부 managed(`BattleBridge` 드레인 · `EffectSpawner`)라
Bridge 필드 카운터로 충분. 코드 주석이 승격 시점을 "M1 이 이벤트·스냅샷 키로 ID 를 쓸 때"로 못박음.
→ 캐리어 ID 는 **별도 대역** 또는 부착 지연. unit 1 의 ID 갭 + 진영 상대화 56곳은 **A/B 분할**
(골든이 빨개지면 어느 쪽인지 못 가름).

### 5. 검증 질문이 **오늘 이미 참**이다 (C2)

"BossLeap 을 잡몹에 슬롯 한 줄 → 코드 0줄"은 현행 슬롯 기계로 이미 성립:
① 화이트리스트가 HealthThreshold 를 적에 개방(`DcTrigger.cs:113~117`)
② bake 가 잡몹에도 슬롯을 굽고 BossTag 는 `tier==Boss` 에만(`BattleBridge.cs:9355`)
③ arm 에 보스 게이트 없음 ④ 타겟 소스가 방어유닛 전수 셀(`HealthThresholdSystem.cs:341~357`)

**rev 3 의 «니들러 거짓 검증»과 동형.**
→ 교체: **"방어유닛이 `BossLeap` 을 장착하면 «상대 진영» 밀집 셀로 도약하는가"**
(현재 defender-cell 하드코딩이라 거짓 → 진영 상대화 + 레이어가 끝나야 참).

### 6·7. 범위 누락 2건 — 액티브 · 소환 (C1 + C3)

**액티브(`SkillData`/`SkillEffectType`)**: 6에셋, 라이브 3(`Active_Meteor/Tornado/Portal.asset`).
arm 6개가 전부 `BattleBridge.CastSkillAtTile` switch 한 곳(`:2515~2558`) — §4 의 "arm 6곳"이
`DcPayloadKind` 어휘만 세서 **통째로 빠졌다**. 어휘 3개 중 1개가 census 밖.
- **시전 주체 엔티티가 없다** — `ThreatTable.cs:20` "bridge-cast skills (player Meteor, owner == Null)"
- **Portal 은 타일 2개**(entry/exit, 입구==출구 거절 규칙까지 대상 축)
- 단 편입 비용은 **낮다**(protocol 실측): 신규 질의 **0** · 신규 의도 **1**(존 캐리어 스폰).
  경로장 안 읽음. 진행형 상태는 이미 Effects/Movement 소유 → 계약 5 정합.
  쿨다운·코스트가 이미 호출자 소유(`skillRuntime`) → «호출자=소유자» 정합.

**소환(`SummonPatrolAbility`)**: `Ability_SummonPatrol_Summoner.asset` + 전용 시스템 3개
(`SummonerState`·`PatrolLifecycleSystem`·`PatrolFieldSystem`). 사용자가 2026-08-16 에 가지 않기로
결정한 반대 방향("능력마다 전용 ECS 상태 + 전용 시스템")의 **다섯 번째 구성원**.
`on-place-skill-rework` 계약 2 가 "캐스트 4종"이라 적은 건 소환사가 그 뒤에 생겨서다.

→ **unit 0 의 전수 정의를 3어휘로**: `DcPayloadKind` arm + `OnPlaceEffectType` arm + `SkillEffectType` arm.
도출 소스에 액티브가 없으면 그 가족의 동사(타일 지정 존 스폰·2타일 링크·플레이어 시전)가
표면에서 빠진 채 계약 7("상상으로 정의하지 않는다")이 **형식만** 지켜진다.

---

## C. 재산정된 census (~75행)

| 부류 | 실측 | 초안 배정 |
|---|---|---|
| 보스 mechanics | 10행/3에셋 (짱쎈4·마메모3·나이트메어3) | unit 4 ✓ |
| 비보스 적 | 3행 — 드래곤 `AreaBreath` 1(tier 1) · 슬라임 분열 2 | **드래곤 미배정** |
| 방어유닛 규칙(OnPlace) | 5행/5에셋 (SkyStrike·Taunt·AreaShield·OnPlaceBlast·BombMan) | **미배정** |
| 레거시 `OnPlaceEffectType` | arm 9종/12에셋 | unit 6 ✓ |
| 드림캐쳐 카드 | **32행/30장**, 실행 클래스 3종(슬롯 arm / 즉발 5행 / hand-op) | unit 7 이 뭉갬 |
| 캐스트 계열 | 8에셋 (하자드4·실드1·볼리2·폭탄1) | unit 7 ✓ |
| **소환** | 1에셋 + 전용 시스템 3 | **없음** |
| **액티브** | 6에셋 (라이브 3) | **없음** |
| 기믹 | **3계열** — 과로(Fatigue·LastRun·Pickup×2) · 퇴근(사직서+임계 메테오) · 온천(Heat) | 과로만 명명 |
| 공격 출력 수식자 | HeavyStrike·DoubleFire·게이트 + `DcAttackModSlot`·`FrontmostAttackLock` | §7 예외 (정당) |

§4 의 기존 수치는 **전건 재검산 일치**(진영 리터럴 56=29+27 · payload 26 · 트리거 10 ·
enum 11종/에셋 12/arm 9 · 브리지 10,262줄 · 분기 5393~5590 · PlayMode 3종 · 채널 28).
문제는 수치가 아니라 **census 의 범위**였다.

---

## D. 예외 목록 정정 (§7)

| 예외 후보 | 판정 |
|---|---|
| 공격 출력 수식자 | **정당 — 코드 확인됨**. `AttackSystem.cs:1114~1148` pre-scan 이 출력 계산 내부(Burst RESOLVE)에 거주, WouldFire∧GatePass 를 같은 프레임·같은 bestTarget·pre-damage HP 로 평가. 단 가족 2개(`DcAttackModSlot`·`FrontmostAttackLock.damageMulSnapshot`) 미명명 |
| 기믹 | **근거가 §0 자기 규칙과 모순.** 파킹 설계가 Fatigue·LastRun 을 "rule 이관 가능"으로 판정 → 예외가 아니라 "아직 안 옮김". → **«스킬» 정의에서 정의 수준으로 제외**(매치 규칙·시즌 SO 활성화)를 §0 에 명문화 + 로스터 3계열 전부 명명 |
| SplitOnDeath | **결론 맞고 근거 틀림.** "슬롯 안 씀"은 현황 서술이지 구조적 불가 아님(디스패치가 managed 라 `SpawnUnitIntent` 로 표현 가능). 진짜 논거: **이미 끝점의 성질을 옛 경로로 충족**(아무 적 에셋에 payload 한 줄 = 코드 0) |

---

## E. 표면 설계 공백 (protocol)

- **쓰기 100% 의도 방출은 거짓** — 직접 쓰기 4형태: `AttackState.cooldownRemaining`
  `SetComponentData`(`BattleBridge.cs:5544`) · `AwakeningReward` 덮어씀(`.Dreamcatcher.cs:1120`) ·
  `EffectSpawner.ApplyCc` 가 `CcEffect` **라이브 버퍼** 직접 append(큐 우회 — 같은 CC 경로 2개) ·
  진행형 상태 부착 4종(`LethalTimer`·`DreamCocoon`·`NextAttackDoubleFire`·`UltimateLeapState`)
- **`Execute` void 인데 arm 3종은 요청-응답** — 부착 코드(-1=무차감 거절, 코스트 환불 결정) ·
  `RegisterPlacementAura` revoke 핸들 · affected 수
- **`SkillParams` 겸직** — `tileRange` **8+ 의미**(AoE 반경·궤도 반경·maxStack·피해감소%·폴백 반경·
  착지 링 상한·최대중첩·조준 사거리), `period` 는 AttackN 카운트이자 orbitCount.
  bake 가 값 **변환**까지 함(coneCosSq 사전계산 등). → skillId별 typed params + 디스패처 번역층.
  **rev 4 의 «params 뷰 struct» 가 이미 확정한 답** — 새 발명 아님
- **Mono 도메인 의도 5** — `GainCost`·`ReduceSkillCooldown`(ECS 안 만짐) · hand-op · PlacementAura · SplitSpawn
- **`Opponents` 필터가 오늘 5벌** — BossPeriodic 공유 풀 무필터 / `IsLegalOnPlaceTarget` /
  AttackSystem 3술어 / AreaSleep +PendingDeployment. 필터 축(사망·배치중·이탈·통행층·피해수신가능)을
  enum flag 로 명세하고 arm별 현행 조합을 박제하지 않으면 «같은 이름, 다른 후보» 버그가 숨는다
- **저작 계층을 §3 구조도에서 떨어뜨렸다** → 복원 필요.
  `DcApplicability`(25 case) = **ISkill 요구 플래그 선언으로 이관 가능, 충돌 없음**
  (이 계층은 이미 ECS 무참조 순수, case 내용이 UI 지식이 아니라 스킬의 자기 서술).
  `DreamcatcherCardText`(20 case) = **저작 SO 소유, 도메인 이관 금지**.
  얻는 것: 새 스킬 = **switch 4곳 갱신**(적용성+bake+arm+문안) → concrete 1곳 + 저작 필드
- **`SystemAPI` 는 시스템 타입 안에서만 동작** — 독립 어댑터 클래스에서 호출 불가.
  어댑터는 호스트가 주입한 `EntityManager`/`ComponentLookup` 으로 살아야 한다 (§3 문장 오류)
- **asmdef 게이트 성립** — 현재 런타임은 `Wassup.Runtime` 단일 어셈블리. 신규 `Wassup.Skills` 가
  Entities/Collections 미참조면 게이트 성립, 단방향 참조로 순환 없음. 단 `SimEntityId` 가
  `IComponentData` 라 **도메인 핸들 타입 신설 + 컴포넌트가 그것을 싣는 이중화** 필요

---

## F. 순서·완주 (sequencing)

- **M1 관계 = 선행(조건부).** M1 설계 정본에 "드림캐쳐 파셜 64KB — 이것 없이는 sim lib 이 반쪽".
  M1 먼저면 10k줄 브리지 legacy switch 를 **있는 그대로** 이식해 parity 통과 후 lib 안에서 재구조화
  = 같은 코드 2회 + 골든 사이클 2회.
  반대 논거 3종도 기록: ① M1 후엔 Burst-off 라 큐 seam 불필요 ② M1 은 이미 사용자 착수 승인(2026-08-22)
  ③ `EcsSkillContext` 는 M1 스왑 시 은퇴 → **"버려지는 코드 0"은 과장**, "버려지는 건 어댑터뿐이고
  그게 포트 패턴의 비용"으로 정정
- **계약 12 폐기는 조건부 정당** — 조항이 막으려던 위험은 "M0 **진행 중** 착수" 국면의 것.
  단 끝점은 범위를 뒤집었지 **골든 기준선 충돌 논리를 반박하지 않는다**(E1·E2 가 그 실체).
  M1 앞 배치 유지하려면 «spec 종료 시 골든 재기준 1회 = M1 의 새 A/B 기준선» 절차를 spec 에 박을 것
- **사용자 결정 2건의 override 를 결정 기록에 명시**: ① skill-fire-dispatch 홀드 해제
  ("재개 = 다음 보스 제작 때, 지시 없이 재개 금지") ② M1 착수 순서 재조정
- **이중 경로 라우팅 축 미정의(MAJOR)** — Burst 감지는 managed registry 를 못 읽는다.
  "이 슬롯은 새 경로인가 legacy arm 인가"를 가르는 **unmanaged 축**(베이크된 skillId, 0=legacy)이
  계약에 필요. 중간 커밋마다 게임이 도는지가 여기 달림
- **작업량**: 초안 스코프(21행)로도 **18~28커밋**. 확장 census(~75행 + 소환 + 액티브 + 그물 재산정)면
  **30~40커밋**. unit 1·4·6·7 전부 1커밋 규모 아님 → 분할 필수
- **Burst 재작성 실체** — "감지만 잔존"은 문구로만 참. `BossPeriodicTriggerSystem` **~500/733줄**,
  `HealthThresholdSystem` **~220/358줄**, `AttackSystem` ~200/2235줄이 managed 이전.
  `DamageApplicationSystem` 은 **이미 스냅샷+이벤트+브리지 실행기 모양**(초안 구조의 기존 구현)
- **Burst→managed ulp 게이트(D5)** — near-tie 대상 선택 flip 가능. parity 가 exact 라 재베이스라인
  외 답이 없고, 재베이스라인하면 "동작 무변경" 증언이 사라진다.
  → 완료 기준에 **«구 sim 을 Burst-off 로 돌린 골든 재검증»** 추가(컴파일 도메인 차 vs 로직 차 판독기)
- **시트 무손실 PASS** — `UnitStatImportDto` 에 onPlace 필드 **0건** → flat 필드 7개 삭제가 시트 왕복
  안 깸. 조건 2줄: ① `DcTriggerKind`/`DcPayloadKind` 기존 값 재번호·은퇴 금지(시트가 enum 값으로 왕복)
  ② unit 6 의 12에셋 재저작 중 로그인 시트 임포트 오염 경고를 완료 기준에
- **`onPlacePush*` 3필드 + `ApplyOnPlacePush`** — 에셋 소비자 **0**(nonzero grep 0건). 무비용 제거
- **order-capture 재덤프**를 완료 기준에 — arm 실행 이전 = **생산자 위치 이동**과 등가.
  M0 unit 0 이 "소비자가 뒤로 밀리면 8개 생산자가 **조용히** 같은 프레임 반영으로 바뀐다"고 경고

---

## G. 추가 계약 (리뷰 산출)

1. `SkillFiredEvent` enqueue 는 **메인스레드 한정**, `ParallelWriter` 금지 (위반 시 SimEntityId 정렬 드레인)
2. 드레인은 **시작 시점 스냅샷 1회** — 드레인 중 재유입분은 다음 틱 (재진입 차단기)
3. 어댑터의 intent 적용 = **소유 맥락 채널 enqueue/append 만**. component 직접 쓰기·구조 변경은
   소유 맥락 시스템이 수행 (계약 3 문구 조임). 예외 1건: UltimateLeap 개시의 원자 동시 부착
   (`UltimateLeapState`+`LeapFlight`, 로컬 Temp ECB 즉시 재생) — intent 화 시 수행 주체·재생 시점·원자성 명시
4. 29번째 채널 수명주기 = 하우스 패턴 3점 세트(생성 Persistent / 싱글턴 파괴 / Dispose), CLAUDE.md 채널 목록 갱신
5. 어댑터는 후보 풀을 **프레임 공유 lazy 캐시**로 (선례 `BossPeriodicTriggerSystem.cs:114~125`)
6. 계약 10 일반화: "이전하는 **모든 행**에 골든 또는 특성화 테스트 선행"
7. 레지스트리 미등록 검출 = `DcApplicability` 의 `default→Unclassified` fail-closed 계승
