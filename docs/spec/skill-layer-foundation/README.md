# skill-layer-foundation — 스킬 단일 레이어의 토대

> 상태: **작성 완료 2026-08-25 · 미착수 (사용자 승인 대기)**
> 흡수: `docs/spec/skill-fire-dispatch/`(rev 4, 홀드 해제). 그 spec 의 계약 6·12 는 폐기한다.
> 이전 작업은 별 spec: `docs/spec/skill-layer-migration/`
> 설계 근거: [`docs/plans/2026-08-24-skill-layer-unification-critic.md`](../../plans/2026-08-24-skill-layer-unification-critic.md)
> (spec critic 5트랙 수렴본 — 5명 전원 REQUEST CHANGES, CRITICAL 7건을 반영한 결과가 이 문서다)

## 끝점 (두 spec 이 함께 도달하는 곳)

**이 게임의 모든 스킬 — 보스 스킬 · 방어유닛 배치 스킬 · 특수 스킬 — 이 하나의 레이어 위에 있다.**
스킬 하나 = concrete 하나이고, concrete 는 필요한 모듈을 조합해 자기를 구현한다.
`Execute` 를 호출하는 주체가 곧 그 스킬의 소유자다.

이 spec 은 그 레이어의 **토대**만 짓는다. 실제 이전은 `skill-layer-migration` 이 한다.
끝점 도달 여부는 그쪽 완료 조건으로 판정한다.

## 검증 질문

> **방어유닛이 `BossLeap` 을 장착하면 «상대 진영» 밀집 셀로 도약하는가?**

⚠ **원래 질문("`BossLeap` 을 잡몹 에셋에 장착하면 코드 0줄로 도는가")은 오늘 main 에서 이미
참이라 폐기했다** — 화이트리스트가 `HealthThreshold` 를 적에 개방하고(`DcTrigger.cs:113~117`),
bake 가 잡몹에도 슬롯을 굽고 `BossTag` 는 `tier==Boss` 에만 붙으며(`BattleBridge.cs:9355`),
arm 에 보스 게이트가 없다. 통과해도 아무것도 증명하지 않는다. `skill-fire-dispatch` rev 3 이
「니들러 실증」으로 같은 함정에 빠져 죽었다. 교체 질문은 진영 하드코딩(리터럴 56곳)이 실제로
풀려야만 참이 된다.

부수 질문: **스킬 하나의 동작을 ECS 월드 없이 단위 테스트할 수 있는가?**

## 구조

```
[도메인]  ISkill / concrete        ECS·Unity 참조 0. 값 타입과 도메인 핸들만.
[저작]    SkillDescriptor(SO)      타입 필드 · 발동 조건 · 부착 자격 · 문안. Battle 을 모른다.
              │  질의 ↓      ↑ 의도
[포트]    ISkillContext            질의 12동사 · 의도 14종.
              │
[어댑터]  EcsSkillContext          지금. EntityManager/ComponentLookup 주입형.
          TestSkillContext         페이크. 월드 없이 도는 단위 테스트.
          SimSkillContext          M1 이후. 같은 포트, 다른 구현.
```

**«통합»의 정의 = 단일 레지스트리 · 단일 어휘 · 드레인 지점 3.**
단일 호출 지점이 아니다 — 그건 산술적으로 불가능하고(계약 7), 통합은 concrete 와 어휘에 있다.

## 작업 단위

| 파일 | 작업 구분 | 목적 |
|---|---|---|
| [0](0_protocol_surface_derivation.md) | 표면 도출 | 3어휘 arm 전수 → 질의·의도 동사 확정. **코드 0줄** |
| [1](1_characterization_net.md) | 그물 | arm 특성화 테스트 — 이전의 유일한 증인 |
| [2a](2a_sim_entity_id_gap.md) | 핸들 | 도메인 핸들 타입 + 캐리어 ID 별도 대역 |
| [2b](2b_faction_relativization.md) | 진영 | 리터럴 56곳 → `Opponents/Allies(caster)` |
| [3](3_port_adapter_registry.md) | 포트 | `ISkillContext` + `EcsSkillContext` + `TestSkillContext` + 레지스트리 |
| [4](4_dispatchers_three_seams.md) | 디스패처 | `SystemBase` 3인스턴스 seam 핀 + `SkillFiredEvent` |
| [5](5_first_concrete.md) | 첫 concrete | 자장가(`AreaSleep`) 1개로 경로 전체 관통 |

## Feature-wide 계약

1. **도메인 계층은 ECS 를 모른다.** `ISkill`/concrete 에 `Entity`·`EntityManager`·`SystemAPI`·
   `DynamicBuffer`·`NativeQueue`·`IComponentData` 등장 금지(사용자 하드 제약 2026-08-24).
   `Wassup.Skills` asmdef 가 Entities/Collections 를 참조하지 않아 **컴파일이 이를 강제**한다.
   허용되는 유일한 외부 타입은 `Unity.Mathematics` 값 타입 — 순수 managed 라 M1 의 Burst-off·
   CoreCLR 교차 실행과 양립하고, 자체 벡터 신설은 제약 8(과잉 추상화) 위반이다.
2. **핸들은 도메인 타입이다.** `SimEntityId` 는 `IComponentData` 라 도메인이 못 쓴다 →
   plain 핸들 타입을 신설하고 ECS 컴포넌트가 그것을 싣는다(unit 2a).
3. **쓰기는 의도 방출.** concrete 는 상태를 바꾸지 않고 intent 를 `ctx.Emit` 한다.
   어댑터의 intent 적용은 **둘 중 하나**다:
   - **채널 쓰기** — 기존 소유 맥락 채널(`NativeQueue` / 인박스 버퍼)에 enqueue·append.
   - **구조 변경 스테이징** — 어댑터는 **호스트가 주입한 ECB 에 담기만** 하고 재생하지
     않는다. 재생은 **디스패처가 자기 seam 안에서** 한다(그 seam 계약이 곧 「언제
     materialize 되나」의 답이다). 컴포넌트 **직접** 쓰기는 여전히 금지다.

   ⚠ 이 두 번째 갈래가 필요한 이유(투트랙 리뷰 M-3, 2026-08-25 개정): 진행형 상태 개시가
   **두 컴포넌트의 원자 동시 부착**인 스킬이 있다(궁극기 — 잠금과 무적은 레이어가 갈리지만
   수명이 하나다). 그것을 채널로 쪼개면 어느 하나만 붙는 프레임이 생긴다. 한 ECB 한 재생이
   그 원자성의 실체이고, 원본 arm 도 같은 방식이었다.

   개정 전 문면은 「enqueue/append **만**」이었다 — 코드가 건전한데 계약이 못 따라온 경우라
   계약을 고쳤다. 반대로 고치면 원자성이 깨진다.

   - **직접 쓰기(예외 3건, 폐쇄 목록)** — ECB 가 **구조적으로 표현할 수 없는** 경우에만.
     ECB 는 「값을 지금 기록하고 나중에 재생」이라 **읽고-고쳐-쓰기를 못 한다**(재생 시점의
     현재값이 아니라 기록 시점의 값을 쓴다). 그리고 채널 append 와 **같은 순간**이어야 하는
     쓰기도 못 한다(재생이 한 박자 뒤다). 아래 셋이 전부이고, **네 번째가 생기면 그건 이
     목록에 이유와 함께 올리거나 ECB 로 가야 한다**(재리뷰 H-2, 2026-08-26):

     | 지점 | 왜 ECB 가 안 되나 | 구조 변경 |
     |---|---|---|
     | `DelaySelfAttack` | 쿨다운을 `max` 로 **읽고-고쳐-쓴다**. ECB 로 하면 그 사이 다른 쓰기를 덮는다 | 아니오 |
     | `ScaleKillReward` | 표식은 그 적이 **죽을 때** 소비되는데 처치 이벤트가 enqueue 시점에 값을 복사한다 — 재생을 기다리면 같은 프레임에 죽는 적이 배율 없는 값을 싣는다 | 아니오 |
     | `BeginDreamCocoon` 의 감시자 부착 | 잠(버퍼 append)과 **원자적으로 같이** 붙어야 한다. 한 박자 늦으면 그 사이 맞은 대상은 깨울 잠이 없어 파탄이 안 나고 **감시만 남는다**(공짜 완주) | **예** |

     ⚠ **위 셋은 「직접 쓰기」의 목록이지 「구조 변경」의 목록이 아니다**(2026-08-26 자기
     수정 — 한때 「셋 중 구조 변경은 하나뿐」이라 적었는데 거짓이었다). 두 축이 다르다:

     - **직접 쓰기**(위 표 셋) — 어댑터가 `_em` 으로 **기존** 컴포넌트를 건드린다.
     - **구조 변경** — 아키타입을 바꾼다. `BeginDreamCocoon` 의 감시자 부착에 더해,
       **캐리어 생성 셋**(`EffectSpawner.SpawnTornadoField`·`SpawnPortal`·
       `SpawnAllyBuffField`)이 전부 `CreateEntity` 라 여기 속한다. 이쪽은 헬퍼를
       거치므로 「직접 쓰기」가 아니고, 그래서 위 표에 없다.

     구분이 중요한 이유: 디스패처는 lookup 과 후보 풀을 **프레임당 한 번** 묶어두고
     드레인 내내 재사용하는데, 그 스냅샷의 전제를 건드리는 것은 **구조 변경 쪽**이다.
     즉 위험을 세려면 표가 아니라 이 문단을 봐야 한다.

     ⚠ `ApplyCc` 는 버퍼 append 라 어느 쪽도 아니다 — `_em` 을 인자로 받는다는 이유로
     구조 변경으로 세지 말 것.

     ⚠ **실드 부여(`GrantShield`)와 잠 자체(`ApplyCc`)는 예외가 아니다** — 둘 다 인박스 버퍼
     append 라 위 첫 갈래에 해당한다. 「`_em` 을 만졌다」와 「컴포넌트를 직접 썼다」는 다르다.
4. **호출자 = 소유자.** `caster` 는 인자다. concrete 는 진영·host 종류를 갖지 않고 모듈이
   caster 상대적으로 답한다. **caster 없음(액티브 = 플레이어 시전)도 표현 가능해야 한다.**
5. **무상태.** concrete 는 필드를 갖지 않는다. 진행형 상태(도약 비행·수면 완주 등)는 지금처럼
   컴포넌트+시스템 소유 — 스킬은 개시와 수치까지다.
6. **감지는 분산 유지.** 사건은 그게 나는 시스템에서 난다. 통합 대상은 어휘와 실행이지
   감지가 아니다(통합하면 매 프레임 전 유닛 재스캔이 된다).
7. **통합 = 단일 레지스트리 · 단일 어휘 · 드레인 지점 3.** 감지자들이 각자 same-frame 하류
   계약을 갖고 그 구간이 서로 겹치지 않아(#8 < #45) 단일 드레인은 **산술적으로 불가능**하다.
8. **이벤트는 값 스냅샷이다.** `SkillFiredEvent` 가 params 값 + 대상 핸들 + 발화 위치를 싣는다.
   드레인 시점에 슬롯을 재독하면 죽음 계열에서 host 가 이미 없다.
9. **프로토콜 표면은 «도출»한다.** unit 0 이 **3어휘**(`DcPayloadKind`·`OnPlaceEffectType`·
   `SkillEffectType`) arm 전수에서 동사를 뽑는다. 상상으로 정의하지 않는다.
10. **스킬 = 저작 SO 1개 + 순수 로직 1개.** 저작은 UnityEngine 을 알고 Battle 을 모른다.
    로직은 UnityEngine 을 모른다. 문안·타입 필드는 저작 소유(도메인 이관 시 계약 1 위반).
11. **증인은 arm 특성화 테스트다.** 골든 코퍼스(`LegacyTraceV0`)에 기대지 않는다 — 그 축은
    `battle-sim-extraction` 소관이고 **아직 착수도 확정도 되지 않았다**(사용자 판정 2026-08-25).
    이 spec 은 그 진행 여부와 무관하게 선다. unit 1 이 그물을 먼저 깐다.
12. **이중 경로 라우팅 축은 unmanaged 다.** Burst 감지 시스템은 managed 레지스트리를 읽을 수
    없다 → 슬롯에 **베이크된 `skillId`**(0 = legacy arm)로 가른다. 이전 중 매 커밋에서 게임이
    도는 것이 이 축에 달렸다.

## 파이프라인 커버리지

**N/A** — 플레이 오브젝트 신설도 생성→렌더 경로 변경도 없다(코드 재배치 + 저작 형식 변경).
`skill-layer-migration` 에서도 같다 — 이전되는 arm 이 만드는 오브젝트(투사체·장판·소환물)는
기존 파이프라인을 그대로 탄다.

## 순서·의존

- **`battle-sim-extraction` M1 착수 전**에 끝낸다. M1 설계 정본이 「드림캐쳐 파셜 64KB — 이것
  없이는 sim lib 이 반쪽」이라 적었고, M1 이 먼저면 10k줄 브리지의 legacy switch 를 **있는
  그대로** 이식해 parity 를 통과시킨 뒤 lib 안에서 다시 재구조화하게 된다(같은 코드를 두 번 만진다).
- 버려지는 것은 **`EcsSkillContext` 어댑터뿐**이고 그것이 포트 패턴의 비용이다.
  감지/실행 분리 seam 은 M1 의 「내부 phase queue」와 동형이라 살아남는다.
- ⚠ **M1 의 기준선 정비는 이 spec 의 일이 아니다.** 그쪽이 착수될 때 그쪽이 정한다.

## 결정 기록 (재론 전 필독)

- **08-11 「ISkill 기각」의 실근거 3종을 이 설계는 셋 다 밟지 않는다** — ① 에셋
  `SerializeReference`(→ concrete 는 에셋이 아니라 코드 레지스트리) ② 보스별 클래스(→ **스킬별**
  concrete 이고 변형은 params) ③ 스킬의 상태 소유(→ 계약 5 무상태). 기각 결정은 유효하고
  이 설계가 그 밖에 있다.
- **`skill-fire-dispatch` 홀드 해제** — 원래 「재개 = 다음 보스 제작 때, 지시 없이 재개 금지」
  였다. **사용자 결정 override 2026-08-24.**
- **M1 착수 순서 재조정** — M1 은 2026-08-22 사용자 착수 승인을 받은 상태였다.
  **사용자 결정 override 2026-08-24.**
- **기믹은 「스킬」 정의에서 제외** — `Data/Gimmick/` **SO 4개**: `Burnout`(불금 ·
  `FatigueAccrualSystem`) · `RedBull`(먹고 달리자 · `PickupSpawn/ConsumeSystem`+`LastRunSystem`) ·
  `ClockOut`(사직서 · `ResignationDrop/ThresholdSystem`) · `Onsen`(온천 · `HeatAccrualSystem`).
  이들은 **매치 규칙**이고 활성화가 시즌 SO 게이팅이다. 「아직 안 옮김」이 아니라 정의상 다른 축이다.
  (census 실측 2026-08-25 — 앞 둘을 「과로」 하나로 묶은 초기 집계는 틀렸다. `BattleConfig`
  활성화 단위로 독립 2개다.)
- **공격 출력 수식자는 스킬이 아니다** — `HeavyStrike` · 게이트 합성 · `DcAttackModSlot` 전 kind
  (FrontmostTarget·ProjectileBounce·DamageVsSleeping) · `FrontmostAttackLock.damageMulSnapshot` ·
  `NextAttackDoubleFire` 의 **소비**. pre-scan 합성 불변식이 공격 계산 내부 거주를 강제한다
  (`AttackSystem.cs:1114~1148` 확인 — `WouldFire`∧`GatePass` 예측과 실제 counter Tick 이 같은 프레임·
  같은 bestTarget·같은 pre-damage HP 를 읽어야 일치). 큐 드레인 후의 `Execute` 는 이미 만들어진
  출력에 개입할 수 없다.
  **판별 기준**(unit 0 도출): 「이번 공격의 출력 숫자/타이밍 **조립에 곱·합으로 참여**」 = 밖 /
  「발동 후 **별도 대상·캐리어·채널로 나감**」 = 안.
  ⚠ **경계 정정 2026-08-25**: `OnDamagedN × NextAttackDoubleFire` 는 발동(감지·Tick)이 다른
  `OnDamagedN` payload 와 **동형**이다. 통째로 밖에 두면 `OnDamagedN` 계열이 payload 별로 두
  레이어에 갈라진다. → **「charge 부여」까지가 스킬(안), charge 의 소비는 `AttackSystem` 내부(밖).**

## 후속 후보

- **`SkillEffectType`·`OnPlaceEffectType` enum 완전 철거** — 이전이 끝나면 죽는다.
  `onPlacePush*` 3필드 + `ApplyOnPlacePush` 는 **이미 에셋 소비자 0**이라 무비용으로 떨어진다.
- **채널 접힘 판정** — `ShieldBreakEventsSingleton`·`MeteorBarrageRequestsSingleton` 은
  `SkillFiredEvent` 의 선행 특수형이다. 이전 후 접히는지 확인. 안 접히면 「단일 발동 경로」
  관측성이 거짓이 된다.
- **무거운 arm 이관** — `AttackN`·`OnKill`·`OnDamagedN`·`OnShieldBreak`·`OnDeath` 중 전 유닛
  순회 Burst 코드 내부에 있는 것들.
- **악몽의 늪(자리에 남는 장판)** — `skill-fire-dispatch` 홀드 인계 #4 가 함께 대기시킨 축.
  이 spec 범위 밖이며 다음 보스 제작 때 콘텐츠 결정으로 꺼낸다.
- **`ISkill` 요구 플래그로 `DcApplicability` 판정기 단순화** — unit 3 이 선언 축만 열고
  판정기 이관은 migration 이후.
