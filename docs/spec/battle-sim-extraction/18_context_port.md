# 18 — 맥락 4 이식 (Units → Movement → Effects → Combat)

## 목적

M1 의 본체. ECS 44시스템·컴포넌트 97+21 을 순수 C# 틱 파이프라인으로 옮긴다. 정본은 **청사진 ③**
(`m1_blueprint_tick_pipeline.md`) — phase 배치·내부 채널 26쌍의 같은틱/1틱-지연·사망 4단계 릴레이·
ECB "루프 중 기록, 루프 후 적용"·RNG write-back 이 전부 그 문서에 있다. 이 unit 은 그것을 **코드로
옮기는 작업**이고 새 설계 결정을 하지 않는다.

## 변경 대상

의존 역순으로 4단계, **단계별 독립 커밋**(각 단계 후 골든 대조):

1. **Units** (7시스템 · 컴포넌트 22+5): Health·IncomingDamage 인박스 3종·사망 마킹/파괴 릴레이·
   `SimEntityId` 발급·HitFlash 는 **discard**(뷰 이관 — salvage §1)
2. **Movement** (2시스템 · 4+0): 위치 갱신 단일 권한·flowfield/patrol/chase 하강·포탈·`PastGoalTag`·
   Blink 소비
3. **Effects** (26시스템 · 40+8): CC/DoT 병합(**duration 정책 비대칭 보존** — 청사진 ③ §6)·모디파이어
   3단(Apply→Tick→Aggregate)·해저드/존·기믹 4종·픽업·캐리어 TTL
4. **Combat** (9시스템 · 30+8): 공격 루프(최대 클러스터 1,600줄)·투사체 2축·발사 명세·임계/도약·
   위협 테이블(**하류 소비자 0 — discard 여부 사용자 확인**, salvage §2)

각 단계에 딸린 테스트 포팅: World-조립 38파일은 "**어서션만 salvage, 골격 재작성**"(정본 M1-5).

## 구현

- **게이트 35개를 phase early-return 으로 번역**(청사진 ② §3). 함의 보존 필수 3건:
  `DamageApplication` 게이트는 버퍼 **부재**만 본다 · `Attack` 정지 시 Cast 드레인 동반 정지 ·
  `StackModifierTick` 3중 AND 비대칭. 채널 소멸로 자연 해소되는 것은 **명시 변경으로 기록**.
- **부재-상태 20건은 개별 체크**(청사진 ② 부속 B-2). 최우선 함정: 궁극기 이탈 무적은 `WithNone` 이
  아니라 **버퍼 Clear + continue** — 직역하면 착지 프레임 지연 폭탄이 된다.
- 내부 9채널은 함수 호출로 접되 **26쌍의 타이밍을 재현**한다. 1틱 지연 14쌍은 버그가 아니라 unit 0 이
  박제한 계약(특히 AggroHit 의 구조적 영구 지연, EnemyCc 의 생산자별 지연 혼재).
- **A/B 병행 구동**: 이 단계들 동안 구 sim 이 정본이고 신 sim 은 그림자로 돈다. 스왑은 unit 20.

## 완료 기준

- 단계별 compile 0 · EditMode 회귀 0.
- **단계별 골든 대조**: 그 맥락이 담당하는 시나리오가 구 sim 골든과 parity 통과(exact 축 = semantic
  이벤트·점수·상태 해시 / epsilon = 연속값). 실패 시 **그 단계에서 멈춘다**(누적 금지).
- 컴포넌트 97+21 · 게이트 35 · 부재-상태 20 각각 이식 체크리스트 100%(청사진 ② 부속을 체크박스로 사용).
- 포팅 테스트에서 어서션 손실 0 — 재작성한 골격이 같은 것을 단정하는지 리뷰.

---

## ⚠ 정찰 결과 (2026-08-05) — 위 "의존 역순 4단계" 는 실행 불가능하다

읽기 전용 정찰이 맥락 구조를 전수 측정했다. **위 §변경 대상의 4단계 분할과 §완료 기준의 "단계별
골든 대조" 는 달성 불가능**하고, 아래 규칙 클러스터 축 분할로 대체한다.

### 반증 3건 (실측)

1. **맥락 간 타입 의존이 완전 순환이다.** Units 조차 Combat·Effects·Movement 를 import 한다 —
   `Battle/Units/DamageApplicationSystem.cs:6-7`, `UnitLifecycleSystem.cs:5-7`,
   `Movement/MovementSystem.cs:6-7`. 위상 정렬이 불가능하므로 "의존 역순" 이라는 전제 자체가 없다.
2. **어떤 맥락도 한 틱을 단독으로 돌 수 없다.** phase×맥락이 최대로 인터리브돼 있다 — Units 7시스템은
   P3·P5·P7·P9·P10·P12 에 흩어져 있고 최초와 최후 사이에 **타 맥락 30개**가 낀다. 골든 시나리오 7종은
   전부 **전체 틱**을 요구하므로 "그 맥락이 담당하는 시나리오" 라는 것이 존재하지 않는다.
3. **Units 를 먼저 옮기면 스텁이 최대가 된다.** Units 는 내부 9채널을 **0개 소비하고 2개로 생산**한다.
   반대로 Effects 는 6/9 를 소비해 먼저 옮길수록 스텁이 줄어든다.

### 규모 실측

| 맥락 | 파일 | 총 줄 | 시스템 | 시스템 몸체 | 컴포넌트 | 버퍼 | 소유 채널 |
|---|---:|---:|---:|---:|---:|---:|---:|
| Units | 41 | 1,404 | 7 | 791 | 22 | 5 | 6 |
| Movement | 11 | 648 | 2 | 332 | 4 | 0 | 1 |
| Combat | 74 | 6,102 | 9 | 3,688 | 30 | 8 | 9 |
| Effects | 92 | 4,532 | 26 | 2,493 | 40 | 8 | 11 |
| **계** | **218** | **12,686** | **44** | **7,304** | **96** | **21** | **27** |

시스템 몸체는 58% 뿐이고 42%(5,382줄)는 데이터 타입 + 순수 유틸이다 — 후자의 상당분은 unit 17 이
가져간다. **실질 이식 대상은 7,304줄 + 117 데이터 타입.**

### 🟢 가장 반가운 측정 — 병렬 job 이 0 이다

`IJobEntity` 선언 **3개**(`CcDecayJob`·`DotApplyJob`·`DotApplyWithEventsJob`)가 전부 `.Run()` 이고
`.Schedule()`/`.ScheduleParallel()` 는 **0건**이다. 이 sim 은 이미 사실상 단일 스레드이므로
**재현해야 할 스케줄링 비결정성이 없다.** `[BurstCompile]` 42/44 중 실제 job 구조를 가진 것은
2시스템뿐이고 나머지는 어트리뷰트만 떼면 되는 `SystemAPI.Query` 순회다. 재작성 압력의 실체는
Burst 가 아니라 **ECB(28 사이트)·lookup·아키타입 쿼리**다.

### 골든의 지위가 이 unit 에서 격하된다

그림자 이식은 구 sim 코드를 한 줄도 건드리지 않으므로 **골든은 항상 초록이다** — 즉 units 12~18 의
"byte diff 0" 계약이 여기서는 **비회귀 증인**일 뿐 신 코드의 정확성에 대해 침묵한다. 이것을 인정하지
않으면 "골든 초록 = 이식 성공" 이라는 거짓 신호로 여러 커밋을 진행하게 된다.

**실제 중간 증인은 이미 있다**: 테스트 269파일 중 **`new World(` 조립 40파일**이 시스템 단위 행동
오라클이다. 정본 M1-5 의 "어서션만 salvage, 골격 재작성" 을 **"복제(어서션 동일)"** 로 바꾼다 —
재작성하면 그 순간 구 sim 의 오라클이 사라져 비교 기준 자체가 없어진다. 구 버전은 unit 20 스왑 때 삭제.

### 분할안 — 18-A ~ 18-K (규칙 클러스터 축)

폴더는 여전히 맥락을 따르고(쓰기 소유권 = 제약 2 후계 승계) **커밋 순서만** 클러스터로 바꾼다.

| 조각 | 내용 | 시스템 | 줄 | 증인 |
|---|---|---:|---:|---|
| **18-A** | `SimWorld` 저장소 + 틱 골격 (선택적 컴포넌트 표현 · `SimCommandBuffer` · 사망 4단계 릴레이 · P1~P12 빈 슬롯 · 내부 9채널) | 0 | — | 신규(ID 비재사용·지연적용·지연채널 순서) |
| **18-B** | 게이트 53 호출 → phase early-return | 0 | — | 게이트 진리표 39행 |
| **18-C** | 모디파이어 3단 (Apply→Tick→Aggregate) | 6 | 681 | `ModifierFrameworkTests` 등 |
| **18-D** | CC / DoT | 4 | 264 | `CcApplySystemTests` 등 4파일 |
| **18-E** | 필드·존·해저드·캐리어 | 8 | 708 | `ZoneApplyFactionGateTests` 등 5파일 |
| **18-F** | 어그로·AI·이동 | 5 | 744 | `MovementSystemTests` 등 5파일 |
| **18-G** | 피해·실드·사망 릴레이 | 7 | 877 | `UnitLifecycleSystemTests` 등 4파일 |
| **18-H** | 투사체 3종 | 3 | 1,081 | `ProjectileSystemTests` 등 5파일 |
| **18-I** | 공격 루프 (**재분할 권고**: I1 후보/타겟팅, I2 출력해결/발사) | 1 | **1,729** | `AttackSystemUnifiedLoopTests` 등 6파일 |
| **18-J** | 기믹·보스·임계·도약 | 9 | 1,171 | 오라클 얇음 — **테스트 부채 지점** |
| **18-K** | 통합 + 그림자 A/B 무장 | 0 | — | **여기서 처음 골든이 진짜 증인** |

합계 44 시스템 / 7,304줄 ✓ (HitFlash 49줄 discard 포함)

**시작 순서**: 스캐폴딩은 **18-A**(모든 조각이 의존, 위험 0), 규칙 이식은 **18-C**(인바운드 결합 최소 ·
관용구 밀도 최대 — ECB/EntityManager 혼용·중간 Playback·3중 AND 비대칭이 여기 모여 있어 18-A 설계를
최악 케이스로 즉시 시험한다 · 출력 `ModifierStats` 가 다운스트림을 가장 많이 연다).

### 중간 상태 보장 — 불변식 3개

"동작한다" 가 아니라 **"라이브 경로가 신 코드를 절대 만나지 않는다"** 로 보장한다.

| # | 불변식 | 집행 |
|---|---|---|
| **I1** | 18-A~18-J 의 어떤 커밋도 `Scripts/Battle/**`·`Scripts/Bridge/**` 를 수정하지 않는다 | 커밋별 `git diff --name-only`. **이것이 골든 byte diff 0 의 실제 근거다** — "돌려봤더니 같더라" 가 아니라 "건드린 파일이 없다" |
| **I2** | `Sim/{Units,Movement,Combat,Effects}/**` 를 부르는 프로덕션 코드 0 (`Sim/Match/**` 는 예외 — units 14~16 이 이미 라이브) | `SimEngineIndependenceTests` 확장 |
| **I3** | 신 sim 은 UnityEngine·Entities·Bridge 무참조 | `Wassup.Sim.asmdef` — 컴파일러 집행 |

유일한 예외는 18-K(`StepOneTick` tap). `#if UNITY_EDITOR` + `HarnessActive` 이중 게이트이고,
tap 은 `_harnessSimGroup.Update()` **바깥**에 건다 — 그룹 내부에 옵저버 시스템을 넣으면 44→45 가 되어
`order-capture.md` 의 "기대 44" 전제와 tie-break 정렬이 흔들린다.

### 🚨 18-A 이전에 처분해야 할 충돌 2건 (unit 20 에서 터진다)

골든 상태 해시(`BattleBridge.LegacyTrace.cs:228-289`)가 직렬화하는 축을 salvage discard 가 지운다:

1. **`HitFlash` discard → `LocalTransform.Scale` 소실.** `HitFlashSystem.cs:36,42` 가 Scale 을 쓰고
   `:264` 가 `LocalTransform` **전체**를 해시에 넣는다. 신 sim 이 뷰 연출을 재현하지 않으면 상태 해시가
   구조적으로 불일치한다(parity 기준은 상태 해시를 **exact** 로 규정).
2. **`ThreatEntry` discard → 버퍼 라인 소실.** `:279` 가 `AppendBuffer<ThreatEntry>` 를 넣는다.
   `ThreatTable.Leader` 의 호출처는 실측 0 이라 discard 판정 자체는 옳다.

### ✅ 결정 (사용자, 2026-08-05) — A/B 비교기가 두 축을 **명시 제외**한다

둘 다 권고안대로 처분한다. 신 sim 은 `HitFlash`(→`LocalTransform.Scale` 진동)와 `ThreatEntry` 를
재현하지 않고, **A/B parity 비교기가 그 축을 제외 목록으로 갖는다.** 골든 코퍼스는 건드리지 않는다.

근거: 두 값 모두 **아무도 읽지 않거나 뷰가 소유한다**. `HitFlash` 는 피격 연출이고(sim 판정에
기여 0), `ThreatTable.Leader` 는 호출처 실측 0 이다. "상태 해시에 실려 있다" 는 것은 그 값이
sim 상태라는 뜻이 아니라 **해시가 컴포넌트를 통째로 직렬화한다**는 뜻이다.

기각한 대안: 신 sim 이 "아무도 안 읽는 상태" 를 그대로 유지하는 것. parity 는 통과하지만 이식의
목적(뷰 소유 값을 sim 에서 빼내는 것)과 정면으로 어긋나고, M2 까지 죽은 필드를 끌고 간다.

집행 방법 — **제외는 코드에 적히고 로그로 드러나야 한다**:

- 비교기가 제외 축을 **상수 목록으로 선언**한다(주석이 아니라 데이터). 목록 밖 필드가 불일치하면
  기존대로 실패다 — 제외가 "해시 비교를 느슨하게" 로 번지지 않게 한다.
- 매 실행 **제외한 축을 로그로 찍는다**. 조용한 제외는 parity 초록을 거짓으로 만든다.
- 제외 목록에 항목을 **추가하는 것은 unit 20 의 완료 기준을 낮추는 것**이므로, 추가 시 이 문서에
  근거(누가 읽는가·왜 sim 상태가 아닌가)를 함께 적는다.
- `LocalTransform` 은 **필드 단위 제외**다(`Scale` 만) — 위치·회전은 exact 축으로 남는다.
  컴포넌트 통째 제외는 이동 회귀를 통째로 눈감는다.

### 파생 계약 — 상태 해시 필드 이름 승계 (18-A 필수 입력)

포매터(`BattleBridge.LegacyTrace.cs:340-341`)가 **public 필드를 ordinal 이름순 정렬**해 직렬화한다.
따라서 신 sim 의 대응 struct 는 아래 21 타입에 대해 **public 필드 이름·타입·개수를 그대로 승계**해야
상태 해시가 일치할 수 있다 — 이름을 "개선" 하는 순간 A/B 는 영구히 불가능해진다.

- 컴포넌트 11: `LocalTransform` `Health` `FactionTag` `KillScore` `DefenderTile` `PathFollowState`
  `AttackState` `ModifierStats` `ProjectileState` `BombLauncherState` `PickupSpawnState`
- 버퍼 10: `PatternSlot` `CcEffect` `DotEffect` `StatModifierSlot` `StackModifierSlot` `ThreatEntry`
  `ShieldSlot` `IncomingDamage` `IncomingHeal` `IncomingShield`

### 문서 드리프트 정정

- `m1_salvage_matrix.md:69` 집계 오기: conform **6**·adapt **32**(합계 44 동일). 행 전수 = #7·#12·#14·#16·#29·#30.
- `m1_data_inventory_gates.md` A-6 의 `FlowFieldSingleton` "8 시스템" → 실측 **9**.
- 게이트 수 표기: `RequireForUpdate<` **선언 파일** 35 · **호출** 53 · **보유 시스템** 39(무게이트 5).
  성격별 번역: 채널 부재 14(**자동 소멸 — 명시 기록 대상**) · 인프라 13 · 기믹 config 7 · 콘텐츠 19.
  자명하지 않은 4건은 전부 `RequireAnyForUpdate` — **AND 로 오번역 금지**(`TauntAttackGrantSystem.cs:26`
  `AggroStateSystem.cs:29` `EffectTickSystem.cs:21` `UnitLifecycleSystem.cs:39`).
