# unit 18 세션 인계 — S1 종료 시점

> 2026-08-05 · HEAD `8f028dcf` · 이 문서는 **지도**다. 계약은 [`m1_unit18_plan.md`](m1_unit18_plan.md),
> 설계는 [`m1_blueprint_tick_pipeline.md`](m1_blueprint_tick_pipeline.md) 가 소유한다.
> 세션마다 이 문서 끝에 덧붙인다.

## Commit

| 해시 | 내용 |
|---|---|
| `4e6e1c59` | 18-A/1 — sim 자체 수학 + 비트 동일 게이트 |
| `40f590b5` | asmdef 배치 확정 + Bridge 해체 백로그 등재 |
| `2eeb1fdf` | 18-A/2 — `SimWorld` 저장소 + 틱 골격 (계약 ①②③) |
| `beb5931d` | 18-A/3 — 레거시 트레이스 키 박제 (계약 ④) |
| `17f1e5b0` | 18-A/4 — config 주입면 |
| `77752f41` | 18-C/1 — 모디파이어 어휘·산식 + 차등 오라클 |

## Implemented

- **18-A 완료.** 계획서 중단 기준 ②("4계약 중 하나라도 못 고정하면 18-C 로 안 넘어간다") 충족.
- `Sim/Lib/Math/` — `SimVec3`·`SimVec2`·`SimInt2`·`SimMath`(13함수)·`SimRandom`(xorshift32).
  `Unity.Mathematics` **무참조**. `Wassup.Sim.asmdef` references 는 `[]` 유지.
- `Sim/Lib/Core/` — `SimEntityId`·`SimWorld`·`SimCommandBuffer`·`SimChannel<T>`·`SimTick`·`SimConfig`.
- `Sim/Lib/Effects/` — 모디파이어 어휘 4 enum + 슬롯 4 struct + `SimModifierMath`·`SimModifierAuthoring`.
- 신규 테스트 36건. EditMode 전체 **2014 통과 / 실패 0 / skip 1**.

## 다음 조각 — 18-C 시스템 몸체 6개 (681줄)

어휘·산식은 깔려 있다. 남은 것은 시스템 본문이다.

| # | 시스템 | phase | 오라클 |
|---|---|---|---|
| 9 | `ModifierApplySystem` (203줄) | P2 Intake | `ModifierFrameworkTests` 등 |
| 28 | `FatigueAccrualSystem` | P7 | **없음 — 특성화 선행** |
| 29 | `StatModifierTickSystem` (50줄) | P7 | `ModifierFrameworkTests` |
| 30 | `ModifierStatsAggregateSystem` (127줄) | P7 | `ModifierFrameworkTests` |
| 31 | `MaxHealthScaleSystem` | P7 | **없음 — 특성화 선행** |
| 32 | `StackModifierTickSystem` (177줄) | P7 | 스택 계열 |

오라클 0 인 둘은 **구 sim 에 먼저 특성화 테스트를 붙여 초록을 확인한 뒤** 신 sim 에 복제한다.
신 코드에 먼저 붙이면 자기 확인이지 오라클이 아니다.

## Notes — 되돌리면 안 되는 의도

**설계**

- **채널의 같은틱/1틱-지연에 장치를 만들지 말 것.** `SimChannel` 은 평범한 FIFO 이고, 지연은
  **phase 순서에서 파생**된다(소비자가 생산자보다 앞이면 자동으로 다음 틱). 플래그로 만들면
  phase 순서와 플래그가 두 개의 진실이 되고, `StatModifierApply` 처럼 한 채널에 같은틱 3 + 지연 7
  생산자가 공존하면 애초에 표현이 안 된다. ⇒ 26쌍은 **테스트 행렬**이지 구현 장치가 아니다.
- **순회는 생성 순서.** 사전 순회로 바꾸면 동률 판정이 런타임마다 달라진다.
- **`SimWorld.Destroy` 는 P12 만 부른다.** 사망 4단계 릴레이의 1틱 창이 사라지면 사직서 드랍·
  순찰병 전파·DefenderDeath 베이크가 전부 깨진다.
- **부재 ≠ 빈 버퍼.** `GetBuffer` 가 자동 생성하지 않는다(`DamageApplication` 게이트가 부재만 본다).
- **`SimConfig` 는 생성자 필수 인자.** 기본값 경로를 만들면 배선 누락이 "규칙 없음"으로 위장한다.

**이식 함정 (18-C 에서 바로 만난다)**

- **`StatModifierTick` 을 dirty 로 좁히지 말 것.** 한때 그렇게 쿼리했다가 만료가 영영 안 오는
  버그가 났고 그 주석이 구 코드에 남아 있다. 모든 슬롯 보유자를 훑는다.
- **슬롯 제거는 `RemoveAtSwapBack` + 역순 순회**다. 안정 제거로 바꾸면 순서가 달라진다.
- **enum 은 append-only.** 상태 해시가 enum 을 정수로 찍는다 — 재정렬/중간 삽입이 해시를 바꾼다.
- **`CombineOp` 기본값(0)이 `Multiplicative`.** `op` 를 안 채운 생산자가 곱셈으로 들어간다
  (EffectTile 이 그 경로 — `PlacementAuraTest` ×1.2 의 정체). **재현 대상이지 고칠 것이 아니다.**
- **`FromMultiplier` 경계는 `>= 1f`.** `> 1f` 면 배율 1.0(revoke 중립화의 항등 refresh)이
  Multiplicative 로 가서 슬롯이 갈린다.
- **신 emitter 는 자기 타입명을 찍으면 안 된다.** 트레이스 키는 `Unity.Mathematics.float3` 같은
  **구 FullName** 을 그대로 써야 한다(`LegacyTraceKeyContractTests` 가 박제).

## Notes — 운영 함정 (문서에 없던 것)

- **신규 파일은 Unity 임포트 전까지 `dotnet build` 가 CS0246 을 낸다.** 생성된 csproj 는 명시
  파일 목록이라 새 파일이 없다. 빠른 사전 검증을 하려면 `<Compile Include>` 를 손으로 끼워 넣거나,
  그냥 Unity Refresh 를 먼저 돌린다. **모르면 "내 파일이 컴파일 안 된다"로 한 사이클 이상 태운다.**
  파일이 어느 어셈블리인지 주의 — `Sim/Lib/**` 는 `Wassup.Sim.csproj` 다(`Wassup.Runtime` 아님).
- **PlayMode 를 돌린 세션은 골든 전에 `ReimportData` 를 넣는다.** PlayMode 가 SO 를 메모리에서
  변조해 `configHash` 가 흔들린다. 도메인 리로드로는 안 된다(에셋 인스턴스가 리로드를 넘어 산다).
- **러너 트리거의 함정 4개** (헬퍼 주석에 적혀 있다):
  ① 결과 파일에 **BOM** 이 있다 — `utf-8` 로 읽으면 `startswith("RUNNING")` 이 항상 거짓
  ② 트리거 직후 결과 파일은 **직전 실행 결과**다 — mtime 추적 없이 읽으면 stale 을 완료로 오독
  ③ **`Golden` 은 결과 파일을 쓰지 않는다** — 완료 신호는 에디터 로그의 `[LegacyTrace] PASS`
  ④ **Refresh 타임아웃 ≠ 실패** — 바뀐 게 없으면 도메인 리로드가 없다. 로그의 `error CS` 로 가른다
- **골든 판정은 재생성 후 `git diff Assets/_Project/Tests/Golden/` 이 비는 것**이고, 판정 뒤
  `git checkout --` 로 되돌린다. 코퍼스 커밋 권한은 **unit 19** 뿐이다.

## Verified

- EditMode **2014 / 실패 0 / skip 1**.
- 골든 7종 two-run diff 0 + 커밋본과 byte 동일 (18-A 착수 전 마지막 라이브 변경 시점 기준).
  **18-A~18-J 는 골든이 증인이 아니다** — I1 이 실제 근거다.
- **I1**: 18-A~18-C/1 전 커밋에서 `Scripts/Battle/**`·`Scripts/Bridge/**` 수정 **0건**(커밋별 확인).
- PlayMode 전체 **75 통과 / 16 실패**. 16건은 **이 spec 이 만든 파손이 아니다**(아래).

## Follow-up

**열어둔 판단 2개**

1. **18-B 를 18-C 뒤로 미룰지.** 18-B 의 증인인 "게이트 진리표 39행" 은 **존재하지 않는다** —
   지금 저작하면 이식할 코드를 읽어 만든 자기 확인이 된다. 18-C 를 옮기며 게이트를 실물로 만나면
   표를 *동작*에서 뽑을 수 있다. 권고는 **미루기**.
2. **PlayMode 16건을 18 이 깊어지기 전에 정리할지.** 18 의 중간 증인이 `new World(` 조립 군인데
   그 이웃이 빨간불이면 이식 회귀와 기존 파손을 구분하기 어렵다. spec 범위 밖이라 별도 결정.

**PlayMode 16건의 성격** (2026-08-05 실측)

- 환경 1: `AuthE2ETest`(개발 DB `user_name` 중복)
- 스위트 내 상태 누출 3: `PlacementAuraTest` ×3 — **단독은 3/3 통과**. 전체 실행에서만 실패하고,
  `TestModeContext.RuntimeImportsBlocked` 가 static 인데 PlayMode 가 테스트 간 도메인 리로드를
  하지 않는 축이 남아 있다는 신호다.
- 미진단 12: Dreamstone/Squad/Deck carry-in 5 · Dreamcatcher 4 · DragCancelZone · DropDismount ·
  SceneTransition · BountyMark.

**이 세션에서 수리한 것** — 참고로 남긴다: `PlacementAuraTest` ×3(맵 EffectTile 회피) ·
재배치 ×4(15-A 가 닫은 뷰 우회 경로를 쓰고 있었다). 후자의 교훈은 **배치 판정을 건드리는 unit 은
배치를 덮는 PlayMode 군을 함께 돌린다** — 골든은 이 회귀를 못 잡는다(하네스는 유닛 타입마다
1회만 배치한다).

**계획서 rev 2 가 남긴 미결** — `m1_unit18_plan.md` 의 §"착수 전 사용자 확인" 3번:
A/B 비교기가 `GameManager.CurrentPhase`·`CostRuntime.Current` 를 어떻게 다룰지. 그림자 sim 은
둘 다 없는데 `LegacyTrace.cs:238-243` 이 상태 라인에 넣는다. **18-K 의 읽기 모델을 제약**한다.

---

# S2 (2026-08-05) — 18-C 특성화 선행

## Commit

| 해시 | 내용 |
|---|---|
| `10a9cbc7` | 폴더 meta 2개 편입 + 인계 HEAD 정정 (코드 변경 0) |
| `f57c80e8` | 18-C/2 — 오라클 0 시스템 2개에 특성화 선행 (테스트만) |

## Implemented

- **`10a9cbc7`** — `Sim/Lib/Core.meta`·`Effects.meta` 가 untracked 로 남아 있었다.
  `2eeb1fdf`·`77752f41` 이 경로 명시 스테이징에서 `.cs.meta` 만 잡고 **폴더 meta** 를 놓친 것.
  형제(Contracts/Match/Math)는 추적 중이었으므로 이 둘만 구멍이었다.
- **`f57c80e8`** — `FatigueAccrualSystemTests` 6건 + `MaxHealthScaleSystemTests` 7건.
  계획서 §증인 4의 "구 sim 에 먼저 붙여 초록 확인" 집행. 각 픽스처는 **대상 시스템 하나만**
  월드에 올린다(상·하류를 끼우면 clamp·dirty·채널 지연이 섞여 계약이 흐려진다).
- 박제한 것은 **산식이 아니라 시스템 골격**이다. `Health.ScaleMax` 는 `HealthScaleMaxTests`
  6건이 이미 덮으므로 재검증하지 않았다 — 이식이 갈리는 자리는 lazy attach 조건 · baseMax
  캡처 시점 · `appliedMul` 래치 · `mul<=0` 가드 · **중간 Playback**(부착과 적용이 같은 프레임)이다.

## 다음 조각 — 18-C 시스템 몸체 6개 (변동 없음)

오라클 게이트는 이제 6/6 이 채워졌다(4개는 `ModifierFrameworkTests` 계열, 2개는 위 신규).
남은 것은 이식 본문과 **S2 끝의 성능 프로브**(중단 기준 ④ — 합성 모디파이어 틱 1만 회 A/B).

## Notes — S1 함정 목록의 정정 1건

- **러너 결과 파일에 BOM 은 없다.** S1 이 적은 운영 함정 ①은 현 러너(`RunnerVersion = "4-reimport"`)
  에서 **사실이 아니다** — `SimTestAutoRunner.Write` 가 `new UTF8Encoding(false)` 로 쓴다
  (실측: 첫 4바이트 `53 54 41 54` = `STAT`). 방어적 `TrimStart(U+FEFF)` 는 무해하지만 불필요하다.
  나머지 3함정(② stale 결과 · ③ `Golden` 은 결과 파일 미기록 · ④ Refresh 타임아웃 ≠ 실패)은
  **그대로 유효하다** — 이 세션도 ②를 mtime 추적으로, ④를 신분증 파일 mtime 변화로 갈랐다.

## Verified

- 신규 13건 **13/13**.
- 전체 EditMode **2027 통과 / 실패 0 / skip 1**. S1 기준선 2014 + 13 = 2027 로 정확히 일치
  (skip 1 은 기존 `ModifierFrameworkTests` Test 4 의 `[Ignore]`).
- I1 유지 — 두 커밋 모두 `Scripts/Battle/**`·`Scripts/Bridge/**` 수정 0.

## Follow-up

- **S1 이 열어둔 판단 2개는 그대로 열려 있다** — 18-B 미루기(권고 유지) · PlayMode 16건 정리.
- 특성화 2건은 **변이 검증을 하지 않았다.** 어서션이 값-특정적이라 공허하지는 않지만,
  구 sim 을 일부러 깨뜨려 빨간불을 확인하려면 `Scripts/Battle/**` 을 건드려야 해서 I1 과
  공유 워크트리 위생 양쪽에 걸린다. **이식 시점에 신 sim 쪽에서 확인**하는 것이 맞다 —
  신 코드가 이 어서션을 통과하지 못하면 그것이 곧 변이 검증이다.
  → **닫혔다**(18-C/5). 복제가 통과했고, 이식 중 실제로 조건을 좁힌 건이 하나 잡혔다(아래).

---

# S2 (2026-08-05) — 18-C 완료

## Commit

| 해시 | 내용 |
|---|---|
| `afc75890` | 18-C/3 — `ModifierApplySystem`(#9, P2 Intake) |
| `aeea0561` | 18-C/4 — #29 `StatModifierTick` · #30 `Aggregate` (+ `SimWorld.DeltaTime`) |
| `6b6171d7` | 18-C/5 — #28 `FatigueAccrual` · #31 `MaxHealthScale` (+ `Sim/Lib/Units/`) |
| `e7574555` | 18-C/6 — #32 `StackModifierTick` + `ModifierCluster` 조립 |
| `95a6075c` | 18-C/7 — 성능 프로브 (중단 기준 ④) |

## Implemented

**18-C 6시스템 전부 이식 완료.** phase 배치는 **P2 에 하나(#9), P7 에 다섯(#28·#29·#30·#31·#32)**
이고 `ModifierCluster.Register` 한 곳에 모여 있다 — 6곳에 흩어지면 캡처 순서가 다시 확인할 수
없는 계약이 된다.

시스템 표현은 **채널을 생성자로 받는 인스턴스 클래스 + `Run(SimWorld)`** 로 정착했다.
`SimTick.Register` 의 `Action<SimWorld>` 에 그대로 꽂힌다. 18-D 이후도 이 모양을 따르면 된다.

## Notes — 되돌리면 안 되는 의도 (18-C 분)

- **`StatModifierTick` 의 `HasComponent<ModifierStatsDirty>` 가드는 떼는 것이 보존이다.**
  그 가드는 구 sim 3상태 표현(부재/존재+비활성/존재+활성)의 산물이라 슬롯 보유 엔티티에겐
  **항상 참**이었다. 2상태(존재=dirty)로 접힌 신 sim 에 그대로 옮기면 집계가 마커를 제거한
  뒤 영영 거짓이 되어 **집계가 안 깨어난다**. 접힘으로 도달 불가능해진 구 상태 1건은 코드
  주석에 명시했다(프로덕션에선 `ApplyStat` 이 항상 MarkDirty 를 부르므로 만들어지지 않는다).
- **스탯/스택의 비대칭 3종**: 병합 키 4축 vs 2축 · refresh 시 `max(old,new)` vs 덮어쓰기 ·
  MarkDirty 함 vs 안 함. 셋 다 계약이다.
- **스택 병합의 cap 은 슬롯의 `maxStack`** 을 쓴다(이벤트 값이 아니다). `stackId` ·
  `lastTriggeredStack` 도 슬롯 것을 유지 — 엣지 캐시를 리셋하면 임계가 매 부착마다 재발화한다.
- **버퍼 신설을 지연시키지 않는다.** 구 sim 이 ECB 대신 EntityManager 로 즉시 만든 이유가
  "같은 드레인의 두 번째 이벤트가 첫 슬롯을 덮어쓴다" 였다. 신 sim 에선 구조적으로 사라지지만
  분기 모양을 보존했고 회귀 핀을 새로 붙였다(구 sim 에 없던 테스트).
- **`RemoveAtSwapBack` + 역순 순회**를 두 틱 시스템 모두 유지. 안정 제거로 바꾸면 슬롯 순서가
  달라지고 집계의 곱셈 누적 순서가 바뀌어 부동소수 마지막 비트가 갈린다.

## Notes — 이식 중 잡은 결함 1건 (같은 실수를 반복하지 않기 위해)

**`StackModifierTick` 의 쿼리 축을 좁혔다가 잡았다.** 구 쿼리는
`SystemAPI.Query<DynamicBuffer<StackModifierSlot>>()` 로 **버퍼만** 보는데,
바로 앞에서 옮긴 `StatModifierTick`(`RefRO<ModifierStats>` + `WithAll<StatModifierSlot>`)의
모양을 따라 `With<ModifierStats>()` 로 썼다. 스탯 캐시 없는 대상이 통째로 빠지는 회귀였다.

교훈 두 개: ① **인접 시스템의 쿼리 모양을 복사하지 말 것** — 컴포넌트 쿼리와 버퍼 쿼리는
다른 축이다. ② 18-A 의 `SimWorld` 에는 그 차이를 표현할 수단이 **없었다**(`With<T>` 만 있었다).
표현 수단이 없으면 이식은 조용히 조건을 바꾼다 — `WithBuffer<T>()` 를 신설했다.

## Notes — 18-A 표면을 넓힌 2건 (재설계 아님)

중단 기준 ③("18-C 가 18-A 의 저장소/채널 표현을 바꾸도록 강제하면 재설계") 판정: **해당 없음**.
둘 다 저장소·채널 **표현**이 아니라 없던 표면이다.

1. **`SimWorld.DeltaTime` + `SimTick.Run(world, dt)`** — 18-A 는 시스템이 없어 시간이 필요 없었다.
   구 sim 이 `SystemAPI.Time`(=`World.Time`)을 읽던 배치 그대로 두고, writer 는 `SimTick` 하나.
   `Run` 에 기본값 dt 를 두지 않았다(18-A/4 가 `SimConfig` 에서 거부한 "조용한 no-op" 과 같은 패턴).
2. **`SimWorld.WithBuffer<T>()`** — 위 결함의 처방.

`SimConfig.StackThreshold` 자리표시자는 **채웠다**(`StackThresholdRule`). 18-A/4 가 "내용은
조각이 채운다" 고 위임한 대로다. int 인코딩이었던 이유("Battle enum 은 여기 못 온다")는
유효하지만 이제 sim 자신의 enum 이 있어 우회가 필요 없다.

## Notes — 18-D 가 물려받는 것

`CcEffect`·`DotEffect`·`EnemyCcEvent`·`DotApplyEvent`·`DotElementMap` 을 18-C 가 **먼저 열었다** —
`StackModifierTick` 이 생산자라서다. 18-D 는 **소비자**(적용·병합·감쇠)를 가져오고,
duration 병합의 비대칭도 거기 소유다. 필드가 모자라면 18-D 가 넓힌다.

## Verified

- 전체 EditMode **2087 통과 / 실패 0 / skip 1**. 누적이 정확히 맞는다:
  2014(S1) + 13(18-C/2) + 14(/3) + 11(/4) + 19(/5) + 15(/6) + 1(/7).
- **오라클 복제 초록** — 구 `ModifierFrameworkTests` 의 Test 1·2·3·5 + vsCc 2 + clamp +
  additive 합산 + 파괴 대상 2건, 그리고 18-C/2 특성화 13건 전부.
- **중단 기준 ④ 통과** — 프로브 실측 구 49.26 µs/tick vs 신 25.71 µs/tick = **×0.52**.
  자릿수가 갈리지 않았다. ⚠ **이 숫자로 unit 20 을 예단하지 말 것** — 에디터·x64·Mono 이고
  진짜 게이트는 ARM64 IL2CPP p95/p99 다.
- I1 유지 — 18-C 전 커밋에서 `Scripts/Battle/**`·`Scripts/Bridge/**` 수정 **0**.

## Follow-up

- **다음은 18-D(CC/DoT) + I2 검출기.** 계획서 S3.
- **18-B 는 조각에서 삭제됐다**(계획서 rev 3). "미루기" 가 아니라 **조각이 아니었다** —
  게이트는 독립 관측면이 없어서(유일한 관측면 = "그 시스템이 일을 했나") ①스왑될 코드도
  ②증거도 못 만든다. 53건은 전수 분류해 A(채널 싱글턴 14, 증발) · B(기믹 config 7, 시스템별) ·
  C(월드 싱글턴 13, 18-K) · D(일감 존재 19, 시스템별)로 배정했다. 같은 잣대로 **26쌍 진리표**와
  **전역 증인 명세표**도 철회했다 — 셋 다 "따로 검증할 수 없는 것" 을 "검증할 수 없는 것" 으로
  오독한 결과다.
- **18-C 가 증발시킨 분류 A 게이트 6건**(`ModifierApply` 2 · `StackModifierTick` 3 ·
  `FatigueAccrual` 1)을 커밋에 적지 않았다. 계획서 장부에 기록했고, 남은 A 8건은 각 조각이 적는다.
- **틱당 관리 할당량은 아직 모른다.** 에디터 Mono 에서
  `GC.GetAllocatedBytesForCurrentThread` 가 **동작하지 않는다**(1 MB 대조 할당에 delta=0).
  프로브는 이제 계측 불가를 통과로 위장하지 않고 명시적으로 비운다. unit 20 의 기기
  프로파일이 답할 항목이다.
- **네이밍 규칙 미결(사용자 판단 보류 2026-08-05)** — `Sim` 접두사가 일관되지 않다(아래 S3 이후에도 유효).
  `Sim/Lib/Math/` 안에서 `SimMath` 와 `ScoreMath` 가 나란히 있고, 도메인 타입인
  `SimModifierMath`·`SimModifierAuthoring` 만 접두사를 달고 있다(`ModifierStats`·
  `StatModifierSlot` 은 안 달았다). 성립하는 규칙은 "**엔진/BCL 타입 대체분에만 `Sim`**"
  이고 어긋난 것은 그 둘뿐이다. 사용자 판단은 *"나중에 바뀌는 거라면 일단 진행"* —
  **unit 20 스왑(구 sim 삭제 = 접두사의 존재 이유 소멸) 시점에 함께 정리**하는 것이
  자연스럽다. 그때 정리하지 않으면 `Sim*` 이 영구히 남는다.

---

# S3 (2026-08-05) — 18-D + I2 검출기

## Commit

| 해시 | 내용 |
|---|---|
| `5efc42fb` | 계획 rev 3 — 목적 기준 재도출(18-B·26쌍·전역 증인표 철회, I2 배정) |
| `f03f05d3` | 18-D — CC/DoT 이식 + I2 검출기 |

## Implemented

- 시스템 4(#10 `CcApply` · #15 `DotApply` · #37 `CcClear` · #40 `CcDecay`) + 병합 정책 2 +
  `DotTick` · `CcActionLock`. `Sim/Lib/Combat/`(BossTag) · `Units/IncomingDamage` 개설.
- **I2 검출기 신설** — 프로덕션이 그림자 맥락(`Sim/Lib/{Units,Movement,Combat,Effects}`)을
  참조하면 빨간불. 검출기 자신이 조용한 no-op 이 되지 않도록 스캔 대상 실재도 함께 잰다.

## Notes — 구조가 두 번 바뀌었다 (되돌리지 말 것)

**둘 다 캡처 표를 읽다가 드러났고, 둘 다 "표현 수단이 없으면 이식이 조용히 달라진다" 의 사례다.**

1. **등록 → 신고.** `CcApply` 가 P3 인 줄 알았는데 캡처는 **#10 = P2** 였고, 그러자
   **한 phase 에 여러 클러스터가 교차**한다는 사실이 드러났다(P2 = #8/18-F + #9/18-C + #10/18-D).
   클러스터 단위 `Register(tick)` 으로는 표현이 안 되고, 표현이 안 되면 **조각을 얹는 순서가
   실행 순서를 바꾼다.** ⇒ 클러스터는 `Steps()` 로 신고하고 `SimPipeline` 이 캡처 번호로
   정렬한다. 순서의 정본이 캡처 표 하나로 돌아왔다.
2. **채널 소유 이관.** 18-C 는 `ModifierCluster` 가 `EnemyCc`·`DotApply` 를 들고 있었는데
   18-D 가 그 **소비자**다 — 생산자 소유는 성립하지 않는다. ⇒ `SimChannels` 신설.
   계획서가 "27채널 소유는 18-K" 라 한 자리를 미리 연 것이고, 18-K 는 채우기만 하면 된다.

**남은 조각은 `Steps()` 로 신고하고 채널은 `SimChannels` 에서 받는다** — 이 두 모양을 따르면 된다.

## Notes — 보존한 CC/DoT 비대칭

- **CC 는 버퍼가 없으면 만들고 DoT 는 건너뛴다**(구 sim 실존 비대칭). CcApply 는 `HasBuffer`
  없이 `GetBuffer` 직행이라 부재면 던졌다 — 신 sim 은 생성으로 흡수(성공 경로 동일, 없던
  크래시를 안 만든다). DotApply 의 명시적 스킵은 그대로 보존.
- **DoT 는 `IncomingDamage` 버퍼가 없으면 틱조차 안 한다**(구 job 이 두 버퍼를 모두 요구).
- **지급 정방향 / 만료 제거 역순 별도 패스** · **틱 지급이 remainingTime 차감보다 앞**.
- 로그 채널 유무로 갈리던 **두 job 변형을 하나로 접었다** — 분기 이유가 Burst 였고 피해
  계산은 동일하다. ⚠ `HazardRuntime` 채널은 **드레인 소유자가 아직 없다**(18-K 가 뷰를 잇는다).

## 게이트 장부

분류 A 증발: `CcApply` 1 · `CcClear` 1 = **2건**(누적 8). `DotApply`·`CcDecay` 는
`RequireForUpdate` 없음. DotApply 의 `TryGetSingleton` 2건은 53 목록 밖 soft gate.

## Verified

- 신규 35건 · 전체 EditMode **2122 / 실패 0 / skip 1**. I1 유지(Battle/**·Bridge/** 수정 0).
- 복제 불가 1건: `EffectSpawner_ApplyCc_Uses_Same_Merge_Policy` — 두 번째 호출자가 Bridge 라
  대응물이 없다. 그 테스트가 지키던 "두 경로가 같은 정책" 은 신 sim 에선 **호출자가 하나**라
  구조가 보증한다.

## Follow-up — 남은 규모 (실측)

| 조각 | 시스템 | 몸체 줄 | 특성화 선행 | 비고 |
|---|---:|---:|---:|---|
| 18-E 필드·존·해저드 | 8 | 708 | 4 | `HazardLifetime` 재작성 — **순회 순서 보존**(tie-break ⑥) |
| 18-F 어그로·AI·이동 | 5 | 744 | 0 | P2 의 #8 이 여기 |
| 18-G 피해·실드·사망 | 7 | 877 | 3 | 사망 4단계 릴레이 — 핀 3개 |
| 18-H 투사체 | 3 | 1,081 | 0 | 궤적/페이로드 2축 |
| 18-I1/I2 공격 루프 | 1 | 1,729 | 0 | 단일 파일 최대 · 동률 tie-break |
| 18-J 기믹·보스·임계 | 9 | 1,171 | 6 | `_meteorRng` · 테스트 부채 최다 |
| **T1 합계** | **33** | **6,310** | **13** | 데이터 타입은 별도 |
| 18-K 통합 | — | — | — | P0/P13 · 분류 C 게이트 13 · 트레이스 emitter · 무장 |
| 18-L Bridge 축출 | — | — | — | 라이브 코드 · **골든 14 Play 세션**으로 판정 |

44 시스템 중 **10 이식 완료**(#9·#10·#15·#28~32·#37·#40).

---

# S4 (2026-08-05) — 18-E (7/8) + 공간 토대

## Commit

| 해시 | 내용 |
|---|---|
| `7876cc3d` | 18-E/1 — 오라클 0 시스템 4개 특성화 선행 |
| `ba61015f` | 18-E/2 — 공간 토대 415줄(`Sim/Lib/Movement/` 개설) |
| `ade2ebc6` | 18-E/3 — #1 LastRun · #2 HazardLifetime · #6 ObstacleLifetime |
| `ae9fc480` | 18-E/4 — #3 AllyBuffField · #7 DefenderField |
| `05ad9181` | 18-E/5 — #5 ZoneApply · #16 PatrolField |
| (이 커밋) | 18-E/6 — `EnvironmentCluster` 조립 + #18 이관 기록 |

## Implemented

**44 시스템 중 17 이식 완료.** 18-E 는 **7/8** 로 닫았다 — #18 `HazardCast` 는 **18-I 로 이관**.

이관 근거: 그 시스템이 `DcTriggerSlot`(25필드 + `Wassup.Data` enum 4개, 쓰기 소유자 =
`AttackSystem`)의 **버퍼 존재**를 본다. 존재 확인 하나 때문에 18-I 의 타입을 추측으로 옮기면
필드 하나만 틀려도 상태 라인이 갈린다(`AttackState` 를 9필드 전부 옮긴 것과 같은 이유).
**누락이 사고가 아니라 결정임을 테스트로 박았다** — `HazardCastIsAbsent_BecauseItMovedTo18I`.

## Notes — 이 세션의 관통 주제: "주석 계약" 이 결함이 되는 자리

네이티브→관리 치환은 **구 sim 이 주석으로만 유지했던 계약**을 실제 결함으로 바꾼다.
구 sim 에서는 `Allocator.Temp`(범프 할당)와 네이티브 컨테이너의 성질이 그 계약을 우연히
지켜줬는데, 관리 코드는 그러지 않는다. 이번에 셋을 만났다:

1. **`HazardCellIndex` 순회 순서** — 구 `NativeParallelMultiHashMap` 은 버킷에 prepend 해서
   **역-삽입순**으로 읽힌다. 관리 `List` 를 그대로 쓰면 뒤집힌다. 계획서는 "순회 순서를
   보존한다" 고만 적었고 **그 순서가 무엇인지는 없었다** — 구 sim 에 특성화를 붙여 **측정**했다.
   처방: 리스트를 노출하지 않고 `Get(cell, index)` 만 준다(`index 0` = 최신). 소비자가 틀릴 수 없다.
2. **`FillAreaMask` 의 자체 소거** — 구 sim 은 "호출자가 0 초기화해 넘긴다" 는 **주석 계약**이었다.
   신 sim 은 버퍼를 재사용하므로(그럴 유인이 실제로 있다) 앞 엔티티의 구역이 남아 뒤 엔티티가
   자기 구역 밖을 walkable 로 본다 = **순찰병이 거점을 벗어난다**. 함수가 스스로 지우게 했다.
3. **`ObstacleSingleton` 순회 부재** — `HashSet` 은 순서가 없다. 현 소비자는 `Contains` 만
   쓰지만, 순회가 필요한 규칙이 생기면 순서 있는 표현으로 바꿔야 한다(주석에 명시).

**다음 조각도 이 렌즈로 볼 것**: 구 sim 이 네이티브 컨테이너/범프 할당의 성질에 기대고 있던
곳은 어디인가. `NativeList` 의 인덱스 안정성 · `NativeQueue` 의 FIFO · chunk 순회 순서.

## Notes — 18-A 계약 정정 1건 (18-E/3)

`SimWorld.Destroy` 의 *"P12 만 부른다"* 는 **`DeadTag` 마킹된 유닛에만** 참이다.
구 sim 에는 P1 에 수명 만료 파괴자가 둘 더 있다(#2 해저드 · #6 장애물 — 릴레이 미참여).
정확한 계약: **`DeadTag` 를 가진 엔티티를 파괴하는 것은 #41 뿐.** 초판대로 읽으면 #2·#6 이
이식 불가로 보인다.

## Notes — 구조 (18-D 가 세운 것을 18-E 가 검증)

`SimPipeline`(캡처 번호 정렬)이 실제로 값을 했다. 18-E 는 P1 을 **독점하지 않는다** —
#4 `BossPeriodicTrigger` 가 18-J 소속으로 같은 phase 에 끼어든다. 클러스터 단위 등록이었다면
조각을 얹는 순서가 실행 순서를 바꿨다. 번호 중복은 조립 시 예외로 막는다(테스트 있음).

## 게이트 장부

분류 A 증발: 없음(18-E 의 게이트는 B 1건 · C 4건 · D 2건). 누적 A 증발 **8건**.
- B: `RedBullGimmickConfig`(#1)
- C: `HazardSingleton`(#2·#5) · `ObstacleSingleton`(#6) · `DefenderFieldSingleton`(#7) ·
  `FlowFieldSingleton`(#5·#16) — **전부 `SimSingleton.TryGet` + early-return 으로 이식**
- D: `AllyBuffField`(#3, 명시 카운트 체크로 대체) · `PatrolAnchor`(#16, 쿼리가 곧 게이트)

루프 밖 부수효과 점검(분류 D 처분 기준): 7시스템 모두 **채널 드레인 없음**(전부 생산자만) ·
**RNG 전진 없음** · 싱글턴 갱신은 자기 소유분(인덱스·집합·필드 배열)뿐. ⇒ 게이트 제거 안전.

## Verified

- 전체 EditMode **2238 / 실패 0 / skip 1**. 누적: 2014(S1) → 2087(S2) → 2122(S3) → 2238(S4).
- **tie-break ⑥ 양쪽 박제** — 생산측(18-E/3, 구 sim 측정) + 소비측(18-E/5).
- I1 유지 — 이 세션 24커밋 전부 `Scripts/Battle/**`·`Scripts/Bridge/**` 수정 **0**.

## Follow-up

**다음은 18-F(어그로·AI·이동, 5시스템 744줄).** 공간 토대가 이미 깔려 있어 새로 읽을 유틸이 적다.
남은 것: F(744) → G(829) → H(1,081) → **I(1,870 — #18 포함)** → J(1,171) → K → L.

- `SimTransform` 의 `Rotation` 질문은 여전히 18-K 몫(18-E/2 주석).
- `Sim` 접두사 정리는 unit 20 에 걸려 있다.
- **PlayMode 16건은 18-K 착수 전에 다시 판단**(rev 3 결정).

---

# S5 (2026-08-05~06) — 18-F 완료 · 18-G 착수

## Commit

| 해시 | 내용 |
|---|---|
| `bfd1de38` | 18-F/1 — 어그로 토대(정책·추격 산식·타입) |
| `3cc8d677` | 18-F/2 — #8 AggroState (+ `SimWorld.RemoveBuffer`) |
| `f14b9e3a` | 18-F/3 — #13 TauntAttackGrant · #14 EnemyAiState |
| `b59b8675` | 18-F/4 — #17 Movement · #44 BlinkApply (18-F 5/5) |
| `b7bbcb89` | 18-G/1 — 오라클 0 시스템 3개 특성화 선행 |

## Implemented

**44 중 22 이식 — 정확히 절반.** 캡처 번호로 확인:
`1 2 3 5 6 7 8 9 10 13 14 15 16 17 28 29 30 31 32 37 40 44`.

18-F 는 5/5 로 닫혔다. 다섯 시스템이 **네 phase**(P2·P3·P3·P4·P12)에 흩어지고
**유일한 인접이 #13 → #14** 인데 그게 계약이다(도발로 부여된 `AttackState.range` 를
FSM 이 같은 프레임에 봐야 `Standoff` 판정이 맞는다).

## Notes — 되돌리면 안 되는 의도 (18-F 분)

- **#8 의 게이트는 OR** (`RequireAnyForUpdate`). AND 로 오번역하면 마지막 가디언 소멸 후
  orphan 해제 패스가 죽어 **적이 영원히 어그로된 채** 남는다. 계획서가 경고한 4건 중 첫 번째.
- **#13 의 게이트도 OR.** `TauntAttackGranted` 만으로도 돌아야 회수(strip)가 산다.
- **#8 의 구조 변경은 전부 지연**이고 그 지연이 관측 가능하다 — Pass 1 의 해제가 예약이라
  Pass 3 에서 그 적은 여전히 `Aggroed` 로 보인다 = **같은 틱 재획득 불가.**
- **#17 의 `locked` 는 AiState 직후에 계산한다.** Chasing/goal/tornado 분기가 flow-step
  **앞에서** continue 하므로, 뒤로 미루면 그 경로들이 잠금을 무시한다.
- **순찰병 골 누수 게이트는 3연쇄다**: 태그가 붙으면 ⑴ 이동 루프 영구 제외 ⑵ #41 의 파괴
  루프가 `AttackUnitTag` 를 요구해 파괴도 안 됨 ⑶ 소환사가 남은 판 내내 재소환 불가.
- **`held` 는 커밋된 상태에서만 나온다** — Pass 2 가 부착 **전**이라 새 부착은 다음 틱 반영.

`SimWorld.RemoveBuffer<T>` 신설(18-A 엔 추가만 있었다). 어그로 해제 시 chase field 를
**비우는 게 아니라 없애야** 한다 — 소비자가 `HasBuffer` 로 분기하므로 빈 버퍼는
"전부 dist 0" 이라는 없는 상태다. "표현 수단이 없으면 이식이 조용히 달라진다" 의 네 번째 사례.

## ⚠ 다음 세션이 먼저 결정할 것 — #34 가 강제하는 어휘 이관

`DamageApplicationSystem`(#34, 384줄)을 읽었다. **18-G 의 진짜 관문은 사망 릴레이가 아니라
드림캐쳐 트리거 어휘다.** #34 는 `DcTriggerSlot` 을 **세 곳**에서 읽는다:

1. `DamagedCounter` 게이트 — `DcTrigger.GatePass`/`Tick` + `DcPayloadKind` 디스패치
2. 실드 파열 페이로드 — `DcTriggerKind.OnShieldBreak`
3. 킬 파생 — `DcTriggerKind.OnKill` × (`SelfTileAoe` 시체폭발 · `SelfStatBuff` devouring)

그리고 `UltimateLeapState`(18-J)도 읽는다(이탈 중 피해 버퍼 **비우고 continue** — 쿼리에서
빼면 착지 프레임에 통째로 터지는 지연 폭탄이 된다).

**18-E 에서 #18 을 18-I 로 미룬 근거가 여기선 반대로 작동한다**: 그때는 소비자가 하나뿐이라
미루는 게 쌌지만, 지금은 **#34(18-G)와 #18·#33(18-I) 둘이 같은 타입을 요구**한다 =
공유 어휘다. 선택지 셋:

| 안 | 내용 | 대가 |
|---|---|---|
| A | `DcTriggerSlot` + `DcTrigger` + Data enum 4개를 **18-G 가 먼저 옮긴다** | 25필드를 18-I 를 보기 전에 확정 |
| B | #34 도 18-I 로 미룬다 | 18-G 에 핵심이 사라져 사망 릴레이를 테스트할 수 없다 |
| C | #34 를 옮기되 드림캐쳐 arm 3개를 비워둔다 | **조용한 누락** — 이 spec 이 가장 경계하는 모양 |

**권고는 A.** 근거: ⑴ 두 조각이 요구하므로 이미 공유 어휘다 ⑵ `AttackState` 를 9필드 전부
옮긴 것과 같은 판단 기준(**부분 이식이 해시를 깨뜨리는가**)을 적용하면 통째로 옮기는 게 맞다
⑶ C 를 고르면 18-K 에서야 빈 arm 이 드러나고 되돌릴 반경이 그때 훨씬 크다.
A 를 고르면 18-I 는 **쓰기**(AttackSystem 의 counter 전진)만 얹으면 된다.

## Verified

- 전체 EditMode **2325 / 실패 0 / skip 1**. 누적: 2014(S1) → 2087 → 2122 → 2238(S4) → 2325(S5).
- I1 유지 — 이 세션 **34커밋 전부** `Scripts/Battle/**`·`Scripts/Bridge/**` 수정 **0**.

## Follow-up

남은 T1: **18-G 5시스템**(#11·#12·#19·#34·#35·#36·#41 중 특성화 완료 3 제외) ·
H(1,081) · I(1,870 — #18 포함) · J(1,171). 그 뒤 18-K · 18-L.
