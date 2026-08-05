# unit 18 세션 인계 — S1 종료 시점

> 2026-08-05 · HEAD `77752f41` · 이 문서는 **지도**다. 계약은 [`m1_unit18_plan.md`](m1_unit18_plan.md),
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
