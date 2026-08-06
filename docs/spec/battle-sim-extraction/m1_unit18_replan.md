# unit 18 — 남은 작업 재정리 (리뷰 반영판)

작성: 2026-08-06 · 근거: [`m1_unit18_review.md`](m1_unit18_review.md) · 기준 시점 33/44 이식

---

## 🚩 다음 세션은 여기서 시작한다

**T1(이식)이 끝났다 — 44/44 (2026-08-06).** 지금 할 일은 **18-K 통합**이고, 그 전에
**작업 규칙이 바뀐다는 것**을 먼저 읽어야 한다.

### ⚠ 18-K 부터 규칙이 다르다

| | T1(18-A~18-J, 지금까지) | **T2(18-K)** |
|---|---|---|
| **I1** | `Scripts/Battle/**`·`Bridge/**` 수정 **0** | **예외 지점** — 그림자 무장이 라이브 코드를 만진다 |
| **증인** | EditMode 오라클(복제) | **골든이 처음으로 진짜 증인** |
| **되돌리기** | 커밋 되돌리면 끝 | 라이브 동작이 걸린다 — 골든 byte diff 로 판정 |

그래서 **첫 커밋 전에 골든 기준선을 먼저 잡는다**(`Golden` 트리거 → `git diff` 가 비는지 확인).
그게 비지 않으면 T1 이 무언가 건드렸다는 뜻이고, 18-K 를 시작하기 전에 그걸 먼저 푼다.

### 18-K 남은 항목

- **P0/P13 흡수** — 커맨드 반입 · 읽기 모델 스탬프 · 도약 드레인
- **분류 C 게이트 13건** — 월드 싱글턴 준비. 기동 순서 문제라 **조립 지점이 소유**한다
- **레거시 키 트레이스 emitter** — ⚠ `typeof(T).FullName` 을 쓰면 `Wassup.Sim.XXX` 가 나오는데
  구 키는 `Unity.Transforms.LocalTransform` 이다. **매핑표를 하드코딩**한다(리플렉션 금지)
- **그림자 A/B 무장** + 게이트 53 계정 감사
- **`SimTransform` 팩토리 강제**(F5) — 스폰 배선이 `FromPosition` 을 안 쓰면 스케일 0
- **동률 예외 목록 재유도**(D3) — ⚠ stale 목록으로 비교기를 짜면 진짜 회귀를 로그로 격하시킨다
- **성능 재측정** — `SimWorld._order` 무한 증가는 판 **길이**에 비례해 짧은 프로브가 못 잡는다

✅ **F6 · D5 는 닫혔다** — `{1..44}` 전수 등록 단정 통과(`SimThresholdAndPeriodicTests`),
#24 는 **이식** 판정(근거는 아래 "18-J 에서 확정된 것").

### 읽을 순서 (3개면 된다)

1. **이 문서** — 무엇을 어떤 순서로. arm 지도가 아래에 있다.
2. [`m1_unit18_handoff.md`](m1_unit18_handoff.md) 의 **"운영 함정"** — 테스트 러너 트리거 4함정 ·
   신규 파일 csproj 등록 · PlayMode 후 `ReimportData`. **이걸 건너뛰면 첫 컴파일에서 막힌다.**
3. 옮길 arm 의 **구 코드 해당 라인** — 그 구간만. 파일 전체를 읽지 않는다.

### 작업 1주기 (arm 하나 = 커밋 하나)

```
구 코드 해당 라인만 읽기 → 이식 → Refresh 트리거 → error CS 확인 → EditMode → 커밋
```

세션이 끊겨도 **arm 경계에서** 끊기게 하는 것이 이 절차의 목적이다. 1,729줄을 한 번에 읽으면
이식 도중 끊기고, 반쯤 옮겨진 공격 루프가 이 spec 에서 되돌리기 가장 비싼 상태다.

### 협상 불가 4가지

| | |
|---|---|
| **I1** | 그림자 커밋은 `Scripts/Battle/**`·`Scripts/Bridge/**` 를 **수정하지 않는다**. 커밋마다 `git diff-tree --no-commit-id --name-only -r HEAD \| grep -c "^Assets/_Project/Scripts/\(Battle\|Bridge\)/"` 가 **0** 이어야 한다 |
| **오라클** | 레거시 테스트가 있으면 **어서션을 복제**한다(재작성 금지). 없으면 구 sim 에 특성화를 **먼저** 쓰고 복제한다 |
| **푸시** | `git push` 는 **매번 사용자 승인**. 커밋은 자율 |
| **스테이징** | 여러 세션이 워크트리를 공유한다 — **경로 명시**로만 `git add`. `.meta` 파일 빠뜨리지 말 것 |

### 지금 상태

**44/44 이식 완료** · EditMode **2797 passed / 0 failed / 1 skipped / 1 inconclusive**.
그 inconclusive 는 **의도된 것**이다 — 이 런타임에서 할당 카운터가 항상 0 이라 프로브가
거짓 통과 대신 판정을 비운다(N2 참조). 초록으로 만들려고 손대지 말 것.

클러스터는 **8개**다: Gimmick · Attack · Modifier · **CcDot** · Environment · Movement · Damage ·
Projectile. ⚠ `CcDotCluster` 는 `ModifierCluster.cs` **안에** 있어서 조립 목록에서 빠뜨리기 쉽다
(실제로 전수 단정 초판이 그랬다).

---

## 결론 먼저

**순서를 바꾸지 않는다. 삽입 2건 + 문서 수정 1건이다.**

> **2026-08-06 개정**: 초판은 N1 에서 *"고정 다항식 `Sin`/`Cos` 를 sim 이 소유"* 를 권고했다.
> **철회했다** — 사용자 반문(*"`Math.Sin` 이 있는데 왜 만드는거냐"*)이 맞다. 이유는 N1 참조.
> N1 은 이제 **만드는 작업이 아니라 측정 보장 작업**이다.

`#33 → 18-J → 18-K → 18-L → 19 → 20` 의 근거는 서버권위 렌즈가 대체할 만한 근거보다 낫다고
판정했다. 특히 **#33 은 새 초월함수 호출처를 만들지 않는다**(구 sim 전체에서 `math.sin`/`sincos`
사용 파일이 정확히 둘이고 `AttackSystem` 은 그중에 없다 — 직접 확인).

바뀌는 것은 **18-J 앞에 수치 계층 결정이 하나 들어간다**는 것뿐이다.

---

## 재정리된 순서

| 순서 | 단위 | 규모 | 새로 들어간 것 |
|---|---|---|---|
| 1 | ~~**18-I/2** #33 `AttackSystem`~~ **완료** | 1,729줄 | **F6** 절반 닫힘(`AttackCluster` = #18 + #33) |
| 2 | ~~**N1** 초월함수 측정 보장~~ **완료** | — | `fea4de0e` + 코퍼스 실측 |
| 3 | ~~**N2** 할당 회귀 차단~~ **완료** | — | `1b6c3f57` · 수치 검증은 unit 20 이관 |
| 4 | ~~**18-J** 기믹·보스·임계·도약~~ **완료** | 10시스템 1,242줄 | #24 = **이식** 판정 |
| 5 | **18-K** 통합 ← **여기** | — | F5·D3 + 성능 재측정 (**F6·D5 는 닫힘**) |
| 6 | **18-L** Bridge 축출 | 합류 4건 | 변경 없음 |
| 7 | **19** 시계·커맨드로그 | — | 변경 없음 |
| 8 | **20** A/B parity·스왑 | — | **D1** 결론 반영 |

---

## N1 — 수치 계층 (**결정 완료 2026-08-06: 만들지 않는다**)

### 결정: `Math.Sin`/`Math.Cos` 를 그대로 유지한다 (안 A)

**사용자 판정** — *"`Math.Sin` 이 있는데 왜 만드는거냐?"* 그 반문이 맞고, 초판의 권고(안 B,
고정 다항식 자체 소유)는 **리뷰어의 "지금이 제일 싸다" 논리를 과대평가한 것**이었다.
기록으로 남긴다 — 같은 오판을 반복하지 않게.

### 왜 안 만드는가 (초판이 계산에 안 넣은 것 둘)

**① 실제로 갈리는지 아직 측정하지 않았다.** *"갈릴 수 있다"*(IEEE-754 가 초월함수에 정확
반올림을 요구하지 않는다 — 참)와 *"이 게임의 입력에서 갈린다"* 는 다른 명제다. 아치 각도와
패턴 각도가 만드는 입력 범위에서 bionic/ucrt/glibc 가 실제로 다른 비트를 내는지는 **측정하면
아는 것**이고, 그 측정은 이미 unit 20 교차 골든으로 계획에 있다. 추측으로 선제 대응할 일이 아니다.

**② 직접 만들면 구 sim 과 확실히 갈린다.** 구 sim 은 이 수학을 Burst 로 돌리고 Burst 는
초월함수를 **자기 구현으로 인트린식화**한다. 자체 다항식을 넣으면 Burst-sin 과 다른 값이 나오고,
**unit 20 A/B parity 의 목적이 "신 sim == 구 sim" 증명인데 일부러 다른 수학을 쓰면 그걸 증명할 수
없다.**

⇒ `Math.Sin` 유지 = Burst-sin 대 libm-sin 비교. **최소한 우연히 같을 가능성이 있다.**
자체 다항식 = 같을 가능성이 0. 후자가 명백히 나쁘다.

### 그래서 지금 할 일은 하나뿐

**unit 20 의 교차 골든이 `sin` 경로를 실제로 밟게 보장한다.** 골든 코퍼스 7종에
`DirectionalLinear` 패턴 발사나 탄도 아치가 없으면 그 게이트가 **F2 를 건드리지 않고 통과**한다.
코퍼스 도달 여부 확인은 싸고 결정을 바꾸므로 **18-J 전에** 한다.

거기서 실제로 갈리면 그때 대응한다(그 시점엔 근거가 측정이지 추측이 아니다).
안 갈리면 만들 이유가 없었던 것이다.

### D1 과의 관계

이 결정은 `README.md:155`(Editor/IL2CPP/**CoreCLR** 교차 골든)를 **철회하지 않는다** —
측정을 포기하는 게 아니라 측정 전에 만들지 않는 것이다. D1 은 여전히 정합성 수정이 필요하다
(unit 20 항목 참조).

### 남는 것: 호출처 제거(구 안 C)는 unit 19 권한

`ArcHeight` 는 자기 주석대로면 이미 뷰 전용(D4)이고 패턴 각도는 방향 벡터로 저작 가능하다.
다만 아치가 sim `Position.y` 에 실려 있어 **골든이 갈리므로** unit 19 밖에서는 손댈 수 없다.
후속 후보로만 남긴다.

### 함께 처리 — `SimMathParityTests` 확장 (F3·D2)

정책과 **독립적으로** 값이 있다. `Unity.Mathematics` 를 링크할 수 있는 창은 **unit 20 에 닫힌다.**

⚠ `Sin`/`Cos` 대조는 EditMode 에선 **libm 대 libm** 이라 지금은 자명하게 통과한다. 그래도 넣는
이유는 **드리프트 감지**다 — 나중에 누가 `SimMath.Sin` 을 다항식으로 바꾸면 그 순간 잡힌다.
`SimVec2`·`CreateFromIndex` 는 그런 단서가 없어 진짜 미검증 표면이다.

미검증 표면:

- `Sin` · `Cos` · `SinCos` · `Radians` · `PI`
- **`SimVec2` 오버로드 전부** — 파일 전체에 `SimVec2` 가 한 번도 안 나온다. `MovementSystem.cs:183`
  의 `NormalizeSafe(SimVec2)` 가 **모든 이동 유닛의 스텝 방향**이다
- `SimRandom.CreateFromIndex`
- `ModifierAuthoring` ↔ `SimModifierAuthoring` **일치 단정**(중복 2벌이 갈리는 것을 막는다)

그리고 세 파일의 게이트 주장(`SimMath.cs:18`·`SimVec.cs:17-18`·`SimRandom.cs:16`)을
**실제 커버리지와 일치**시킨다 — 넓게 주장하고 좁게 덮는 상태를 남기지 않는다.

### 실측 결과 — 골든 코퍼스는 `sin` 경로를 밟는다 ✅ (2026-08-06)

**결론: unit 20 교차 골든이 F2 를 건드리지 않고 통과할 위험은 없다.** 코퍼스 변경 불필요.

근거(저작 링크 추적):

| flightMode | 탄 에셋 | 소유자 | sim 경로 |
|---|---|---|---|
| 1 `BallisticArcToPoint` | `Projectile_ArtilleryShell` | `Defender_Artillery` | `ArcPosition` → **`Sin`** |
| 2 `SkyFall` | `MachineGunBullet`·`ShotgunPellet` | 방어유닛 2 + 패턴 2 | (아치 없음) |
| 3 **`DirectionalLinear`** | `Projectile_NightmareMissile` | `Pattern_NightmareMissile` | `PatternDirection.Resolve` → **`SinCos`** |
| 4 **`GrenadeToCell`** | `Projectile_NightmareBarrage` | `Pattern_NightmareBarrage` | `ArcPosition` → **`Sin`** |

- 골든 덱 `Deck_Serpent` 에 **`Enemy_Boss_Nightmare` 포함** — 그 보스의 패턴 2종이 정확히
  위 3·4번이다.
- 투사체 생존 틱: `boss_wave` 27 · `dreamcatcher_heavy` 68 · `forced_wave` 29 ·
  `multi_goal` 17 · `normal` 22 (`restart`·`simultaneous_death` 는 0).

⚠ **이건 저작 링크 추론이지 실행 계측이 아니다.** 보스가 실제로 그 패턴을 발사하는 틱이
코퍼스 안에 있는지는 **18-K 트레이스 emitter 작업 중 한 줄로 확정**할 수 있다 — 그때 확인한다.
(`ProjectileState` 는 `stateHash` 에 들어가므로 골든 JSON 에 타입명이 리터럴로 안 보인다.)

### 완료 기준 ✅ (`fea4de0e` + 코퍼스 실측)
- `SimMathParityTests` 가 `Sin`·`Cos`·`SinCos`·`Radians`·`PI`·**`SimVec2` 전부**·
  `CreateFromIndex`·`ModifierAuthoring` 일치를 덮는다
- 세 파일의 게이트 주장(`SimMath.cs:18`·`SimVec.cs:17-18`·`SimRandom.cs:16`)이 실제 커버리지와
  일치한다 — 넓게 주장하고 좁게 덮는 상태를 남기지 않는다

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

### 완료 기준 ✅ (`fea4de0e` + 코퍼스 실측)
`ProjectileCluster`·`DamageCluster` 가 `perTickBytes < 1024` 를 통과. 통과 못 하면 수치를
근거와 함께 조정하되, **왜 그 수치인지**를 남긴다.

---

## 18-I/2 — #33 `AttackSystem` ✅ 완료 (2026-08-06)

**1,729줄 단일 파일.** 한 번에 읽으면 이식 도중 끊기고, 반쯤 옮겨진 공격 루프가 이 spec 에서
되돌리기 가장 비싼 상태다. **arm 단위**(타겟팅 / 출력 해결 / 발사 / 드림캐쳐 / 캐스트 드레인)로
잘라 읽고, arm 마다 컴파일 + 테스트를 돌린다.

이미 sim 에 있어 **다시 옮기면 안 되는** 어휘: `AttackState` · `AttackOutput`/`AttackOutputElement` ·
`DcTriggerSlot`/`DcTrigger` · `NextAttackDoubleFire` · `PatternSlot`/`EmitterInstance`/`EmitterTick`/
`PatternLogic` · `ThreatEntry`/`ThreatTable` · `ProjectileSpawnRequest`/`ProjectileRequestCarrier` ·
`CastEvent`/`UnitAttackVisualEvent` · `EnemyAiState` · `AggroPolicy` · `TileAoe` ·
`CcEffect`/`EnemyCcEvent` · `StatModifierApplyEvent`/`StackModifierApplyEvent`.

**추가 완료 기준(F6)**: 공격 클러스터가 `SimStep(18, SimPhase.PostMoveCast, ...)` 를 **포함**한다.

### 진행 상황 — 2/N 완료 (`29953628` · `5f0216d4`)

**어휘 토대 이식 끝.** 실측 결과 새로 옮길 타입은 **6개뿐**이었다(위 "다시 옮기면 안 되는" 14종
덕분). 이제 남은 것은 **본체 arm 뿐이고 전부 이미 있는 어휘 위에 선다.**

| 옮긴 것 | 되돌리면 안 되는 것 |
|---|---|
| `BombLauncherState` | `rng` 가 **상태 해시에 실린다** — 캐스터별 독립 스트림 |
| `DcAttackModSlot`·`DcAttackModKind` | 카운터 **없다**(상시 적용) — `DcTriggerSlot` 과 다르다 |
| `DefenderCcData` | `sleepOnHitSec`(주 타겟 1) ≠ `knockupOnHitSec`(전 대상) — 합치면 깨진다 |
| `DeployedFacing` | 활성화 시 1회 쓰기, 이후 불변 |
| `FrontmostAttackLock` | **strict lapse** — RESOLVE 에서 잠금이 무효면 재선택 없이 불발 |
| `SummonerState` | 자체 쿨다운 없음 · `hasSummonedOnce` writer 는 **실제 생성 시점** 하나 |
| `NearestTargeting` | 반경 필터가 **함수 안**(계약이 호출처마다 갈리면 안 된다) |
| `AttackMath` | `DistanceSqToTarget` 은 다중 셀 대상에서 **최근접 점유 셀** |

**arm B(`5f0216d4`)가 세운 것** — 되돌리면 안 되는 것:

| | |
|---|---|
| `AttackSystem.CastCountedHosts` | **arm E 로 가는 seam**. 캐스트로 센 host 는 RESOLVE 카운팅을 건너뛴다(계약 2). 프레임 로컬 — `Run` 선두에서 비운다 |
| host 표시 시점 | 발동 슬롯이 **하나도 없어도** 표시한다. 슬롯 루프의 성과가 아니라 **사건**이 기준이다 |
| 게이트 미평가 | 이 자리는 `DcTrigger.GatePass` 를 **보지 않는다**(처형타는 대상이 있는 RESOLVE 전용) |
| `RequireForUpdate<AttackState>` | **증발이 아니라 이사**(`HazardCastSystem` 선례) — 공격자 0 이면 캐스트 채널도 안 비는 것이 구 sim 동작이다 |
| 그리드 폴백 128×128 | 필드 없는 프레임의 셀 계산이 여기 걸린다 — 구 sim 값 |
| `PickFallbackTarget` 진영 | **Enemy 고정**. 호출처가 전부 defender 게이트 안이라 성립 — 적이 니들을 쏘는 날 아군 오사가 된다 |
| `AttackCluster` | #18(P5) → #33(P8) 이 **같은 틱**임을 파이프라인이 강제한다(구 `[UpdateBefore]` 자리) |

새 채널 2개(`DcTriggerFired`·`AttackOutputLog`)와 `SimWarningCode.CastEventUnhandledPayload` 는
**상태 해시에 실리지 않는다** — 비거나 넘쳐도 A/B 는 갈리지 않아야 한다.

### 남은 arm 지도 (구 `AttackSystem.cs` 라인 기준)

한 번에 읽지 말 것. **arm 하나 = 읽기 → 이식 → 컴파일 → 테스트 → 커밋** 1주기다.

**전 arm 완료(2026-08-06).** 실제 이식 순서와 커밋:

| # | 구간 | 줄 | 내용 | 커밋 |
|---|---|---|---|---|
| A | 25–171 | 147 | 선두 lookup·스냅샷 — **대부분 증발**(아래) | (B·C/1 에 흡수) |
| B | 172–228 | 57 | 캐스트 사건 드레인 | `5f0216d4` |
| C/1 | 233–357 | 125 | 루프 골격(쿨다운·action-lock) + 폭탄맨 | `b230ec91` |
| C/2 | 359–434 | 76 | 소환사 | `0af9f06f` |
| C/3+D | 436–798 | 363 | 타겟팅 스캔 + START | `08df9713` |
| E | 799–1103 | 305 | RESOLVE(투사체) + 에필로그 | `94131f69` |
| F | 1104–1589 | 486 | 근접·Outputs·CC·카드 카운트 | `a293db5e` |
| G | 1618–1664 | 47 | `SpawnNeedleCarrier` | (B 가 호출처라 함께) |

### 초판이 틀렸던 것 셋 (기록)

**① 권장 순서 `B → D → E → C → F → A` 는 코드 의존과 반대였다.** arm D(START)·E(RESOLVE)는
arm C 가 만드는 로컬(`bestTarget`·`hasFacing`·`frontmostMul`…)을 읽는다 — C 없이는 옮길 대상이
없다. 실제로는 C 를 셋으로 쪼개 **자기완결 분기부터** 갔다(C/1 폭탄맨 · C/2 소환사 둘 다
`continue` 로 끝나 경계가 뚜렷하다). C/3 과 D 는 **한 커밋**이어야 했다 — C/3 단독은 관측점이
없고(고른 대상을 아무도 소비하지 않는다) START 가 붙어야 `UnitAttackVisualEvent` 로 관측된다.

**② `PickFallbackTarget` 은 "이미 옮긴 순수 헬퍼 3종" 에 없었다.** 옮겨져 있던 것은 랭킹
(`NearestTargeting.SelectNearest`)뿐이고 후보 조립(진영 마스크·자기 제외·그리드 변환·`PastGoal`)은
arm B 가 처음 옮겼다.

**③ arm A 는 147줄이 아니다.** 대부분(lookup 27종 hoist)이 신 sim 에서 `world.Has/Get` 직접
호출로 **증발한다**. 남는 ① 후보 스냅샷 ② 그리드 폴백은 arm B 가 구 쿼리 조건 그대로 세웠고
③ 채널 writer 는 채널이 항상 존재해 사라진다. ⇒ **arm A 는 별도 작업 없이 끝났다.**

### 이식 중 발견 2건

- **`math.hash(int2)` 상수를 기억으로 적으면 안 된다.** 초판이 다른 타입 상수를 적었고
  `SimMathParityTests` 가 잡았다(실제: `0x83B58237/0x833E3E29/0xA9D919BF`, `int2.gen.cs:947`).
  이 값이 랜덤 패턴의 각도·간격을 정하고 그것이 골든에 실린다.
- **RESOLVE 에필로그(잠금 해제·기준축 리셋)는 arm E 에 넣어야 한다.** replan 은 `1590-1610` 을
  F 구간에 뒀지만, 본문의 어느 분기로 끝나든 지나야 하고 빠지면 잠금이 영구 활성으로 남는다.

### 남긴 것 (죽은 코드를 옮기지 않았다)

`FrontmostTargeting.SelectFrontmost` / `LowestHealthTargeting.SelectLowest` 는 **구 sim 에서도
호출처가 테스트뿐**이었다(프로덕션은 후보 배열을 만들지 않고 running-best 로 `RanksBefore` 만
쓴다). 신 sim 에 들이는 것은 제약 8 위반이고, 그 함수가 하던 도달 불가 필터는 호출부에 이미 있다.

**주의**: `BombLauncherState.rng` write-back 이 상태 해시에 실린다 — xorshift 상수 하나만 달라도
parity 가 조용히 깨진다. `counter` 쓰기 단일 소유(RESOLVE / 폭탄 훅 / 캐스트 드레인 중 정확히 1곳)
계약도 유지.

---

## 18-J — 기믹·보스·임계·도약 ✅ 완료 (2026-08-06)

실측 10시스템/1,242줄(계획서의 9/1,171 은 #24 를 빼고 센 값이었다). 네 조각으로 옮겼다:

| 조각 | 시스템 | 커밋 |
|---|---|---|
| 18-J/1 | #20 사직서 임계 · #24 피격 플래시 · #25 캐리어 수명 | `22c6ffba` |
| 18-J/2 | #21 온천 열기 · #22·#23 레드불 픽업 | `30514757` |
| 18-J/3 | #39 호접몽 · #43 궁극기 도약 | `53616442` |
| 18-J/4 | #42 체력 임계 · #4 주기 트리거 | `a445cbef` |

### 18-J 에서 확정된 것

**#24 살베지(D5) = 이식.** 청사진 P5 는 *"`Scale` 은 상태 해시의 제외 축"* 이라고 적었지만
**실제 기록기가 그렇지 않다** — `BattleBridge.LegacyTrace` 는 `LocalTransform` 을 통째로 남기고
직렬화가 **public 필드 전수 리플렉션**이라 `Scale` 이 상태 라인에 들어간다.
골든을 만드는 것은 기록기이므로 **기록기가 정본**이고, 뷰로 밀면 A/B parity 에서 스케일이 갈린다.
⚠ 이 정정은 청사진 P5 의 다른 "제외 축" 주장에도 같은 의심을 걸어야 한다는 뜻이다 —
**18-K 의 D3(동률 예외 목록 재유도)를 할 때 제외 목록도 기록기에서 다시 뽑는다.**

**#4 는 P1 이라 `GimmickCluster` 가 신고하고 정렬은 `SimPipeline` 이 한다** — `EnvironmentCluster`
에 직접 넣으면 그 클러스터의 phase 경계가 무너진다.

`_meteorRng` 는 상태 해시에 실린다(`BattleBridge.LegacyTrace.cs:246`) — 18-K 의 배선이 진다.

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
