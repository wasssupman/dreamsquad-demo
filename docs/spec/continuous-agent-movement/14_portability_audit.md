# unit 14 — 이식성 감사 (주장이 아니라 컴파일로)

## 목적

README 의 「이식성의 정확한 범위」가 **지금 코드에서 실제로 참인지** 확인한다. 그 절은 이렇게 주장한다:

> "수정 없이 이식된다"고 주장하지 않는다. 이 spec 의 함수는 `NativeArray` / `Unity.Mathematics` 를 쓴다. 의무는 그보다 좁다 — **sim 이 이미 쓰는 어휘를 벗어나지 않는 것.** 즉 `EntityManager` / `SystemAPI` / `MonoBehaviour` / `Time` 을 계산 안으로 들이지 않는다.

주장은 검증이 아니다. **Unity 없이 실제로 컴파일하고 테스트를 돌려** 확인했다.

## 방법

`dotnet 9` 콘솔 프로젝트에 (1) 순수 후보 9파일을 **한 글자도 고치지 않고** 복사, (2) `Unity.Mathematics` / `Unity.Collections` / `Unity.Burst` 최소 shim, (3) 기존 EditMode 테스트 7파일을 **그대로** + 최소 NUnit shim + 리플렉션 러너. Unity 어셈블리 참조 0.

핵심은 **shim 이 곧 측정치**라는 점이다 — shim 에 들어간 것이 포팅 시 갈아끼워야 할 어휘의 전부다.

## 결과

### 1. 순수 로직 984줄이 Unity 없이 컴파일된다

| 파일 | 줄 | Unity 없이 컴파일 |
|---|---|---|
| `GridMath.cs` | 65 | ✅ (`[BurstCompile]` 은 no-op 속성으로 충분) |
| `NavGrid.cs` | 76 | ✅ |
| `AgentCollision.cs` | 148 | ✅ |
| `PathSmoothing.cs` | 234 | ✅ |
| `Separation.cs` | 94 | ✅ |
| `FlowRecovery.cs` | 27 | ✅ |
| `SpawnSpread.cs` | 51 | ✅ |
| `MovementCellTrim.cs` | 88 | ⚠️ **분리 필요** (아래 3) |
| `FlowFieldBuilder.cs` | 201 | ⚠️ **1줄 수정** (아래 2) |

첫 컴파일 에러는 **8건, 정확히 2파일**에서만 났다. 나머지 7파일은 컨테이너/수학 shim 만으로 그대로 섰다.

### 2. 엔진 호출은 전체에서 **딱 1줄**

`FlowFieldBuilder.cs:167` 의 `UnityEngine.Debug.Assert` 하나. `System.Diagnostics.Debug.Assert` 로 치환하면 끝이며 의미도 동일하다.

984줄 중 1줄. 이것이 "계산 안으로 엔진을 들이지 않았다"의 실측값이다.

### 3. `MovementCellTrim` 은 순수/ECS 가 섞인 유일한 파일

세 메서드가 ECS 컴포넌트(`FlowFieldSingleton` · `ObstacleSingleton`)를 시그니처에 받는다:

- `BuildNavGrid(in FlowFieldSingleton, bool, in ObstacleSingleton)`
- `FillWalkMask(in FlowFieldSingleton, bool, in ObstacleSingleton, NativeArray<byte>)`
- `Apply(float3, int2, in FlowFieldSingleton, bool, in ObstacleSingleton)`

**이건 결함이 아니라 설계다** — 이 셋은 "ECS 싱글턴 둘 → `NavGrid` 프레임 뷰 조립"이라는 *어댑터*이고, README 가 "조립은 호출자 책임"이라고 명시한 지점이다. 다만 파일이 어댑터와 순수 계산을 한 타입에 담고 있어서 **포팅 시 잘라야 한다**: 21줄(88 → 67)을 떼면 나머지(`ClampToBoundary` · `ClampDisplacement` · `Apply(NavGrid)`)는 그대로 선다.

### 4. 테스트 95개 중 **94개 통과 · 실패 0** (Unity 밖)

```
AgentCollisionTests     pass 15   FlowFieldBuilderTests  pass 14
FlowRecoveryTests       pass  4   GridMathTests          pass 16
PathSmoothingTests      pass 16   SeparationTests        pass 13 (+1 ignored)
SpawnSpreadTests        pass 16
TOTAL  pass 94  fail 0  ignored 1
```

테스트 파일 1,582줄을 **수정 없이** 그대로 돌렸다. 1건의 ignored 는 Unity 안에서도 ignored 인 float 결합법칙 테스트(`Accumulation_OfThreeOrMore_IsOrderIndependent`)다.

컴파일만이 아니라 **거동이 같다**는 뜻이다. 조준·충돌·평활화·분리·필드 생성의 기대값이 엔진 밖에서 동일하게 재현된다.

### 5. 포팅 비용 = shim 169줄

| shim 대상 | 내용 |
|---|---|
| `Unity.Mathematics` | `float2` · `float3` · `int2` · `bool2` + `math` 17개 함수(`abs` `ceil` `clamp` `cmax` `distancesq` `dot` `floor` `lengthsq` `max` `min` `normalizesafe` `round` `saturate` `sign` `sqrt` `all` `asuint`) |
| `Unity.Collections` | `NativeArray<T>` · `NativeHashSet<T>` · `NativeList<T>` · `NativeQueue<T>` · `Allocator` — 전부 `T[]`/`HashSet`/`List`/`Queue` 위의 얇은 껍데기 |
| `Unity.Burst` | `[BurstCompile]` · `[BurstDiscard]` — **no-op 속성 2개** |

`Unity.Mathematics` 는 실제로는 shim 이 아니라 **패키지째 가져갈 수 있다**(MIT, 엔진 비의존). 그러면 남는 건 컨테이너 어휘뿐이다.

## 판정

| 질문 | 답 |
|---|---|
| 다른 프로젝트에서 재사용 가능한가? | **가능하다.** 984줄 중 실제 수정은 1줄 + 21줄 분리 |
| Unity 전용인가? | 아니다. `Unity.Mathematics` 는 엔진 비의존 패키지고, 컨테이너는 표준 자료구조로 1:1 치환된다 |
| ECS 전용인가? | 아니다. **`EntityManager`/`SystemAPI`/`MonoBehaviour`/`Time` 참조 0** |
| Burst 가 필수인가? | 아니다. 속성 2개를 no-op 로 두면 된다(성능은 잃는다) |
| 거동이 보존되나? | **테스트 94개로 확인** |

README 의 주장은 **참이었다.** 다만 두 군데를 정정한다 — ① `FlowFieldBuilder` 에 엔진 호출이 1줄 있었다(있어도 되지만 "0"은 아니었다) ② `MovementCellTrim` 이 어댑터와 순수 계산을 한 타입에 담고 있어 포팅 시 분리가 필요하다.

## 이식되지 **않는** 것 (경계를 정직하게)

| 파일 | 줄 | 왜 |
|---|---|---|
| `MovementSystem.cs` | ~360 | `SystemAPI` 쿼리 · 컴포넌트 룩업 12종 · ECB. **정책과 순서**를 소유하고 계산은 전부 위임한다 |
| `AgentSeparationSystem.cs` | ~145 | 이웃 수집이 엔티티 순회다 — 순수화 불가(README ECS 접점 6) |
| `BlinkApplySystem.cs` | ~130 | 텔레포트 seam |
| `FlowFieldRebuildSystem.cs` | — | 싱글턴 내용 갱신 |
| `SimFieldInstaller` / `BattleBridge` | — | 라이프사이클 소유 |

**비율이 요점이다**: 계산 984줄은 이식되고, 그것을 구동하는 ECS 배선 ~635줄은 재작성 대상이다. 이 경계가 흐려졌다면 계산 쪽에 `SystemAPI` 가 새어 있었을 텐데, 감사 결과 0건이다.

## 재현 방법

이 감사는 리포지토리에 도구를 남기지 않는다(제약 8 — 지금 쓰지 않을 빌드 타깃을 심지 않는다). 다시 하려면:

1. `dotnet new console`, `EnableDefaultCompileItems=false`
2. 위 9파일 + 테스트 7파일 복사
3. shim 3종 작성(169줄 · 위 표가 전부)
4. `MovementCellTrim` 의 ECS 오버로드 3개 제거, `UnityEngine.Debug.Assert` → `System.Diagnostics.Debug.Assert`
5. `[Test]` 를 리플렉션으로 도는 러너 + `Assert` 껍데기

**소요: 1세션.** 이식을 실제로 할 때도 같은 순서면 된다.

## 완료 기준

- [x] 순수 후보 9파일을 Unity 참조 0 으로 컴파일 — 성공(수정 1줄 + 분리 21줄)
- [x] 기존 EditMode 테스트를 수정 없이 Unity 밖에서 실행 — 94/95 통과, 실패 0
- [x] 엔진/ECS 참조 실측 — `EntityManager`/`SystemAPI`/`MonoBehaviour`/`Time` 0건, `UnityEngine` 1건
- [x] README 주장 대조 및 정정 2건 반영

---

**완료 기준 확인**: 2026-08-09 · 감사 실행 · 코드 변경 없음(문서 전용)
