# unit 18 실행 계획 — 맥락 이식 본체

> rev 2 (2026-08-05) — critic 리뷰 REWORK 반영. 초판이 틀렸던 것은 분해가 아니라 **분해 주변의
> 보증**이었다: I1 이 달성 불가였고, I3 는 이미 집행 중이라고 잘못 적었고, 증인 40파일을 재측정
> 없이 물려받았고, 스코프가 5,535줄 모자랐다. 아래는 그것을 고친 판이다.
>
> 계약과 정찰 결과는 [`18_context_port.md`](18_context_port.md), 설계 정본은
> [`m1_blueprint_tick_pipeline.md`](m1_blueprint_tick_pipeline.md) 가 소유한다. 이 문서는
> **순서·증인·중단 기준**만 담고 새 설계 결정을 하지 않는다.

## 왜 별도 계획인가

**이 unit 에서는 골든이 증인이 아니다.** 그림자 이식은 구 sim 코드를 한 줄도 건드리지 않으므로
골든은 **항상 초록**이다. 이것을 인정하지 않으면 "골든 초록 = 이식 성공" 이라는 거짓 신호로
여러 커밋을 진행하게 된다. 그래서 증인을 따로 설계해야 하고, 그 설계가 이 문서의 본론이다.

## 스코프 (rev 2 정정)

| 축 | 초판 | 실측 |
|---|---:|---:|
| 시스템 몸체 | 7,304줄 | 7,304줄 ✓ |
| 데이터 타입 + 유틸 | **제외** ("unit 17 이 가져간다") | **5,535줄 — unit 17 은 하나도 안 가져갔다** |
| 합계 | 7,304 | **12,839** |
| 데이터 타입 | 117 | **118** (`IComponentData` 97 + `IBufferElementData` 21) |

초판은 `18_context_port.md:72` 의 *"후자의 상당분은 unit 17 이 가져간다"* 를 물려받았는데,
unit 17 은 **코드 변경 0** 으로 끝났고(README 진행표) `Sim/Lib/` 에는 11파일뿐이다. 지목됐던
conform 유틸(`ModifierMath`·`CcEffectMerge`·`DotEffectMerge`·`KillAttribution`·`AggroPolicy`·
`TileAoe`·`GridMath`·`MovementCellTrim`·`ShotOrder`·`HeatMath`)은 **전부 아직 `Scripts/Battle/`
아래**다. unit 17 자신의 드리프트 노트(`17_sim_lib_skeleton.md`)가 그 전제를 이미 철회했었다.

⇒ 각 조각은 자기 클러스터가 쓰는 **데이터 타입·유틸을 함께** 가져간다. 세션 수는 그것을 포함해 잡는다.

## 확정된 전제 (재론 금지 — 근거는 18 문서)

| # | 전제 | 근거 |
|---|---|---|
| P1 | 분할 축은 **규칙 클러스터**. "맥락 의존 역순 4단계" 는 폐기 | 반증 3건. **critic 이 재검증해 구조 수치 전부 일치**(44시스템 · 791/332/3688/2493 · 클러스터 합 7,304) |
| P2 | 시작은 **18-A** → **18-C** | 단 그 근거는 아래 P2' 로 정정 |
| P2' | 18-C 가 먼저인 이유는 "인바운드 결합 최소" 가 **아니다** — 그건 사실과 반대다(`StatModifierApply` 10 producer + `StackModifierApply` 3 = **26쌍 중 13이 18-C 로 들어온다**, 채널 그래프 최대 fan-in). 진짜 이유는 **관용구 밀도**(ECB/EntityManager 혼용 · 중간 Playback · 3중 AND 비대칭)와 **출력 `ModifierStats` 가 하류를 가장 많이 연다**는 것 | `m1_blueprint_tick_pipeline.md` producer 목록 |
| P3 | 폴더는 계속 맥락을 따른다(제약 2 후계). **커밋 순서만** 클러스터 축 | — |
| P4 | 재현할 **스케줄링 비결정성이 없다** — `.Schedule()`/`.ScheduleParallel()` 0건, `IJobEntity` 3개 전부 `.Run()` | 실측 · critic 재검증 |
| P5 | 상태 해시 **제외 축 2건**(`LocalTransform.Scale` · `ThreatEntry`) | 사용자 결정. 집행 조건은 [`20_ab_parity_swap.md`](20_ab_parity_swap.md) |
| **P6** | **트레이스 키 승계** — 필드 이름·타입·개수**만으로는 부족하다**. 아래 §"트레이스 키 계약" 이 정본 | rev 2 신설 |

### 트레이스 키 계약 (P6 — rev 2 에서 확장)

초판 P6 은 *"public 필드를 ordinal 이름순 정렬해 직렬화하므로 필드 이름을 승계하라"* 였다.
실측하면 **포매터는 타입 이름도 박는다**(`Bridge/BattleBridge.LegacyTrace.cs`):

- `:293` `AppendStateLine(sb, typeof(T).FullName, …)` — **라인 키가 정규화 타입명**
- `:300` `sb.Append(typeof(T).FullName).Append("[")` — 버퍼도 동일
- `:344` `sb.Append(type.FullName ?? type.Name).Append('{')` — **중첩 struct 값마다 FullName**
- `:331` `if (value is Entity entity) return "sim:" + ResolveLegacyTraceEntity(entity)`

⇒ 신 sim 이 자기 타입으로 `typeof(T).FullName` 을 찍으면 **키가 통째로 달라진다.** 특히
`Unity.Transforms.LocalTransform` · `Unity.Mathematics.float3` · `Unity.Mathematics.Random` 은
`noEngineReferences` 어셈블리가 **만들 수 없는 이름**이다.

**18-A 의 산출물로 "레거시 키 매핑표" 를 만든다** — 21 타입 각각에 대해 ① 라인 키 문자열
② 중첩 값 타입의 FullName 문자열 ③ `Entity`→`sim:N` 렌더 규칙. 신 emitter 는 리플렉션이 아니라
**그 문자열을 그대로 쓴다**. 이걸 unit 20 에서 발견하면 되돌릴 반경이 7,000줄이다.

## 조각 — 18-A ~ 18-L

| 조각 | 내용 | 시스템 | 몸체 줄 | 증인 | salvage | 위험 |
|---|---|---:|---:|---|---|---|
| **18-A** | `SimWorld` + 틱 골격 + **asmdef 배치** + **키 매핑표** + **config 주입면** | 0 | — | 신규 4계약 | — | **설계 위험 최대** |
| **18-B** | 게이트 53 호출 → phase early-return | 0 | — | 진리표(**신규 저작**) | — | `RequireAnyForUpdate` 4건 AND 오번역 |
| **18-C** | 모디파이어 3단 | 6 | 681 | `ModifierFrameworkTests` 등 | adapt | 26쌍 중 13의 수신부 |
| **18-D** | CC / DoT | 4 | 264 | 4파일 | adapt | duration 병합 **비대칭 보존** |
| **18-E** | 필드·존·해저드·캐리어 | 8 | 708 | 얇음(아래) | **rewrite 1** | `HazardLifetime` 재작성이 **tie-break ⑥ 을 바꾼다** |
| **18-F** | 어그로·AI·이동 | 5 | 744 | 5파일 | adapt | AggroHit 구조적 영구 1틱 지연 |
| **18-G** | 피해·실드·사망 릴레이 | 7 | 877 | 얇음(5 assert) | **rewrite 1** | 사망 4단계 릴레이 — 아래 ⚠ |
| **18-H** | 투사체 3종 | 3 | 1,081 | 5파일 | adapt | 궤적/페이로드 2축 |
| **18-I1/I2** | 공격 루프 (후보·타겟팅 / 출력해결·발사) | 1 | **1,729** | 6파일 | adapt | 단일 파일 최대. 동률 tie-break |
| **18-J** | 기믹·보스·임계·도약 + **`_meteorRng`** | 9 | 1,171 | **최다 결손** | rewrite 2 | 테스트 부채 |
| **18-K** | **P0/P13 흡수** + 통합 + 그림자 A/B 무장 | 0 | — | **여기서 처음 골든이 진짜 증인** | — | I1·I2 의 예외 |
| **18-L** | **합류 항목 4건** (아래) | 0 | — | **골든 byte diff 0**(라이브 코드라 진짜 증인) | — | I1 **밖** |

⚠ **`UnitLifecycle` 의 salvage 등급은 무효다.** `m1_salvage_matrix.md` 가 *"신 sim 은 객체 제거가
즉시"* 를 근거로 `rewrite` 로 매겼는데, 청사진 ③ 은 **1틱 "죽었지만 아직 있는" 창을 핀 3개로
의무화**한다. 청사진이 이긴다(이 unit 은 새 설계를 하지 않는다). 그 철회를 salvage 표에도 적어야
다음 사람이 즉시 삭제로 구현하지 않는다.

⚠ **`HazardLifetime` 재작성 범위 제한.** 매 틱 `NativeParallelMultiHashMap` 재구축이 tie-break
예외 ⑥(`HazardSingleton` 셀 순회)의 뿌리다. 증분 인덱스로 바꾸면 **순회 순서가 바뀌고 그것은
exact parity 축**이며 P5 제외 목록에 없다. 재작성은 자료구조만, **순회 순서는 보존**한다.

### 18-L — 합류 항목 (I1 밖, 라이브 코드)

| 출처 | 항목 | 왜 I1 밖인가 |
|---|---|---|
| 17-F | `Vector2Int`→`int2` · `GeneratedMap`→plain · `SpawnEntry` · `GeneratedWavePlan` | `BattleBridge._occupiedTiles` 를 함께 바꿔야 한다(`Bridge/BattleBridge.cs:229` + 9 사용처) |
| 16-F | eval↔bake 수동 동기화 접기 | `BattleBridge.Dreamcatcher.cs:243`/`:734` 재작성 |
| 15 | `ApplyOnPlaceEffect` 8분기 | `BattleBridge.cs:4339` |
| 15 | `MatchPlacementRules`·`MatchWaveSchedule` 졸업 | 위 엔진 타입 4종에 걸려 있다 |

초판은 이 넷을 "합류 항목" 으로 나열해 놓고 **어느 조각에도 배정하지 않았고**, 동시에 I1 이
`Scripts/Bridge/**` 수정을 금지했다. 두 진술이 양립하지 않는다 — 그것이 critic 의 H1 이다.
18-L 은 **맨 마지막**(18-K 이후)에 둔다: 라이브 코드를 바꾸므로 골든이 진짜 증인이고,
그림자 구간(A~J)의 I1 을 오염시키지 않는다.

## 불변식 3개 (rev 2 정정)

| # | 불변식 | 집행 |
|---|---|---|
| **I1** | **그림자 조각 18-A~18-J** 의 어떤 커밋도 `Scripts/Battle/**`·`Scripts/Bridge/**` 를 수정하지 않는다. **예외 2개: 18-K**(tap) · **18-L**(합류 항목) | 커밋별 `git diff --name-only`. **이것이 골든 byte diff 0 의 실제 근거다** — "돌려봤더니 같더라" 가 아니라 "건드린 파일이 없다" |
| **I2** | `Sim/{Units,Movement,Combat,Effects}/**` 를 부르는 프로덕션 코드 0. **예외: 18-K**(그림자 무장) | 신규 검출기가 필요하다 — 현 `SimEngineIndependenceTests` 는 *sim 이 무엇을 참조하나* 를 보지 *누가 sim 을 부르나* 를 안 본다. "확장" 이 아니라 **신설** |
| **I3** | 신 sim 은 UnityEngine·Entities·Bridge 무참조 | ⚠ **아직 집행되지 않는다.** `Wassup.Sim.asmdef` 는 `Sim/Lib/` 에만 걸려 있고, 새 폴더는 `Wassup.Runtime`(=`Unity.Entities` 참조, `noEngineReferences:false`) 소속이 된다. **18-A 가 asmdef 배치를 해결해야 그때부터 집행된다** |

I3 보충 — 텍스트 게이트는 그동안 **부분적으로** 막아준다: `SimEngineIndependenceTests` 는
`using Unity.Entities`·`Unity.Collections` 와 그 정규화 참조도 본다(critic 은 UnityEngine 만
본다고 했으나 그건 부정확하다). 다만 그것은 **스테이징 층 스캔**이라 새 폴더를 자동으로 덮지
않으므로, 18-A 가 asmdef 를 옮기기 전까지는 스캔 경로를 새 폴더로 넓혀 둔다.

## 증인 전략

1. **골든** = 비회귀 증인만. 실제 근거는 I1(파일을 안 건드렸다)이고 실행 결과는 그 확인이다.
2. **`new World(` 조립 40파일 중 실제 클러스터 오라클은 약 30이다.** 초판은 40을 재측정 없이
   물려받았다. 제외해야 할 것:
   - **복제 불가(설계상)**: `ThreatTableTests`(unit 18 이 discard 하는 축을 단정) ·
     `BattleScaledRateManagerTests`(salvage **discard**) · HitFlash 인접분.
     **이 목록을 미리 선언한다** — 안 그러면 아래 중단 기준 ①이 *의도된* discard 에서 오발한다.
   - **순수 함수 테스트(시스템 오라클 아님)**: `KillAttributionTests`(자기 주석이 *"시스템 실행은
     불필요"* 라고 적었다) · `ShieldMathTests` · `DotEffectMergeTests` · `PatternBakeTests`.
     이들은 유틸과 함께 이주하지 이식을 증인하지 않는다.
   - **클러스터 밖**: `BattleBridgeDraftMapTests` · `NextWaveClearReadyTests` ·
     `OutcomeRulesDeckResolutionTests` · `PlacementCooldownGateTests`(이미 졸업한 규칙).
3. **오라클 0 인 시스템이 존재한다** — critic 실측:
   18-C `FatigueAccrual`·`MaxHealthScale` / 18-E `LastRun`·`HazardLifetime`·`AllyBuffField`·
   `DefenderField` / 18-G `LethalTimer`·`ShieldCast`·`ResignationDrop` / 18-J
   `ResignationThreshold`·`HeatAccrual`·`PickupSpawn`·`PickupConsume`·`UltimateLeap`·`BlinkApply`.
   **"18-J 만 얇다" 는 초판의 오판이다** — 18-E 는 결손이 `rewrite` 등급 위에 얹혀 더 나쁘고,
   18-C 는 **첫 이식 조각인데도** 결손이 있다.
4. ⇒ **조각별 게이트**: 그 클러스터에서 오라클 0 인 시스템은 **구 sim 에 먼저 특성화 테스트를
   붙여 초록을 확인**한 뒤 신 sim 에 복제한다. 구 sim 에 먼저 붙이지 않으면 오라클이 아니라
   자기 확인이다. 이 순서는 18-J 전용이 아니라 전 조각 규칙이다.
5. **어서션만 salvage 가 아니라 복제(어서션 동일)** — 재작성하면 그 순간 구 sim 의 오라클이
   사라져 비교 기준 자체가 없어진다. 구 버전은 unit 20 스왑 때 삭제한다.
6. **18-A 가 정본 증인 명세를 만든다**: 파일 → 클러스터 → 테스트 수 → 어서션 수 → 복제 가능 여부.
   위 3번의 이름 목록은 critic 실측이고, 정본 수치는 18-A 가 재측정해 게시한다.
7. **PlayMode 잔여 16건은 18 착수 전 정리 대상**이다. 중간 증인이 `new World(` 군인데 그 이웃이
   빨간불이면 이식 회귀와 기존 파손을 구분할 수 없다.

## 소유자 없는 항목 (rev 2 에서 배정)

| 항목 | 초판 | rev 2 |
|---|---|---|
| **P0**(커맨드 반입·`SkillRuntime` tick·Bridge 프레임) · **P13**(도약 드레인·읽기 모델 스탬프) | 없음 — 18-A 는 `P1~P12` 만 | **18-K**. 청사진이 *"신 sim 의 `Sim.Tick` 은 P0 의 Bridge 몫을 흡수한다"* 고 못박았다 |
| **`_meteorRng`** — 상태 해시에 실린다(`LegacyTrace.cs:246`) | 없음 | **18-J**(생산자 `ResignationThreshold` 와 같은 조각) |
| **config 주입**(`StackThresholdRegistry` · `MatchConfig` 물질화 · 유닛 스탯 bake · 파생 시드) | 없음 | **18-A 의 4번째 계약** — 그림자 sim 이 생성 시 스냅샷 struct 를 받는다. 없으면 `StackModifierTick`(18-C, S2)이 **6세션 동안 조용히 no-op** 이다(미등록 kind → 빈 배열 = "규칙 없음") |
| **26쌍 진리표** | 없음 | **18-A 산출물**. 청사진의 `같은 틱 12 / 1틱 지연 14` 는 sim-내부만 세면 11/15 로 어긋난다(12번째 same-tick 은 Bridge `OnPlace` = 18-L 항목). **먼저 조정한 뒤** 쌍당 테스트 1개 |

## 세션 분할 (rev 2 — 8 → 12)

| 세션 | 조각 | 종료 조건 |
|---|---|---|
| S1 | 18-A | 4계약(ID 비재사용 · 지연적용 · 채널 순서 · **키 매핑표**) + asmdef 배치 + config 주입면 |
| S2 | 18-B + 18-C | 진리표 초록 · 모디파이어 오라클 복제 초록 · **성능 프로브**(아래) |
| S3 | 18-D | CC/DoT 비대칭 |
| S4 | 18-E | 특성화 4건 선행 + `HazardLifetime` 순회 순서 보존 |
| S5 | 18-F | 이동·어그로 |
| S6 | 18-G | 사망 4단계 릴레이(핀 3개) + 특성화 3건 |
| S7 | 18-H | 투사체 3종 |
| S8 | 18-I1 | 후보·타겟팅 |
| S9 | 18-I2 | 출력해결·발사 |
| S10 | 18-J | 특성화 6건 + `_meteorRng` |
| S11 | 18-K | P0/P13 + 통합 + 그림자 무장 → 여기서 처음 골든이 진짜 증인 |
| S12 | 18-L | 합류 4건 — **골든 byte diff 0 로 판정** |

초판이 S3(D+E)·S4(F+G)를 묶은 것은 자기 수치와 안 맞았다 — 그 두 세션이 S6(공격 루프)보다
시스템 수·계약 밀도·테스트 부채가 컸다. 데이터 타입 5,535줄도 어디에도 없었다.

각 세션은 인계를 누적한다(`m1_unit18_handoff.md`). **조각 중간에서 세션이 끊기면 커밋하지 않는다**
— I1 이 깨진 채 남으면 안 된다.

## 중단 기준 (rev 2 — 선행 지표로 교체)

- ① **오라클 복제가 불가능한 조각이 나오면 그 조각에서 멈춘다.** 단 §증인 2의 **복제 불가 선언
  목록**은 예외다(의도된 discard 에서 오발 금지).
- ② **18-A 의 4계약 중 하나라도 테스트로 고정할 수 없으면 18-C 로 넘어가지 않는다.**
  스캐폴딩 결함은 뒤 조각 전부를 오염시킨다.
- ③ **18-C 가 18-A 의 저장소/채널 표현을 바꾸도록 강제하면, 18-A 를 재설계하고 D 이후를
  재계획한다 — 누적 금지.** 18-C 를 먼저 두는 이유가 이 신호를 일찍 받는 것이다.
- ④ **성능 프로브를 S2 끝으로 당긴다.** 18-C 이식 직후 EditMode 에서 합성 모디파이어 틱
  1만 회를 양쪽에 돌려 비교한다. 관리 컬렉션 비용은 거기서 이미 측정된다.
  **S11 에서 처음 알면 되돌릴 반경이 7,000줄이다.**
- ⑤ **커버리지 기준**: 어느 조각이든 오라클 0 시스템이 절반을 넘으면, 이식 **전에** 특성화
  테스트를 먼저 쓴다(카운트가 아니라 비율로 본다).

## 착수 전 사용자 확인

1. **`Unity.Mathematics` 유지 여부 — S1 전에 닫아야 한다(초판은 "착수 전" 으로 뭉뚱그렸다).**
   unit 17 결정 (a) 는 조건부였고, 18 은 `float3` 를 들인다. `implicit operator Vector3` 가
   오버로드 해석에 들어와 **CS0012** 가 나면 (a) 를 접어야 하는데, 그 순간 **P6 의 키 매핑표가
   통째로 바뀐다**(`Unity.Mathematics.float3` → 자체 타입명). 반경: `Battle/` 안
   `using Unity.Mathematics` 98파일.
2. **PlayMode 16건을 18 착수 전에 정리할지.** 증인 전략 7번의 근거로는 정리가 맞지만 spec 범위 밖.
3. **18-K 의 A/B 비교기가 `GameManager.CurrentPhase`·`CostRuntime.Current` 를 어떻게 다룰지.**
   `LegacyTrace.cs:238-243` 이 그것을 상태 라인에 넣는데 그림자 sim 은 둘 다 없다 — 비교기가
   공급할지 제외할지가 **18-A 의 읽기 모델을 제약**한다.

## 아직 확정하지 않은 것 (설계는 청사진 ③ 이 소유)

phase 배치 · 내부 채널 26쌍의 같은틱/1틱-지연 · 사망 4단계 릴레이 · ECB "루프 중 기록, 루프 후
적용" · RNG write-back — 전부 `m1_blueprint_tick_pipeline.md` 에 있다. **이 계획은 그것을 다시
결정하지 않는다.**
