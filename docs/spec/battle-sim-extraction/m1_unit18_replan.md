# unit 18 — 남은 작업 재정리 (리뷰 반영판)

작성: 2026-08-06 · 근거: [`m1_unit18_review.md`](m1_unit18_review.md) · 기준 시점 33/44 이식

## 결론 먼저

**순서를 바꾸지 않는다. 삽입 2건 + 문서 수정 1건이다.**

`#33 → 18-J → 18-K → 18-L → 19 → 20` 의 근거는 서버권위 렌즈가 대체할 만한 근거보다 낫다고
판정했다. 특히 **#33 은 새 초월함수 호출처를 만들지 않는다**(구 sim 전체에서 `math.sin`/`sincos`
사용 파일이 정확히 둘이고 `AttackSystem` 은 그중에 없다 — 직접 확인).

바뀌는 것은 **18-J 앞에 수치 계층 결정이 하나 들어간다**는 것뿐이다.

---

## 재정리된 순서

| 순서 | 단위 | 규모 | 새로 들어간 것 |
|---|---|---|---|
| 1 | **18-I/2** #33 `AttackSystem` | 1,729줄 | **F6** — #18 등록을 완료 기준에 |
| 2 | **N1** 수치 계층 확정 | 파일 1~2 | ⬅ **신설 · 18-J 앞** |
| 3 | **N2** 할당 회귀 차단 | 시스템 6 + 테스트 | ⬅ **신설 · N1 과 같은 패스 가능** |
| 4 | **18-J** 기믹·보스·임계·도약 | 10시스템 1,242줄 | #24 살베지 판정 선행 |
| 5 | **18-K** 통합 | — | **F5·F6·D3·D5** + 성능 재측정 |
| 6 | **18-L** Bridge 축출 | 합류 4건 | 변경 없음 |
| 7 | **19** 시계·커맨드로그 | — | 변경 없음 |
| 8 | **20** A/B parity·스왑 | — | **D1** 결론 반영 |

---

## N1 — 수치 계층 확정 (**18-J 앞**, 신설)

### 왜 여기인가

18-J 는 기믹·보스·임계·**도약**이다. 도약 아치가 새 `sin`/`cos` 호출처가 될 가능성이 가장 높은
자리고, **현재 호출처는 둘뿐이다.** 늘어난 뒤에 결정하는 건 순수하게 더 비싸다.

수치 계층은 **소급이 싼 쪽**이다. 구조적 규율(asmdef 경계·생성 순서 순회·명시 tie-break·
"phase 순서가 지연을 함의한다")은 소급이 비싸서 먼저 값을 치렀다. 이건 반대다 —
그래서 지금이 맞다.

### 결정할 것: 초월함수 정책 (택 1)

| 안 | 내용 | 대가 |
|---|---|---|
| **A** | **libm 을 수용하고 교차 런타임 권위를 포기** | M0~M2 로는 정당. 다만 `README.md:155` 가 그렇게 말해야 한다(D1 과 한 묶음) |
| **B** | **고정 다항식 `Sin`/`Cos` 를 `SimMath` 가 소유** | `SimVec`/`SimRandom` 이 이미 둔 수(手)와 같고 *"sim 이 소유하는 수학"* 과 일관. 구현·검증 비용 |
| **C** | **호출처 2개를 제거** | `ArcHeight` 는 자기 주석대로면 이미 뷰 전용(D4) · 패턴 각도는 방향 벡터로 저작 가능. **골든이 갈린다** — 아치가 sim `Position.y` 에 실려 있으므로 |

⚠ **C 는 골든을 바꾸므로 unit 19 권한이다.** A 와 B 만 이 자리에서 가능하다.

**사용자 결정 필요.** 이건 아키텍처 수준 결정이고 여러 정답이 있다.

### 함께 처리 — `SimMathParityTests` 확장 (F3·D2)

`Unity.Mathematics` 를 링크할 수 있는 창은 **unit 20 에 닫힌다.** 미검증 표면을 그 전에 덮는다:

- `Sin` · `Cos` · `SinCos` · `Radians` · `PI`
- **`SimVec2` 오버로드 전부** — 파일 전체에 `SimVec2` 가 한 번도 안 나온다. `MovementSystem.cs:183`
  의 `NormalizeSafe(SimVec2)` 가 **모든 이동 유닛의 스텝 방향**이다
- `SimRandom.CreateFromIndex`
- `ModifierAuthoring` ↔ `SimModifierAuthoring` **일치 단정**(중복 2벌이 갈리는 것을 막는다)

그리고 세 파일의 게이트 주장(`SimMath.cs:18`·`SimVec.cs:17-18`·`SimRandom.cs:16`)을
**실제 커버리지와 일치**시킨다 — 넓게 주장하고 좁게 덮는 상태를 남기지 않는다.

### 선행 확인 (싸고 결정을 바꾼다)

**골든 코퍼스 7종이 `Sin`/`Cos` 경로를 실제로 밟는가?** `DirectionalLinear` 패턴 발사나
탄도 아치가 코퍼스에 없으면 unit 20 의 교차 골든이 **F2 를 건드리지 않고 통과**한다.
unit 20 중이 아니라 **여기서** 확인한다.

### 완료 기준
- 정책 A/B 중 하나가 문서에 기록되고 코드가 그것을 따른다
- `SimMathParityTests` 가 위 표면을 덮고, 세 파일의 주장이 커버리지와 일치
- 골든 코퍼스의 초월함수 경로 도달 여부가 기록된다

---

## N2 — 할당 회귀 차단 (F4, 신설)

`SimCommandBuffer` 가 op 당 힙 객체 2개를 만든다. 구 sim 은 `Allocator.Temp` 로 **GC 0** 이었다.
호출처가 전부 hot path(착탄마다·투사체 만료마다·발사 1발마다·파괴 6지점)다.

**게이트는 이미 있는데 엉뚱한 데를 겨눈다** — `SimModifierPerfProbeTests:157` 이 돌리는
`ModifierCluster` 는 `SimCommandBuffer` 사용이 **0**(직접 확인).

### 할 것
1. `SimCommandBuffer` 를 클로저 대신 **값 기록**(op 종류 + 타입 핸들 + 페이로드)으로 전환
2. 프로브를 `ProjectileCluster`·`DamageCluster` 로 확장
3. 같이 측정: `AoeTargetCap`/`ShieldTargeting` 의 `new bool[total]`(호출마다) ·
   `SimWorld` 순회 이터레이터 할당 · `FlowFieldBuilder` 큐 할당 · `StepChase` 전체 그리드 복사

⚠ **게이트 확장을 먼저 하면 스위트가 빨개진다** — 같은 작업 단위로 묶는 이유다.

### 완료 기준
`ProjectileCluster`·`DamageCluster` 가 `perTickBytes < 1024` 를 통과. 통과 못 하면 수치를
근거와 함께 조정하되, **왜 그 수치인지**를 남긴다.

---

## 18-I/2 — #33 `AttackSystem` (기존 + F6)

**1,729줄 단일 파일.** 한 번에 읽으면 이식 도중 끊기고, 반쯤 옮겨진 공격 루프가 이 spec 에서
되돌리기 가장 비싼 상태다. **arm 단위**(타겟팅 / 출력 해결 / 발사 / 드림캐쳐 / 캐스트 드레인)로
잘라 읽고, arm 마다 컴파일 + 테스트를 돌린다.

이미 sim 에 있어 **다시 옮기면 안 되는** 어휘: `AttackState` · `AttackOutput`/`AttackOutputElement` ·
`DcTriggerSlot`/`DcTrigger` · `NextAttackDoubleFire` · `PatternSlot`/`EmitterInstance`/`EmitterTick`/
`PatternLogic` · `ThreatEntry`/`ThreatTable` · `ProjectileSpawnRequest`/`ProjectileRequestCarrier` ·
`CastEvent`/`UnitAttackVisualEvent` · `EnemyAiState` · `AggroPolicy` · `TileAoe` ·
`CcEffect`/`EnemyCcEvent` · `StatModifierApplyEvent`/`StackModifierApplyEvent`.

**추가 완료 기준(F6)**: 공격 클러스터가 `SimStep(18, SimPhase.PostMoveCast, ...)` 를 **포함**한다.

**주의**: `BombLauncherState.rng` write-back 이 상태 해시에 실린다 — xorshift 상수 하나만 달라도
parity 가 조용히 깨진다. `counter` 쓰기 단일 소유(RESOLVE / 폭탄 훅 / 캐스트 드레인 중 정확히 1곳)
계약도 유지.

---

## 18-J — 기믹·보스·임계·도약 (10시스템 1,242줄)

**선행 판정**: 계획서는 9시스템/1,171줄인데 실측은 10/1,242다. 가장 유력한 후보는
**#24 `HitFlash`(49줄)** — 파이프라인 문서가 *"뷰성 상태 — salvage 판정 대상"* 으로 표시해 뒀다.
**착수 시 먼저 판정**한다(이식 대상인가 뷰로 미는가). D5 와 같은 축이다.

**주의**: **#4 `BossPeriodicTrigger` 는 P1** 이라 `EnvironmentCluster` 의 phase 에 끼어든다.
직접 넣으면 클러스터 경계가 무너진다 — 별도 클러스터에서 `SimStep(4, SimPhase.FieldsAndPeriodic, ...)`
로 신고하고 `SimPipeline` 이 정렬하게 둔다.

`_meteorRng` 는 상태 해시에 실린다(`BattleBridge.LegacyTrace.cs:246`).

---

## 18-K — 통합 (기존 + F5·F6·D3·D5 + 성능)

기존 범위: P0/P13 · 분류 C 게이트 13 · 트레이스 emitter · 그림자 무장 · 게이트 계정 감사.

### 추가된 것

- **{1..44} 전수 등록 단정 테스트**(F6) — `SimPipeline` 은 번호 중복만 막고 누락은 못 막는다
- **`SimTransform` 팩토리 강제**(F5) — 스폰 배선이 `FromPosition` 을 쓰지 않으면 스케일 0.
  타입이 강제하게 하거나, 최소한 배선의 완료 기준에 넣는다
- **동률 예외 목록 재유도**(D3) — ⚠ **stale 목록으로 비교기를 짜면 진짜 회귀를 로그로 격하시킨다.**
  신 sim 이 이미 닫은 것들(`HazardTypes.cs:58-66` · `HazardCastSystem.cs:96-98`)을 반영해
  **이식된 코드에서** 목록을 다시 뽑는다
- **`HitFlashTag` 살베지 결론**(D5) — 해시 제외 목록을 넓힐지, 뷰로 밀지
- **성능 재측정** — `SimWorld._order` 무한 증가는 판 **길이**에 비례하므로 짧은 EditMode 프로브가
  못 잡는다. 긴 매치 시나리오로 측정
- **트레이스 emitter 의 타입명**: `typeof(T).FullName` 을 쓰면 `Wassup.Sim.XXX` 가 나오고 구 키는
  `Unity.Transforms.LocalTransform` 이다. 18-A/3 의 `LegacyTraceKeyContractTests` 매핑표를
  **하드코딩**해야 한다(리플렉션 금지)

---

## 18-L — Bridge 규칙 축출

변경 없음. **주의 1건 추가**: `_occupiedTiles`(`BattleBridge.cs:229`)의 `Dictionary<Vector2Int, …>`
를 `SimInt2` 키로 바꾸면 **해시가 달라져 같은 키가 다른 버킷**에 들어간다.
`Vector2Int` 와 동일 해시를 구현하거나 변환 시점에 딕셔너리를 재구축한다 — 성능이 아니라
**정확성** 문제다.

---

## unit 20 — D1 결론 반영

`README.md:155` 는 M1 게이트를 **Editor / IL2CPP / CoreCLR** 교차 골든이라 못박는데
`20_ab_parity_swap.md:42` 는 **Editor / Android IL2CPP** 만 적는다.
**CoreCLR = 권위 sim 을 실제로 호스팅할 런타임**이 게이트에서 빠졌다.

한쪽을 고른다:
- `:42` 에 CoreCLR 복원 → M1 이 **서버측** 이식 가능성의 증거를 낸다
- `README.md:155` 개정 → M1 은 **클라측**만 증명한다고 명시

N1 의 정책 선택(A vs B)과 **같은 결정의 두 면**이다 — 함께 정한다.

---

## 문서 위생 (D6)

`m1_unit18_handoff.md` 617줄. CLAUDE.md 규칙은 *"30~80줄, source of truth 가 아니라 다음
에이전트가 커밋과 spec 을 빠르게 찾기 위한 지도"* 다.

인계가 계약을 이중화하기 시작했다 — 이 문서와 [`m1_unit18_review.md`](m1_unit18_review.md) 가
생겼으므로 **인계는 지도로 되돌린다**: 조각별 커밋 표 + 되돌리면 안 되는 것 + 다음 한 걸음.
계약은 번호 문서가, 리뷰 결과는 리뷰 문서가 소유한다.
