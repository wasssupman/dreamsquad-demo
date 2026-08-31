# 전투 시뮬 설계 원칙

## 구조적 결정론 (seeded RNG 보다 index 기반)

전투 시뮬의 비주얼/배치 분산은 RNG(seeded 포함) 대신 **결정론 수열**을 쓴다. 목표 = 구조적 결정성(같은 입력 → byte-identical).

- **Why**: 비동기 토너먼트 리플레이/공정성. 사용자 명시 요구 "랜덤 있으면 안됨".
- **적용**: 분산/지터/선택은 index 기반 결정론으로. 예) 스폰 측면 분산을 `_spawnSpreadRng`(seeded) → `SpawnSpread.LaneFraction`(이산 N-레인 round-robin, 스폰 순번 % N)로 교체.
- **선호**: clever 한 저불일치(golden-ratio)보다 **단순·예측가능한 이산 N-레인 round-robin**(또렷한 N줄 대형 + 디버그 용이). seeded RNG 는 차선.

## 시간 제어는 TimeManager 만 — `Time.timeScale` 금지

시간 스케일 제어는 `Wassup.Core.TimeControl.TimeManager`(의도된 예외 싱글턴, TRD §5.2, 커밋 c2fe03d, spec `docs/spec/time-manager/`)만 담당. 코드에서 `Time.timeScale` 은 **절대 write 안 함(항상 1)**.

- **Why**: 글로벌 `Time.timeScale` 은 너무 blunt — 전투만 멈추고 UI·드래그·카메라는 실시간으로 두려면 도메인 분리 필요.
- **사용**: 정지 = `TimeManager.Instance.Request(TimeDomain.Battle, 0f, priority:100)`, 슬로우 = `Request(Battle, 0.2f)`. 반환 `TimeLease` 를 보관 후 Dispose(멱등)로 해제.
- **전투 도메인 스케일**: ECS 는 `BattleSimGroup` 위 `BattleScaledRateManager`(scale 0=skip, >0=scaled delta). BattleBridge 가 `BattleTimeScale` singleton write + `_battleClock`(unscaledDeltaTime×scale)로 웨이브/타이머 구동.
- **되돌리면 안 되는 것**: `DestroyEcsInfrastructureEntities` 의 `DestroyEntitiesByType<BattleTimeScale>()`(빼면 StopBattle 후 orphan → 시간제어 무력화) · RateManager 로컬 `_elapsedTime` 누산(월드 elapsed 읽으면 정지 후 점프).
- **부작용**: `Time.timeScale=0` 으로는 이제 웨이브/타이머가 안 멈춘다(`_battleClock` 이 unscaledDeltaTime 기반). 검증 목적 완전 동결은 `TimeManager.Request(Battle,0)`. (→ `01-unity-mcp-operation.md` 애니 검증.)

### 함정 — `TeardownCurrentBattle` 안에서 `?.` 를 쓰면 그 뒤가 통째로 죽는다

**증상**: 무관해 보이는 테스트 여러 개가 `HasSingleton<BattleTimeScale>() found 2 instances` 로 무너진다.

**원인**: `TeardownCurrentBattle` 은 `OnDestroy` 에서도 불린다. 그 시점엔 씬의 다른 컴포넌트가 이미
파괴돼 있는데, **C# 의 null 조건 연산자 `?.` 는 Unity 의 fake-null 을 모른다** — 파괴된 UnityEngine.Object
는 `== null` 이 true 지만 C# 참조로는 non-null 이라 `?.` 가 short-circuit 하지 않고
`MissingReferenceException` 을 던진다. 그러면 **그 메서드가 거기서 중단돼 뒤에 있는
`DestroyEntitiesByType<BattleTimeScale>()`(및 나머지 정리)이 실행되지 않는다.** 싱글턴이 살아남고
다음 씬의 BattleBridge 가 하나 더 만들어 2개가 된다.

**규칙**: `TeardownCurrentBattle`/`OnDestroy` 계열에서 UnityEngine.Object 를 부를 땐 반드시
`if (x != null) x.Foo();` (Unity 오버로드 `==` 가 fake-null 을 처리). 그 메서드의 기존 줄들이 전부
그 형태인 것이 우연이 아니다. `?.` 는 **순수 C# 객체에만** 쓴다.

**진단법**: 여러 테스트가 한꺼번에 무너지면 **신규 테스트를 먼저 빼고 돌려본다.** 그래도 실패하면
테스트가 아니라 프로덕션 변경이 원인이다. 그 다음 콘솔에서 **첫 예외**(여기서는 InvalidOperationException
이 아니라 그 앞의 MissingReferenceException)를 찾는다 — 뒤에 쏟아지는 것은 전부 파생이다.

**출처**: defender-clock-out 코드리뷰 반영 중 실측(2026-08-15). 상세는
`docs/spec/defender-clock-out/4_handoff_summary.md`.

## Bursted ISystem 에서 순수 함수를 부를 때 — 함정 둘

전투 심의 순수 계산을 별 asmdef(`Wassup.Skills`)로 빼면 두 번 넘어진다. **증상이 둘 다
「그 함수와 무관해 보이는 대량 실패」**라서 원인에 도달하는 데 시간이 든다.

### ① 대상 asmdef 이 `Unity.Burst` 를 참조하지 않으면 Burst 가 본체를 못 찾는다

```
Burst error BC1055: Unable to resolve the definition of the method
  `Wassup.Skills.SkillMath.InBodyReach(float, float, float, float)`
```

**호출하는 쪽이 아니라 정의된 쪽 asmdef 에 `Unity.Burst` 참조가 필요하다.** 없으면 Burst 가
그 어셈블리를 로드하지 않아 메서드를 해석하지 못한다. `noEngineReferences: true` 는
유지해도 된다 — Burst 는 엔진 어셈블리가 아니라 패키지다.

⚠ **연쇄 증상에 속지 말 것.** BC1055 는 컴파일을 막지 않고, 실패는 **런타임에** 그 시스템이
무너지는 모습으로 나온다. 실측에서는 EditMode 25건 이상이
`ObjectDisposedException: EntityTypeHandle ... invalidated by a structural change` 와
「공격이 0건」으로 동시에 빨개졌다 — 전부 BC1055 하나 때문이었고, 그 직전에 추가한
`ComponentLookup` 이 범인처럼 보였다. **콘솔에서 Burst 에러를 먼저 확인한다.**

### ② `SystemAPI.GetComponentLookup` 지역 변수가 어떤 시스템에서는 NRE 를 낸다

```
NullReferenceException ... compiled with Burst, which has limited exception support
  #3 Wassup.Battle.Effects.HazardCastSystem.OnUpdate
```

초기화 안 된 lookup 포인터다. **같은 파일의 다른 `SystemAPI.GetComponentLookup` 이 멀쩡히
도는 것이 함정** — 「저 형태가 되니까 여기도 되겠지」로 되돌리게 된다. 실측에서 차이는
그 타입이 **같은 시스템의 쿼리 `.WithAll<>` 에도 쓰인다**는 점 하나였지만 **원인은 확정하지
못했다**(재현은 확실하다).

**해법 = Entities 정본 형태.** `SystemAPI.GetComponentLookup` 은 그 위에 얹힌 소스 생성기
설탕이므로, 아래가 축약이 아니라 원형이다:

```csharp
private ComponentLookup<Foo> _fooLookup;                  // 시스템 필드
public void OnCreate(ref SystemState state)
    => _fooLookup = state.GetComponentLookup<Foo>(isReadOnly: true);
public void OnUpdate(ref SystemState state)
{
    _fooLookup.Update(ref state);                          // 소비처보다 앞, early-return 앞
    ...
}
```

출처: `distance-based-range` unit 4a · `HazardCastSystem`(unit 1).
